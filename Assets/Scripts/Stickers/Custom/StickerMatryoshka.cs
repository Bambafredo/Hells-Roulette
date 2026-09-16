using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StickerMatryoshka",
    menuName = "Stickers/Sticker Matryoshka"
)]
public class StickerMatryoshka : StickerEffect
{
    // =========================================================
    // CONFIGURATION
    // =========================================================

    [Header("Matryoshka")]

    [Tooltip(
        "Exact physical sticker prefab granted as a bonus when this Matryoshka " +
        "wins on a Lucky Shot while it is still at maximum uses. Leave empty " +
        "for the smallest Matryoshka in the chain."
    )]
    public GameObject bonusStickerPrefab;


    // =========================================================
    // EFFECT
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
            return;
        }


        if (owner == null)
        {
            Debug.LogWarning(
                "StickerMatryoshka: BaseSticker owner was not provided."
            );

            return;
        }


        /*
         * IMPORTANT:
         * The bonus condition is captured BEFORE RegisterActivation(), because
         * a normal winning activation may consume one use immediately.
         *
         * Example:
         * Large Matryoshka at 3/3 + Lucky Shot -> bonus is eligible, then the
         * normal activation may reduce the physical sticker to 2/3.
         */
        bool luckyShotUsed =
            RouletteController.Instance != null &&
            RouletteController.Instance.SpinInProgress &&
            RouletteController.Instance.CurrentSpinMethod ==
                RouletteController.SpinMethod.LuckyShot;


        bool wasAtMaximumUses =
            owner.HasLimitedUses &&
            maxUses > 0 &&
            owner.RemainingUses ==
                maxUses;


        bool bonusRequested =
            false;


        if (luckyShotUsed &&
            wasAtMaximumUses &&
            bonusStickerPrefab != null)
        {
            if (RewardManager.Instance != null)
            {
                bonusRequested =
                    RewardManager.Instance
                        .RequestFreeStickerReward(
                            bonusStickerPrefab,
                            stickerName
                        );
            }
            else
            {
                Debug.LogWarning(
                    "[MATRYOSHKA] RewardManager missing. " +
                    "Bonus Matryoshka could not be queued."
                );
            }
        }


        string activationDescription =
            bonusRequested
                ? "Lucky Shot at maximum uses. Bonus Matryoshka earned"
                : null;


        RegisterActivation(
            owner,
            StickerSpinLocation.WinningSegment,
            activationDescription,
            Mathf.Max(
                0,
                dollarReward
            ),
            null
        );


        if (CurrencyManager.Instance != null &&
            dollarReward > 0)
        {
            CurrencyManager.Instance
                .AddDollar(
                    dollarReward
                );
        }


        Debug.Log(
            bonusRequested
                ? $"[MATRYOSHKA] Earned ${Mathf.Max(0, dollarReward)} and queued bonus '{GetBonusStickerName()}'."
                : $"[MATRYOSHKA] Earned ${Mathf.Max(0, dollarReward)}. No bonus Matryoshka."
        );
    }


    // =========================================================
    // TOOLTIP
    // =========================================================

    /*
     * Recommended Winning Segment Tooltip:
     *
     * Earn ${dollarReward}. On Lucky Shot, if at maximum uses, get a {bonusSticker}.
     *
     * For the smallest Matryoshka, where bonusStickerPrefab is empty, you can
     * simply author:
     *
     * Earn ${dollarReward}.
     */
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
                "{bonusSticker}",
                GetBonusStickerName()
            );
    }


    // =========================================================
    // HELPERS
    // =========================================================

    private string GetBonusStickerName()
    {
        if (bonusStickerPrefab == null)
            return "Matryoshka";


        BaseSticker bonusSticker =
            bonusStickerPrefab
                .GetComponentInChildren<BaseSticker>(
                    true
                );


        if (bonusSticker != null &&
            bonusSticker.effect != null &&
            !string.IsNullOrWhiteSpace(
                bonusSticker.effect.stickerName
            ))
        {
            return
                bonusSticker.effect.stickerName;
        }


        return
            bonusStickerPrefab.name;
    }
}
