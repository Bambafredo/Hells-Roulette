using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StickerHealthInsurance",
    menuName = "Stickers/Sticker Health Insurance"
)]
public class StickerHealthInsurance : StickerEffect
{
    // =========================================================
    // CONFIG
    // =========================================================

    [Header("Health Insurance")]

    [Tooltip(
        "Blood this sticker attempts to restore whenever it activates."
    )]
    [Min(0)]
    public int bloodRestoreAmount = 3;

    [Tooltip(
        "Tariff paid the first time this physical sticker activates. " +
        "After every successful activation, the tariff doubles."
    )]
    [Min(0)]
    public int startingTariff = 3;


    // =========================================================
    // PER-INSTANCE STATE
    // =========================================================

    private const string CurrentTariffKey =
        "HealthInsurance.CurrentTariff";


    // =========================================================
    // WINNING EFFECT
    // =========================================================

    public override void ApplyEffect(
        BaseSticker owner)
    {
        if (RoundManager.Instance != null &&
            !RoundManager.Instance.WasLastSpinValid)
        {
            return;
        }


        if (owner == null)
        {
            Debug.LogWarning(
                "StickerHealthInsurance: BaseSticker owner was not provided."
            );

            return;
        }


        if (CurrencyManager.Instance == null)
        {
            Debug.LogWarning(
                "StickerHealthInsurance: CurrencyManager was not found."
            );

            return;
        }


        if (BloodManager.Instance == null)
        {
            Debug.LogWarning(
                "StickerHealthInsurance: BloodManager was not found."
            );

            return;
        }


        int currentTariff =
            GetCurrentTariff(
                owner
            );


        // -----------------------------------------------------
        // CANNOT AFFORD
        // -----------------------------------------------------

        if (!CurrencyManager.Instance
            .CanAfford(currentTariff))
        {
            LogCannotAfford(
                currentTariff
            );


            Debug.Log(
                $"[HEALTH INSURANCE] Cannot afford tariff: " +
                $"${currentTariff}."
            );

            /*
             * No heal, no tariff increase and no use consumption.
             * The effect did not successfully activate.
             */
            return;
        }


        int actualBloodRestored =
            Mathf.Clamp(
                bloodRestoreAmount,
                0,
                Mathf.Max(
                    0,
                    BloodManager.Instance.maxBlood -
                    BloodManager.Instance.currentBlood
                )
            );


        int nextTariff =
            CalculateNextTariff(
                currentTariff
            );


        // -----------------------------------------------------
        // LOG / USE
        // -----------------------------------------------------

        /*
         * Register the successful activation through the shared sticker
         * pipeline so optional uses behave exactly like every other
         * winning-segment sticker.
         */
        RegisterActivation(
            owner,
            StickerSpinLocation.WinningSegment,
            BuildSuccessLogDescription(
                actualBloodRestored,
                currentTariff,
                nextTariff
            ),
            0,
            null
        );


        // -----------------------------------------------------
        // PAY CURRENT TARIFF
        // -----------------------------------------------------

        bool paid =
            CurrencyManager.Instance
                .Spend(
                    currentTariff
                );


        /*
         * We already checked affordability immediately before this call,
         * so this should only fail if another system changes the player's
         * money synchronously during this activation.
         */
        if (!paid)
        {
            Debug.LogWarning(
                "[HEALTH INSURANCE] Tariff payment unexpectedly failed."
            );

            return;
        }


        // -----------------------------------------------------
        // RESTORE BLOOD
        // -----------------------------------------------------

        if (bloodRestoreAmount > 0)
        {
            BloodManager.Instance
                .HealBlood(
                    bloodRestoreAmount
                );
        }


        // -----------------------------------------------------
        // DOUBLE TARIFF
        // -----------------------------------------------------

        owner.SetRuntimeInt(
            CurrentTariffKey,
            nextTariff
        );


        Debug.Log(
            $"[HEALTH INSURANCE] Restored {actualBloodRestored} Blood, " +
            $"paid ${currentTariff}. " +
            $"Next tariff: ${nextTariff}."
        );
    }


    // =========================================================
    // TARIFF
    // =========================================================

    private int GetCurrentTariff(
        BaseSticker owner)
    {
        return
            Mathf.Max(
                0,
                owner.GetRuntimeInt(
                    CurrentTariffKey,
                    Mathf.Max(
                        0,
                        startingTariff
                    )
                )
            );
    }


    private int CalculateNextTariff(
        int currentTariff)
    {
        long doubled =
            (long)Mathf.Max(
                0,
                currentTariff
            ) *
            2L;


        return
            doubled >= int.MaxValue
                ? int.MaxValue
                : (int)doubled;
    }


    // =========================================================
    // GAME LOG
    // =========================================================

    private string BuildSuccessLogDescription(
        int actualBloodRestored,
        int currentTariff,
        int nextTariff)
    {
        if (GameLogManager.Instance != null)
        {
            string blood =
                GameLogManager.Instance
                    .BloodText(
                        $"+{actualBloodRestored} Blood"
                    );


            string paid =
                GameLogManager.Instance
                    .MoneyText(
                        $"-${currentTariff}"
                    );


            string next =
                GameLogManager.Instance
                    .MoneyText(
                        $"${nextTariff}"
                    );


            return
                $"Restore {blood} and pay {paid} " +
                $"(next tariff: {next})";
        }


        return
            $"Restore +{actualBloodRestored} Blood and pay " +
            $"-${currentTariff} (next tariff: ${nextTariff})";
    }


    private void LogCannotAfford(
        int currentTariff)
    {
        if (GameLogManager.Instance == null)
            return;


        string tariff =
            GameLogManager.Instance
                .MoneyText(
                    $"${currentTariff}"
                );


        GameLogManager.Instance
            .LogStickerActivation(
                stickerName,
                $"Cannot afford {tariff} tariff",
                0
            );
    }


    // =========================================================
    // TOOLTIP TOKENS
    // =========================================================

    /*
     * Available custom tokens:
     *
     * {bloodRestore}
     * {currentTariff}
     * {nextTariff}
     *
     * Recommended Winning Segment Tooltip:
     *
     * Restore {bloodRestore} Blood and pay ${currentTariff}, then double
     * the tariff. (next tariff: ${nextTariff})
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


        int currentTariff =
            owner != null
                ? GetCurrentTariff(owner)
                : Mathf.Max(
                    0,
                    startingTariff
                );


        int nextTariff =
            CalculateNextTariff(
                currentTariff
            );


        return
            resolved
                .Replace(
                    "{bloodRestore}",
                    Mathf.Max(
                        0,
                        bloodRestoreAmount
                    ).ToString()
                )
                .Replace(
                    "{currentTariff}",
                    currentTariff.ToString()
                )
                .Replace(
                    "{nextTariff}",
                    nextTariff.ToString()
                );
    }
}
