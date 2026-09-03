using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class WheelSegmentData
{
    public int index;
    public PolygonCollider2D collider;
    public SegmentMesh meshComponent;
}

public class WheelGenerator : MonoBehaviour
{
    [Header("Segments")]
    [Range(2, 36)]
    public int segmentCount = 8;

    [Tooltip("Ángulos individuales de cada segmento (en grados).")]
    public List<float> segmentAngles = new List<float>();

    [Min(0.1f)]
    public float radius = 2.5f;

    [Min(3)]
    public int meshResolution = 20;

    [Header("Visual")]
    public bool regenerateOnPlay = true;
    public string sortingLayerName = "Default";
    public int sortingOrder = 0;


    [Header("Segment Visual Shader")]

    [Tooltip(
        "Assign Assets/Shaders/SegmentVisual.shader here. " +
        "If left empty the wheel falls back to Sprites/Default, " +
        "but pattern / SegmentBlock visuals will not be available."
    )]
    public Shader segmentVisualShader;


    [Header("Cosmetic Pattern - Foundation")]

    [Tooltip(
        "Optional global pattern used as a foundation for future wheel " +
        "customization. Keep opacity at 0 for the current plain-color wheel."
    )]
    public Texture cosmeticPatternTexture;

    public Color cosmeticPatternColor =
        Color.black;

    [Range(0f, 1f)]
    public float cosmeticPatternOpacity =
        0f;

    [Min(0.01f)]
    public float cosmeticPatternScale =
        4f;

    public float cosmeticPatternRotation =
        0f;


    [Header("Segment Block Visual")]

    [Tooltip(
        "How strongly a blocked segment is pushed toward the Blocked Base Color."
    )]
    [Range(0f, 1f)]
    public float blockedBaseBlend =
        0.8f;

    public Color blockedBaseColor =
        Color.white;

    public Color blockedStripeColor =
        Color.black;

    [Range(0f, 1f)]
    public float blockedStripeOpacity =
        0.35f;

    [Min(0.01f)]
    public float blockedStripeDensity =
        10f;

    [Range(0.01f, 0.45f)]
    public float blockedStripeWidth =
        0.12f;


    [Header("Segment Block Debug")]

    [Tooltip(
        "0-based segment index used by the two DEBUG context-menu commands."
    )]
    [Min(0)]
    [SerializeField]
    private int debugSegmentIndex =
        0;

    [Tooltip(
        "Number of future VALID spins used by DEBUG - Block Selected Segment."
    )]
    [Min(1)]
    [SerializeField]
    private int debugBlockDurationSpins =
        2;

    [Header("Pins (ticks)")]
    public bool generatePins = true;
    public float pinRadiusOffset = -0.05f;
    public Vector2 pinSize = new Vector2(0.08f, 0.30f);
    public bool pinsAreTriggers = true;
    public string pinsParentName = "Pins";

    [Header("Runtime Stickers")]
    [Tooltip("Contenedor fuera de la rueda donde aparcamos stickers al regenerar.")]
    public Transform dynamicStickerContainer;

    [Tooltip("Referencia al FlagPin para NO destruirlo al regenerar.")]
    public FlagPin flagPin;

    [HideInInspector]
    public List<WheelSegmentData> segments = new List<WheelSegmentData>();

    // ------------------------------------------------------------
    // Info temporal para restaurar stickers tras regenerar
    // ------------------------------------------------------------

    private struct StickerRegenInfo
    {
        public Transform root;
        public int segmentIndex;
    }

    private readonly List<StickerRegenInfo> stickerRegenInfos =
        new List<StickerRegenInfo>();


    // =====================================================================
    // SEGMENT VISUAL / GAMEPLAY RUNTIME STATE
    // =====================================================================

    /*
     * Block state is stored by INDEX, never by Segment_X Transform.
     *
     * Value = how many FUTURE VALID spins still have to be played while
     * this segment remains blocked.
     *
     * WheelShifter destroys and recreates every Segment_X GameObject.
     * Keeping the state here means both the block AND its remaining duration
     * survive wheel regeneration automatically.
     */
    private readonly Dictionary<int, int> blockedSegmentSpinsRemaining =
        new Dictionary<int, int>();


    /*
     * Every generated segment can share this one runtime material.
     * Per-segment color / blocked state is supplied with
     * MaterialPropertyBlock by SegmentMesh.
     */
    private Material runtimeSegmentMaterial;

