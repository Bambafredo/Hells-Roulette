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
    // Necesario para restaurar correctamente un sticker
    // si intentamos colocarlo en una posición inválida.
    private bool originalIsPlaced;
    private Transform originalSegment;

    private Collider2D myCollider;

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
    }

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
        *
        * Si tenemos controller, normalmente ya estará resuelto arriba.
        */
        if (wheelCenter == null && controller != null)
            wheelCenter = controller.wheel;
    }

    protected virtual void Update()
    {
        HandleDragging();
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

        Vector2 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);

        if (Input.GetMouseButtonDown(0))
        {
            if (controller != null && controller.SpinInProgress)
                return;

            if (myCollider.OverlapPoint(mouseWorld))
            {
                isDragging = true;

                // Guardamos TODO el estado anterior antes de modificar nada.
                originalPosition = stickerRoot.position;
                originalRotation = stickerRoot.rotation;
                originalParent = stickerRoot.parent;

                originalIsPlaced = isPlaced;
                originalSegment = currentSegment;

                if (BagManager.Instance != null)
                {
                    BagManager.Instance.FreeBagSlot(this);
                    BagManager.Instance.FreeGameplaySlot(this);
                }

                if (isPlaced)
                {
                    isPlaced = false;
                    currentSegment = null;

                    stickerRoot.SetParent(null, true);
                }

                offset = stickerRoot.position - (Vector3)mouseWorld;

                SetAlpha(0.6f);

                controller?.SetInputBlocked(true);
            }
        }

        if (isDragging)
        {
            stickerRoot.position = (Vector3)mouseWorld + offset;

            if (Input.GetMouseButtonUp(0))
            {
                HandleDrop();

                isDragging = false;

                controller?.SetInputBlocked(false);

                SetAlpha(1f);

                if (StickerPlacementValidator.Instance != null &&
                    StickerPlacementValidator.Instance.InputBlocked)
                {
                    StickerPlacementValidator.Instance.NotifyStickerDropped(this);
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
        if (BagManager.Instance == null)
        {
            TryPlaceSticker();
            return;
        }

        Vector2 p = stickerRoot.position;

        // -------------------------------------------------------
        // BAG PORTAL
        // -------------------------------------------------------

        if (BagManager.Instance.IsPointOnBagPortal(p))
        {
            var free = BagManager.Instance.FindFirstFreeBagSlot();

            if (free != null)
            {
                BagManager.Instance.PlaceStickerInBagSlot_Auto(this, free);
                return;
            }

            ReturnToOrigin();
            return;
        }

        // -------------------------------------------------------
        // GAMEPLAY PORTAL
        // -------------------------------------------------------

        if (BagManager.Instance.IsPointOnGameplayPortal(p))
        {
            if (BagManager.Instance.PlaceStickerInNextEmptyGameplayArea_FromBag(this))
                return;

            ReturnToOrigin();
            return;
        }

        // -------------------------------------------------------
        // BAG SCREEN
        // -------------------------------------------------------

        if (BagManager.Instance.IsBagActive())
        {
            var slot = BagManager.Instance.GetBagSlotAtPosition(p);

            if (slot != null)
            {
                if (BagManager.Instance.TryPlaceInSlotManual(
                        this,
                        slot,
                        stickerRoot.position))
                {
                    return;
                }

                ReturnToOrigin();
                return;
            }

            ReturnToOrigin();
            return;
        }

        // -------------------------------------------------------
        // GAMEPLAY SCREEN
        // -------------------------------------------------------

        var gSlot = BagManager.Instance.GetGameplaySlotAtPosition(p);

        if (gSlot != null)
        {
            if (BagManager.Instance.TryPlaceInSlotManual(
                    this,
                    gSlot,
                    stickerRoot.position))
            {
                return;
            }

            if (TryPlaceOnWheel(p))
                return;

            if (TryPlaceFreelyInGameplayArea(p))
                return;

            ReturnToOrigin();
            return;
        }

        // Primero intentamos colocarlo sobre la ruleta.
        if (TryPlaceOnWheel(p))
            return;

        // Después, en una zona libre de gameplay.
        if (BagManager.Instance.IsPointInsideAnyGameplayArea(p))
        {
            if (TryPlaceFreelyInGameplayArea(p))
                return;

            ReturnToOrigin();
            return;
        }

        // Fallback.
        TryPlaceSticker();
    }

    // ===========================================================
    // ROULETTE LOGIC
    // ===========================================================

    private bool TryPlaceOnWheel(Vector3 dropPos)
    {
        if (myCollider == null)
            myCollider = GetComponent<Collider2D>();

        if (myCollider == null)
            return false;

        /*
         * IMPORTANTE:
         *
         * Ya no usamos:
         *
         * Physics2D.OverlapPoint(dropPos)
         * +
         * IsMostlyInsideSegment()
         * +
         * OverlapCircleAll()
         *
         * Buscamos los segmentos candidatos debajo de la geometría
         * del sticker y StickerPlacementUtility decide si realmente
         * cabe entero y no colisiona con otro sticker.
         */

        Collider2D validSegment =
            StickerPlacementUtility.FindValidSegment(
                this,
                segmentMask,
                tolerance
            );

        if (validSegment == null)
            return false;

        stickerRoot.SetParent(validSegment.transform, true);

        currentSegment = validSegment.transform;
        isPlaced = true;

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
            StickerPlacementUtility.FindValidSegment(
                this,
                segmentMask,
                tolerance
            );

        if (validSegment == null)
        {
            ReturnToOrigin();
            return;
        }

        stickerRoot.SetParent(validSegment.transform, true);

        currentSegment = validSegment.transform;
        isPlaced = true;
    }

    // ===========================================================
    // SEGMENT VALIDATION
    // ===========================================================

    /// <summary>
    /// Se mantiene público porque StickerPlacementValidator
    /// ya utiliza este método.
    ///
    /// Ahora utiliza exactamente la misma validación geométrica
    /// que utilizamos durante el drop.
    /// </summary>
    public bool DebugCheckSegment(Collider2D segmentCol)
    {
        if (segmentCol == null)
            return false;

        return StickerPlacementUtility.CanPlaceOnSegment(
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

        /*
         * Conservamos este pequeño refresh porque ya existía
         * y puede ayudar a Unity a actualizar la geometría
         * después de regenerar la rueda.
         */
        Vector3 p = stickerRoot.position;

        stickerRoot.position =
            p + new Vector3(0.001f, 0f, 0f);

        stickerRoot.position = p;
    }

    // ===========================================================
    // UTILITIES
    // ===========================================================

    protected virtual void ReturnToOrigin()
    {
        if (stickerRoot == null)
            return;

        stickerRoot.SetParent(originalParent, true);

        stickerRoot.position = originalPosition;
        stickerRoot.rotation = originalRotation;

        /*
         * Antes el Transform volvía al segmento,
         * pero isPlaced/currentSegment NO volvían.
         *
         * Eso dejaba stickers visualmente colocados
         * pero lógicamente fuera de la ruleta.
         */
        isPlaced = originalIsPlaced;
        currentSegment = originalSegment;
    }

    private void SetAlpha(float a)
    {
        if (stickerRoot == null)
            return;

        var sr = stickerRoot.GetComponentInChildren<SpriteRenderer>();

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
    // Esta sección permanece esencialmente igual.
    // NO estamos refactorizando todavía la colocación de la bolsa
    // o las áreas libres de gameplay.
    // ===========================================================

    private bool TryPlaceFreelyInGameplayArea(Vector2 dropPos)
    {
        int idx =
            BagManager.Instance.GetGameplayAreaIndexAtPoint(dropPos);

        if (idx < 0)
            return false;

        var area =
            BagManager.Instance.gameplayAreas[idx];

        if (area == null || area.areaCollider == null)
            return false;

        float r = ApproxRadiusWorld();

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

        stickerRoot.SetParent(areaRoot, true);

        Vector3 clamped =
            BagManager.Instance.ClampToGameplay(
                dropPos,
                idx
            );

        stickerRoot.position =
            new Vector3(
                clamped.x,
                clamped.y,
                stickerRoot.position.z
            );

        stickerRoot.rotation = Quaternion.identity;

        isPlaced = false;
        currentSegment = null;

        return true;
    }

    private float ApproxRadiusWorld()
    {
        if (myCollider == null)
            myCollider = GetComponent<Collider2D>();

        if (myCollider == null)
            return 0f;

        var e = myCollider.bounds.extents;

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

        Bounds b = areaCol.bounds;

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
            areaRoot.GetComponentsInChildren<Collider2D>(true);

        Transform selfRoot = stickerRoot;

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

            if (d < (r + ro) * 0.98f)
                return true;
        }

        return false;
    }
}