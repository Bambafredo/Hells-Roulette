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
/// - the special effect captured when a spin lands on one of those blocks;
/// - cleanup when Limbo leaves the encounter.
/// </summary>
public class LimboBossController : BossEncounterController
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


    [Serializable]
    public class LimboPatternStyle
    {
        public Texture texture;

        public Color color =
            Color.black;

        [Range(0f, 1f)]
        public float opacity =
            0.8f;

        [Min(0.01f)]
        public float scale =
            4f;

        public float rotation =
            0f;
    }


    // =========================================================
    // BLOCK EFFECT VALUES
    // =========================================================

    [Header("Limbo - Special Block Effects")]

    [Tooltip(
        "Blood damage requested when the player lands on a Damage Player block. " +
        "Uses BloodManager.TakeDamage(), so Shield and future mitigation work."
    )]
    [Min(0)]
    public int playerDamage =
        5;


    [Tooltip(
        "Current dollars are divided by this integer, rounded down. " +
        "Example: $17 / 2 = $8."
    )]
    [Min(1)]
    public int moneyDivisor =
        2;


    [Tooltip(
        "Damage Limbo takes when the player lands on a Damage Boss block."
    )]
    [Min(0)]
    public int bossDamage =
        5;


    // =========================================================
    // PATTERNS
    // =========================================================

    [Header("Limbo - Block Patterns")]

    public LimboPatternStyle damagePlayerPattern =
        new LimboPatternStyle();

    public LimboPatternStyle divideMoneyPattern =
        new LimboPatternStyle();

    public LimboPatternStyle damageBossPattern =
        new LimboPatternStyle();


    // =========================================================
    // RUNTIME STATE
    // =========================================================

    private readonly Dictionary<int, LimboBlockEffectType>
        specialBlocks =
            new Dictionary<int, LimboBlockEffectType>();


    private bool pendingBlockEffect =
        false;

    private int pendingBlockSegmentIndex =
        -1;

    private LimboBlockEffectType pendingBlockType;


    private RouletteController roulette;
    private WheelGenerator generator;


    // =========================================================
    // PUBLIC DEBUG STATE
    // =========================================================

    public int ActiveSpecialBlockCount =>
        specialBlocks.Count;


    public bool HasPendingBlockEffect =>
        pendingBlockEffect;


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
    }


    protected override void OnBossEncounterActivated()
    {
        /*
         * Normal round transitions already enter with tokens available.
         * This is only a defensive escape hatch for debug/manual corridor
         * transitions that happen to enter Limbo with zero tokens/debt pending.
         */
        if (RoundManagerRef != null &&
            RoundManagerRef.TokensRemaining <= 0)
        {
            RoundManagerRef.AddTokens(1);
        }


        ReconcileSpecialBlocksWithWheel();
        ReapplyAllSpecialPatterns();


        Debug.Log(
            "[LIMBO] Boss encounter active. Valid spins will not consume tokens."
        );
    }


    protected override void OnBossEncounterDeactivated()
    {
        ClearAllLimboBlocks();

        pendingBlockEffect =
            false;
    }


    protected override void OnBossDefeated()
    {
        ClearAllLimboBlocks();

        pendingBlockEffect =
            false;


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
         * RoundManager invokes OnSpinValidated BEFORE its private SpendToken().
         * Adding one token here and letting RoundManager spend normally leaves
         * the count unchanged, without adding boss branches to RoundManager.
         */
        if (RoundManagerRef != null)
        {
            RoundManagerRef.AddTokens(1);
        }


        /*
         * Snapshot the special block under the physical winner BEFORE sticker
         * effects can regenerate the wheel. Resolution happens later, at the
         * beginning of Limbo's Enemy Action.
         */
        CapturePendingWinningBlockEffect();


        /*
         * Limbo blocks are created with 1 remaining future valid spin.
         * Refreshing each existing block from 1 -> 2 here, before the normal
         * WheelGenerator countdown, makes it return to 1 after the current
         * valid spin. It therefore remains permanent while Limbo is active.
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
                out LimboBlockEffectType type))
        {
            return;
        }


        pendingBlockEffect =
            true;

        pendingBlockSegmentIndex =
            winningIndex;

        pendingBlockType =
            type;
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


        foreach (KeyValuePair<int, LimboBlockEffectType> entry in
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
            RestoreFoundationPattern(index);
            specialBlocks.Remove(index);
        }
    }


    // =========================================================
    // EA API - PERMANENT BLOCK
    // =========================================================

    public void ExecutePermanentBlock()
    {
        if (Enemy == null ||
            Enemy.IsDead ||
            generator == null)
        {
            return;
        }


        ReconcileSpecialBlocksWithWheel();


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
        {
            LogNoAvailableSegment();
            return;
        }


        int targetIndex =
            candidates[
                UnityEngine.Random.Range(
                    0,
                    candidates.Count
                )
            ];


        LimboBlockEffectType type =
            (LimboBlockEffectType)
            UnityEngine.Random.Range(
                0,
                3
            );


        bool blocked =
            generator.BlockSegment(
                targetIndex,
                1
            );


        if (!blocked)
        {
            LogNoAvailableSegment();
            return;
        }


        specialBlocks[targetIndex] =
            type;


        ApplySpecialPattern(
            targetIndex,
            type
        );


        LogPermanentBlockCreated(
            targetIndex,
            type
        );
    }


    // =========================================================
    // EA API - COLLECT + UNLOCK
    // =========================================================

    public void ExecuteCollectUnlock(
        int requestedMoney)
    {
        if (Enemy == null ||
            Enemy.IsDead)
        {
            return;
        }


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
    /// Called by Limbo's authored EnemyAction BEFORE that action resolves.
    ///
    /// Returning true means a special block effect was actually resolved.
    /// If the Damage Boss block kills Limbo, the EnemyAction checks IsDead and
    /// stops, so the boss cannot perform its normal EA after dying.
    /// </summary>
    public bool ResolvePendingBlockEffect()
    {
        if (!pendingBlockEffect)
            return false;


        int segmentIndex =
            pendingBlockSegmentIndex;

        LimboBlockEffectType type =
            pendingBlockType;


        pendingBlockEffect =
            false;

        pendingBlockSegmentIndex =
            -1;


        /*
         * A WheelShifter may have regenerated SegmentMesh objects during the
         * sticker pass. Reapply surviving Limbo patterns now, after stickers
         * and before the boss EA.
         */
        ReconcileSpecialBlocksWithWheel();
        ReapplyAllSpecialPatterns();


        switch (type)
        {
            case LimboBlockEffectType.DamagePlayer:
                ResolvePlayerDamageBlock(
                    segmentIndex
                );
                break;


            case LimboBlockEffectType.DivideMoney:
                ResolveDivideMoneyBlock(
                    segmentIndex
                );
                break;


            case LimboBlockEffectType.DamageBoss:
                ResolveBossDamageBlock(
                    segmentIndex
                );
                break;
        }


        return true;
    }


    private void ResolvePlayerDamageBlock(
        int segmentIndex)
    {
        int requestedDamage =
            Mathf.Max(
                0,
                playerDamage
            );


        BloodManager.DamageResult result =
            new BloodManager.DamageResult(
                requestedDamage,
                0,
                0
            );


        if (BloodManager.Instance != null)
        {
            result =
                BloodManager.Instance
                    .TakeDamage(
                        requestedDamage
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
            $"[LIMBO] Damage Player block requested {requestedDamage}. " +
            $"Prevented = {result.preventedDamage}, Blood lost = {result.bloodLost}."
        );
    }


    private void ResolveDivideMoneyBlock(
        int segmentIndex)
    {
        int divisor =
            Mathf.Max(
                1,
                moneyDivisor
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
        int segmentIndex)
    {
        int damage =
            Mathf.Max(
                0,
                bossDamage
            );


        /*
         * Log the damage BEFORE TakeDamage(). If this kills Limbo,
         * BaseEnemy.Die() then appends the normal "Limbo dies" line after it.
         */
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
        if (generator == null)
        {
            specialBlocks.Clear();
            return;
        }


        RestoreAllSpecialPatterns();


        /*
         * Limbo's unlock/defeat is an encounter-wide reset. The current
         * WheelGenerator has only a public ClearAllSegmentBlocks API, so this
         * deliberately clears every active Segment Block, including any normal
         * block that happened to survive into the boss encounter.
         */
        generator.ClearAllSegmentBlocks();


        specialBlocks.Clear();
    }


    // =========================================================
    // PATTERNS
    // =========================================================

    private LimboPatternStyle GetPatternStyle(
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
                return null;
        }
    }


    private void ApplySpecialPattern(
        int segmentIndex,
        LimboBlockEffectType type)
    {
        if (!TryGetSegmentMesh(
                segmentIndex,
                out SegmentMesh mesh))
        {
            return;
        }


        LimboPatternStyle style =
            GetPatternStyle(type);


        if (style == null)
            return;


        mesh.ConfigureCosmeticPattern(
            style.texture,
            style.color,
            style.opacity,
            style.scale,
            style.rotation
        );
    }


    private void ReapplyAllSpecialPatterns()
    {
        if (generator == null)
            return;


        foreach (KeyValuePair<int, LimboBlockEffectType> entry in
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
            RestoreFoundationPattern(index);
        }
    }


    private void RestoreFoundationPattern(
        int segmentIndex)
    {
        if (generator == null ||
            !TryGetSegmentMesh(
                segmentIndex,
                out SegmentMesh mesh))
        {
            return;
        }


        mesh.ConfigureCosmeticPattern(
            generator.cosmeticPatternTexture,
            generator.cosmeticPatternColor,
            generator.cosmeticPatternOpacity,
            generator.cosmeticPatternScale,
            generator.cosmeticPatternRotation
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
    // LOGGING
    // =========================================================

    private void LogPermanentBlockCreated(
        int segmentIndex,
        LimboBlockEffectType type)
    {
        if (GameLogManager.Instance != null)
        {
            string effectDescription =
                GetBlockEffectDescription(type);


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
            $"with effect {type}."
        );
    }


    private string GetBlockEffectDescription(
        LimboBlockEffectType type)
    {
        switch (type)
        {
            case LimboBlockEffectType.DamagePlayer:
                return
                    $"landing here deals {Mathf.Max(0, playerDamage)} Blood damage";


            case LimboBlockEffectType.DivideMoney:
                return
                    $"landing here divides current money by {Mathf.Max(1, moneyDivisor)}";


            case LimboBlockEffectType.DamageBoss:
                return
                    $"landing here deals {Mathf.Max(0, bossDamage)} damage to Limbo";


            default:
                return
                    "special effect";
        }
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
