using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime state/presentation owner for EnemyActionDominate.
///
/// EnemyAction assets are ScriptableObjects and can be shared by several enemy
/// instances, so the three random marked segments MUST NOT be stored in the asset.
/// This component keeps that state on the individual enemy instead.
///
/// Add this component to any enemy prefab whose action sequence can contain
/// EnemyActionDominate.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BaseEnemy))]
public class EnemyActionDominateController : MonoBehaviour
{
    private const int MarkedSegmentCount =
        3;


    /*
     * GLOBAL DOMINATE TARGET GROUP
     * ----------------------------
     *
     * Every enemy currently advertising Dominate shares the same three logical
     * segment indices. This prevents several Dominate enemies from covering most
     * or all of the wheel with independent target sets.
     *
     * The state is static because EnemyAction assets are shared ScriptableObjects
     * and we deliberately do not want scene-level coordinator objects. It exists
     * only while at least one EnemyActionDominateController is actively using
     * Dominate; the last controller leaving the action clears the group.
     */
    private static readonly List<int>
        sharedMarkedSegmentIndices =
            new List<int>();


    private static readonly HashSet<EnemyActionDominateController>
        activeDominateControllers =
            new HashSet<EnemyActionDominateController>();


    private BaseEnemy enemy;
    private RouletteController roulette;
    private WheelGenerator generator;


    private EnemyActionDominate activeAction;


    private readonly List<int>
        markedSegmentIndices =
            new List<int>();


