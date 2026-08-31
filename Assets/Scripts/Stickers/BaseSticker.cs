using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BaseSticker : MonoBehaviour
{
    [Header("Sticker Config")]
    public StickerEffect effect;
    public Transform wheelCenter;
    public WheelGenerator generator;
    public RouletteController controller;

    [Header("Sticker Collider")]
    [Tooltip(
        "Collider físico que representa la forma real del sticker. " +
        "Puede estar en este GameObject o en cualquier otro GameObject del prefab. " +
        "Si se deja vacío, BaseSticker mantiene compatibilidad con los prefabs antiguos " +
        "buscando un Collider2D en este mismo GameObject."
    )]
    [SerializeField]
    private Collider2D stickerCollider;

    /// <summary>
    /// Única referencia pública al collider físico del sticker.
    ///
    /// Prefabs antiguos:
    ///     campo vacío -> usa GetComponent<Collider2D>() como siempre.
    ///
    /// Prefabs nuevos:
    ///     se puede asignar un collider situado en otro GameObject del prefab.
    /// </summary>
    public Collider2D StickerCollider
    {
        get
        {
            if (stickerCollider != null)
                return stickerCollider;

            if (myCollider == null)
                myCollider = GetComponent<Collider2D>();

            return myCollider;
        }
    }

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

    // ===========================================================
    // LIMITED USES - RUNTIME STATE
    // ===========================================================

    [Header("Runtime Uses")]
    [Tooltip(
        "Runtime only. -1 means unlimited. " +
        "Limited stickers initialize this value from StickerEffect.maxUses."
    )]
    [SerializeField]
    private int remainingUses = -1;

    private bool useStateInitialized = false;
    private StickerEffect useStateEffect = null;
    private bool consumed = false;

    /*
     * Generic per-instance runtime memory for stateful stickers.
     *
     * Example:
     * Piggy Bank can store money on THIS physical sticker without
     * putting mutable runtime state inside the shared ScriptableObject.
     */
    private readonly Dictionary<string, int> runtimeIntState =
        new Dictionary<string, int>();

    public bool HasLimitedUses
    {
        get
        {
            EnsureUseStateInitialized();

            return
                effect != null &&
                effect.HasLimitedUses;
        }
    }

    public int RemainingUses
    {
        get
        {
            EnsureUseStateInitialized();

            return remainingUses;
        }
    }

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
    // SPIN COLLIDER OPTIMIZATION
    // ===========================================================

    /*
     * During the physical wheel spin, a sticker placed on the wheel
     * cannot be dragged or hovered meaningfully, but its Collider2D
     * would otherwise keep moving with the rotating wheel and continue
     * participating in Physics2D broadphase updates.
     *
     * We temporarily disable ONLY the sticker's explicit interaction /
     * placement collider, then restore its exact previous enabled state
     * before spin resolution begins.
     */
    private bool spinColliderTemporarilyDisabled =
        false;

    private bool spinColliderPreviousEnabledState =
        false;


    // ===========================================================
    // UNITY
    // ===========================================================

    protected virtual void Awake()
    {
        cam = Camera.main;
        myCollider = StickerCollider;

        // Root fijo del sticker.
        if (stickerRoot == null)
            stickerRoot = transform.parent != null ? transform.parent : transform;

        EnsureSceneReferences();
        EnsureUseStateInitialized();
    }

    private void OnValidate()
    {
        /*
         * Backwards compatibility for existing prefabs:
         * if they still keep the collider beside BaseSticker,
         * Unity fills the explicit reference automatically.
         *
         * We intentionally do NOT search children here: a sticker prefab can
         * contain several unrelated colliders and we never want to guess.
         */
        if (stickerCollider == null)
            stickerCollider = GetComponent<Collider2D>();
    }

    protected virtual void OnEnable()
    {
        /*
         * Especialmente importante para stickers que comienzan
         * bajo BagScreen, porque ese GameObject empieza inactive.
         */
        EnsureSceneReferences();
        EnsureAlbumStateFromHierarchy();
        EnsureUseStateInitialized();
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

            /*
             * A SegmentBlock freezes every sticker already inside it.
             *
             * The segment remains visually readable, but its contents cannot
             * be dragged out until the round ends.
             */
            if (isPlaced &&
                currentSegment != null &&
                generator != null &&
                generator.IsSegmentBlocked(
                    currentSegment
                ))
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
            myCollider = StickerCollider;

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
            myCollider = StickerCollider;

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
    // SPIN COLLIDER OPTIMIZATION
    // ===========================================================

    /// <summary>
    /// Temporarily disables this physical sticker collider while the wheel
    /// is spinning.
    ///
    /// Only stickers currently placed on the wheel are eligible.
    /// Album / reward / loose stickers remain untouched.
    ///
    /// The collider's exact previous enabled state is remembered so a
    /// collider that was intentionally disabled before the spin is never
    /// accidentally enabled afterwards.
    /// </summary>
    public void DisableWheelColliderForPhysicalSpin()
    {
        if (spinColliderTemporarilyDisabled)
            return;


        if (!isPlaced ||
            currentSegment == null)
        {
            return;
        }


        Collider2D collider =
            StickerCollider;


        if (collider == null)
            return;


        spinColliderPreviousEnabledState =
            collider.enabled;

        spinColliderTemporarilyDisabled =
            true;


        if (collider.enabled)
        {
            collider.enabled =
                false;
        }
    }


    /// <summary>
    /// Restores the collider state captured by
    /// DisableWheelColliderForPhysicalSpin().
    ///
    /// This is called BEFORE any valid-spin sticker effects resolve, so
    /// WheelShifter / Magic Bean wheel regeneration and placement validation
    /// always see fully active sticker colliders.
    /// </summary>
    public void RestoreWheelColliderAfterPhysicalSpin()
    {
        if (!spinColliderTemporarilyDisabled)
            return;


        Collider2D collider =
            StickerCollider;


        if (collider != null)
        {
            collider.enabled =
                spinColliderPreviousEnabledState;
        }


        spinColliderTemporarilyDisabled =
            false;
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

    // ===========================================================
    // SPIN LOCATION RESOLUTION
    // ===========================================================

    /// <summary>
    /// Optional preparation pass used before normal sticker resolution.
    ///
    /// The roulette calls this for the COMPLETE location snapshot before any
    /// ResolveSpinLocation() call. Most stickers are a no-op here; passive
    /// reactive effects such as Shield can register themselves safely.
    /// </summary>
    public virtual void PrepareSpinLocation(
        StickerSpinLocation location)
    {
        if (effect == null ||
            consumed)
        {
            return;
        }

        EnsureUseStateInitialized();

        effect.PrepareSpinLocation(
            this,
            location
        );
    }


    /// <summary>
    /// Generic entry point used when a valid spin finishes.
    ///
    /// The roulette snapshots every sticker's location BEFORE any
    /// sticker effect runs, then calls this method with one of:
    ///
    /// - WinningSegment
    /// - NonWinningSegment
    /// - Album
    ///
    /// This means effects such as WheelShifter cannot retroactively
    /// change where another sticker was considered to be when the
    /// spin stopped.
    /// </summary>
    public virtual void ResolveSpinLocation(
        StickerSpinLocation location)
    {
        if (effect == null ||
            consumed)
        {
            return;
        }

        EnsureUseStateInitialized();

        effect.ResolveSpinLocation(
            this,
            location
        );
    }


    /// <summary>
    /// Backwards-compatible API.
    /// Existing code that explicitly triggers a winning sticker can
    /// continue to call OnSegmentWin().
    /// </summary>
    public virtual void OnSegmentWin()
    {
        ResolveSpinLocation(
            StickerSpinLocation.WinningSegment
        );
    }


    // ===========================================================
    // LIMITED USES
    // ===========================================================

    /// <summary>
    /// Initializes the runtime use counter for this physical sticker instance.
    ///
    /// IMPORTANT:
    /// maxUses lives in the StickerEffect ScriptableObject because it is
    /// configuration shared by every copy of that sticker.
    ///
    /// remainingUses lives here because every physical sticker instance
    /// needs its own independent counter.
    /// </summary>
    private void EnsureUseStateInitialized()
    {
        /*
         * If the effect reference changes at runtime, the new effect starts
         * with its own configured use count.
         */
        if (useStateInitialized &&
            useStateEffect == effect)
        {
            return;
        }

        useStateInitialized = true;
        useStateEffect = effect;
        consumed = false;

        /*
         * Runtime memory belongs to the current effect configuration.
         * If a prefab swaps to another StickerEffect at runtime, the
         * new effect must not inherit the previous effect's state.
         */
        runtimeIntState.Clear();

        if (effect != null &&
            effect.HasLimitedUses)
        {
            remainingUses =
                Mathf.Max(
                    1,
                    effect.maxUses
                );
        }
        else
        {
            /*
             * -1 is our runtime marker for unlimited.
             */
            remainingUses = -1;
        }
    }


    /// <summary>
    /// Called by StickerEffect immediately after the activation itself has
    /// been written to the Game Log.
    ///
    /// Unlimited stickers ignore this completely.
    /// Limited stickers lose one use, report the new amount, and are
    /// destroyed when the counter reaches zero.
    /// </summary>
    public void ConsumeUseAfterActivation(
        bool logRemainingUses = true)
    {
        EnsureUseStateInitialized();

        if (effect == null ||
            !effect.HasLimitedUses ||
            consumed)
        {
            return;
        }

        remainingUses =
            Mathf.Max(
                0,
                remainingUses - 1
            );


        /*
         * Most stickers log their remaining uses immediately.
         *
         * Reactive effects such as Shield can pass false so their complete
         * feedback can be deferred until AFTER the damage source itself has
         * been written to the Game Log.
         */
        if (logRemainingUses)
        {
            LogRemainingUses();
        }


        if (remainingUses <= 0)
        {
            consumed = true;
            DestroyStickerInstance();
        }
    }


    private void LogRemainingUses()
    {
        if (GameLogManager.Instance == null)
            return;

        string stickerName =
            effect != null &&
            !string.IsNullOrWhiteSpace(
                effect.stickerName
            )
                ? effect.stickerName
                : gameObject.name;

        GameLogManager.Instance
            .AddGameplayLine(
                GameLogManager.Instance
                    .StickerText(
                        stickerName
                    ) +
                $" uses remaining: {remainingUses}"
            );
    }


    // ===========================================================
    // GENERIC PER-INSTANCE RUNTIME STATE
    // ===========================================================

    /// <summary>
    /// Reads an integer value stored only on this physical sticker.
    /// Useful for stateful effects such as Piggy Bank.
    /// </summary>
    public int GetRuntimeInt(
        string key,
        int defaultValue = 0)
    {
        if (string.IsNullOrEmpty(key))
            return defaultValue;

        if (runtimeIntState.TryGetValue(
                key,
                out int value))
        {
            return value;
        }

        return defaultValue;
    }


    /// <summary>
    /// Stores an integer value only on this physical sticker instance.
    /// </summary>
    public void SetRuntimeInt(
        string key,
        int value)
    {
        if (string.IsNullOrEmpty(key))
            return;

        runtimeIntState[key] = value;
    }


    /// <summary>
    /// Adds to an integer value and returns the new total.
    /// </summary>
    public int AddRuntimeInt(
        string key,
        int amount)
    {
        int newValue =
            GetRuntimeInt(key) +
            amount;

        SetRuntimeInt(
            key,
            newValue
        );

        return newValue;
    }


    private void DestroyStickerInstance()
    {
        GameObject objectToDestroy =
            stickerRoot != null
                ? stickerRoot.gameObject
                : gameObject;

        Debug.Log(
            $"[STICKER] '{effect?.stickerName ?? name}' " +
            "ran out of uses and was destroyed."
        );

        /*
         * Destroy is deferred until the end of the frame, so the current
         * effect can safely finish resolving after scheduling destruction.
         */
        Destroy(
            objectToDestroy
        );
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
            myCollider = StickerCollider;

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