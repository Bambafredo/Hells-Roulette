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
public class EnemyActionDominateController :
    MonoBehaviour,
    ISegmentMarkTooltipProvider
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


    /*
     * The currently visible Dominate marks are group-level presentation.
     *
     * Several simultaneous Dominators share the same three logical targets, so
     * only one controller needs to write the visual overlay to SegmentMesh.
     */
    private static readonly List<SegmentMesh>
        sharedMarkedMeshes =
            new List<SegmentMesh>();

    private static EnemyActionDominateController
        sharedVisualOwner;


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        enemy =
            GetComponent<BaseEnemy>();
    }


    private void OnEnable()
    {
        SegmentMarkTooltipRegistry.Register(
            this
        );
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


        if (sharedVisualOwner == this)
        {
            RefreshSharedMarks();
        }
    }


    private void OnDisable()
    {
        ClearCurrentDominate();

        SegmentMarkTooltipRegistry.Unregister(
            this
        );
    }


    private void OnDestroy()
    {
        LeaveSharedDominateTargets();

        SegmentMarkTooltipRegistry.Unregister(
            this
        );
    }


    // =========================================================
    // ACTION LIFECYCLE
    // =========================================================

    private void BeginDominate(
        EnemyActionDominate action)
    {
        activeAction =
            action;


        JoinSharedDominateTargets();


        if (sharedVisualOwner == this)
        {
            RefreshSharedMarks();
        }


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

            return;
        }


        /*
         * Blood Damage already reaches GameLog through BaseEnemy's normal attack
         * pipeline. Random Infestation is a different consequence, so log it
         * explicitly once the InfestationManager has accepted the request.
         *
         * Do this here rather than inside InfestationManager because only the
         * Enemy Action knows WHICH enemy caused the infestation.
         */
        LogInfestation(
            infestationPrefab
        );
    }


    private void LogInfestation(
        GameObject infestationPrefab)
    {
        if (GameLogManager.Instance == null)
            return;


        string enemyName =
            enemy != null &&
            !string.IsNullOrWhiteSpace(
                enemy.EnemyName
            )
                ? enemy.EnemyName
                : "Enemy";


        string infestationName =
            GetInfestationDisplayName(
                infestationPrefab
            );


        GameLogManager.Instance
            .AddGameplayLine(
                GameLogManager.Instance
                    .EnemyText(
                        enemyName
                    ) +
                " infests you with " +
                GameLogManager.Instance
                    .StickerText(
                        infestationName
                    )
            );
    }


    private string GetInfestationDisplayName(
        GameObject infestationPrefab)
    {
        if (infestationPrefab == null)
            return "Infestation";


        BaseSticker sticker =
            infestationPrefab
                .GetComponentInChildren<BaseSticker>(
                    true
                );


        if (sticker != null &&
            sticker.effect != null &&
            !string.IsNullOrWhiteSpace(
                sticker.effect.stickerName
            ))
        {
            return
                sticker.effect.stickerName;
        }


        return
            infestationPrefab.name;
    }


    // =========================================================
    // SAFE SEGMENTS
    // =========================================================

    private void JoinSharedDominateTargets()
    {
        /*
         * Rejoining can happen when the same enemy moves directly from one
         * Dominate asset to another. Remove first so the shared group state is
         * recalculated cleanly.
         */
        activeDominateControllers.Remove(
            this
        );


        /*
         * The first currently-active Dominator creates the target set. Every
         * additional Dominator copies exactly those same logical indices.
         */
        if (activeDominateControllers.Count <= 0 ||
            sharedMarkedSegmentIndices.Count <= 0)
        {
            ChooseNewSharedMarkedSegments();
        }


        activeDominateControllers.Add(
            this
        );


        if (sharedVisualOwner == null ||
            !activeDominateControllers.Contains(
                sharedVisualOwner
            ))
        {
            sharedVisualOwner =
                this;
        }


        markedSegmentIndices.Clear();

        markedSegmentIndices.AddRange(
            sharedMarkedSegmentIndices
        );


        WarnIfSharedRulesConflict();
    }


    private void LeaveSharedDominateTargets()
    {
        bool wasActive =
            activeDominateControllers.Remove(
                this
            );


        if (!wasActive &&
            sharedVisualOwner != this)
        {
            return;
        }


        if (sharedVisualOwner == this)
        {
            sharedVisualOwner =
                null;


            foreach (
                EnemyActionDominateController controller
                in activeDominateControllers)
            {
                if (controller == null ||
                    controller.activeAction == null)
                {
                    continue;
                }


                sharedVisualOwner =
                    controller;

                break;
            }
        }


        if (activeDominateControllers.Count <= 0)
        {
            ClearSharedMarks();

            sharedMarkedSegmentIndices.Clear();

            sharedVisualOwner =
                null;

            return;
        }


        RefreshSharedMarks();
    }


    private void WarnIfSharedRulesConflict()
    {
        if (activeAction == null)
            return;


        foreach (
            EnemyActionDominateController controller
            in activeDominateControllers)
        {
            if (controller == null ||
                controller == this ||
                controller.activeAction == null)
            {
                continue;
            }


            if (controller.activeAction.markedSegmentRule ==
                activeAction.markedSegmentRule)
            {
                continue;
            }


            Debug.LogWarning(
                "[DOMINATE] Simultaneous Dominate actions use different " +
                "Marked Segment Rules. They still share the same three targets, " +
                "but the visible pattern follows the first active Dominate. " +
                "The segment tooltip still lists every distinct consequence."
            );

            return;
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
    // TEMPORARY SEGMENT MARK PRESENTATION
    // =========================================================

    private static void RefreshSharedMarks()
    {
        EnemyActionDominateController owner =
            sharedVisualOwner;


        if (owner == null ||
            owner.activeAction == null)
        {
            ClearSharedMarks();
            return;
        }


        owner.ResolveReferences();


        WheelGenerator targetGenerator =
            owner.generator;


        if (targetGenerator == null ||
            targetGenerator.segments == null)
        {
            return;
        }


        /*
         * WheelShifter can rebuild every SegmentMesh. Clear any still-alive old
         * references, then rebind the SAME logical Dominate indices to the
         * current wheel meshes.
         */
        ClearSharedMarks();


        foreach (int segmentIndex in
                 sharedMarkedSegmentIndices)
        {
            if (segmentIndex < 0 ||
                segmentIndex >=
                    targetGenerator.segments.Count)
            {
                continue;
            }


            WheelSegmentData data =
                targetGenerator
                    .segments[segmentIndex];


            SegmentMesh mesh =
                data != null
                    ? data.meshComponent
                    : null;


            if (mesh == null)
                continue;


            mesh.ConfigureMark(
                owner.activeAction.highlightColor,
                owner.activeAction.highlightStrength,
                owner.activeAction.markDensity,
                owner.activeAction.markWidth,
                owner.activeAction.GetActiveMarkPattern()
            );


            mesh.SetMarked(
                true
            );


            sharedMarkedMeshes.Add(
                mesh
            );
        }
    }


    private static void ClearSharedMarks()
    {
        foreach (SegmentMesh mesh in
                 sharedMarkedMeshes)
        {
            if (mesh == null)
                continue;


            /*
             * ONLY the temporary mark layer is disabled here.
             *
             * We never call SetBlocked(false), so Segment Block and Limbo's
             * permanent/special blocked patterns are untouched.
             */
            mesh.SetMarked(
                false
            );
        }


        sharedMarkedMeshes.Clear();
    }


    // =========================================================
    // SEGMENT MARK TOOLTIP
    // =========================================================

    public bool TryGetSegmentMarkTooltip(
        int segmentIndex,
        out SegmentMarkTooltipData data)
    {
        data =
            default;


        if (activeAction == null ||
            enemy == null ||
            enemy.IsDead ||
            !enemy.CombatActive ||
            !markedSegmentIndices.Contains(
                segmentIndex
            ))
        {
            return false;
        }


        data =
            new SegmentMarkTooltipData(
                "Dominate",
                activeAction.GetMarkedSegmentDescription(),
                activeAction.GetMarkedSegmentStatus()
            );


        return true;
    }


    private void ClearCurrentDominate()
    {
        bool wasActive =
            activeAction != null ||
            activeDominateControllers.Contains(
                this
            );


        if (wasActive)
        {
            LeaveSharedDominateTargets();
        }


        activeAction =
            null;

        markedSegmentIndices.Clear();
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
