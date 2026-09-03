using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BitcoinGrowthMode
{
    Double,
    Fibonacci
}


[CreateAssetMenu(
    fileName = "StickerBitcoin",
    menuName = "Stickers/Sticker Bitcoin"
)]
public class StickerBitcoin : StickerEffect
{
    // =========================================================
    // CONFIGURATION
    // =========================================================

    [Header("Bitcoin")]

    [Tooltip(
        "How a successful growth result changes this Bitcoin's value. " +
        "Double: current value x2. Fibonacci: original value multiplied by " +
        "1, 2, 3, 5, 8, 13, 21, 34..."
    )]
    public BitcoinGrowthMode growthMode =
        BitcoinGrowthMode.Double;


    [Tooltip(
        "Chance that a losing-segment activation applies the selected " +
        "Bitcoin growth mode."
    )]
    [InspectorName("Growth Chance Percent")]
    [Range(0f, 100f)]
    public float doubleChancePercent =
        80f;


    [Tooltip(
        "Chance that a losing-segment activation resets this physical " +
        "Bitcoin to its original value. Double + Reset cannot exceed 100%. " +
        "Any remaining percentage means the value does not change."
    )]
    [Range(0f, 100f)]
    public float resetChancePercent =
        20f;


    /*
     * dollarReward inherited from StickerEffect is Bitcoin's ORIGINAL value.
     *
     * The changing CURRENT value must live on BaseSticker because each
     * physical Bitcoin can have a different price history.
     */
    private const string CurrentValueKey =
        "Bitcoin.CurrentValue";


    /*
     * Fibonacci index is per physical Bitcoin.
     *
     * Index 0 -> x1
     * Index 1 -> x2
     * Index 2 -> x3
     * Index 3 -> x5
     * ...
     */
    private const string FibonacciIndexKey =
        "Bitcoin.FibonacciIndex";


    // =========================================================
    // UNITY VALIDATION
    // =========================================================

    private void OnValidate()
    {
        doubleChancePercent =
            Mathf.Clamp(
                doubleChancePercent,
                0f,
                100f
            );


        resetChancePercent =
            Mathf.Clamp(
                resetChancePercent,
                0f,
                100f -
                doubleChancePercent
            );
    }


    // =========================================================
    // RESOLUTION
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
        {
            Debug.LogWarning(
                "StickerBitcoin: BaseSticker owner was not provided."
            );

            return;
        }


