using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StickerCupon",
    menuName = "Stickers/Sticker Cupon"
)]
public class StickerCupon : StickerEffect
{
    // =========================================================
    // SPIN RESOLUTION
    // =========================================================

    public override void ResolveSpinLocation(
        BaseSticker owner,
        StickerSpinLocation location)
    {
        if (RoundManager.Instance != null &&
            !RoundManager.Instance.WasLastSpinValid)
        {
            return;
        }


        if (location !=
            StickerSpinLocation.WinningSegment)
        {
            /*
             * Cupon's Album effect is passive during Reward Phase.
             * It deliberately does NOT activate once per gameplay spin.
             */
            return;
        }


        if (owner == null)
        {
            Debug.LogWarning(
                "StickerCupon: BaseSticker owner was not provided."
            );

            return;
        }


        ResolveWinningSegment(
            owner
        );
    }


    private void ResolveWinningSegment(
        BaseSticker owner)
    {
        int remainingUsesBeforeActivation =
            owner.HasLimitedUses
                ? Mathf.Max(
                    0,
                    owner.RemainingUses
                )
                : 0;


        string description =
            owner.HasLimitedUses
                ? $"Get a free sticker. Consume all " +
                  $"{remainingUsesBeforeActivation} remaining uses"
                : "Get a free sticker. Consume this sticker";


        /*
         * Win consumes ALL remaining uses, so the normal one-use consumption
         * route must be bypassed for this activation.
         */
        RegisterActivation(
            owner,
            StickerSpinLocation.WinningSegment,
            description,
            0,
            false
        );


        if (RewardManager.Instance != null)
        {
            RewardManager.Instance
                .RequestFreeStickerReward(
                    stickerName
                );
        }
        else
        {
            Debug.LogWarning(
                "[CUPON] RewardManager missing. Free sticker could not be queued."
            );
        }


        ConsumeAllUsesAfterWin(
            owner,
            remainingUsesBeforeActivation
        );
    }


    private void ConsumeAllUsesAfterWin(
        BaseSticker owner,
        int remainingUses)
    {
        if (owner == null)
            return;


        if (!owner.HasLimitedUses)
        {
            /*
             * Cupon is intended to have limited uses. This fallback still
             * preserves the design promise that a winning Cupon disappears.
             */
            owner.DestroyFromGameplay(
                "Cupon redeemed on winning segment"
            );

            return;
        }


        int usesToConsume =
            Mathf.Max(
                0,
                remainingUses
            );


        for (int i = 0;
             i < usesToConsume;
             i++)
        {
            /*
             * The activation line already states that ALL uses are consumed.
             * Avoid printing one remaining-use line for every use removed.
             */
            owner.ConsumeUseAfterActivation(
                false
            );
        }
    }


    // =========================================================
    // PASSIVE REWARD REROLL EFFECT
    // =========================================================

    public override bool CanProvideFreeRewardReroll(
        BaseSticker owner)
    {
        if (owner == null ||
            owner.IsConsumed ||
            owner.IsPendingGameplayDestruction ||
            !owner.HasLimitedUses ||
            owner.RemainingUses <= 0)
        {
            return false;
        }


        if (AlbumManager.Instance == null)
            return false;


        return
            AlbumManager.Instance
                .IsStickerInAlbum(
                    owner
                );
    }


    public override bool TryConsumeFreeRewardReroll(
        BaseSticker owner)
    {
        if (!CanProvideFreeRewardReroll(
                owner))
        {
            return false;
        }


        if (GameLogManager.Instance != null)
        {
            GameLogManager.Instance
                .AddGameplayLine(
                    GameLogManager.Instance
                        .StickerText(
                            stickerName
                        ) +
                    " makes this reroll free"
                );
        }


        /*
         * One successful Reward reroll consumes exactly one use from this
         * physical Cupon. BaseSticker owns the runtime use counter and handles
         * destruction when the final use reaches zero.
         */
        owner.ConsumeUseAfterActivation();


        return true;
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
                StickerSpinLocation.Album;
    }


    protected override string ResolveTooltipTokens(
        BaseSticker owner,
        StickerSpinLocation location,
        string template)
    {
        string result =
            base.ResolveTooltipTokens(
                owner,
                location,
                template
            );


        int remainingUses =
            owner != null &&
            owner.HasLimitedUses
                ? Mathf.Max(
                    0,
                    owner.RemainingUses
                )
                : 0;


        return
            result.Replace(
                "{remainingUses}",
                remainingUses.ToString()
            );
    }
}