    private Shader runtimeSegmentMaterialShader;

    // =====================================================================
    // NORMALIZACIÓN DE ÁNGULOS = SIEMPRE 360º
    // =====================================================================

    void NormalizeAngles()
    {
        if (segmentAngles == null)
            segmentAngles = new List<float>();

        // Si el tamaño de la lista no coincide con el número
        // de segmentos, la reseteamos.
        if (segmentAngles.Count != segmentCount)
        {
            segmentAngles.Clear();

            float def = 360f / segmentCount;

            for (int i = 0; i < segmentCount; i++)
                segmentAngles.Add(def);

            return;
        }

        float sum = 0f;

        foreach (var a in segmentAngles)
            sum += a;

        // Si ya suman 360 (con tolerancia), no tocamos nada.
        if (Mathf.Abs(sum - 360f) < 0.01f)
            return;

        // Reescalamos proporcionalmente.
        float correction = 360f / sum;

        for (int i = 0; i < segmentAngles.Count; i++)
            segmentAngles[i] *= correction;
    }

    // =====================================================================
    // SCALE SEGMENT
    // =====================================================================

    public void ScaleSegmentAngle(int index, float factor)
    {
        if (index < 0 || index >= segmentAngles.Count)
            return;

        segmentAngles[index] *= factor;

        NormalizeAngles();

        GenerateWheel();
    }

    // =====================================================================
    // MAIN GENERATION
    // =====================================================================

    [ContextMenu("Generate Wheel")]
    public void GenerateWheel()
    {
        NormalizeAngles();

        // 1) Guardar stickers ANTES de destruir la rueda actual.
        SaveStickersBeforeGeneration();

        // 2) Destruir hijos actuales respetando el FlagPin.
        ForceDestroyChildren();

        // 3) Limpiar datos y regenerar segmentos.
        segments.Clear();

        Transform root =
            new GameObject("Segments").transform;

        root.SetParent(transform, false);

        int segmentLayer =
            LayerMask.NameToLayer("Segment");

        int pinLayer =
            LayerMask.NameToLayer("Pin");

        float angleStart = 0f;

        Material sharedSegmentMaterial =
            GetOrCreateSegmentMaterial();


        RemoveInvalidBlockedIndices();


        for (int i = 0; i < segmentCount; i++)
        {
            float angle = segmentAngles[i];

            GameObject segGO =
                new GameObject("Segment_" + i);

            segGO.transform.SetParent(root, false);

            // Rotación acumulada del segmento.
            segGO.transform.localRotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    angleStart
                );

            // ---------------------------------------------------------
            // MESH FILTER
            // ---------------------------------------------------------

            MeshFilter mf =
                segGO.GetComponent<MeshFilter>();

            if (mf == null)
                mf = segGO.AddComponent<MeshFilter>();

            // ---------------------------------------------------------
            // MESH RENDERER
            // ---------------------------------------------------------

            MeshRenderer mr =
                segGO.GetComponent<MeshRenderer>();

            if (mr == null)
                mr = segGO.AddComponent<MeshRenderer>();

            // ---------------------------------------------------------
            // SEGMENT MESH
            // ---------------------------------------------------------

            SegmentMesh sm =
                segGO.GetComponent<SegmentMesh>();

            if (sm == null)
                sm = segGO.AddComponent<SegmentMesh>();

            sm.angle = angle;
            sm.radius = radius;
            sm.resolution = meshResolution;

            /*
             * Needed only for wheel-space UV generation.
             * It makes pattern / hatch density independent from wedge size.
             */
            sm.wheelAngleOffset =
                angleStart;

            sm.color =
                Color.HSVToRGB(
                    i / (float)segmentCount,
                    0.85f,
                    1f
                );


            sm.SetVisualMaterial(
                sharedSegmentMaterial
            );


            sm.ConfigureCosmeticPattern(
                cosmeticPatternTexture,
                cosmeticPatternColor,
                cosmeticPatternOpacity,
                cosmeticPatternScale,
                cosmeticPatternRotation
            );


            sm.ConfigureBlockedVisual(
                blockedBaseColor,
                blockedBaseBlend,
                blockedStripeColor,
                blockedStripeOpacity,
                blockedStripeDensity,
                blockedStripeWidth
            );


            sm.SetBlocked(
                blockedSegmentSpinsRemaining
                    .ContainsKey(i)
            );


            sm.GenerateMesh();

            mr.sortingLayerName =
                sortingLayerName;

            mr.sortingOrder =
                sortingOrder;

            // ---------------------------------------------------------
            // COLLIDER DEL SEGMENTO
            // ---------------------------------------------------------

            PolygonCollider2D poly =
                segGO.GetComponent<PolygonCollider2D>();

            if (poly == null)
                poly =
                    segGO.AddComponent<PolygonCollider2D>();

            poly.isTrigger = true;

            List<Vector2> pts =
                new List<Vector2>();

            // Centro de la ruleta.
            pts.Add(Vector2.zero);

            int res =
                Mathf.Max(
                    3,
                    meshResolution
                );

            for (int k = 0; k <= res; k++)
            {
                float t =
                    k / (float)res;

                float a =
                    t *
                    angle *
                    Mathf.Deg2Rad;

                pts.Add(
                    new Vector2(
                        Mathf.Cos(a) * radius,
                        Mathf.Sin(a) * radius
                    )
                );
            }

            poly.SetPath(
                0,
                pts.ToArray()
            );

            if (segmentLayer != -1)
                segGO.layer = segmentLayer;

            segments.Add(
                new WheelSegmentData
                {
                    index = i,
                    collider = poly,
                    meshComponent = sm
                }
            );

            angleStart += angle;
        }