        switch (location)
        {
            case StickerSpinLocation.WinningSegment:
                ResolveWinningSegment(
                    owner
                );
                break;


            case StickerSpinLocation.NonWinningSegment:
                ResolveNonWinningSegment(
                    owner
                );
                break;


            case StickerSpinLocation.Album:
                // Bitcoin has no Album effect.
                break;
        }
    }


    // =========================================================
    // WINNING SEGMENT
    // =========================================================

    private void ResolveWinningSegment(
        BaseSticker owner)
    {
        int payout =
            GetCurrentValue(
                owner
            );


        /*
         * Winning pays the CURRENT value but does NOT reset it.
         * The value only changes through losing-segment volatility.
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
            $"[BITCOIN] Won ${payout}. " +
            $"Current value remains ${payout}."
        );
    }


    // =========================================================
    // LOSING SEGMENT
    // =========================================================

    private void ResolveNonWinningSegment(
        BaseSticker owner)
    {
        int originalValue =
            GetOriginalValue();


        int previousValue =
            GetCurrentValue(
                owner
            );


        float growthChance =
            GetEffectiveDoubleChance();


        float resetChance =
            GetEffectiveResetChance();


        float roll =
            Random.Range(
                0f,
                100f
            );


        bool grew =
            roll <
            growthChance;


        bool reset =
            !grew &&
            roll <
                growthChance +
                resetChance;


        int newValue =
            previousValue;


        string description;


        if (grew)
        {
            newValue =
                GetNextGrowthValue(
                    owner,
                    previousValue
                );


            description =
                BuildGrowthDescription(
                    previousValue,
                    newValue
                );
        }
        else if (reset)
        {
            newValue =
                originalValue;


            /*
             * Resetting value also resets Fibonacci progression back to x1.
             * In Double mode this state is harmless but keeping it in sync
             * makes mode changes during development deterministic.
             */
            owner.SetRuntimeInt(
                FibonacciIndexKey,
                0
            );


            description =
                BuildResetDescription(
                    previousValue,
                    newValue
                );
        }
        else
        {
            description =
                BuildNoChangeDescription(
                    previousValue
                );
        }


        owner.SetRuntimeInt(
            CurrentValueKey,
            newValue
        );


        /*
         * Normal StickerEffect consumption rules still apply.
         *
         * - Growth / No Change:
         *   use the normal "Consume Use On Non Winning" setting.
         *
         * - Reset:
         *   force one use to be consumed even if that normal toggle is OFF.
         *
         * Because RegisterActivation receives one final decision, a reset
         * consumes ONE use total, never two.
         */
        RegisterActivation(
            owner,
            StickerSpinLocation.NonWinningSegment,
            description,
            0,
            reset
                ? true
                : (bool?)null
        );


        if (grew)
        {
            Debug.Log(
                growthMode ==
                    BitcoinGrowthMode.Fibonacci
                    ? $"[BITCOIN] Fibonacci growth: " +
                      $"${previousValue} -> ${newValue}."
                    : $"[BITCOIN] Value doubled: " +
                      $"${previousValue} -> ${newValue}."
            );
        }
        else if (reset)
        {
            Debug.Log(
                $"[BITCOIN] Value reset: " +
                $"${previousValue} -> ${newValue}. " +
                "Consumed 1 use."
            );
        }
        else
        {
            Debug.Log(
                $"[BITCOIN] Value unchanged at ${newValue}."
            );
        }
    }


    // =========================================================
    // GROWTH
    // =========================================================

    private int GetNextGrowthValue(
        BaseSticker owner,
        int currentValue)
    {
        switch (growthMode)
        {
            case BitcoinGrowthMode.Fibonacci:
                return
                    GetNextFibonacciValue(
                        owner
                    );


            case BitcoinGrowthMode.Double:
            default:
                return
                    SafeDouble(
                        currentValue
                    );
        }
    }


    private int SafeDouble(
        int value)
    {
        long doubled =
            (long)Mathf.Max(
                0,
                value
            ) *
            2L;


        return
            doubled >
            int.MaxValue
                ? int.MaxValue
                : (int)doubled;
    }


    private int GetNextFibonacciValue(
        BaseSticker owner)
    {
        int currentIndex =
            Mathf.Max(
                0,
                owner.GetRuntimeInt(
                    FibonacciIndexKey,
                    0
                )
            );


        int nextIndex =
            currentIndex ==
            int.MaxValue
                ? int.MaxValue
                : currentIndex + 1;


        owner.SetRuntimeInt(
            FibonacciIndexKey,
            nextIndex
        );


        return
            GetFibonacciValueForIndex(
                nextIndex
            );
    }


    private int GetFibonacciValueForIndex(
        int index)
    {
        int originalValue =
            GetOriginalValue();


        if (originalValue <= 0)
            return 0;


        long multiplier =
            GetFibonacciMultiplier(
                index
            );


        long value =
            multiplier *
            (long)originalValue;


        return
            value >
            int.MaxValue
                ? int.MaxValue
                : (int)value;
    }


    /*
     * User-facing Bitcoin sequence:
     *
     * index 0 = 1
     * index 1 = 2
     * index 2 = 3
     * index 3 = 5
     * index 4 = 8
     * ...
     */
    private long GetFibonacciMultiplier(
        int index)
    {
        if (index <= 0)
            return 1L;


        if (index == 1)
            return 2L;


        long previous =
            1L;

        long current =
            2L;


        for (int i = 2;
             i <= index;
             i++)
        {
            /*
             * Saturate before long overflow. Bitcoin value itself later
             * saturates to int.MaxValue.
             */
            if (long.MaxValue -
                current <
                previous)
            {
                return
                    long.MaxValue;
            }


            long next =
                previous +
                current;


            previous =
                current;

            current =
                next;
        }


        return
            current;
    }


    private int GetNextPreviewValue(
        BaseSticker owner)
    {
        int currentValue =
            GetCurrentValue(
                owner
            );


        if (growthMode ==
            BitcoinGrowthMode.Double)
        {
            return
                SafeDouble(
                    currentValue
                );
        }


        int currentIndex =
            owner != null
                ? Mathf.Max(
                    0,
                    owner.GetRuntimeInt(
                        FibonacciIndexKey,
                        0
                    )
                )
                : 0;


        int nextIndex =
            currentIndex ==
            int.MaxValue
                ? int.MaxValue
                : currentIndex + 1;


        return
            GetFibonacciValueForIndex(
                nextIndex
            );
    }


    // =========================================================
    // PROBABILITIES
    // =========================================================

    private float GetEffectiveDoubleChance()
    {
        return
            Mathf.Clamp(
                doubleChancePercent,
                0f,
                100f
            );
    }


    private float GetEffectiveResetChance()
    {
        return
            Mathf.Clamp(
                resetChancePercent,
                0f,
                100f -
                GetEffectiveDoubleChance()
            );
    }


    private float GetNoChangeChance()
    {
        return
            Mathf.Max(
                0f,
                100f -
                GetEffectiveDoubleChance() -
                GetEffectiveResetChance()
            );
    }


    // =========================================================
    // VALUE
    // =========================================================

    private int GetOriginalValue()
    {
        return
            Mathf.Max(
                0,
                dollarReward
            );
    }


    private int GetCurrentValue(
        BaseSticker owner)
    {
        int originalValue =
            GetOriginalValue();


        if (owner == null)
        {
            return
                originalValue;
        }


        /*
         * A fresh physical Bitcoin starts at its authored dollarReward.
         * We do not need a separate initialization pass because GetRuntimeInt
         * can supply that original value as the default.
         */
        return
            Mathf.Max(
                0,
                owner.GetRuntimeInt(
                    CurrentValueKey,
                    originalValue
                )
            );
    }


    // =========================================================
    // GAME LOG
    // =========================================================

    private string BuildGrowthDescription(
        int previousValue,
        int newValue)
    {
        string verb =
            growthMode ==
                BitcoinGrowthMode.Fibonacci
                ? "Fibonacci growth"
                : "Value doubles";


        if (GameLogManager.Instance != null)
        {
            return
                verb +
                ": " +
                GameLogManager.Instance
                    .MoneyText(
                        $"${previousValue}"
                    ) +
                " → " +
                GameLogManager.Instance
                    .MoneyText(
                        $"${newValue}"
                    );
        }


        return
            $"{verb}: ${previousValue} → ${newValue}";
    }


    private string BuildResetDescription(
        int previousValue,
        int newValue)
    {
        if (GameLogManager.Instance != null)
        {
            return
                "Value resets: " +
                GameLogManager.Instance
                    .MoneyText(
                        $"${previousValue}"
                    ) +
                " → " +
                GameLogManager.Instance
                    .MoneyText(
                        $"${newValue}"
                    );
        }


        return
            $"Value resets: ${previousValue} → ${newValue}";
    }


    private string BuildNoChangeDescription(
        int currentValue)
    {
        if (GameLogManager.Instance != null)
        {
            return
                "Value remains " +
                GameLogManager.Instance
                    .MoneyText(
                        $"${currentValue}"
                    );
        }


        return
            $"Value remains ${currentValue}";
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


    /*
     * Available custom tooltip tokens:
     *
     * {currentValue}
     * {originalValue}
     * {doubleChance}
     * {growthChance}
     * {resetChance}
     * {noChangeChance}
     * {growthAction}
     * {resetAction}
     * {nextValue}
     *
     * Suggested authoring:
     *
     * Winning Segment Tooltip:
     * Earn ${currentValue}.
     *
     * Losing Segment Tooltip:
     * {growthChance}% chance to {growthAction}.
     * {resetChance}% chance to {resetAction}.
     * {noChangeChance}% chance to stay unchanged.
     * Current value: ${currentValue}. Next growth value: ${nextValue}.
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


        float doubleChance =
            GetEffectiveDoubleChance();


        float resetChance =
            GetEffectiveResetChance();


        float noChangeChance =
            GetNoChangeChance();


        return
            resolved
                .Replace(
                    "{currentValue}",
                    GetCurrentValue(
                        owner
                    )
                    .ToString()
                )
                .Replace(
                    "{originalValue}",
                    GetOriginalValue()
                        .ToString()
                )
                .Replace(
                    "{doubleChance}",
                    FormatPercent(
                        doubleChance
                    )
                )
                .Replace(
                    "{growthChance}",
                    FormatPercent(
                        doubleChance
                    )
                )
                .Replace(
                    "{growthAction}",
                    GetGrowthActionText(
                        owner
                    )
                )
                .Replace(
                    "{resetAction}",
                    $"reset to ${GetOriginalValue()} and lose 1 use"
                )
                .Replace(
                    "{nextValue}",
                    GetNextPreviewValue(
                        owner
                    )
                    .ToString()
                )
                .Replace(
                    "{resetChance}",
                    FormatPercent(
                        resetChance
                    )
                )
                .Replace(
                    "{noChangeChance}",
                    FormatPercent(
                        noChangeChance
                    )
                );
    }


    private string GetGrowthActionText(
        BaseSticker owner)
    {
        int nextValue =
            GetNextPreviewValue(
                owner
            );


        return
            growthMode ==
                BitcoinGrowthMode.Fibonacci
                ? $"increase value to ${nextValue}"
                : $"double current value to ${nextValue}";
    }


    private string FormatPercent(
        float value)
    {
        return
            Mathf.Approximately(
                value,
                Mathf.Round(value)
            )
                ? Mathf.RoundToInt(
                    value
                )
                .ToString()
                : value.ToString(
                    "0.##"
                );
    }
}
