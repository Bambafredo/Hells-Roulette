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
     * Each physical row keeps the local positions of the enemy roots that
     * were manually placed there in the editor.
     *
     * When that row is later recycled to Future, fresh enemies spawn in
     * those exact positions.
     */
    private readonly Dictionary<Transform, List<Vector3>>
        rowSpawnPositions =
            new Dictionary<Transform, List<Vector3>>();


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
        CacheSpawnPositionsForRow(
            currentRow
        );

        CacheSpawnPositionsForRow(
            nextRow
        );

        CacheSpawnPositionsForRow(
            futureRow
        );
    }


    private void CacheSpawnPositionsForRow(
        Transform row)
    {
        if (row == null ||
            rowSpawnPositions.ContainsKey(row))
        {
            return;
        }


        List<Vector3> positions =
            new List<Vector3>();


        foreach (Transform child in row)
        {
            BaseEnemy enemy =
                child.GetComponent<BaseEnemy>();


            if (enemy == null)
                continue;


            positions.Add(
                child.localPosition
            );
        }


        positions.Sort(
            (a, b) =>
                a.x.CompareTo(b.x)
        );


        rowSpawnPositions[row] =
            positions;
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


        if (!rowSpawnPositions.TryGetValue(
            row,
            out List<Vector3> spawnPositions))
        {
            Debug.LogWarning(
                "[ENEMY CORRIDOR] No cached spawn positions for row: " +
                row.name
            );

            return;
        }


        ClearEnemyRootsFromRow(
            row
        );


        foreach (Vector3 localPosition in
                 spawnPositions)
        {
            GameObject prefab =
                GetRandomEnemyPrefab();


            if (prefab == null)
                continue;


            GameObject instance =
                Instantiate(
                    prefab,
                    row
                );


            instance.transform.localPosition =
                localPosition;

            instance.transform.localRotation =
                Quaternion.identity;

            instance.transform.localScale =
                prefab.transform.localScale;


            RegisterBaseColorsForRoot(
                instance.transform
            );


            BaseEnemy enemy =
                instance.GetComponent<BaseEnemy>();


            if (enemy != null)
            {
                enemy.SetCombatActive(
                    false
                );
            }
        }
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


        List<GameObject> toDestroy =
            new List<GameObject>();


        foreach (Transform child in row)
        {
            BaseEnemy enemy =
                child.GetComponent<BaseEnemy>();


            if (enemy == null)
                continue;


            ForgetBaseColorsForRoot(
                child
            );


            child.gameObject
                .SetActive(false);


            toDestroy.Add(
                child.gameObject
            );
        }


        foreach (GameObject go in
                 toDestroy)
        {
            Destroy(go);
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
