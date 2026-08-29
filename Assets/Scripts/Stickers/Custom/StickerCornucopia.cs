using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StickerCornucopia",
    menuName = "Stickers/Sticker Cornucopia"
)]
public class StickerCornucopia : StickerEffect
{
    // =========================================================
    // CONFIG
    // =========================================================

    [Header("Cornucopia")]

    [Tooltip(
        "Money earned for every sticker currently placed anywhere on the wheel, " +
        "including this Cornucopia itself."
    )]
    [Min(0)]
    public int moneyPerSticker =
        1;


    // =========================================================
    // LOCATION-AWARE EFFECT
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


        if (owner == null)
            return;


        if (location !=
            StickerSpinLocation.WinningSegment)
        {
            return;
        }


        ResolveWinningSegment(
            owner
        );
    }


    // =========================================================
    // WINNING SEGMENT
    // =========================================================

    private void ResolveWinningSegment(
        BaseSticker owner)
    {
        int stickerCount =
            CountStickersInWheel();


        int payout =
            Mathf.Max(
                0,
                moneyPerSticker
            ) *
            stickerCount;


        string description =
            BuildLogDescription(
                stickerCount,
                payout
            );


        RegisterActivation(
            owner,
            StickerSpinLocation.WinningSegment,
            description,
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
            $"[CORNUCOPIA] {stickerCount} sticker(s) in wheel. " +
            $"Earned ${payout}."
        );
    }


    // =========================================================
    // COUNTING
    // =========================================================

    private int CountStickersInWheel()
    {
        int count =
            0;


        BaseSticker[] allStickers =
            Object.FindObjectsOfType<BaseSticker>(
                true
            );


        foreach (BaseSticker sticker in
                 allStickers)
        {
            if (sticker == null)
                continue;


            /*
             * "In the wheel" means physically placed on a roulette segment.
             *
             * Album, reward offers and unplaced stickers do not count.
             *
             * Cornucopia itself DOES count.
             */
            if (!sticker.isPlaced ||
                sticker.currentSegment == null)
            {
                continue;
            }


            count++;
        }


        return count;
    }


    // =========================================================
    // GAME LOG
    // =========================================================

    private string BuildLogDescription(
        int stickerCount,
        int payout)
    {
        string countText =
            stickerCount == 1
                ? "1 sticker"
                : $"{stickerCount} stickers";


        if (GameLogManager.Instance != null)
        {
            return
                "Earn " +
                GameLogManager.Instance
                    .MoneyText(
                        $"${payout}"
                    ) +
                $" from {countText} in the wheel";
        }


        return
            $"Earn ${payout} from {countText} in the wheel";
    }


    // =========================================================
    // TOOLTIP
    // =========================================================

    protected override bool SupportsTooltipLocation(
        StickerSpinLocation location)
    {
        return
            location ==
                StickerSpinLocation.WinningSegment;
    }


    /*
     * Available custom tokens:
     *
     * {moneyPerSticker}
     * {stickerCount}
     * {totalPayout}
     *
     * Suggested tooltip:
     *
     * Earn ${moneyPerSticker} for every sticker in the wheel
     *
     * Or, if you want the current calculated value:
     *
     * Earn ${totalPayout} ({stickerCount} stickers)
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


        int stickerCount =
            CountStickersInWheel();


        int payout =
            Mathf.Max(
                0,
                moneyPerSticker
            ) *
            stickerCount;


        return
            resolved
                .Replace(
                    "{moneyPerSticker}",
                    Mathf.Max(
                        0,
                        moneyPerSticker
                    ).ToString()
                )
                .Replace(
                    "{stickerCount}",
                    stickerCount.ToString()
                )
                .Replace(
                    "{totalPayout}",
                    payout.ToString()
                );
    }


    // =========================================================
    // EDITOR SAFETY
    // =========================================================

    private void OnValidate()
    {
        moneyPerSticker =
            Mathf.Max(
                0,
                moneyPerSticker
            );
    }
}
