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
        "Chance that a losing-segment activation advances this physical " +
        "Bitcoin to the next configured progression value. " +
        "The remaining chance resets it to its original Dollar Reward value."
    )]
    [Range(0f, 100f)]
    public float growthChancePercent =
        70f;


    [Tooltip(
        "Absolute dollar values used as Bitcoin's progression, in order. " +
        "Example with Dollar Reward = 1 and entries 2, 3, 5, 8: " +
        "$1 -> $2 -> $3 -> $5 -> $8. " +
        "Once the final entry is reached, further successful growth keeps " +
        "Bitcoin at that final value."
    )]
    public int[] progressionValues =
        new int[0];


    /*
     * dollarReward inherited from StickerEffect is Bitcoin's ORIGINAL value.
     *
     * Current value and progression index live on BaseSticker so every
     * physical Bitcoin can evolve independently.
     */
    private const string CurrentValueKey =
        "Bitcoin.CurrentValue";


    /*
     * -1 = original Dollar Reward value.
     *  0 = progressionValues[0].
     *  1 = progressionValues[1].
     * etc.
     */
    private const string ProgressionIndexKey =
        "Bitcoin.ProgressionIndex";


    // =========================================================
    // UNITY VALIDATION
    // =========================================================

    private void OnValidate()
    {
        growthChancePercent =
            Mathf.Clamp(
                growthChancePercent,
                0f,
                100f
            );


        if (progressionValues == null)
            return;


        /*
         * Do not reorder or auto-correct the progression.
         * Inspector order is deliberately the source of truth.
         */
        for (int i = 0;
             i < progressionValues.Length;
             i++)
        {
            progressionValues[i] =
                Mathf.Max(
                    0,
                    progressionValues[i]
                );
        }
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
         * Winning pays the CURRENT value but does not alter progression.
         *
         * Use consumption is handled by the normal StickerEffect
         * "Consume Use On Winning" toggle.
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
        int previousValue =
            GetCurrentValue(
                owner
            );


        float growthChance =
            GetGrowthChance();


        bool grew =
            Random.Range(
                0f,
                100f
            ) <
            growthChance;


        int newValue;
        string description;


        if (grew)
        {
            newValue =
                AdvanceProgression(
                    owner
                );


            description =
                BuildGrowthDescription(
                    previousValue,
                    newValue
                );
        }
        else
        {
            newValue =
                GetOriginalValue();


            owner.SetRuntimeInt(
                ProgressionIndexKey,
                -1
            );


            description =
                BuildResetDescription(
                    previousValue,
                    newValue
                );
        }


        owner.SetRuntimeInt(
            CurrentValueKey,
            newValue
        );


        /*
         * Losing NEVER consumes a use.
         *
         * Bitcoin's limited-use behavior is therefore exclusively controlled
         * by "Consume Use On Winning" in StickerEffect.
         */
        RegisterActivation(
            owner,
            StickerSpinLocation.NonWinningSegment,
            description,
            0,
            false
        );


        Debug.Log(
            grew
                ? $"[BITCOIN] Progression: " +
                  $"${previousValue} -> ${newValue}."
                : $"[BITCOIN] Reset: " +
                  $"${previousValue} -> ${newValue}."
        );
    }


    // =========================================================
    // CUSTOM PROGRESSION
    // =========================================================

    private int AdvanceProgression(
        BaseSticker owner)
    {
        if (progressionValues == null ||
            progressionValues.Length <= 0)
        {
            /*
             * No authored progression = successful growth has nowhere to go.
             * Keep current value rather than inventing a fallback formula.
             */
            return
                GetCurrentValue(
                    owner
                );
        }


        int currentIndex =
            owner.GetRuntimeInt(
                ProgressionIndexKey,
                -1
            );


        int nextIndex =
            Mathf.Clamp(
                currentIndex + 1,
                0,
                progressionValues.Length - 1
            );


        owner.SetRuntimeInt(
            ProgressionIndexKey,
            nextIndex
        );


        return
            Mathf.Max(
                0,
                progressionValues[
                    nextIndex
                ]
            );
    }


    private int GetNextProgressionValue(
        BaseSticker owner)
    {
        if (progressionValues == null ||
            progressionValues.Length <= 0)
        {
            return
                GetCurrentValue(
                    owner
                );
        }


        int currentIndex =
            owner != null
                ? owner.GetRuntimeInt(
                    ProgressionIndexKey,
                    -1
                )
                : -1;


        int nextIndex =
            Mathf.Clamp(
                currentIndex + 1,
                0,
                progressionValues.Length - 1
            );


        return
            Mathf.Max(
                0,
                progressionValues[
                    nextIndex
                ]
            );
    }


    private bool IsAtFinalProgressionValue(
        BaseSticker owner)
    {
        if (progressionValues == null ||
            progressionValues.Length <= 0 ||
            owner == null)
        {
            return true;
        }


        int currentIndex =
            owner.GetRuntimeInt(
                ProgressionIndexKey,
                -1
            );


        return
            currentIndex >=
            progressionValues.Length - 1;
    }


    // =========================================================
    // PROBABILITIES
    // =========================================================

    private float GetGrowthChance()
    {
        return
            Mathf.Clamp(
                growthChancePercent,
                0f,
                100f
            );
    }


    private float GetResetChance()
    {
        return
            100f -
            GetGrowthChance();
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
        if (GameLogManager.Instance != null)
        {
            return
                "Value increases: " +
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
            $"Value increases: ${previousValue} → ${newValue}";
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
     * Custom tokens:
     *
     * {currentValue}
     * {originalValue}
     * {growthChance}
     * {resetChance}
     * {nextValue}
     * {growthAction}
     *
     * Recommended:
     *
     * Winning:
     * Earn ${currentValue}.
     *
     * Losing:
     * {growthChance}% chance to {growthAction}.
     * {resetChance}% chance to reset to ${originalValue}.
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
                    "{growthChance}",
                    FormatPercent(
                        GetGrowthChance()
                    )
                )
                .Replace(
                    "{resetChance}",
                    FormatPercent(
                        GetResetChance()
                    )
                )
                .Replace(
                    "{nextValue}",
                    GetNextProgressionValue(
                        owner
                    )
                    .ToString()
                )
                .Replace(
                    "{growthAction}",
                    GetGrowthActionText(
                        owner
                    )
                );
    }


    private string GetGrowthActionText(
        BaseSticker owner)
    {
        int nextValue =
            GetNextProgressionValue(
                owner
            );


        if (progressionValues == null ||
            progressionValues.Length <= 0)
        {
            return
                $"keep value at ${nextValue}";
        }


        if (IsAtFinalProgressionValue(
            owner))
        {
            return
                $"keep value at ${nextValue}";
        }


        return
            $"increase value to ${nextValue}";
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