    private readonly List<SegmentTelegraphPresenter>
        telegraphs =
            new List<SegmentTelegraphPresenter>();


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        enemy =
            GetComponent<BaseEnemy>();
    }


    private void Start()
    {
        ResolveReferences();
    }


    private void Update()
    {
        ResolveReferences();


        EnemyActionDominate currentDominate =
            enemy != null &&
            enemy.CombatActive &&
            !enemy.IsDead
                ? enemy.CurrentAction as EnemyActionDominate
                : null;


        if (currentDominate == null)
        {
            ClearCurrentDominate();
            return;
        }


        /*
         * First frame on which this enemy is actually advertising Dominate:
         * choose three UNIQUE logical segments and keep them stable until that
         * action resolves. Invalid spins therefore do not reroll the marked segments.
         */
        if (activeAction !=
            currentDominate ||
            markedSegmentIndices.Count <= 0)
        {
            BeginDominate(
                currentDominate
            );
        }


        RefreshTelegraphs();
    }


    private void OnDisable()
    {
        ClearCurrentDominate();
    }


    private void OnDestroy()
    {
        LeaveSharedDominateTargets();
        DisposeTelegraphs();
    }


    // =========================================================
    // ACTION LIFECYCLE
    // =========================================================

    private void BeginDominate(
        EnemyActionDominate action)
    {
        ClearTelegraphsOnly();


        activeAction =
            action;


        JoinSharedDominateTargets();


        RefreshTelegraphs();


        Debug.Log(
            $"[DOMINATE] {enemy.EnemyName} uses shared marked segments: " +
            BuildMarkedSegmentDebugList() +
            "."
        );
    }


    public void ResolveDominate(
        EnemyActionDominate action)
    {
        ResolveReferences();


        /*
         * Defensive fallback for direct/debug Execute() calls that happen before
         * Update had a chance to prepare the three targets.
         */
        if (activeAction != action ||
            markedSegmentIndices.Count <= 0)
        {
            BeginDominate(
                action
            );
        }


        int landedSegment =
            roulette != null
                ? roulette.LastResolvedSegmentIndex
                : -1;


        bool landedOnMarkedSegment =
            markedSegmentIndices.Contains(
                landedSegment
            );


        bool failureTriggered =
            action.markedSegmentRule ==
                EnemyActionDominate.MarkedSegmentRule.MustAvoidMarked
                ? landedOnMarkedSegment
                : !landedOnMarkedSegment;


        if (!failureTriggered)
        {
            Debug.Log(
                $"[DOMINATE] {enemy.EnemyName}: landed on Segment " +
                $"{landedSegment + 1}. Marked rule passed; no consequence."
            );


            ClearCurrentDominate();
            return;
        }


        ResolveFailure(
            action,
            landedSegment
        );


        /*
         * Clear immediately after execution.
         *
         * This also handles two consecutive sequence entries that reference the
         * SAME Dominate asset: next Update sees no active runtime state and
         * rolls a fresh set of three marked segments.
         */
        ClearCurrentDominate();
    }


    private void ResolveFailure(
        EnemyActionDominate action,
        int landedSegment)
    {
        if (action == null)
            return;


        switch (action.failureEffect)
        {
            case EnemyActionDominate.FailureEffect.BloodDamage:

                int damage =
                    Mathf.Max(
                        0,
                        action.bloodDamage
                    );


                if (damage > 0)
                {
                    /*
                     * Use BaseEnemy's normal Blood attack path so Shield /
                     * reactive mitigation, log ordering and feedback remain
                     * consistent with the rest of the game.
                     */
                    enemy.PerformBloodAttack(
                        damage
                    );
                }


                Debug.Log(
                    $"[DOMINATE] {enemy.EnemyName}: landed on Segment " +
                    $"{landedSegment + 1}. Marked rule failed. " +
                    $"Blood Damage consequence = {damage}."
                );

                break;


            case EnemyActionDominate.FailureEffect.RandomInfestation:
            default:

                RequestRandomInfestation(
                    action
                );


                Debug.Log(
                    $"[DOMINATE] {enemy.EnemyName}: landed on Segment " +
                    $"{landedSegment + 1}. Marked rule failed. " +
                    "Random Infestation consequence requested."
                );

                break;
        }
    }


    // =========================================================
    // INFESTATION
    // =========================================================

    private void RequestRandomInfestation(
        EnemyActionDominate action)
    {
        GameObject infestationPrefab =
            action != null
                ? action.GetRandomInfestationPrefab()
                : null;


        if (infestationPrefab == null)
        {
            Debug.LogWarning(
                "[DOMINATE] Random Infestation selected, but Dominate's " +
                "Infestation Pool has no valid prefab references."
            );

            return;
        }


        InfestationManager manager =
            InfestationManager.Instance != null
                ? InfestationManager.Instance
                : FindObjectOfType<InfestationManager>(
                    true
                );


        if (manager == null)
        {
            Debug.LogWarning(
                "[DOMINATE] InfestationManager was not found."
            );

            return;
        }


        bool requested =
            manager.RequestInfestation(
                infestationPrefab,
                1
            );


        if (!requested)
        {
            Debug.LogWarning(
                $"[DOMINATE] Could not grant infestation " +
                $"'{infestationPrefab.name}'."
            );
        }
    }


    // =========================================================
    // SAFE SEGMENTS
    // =========================================================

    private void JoinSharedDominateTargets()
    {
        /*
         * If this controller was already registered, remove it before joining
         * again. This keeps repeated/consecutive Dominate actions clean.
         */
        activeDominateControllers.Remove(
            this
        );


        /*
         * The first currently-active Dominate enemy creates the group targets.
         * Every additional Dominate enemy simply copies those same indices.
         */
        if (activeDominateControllers.Count <= 0 ||
            sharedMarkedSegmentIndices.Count <= 0)
        {
            ChooseNewSharedMarkedSegments();
        }


        activeDominateControllers.Add(
            this
        );


        markedSegmentIndices.Clear();

        markedSegmentIndices.AddRange(
            sharedMarkedSegmentIndices
        );


        EnsureTelegraphCount(
            markedSegmentIndices.Count
        );
    }


    private void LeaveSharedDominateTargets()
    {
        activeDominateControllers.Remove(
            this
        );


        /*
         * No Dominate action is currently being advertised anymore.
         * The next Dominate group must roll a fresh set of three segments.
         */
        if (activeDominateControllers.Count <= 0)
        {
            sharedMarkedSegmentIndices.Clear();
        }
    }


    private void ChooseNewSharedMarkedSegments()
    {
        sharedMarkedSegmentIndices.Clear();


        if (generator == null ||
            generator.segmentCount <= 0)
        {
            return;
        }


        List<int> candidates =
            new List<int>();


        for (int i = 0;
             i < generator.segmentCount;
             i++)
        {
            candidates.Add(
                i
            );
        }


        int targetCount =
            Mathf.Min(
                MarkedSegmentCount,
                candidates.Count
            );


        /*
         * Partial Fisher-Yates: draw unique random segment indices without
         * retries or duplicate-target edge cases.
         */
        for (int i = 0;
             i < targetCount;
             i++)
        {
            int randomIndex =
                Random.Range(
                    i,
                    candidates.Count
                );


            int temp =
                candidates[i];

            candidates[i] =
                candidates[randomIndex];

            candidates[randomIndex] =
                temp;


            sharedMarkedSegmentIndices.Add(
                candidates[i]
            );
        }
    }


    // =========================================================
    // TELEGRAPH
    // =========================================================

    private void EnsureTelegraphCount(
        int count)
    {
        while (telegraphs.Count <
               count)
        {
            telegraphs.Add(
                new SegmentTelegraphPresenter(
                    generator,
                    roulette
                )
            );
        }


        for (int i = 0;
             i < telegraphs.Count;
             i++)
        {
            telegraphs[i]
                .SetGenerator(
                    generator
                );

            telegraphs[i]
                .SetRouletteController(
                    roulette
                );
        }
    }


    private void RefreshTelegraphs()
    {
        if (activeAction == null)
        {
            ClearTelegraphsOnly();
            return;
        }


        EnsureTelegraphCount(
            markedSegmentIndices.Count
        );


        for (int i = 0;
             i < telegraphs.Count;
             i++)
        {
            if (i >=
                markedSegmentIndices.Count)
            {
                telegraphs[i].Hide();
                continue;
            }


            telegraphs[i].Show(
                markedSegmentIndices[i],
                activeAction.highlightColor,
                activeAction.highlightStrength,
                activeAction.pulseSpeed,
                activeAction.hideWhileDraggingSticker,
                true
            );


            /*
             * Refresh every frame so WheelShifter / regenerated SegmentMesh
             * objects are rebound exactly like Limbo's telegraph.
             */
            telegraphs[i].Refresh();
        }
    }


    private void ClearTelegraphsOnly()
    {
        foreach (
            SegmentTelegraphPresenter telegraph
            in telegraphs)
        {
            telegraph?.Hide();
        }
    }


    private void ClearCurrentDominate()
    {
        ClearTelegraphsOnly();


        LeaveSharedDominateTargets();


        activeAction =
            null;

        markedSegmentIndices.Clear();
    }


    private void DisposeTelegraphs()
    {
        foreach (
            SegmentTelegraphPresenter telegraph
            in telegraphs)
        {
            telegraph?.Dispose();
        }


        telegraphs.Clear();
    }


    // =========================================================
    // REFERENCES / DEBUG
    // =========================================================

    private void ResolveReferences()
    {
        if (enemy == null)
        {
            enemy =
                GetComponent<BaseEnemy>();
        }


        if (roulette == null)
        {
            roulette =
                RouletteController.Instance != null
                    ? RouletteController.Instance
                    : FindObjectOfType<RouletteController>();
        }


        if (generator == null)
        {
            generator =
                roulette != null
                    ? roulette.generator
                    : FindObjectOfType<WheelGenerator>();
        }
    }


    private string BuildMarkedSegmentDebugList()
    {
        if (markedSegmentIndices.Count <= 0)
            return "NONE";


        string result =
            "";


        for (int i = 0;
             i < markedSegmentIndices.Count;
             i++)
        {
            if (i > 0)
            {
                result +=
                    ", ";
            }


            result +=
                (markedSegmentIndices[i] + 1)
                    .ToString();
        }


        return result;
    }
}
