using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StickerLuckyCharm",
    menuName = "Stickers/Sticker Lucky Charm"
)]
public class StickerLuckyCharm : StickerEffect
{
    // =========================================================
    // CONFIGURATION
    // =========================================================

    [Header("Lucky Charm")]

    [Tooltip(
        "Money earned when Lucky Charm activates after a spin that was not " +
        "started with Lucky Shot."
    )]
    [Min(0)]
    public int normalReward =
        3;


    [Tooltip(
        "Money earned when Lucky Charm activates after a Lucky Shot."
    )]
    [Min(0)]
    public int luckyShotReward =
        8;


    // =========================================================
    // EFFECT
    // =========================================================

    public override void ApplyEffect(
        BaseSticker owner)
    {
        if (RoundManager.Instance != null &&
            !RoundManager.Instance.WasLastSpinValid)
        {
            Debug.Log(
                "StickerLuckyCharm did not activate because the spin was invalid."
            );

            return;
        }


        bool luckyShotUsed =
            RouletteController.Instance != null &&
            RouletteController.Instance.CurrentSpinMethod ==
                RouletteController.SpinMethod.LuckyShot;


        int payout =
            luckyShotUsed
                ? Mathf.Max(
                    0,
                    luckyShotReward
                )
                : Mathf.Max(
                    0,
                    normalReward
                );


        /*
         * Lucky Charm is a normal WinningSegment sticker.
         *
         * RegisterActivation keeps the normal StickerEffect use-consumption
         * rules and logs the dynamic payout actually earned this activation.
         */
        RegisterActivation(
            owner,
            StickerSpinLocation.WinningSegment,
            null,
            payout,
            null
        );


        if (CurrencyManager.Instance != null &&
            payout > 0)
        {
            CurrencyManager.Instance
                .AddDollar(
                    payout
                );
        }


        Debug.Log(
            luckyShotUsed
                ? $"[LUCKY CHARM] Lucky Shot used. Earned ${payout}."
                : $"[LUCKY CHARM] No Lucky Shot. Earned ${payout}."
        );
    }


    // =========================================================
    // TOOLTIP
    // =========================================================

    /*
     * Custom tokens:
     *
     * {normalReward}
     * {luckyShotReward}
     *
     * Recommended Winning Segment Tooltip:
     *
     * Earn ${normalReward}. If Lucky Shot was used, earn
     * ${luckyShotReward} instead.
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
            resolved
                .Replace(
                    "{normalReward}",
                    Mathf.Max(
                        0,
                        normalReward
                    )
                    .ToString()
                )
                .Replace(
                    "{luckyShotReward}",
                    Mathf.Max(
                        0,
                        luckyShotReward
                    )
                    .ToString()
                );
    }
}
