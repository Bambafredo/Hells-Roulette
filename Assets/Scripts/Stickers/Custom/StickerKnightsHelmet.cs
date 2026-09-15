using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StickerKnightsHelmet",
    menuName = "Stickers/Sticker Knight's Helmet"
)]
public class StickerKnightsHelmet : StickerEffect
{
    // =========================================================
    // PREPARATION
    // =========================================================

    /// <summary>
    /// Knight's Helmet is reactive protection, like Shield.
    ///
    /// On every valid spin, while it is on either a winning or non-winning
    /// roulette segment, it registers a temporary damage-block pool equal to
    /// this physical sticker's CURRENT remaining uses.
    ///
    /// Example:
    /// 3 uses remaining -> 3 total Block for this spin.
    ///
    /// The Losing version consumes one use the first time it actually prevents
    /// damage. The Winning version never consumes a use.
    /// </summary>
    public override void PrepareSpinLocation(
        BaseSticker owner,
        StickerSpinLocation location)
    {
        if (RoundManager.Instance != null &&
            !RoundManager.Instance.WasLastSpinValid)
        {
            return;
        }


        if (owner == null ||
            BloodManager.Instance == null)
        {
            return;
        }


        if (location !=
                StickerSpinLocation.WinningSegment &&
            location !=
                StickerSpinLocation.NonWinningSegment)
        {
            return;
        }


        /*
         * This sticker is designed around limited uses.
         *
         * Max Uses = 0 means unlimited in the generic StickerEffect system,
         * which would make "Block equal to uses remaining" undefined.
         */
        if (!owner.HasLimitedUses)
            return;


        int capacity =
            Mathf.Max(
                0,
                owner.RemainingUses
            );


        if (capacity <= 0)
            return;


        BloodManager.Instance
            .RegisterSpinDamageBlocker(
                owner,
                capacity,
                blockEvent =>
                    HandleDamagePrevented(
                        owner,
                        location,
                        blockEvent
                    )
            );
    }


    // =========================================================
    // NORMAL RESOLUTION
    // =========================================================

    /// <summary>
    /// Intentionally empty.
    ///
    /// Protection is registered in PrepareSpinLocation() so it exists before
    /// damage-dealing stickers and enemy actions begin resolving.
    ///
    /// A Losing Helmet does NOT spend a use merely because the spin happened:
    /// it spends one only if it actually prevents damage.
    /// </summary>
    public override void ResolveSpinLocation(
        BaseSticker owner,
        StickerSpinLocation location)
    {
    }


    // =========================================================
    // DAMAGE CALLBACK
    // =========================================================

    private void HandleDamagePrevented(
        BaseSticker owner,
        StickerSpinLocation location,
        BloodManager.DamageBlockEvent blockEvent)
    {
        if (blockEvent.preventedDamage <= 0 ||
            owner == null)
        {
            return;
        }


        string safeStickerName =
            string.IsNullOrWhiteSpace(
                stickerName
            )
                ? "Knight's Helmet"
                : stickerName;


        int preventedDamage =
            blockEvent.preventedDamage;

        int remainingCapacity =
            blockEvent.remainingCapacity;


        /*
         * Keep the same deferred-feedback ordering as Shield:
         *
         * Damage source
         * Knight's Helmet blocks...
         * Burnout...                (if relevant)
         * Knight's Helmet uses...
         */
        BloodManager.Instance?
            .QueueDeferredDamageFeedback(
                () =>
                {
                    LogDeferredPrevention(
                        safeStickerName,
                        preventedDamage,
                        remainingCapacity
                    );
                }
            );


        /*
         * WINNING:
         * Protection is completely free.
         *
         * LOSING:
         * Consume exactly one use on the FIRST real prevention made by this
         * blocker during the spin.
         *
         * BaseSticker's generic use-consumption hook remains in charge, so
         * Burnout can still turn this 1-use cost into "consume all uses".
         */
        if (location ==
                StickerSpinLocation.NonWinningSegment &&
            blockEvent.firstPreventionForBlocker &&
            owner.HasLimitedUses)
        {
            owner.ConsumeUseAfterActivation(
                false
            );


            int usesRemainingAfterActivation =
                owner.RemainingUses;


            BloodManager.Instance?
                .QueueDeferredDamageFeedback(
                    () =>
                    {
                        LogDeferredUsesRemaining(
                            safeStickerName,
                            usesRemainingAfterActivation
                        );
                    }
                );
        }
    }


    // =========================================================
    // DEFERRED LOG
    // =========================================================

    private void LogDeferredPrevention(
        string safeStickerName,
        int preventedDamage,
        int remainingCapacity)
    {
        if (GameLogManager.Instance == null)
            return;


        string preventedText =
            GameLogManager.Instance
                .ProtectionText(
                    $"{preventedDamage} damage"
                );


        string remainingText =
            GameLogManager.Instance
                .ProtectionText(
                    $"{remainingCapacity} block"
                );


        GameLogManager.Instance
            .AddGameplayLine(
                GameLogManager.Instance
                    .StickerText(
                        safeStickerName
                    ) +
                " blocks: " +
                preventedText +
                " (" +
                remainingText +
                " remaining this spin)"
            );
    }


    private void LogDeferredUsesRemaining(
        string safeStickerName,
        int usesRemaining)
    {
        if (GameLogManager.Instance == null)
            return;


        GameLogManager.Instance
            .AddGameplayLine(
                GameLogManager.Instance
                    .StickerText(
                        safeStickerName
                    ) +
                $" uses remaining: {usesRemaining}"
            );
    }


    // =========================================================
    // USE CONSUMPTION
    // =========================================================

    /// <summary>
    /// We handle the Losing use cost manually when damage is actually blocked.
    ///
    /// Returning false here prevents generic automatic consumption from ever
    /// charging the Winning location.
    /// </summary>
    public override bool ShouldConsumeUseOnActivation(
        StickerSpinLocation location)
    {
        return false;
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


    /// <summary>
    /// Custom tokens:
    ///
    /// {block} -> current Block this physical Helmet would provide
    /// {uses}  -> current remaining uses
    ///
    /// Both are the same value by design.
    /// </summary>
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


        int uses =
            GetCurrentUses(
                owner
            );


        return
            resolved
                .Replace(
                    "{block}",
                    uses.ToString()
                )
                .Replace(
                    "{uses}",
                    uses.ToString()
                );
    }


    private int GetCurrentUses(
        BaseSticker owner)
    {
        if (owner != null &&
            owner.HasLimitedUses)
        {
            return
                Mathf.Max(
                    0,
                    owner.RemainingUses
                );
        }


        /*
         * Inspector / fallback context without a physical BaseSticker.
         */
        return
            Mathf.Max(
                0,
                maxUses
            );
    }
}
