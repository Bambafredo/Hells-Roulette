using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
        "Chance that a losing-segment activation doubles this physical " +
        "Bitcoin's current value."
    )]
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


        float doubleChance =
            GetEffectiveDoubleChance();


        float resetChance =
            GetEffectiveResetChance();


        float roll =
            Random.Range(
                0f,
                100f
            );


        bool doubled =
            roll <
            doubleChance;


        bool reset =
            !doubled &&
            roll <
                doubleChance +
                resetChance;


        int newValue =
            previousValue;


        string description;


        if (doubled)
        {
            /*
             * Use long for the multiplication so an absurdly successful
             * Bitcoin cannot overflow into a negative integer.
             */
            long doubledValue =
                (long)previousValue *
                2L;


            newValue =
                doubledValue >
                int.MaxValue
                    ? int.MaxValue
                    : (int)doubledValue;


            description =
                BuildDoubleDescription(
                    previousValue,
                    newValue
                );
        }
        else if (reset)
        {
            newValue =
                originalValue;


            description =
                BuildResetDescription(
                    previousValue,
                    newValue
                );
        }
        else
        {
            /*
             * Any probability left after Double + Reset is an explicit
             * "no movement" result.
             *
             * Example:
             * Double = 60
             * Reset  = 20
             * Stable = 20
             */
            description =
                BuildNoChangeDescription(
                    previousValue
                );
        }


        owner.SetRuntimeInt(
            CurrentValueKey,
            newValue
        );


        RegisterActivation(
            owner,
            StickerSpinLocation.NonWinningSegment,
            description,
            0,
            null
        );


        if (doubled)
        {
            Debug.Log(
                $"[BITCOIN] Value doubled: " +
                $"${previousValue} -> ${newValue}."
            );
        }
        else if (reset)
        {
            Debug.Log(
                $"[BITCOIN] Value reset: " +
                $"${previousValue} -> ${newValue}."
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

    private string BuildDoubleDescription(
        int previousValue,
        int newValue)
    {
        if (GameLogManager.Instance != null)
        {
            return
                "Value doubles: " +
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
            $"Value doubles: ${previousValue} → ${newValue}";
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
     * {resetChance}
     * {noChangeChance}
     *
     * Suggested authoring:
     *
     * Winning Segment Tooltip:
     * Earn ${currentValue}.
     *
     * Losing Segment Tooltip:
     * {doubleChance}% chance to double current value.
     * {resetChance}% chance to reset to ${originalValue}.
     * {noChangeChance}% chance to stay unchanged.
     * Current value: ${currentValue}.
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
