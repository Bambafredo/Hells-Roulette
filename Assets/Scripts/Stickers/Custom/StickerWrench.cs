using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StickerWrench",
    menuName = "Stickers/Sticker Wrench"
)]
public class StickerWrench : StickerEffect
{
    [Header("Wrench")]
    [Min(0)]
    public int damageAmount = 1;


    public override void ResolveSpinLocation(
        BaseSticker owner,
        StickerSpinLocation location)
    {
        if (RoundManager.Instance != null &&
            !RoundManager.Instance.WasLastSpinValid)
        {
            return;
        }

        if (owner == null)
            return;

        switch (location)
        {
            case StickerSpinLocation.WinningSegment:
                ResolveWinning(owner);
                break;

            case StickerSpinLocation.NonWinningSegment:
                ResolveLosing(owner);
                break;
        }
    }


    // =========================================================
    // WIN
    // =========================================================

    private void ResolveWinning(
        BaseSticker owner)
    {
        EnemyPanelManager enemyPanel =
            Object.FindObjectOfType<EnemyPanelManager>();

        BaseEnemy target =
            enemyPanel != null
                ? StickerTargetingUtility.GetFirstDamageTarget(
                    enemyPanel,
                    owner,
                    StickerSpinLocation.WinningSegment
                )
                : null;

        if (target == null)
        {
            RegisterActivation(
                owner,
                StickerSpinLocation.WinningSegment,
                "No valid target",
                0,
                null
            );

            return;
        }

        string targetName =
            target.enemyName;

        if (GameLogManager.Instance != null)
        {
            targetName =
                GameLogManager.Instance
                    .EnemyText(targetName);
        }

        RegisterActivation(
            owner,
            StickerSpinLocation.WinningSegment,
            $"Deal {Mathf.Max(0, damageAmount)} damage to {targetName}",
            0,
            null
        );

        target.TakeDamage(
            Mathf.Max(0, damageAmount)
        );
    }


    // =========================================================
    // LOSE
    // =========================================================

    private void ResolveLosing(
        BaseSticker owner)
    {
        List<BaseSticker> candidates =
            GetRepairCandidates(owner);

        if (candidates.Count <= 0)
        {
            RegisterActivation(
                owner,
                StickerSpinLocation.NonWinningSegment,
                "No sticker needs repair",
                0,
                null
            );

            return;
        }

        BaseSticker target =
            candidates[
                Random.Range(
                    0,
                    candidates.Count
                )
            ];

        int restored =
            target.RestoreUses(1);

        string targetName =
            target.effect != null &&
            !string.IsNullOrWhiteSpace(
                target.effect.stickerName
            )
                ? target.effect.stickerName
                : target.name;

        if (GameLogManager.Instance != null)
        {
            targetName =
                GameLogManager.Instance
                    .StickerText(targetName);
        }

        RegisterActivation(
            owner,
            StickerSpinLocation.NonWinningSegment,
            restored > 0
                ? $"Restore 1 use to {targetName}"
                : $"Could not restore a use to {targetName}",
            0,
            false
        );

        /*
         * Conditional consumption, same pattern as the other stickers whose
         * use cost depends on the effect actually succeeding.
         *
         * RegisterActivation never consumes Wrench's use here.
         * We spend exactly one use manually only after a real repair.
         */
        if (restored > 0 &&
            consumeUseOnNonWinningActivation)
        {
            owner.ConsumeUseAfterActivation();
        }
    }


    private List<BaseSticker> GetRepairCandidates(
        BaseSticker owner)
    {
        List<BaseSticker> candidates =
            new List<BaseSticker>();

        if (owner == null ||
            owner.currentSegment == null)
        {
            return candidates;
        }

        BaseSticker[] stickers =
            owner.currentSegment
                .GetComponentsInChildren<BaseSticker>(
                    true
                );

        foreach (BaseSticker candidate in stickers)
        {
            if (candidate == null ||
                candidate == owner ||
                candidate.IsConsumed ||
                candidate.IsPendingGameplayDestruction ||
                !candidate.isPlaced ||
                candidate.currentSegment != owner.currentSegment ||
                !candidate.HasLimitedUses ||
                candidate.effect == null ||
                candidate.RemainingUses <= 0 ||
                candidate.RemainingUses >= candidate.effect.maxUses)
            {
                continue;
            }

            candidates.Add(candidate);
        }

        return candidates;
    }


    // =========================================================
    // CONDITIONAL USE CONSUMPTION
    // =========================================================

    /*
     * IMPORTANT:
     * Wrench must report FALSE to the generic use-consumption system.
     *
     * Its Losing cost is conditional, so the generic roulette/effect flow
     * must never consume it merely because the Non-Winning checkbox is on.
     *
     * ResolveLosing() still reads the authored checkbox explicitly through
     * base.ShouldConsumeUseOnActivation(...) and consumes one use manually
     * only after RestoreUses(1) actually succeeds.
     */
    public override bool ShouldConsumeUseOnActivation(
        StickerSpinLocation location)
    {
        return false;
    }


    // =========================================================
    // CONDITIONAL USE TOOLTIP
    // =========================================================

    protected override bool DefaultShowsUseConsumptionTag(
        StickerSpinLocation location)
    {
        if (location ==
            StickerSpinLocation.NonWinningSegment)
        {
            // Consumption is conditional, so the generic unconditional
            // "[Consumes 1 use]" tag would be misleading.
            return false;
        }

        return
            base.DefaultShowsUseConsumptionTag(
                location
            );
    }


    // =========================================================
    // TOOLTIP
    // =========================================================

    protected override bool SupportsTooltipLocation(
        StickerSpinLocation location)
    {
        return
            location ==
                StickerSpinLocation.WinningSegment ||
            location ==
                StickerSpinLocation.NonWinningSegment;
    }


    protected override string ResolveTooltipTokens(
        BaseSticker owner,
        StickerSpinLocation location,
        string template)
    {
        string resolved =
            base.ResolveTooltipTokens(
                owner,
                location,
                template
            );

        return
            resolved.Replace(
                "{damage}",
                Mathf.Max(
                    0,
                    damageAmount
                ).ToString()
            );
    }
}
