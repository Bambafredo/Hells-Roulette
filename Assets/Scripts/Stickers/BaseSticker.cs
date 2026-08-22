using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class BaseSticker : MonoBehaviour
{
    [Header("Sticker Config")]
    public StickerEffect effect;
    public Transform wheelCenter;
    public WheelGenerator generator;
    public RouletteController controller;

    [Header("Placement")]
    public bool isPlaced = false;
    public Transform currentSegment;

    [HideInInspector] public BagZone currentBagZone;
    [HideInInspector] public BagZone currentGameplayZone;

    // Estado específico del sistema PC / Album.
    // En la escena mobile puede permanecer siempre null.
    [HideInInspector] public AlbumZone currentAlbumZone;

    [Header("Validation Masks")]
    public LayerMask segmentMask;
    public LayerMask stickerMask;

    [Header("Placement Tuning")]
    [Range(0f, 0.2f)]
    public float tolerance = 0.01f;

    // Se mantiene para no romper datos serializados ni referencias antiguas.
    // La nueva validación ya no utiliza un porcentaje aproximado de cobertura.
    [Range(0.5f, 1f)]
    public float coverageThreshold = 0.75f;

    [Header("Internal")]
    [Tooltip("GO raíz del sticker (prefab). Si no se asigna, se detecta en Awake una vez.")]
    public Transform stickerRoot;

    private Camera cam;
    private bool isDragging = false;
    private Vector3 offset;

    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Transform originalParent;

    // Estado lógico antes de empezar el drag.
    private bool originalIsPlaced;
    private Transform originalSegment;
    private AlbumZone originalAlbumZone;

    private Collider2D myCollider;

    // ===========================================================
    // UNITY
    // ===========================================================

    protected virtual void Awake()
    {
        cam = Camera.main;
        myCollider = GetComponent<Collider2D>();

        // Root fijo del sticker.
        if (stickerRoot == null)
            stickerRoot = transform.parent != null ? transform.parent : transform;

        EnsureSceneReferences();
    }

    protected virtual void OnEnable()
    {
        /*
         * Especialmente importante para stickers que comienzan
         * bajo BagScreen, porque ese GameObject empieza inactive.
         */
        EnsureSceneReferences();
        EnsureAlbumStateFromHierarchy();
    }

    protected virtual void Update()
    {
        /*
         * Si AlbumManager despertó después que este sticker,
         * aquí terminaremos de registrar stickers que ya estaban
         * dentro del Album desde Editor.
         */
        EnsureAlbumStateFromHierarchy();

        HandleDragging();
    }

    // ===========================================================
    // SCENE REFERENCES
    // ===========================================================

    private void EnsureSceneReferences()
    {
        /*
         * FindObjectOfType normal ignora GameObjects inactive.
         *
         * Buscamos incluyendo inactive porque GameplayScreen puede
         * estar apagada justo cuando se activa BagScreen.
         */

        if (controller == null)
        {
            RouletteController[] controllers =
                FindObjectsOfType<RouletteController>(true);

            if (controllers.Length > 0)
                controller = controllers[0];
        }

        /*
         * Si RouletteController ya conoce estas referencias,
         * es mejor utilizarlas directamente.
         */
        if (controller != null)
        {
            if (wheelCenter == null && controller.wheel != null)
                wheelCenter = controller.wheel;

            if (generator == null && controller.generator != null)
                generator = controller.generator;
        }

        /*
         * Fallback para Generator, también incluyendo inactive.
         */
        if (generator == null)
        {
            WheelGenerator[] generators =
                FindObjectsOfType<WheelGenerator>(true);

            if (generators.Length > 0)
                generator = generators[0];
        }

        /*
         * Último fallback para Wheel.
         */
        if (wheelCenter == null && controller != null)
            wheelCenter = controller.wheel;
    }

    // ===========================================================
    // ALBUM STATE DISCOVERY
    // ===========================================================

    private void EnsureAlbumStateFromHierarchy()
    {
        /*
         * Esto permite colocar stickers directamente en:
         *
         * Album
         * └── AlbumZone
         *     └── ContentRoot
         *         └── Sticker
         *
         * desde Editor antes de darle a Play.
         *
         * No dependemos del orden de Awake entre AlbumManager
         * y los stickers: si AlbumManager todavía no existe,
         * simplemente volveremos a comprobarlo durante Update.
         */

        if (currentAlbumZone != null)
            return;

        if (AlbumManager.Instance == null)
            return;

        if (!AlbumManager.Instance.IsStickerInAlbum(this))
            return;

        currentAlbumZone =
            AlbumManager.Instance.albumZone;

        // Un sticker guardado en Album NO está colocado en ruleta.
        isPlaced = false;
        currentSegment = null;
    }

    // ===========================================================
    // DRAGGING
    // ===========================================================

    protected virtual void HandleDragging()
    {
#if UNITY_EDITOR || UNITY_STANDALONE

        EnsureSceneReferences();

        if (cam == null)
            cam = Camera.main;

        if (cam == null || myCollider == null || stickerRoot == null)
            return;

        Vector2 mouseWorld =
            cam.ScreenToWorldPoint(Input.mousePosition);

        // -------------------------------------------------------
        // MOUSE DOWN
        // -------------------------------------------------------

        if (Input.GetMouseButtonDown(0))
        {
            if (controller != null &&
                controller.SpinInProgress)
            {
                return;
            }

            if (myCollider.OverlapPoint(mouseWorld))
            {
                isDragging = true;

                // ------------------------------------------------
                // GUARDAMOS TODO EL ESTADO ORIGINAL
                // ------------------------------------------------

                originalPosition =
                    stickerRoot.position;

                originalRotation =
                    stickerRoot.rotation;

                originalParent =
                    stickerRoot.parent;

                originalIsPlaced =
                    isPlaced;

                originalSegment =
                    currentSegment;

                originalAlbumZone =
                    currentAlbumZone;

                // ------------------------------------------------
                // SISTEMA BAG / MOBILE
                // ------------------------------------------------

                if (BagManager.Instance != null)
                {
                    BagManager.Instance.FreeBagSlot(this);
                    BagManager.Instance.FreeGameplaySlot(this);
                }

                // ------------------------------------------------
                // SI VENÍA DE LA RULETA
                // ------------------------------------------------

                if (isPlaced)
                {
                    isPlaced = false;
                    currentSegment = null;

                    stickerRoot.SetParent(
                        null,
                        true
                    );
                }

                // ------------------------------------------------
                // SI VENÍA DEL ALBUM
                // ------------------------------------------------

                /*
                 * Lo sacamos temporalmente del ContentRoot.
                 *
                 * Esto evita que AlbumPlacementUtility lo siga
                 * considerando parte física del álbum mientras
                 * estamos arrastrándolo.
                 */
                if (currentAlbumZone != null)
                {
                    currentAlbumZone = null;

                    stickerRoot.SetParent(
                        null,
                        true
                    );
                }

                offset =
                    stickerRoot.position -
                    (Vector3)mouseWorld;

                SetAlpha(0.6f);

                controller?.SetInputBlocked(true);
            }
        }

        // -------------------------------------------------------
        // DRAG
        // -------------------------------------------------------

        if (isDragging)
        {
            stickerRoot.position =
                (Vector3)mouseWorld +
                offset;

            // ---------------------------------------------------
            // MOUSE UP
            // ---------------------------------------------------

            if (Input.GetMouseButtonUp(0))
            {
                HandleDrop();

                isDragging = false;

                controller?.SetInputBlocked(false);

                SetAlpha(1f);

                /*
                 * Si la ruleta estaba bloqueada por una colocación
                 * inválida tras regeneración, volvemos a validar
                 * ahora que el jugador ha movido un sticker.
                 */
                if (StickerPlacementValidator.Instance != null &&
                    StickerPlacementValidator.Instance.InputBlocked)
                {
                    StickerPlacementValidator.Instance
                        .NotifyStickerDropped(this);
                }
            }
        }

#endif
    }

    // ===========================================================
    // DROP LOGIC
    // ===========================================================

    private void HandleDrop()
    {
        Vector2 p =
            stickerRoot.position;

        // -------------------------------------------------------
        // ALBUM (PC)
        // -------------------------------------------------------
        //
        // Se comprueba ANTES que BagManager porque Album es
        // un sistema paralelo.
        //
        // Puede funcionar aunque BagManager no exista.
        // -------------------------------------------------------

        if (AlbumManager.Instance != null &&
            AlbumManager.Instance.IsPointInsideAlbum(p))
        {
            if (TryPlaceInAlbum(p))
                return;

            /*
             * El centro está dentro del álbum pero:
             *
             * - el collider se sale del límite
             * - o solapa otro sticker
             *
             * → posición inválida.
             */
            ReturnToOrigin();
            return;
        }

        // =======================================================
        // BAG / MOBILE
        //
        // Todo este flujo se conserva.
        // =======================================================

        if (BagManager.Instance != null)
        {
            // ---------------------------------------------------
            // BAG PORTAL
            // ---------------------------------------------------

            if (BagManager.Instance.IsPointOnBagPortal(p))
            {
                var free =
                    BagManager.Instance
                        .FindFirstFreeBagSlot();

                if (free != null)
                {
                    BagManager.Instance
                        .PlaceStickerInBagSlot_Auto(
                            this,
                            free
                        );

                    currentAlbumZone = null;

                    return;
                }

                ReturnToOrigin();
                return;
            }

            // ---------------------------------------------------
            // GAMEPLAY PORTAL
            // ---------------------------------------------------

            if (BagManager.Instance.IsPointOnGameplayPortal(p))
            {
                if (BagManager.Instance
                    .PlaceStickerInNextEmptyGameplayArea_FromBag(
                        this
                    ))
                {
                    currentAlbumZone = null;
                    return;
                }

                ReturnToOrigin();
                return;
            }

            // ---------------------------------------------------
            // BAG SCREEN
            // ---------------------------------------------------

            if (BagManager.Instance.IsBagActive())
            {
                var slot =
                    BagManager.Instance
                        .GetBagSlotAtPosition(p);

                if (slot != null)
                {
                    if (BagManager.Instance
                        .TryPlaceInSlotManual(
                            this,
                            slot,
                            stickerRoot.position
                        ))
                    {
                        currentAlbumZone = null;
                        return;
                    }

                    ReturnToOrigin();
                    return;
                }

                ReturnToOrigin();
                return;
            }

            // ---------------------------------------------------
            // GAMEPLAY SLOT
            // ---------------------------------------------------

            var gSlot =
                BagManager.Instance
                    .GetGameplaySlotAtPosition(p);

            if (gSlot != null)
            {
                if (BagManager.Instance
                    .TryPlaceInSlotManual(
                        this,
                        gSlot,
                        stickerRoot.position
                    ))
                {
                    currentAlbumZone = null;
                    return;
                }

                if (TryPlaceOnWheel(p))
                    return;

                if (TryPlaceFreelyInGameplayArea(p))
                    return;

                ReturnToOrigin();
                return;
            }
        }

        // -------------------------------------------------------
        // ROULETTE
        // -------------------------------------------------------

        if (TryPlaceOnWheel(p))
            return;

        // -------------------------------------------------------
        // FREE GAMEPLAY AREA
        // MOBILE / LEGACY
        // -------------------------------------------------------

        if (BagManager.Instance != null &&
            BagManager.Instance
                .IsPointInsideAnyGameplayArea(p))
        {
            if (TryPlaceFreelyInGameplayArea(p))
                return;

            ReturnToOrigin();
            return;
        }

        // -------------------------------------------------------
        // FALLBACK
        // -------------------------------------------------------

        TryPlaceSticker();
    }

    // ===========================================================
    // ALBUM LOGIC
    // ===========================================================

    private bool TryPlaceInAlbum(
        Vector3 dropPos)
    {
        if (AlbumManager.Instance == null)
            return false;

        AlbumZone zone =
            AlbumManager.Instance
                .GetAlbumZoneAtPosition(
                    dropPos
                );

        if (zone == null)
            return false;

        /*
         * AlbumManager + AlbumPlacementUtility comprueban:
         *
         * - collider completo dentro
         * - tolerancia
         * - no overlap
         */
        if (!AlbumManager.Instance
            .TryPlaceStickerInAlbum(this))
        {
            return false;
        }

        // -------------------------------------------------------
        // ESTADO LÓGICO
        // -------------------------------------------------------

        currentAlbumZone =
            zone;

        /*
         * Album y Roulette son mutuamente excluyentes.
         */
        isPlaced = false;
        currentSegment = null;

        /*
         * Si veníamos del sistema antiguo,
         * HandleDragging ya pidió liberar sus slots.
         */
        currentBagZone = null;
        currentGameplayZone = null;

        return true;
    }

    // ===========================================================
    // ROULETTE LOGIC
    // ===========================================================

    private bool TryPlaceOnWheel(
        Vector3 dropPos)
    {
        if (myCollider == null)
            myCollider = GetComponent<Collider2D>();

        if (myCollider == null)
            return false;

        /*
         * StickerPlacementUtility es la única autoridad
         * para colocación sobre la ruleta.
         */

        Collider2D validSegment =
            StickerPlacementUtility
                .FindValidSegment(
                    this,
                    segmentMask,
                    tolerance
                );

        if (validSegment == null)
            return false;

        stickerRoot.SetParent(
            validSegment.transform,
            true
        );

        currentSegment =
            validSegment.transform;

        isPlaced = true;

        // Ya no pertenece al Album.
        currentAlbumZone = null;

        return true;
    }

    // ===========================================================
    // FALLBACK PLACEMENT
    // ===========================================================

    protected virtual void TryPlaceSticker()
    {
        if (myCollider == null)
            myCollider = GetComponent<Collider2D>();

        if (myCollider == null)
        {
            ReturnToOrigin();
            return;
        }

        Collider2D validSegment =
            StickerPlacementUtility
                .FindValidSegment(
                    this,
                    segmentMask,
                    tolerance
                );

        if (validSegment == null)
        {
            ReturnToOrigin();
            return;
        }

        stickerRoot.SetParent(
            validSegment.transform,
            true
        );

        currentSegment =
            validSegment.transform;

        isPlaced = true;

        currentAlbumZone = null;
    }

    // ===========================================================
    // SEGMENT VALIDATION
    // ===========================================================

    /// <summary>
    /// Se mantiene público porque StickerPlacementValidator
    /// ya utiliza este método.
    /// </summary>
    public bool DebugCheckSegment(
        Collider2D segmentCol)
    {
        if (segmentCol == null)
            return false;

        return
            StickerPlacementUtility
                .CanPlaceOnSegment(
                    this,
                    segmentCol,
                    tolerance
                );
    }

    // ===========================================================
    // RESTORE AFTER WHEEL REGEN
    // ===========================================================

    public void OnRestoredAfterWheelRegen()
    {
        if (currentSegment == null)
            return;

        isPlaced = true;
        currentAlbumZone = null;

        /*
         * Conservamos este pequeño refresh.
         */
        Vector3 p =
            stickerRoot.position;

        stickerRoot.position =
            p +
            new Vector3(
                0.001f,
                0f,
                0f
            );

        stickerRoot.position =
            p;
    }

    // ===========================================================
    // RETURN TO ORIGIN
    // ===========================================================

    protected virtual void ReturnToOrigin()
    {
        if (stickerRoot == null)
            return;

        stickerRoot.SetParent(
            originalParent,
            true
        );

        stickerRoot.position =
            originalPosition;

        stickerRoot.rotation =
            originalRotation;

        /*
         * Restauramos también TODO el estado lógico.
         *
         * Esto es esencial para:
         *
         * Album → drop inválido → Album
         * Wheel → drop inválido → Wheel
         */

        isPlaced =
            originalIsPlaced;

        currentSegment =
            originalSegment;

        currentAlbumZone =
            originalAlbumZone;
    }

    // ===========================================================
    // VISUAL
    // ===========================================================

    private void SetAlpha(float a)
    {
        if (stickerRoot == null)
            return;

        var sr =
            stickerRoot
                .GetComponentInChildren<SpriteRenderer>();

        if (sr)
        {
            Color c = sr.color;

            c.a = a;

            sr.color = c;
        }
    }

    // ===========================================================
    // EFFECT
    // ===========================================================

    // El ScriptableObject recibe el contexto del sticker.
    public virtual void OnSegmentWin()
    {
        if (effect == null)
            return;

        effect.ApplyEffect(this);
    }

    // ===========================================================
    // GAMEPLAY FREE AREA LOGIC
    //
    // Se conserva el sistema anterior.
    // ===========================================================

    private bool TryPlaceFreelyInGameplayArea(
        Vector2 dropPos)
    {
        if (BagManager.Instance == null)
            return false;

        int idx =
            BagManager.Instance
                .GetGameplayAreaIndexAtPoint(
                    dropPos
                );

        if (idx < 0)
            return false;

        var area =
            BagManager.Instance
                .gameplayAreas[idx];

        if (area == null ||
            area.areaCollider == null)
        {
            return false;
        }

        float r =
            ApproxRadiusWorld();

        if (!PointInsideAreaWithMargin(
                area.areaCollider,
                dropPos,
                r))
        {
            return false;
        }

        var areaRoot =
            area.contentRoot != null
                ? area.contentRoot
                : stickerRoot.parent;

        if (OverlapsAnyInArea(
                areaRoot,
                dropPos,
                r))
        {
            return false;
        }

        stickerRoot.SetParent(
            areaRoot,
            true
        );

        Vector3 clamped =
            BagManager.Instance
                .ClampToGameplay(
                    dropPos,
                    idx
                );

        stickerRoot.position =
            new Vector3(
                clamped.x,
                clamped.y,
                stickerRoot.position.z
            );

        stickerRoot.rotation =
            Quaternion.identity;

        isPlaced = false;
        currentSegment = null;
        currentAlbumZone = null;

        return true;
    }

    // ===========================================================
    // LEGACY GAMEPLAY AREA HELPERS
    // ===========================================================

    private float ApproxRadiusWorld()
    {
        if (myCollider == null)
            myCollider = GetComponent<Collider2D>();

        if (myCollider == null)
            return 0f;

        var e =
            myCollider.bounds.extents;

        return Mathf.Max(
            e.x,
            e.y
        );
    }

    private bool PointInsideAreaWithMargin(
        Collider2D areaCol,
        Vector2 p,
        float margin)
    {
        if (!areaCol.OverlapPoint(p))
            return false;

        Bounds b =
            areaCol.bounds;

        return
            p.x >= b.min.x + margin &&
            p.x <= b.max.x - margin &&
            p.y >= b.min.y + margin &&
            p.y <= b.max.y - margin;
    }

    private bool OverlapsAnyInArea(
        Transform areaRoot,
        Vector2 candidate,
        float r)
    {
        if (areaRoot == null)
            return false;

        var others =
            areaRoot
                .GetComponentsInChildren<Collider2D>(
                    true
                );

        Transform selfRoot =
            stickerRoot;

        foreach (var o in others)
        {
            if (o == null)
                continue;

            if (o.transform.IsChildOf(selfRoot))
                continue;

            BaseSticker other =
                o.GetComponentInParent<BaseSticker>();

            if (other == null)
                continue;

            float d =
                Vector2.Distance(
                    candidate,
                    o.bounds.center
                );

            float ro =
                Mathf.Max(
                    o.bounds.extents.x,
                    o.bounds.extents.y
                );

            if (d <
                (r + ro) * 0.98f)
            {
                return true;
            }
        }

        return false;
    }
}