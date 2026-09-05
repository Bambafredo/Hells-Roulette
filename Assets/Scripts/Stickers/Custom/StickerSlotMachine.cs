using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StickerSlotMachine",
    menuName = "Stickers/Sticker Slot Machine"
)]
public class StickerSlotMachine : StickerEffect
{
    // =========================================================
    // CONFIGURATION
    // =========================================================

    [Header("Slot Machine")]

    [Tooltip(
        "Maximum dollars Slot Machine can earn on a winning-segment activation."
    )]
    [Min(0)]
    public int maxWinAmount =
        10;


    [Tooltip(
        "How much each Quarter currently in the wheel increases the minimum " +
        "possible payout."
    )]
    [Min(0)]
    public int minimumIncreasePerQuarter =
        2;


    [Header("Lucky Shot Interaction")]

    [Tooltip(
        "If enabled, activating Slot Machine during a Lucky Shot uses a " +
        "separate maximum payout. Quarter bonuses still increase the minimum."
    )]
    public bool useLuckyShotMaxOverride =
        false;


    [Tooltip(
        "Maximum payout used during a Lucky Shot when the override is enabled."
    )]
    [Min(0)]
    public int luckyShotMaxWinAmount =
        20;


    [Header("Quarter Identification")]

    [Tooltip(
        "Optional direct reference to the StickerEffect asset used by Quarter. " +
        "If assigned, this is the preferred way to identify Quarter stickers."
    )]
    public StickerEffect quarterStickerEffect;


