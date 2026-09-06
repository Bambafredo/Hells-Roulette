using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Encounter-specific rules for Limbo.
///
/// Limbo remains a normal BaseEnemy for HP, damage, death, Curse and the
/// authored EnemyAction sequence. This controller owns only:
/// - unlimited valid spins / no debt trigger while Limbo is active;
/// - Limbo's permanent special Segment Blocks;
/// - stable pre-planning of the next random target segment for EA preview;
/// - the special effect captured when a spin lands on one of those blocks;
/// - post-money resolution for Divide Money and Collect + Unlock;
/// - tooltip presentation for Limbo's special Segment Blocks;
/// - cleanup when Limbo leaves the encounter.
///
/// IMPORTANT:
/// The NUMBER and TYPES of blocks before Collect + Unlock are authored in
/// BaseEnemy.actionSequence through EnemyActionLimboPermanentBlock assets.
/// LimboBossController does not impose a 2/2/2 distribution or six-block cycle.
/// </summary>
public class LimboBossController :
    BossEncounterController,
    ISegmentBlockTooltipOverrideProvider
{
    // =========================================================
    // SPECIAL BLOCK TYPES
    // =========================================================

    public enum LimboBlockEffectType
    {
        DamagePlayer,
        DivideMoney,
        DamageBoss
    }


    private struct LimboBlockData
    {
        public LimboBlockEffectType type;
        public int value;

        public LimboBlockData(
            LimboBlockEffectType type,
            int value)
        {
            this.type =
                type;

            this.value =
                value;
        }
    }


    // =========================================================
    // PROCEDURAL BLOCK PATTERNS
    // =========================================================

    [Header("Limbo - Procedural Block Patterns")]

    [Tooltip(
        "Procedural blocked pattern used by Damage Player blocks."
    )]
    public SegmentMesh.BlockedPatternType damagePlayerPattern =
        SegmentMesh.BlockedPatternType.Crosshatch;

    [Tooltip(
        "Procedural blocked pattern used by Divide Money blocks."
    )]
    public SegmentMesh.BlockedPatternType divideMoneyPattern =
        SegmentMesh.BlockedPatternType.Horizontal;

    [Tooltip(
        "Procedural blocked pattern used by Damage Boss blocks."
    )]
    public SegmentMesh.BlockedPatternType damageBossPattern =
        SegmentMesh.BlockedPatternType.Dots;


    // =========================================================
    // TELEGRAPHING
    // =========================================================

    [Header("Limbo - Telegraphing")]

    [Tooltip(
        "If enabled, Limbo's Permanent Block EA tooltip reveals the exact " +
        "segment that will be blocked next. Disable this to hide the target " +
        "while keeping all gameplay behaviour unchanged."
    )]
    public bool showPermanentBlockTargetInTooltip =
        true;


    // =========================================================
    // RUNTIME STATE
    // =========================================================

    private readonly Dictionary<int, LimboBlockData>
        specialBlocks =
            new Dictionary<int, LimboBlockData>();


    /*
     * The next Permanent Block target is chosen before execution so its EA
     * tooltip can truthfully preview "Target: Segment X".
     *
     * It remains stable until that Permanent Block executes, unless the target
     * becomes blocked by another effect first, in which case we safely reroll.
     */
    private int plannedTargetSegmentIndex =
        -1;


    private bool pendingBlockEffect =
        false;

    private int pendingBlockSegmentIndex =
        -1;

    private LimboBlockData pendingBlockData;


    /*
     * Collect + Unlock is authored/executed as a normal EnemyAction, but its
     * money swipe is deliberately deferred until RoundManager announces that
     * the complete gameplay resolution has finished. RouletteController calls
     * that hook only AFTER sticker/enemy money and the Lucky Shot bonus have
     * already resolved.
     */
    private bool collectUnlockPending =
        false;

    private int pendingCollectMoney =
        0;


    /*
     * Divide Money is also deferred to the same post-money hook.
     *
     * This makes the block divide the player's FINAL money after all sticker
     * earnings and the Lucky Shot bonus have been awarded. If Collect + Unlock
     * is also queued on that spin, Divide Money resolves first and Collect
     * resolves second.
     */
    private bool divideMoneyPending =
        false;

    private int pendingDivideSegmentIndex =
        -1;

    private int pendingMoneyDivisor =
        1;


    private RouletteController roulette;
    private WheelGenerator generator;


    // =========================================================
    // PUBLIC STATE
    // =========================================================

    public int ActiveSpecialBlockCount =>
        specialBlocks.Count;

    public bool HasPendingBlockEffect =>
        pendingBlockEffect;


    // =========================================================
    // UNITY / TOOLTIP REGISTRY
    // =========================================================

    private void OnEnable()
    {
        SegmentBlockTooltipOverrideRegistry
            .Register(
                this
            );
    }


    private void OnDisable()
    {
        SegmentBlockTooltipOverrideRegistry
            .Unregister(
                this
            );
    }


    // =========================================================
    // BOSS LIFECYCLE
    // =========================================================

    protected override void Start()
    {
        roulette =
            RouletteController.Instance != null
                ? RouletteController.Instance
                : FindObjectOfType<RouletteController>();


        generator =
            roulette != null
                ? roulette.generator
                : FindObjectOfType<WheelGenerator>();


        base.Start();


        if (RoundManagerRef != null)
        {
            RoundManagerRef.OnGameplaySpinResolutionCompleted +=
                HandlePostMoneySpinResolution;
        }
    }


    protected override void OnDestroy()
    {
        if (RoundManagerRef != null)
        {
            RoundManagerRef.OnGameplaySpinResolutionCompleted -=
                HandlePostMoneySpinResolution;
        }


        base.OnDestroy();
    }


    protected override void OnBossEncounterActivated()
    {
        /*
         * Limbo starts from a clean Segment Block puzzle state.
         *
         * This prevents a normal block inherited from the previous encounter
         * from stealing one of Limbo's available targets.
         */
        if (generator != null)
        {
            generator.ClearAllSegmentBlocks();
        }


        specialBlocks.Clear();

        plannedTargetSegmentIndex =
            -1;

        pendingBlockEffect =
            false;

        pendingBlockSegmentIndex =
            -1;

        collectUnlockPending =
            false;

        pendingCollectMoney =
            0;

        divideMoneyPending =
            false;

        pendingDivideSegmentIndex =
            -1;

        pendingMoneyDivisor =
            1;


        /*
         * Defensive escape hatch for debug/manual corridor transitions that
         * enter Limbo with no token available.
         */
        if (RoundManagerRef != null &&
            RoundManagerRef.TokensRemaining <= 0)
        {
            RoundManagerRef.AddTokens(1);
        }


        Debug.Log(
            "[LIMBO] Boss encounter active. Valid spins will not consume tokens."
        );
    }


    protected override void OnBossEncounterDeactivated()
    {
        ClearAllLimboBlocks();

        pendingBlockEffect =
            false;

        collectUnlockPending =
            false;

        pendingCollectMoney =
            0;

        divideMoneyPending =
            false;

        pendingDivideSegmentIndex =
            -1;

        pendingMoneyDivisor =
            1;
    }


    protected override void OnBossDefeated()
    {
        ClearAllLimboBlocks();

        pendingBlockEffect =
            false;

        collectUnlockPending =
            false;

        pendingCollectMoney =
            0;

        divideMoneyPending =
            false;

        pendingDivideSegmentIndex =
            -1;

        pendingMoneyDivisor =
            1;


        Debug.Log(
            "[LIMBO] Defeated. Boss Segment Blocks cleared; normal encounter flow resumes."
        );
    }


    // =========================================================
    // VALID SPIN HOOK
    // =========================================================

    protected override void OnBossValidSpinValidated()
    {
        /*
         * RoundManager invokes OnSpinValidated BEFORE its normal SpendToken().
         * +1 here and -1 there = no net token consumption during Limbo.
         */
        if (RoundManagerRef != null)
        {
            RoundManagerRef.AddTokens(1);
        }


        CapturePendingWinningBlockEffect();


        /*
         * Each Limbo block lives at 1 future valid spin remaining.
         * Refresh 1 -> 2 here; WheelGenerator's normal countdown later returns
         * it to 1. The block therefore remains permanent until Collect/cleanup.
         */
        RefreshPermanentBlocks();
    }


    private void CapturePendingWinningBlockEffect()
    {
        pendingBlockEffect =
            false;

        pendingBlockSegmentIndex =
            -1;


        if (roulette == null)
            return;


        int winningIndex =
            roulette.LastResolvedSegmentIndex;


        if (!specialBlocks.TryGetValue(
                winningIndex,
                out LimboBlockData data))
        {
            return;
        }


        pendingBlockEffect =
            true;

        pendingBlockSegmentIndex =
            winningIndex;

        pendingBlockData =
            data;
    }


    // =========================================================
    // NEXT PERMANENT BLOCK TARGET
    // =========================================================

    /// <summary>
    /// Returns the stable random segment targeted by the NEXT Permanent Block.
    ///
    /// EnemyActionLimboPermanentBlock uses this for its tooltip and execution,
    /// so the player sees the same target that will actually be blocked.
    /// </summary>
    public bool TryGetNextPermanentBlockTarget(
        out int segmentIndex)
    {
        segmentIndex =
            -1;


        if (!EncounterActive ||
            Enemy == null ||
            Enemy.IsDead ||
            generator == null)
        {
            return false;
        }


        ReconcileSpecialBlocksWithWheel();


        if (IsValidAvailableTarget(
                plannedTargetSegmentIndex))
        {
            segmentIndex =
                plannedTargetSegmentIndex;

            return true;
        }


        plannedTargetSegmentIndex =
            -1;


        List<int> candidates =
            new List<int>();


        for (int i = 0;
             i < generator.segmentCount;
             i++)
        {
            if (!generator.IsSegmentBlocked(i))
            {
                candidates.Add(i);
            }
        }


        if (candidates.Count == 0)
            return false;


        plannedTargetSegmentIndex =
            candidates[
                UnityEngine.Random.Range(
                    0,
                    candidates.Count
                )
            ];


        segmentIndex =
            plannedTargetSegmentIndex;


        return true;
    }


    private bool IsValidAvailableTarget(
        int segmentIndex)
    {
        return
            generator != null &&
            segmentIndex >= 0 &&
            segmentIndex < generator.segmentCount &&
            !generator.IsSegmentBlocked(
                segmentIndex
            );
    }


    // =========================================================
    // PERMANENT BLOCK MAINTENANCE
    // =========================================================

    private void RefreshPermanentBlocks()
    {
        if (generator == null ||
            specialBlocks.Count == 0)
        {
            return;
        }


        ReconcileSpecialBlocksWithWheel();


        List<int> indices =
            new List<int>(
                specialBlocks.Keys
            );


        foreach (int index in indices)
        {
            if (!generator.IsSegmentBlocked(index))
                continue;


            generator.AddSegmentBlockSpins(
                index,
                1,
                2
            );
        }


        ReapplyAllSpecialPatterns();
    }


    private void ReconcileSpecialBlocksWithWheel()
    {
        if (generator == null ||
            specialBlocks.Count == 0)
        {
            return;
        }


        List<int> staleIndices =
            new List<int>();


        foreach (KeyValuePair<int, LimboBlockData> entry in
                 specialBlocks)
        {
            int index =
                entry.Key;


            bool validIndex =
                index >= 0 &&
                index < generator.segmentCount;


            if (!validIndex ||
                !generator.IsSegmentBlocked(index))
            {
                staleIndices.Add(index);
            }
        }


        foreach (int index in staleIndices)
        {
            RestoreDefaultBlockedPattern(index);

            specialBlocks.Remove(index);
        }
    }


    // =========================================================
    // EA API - PERMANENT BLOCK
    // =========================================================

    /// <summary>
    /// Creates the next authored Limbo block.
    ///
    /// The EnemyAction asset decides WHAT the block does and its numeric value.
    /// This controller decides WHERE it goes and owns its runtime state.
    /// </summary>
    public void ExecutePermanentBlock(
        LimboBlockEffectType type,
        int effectValue)
    {
        if (Enemy == null ||
            Enemy.IsDead ||
            generator == null)
        {
            return;
        }


        ReconcileSpecialBlocksWithWheel();


        if (!TryGetNextPermanentBlockTarget(
                out int targetIndex))
        {
            LogNoAvailableSegment();
            return;
        }


        bool blocked =
            generator.BlockSegment(
                targetIndex,
                1
            );


        if (!blocked)
        {
            /*
             * Defensive retry if some other effect changed the target between
             * tooltip preview and execution.
             */
            plannedTargetSegmentIndex =
                -1;


            if (!TryGetNextPermanentBlockTarget(
                    out targetIndex))
            {
                LogNoAvailableSegment();
                return;
            }


            blocked =
                generator.BlockSegment(
                    targetIndex,
                    1
                );
        }


        if (!blocked)
        {
            LogNoAvailableSegment();
            return;
        }


        int safeValue =
            NormalizeEffectValue(
                type,
                effectValue
            );


        LimboBlockData data =
            new LimboBlockData(
                type,
                safeValue
            );


        specialBlocks[targetIndex] =
            data;


        ApplySpecialPattern(
            targetIndex,
            data
        );


        /*
         * The current plan has now been consumed. A future Permanent Block EA
         * will choose/cache a new unblocked target when previewed.
         */
        plannedTargetSegmentIndex =
            -1;


        LogPermanentBlockCreated(
            targetIndex,
            data
        );
    }


    // =========================================================
    // EA API - COLLECT + UNLOCK
    // =========================================================

    /// <summary>
    /// Queues Collect + Unlock for the post-money phase of this spin.
    ///
    /// Enemy actions normally execute before RouletteController calculates the
    /// Lucky Shot bonus. Limbo is intentionally different: his swipe must see
    /// every dollar the player earned this spin, including that bonus.
    ///
    /// RoundManager.OnGameplaySpinResolutionCompleted is fired after
    /// ResolveSpinMoneyAndLuckyShotBonus(), so it gives Limbo a clean, existing
    /// lifecycle hook without adding any Limbo branch to RouletteController or
    /// RoundManager.
    /// </summary>
    public void ExecuteCollectUnlock(
        int requestedMoney)
    {
        if (Enemy == null ||
            Enemy.IsDead)
        {
            return;
        }


        collectUnlockPending =
            true;

        pendingCollectMoney =
            Mathf.Max(
                0,
                requestedMoney
            );


        Debug.Log(
            $"[LIMBO] Collect + Unlock queued for post-money resolution: " +
            $"up to ${pendingCollectMoney}."
        );
    }


    private void HandlePostMoneySpinResolution()
    {
        if (!divideMoneyPending &&
            !collectUnlockPending)
        {
            return;
        }


        bool resolveDivide =
            divideMoneyPending;

        int divideSegmentIndex =
            pendingDivideSegmentIndex;

        int divisor =
            pendingMoneyDivisor;

        bool resolveCollect =
            collectUnlockPending;

        int requestedCollect =
            pendingCollectMoney;


        divideMoneyPending =
            false;

        pendingDivideSegmentIndex =
            -1;

        pendingMoneyDivisor =
            1;

        collectUnlockPending =
            false;

        pendingCollectMoney =
            0;


        if (!EncounterActive ||
            Enemy == null ||
            Enemy.IsDead)
        {
            return;
        }


        /*
         * IMPORTANT ORDER:
         *
         * 1. The landed Segment Block resolves first.
         * 2. Limbo's authored Enemy Action resolves second.
         *
         * Therefore, when Divide Money and Collect + Unlock happen on the same
         * spin, the player first has final money divided, then Limbo collects.
         */
        if (resolveDivide)
        {
            ResolveDivideMoneyBlock(
                divideSegmentIndex,
                divisor
            );
        }


        if (resolveCollect)
        {
            ResolveCollectUnlockNow(
                requestedCollect
            );
        }
    }


    private void ResolveCollectUnlockNow(
        int requestedMoney)
    {
        ClearAllLimboBlocks();


        int requested =
            Mathf.Max(
                0,
                requestedMoney
            );


        int taken =
            0;


        if (CurrencyManager.Instance != null &&
            requested > 0)
        {
            taken =
                Mathf.Min(
                    requested,
                    Mathf.Max(
                        0,
                        CurrencyManager.Instance.dollars
                    )
                );


            if (taken > 0)
            {
                CurrencyManager.Instance
                    .Spend(taken);
            }
        }


        LogCollectUnlock(
            requested,
            taken
        );
    }


    // =========================================================
    // CAPTURED BLOCK EFFECT RESOLUTION
    // =========================================================

    /// <summary>
    /// Called by Limbo's current EnemyAction BEFORE that action resolves.
    ///
    /// If a Damage Boss block kills Limbo, the EnemyAction sees IsDead and
    /// stops, so Limbo cannot perform its normal EA after dying.
    /// </summary>
    public bool ResolvePendingBlockEffect()
    {
        /*
         * WheelShifter regenerates SegmentMesh objects. WheelGenerator restores
         * the generic blocked presentation correctly, but it cannot know that a
         * particular block belongs to Limbo. Reapply Limbo's authored procedural
         * pattern at the start of EVERY Limbo EA, even when this spin did not
         * land on a special block.
         */
        ReconcileSpecialBlocksWithWheel();
        ReapplyAllSpecialPatterns();


        if (!pendingBlockEffect)
            return false;


        int segmentIndex =
            pendingBlockSegmentIndex;

        LimboBlockData data =
            pendingBlockData;


        pendingBlockEffect =
            false;

        pendingBlockSegmentIndex =
            -1;


        switch (data.type)
        {
            case LimboBlockEffectType.DamagePlayer:
                ResolvePlayerDamageBlock(
                    segmentIndex,
                    data.value
                );
                break;


            case LimboBlockEffectType.DivideMoney:
                QueueDivideMoneyForPostMoneyResolution(
                    segmentIndex,
                    data.value
                );
                break;


            case LimboBlockEffectType.DamageBoss:
                ResolveBossDamageBlock(
                    segmentIndex,
                    data.value
                );
                break;
        }


        return true;
    }


    private void ResolvePlayerDamageBlock(
        int segmentIndex,
        int requestedDamage)
    {
        int safeDamage =
            Mathf.Max(
                0,
                requestedDamage
            );


        BloodManager.DamageResult result =
            new BloodManager.DamageResult(
                safeDamage,
                0,
                0
            );


        if (BloodManager.Instance != null)
        {
            result =
                BloodManager.Instance
                    .TakeDamage(
                        safeDamage
                    );
        }


        if (GameLogManager.Instance != null)
        {
            string resultText =
                result.bloodLost > 0
                    ? GameLogManager.Instance
                        .BloodText(
                            $"-{result.bloodLost} Blood"
                        )
                    : GameLogManager.Instance
                        .BloodText(
                            "0 Blood lost"
                        );


            GameLogManager.Instance
                .AddGameplayLine(
                    BuildSegmentLabel(segmentIndex) +
                    " triggers Limbo's block: " +
                    resultText
                );
        }


        BloodManager.Instance?
            .FlushDeferredDamageFeedback();


        Debug.Log(
            $"[LIMBO] Damage Player block requested {safeDamage}. " +
            $"Prevented = {result.preventedDamage}, Blood lost = {result.bloodLost}."
        );
    }


    private void QueueDivideMoneyForPostMoneyResolution(
        int segmentIndex,
        int requestedDivisor)
    {
        divideMoneyPending =
            true;

        pendingDivideSegmentIndex =
            segmentIndex;

        pendingMoneyDivisor =
            Mathf.Max(
                1,
                requestedDivisor
            );


        Debug.Log(
            $"[LIMBO] Divide Money block queued for post-money resolution: " +
            $"Segment {segmentIndex + 1}, divisor {pendingMoneyDivisor}."
        );
    }


    private void ResolveDivideMoneyBlock(
        int segmentIndex,
        int requestedDivisor)
    {
        int divisor =
            Mathf.Max(
                1,
                requestedDivisor
            );


        int before =
            CurrencyManager.Instance != null
                ? Mathf.Max(
                    0,
                    CurrencyManager.Instance.dollars
                )
                : 0;


        int after =
            before /
            divisor;


        int removed =
            Mathf.Max(
                0,
                before - after
            );


        if (CurrencyManager.Instance != null &&
            removed > 0)
        {
            CurrencyManager.Instance
                .Spend(removed);
        }


        if (GameLogManager.Instance != null)
        {
            GameLogManager.Instance
                .AddGameplayLine(
                    BuildSegmentLabel(segmentIndex) +
                    " triggers Limbo's block: " +
                    GameLogManager.Instance.MoneyText(
                        $"${before} / {divisor} = ${after}"
                    )
                );
        }


        Debug.Log(
            $"[LIMBO] Divide Money block: ${before} / {divisor} = ${after}."
        );
    }


    private void ResolveBossDamageBlock(
        int segmentIndex,
        int requestedDamage)
    {
        int damage =
            Mathf.Max(
                0,
                requestedDamage
            );


        if (GameLogManager.Instance != null)
        {
            GameLogManager.Instance
                .AddGameplayLine(
                    BuildSegmentLabel(segmentIndex) +
                    " triggers Limbo's block: " +
                    GameLogManager.Instance.EnemyText(
                        Enemy != null
                            ? Enemy.EnemyName
                            : "Limbo"
                    ) +
                    $" takes {damage} damage"
                );
        }


        Enemy?
            .TakeDamage(
                damage
            );


        Debug.Log(
            $"[LIMBO] Damage Boss block deals {damage} damage to Limbo."
        );
    }


    // =========================================================
    // CLEANUP
    // =========================================================

    private void ClearAllLimboBlocks()
    {
        plannedTargetSegmentIndex =
            -1;


        if (generator == null)
        {
            specialBlocks.Clear();
            return;
        }


        RestoreAllSpecialPatterns();


        /*
         * Collect + Unlock is intentionally encounter-wide: it unlocks every
         * currently blocked segment, not only blocks created by Limbo.
         */
        generator.ClearAllSegmentBlocks();


        specialBlocks.Clear();
    }


    // =========================================================
    // PROCEDURAL BLOCK PATTERNS
    // =========================================================

    private SegmentMesh.BlockedPatternType GetBlockedPatternType(
        LimboBlockEffectType type)
    {
        switch (type)
        {
            case LimboBlockEffectType.DamagePlayer:
                return damagePlayerPattern;

            case LimboBlockEffectType.DivideMoney:
                return divideMoneyPattern;

            case LimboBlockEffectType.DamageBoss:
                return damageBossPattern;

            default:
                return SegmentMesh.BlockedPatternType.Diagonal;
        }
    }


    private void ApplySpecialPattern(
        int segmentIndex,
        LimboBlockData data)
    {
        if (!TryGetSegmentMesh(
                segmentIndex,
                out SegmentMesh mesh))
        {
            return;
        }


        mesh.ConfigureBlockedPattern(
            GetBlockedPatternType(
                data.type
            )
        );
    }


    private void ReapplyAllSpecialPatterns()
    {
        if (generator == null)
            return;


        foreach (KeyValuePair<int, LimboBlockData> entry in
                 specialBlocks)
        {
            if (!generator.IsSegmentBlocked(
                    entry.Key))
            {
                continue;
            }


            ApplySpecialPattern(
                entry.Key,
                entry.Value
            );
        }
    }


    private void RestoreAllSpecialPatterns()
    {
        List<int> indices =
            new List<int>(
                specialBlocks.Keys
            );


        foreach (int index in indices)
        {
            RestoreDefaultBlockedPattern(index);
        }
    }


    private void RestoreDefaultBlockedPattern(
        int segmentIndex)
    {
        if (!TryGetSegmentMesh(
                segmentIndex,
                out SegmentMesh mesh))
        {
            return;
        }


        mesh.ConfigureBlockedPattern(
            SegmentMesh.BlockedPatternType.Diagonal
        );
    }


    private bool TryGetSegmentMesh(
        int segmentIndex,
        out SegmentMesh mesh)
    {
        mesh =
            null;


        if (generator == null ||
            generator.segments == null ||
            segmentIndex < 0 ||
            segmentIndex >= generator.segments.Count)
        {
            return false;
        }


        WheelSegmentData data =
            generator.segments[
                segmentIndex
            ];


        if (data == null ||
            data.meshComponent == null)
        {
            return false;
        }


        mesh =
            data.meshComponent;


        return true;
    }


    // =========================================================
    // SEGMENT BLOCK TOOLTIP OVERRIDE
    // =========================================================

    public bool TryGetSegmentBlockTooltipOverride(
        int segmentIndex,
        out SegmentBlockTooltipOverrideData data)
    {
        data =
            default;


        if (!EncounterActive ||
            Enemy == null ||
            Enemy.IsDead ||
            generator == null)
        {
            return false;
        }


        if (!specialBlocks.TryGetValue(
                segmentIndex,
                out LimboBlockData blockData))
        {
            return false;
        }


        string effectText =
            GetBlockEffectDescription(
                blockData.type,
                blockData.value,
                sentenceCase: true
            );


        data =
            new SegmentBlockTooltipOverrideData(
                "Limbo's permanent block",
                "Stickers in this segment do not activate and can't be moved. " +
                effectText + ".",
                "Permanent until Limbo uses Collect + Unlock"
            );


        return true;
    }


    // =========================================================
    // TOOLTIP / PRESENTATION API
    // =========================================================

    public string GetSegmentTooltipLabel(
        int segmentIndex)
    {
        string raw =
            $"Segment {segmentIndex + 1}";


        if (!TryGetSegmentMesh(
                segmentIndex,
                out SegmentMesh mesh))
        {
            return raw;
        }


        string colorHex =
            ColorUtility.ToHtmlStringRGB(
                mesh.color
            );


        return
            $"<color=#{colorHex}>{raw}</color>";
    }


    public string GetBlockEffectDescription(
        LimboBlockEffectType type,
        int effectValue,
        bool sentenceCase = false)
    {
        int safeValue =
            NormalizeEffectValue(
                type,
                effectValue
            );


        string text;


        switch (type)
        {
            case LimboBlockEffectType.DamagePlayer:
                text =
                    $"landing here deals {safeValue} Blood damage";
                break;


            case LimboBlockEffectType.DivideMoney:
                text =
                    $"at the end of the spin, divide your money by {safeValue}";
                break;


            case LimboBlockEffectType.DamageBoss:
                text =
                    $"landing here deals {safeValue} damage to Limbo";
                break;


            default:
                text =
                    "landing here triggers a special effect";
                break;
        }


        if (!sentenceCase ||
            string.IsNullOrEmpty(text))
        {
            return text;
        }


        return
            char.ToUpperInvariant(text[0]) +
            text.Substring(1);
    }


    private int NormalizeEffectValue(
        LimboBlockEffectType type,
        int value)
    {
        if (type ==
            LimboBlockEffectType.DivideMoney)
        {
            return
                Mathf.Max(
                    1,
                    value
                );
        }


        return
            Mathf.Max(
                0,
                value
            );
    }


    // =========================================================
    // LOGGING
    // =========================================================

    private void LogPermanentBlockCreated(
        int segmentIndex,
        LimboBlockData data)
    {
        if (GameLogManager.Instance != null)
        {
            string effectDescription =
                GetBlockEffectDescription(
                    data.type,
                    data.value
                );


            GameLogManager.Instance
                .AddGameplayLine(
                    GameLogManager.Instance.EnemyText(
                        Enemy != null
                            ? Enemy.EnemyName
                            : "Limbo"
                    ) +
                    " permanently blocks " +
                    BuildSegmentLabel(segmentIndex) +
                    ": " +
                    effectDescription
                );
        }


        Debug.Log(
            $"[LIMBO] Permanently blocked segment {segmentIndex + 1} " +
            $"with effect {data.type} ({data.value})."
        );
    }


    private void LogCollectUnlock(
        int requested,
        int taken)
    {
        if (GameLogManager.Instance != null)
        {
            GameLogManager.Instance
                .AddGameplayLine(
                    GameLogManager.Instance.EnemyText(
                        Enemy != null
                            ? Enemy.EnemyName
                            : "Limbo"
                    ) +
                    " unlocks all segments and collects " +
                    GameLogManager.Instance.MoneyText(
                        $"-${taken}"
                    )
                );
        }


        Debug.Log(
            $"[LIMBO] Collect Unlock requested ${requested}; actually took ${taken}."
        );
    }


    private void LogNoAvailableSegment()
    {
        if (GameLogManager.Instance != null)
        {
            GameLogManager.Instance
                .AddGameplayLine(
                    GameLogManager.Instance.EnemyText(
                        Enemy != null
                            ? Enemy.EnemyName
                            : "Limbo"
                    ) +
                    " has no unblocked segment available"
                );
        }


        Debug.Log(
            "[LIMBO] Permanent Block has no unblocked segment available."
        );
    }


    private string BuildSegmentLabel(
        int segmentIndex)
    {
        string raw =
            $"Segment {segmentIndex + 1}";


        if (GameLogManager.Instance == null)
            return raw;


        if (TryGetSegmentMesh(
                segmentIndex,
                out SegmentMesh mesh))
        {
            return
                GameLogManager.Instance
                    .SegmentText(
                        raw,
                        mesh.color
                    );
        }


        return
            GameLogManager.Instance
                .SegmentText(raw);
    }
}
