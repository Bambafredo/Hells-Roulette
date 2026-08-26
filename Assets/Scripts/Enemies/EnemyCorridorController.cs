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
        "Pool used only to repopulate a recycled Future row with fresh " +
        "enemy instances. For now this can contain only Enemy_Imp."
    )]
    public GameObject[] enemyPrefabs;

    [Tooltip(
        "If true, when a row is recycled to the back it gets fresh enemies."
    )]
    public bool regenerateFutureRowOnAdvance = true;


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
     * Each physical row remembers WHERE its enemies were authored.
     *
     * This supports both hierarchies:
     *
     * Row
     * ├ Enemy_Imp
     * ├ Enemy_Imp
     * └ Enemy_Imp
     *
     * and:
     *
     * Row
     * ├ Slot_L
     * │  └ Enemy_Imp
     * ├ Slot_C
     * │  └ Enemy_Imp
     * └ Slot_R
     *    └ Enemy_Imp
     *
     * When the row is recycled to Future, the old enemy instances are
     * removed and brand-new prefab instances are created in these same
     * authored spawn points.
     */
    private class RowSpawnPoint
    {
        public Transform parent;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
    }


    private readonly Dictionary<Transform, List<RowSpawnPoint>>
        rowSpawnPoints =
            new Dictionary<Transform, List<RowSpawnPoint>>();


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

        CaptureRowSpawnPositions();

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


    private void CaptureRowSpawnPositions()
    {
        CacheSpawnPointsForRow(
            currentRow
        );

        CacheSpawnPointsForRow(
            nextRow
        );

        CacheSpawnPointsForRow(
            futureRow
        );
    }


    private void CacheSpawnPointsForRow(
        Transform row)
    {
        if (row == null ||
            rowSpawnPoints.ContainsKey(row))
        {
            return;
        }


        List<RowSpawnPoint> spawnPoints =
            new List<RowSpawnPoint>();


        /*
         * We inspect the DIRECT children of the row.
         *
         * A direct child can either be:
         *
         * 1) the enemy instance itself
         * 2) a persistent Slot object that contains the enemy instance
         */
        foreach (Transform child in row)
        {
            // -------------------------------------------------
            // DIRECT ENEMY
            // -------------------------------------------------

            BaseEnemy directEnemy =
                child.GetComponent<BaseEnemy>();


            if (directEnemy != null)
            {
                spawnPoints.Add(
                    new RowSpawnPoint
                    {
                        parent = row,
                        localPosition =
                            child.localPosition,
                        localRotation =
                            child.localRotation,
                        localScale =
                            child.localScale
                    }
                );

                continue;
            }


            // -------------------------------------------------
            // SLOT CONTAINING AN ENEMY
            // -------------------------------------------------

            BaseEnemy nestedEnemy =
                child.GetComponentInChildren<BaseEnemy>(
                    true
                );


            if (nestedEnemy == null)
                continue;


            /*
             * Find the top-level enemy instance underneath this Slot.
             *
             * Example:
             *
             * Slot_L
             * └ Enemy_Imp        <- instanceRoot
             *    └ Visual
             *       └ BaseEnemy
             */
            Transform instanceRoot =
                GetTopLevelChildUnderParent(
                    nestedEnemy.transform,
                    child
                );


            if (instanceRoot == null)
                continue;


            spawnPoints.Add(
                new RowSpawnPoint
                {
                    parent = child,
                    localPosition =
                        instanceRoot.localPosition,
                    localRotation =
                        instanceRoot.localRotation,
                    localScale =
                        instanceRoot.localScale
                }
            );
        }


        /*
         * Keep left-to-right ordering deterministic.
         */
        spawnPoints.Sort(
            (a, b) =>
            {
                float ax =
                    a.parent.TransformPoint(
                        a.localPosition
                    ).x;

                float bx =
                    b.parent.TransformPoint(
                        b.localPosition
                    ).x;

                return
                    ax.CompareTo(bx);
            }
        );


        rowSpawnPoints[row] =
            spawnPoints;


        Debug.Log(
            $"[ENEMY CORRIDOR] Cached " +
            $"{spawnPoints.Count} spawn point(s) for " +
            $"{row.name}."
        );
    }


    private Transform GetTopLevelChildUnderParent(
        Transform descendant,
        Transform parent)
    {
        if (descendant == null ||
            parent == null)
        {
            return null;
        }


        Transform current =
            descendant;


        while (current.parent != null &&
               current.parent != parent)
        {
            current =
                current.parent;
        }


        if (current.parent != parent)
            return null;


        return current;
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
                futureRow
            );
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
        Transform row)
    {
        if (row == null)
            return;


        if (enemyPrefabs == null ||
            enemyPrefabs.Length == 0)
        {
            Debug.LogWarning(
                "[ENEMY CORRIDOR] No enemy prefabs assigned. " +
                "Cannot regenerate FutureRow."
            );

            return;
        }


        if (!rowSpawnPoints.TryGetValue(
            row,
            out List<RowSpawnPoint> spawnPoints))
        {
            Debug.LogWarning(
                "[ENEMY CORRIDOR] No cached spawn points for row: " +
                row.name
            );

            return;
        }


        if (spawnPoints.Count == 0)
        {
            Debug.LogWarning(
                "[ENEMY CORRIDOR] Row '" +
                row.name +
                "' has zero cached spawn points. " +
                "Fresh enemies cannot be generated."
            );

            return;
        }


        /*
         * IMPORTANT:
         *
         * Clear the OLD enemy instances first.
         *
         * Persistent Slot_L / Slot_C / Slot_R objects are NOT destroyed.
         * Only the enemy GameObjects living inside them are removed.
         */
        ClearEnemyRootsFromRow(
            row
        );


        foreach (RowSpawnPoint spawnPoint in
                 spawnPoints)
        {
            if (spawnPoint == null ||
                spawnPoint.parent == null)
            {
                continue;
            }


            GameObject prefab =
                GetRandomEnemyPrefab();


            if (prefab == null)
                continue;


            GameObject instance =
                Instantiate(
                    prefab,
                    spawnPoint.parent
                );


            instance.transform.localPosition =
                spawnPoint.localPosition;

            instance.transform.localRotation =
                spawnPoint.localRotation;

            /*
             * Preserve the scale that was authored in the row/slot rather
             * than assuming the prefab's default scale.
             */
            instance.transform.localScale =
                spawnPoint.localScale;


            RegisterBaseColorsForRoot(
                instance.transform
            );


            BaseEnemy enemy =
                instance.GetComponentInChildren<BaseEnemy>(
                    true
                );


            if (enemy != null)
            {
                /*
                 * This is FutureRow, so the new enemy is visible but cannot
                 * participate in combat until the row reaches Current.
                 */
                enemy.SetCombatActive(
                    false
                );
            }
            else
            {
                Debug.LogWarning(
                    "[ENEMY CORRIDOR] Spawned prefab '" +
                    prefab.name +
                    "' does not contain a BaseEnemy."
                );
            }
        }


        Debug.Log(
            $"[ENEMY CORRIDOR] Regenerated " +
            $"{spawnPoints.Count} fresh enemy instance(s) in " +
            $"{row.name}."
        );
    }


    private GameObject GetRandomEnemyPrefab()
    {
        if (enemyPrefabs == null ||
            enemyPrefabs.Length == 0)
        {
            return null;
        }


        /*
         * This is intentionally only a minimal fresh-instance provider.
         *
         * Proper encounter composition / difficulty generation is a later
         * system. With a single Enemy_Imp in the array, it simply creates
         * fresh Imps.
         */
        int startIndex =
            Random.Range(
                0,
                enemyPrefabs.Length
            );


        for (int i = 0;
             i < enemyPrefabs.Length;
             i++)
        {
            int index =
                (
                    startIndex +
                    i
                ) %
                enemyPrefabs.Length;


            if (enemyPrefabs[index] != null)
                return enemyPrefabs[index];
        }


        return null;
    }


    private void ClearEnemyRootsFromRow(
        Transform row)
    {
        if (row == null)
            return;


        if (!rowSpawnPoints.TryGetValue(
            row,
            out List<RowSpawnPoint> spawnPoints))
        {
            return;
        }


        HashSet<GameObject> rootsToDestroy =
            new HashSet<GameObject>();


        foreach (RowSpawnPoint spawnPoint in
                 spawnPoints)
        {
            if (spawnPoint == null ||
                spawnPoint.parent == null)
            {
                continue;
            }


            BaseEnemy[] enemies =
                spawnPoint.parent
                    .GetComponentsInChildren<BaseEnemy>(
                        true
                    );


            foreach (BaseEnemy enemy in enemies)
            {
                if (enemy == null)
                    continue;


                Transform instanceRoot =
                    GetTopLevelChildUnderParent(
                        enemy.transform,
                        spawnPoint.parent
                    );


                if (instanceRoot == null)
                    continue;


                /*
                 * If the row itself is the spawn parent, this is a direct
                 * enemy child.
                 *
                 * If a Slot is the spawn parent, this is the enemy instance
                 * inside that Slot.
                 *
                 * In neither case do we destroy the persistent Slot itself.
                 */
                if (instanceRoot == row)
                    continue;


                rootsToDestroy.Add(
                    instanceRoot.gameObject
                );
            }
        }


        foreach (GameObject enemyRoot in
                 rootsToDestroy)
        {
            if (enemyRoot == null)
                continue;


            ForgetBaseColorsForRoot(
                enemyRoot.transform
            );


            enemyRoot.SetActive(
                false
            );


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
            if (enemy == null)
                continue;


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