    [Tooltip(
        "Fallback Sticker Name used if Quarter Sticker Effect is not assigned."
    )]
    public string quarterStickerName =
        "Quarter";


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
                "StickerSlotMachine did not activate because the spin was invalid."
            );

            return;
        }


        int quarterCount =
            CountQuartersInWheel();


        bool luckyShotUsed =
            IsCurrentSpinLuckyShot();


        int minimum =
            GetCurrentMinimum(
                quarterCount,
                luckyShotUsed
            );


        int maximum =
            GetCurrentMaximum(
                luckyShotUsed
            );


        int payout =
            RollInclusive(
                minimum,
                maximum
            );


        string description =
            $"Wins {MoneyText($"${payout}")} " +
            $"(range {MoneyText($"${minimum}")}–" +
            $"{MoneyText($"${maximum}")}, " +
            $"{quarterCount} Quarter" +
            $"{(quarterCount == 1 ? "" : "s")})";


        /*
         * Slot Machine is a normal WinningSegment sticker.
         *
         * RegisterActivation handles:
         * - normal use consumption
         * - Game Log activation line
         * - shared sticker resolution rules
         *
         * Passing payout as the dollar reward also keeps the normal gross
         * spin-money tracking / Lucky Shot accounting compatible.
         */
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
            $"[SLOT MACHINE] Rolled ${payout} from ${minimum}-${maximum}. " +
            $"Quarters = {quarterCount}. " +
            $"Lucky Shot = {luckyShotUsed}. " +
            $"Lucky max override enabled = {useLuckyShotMaxOverride}."
        );
    }


    // =========================================================
    // PAYOUT RANGE
    // =========================================================

    private int GetCurrentMinimum(
        int quarterCount,
        bool luckyShotUsed)
    {
        int maximum =
            GetCurrentMaximum(
                luckyShotUsed
            );


        long calculatedMinimum =
            (long)Mathf.Max(
                0,
                minimumIncreasePerQuarter
            ) *
            Mathf.Max(
                0,
                quarterCount
            );


        if (calculatedMinimum >=
            maximum)
        {
            return
                maximum;
        }


        return
            Mathf.Max(
                0,
                (int)calculatedMinimum
            );
    }


    private int GetCurrentMaximum(
        bool luckyShotUsed)
    {
        if (useLuckyShotMaxOverride &&
            luckyShotUsed)
        {
            return
                Mathf.Max(
                    0,
                    luckyShotMaxWinAmount
                );
        }


        return
            Mathf.Max(
                0,
                maxWinAmount
            );
    }


    private int RollInclusive(
        int minimum,
        int maximum)
    {
        minimum =
            Mathf.Max(
                0,
                minimum
            );


        maximum =
            Mathf.Max(
                minimum,
                maximum
            );


        if (minimum ==
            maximum)
        {
            return
                minimum;
        }


        /*
         * int Random.Range uses an exclusive upper bound.
         * Use a float roll so int.MaxValue can still be represented safely
         * without maximum + 1 overflow.
         */
        float t =
            Random.value;


        long rangeSize =
            (long)maximum -
            minimum +
            1L;


        long offset =
            (long)Mathf.Floor(
                t *
                rangeSize
            );


        if (offset >=
            rangeSize)
        {
            offset =
                rangeSize -
                1L;
        }


        return
            (int)(
                minimum +
                offset
            );
    }


    // =========================================================
    // QUARTERS
    // =========================================================

    private int CountQuartersInWheel()
    {
        BaseSticker[] allStickers =
            Object.FindObjectsOfType<BaseSticker>(
                true
            );


        int count =
            0;


        foreach (BaseSticker sticker in
                 allStickers)
        {
            if (sticker == null ||
                sticker.IsPendingGameplayDestruction ||
                !sticker.isPlaced ||
                sticker.currentSegment == null ||
                sticker.effect == null)
            {
                continue;
            }


            if (IsQuarter(
                sticker.effect))
            {
                count++;
            }
        }


        return
            count;
    }


    private bool IsQuarter(
        StickerEffect candidate)
    {
        if (candidate == null)
            return false;


        if (quarterStickerEffect != null)
        {
            return
                candidate ==
                quarterStickerEffect;
        }


        return
            !string.IsNullOrWhiteSpace(
                quarterStickerName
            ) &&
            string.Equals(
                candidate.stickerName,
                quarterStickerName,
                System.StringComparison.OrdinalIgnoreCase
            );
    }


    // =========================================================
    // LUCKY SHOT
    // =========================================================

    private bool IsCurrentSpinLuckyShot()
    {
        return
            RouletteController.Instance != null &&
            RouletteController.Instance.CurrentSpinMethod ==
                RouletteController.SpinMethod.LuckyShot;
    }


    // =========================================================
    // LOG / DISPLAY
    // =========================================================

    private string MoneyText(
        string text)
    {
        if (GameLogManager.Instance != null)
        {
            return
                GameLogManager.Instance
                    .MoneyText(
                        text
                    );
        }


        return
            text;
    }


    // =========================================================
    // TOOLTIP
    // =========================================================

    /*
     * Custom tokens:
     *
     * {maxWin}
     * {luckyShotMaxWin}
     * {minWin}
     * {minimumPerQuarter}
     * {quarterCount}
     * {luckyShotRule}
     *
     * Recommended Winning Segment Tooltip:
     *
     * Win between ${minWin} and ${maxWin}.
     * Each Quarter in the wheel increases the minimum by
     * ${minimumPerQuarter}. {luckyShotRule}
     *
     * {minWin} reflects the CURRENT Quarter count in the wheel.
     *
     * {luckyShotRule} becomes:
     * "Lucky Shot: maximum becomes ${luckyShotMaxWin}."
     * when the optional interaction is enabled, otherwise it becomes empty.
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


        int quarterCount =
            CountQuartersInWheel();


        /*
         * Tooltip minimum shows the normal Quarter-driven minimum.
         * The optional Lucky Shot maximum is communicated separately through
         * {luckyShotRule}.
         */
        int normalMinimum =
            GetCurrentMinimum(
                quarterCount,
                false
            );


        return
            resolved
                .Replace(
                    "{maxWin}",
                    Mathf.Max(
                        0,
                        maxWinAmount
                    )
                    .ToString()
                )
                .Replace(
                    "{luckyShotMaxWin}",
                    Mathf.Max(
                        0,
                        luckyShotMaxWinAmount
                    )
                    .ToString()
                )
                .Replace(
                    "{minWin}",
                    normalMinimum.ToString()
                )
                .Replace(
                    "{minimumPerQuarter}",
                    Mathf.Max(
                        0,
                        minimumIncreasePerQuarter
                    )
                    .ToString()
                )
                .Replace(
                    "{quarterCount}",
                    quarterCount.ToString()
                )
                .Replace(
                    "{luckyShotRule}",
                    useLuckyShotMaxOverride
                        ? $"Lucky Shot: maximum becomes ${Mathf.Max(0, luckyShotMaxWinAmount)}."
                        : ""
                );
    }
}
