using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StickerShield",
    menuName = "Stickers/Sticker Shield"
)]
public class StickerShield : StickerEffect
{
    // =========================================================
    // CONFIGURATION
    // =========================================================

    [Header("Shield")]

    [Tooltip(
        "Total damage this Shield can prevent during the current valid spin " +
        "when it is on the winning segment."
    )]
    [Min(0)]
    public int winningSegmentBlock =
        10;


    [Tooltip(
        "Total damage this Shield can prevent during the current valid spin " +
        "when it is on a non-winning segment."
    )]
    [Min(0)]
    public int losingSegmentBlock =
        6;


    // =========================================================
    // PREPARATION
    // =========================================================

    /// <summary>
    /// Shield is a reactive effect.
    ///
    /// It does NOT activate merely because the spin finished.
    /// Instead, it registers one temporary damage pool for THIS VALID SPIN.
    ///
    /// The pool is cumulative across every damage event in that spin:
    /// a 6-point Shield can block 2 + 2 + 2 from three different enemies.
    ///
    /// Any unused capacity disappears when the spin resolution ends.
    /// It NEVER carries over into a future spin.
    ///
    /// A use is consumed only if this pool actually prevents damage.
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


        int capacity =
            GetBlockAmount(
                location
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
    /// Shield's real activation happens only when damage reaches the player.
    /// This is what guarantees "Only consumes use when prevents damage."
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


        /*
         * IMPORTANT ORDERING:
         *
         * BloodManager has already prevented the damage at this point, but
         * the damage source (enemy / Rat Poison / future source) has not yet
         * written its own log line.
         *
         * Consume the use NOW so gameplay state is correct, but defer Shield's
         * visual feedback. The damage source will flush it immediately after
         * logging itself.
         */
        bool shouldLogUsesRemaining =
            false;

        int usesRemainingAfterActivation =
            owner.RemainingUses;


        if (blockEvent.firstPreventionForBlocker)
        {
            bool shouldConsumeUse =
                ShouldConsumeUseOnActivation(
                    location
                );


            if (shouldConsumeUse &&
                HasLimitedUses)
            {
                owner.ConsumeUseAfterActivation(
                    false
                );

                shouldLogUsesRemaining =
                    true;

                usesRemainingAfterActivation =
                    owner.RemainingUses;
            }
        }


        string safeStickerName =
            string.IsNullOrWhiteSpace(
                stickerName
            )
                ? "Shield"
                : stickerName;


        int preventedDamage =
            blockEvent.preventedDamage;

        int remainingCapacity =
            blockEvent.remainingCapacity;


        BloodManager.Instance?
            .QueueDeferredDamageFeedback(
                () =>
                {
                    LogDeferredPrevention(
                        safeStickerName,
                        preventedDamage,
                        remainingCapacity,
                        shouldLogUsesRemaining,
                        usesRemainingAfterActivation
                    );
                }
            );
    }


    private void LogDeferredPrevention(
        string safeStickerName,
        int preventedDamage,
        int remainingCapacity,
        bool logUsesRemaining,
        int usesRemaining)
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


        if (logUsesRemaining)
        {
            GameLogManager.Instance
                .AddGameplayLine(
                    GameLogManager.Instance
                        .StickerText(
                            safeStickerName
                        ) +
                    $" uses remaining: {usesRemaining}"
                );
        }
    }


    // =========================================================
    // BLOCK AMOUNT
    // =========================================================

    private int GetBlockAmount(
        StickerSpinLocation location)
    {
        switch (location)
        {
            case StickerSpinLocation.WinningSegment:
                return
                    Mathf.Max(
                        0,
                        winningSegmentBlock
                    );


            case StickerSpinLocation.NonWinningSegment:
                return
                    Mathf.Max(
                        0,
                        losingSegmentBlock
                    );
        }


        return 0;
    }


    // =========================================================
    // USE CONSUMPTION
    // =========================================================

    /// <summary>
    /// Limited-use Shields display their uses beside BOTH roulette locations.
    ///
    /// The method returning true does NOT mean a use is spent every spin:
    /// RegisterActivation is only called after real damage prevention.
    /// </summary>
    public override bool ShouldConsumeUseOnActivation(
        StickerSpinLocation location)
    {
        return
            location ==
                StickerSpinLocation.WinningSegment ||
            location ==
                StickerSpinLocation.NonWinningSegment;
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
    /// Custom tooltip tokens:
    ///
    /// {block}         -> protection for the queried location
    /// {winningBlock}  -> configured winning protection
    /// {losingBlock}   -> configured losing protection
    ///
    /// Recommended text:
    /// Winning Segment:
    ///   Blocks up to {block} damage. Only consumes a use when it prevents damage.
    ///
    /// Losing Segment:
    ///   Blocks up to {block} damage. Only consumes a use when it prevents damage.
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


        return
            resolved
                .Replace(
                    "{block}",
                    GetBlockAmount(location)
                        .ToString()
                )
                .Replace(
                    "{winningBlock}",
                    Mathf.Max(
                        0,
                        winningSegmentBlock
                    )
                    .ToString()
                )
                .Replace(
                    "{losingBlock}",
                    Mathf.Max(
                        0,
                        losingSegmentBlock
                    )
                    .ToString()
                );
    }
}
