using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyCorridorController : MonoBehaviour
{
    // =========================================================
    // ROWS
    // =========================================================

    [Header("Rows")]

    [Tooltip("Enemies currently being fought.")]
    public Transform currentRow;

    [Tooltip("Enemies that will become the next encounter.")]
    public Transform nextRow;

    [Tooltip("Enemies waiting furthest in the corridor.")]
    public Transform futureRow;


    // =========================================================
    // ENCOUNTER GENERATION
    // =========================================================

    [Header("Encounter Generation")]

    [Tooltip(
        "Decides WHICH enemies belong to each encounter. " +
        "The Corridor only handles rows, movement and placement."
    )]
    public EnemyEncounterGenerator encounterGenerator;

    [Tooltip(
        "If true, Current / Next / Future are generated when the scene starts."
    )]
    public bool randomizeInitialRowsOnStart =
        true;

    [Tooltip(
        "If true, the recycled Future row receives a newly generated encounter."
    )]
    public bool regenerateFutureRowOnAdvance =
        true;


    // =========================================================
    // ROW VISUALS
    // =========================================================

    [Header("Row Visuals")]

    [Tooltip(
        "Brightness multiplier for enemies in CurrentRow."
    )]
    [Range(0f, 1f)]
    public float currentBrightness = 1f;


    [Space]

    [Tooltip(
        "Brightness multiplier for enemies in NextRow."
    )]
    [Range(0f, 1f)]
    public float nextBrightness = 0.55f;


    [Space]

    [Tooltip(
        "Brightness multiplier for enemies in FutureRow."
    )]
    [Range(0f, 1f)]
    public float futureBrightness = 0.25f;


    // =========================================================
    // MOVEMENT
    // =========================================================

    [Header("Movement")]

    [Tooltip(
        "Time taken for Next and Future rows to advance one step."
    )]
    [Min(0.01f)]
    public float advanceDuration = 0.65f;


    // =========================================================
    // DEBUG
    // =========================================================

    [Header("Debug")]

    [Tooltip(
        "Allows testing the corridor transition with a keyboard key."
    )]
    public bool enableDebugKey = true;

    public KeyCode debugAdvanceKey = KeyCode.N;


    // =========================================================
    // PUBLIC STATE
    // =========================================================

    public bool IsAdvancing
    {
        get;
        private set;
    }


    public Transform CurrentRow
    {
        get { return currentRow; }
    }


    public Transform NextRow
    {
        get { return nextRow; }
    }


    public Transform FutureRow
    {
        get { return futureRow; }
    }


    // =========================================================
    // INTERNAL
    // =========================================================

    private Vector3 currentSlotPosition;
    private Vector3 nextSlotPosition;
    private Vector3 futureSlotPosition;


    /*
     * Pool-round authoring is intentionally 1-based:
     *
     * 1 = first playable round = UI R-0
     * 2 = second playable round = UI R-1
     * 3 = third playable round = UI R-2
     *
     * At scene start we pre-generate rounds 1, 2 and 3 into
     * Current / Next / Future. Every recycled FutureRow then consumes the
     * next number from this counter.
     */
    private int nextPoolRoundToGenerate =
        4;


    /*
     * Each row uses its DIRECT children as persistent enemy Slots.
     *
     * Expected hierarchy:
     *
     * Row
     * ├ Slot_L
     * ├ Slot_C
     * └ Slot_R
     *
     * The Slots stay forever. Enemy prefab instances are created and
     * destroyed underneath them.
     *
     * This means the scene no longer needs placeholder Imps in the Slots.
     */
    private readonly Dictionary<Transform, List<Transform>>
        rowSpawnSlots =
            new Dictionary<Transform, List<Transform>>();


    /*
     * A row first rolls its enemy PREFABS, then sorts those rolled choices
     * by placement priority before instantiating them into Slot_L/C/R.
     *
     * randomTieBreaker makes equal-priority enemies random relative to one
     * another instead of relying on List.Sort implementation details.
     */
    private class GeneratedEnemyChoice
    {
        public GameObject prefab;
        public int placementPriority;
        public float randomTieBreaker;
    }


    /*
     * Row dimming must be reversible.
     *
     * We therefore remember the original tint/alpha of every SpriteRenderer
     * before applying Current / Next / Future multipliers.
     */
    private readonly Dictionary<SpriteRenderer, Color>
        baseSpriteColors =
            new Dictionary<SpriteRenderer, Color>();


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        CaptureSlotPositions();

        CaptureRowSpawnSlots();


        if (encounterGenerator == null)
        {
            encounterGenerator =
                FindObjectOfType<EnemyEncounterGenerator>();
        }


        if (randomizeInitialRowsOnStart)
        {
            /*
             * We are generating THREE future encounters at once, so each
             * row must filter the enemy pool using the round that row
             * actually represents, not simply the current game round.
             *
             * Current = playable round 1 (R-0)
             * Next    = playable round 2 (R-1)
             * Future  = playable round 3 (R-2)
             */
            RegenerateRowWithFreshEnemies(
                currentRow,
                1
            );

            RegenerateRowWithFreshEnemies(
                nextRow,
                2
            );

            RegenerateRowWithFreshEnemies(
                futureRow,
                3
            );
        }


        nextPoolRoundToGenerate =
            4;


        RegisterBaseColorsForRow(
            currentRow
        );

        RegisterBaseColorsForRow(
            nextRow
        );

        RegisterBaseColorsForRow(
            futureRow
        );
    }


    private void Start()
    {
        ApplyCombatStates();

        ApplyRowVisualStates();
    }


    private void Update()
    {
#if UNITY_EDITOR || UNITY_STANDALONE

        if (!enableDebugKey ||
            IsAdvancing)
        {
            return;
        }


        if (Input.GetKeyDown(
            debugAdvanceKey))
        {
            AdvanceEncounter();
        }

#endif
    }


    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void CaptureSlotPositions()
    {
        /*
         * Your row positions in the hierarchy are the source of truth.
         * Nothing is duplicated here.
         */
        if (currentRow != null)
        {
            currentSlotPosition =
                currentRow.localPosition;
        }


        if (nextRow != null)
        {
            nextSlotPosition =
                nextRow.localPosition;
        }


        if (futureRow != null)
        {
            futureSlotPosition =
                futureRow.localPosition;
        }
    }


    private void CaptureRowSpawnSlots()
    {
        CacheSpawnSlotsForRow(
            currentRow
        );

        CacheSpawnSlotsForRow(
            nextRow
        );

        CacheSpawnSlotsForRow(
            futureRow
        );
    }


    private void CacheSpawnSlotsForRow(
        Transform row)
    {
        if (row == null ||
            rowSpawnSlots.ContainsKey(row))
        {
            return;
        }


        List<Transform> slots =
            new List<Transform>();


        /*
         * Every DIRECT child of a Row is treated as an enemy Slot.
         *
         * With the current setup these are Slot_L / Slot_C / Slot_R.
         */
        foreach (Transform child in row)
        {
            if (child == null)
                continue;


            slots.Add(
                child
            );
        }


        /*
         * Deterministic left-to-right slot order.
         *
         * The PREFAB selected for each slot is still random, so the visual
         * order of enemy types changes every generated row.
         */
        slots.Sort(
            (a, b) =>
                a.localPosition.x
                    .CompareTo(
                        b.localPosition.x
                    )
        );


        rowSpawnSlots[row] =
            slots;


        Debug.Log(
            $"[ENEMY CORRIDOR] Cached " +
            $"{slots.Count} enemy slot(s) for " +
            $"{row.name}."
        );
    }


    // =========================================================
    // PUBLIC API
    // =========================================================

    public int GetLivingEnemyCountInCurrentRow()
    {
        if (currentRow == null)
            return 0;


        BaseEnemy[] enemies =
            currentRow.GetComponentsInChildren<BaseEnemy>(
                true
            );


        int livingCount =
            0;


        foreach (BaseEnemy enemy in enemies)
        {
            if (enemy == null ||
                enemy.IsDead ||
                !enemy.gameObject.activeInHierarchy)
            {
                continue;
            }


            livingCount++;
        }


        return livingCount;
    }


    public bool IsCurrentEncounterCleared()
    {
        return
            GetLivingEnemyCountInCurrentRow() <=
            0;
    }


    /*
     * Manual/debug advance.
     *
     * This keeps the old protection against changing rows during a spin.
     */
    public void AdvanceEncounter()
    {
        TryAdvanceEncounter(
            false
        );
    }


    /*
     * RoundManager calls this only after the previous round has completely
     * resolved.
     *
     * In scenes without a Reward Phase, StartNextRound can technically be
     * reached while RouletteController is still finishing the final frame
     * of spin resolution. The coroutine itself moves on the following
     * frames, so this automatic transition is safe to queue here.
     */
    public void AdvanceEncounterForRoundTransition()
    {
        TryAdvanceEncounter(
            true
        );
    }


    private void TryAdvanceEncounter(
        bool ignoreSpinInProgress)
    {
        if (IsAdvancing)
            return;


        if (!ignoreSpinInProgress &&
            RouletteController.Instance != null &&
            RouletteController.Instance.SpinInProgress)
        {
            Debug.Log(
                "[ENEMY CORRIDOR] Cannot advance while a spin is in progress."
            );

            return;
        }


        if (currentRow == null ||
            nextRow == null ||
            futureRow == null)
        {
            Debug.LogWarning(
                "[ENEMY CORRIDOR] Row references are missing."
            );

            return;
        }


        StartCoroutine(
            AdvanceRoutine()
        );
    }


    // =========================================================
    // TRANSITION
    // =========================================================

    private IEnumerator AdvanceRoutine()
    {
        IsAdvancing =
            true;


        Transform oldCurrent =
            currentRow;

        Transform oldNext =
            nextRow;

        Transform oldFuture =
            futureRow;


        SetRowCombatActive(
            oldCurrent,
            false
        );

        SetRowCombatActive(
            oldNext,
            false
        );

        SetRowCombatActive(
            oldFuture,
            false
        );


        /*
         * In real gameplay Current will normally be empty/dead already.
         * For debug transitions we hide it immediately before recycling it.
         */
        oldCurrent.gameObject
            .SetActive(false);


        Vector3 nextStart =
            oldNext.localPosition;

        Vector3 futureStart =
            oldFuture.localPosition;


        float elapsed =
            0f;


        while (elapsed <
               advanceDuration)
        {
            elapsed +=
                Time.deltaTime;


            float t =
                Mathf.Clamp01(
                    elapsed /
                    advanceDuration
                );


            float smoothT =
                t *
                t *
                (
                    3f -
                    2f * t
                );


            // -------------------------------------------------
            // PHYSICAL ADVANCE
            // -------------------------------------------------

            oldNext.localPosition =
                Vector3.Lerp(
                    nextStart,
                    currentSlotPosition,
                    smoothT
                );


            oldFuture.localPosition =
                Vector3.Lerp(
                    futureStart,
                    nextSlotPosition,
                    smoothT
                );


            // -------------------------------------------------
            // VISUAL ADVANCE
            // -------------------------------------------------

            /*
             * As the enemies physically approach the player, they also
             * emerge from the darkness.
             *
             * Only RGB brightness changes. Alpha stays untouched so
             * overlapping rows remain visually solid.
             *
             * No fake scale is involved: perspective still controls size.
             */
            SetRowBrightness(
                oldNext,
                Mathf.Lerp(
                    nextBrightness,
                    currentBrightness,
                    smoothT
                )
            );


            SetRowBrightness(
                oldFuture,
                Mathf.Lerp(
                    futureBrightness,
                    nextBrightness,
                    smoothT
                )
            );


            yield return null;
        }


        oldNext.localPosition =
            currentSlotPosition;

        oldFuture.localPosition =
            nextSlotPosition;


        oldCurrent.localPosition =
            futureSlotPosition;


        currentRow =
            oldNext;

        nextRow =
            oldFuture;

        futureRow =
            oldCurrent;


        // -----------------------------------------------------
        // FRESH FUTURE ENCOUNTER
        // -----------------------------------------------------

        if (regenerateFutureRowOnAdvance)
        {
            RegenerateRowWithFreshEnemies(
                futureRow,
                nextPoolRoundToGenerate
            );


            nextPoolRoundToGenerate++;
        }


        futureRow.gameObject
            .SetActive(true);


        /*
         * Only CurrentRow becomes a real combat participant.
         */
        ApplyCombatStates();


        /*
         * Snap to exact role values after the tween and apply Future
         * darkness to the newly spawned row.
         */
        ApplyRowVisualStates();


        IsAdvancing =
            false;


        Debug.Log(
            "[ENEMY CORRIDOR] Encounter rows advanced. " +
            "Current active / Next preview / Future preview."
        );
    }


    // =========================================================
    // FRESH ROW GENERATION
    // =========================================================

    private void RegenerateRowWithFreshEnemies(
        Transform row,
        int poolRoundNumber)
    {
        if (row == null)
            return;


        if (encounterGenerator == null)
        {
            Debug.LogWarning(
                "[ENEMY CORRIDOR] Encounter Generator is missing."
            );

            return;
        }


        if (!rowSpawnSlots.TryGetValue(
            row,
            out List<Transform> slots))
        {
            Debug.LogWarning(
                "[ENEMY CORRIDOR] No cached Slots for row: " +
                row.name
            );

            return;
        }


        if (slots.Count == 0)
        {
            Debug.LogWarning(
                "[ENEMY CORRIDOR] Row '" +
                row.name +
                "' contains no direct-child Slots."
            );

            return;
        }


        ClearEnemyRootsFromRow(
            row
        );


        EnemyEncounterGenerator.GeneratedEncounter generated =
            encounterGenerator.GenerateEncounter(
                poolRoundNumber,
                slots.Count
            );


        if (generated == null ||
            generated.enemies == null)
        {
            Debug.LogWarning(
                $"[ENEMY CORRIDOR] Generator returned no encounter for " +
                $"playable round {poolRoundNumber}."
            );

            return;
        }


        List<GeneratedEnemyChoice> choices =
            new List<GeneratedEnemyChoice>();


        int sourceCount =
            Mathf.Min(
                slots.Count,
                generated.enemies.Length
            );


        for (int i = 0;
             i < sourceCount;
             i++)
        {
            GameObject prefab =
                generated.enemies[i];


            choices.Add(
                new GeneratedEnemyChoice
                {
                    prefab =
                        prefab,

                    placementPriority =
                        prefab != null
                            ? GetPrefabPlacementPriority(
                                prefab
                            )
                            : 0,

                    randomTieBreaker =
                        Random.value
                }
            );
        }


        /*
         * Enemy Pool has no authored formation, so placement priorities are
         * applied.
         *
         * Preset Pool preserves exact Left / Center / Right order.
         */
        if (!generated.preserveSlotOrder)
        {
            choices.RemoveAll(
                choice =>
                    choice == null ||
                    choice.prefab == null
            );


            choices.Sort(
                (a, b) =>
                {
                    int priorityCompare =
                        a.placementPriority
                            .CompareTo(
                                b.placementPriority
                            );


                    if (priorityCompare != 0)
                    {
                        return
                            priorityCompare;
                    }


                    return
                        a.randomTieBreaker
                            .CompareTo(
                                b.randomTieBreaker
                            );
                }
            );
        }


        int spawnedCount =
            0;


        int spawnCount =
            Mathf.Min(
                slots.Count,
                choices.Count
            );


        for (int i = 0;
             i < spawnCount;
             i++)
        {
            Transform slot =
                slots[i];

            GeneratedEnemyChoice choice =
                choices[i];


            /*
             * In Preset Pool, null means an intentionally empty authored
             * slot. Do not shift later enemies into that position.
             */
            if (slot == null ||
                choice == null ||
                choice.prefab == null)
            {
                continue;
            }


            GameObject prefab =
                choice.prefab;


            GameObject instance =
                Instantiate(
                    prefab,
                    slot
                );


            instance.transform.localPosition =
                Vector3.zero;

            instance.transform.localRotation =
                Quaternion.identity;

            instance.transform.localScale =
                prefab.transform.localScale;


            RegisterBaseColorsForRoot(
                instance.transform
            );


            BaseEnemy enemy =
                instance.GetComponentInChildren<BaseEnemy>(
                    true
                );


            if (enemy != null)
            {
                enemy.SetCombatActive(
                    false
                );
            }
            else
            {
                Debug.LogWarning(
                    "[ENEMY CORRIDOR] Spawned prefab '" +
                    prefab.name +
                    "' does not contain BaseEnemy."
                );
            }


            spawnedCount++;
        }


        Debug.Log(
            $"[ENEMY CORRIDOR] Generated row '{row.name}' " +
            $"for playable round {poolRoundNumber} " +
            $"(UI R-{poolRoundNumber - 1}) with " +
            $"{spawnedCount} enemy instance(s)."
        );
    }


    private int GetPrefabPlacementPriority(
        GameObject prefab)
    {
        if (prefab == null)
            return 0;


        BaseEnemy enemy =
            prefab.GetComponentInChildren<BaseEnemy>(
                true
            );


        if (enemy == null)
            return 0;


        return
            enemy.RowPlacementPriority;
    }


    private void ClearEnemyRootsFromRow(
        Transform row)
    {
        if (row == null)
            return;


        if (!rowSpawnSlots.TryGetValue(
            row,
            out List<Transform> slots))
        {
            return;
        }


        List<GameObject> rootsToDestroy =
            new List<GameObject>();


        foreach (Transform slot in slots)
        {
            if (slot == null)
                continue;


            /*
             * Enemy prefab ROOTS are direct children of the persistent Slot.
             *
             * We only remove children that actually contain a BaseEnemy,
             * so future decorative/helper children inside a Slot are not
             * accidentally deleted.
             */
            foreach (Transform child in slot)
            {
                if (child == null)
                    continue;


                BaseEnemy enemy =
                    child.GetComponentInChildren<BaseEnemy>(
                        true
                    );


                if (enemy == null)
                    continue;


                ForgetBaseColorsForRoot(
                    child
                );


                child.gameObject
                    .SetActive(false);


                rootsToDestroy.Add(
                    child.gameObject
                );
            }
        }


        foreach (GameObject enemyRoot in
                 rootsToDestroy)
        {
            if (enemyRoot == null)
                continue;


            Destroy(
                enemyRoot
            );
        }
    }


    // =========================================================
    // COMBAT STATE
    // =========================================================

    private void ApplyCombatStates()
    {
        SetRowCombatActive(
            currentRow,
            true
        );


        SetRowCombatActive(
            nextRow,
            false
        );


        SetRowCombatActive(
            futureRow,
            false
        );
    }


    private void SetRowCombatActive(
        Transform row,
        bool active)
    {
        if (row == null)
            return;


        BaseEnemy[] enemies =
            row.GetComponentsInChildren<BaseEnemy>(
                true
            );


        foreach (BaseEnemy enemy in
                 enemies)
        {
            if (enemy == null ||
                !enemy.gameObject.activeInHierarchy)
            {
                continue;
            }


            enemy.SetCombatActive(
                active
            );
        }
    }


    // =========================================================
    // ROW VISUALS
    // =========================================================

    private void ApplyRowVisualStates()
    {
        SetRowBrightness(
            currentRow,
            currentBrightness
        );


        SetRowBrightness(
            nextRow,
            nextBrightness
        );


        SetRowBrightness(
            futureRow,
            futureBrightness
        );
    }


    private void SetRowBrightness(
        Transform row,
        float brightness)
    {
        if (row == null)
            return;


        RegisterBaseColorsForRow(
            row
        );


        SpriteRenderer[] renderers =
            row.GetComponentsInChildren<SpriteRenderer>(
                true
            );


        float clampedBrightness =
            Mathf.Clamp01(
                brightness
            );


        foreach (SpriteRenderer spriteRenderer in
                 renderers)
        {
            if (spriteRenderer == null)
                continue;


            if (!baseSpriteColors.TryGetValue(
                spriteRenderer,
                out Color baseColor))
            {
                baseColor =
                    spriteRenderer.color;

                baseSpriteColors[
                    spriteRenderer
                ] =
                    baseColor;
            }


            /*
             * Only RGB is darkened.
             *
             * Alpha remains exactly as authored in the sprite, so enemies
             * stay fully solid even when rows overlap in perspective.
             */
            spriteRenderer.color =
                new Color(
                    baseColor.r *
                    clampedBrightness,

                    baseColor.g *
                    clampedBrightness,

                    baseColor.b *
                    clampedBrightness,

                    baseColor.a
                );
        }
    }


    private void RegisterBaseColorsForRow(
        Transform row)
    {
        if (row == null)
            return;


        SpriteRenderer[] renderers =
            row.GetComponentsInChildren<SpriteRenderer>(
                true
            );


        foreach (SpriteRenderer spriteRenderer in
                 renderers)
        {
            if (spriteRenderer == null ||
                baseSpriteColors.ContainsKey(
                    spriteRenderer))
            {
                continue;
            }


            baseSpriteColors[
                spriteRenderer
            ] =
                spriteRenderer.color;
        }
    }


    private void RegisterBaseColorsForRoot(
        Transform root)
    {
        if (root == null)
            return;


        SpriteRenderer[] renderers =
            root.GetComponentsInChildren<SpriteRenderer>(
                true
            );


        foreach (SpriteRenderer spriteRenderer in
                 renderers)
        {
            if (spriteRenderer == null)
                continue;


            baseSpriteColors[
                spriteRenderer
            ] =
                spriteRenderer.color;
        }
    }


    private void ForgetBaseColorsForRoot(
        Transform root)
    {
        if (root == null)
            return;


        SpriteRenderer[] renderers =
            root.GetComponentsInChildren<SpriteRenderer>(
                true
            );


        foreach (SpriteRenderer spriteRenderer in
                 renderers)
        {
            if (spriteRenderer == null)
                continue;


            baseSpriteColors.Remove(
                spriteRenderer
            );
        }
    }


    // =========================================================
    // DEBUG
    // =========================================================

    [ContextMenu("DEBUG - Advance Encounter")]
    private void DebugAdvanceEncounter()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "[ENEMY CORRIDOR] Enter Play Mode first."
            );

            return;
        }


        AdvanceEncounter();
    }
}