        // 4) Generar pins.
        if (generatePins)
            GeneratePins(pinLayer);

        /*
         * IMPORTANTE:
         *
         * Hemos creado y movido bastantes Collider2D en este mismo frame.
         * Sincronizamos antes de restaurar los stickers para asegurarnos
         * de que las consultas físicas utilizan la geometría nueva.
         */
        Physics2D.SyncTransforms();

        // 5) Restaurar stickers sobre la nueva rueda.
        RestoreStickersAfterGeneration();

        /*
         * Volvemos a sincronizar porque los stickers también han cambiado
         * de parent/transform.
         */
        Physics2D.SyncTransforms();

        /*
         * WheelGenerator YA NO decide si un sticker es válido.
         *
         * StickerPlacementValidator llama a BaseSticker.DebugCheckSegment(),
         * que a su vez utiliza StickerPlacementUtility.
         *
         * Por tanto tenemos una única fuente de verdad.
         */
        StickerPlacementValidator.Instance?
            .ValidateAfterWheelRegeneration();
    }

    // =====================================================================
    // GUARDAR STICKERS ANTES DE REGENERAR
    // =====================================================================

    void SaveStickersBeforeGeneration()
    {
        stickerRegenInfos.Clear();

        // Asegurar que existe un contenedor dinámico.
        if (dynamicStickerContainer == null)
        {
            var go =
                new GameObject(
                    "DynamicStickerContainer"
                );

            dynamicStickerContainer =
                go.transform;
        }

        BaseSticker[] allStickers =
            FindObjectsOfType<BaseSticker>(true);

        foreach (BaseSticker s in allStickers)
        {
            if (s == null)
                continue;

            /*
             * A sticker that has just spent its final use is already
             * logically gone, even though Unity defers Destroy() until
             * the end of the frame.
             *
             * This matters especially for WheelShifter / Magic Bean:
             * it can consume its last use and regenerate the wheel during
             * that very same activation. We must NOT preserve / restore
             * that dying sticker onto the freshly generated wheel.
             *
             * We intentionally use BaseSticker's EXISTING public use API
             * here, so no BaseSticker references or contracts change.
             */
            if (s.HasLimitedUses &&
                s.RemainingUses <= 0)
            {
                continue;
            }

            // Solo los stickers colocados en la ruleta.
            if (!s.isPlaced ||
                s.currentSegment == null)
            {
                continue;
            }

            int segIdx =
                ExtractIndexFromName(
                    s.currentSegment.name
                );

            if (segIdx < 0)
                continue;

            /*
             * Usamos stickerRoot como autoridad.
             *
             * Conservamos el fallback para no romper stickers antiguos
             * que pudieran no tenerlo asignado correctamente.
             */
            Transform root =
                s.stickerRoot != null
                    ? s.stickerRoot
                    : (
                        s.transform.parent != null
                            ? s.transform.parent
                            : s.transform
                    );

            stickerRegenInfos.Add(
                new StickerRegenInfo
                {
                    root = root,
                    segmentIndex = segIdx
                }
            );

            /*
             * Sacamos el sticker fuera de la rueda ANTES
             * de destruir los segmentos.
             *
             * Mantenemos posición y rotación mundiales.
             */
            root.SetParent(
                dynamicStickerContainer,
                true
            );
        }

        Physics2D.SyncTransforms();
    }

    // =====================================================================
    // GET SEGMENT INDEX
    // =====================================================================

    // Se mantiene por si queremos utilizarlo para debug
    // o eliminar dependencia del nombre Segment_X más adelante.
    int GetSegmentIndexFromSegmentTransform(
        Transform segTransform)
    {
        if (segTransform == null)
            return -1;

        for (int i = 0;
             i < segments.Count;
             i++)
        {
            if (segments[i].collider == null)
                continue;

            if (segments[i].collider.transform ==
                segTransform)
            {
                return segments[i].index;
            }
        }

        return -1;
    }

    // =====================================================================
    // RESTAURAR STICKERS DESPUÉS DE REGENERAR
    // =====================================================================

    void RestoreStickersAfterGeneration()
    {
        /*
         * IMPORTANTE:
         *
         * Aquí SOLO restauramos.
         *
         * Antes WheelGenerator restauraba un sticker e inmediatamente
         * lo validaba mientras otros stickers todavía no estaban
         * restaurados. Eso podía hacer que el resultado dependiese
         * del orden del foreach.
         *
         * Ahora:
         *
         * 1. Restauramos TODOS.
         * 2. Sincronizamos physics.
         * 3. StickerPlacementValidator valida TODOS.
         */

        foreach (var info in stickerRegenInfos)
        {
            Transform root =
                info.root;

            if (root == null)
                continue;

            BaseSticker s =
                root.GetComponentInChildren<BaseSticker>();

            if (s == null)
                continue;

            if (info.segmentIndex < 0 ||
                info.segmentIndex >= segments.Count)
            {
                Debug.LogWarning(
                    $"Sticker '{s.name}' lost its segment index after wheel regen."
                );

                continue;
            }

            WheelSegmentData segData =
                segments[info.segmentIndex];

            if (segData.collider == null)
            {
                Debug.LogWarning(
                    $"Sticker '{s.name}' has invalid segment collider after wheel regen."
                );

                continue;
            }

            Transform segTransform =
                segData.collider.transform;

            // Re-parent al nuevo segmento manteniendo world transform.
            root.SetParent(
                segTransform,
                true
            );

            // Actualizar estado lógico.
            s.currentSegment =
                segTransform;

            s.isPlaced =
                true;

            s.OnRestoredAfterWheelRegen();
        }

        /*
         * Muy importante antes de que otro sistema consulte
         * Collider2D.Distance / OverlapPoint / ClosestPoint.
         */
        Physics2D.SyncTransforms();

        stickerRegenInfos.Clear();
    }

    // =====================================================================
    // SEGMENT BLOCK STATE
    // =====================================================================

    public bool IsSegmentBlocked(
        int index)
    {
        if (index < 0 ||
            index >= segmentCount)
        {
            return false;
        }


        return
            blockedSegmentSpinsRemaining
                .ContainsKey(index);
    }


    public bool IsSegmentBlocked(
        Transform segmentTransform)
    {
        int index =
            GetSegmentIndex(
                segmentTransform
            );


        return
            IsSegmentBlocked(
                index
            );
    }


    /// <summary>
    /// Returns the number of FUTURE VALID spins for which this segment will
    /// remain blocked.
    ///
    /// 0 means the segment is currently not blocked.
    /// </summary>
    public int GetSegmentBlockRemainingSpins(
        int index)
    {
        if (!IsSegmentBlocked(index))
            return 0;


        return
            Mathf.Max(
                0,
                blockedSegmentSpinsRemaining[index]
            );
    }


    public int GetSegmentIndex(
        Transform segmentTransform)
    {
        int index =
            GetSegmentIndexFromSegmentTransform(
                segmentTransform
            );


        if (index < 0)
        {
            index =
                ExtractIndexFromName(
                    segmentTransform != null
                        ? segmentTransform.name
                        : null
                );
        }


        return
            index;
    }


    /// <summary>
    /// Blocks an UNBLOCKED segment for a fixed number of FUTURE VALID spins.
    ///
    /// Returns false if:
    /// - the index is invalid;
    /// - duration is invalid;
    /// - the segment is already blocked.
    ///
    /// Existing blocks are intentionally NEVER refreshed by another action.
    /// This keeps Random mode and Winning fallback behaviour deterministic.
    /// </summary>
    public bool BlockSegment(
        int index,
        int durationSpins)
    {
        if (index < 0 ||
            index >= segmentCount ||
            durationSpins <= 0)
        {
            return false;
        }


        if (IsSegmentBlocked(index))
            return false;


        blockedSegmentSpinsRemaining[index] =
            Mathf.Max(
                1,
                durationSpins
            );


        ApplyBlockedVisualToSegment(
            index
        );


        /*
         * Becoming blocked changes placement-validity semantics:
         * stickers already inside this segment are now frozen and are allowed
         * to cross the resized boundary until the block expires.
         *
         * A WheelShifter can regenerate/validate the wheel earlier in the same
         * spin, before enemy actions apply this NEW block. Without revalidating
         * here, WrongStickerPanel can remain in a stale locked state until the
         * player happens to move any sticker.
         */
        Physics2D.SyncTransforms();

        StickerPlacementValidator.Instance?
            .ValidateAfterWheelRegeneration();


        Debug.Log(
            $"[SEGMENT BLOCK] Segment {index + 1} blocked for " +
            $"{durationSpins} future valid spin(s)."
        );


        return true;
    }


    /// <summary>
    /// Adds FUTURE VALID spins to an ALREADY BLOCKED segment.
    ///
    /// The block is represented by one shared remaining-turn counter.
    /// "Max stacks" therefore translates to a maximum remaining duration:
    ///
    ///     maxRemainingSpins = durationPerStack * maxStacks
    ///
    /// Example:
    /// durationPerStack = 2
    /// maxStacks = 3
    /// maximum counter = 6 turns
    ///
    /// If the segment currently has 5 turns and another 2-turn stack lands,
    /// only +1 turn is added so the counter stops at 6.
    ///
    /// Returns the number of turns ACTUALLY added.
    /// 0 means nothing changed (invalid target, not blocked, or already capped).
    /// </summary>
    public int AddSegmentBlockSpins(
        int index,
        int additionalSpins,
        int maxRemainingSpins)
    {
        if (index < 0 ||
            index >= segmentCount ||
            additionalSpins <= 0 ||
            maxRemainingSpins <= 0)
        {
            return 0;
        }


        if (!IsSegmentBlocked(index))
            return 0;


        int currentRemaining =
            blockedSegmentSpinsRemaining[index];


        if (currentRemaining >=
            maxRemainingSpins)
        {
            return 0;
        }


        int targetRemaining =
            Mathf.Min(
                maxRemainingSpins,
                currentRemaining +
                    additionalSpins
            );


        int actuallyAdded =
            targetRemaining -
            currentRemaining;


        if (actuallyAdded <= 0)
            return 0;


        blockedSegmentSpinsRemaining[index] =
            targetRemaining;


        Debug.Log(
            $"[SEGMENT BLOCK] Segment {index + 1} stacked: " +
            $"+{actuallyAdded} future valid spin(s), " +
            $"{targetRemaining} remaining."
        );


        return
            actuallyAdded;
    }


    /*
     * Kept for the existing debug context-menu workflow.
     */
    public bool BlockSegment(
        int index)
    {
        return
            BlockSegment(
                index,
                debugBlockDurationSpins
            );
    }


    /// <summary>
    /// Called ONCE after sticker resolution of every VALID spin and BEFORE
    /// enemy actions execute.
    ///
    /// This timing is deliberate:
    ///
    /// - A segment with 1 turn remaining still suppresses stickers on the
    ///   current spin.
    /// - It unlocks immediately AFTER those sticker effects were skipped.
    /// - A SegmentBlock enemy action executed later in the same resolution
    ///   creates a fresh block at its full authored duration and is NOT
    ///   decremented immediately.
    /// </summary>
    public void AdvanceSegmentBlockDurationsAfterValidSpin()
    {
        if (blockedSegmentSpinsRemaining.Count == 0)
            return;


        List<int> indices =
            new List<int>(
                blockedSegmentSpinsRemaining.Keys
            );


        List<int> unlockedIndices =
            new List<int>();


        foreach (int index in indices)
        {
            int remaining =
                blockedSegmentSpinsRemaining[index] - 1;


            if (remaining <= 0)
            {
                blockedSegmentSpinsRemaining
                    .Remove(index);

                unlockedIndices.Add(
                    index
                );

                ApplyBlockedVisualToSegment(
                    index
                );
            }
            else
            {
                blockedSegmentSpinsRemaining[index] =
                    remaining;
            }
        }


        if (unlockedIndices.Count == 0)
            return;


        /*
         * A WheelShifter may have changed the geometry of a frozen segment.
         * Once it becomes movable again, immediately restore normal placement
         * validation.
         */
        Physics2D.SyncTransforms();

        StickerPlacementValidator.Instance?
            .ValidateAfterWheelRegeneration();


        foreach (int index in unlockedIndices)
        {
            LogSegmentUnlocked(
                index
            );


            Debug.Log(
                $"[SEGMENT BLOCK] Segment {index + 1} unlocked."
            );
        }
    }


    public void ClearAllSegmentBlocks()
    {
        if (blockedSegmentSpinsRemaining.Count == 0)
            return;


        blockedSegmentSpinsRemaining.Clear();


        if (segments != null)
        {
            foreach (WheelSegmentData segment in
                     segments)
            {
                if (segment == null ||
                    segment.meshComponent == null)
                {
                    continue;
                }


                segment.meshComponent
                    .SetBlocked(
                        false
                    );
            }
        }


        Physics2D.SyncTransforms();

        StickerPlacementValidator.Instance?
            .ValidateAfterWheelRegeneration();


        Debug.Log(
            "[SEGMENT BLOCK] All segment blocks cleared."
        );
    }


    private void ApplyBlockedVisualToSegment(
        int index)
    {
        if (segments == null ||
            index < 0 ||
            index >= segments.Count)
        {
            return;
        }


        WheelSegmentData segment =
            segments[index];


        if (segment == null ||
            segment.meshComponent == null)
        {
            return;
        }


        segment.meshComponent
            .SetBlocked(
                IsSegmentBlocked(index)
            );
    }


    private void RemoveInvalidBlockedIndices()
    {
        if (blockedSegmentSpinsRemaining.Count == 0)
            return;


        List<int> invalid =
            new List<int>();


        foreach (int index in
                 blockedSegmentSpinsRemaining.Keys)
        {
            if (index < 0 ||
                index >= segmentCount)
            {
                invalid.Add(
                    index
                );
            }
        }


        foreach (int index in
                 invalid)
        {
            blockedSegmentSpinsRemaining
                .Remove(index);
        }
    }


    private void LogSegmentUnlocked(
        int index)
    {
        if (GameLogManager.Instance == null)
            return;


        string segmentLabel =
            GameLogManager.Instance
                .SegmentText(
                    $"Segment {index + 1}"
                );


        if (segments != null &&
            index >= 0 &&
            index < segments.Count &&
            segments[index] != null &&
            segments[index].meshComponent != null)
        {
            segmentLabel =
                GameLogManager.Instance
                    .SegmentText(
                        $"Segment {index + 1}",
                        segments[index]
                            .meshComponent
                            .color
                    );
        }


        GameLogManager.Instance
            .AddGameplayLine(
                segmentLabel +
                " unlocks"
            );
    }


    // =====================================================================
    // SEGMENT VISUAL MATERIAL
    // =====================================================================

    private Material GetOrCreateSegmentMaterial()
    {
        if (segmentVisualShader == null)
            return null;


        if (runtimeSegmentMaterial != null &&
            runtimeSegmentMaterialShader ==
                segmentVisualShader)
        {
            return
                runtimeSegmentMaterial;
        }


        DestroyRuntimeSegmentMaterial();


        runtimeSegmentMaterialShader =
            segmentVisualShader;

        runtimeSegmentMaterial =
            new Material(
                segmentVisualShader
            );

        runtimeSegmentMaterial.name =
            "Runtime_SegmentVisual";

        runtimeSegmentMaterial.hideFlags =
            HideFlags.DontSave;


        return
            runtimeSegmentMaterial;
    }


    private void DestroyRuntimeSegmentMaterial()
    {
        if (runtimeSegmentMaterial == null)
            return;


#if UNITY_EDITOR

        if (!Application.isPlaying)
        {
            DestroyImmediate(
                runtimeSegmentMaterial
            );
        }
        else
        {
            Destroy(
                runtimeSegmentMaterial
            );
        }

#else

        Destroy(
            runtimeSegmentMaterial
        );

#endif


        runtimeSegmentMaterial =
            null;

        runtimeSegmentMaterialShader =
            null;
    }


    // =====================================================================
    // DEBUG - SEGMENT BLOCK
    // =====================================================================

    [ContextMenu(
        "DEBUG - Block Selected Segment"
    )]
    private void DebugBlockSelectedSegment()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "[SEGMENT BLOCK] Enter Play Mode first."
            );

            return;
        }


        BlockSegment(
            debugSegmentIndex
        );
    }


    [ContextMenu(
        "DEBUG - Clear Segment Blocks"
    )]
    private void DebugClearSegmentBlocks()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "[SEGMENT BLOCK] Enter Play Mode first."
            );

            return;
        }


        ClearAllSegmentBlocks();
    }


    // =====================================================================
    // GENERAR PINS
    // =====================================================================

    void GeneratePins(int pinLayer)
    {
        Transform old =
            transform.Find(pinsParentName);

        if (old != null)
        {
#if UNITY_EDITOR

            if (!Application.isPlaying)
            {
                DestroyImmediate(
                    old.gameObject
                );
            }
            else
            {
                /*
                 * Destroy() es diferido hasta final de frame.
                 * Lo desactivamos primero para que sus colliders
                 * no sigan participando en Physics2D.
                 */
                old.gameObject.SetActive(false);

                Destroy(
                    old.gameObject
                );
            }

#else

            old.gameObject.SetActive(false);

            Destroy(
                old.gameObject
            );

#endif
        }

        Transform pinsRoot =
            new GameObject(
                pinsParentName
            ).transform;

        pinsRoot.SetParent(
            transform,
            false
        );

        float angleAccum = 0f;

        float r =
            radius +
            pinRadiusOffset;

        for (int i = 0;
             i < segmentCount;
             i++)
        {
            float mid =
                angleAccum;

            float rad =
                mid *
                Mathf.Deg2Rad;

            Vector3 pos =
                new Vector3(
                    Mathf.Cos(rad) * r,
                    Mathf.Sin(rad) * r,
                    0f
                );

            GameObject pin =
                new GameObject(
                    "Pin_" + i
                );

            pin.transform.SetParent(
                pinsRoot,
                false
            );

            pin.transform.localPosition =
                pos;

            pin.transform.localRotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    mid
                );

            BoxCollider2D bc =
                pin.AddComponent<BoxCollider2D>();

            bc.isTrigger =
                pinsAreTriggers;

            bc.size =
                pinSize;

            if (pinLayer != -1)
                pin.layer = pinLayer;

            angleAccum +=
                segmentAngles[i];
        }
    }

    // =====================================================================
    // DESTROY CHILDREN SIN CARGARSE EL FLAGPIN
    // =====================================================================

    void ForceDestroyChildren()
    {
        List<Transform> toDestroy =
            new List<Transform>();

        foreach (Transform t in transform)
        {
            /*
             * No destruimos un branch que contenga
             * el FlagPin.
             */
            if (t.GetComponentInChildren<FlagPin>(true)
                != null)
            {
                continue;
            }

            toDestroy.Add(t);
        }

        foreach (var t in toDestroy)
        {
            if (t == null)
                continue;

#if UNITY_EDITOR

            if (!Application.isPlaying)
            {
                DestroyImmediate(
                    t.gameObject
                );
            }
            else
            {
                /*
                 * Destroy es diferido.
                 *
                 * Desactivar antes evita que segmentos/pins viejos
                 * sigan participando en la física durante este frame.
                 */
                t.gameObject.SetActive(false);

                Destroy(
                    t.gameObject
                );
            }

#else

            t.gameObject.SetActive(false);

            Destroy(
                t.gameObject
            );

#endif
        }
    }

    // =====================================================================
    // UNITY CLEANUP
    // =====================================================================

    private void OnDestroy()
    {
        DestroyRuntimeSegmentMaterial();
    }


    // =====================================================================
    // START
    // =====================================================================

    void Start()
    {
        if (regenerateOnPlay)
            GenerateWheel();
    }

    // =====================================================================
    // SACAR ÍNDICE DEL NOMBRE "Segment_X"
    // =====================================================================

    int ExtractIndexFromName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return -1;

        if (!name.StartsWith("Segment_"))
            return -1;

        string number =
            name.Substring(
                "Segment_".Length
            );

        if (int.TryParse(
                number,
                out int idx))
        {
            return idx;
        }

        return -1;
    }
}