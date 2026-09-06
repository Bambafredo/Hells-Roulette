using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StickerAmbulance",
    menuName = "Stickers/Sticker Ambulance"
)]
public class StickerAmbulance : StickerEffect
{
    // =========================================================
    // CONFIGURATION
    // =========================================================

    [Header("Ambulance")]

    [Tooltip(
        "Percentage of the player's CURRENT missing Blood restored when " +
        "Ambulance activates on the winning segment. Example: at 40 / 100 Blood, " +
        "50% restores 30 Blood. Fractional results are rounded up so a player " +
        "missing 1 Blood can still recover 1."
    )]
    [Range(0f, 100f)]
    public float missingBloodRecoveryPercent =
        50f;


    [Tooltip(
        "Dollars Ambulance charges after every valid spin while this sticker " +
        "is in the Album. If the player has less than this amount, Ambulance " +
        "takes the remaining dollars without going below zero."
    )]
    [Min(0)]
    public int albumCost =
        5;


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
        {
            Debug.LogWarning(
                "StickerAmbulance: BaseSticker owner was not provided."
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

            case StickerSpinLocation.Album:
                ResolveAlbum(
                    owner
                );
                break;
        }
    }


    // =========================================================
    // WINNING SEGMENT - RECOVER MISSING BLOOD
    // =========================================================

    private void ResolveWinningSegment(
        BaseSticker owner)
    {
        if (BloodManager.Instance == null)
        {
            Debug.LogWarning(
                "StickerAmbulance: BloodManager.Instance is missing."
            );

            return;
        }


        int missingBlood =
            Mathf.Max(
                0,
                BloodManager.Instance.maxBlood -
                BloodManager.Instance.currentBlood
            );


        int bloodToRestore =
            CalculateRecoveryAmount(
                missingBlood
            );


        /*
         * Register before applying the heal so the gameplay log keeps the same
         * causal order used by the rest of the sticker system.
         *
         * Winning-segment use consumption remains controlled by the normal
         * StickerEffect Inspector toggle.
         */
        RegisterActivation(
            owner,
            StickerSpinLocation.WinningSegment,
            BuildWinningLogDescription(
                bloodToRestore,
                missingBlood
            ),
            0,
            null
        );


        if (bloodToRestore > 0)
        {
            BloodManager.Instance
                .HealBlood(
                    bloodToRestore
                );
        }


        Debug.Log(
            $"[AMBULANCE] Missing Blood = {missingBlood}. " +
            $"Recovery = {missingBloodRecoveryPercent:0.##}% -> " +
            $"+{bloodToRestore} Blood."
        );
    }


    private int CalculateRecoveryAmount(
        int missingBlood)
    {
        int safeMissing =
            Mathf.Max(
                0,
                missingBlood
            );


        if (safeMissing <= 0)
            return 0;


        float safePercent =
            Mathf.Clamp(
                missingBloodRecoveryPercent,
                0f,
                100f
            );


        /*
         * Blood is integer-based. Round UP fractional healing so 50% of a
         * 1-Blood deficit still restores 1 instead of silently becoming zero.
         */
        int calculated =
            Mathf.CeilToInt(
                safeMissing *
                safePercent /
                100f
            );


        return
            Mathf.Clamp(
                calculated,
                0,
                safeMissing
            );
    }


    // =========================================================
    // ALBUM - PAY
    // =========================================================

    private void ResolveAlbum(
        BaseSticker owner)
    {
        if (CurrencyManager.Instance == null)
        {
            Debug.LogWarning(
                "StickerAmbulance: CurrencyManager.Instance is missing."
            );

            return;
        }


        int requestedCost =
            Mathf.Max(
                0,
                albumCost
            );


        /*
         * CurrencyManager currently exposes Spend(), not a separate gameplay-loss
         * API. Keep the sticker self-contained and use the existing economy route.
         *
         * Ambulance takes up to X: calculate the payable amount first, then call
         * Spend() with an amount we already know the player can afford. This avoids
         * mutating CurrencyManager.dollars directly and requires no core change.
         */
        int actualPaid =
            Mathf.Min(
                requestedCost,
                Mathf.Max(
                    0,
                    CurrencyManager.Instance.dollars
                )
            );


        bool paid =
            CurrencyManager.Instance
                .Spend(
                    actualPaid
                );


        if (!paid)
        {
            Debug.LogWarning(
                "[AMBULANCE] Album payment unexpectedly failed after " +
                "the payable amount had already been clamped."
            );

            return;
        }


        RegisterActivation(
            owner,
            StickerSpinLocation.Album,
            BuildAlbumLogDescription(
                requestedCost,
                actualPaid
            ),
            0,
            null
        );


        Debug.Log(
            $"[AMBULANCE] Album bill requested ${requestedCost}; " +
            $"actually paid ${actualPaid}."
        );
    }


    // =========================================================
    // GAME LOG
    // =========================================================

    private string BuildWinningLogDescription(
        int bloodRestored,
        int missingBlood)
    {
        string bloodText =
            $"+{Mathf.Max(0, bloodRestored)} Blood";


        if (GameLogManager.Instance != null)
        {
            bloodText =
                GameLogManager.Instance
                    .BloodText(
                        bloodText
                    );
        }


        return
            $"Recover {bloodText} " +
            $"({missingBloodRecoveryPercent:0.##}% of " +
            $"{Mathf.Max(0, missingBlood)} missing Blood)";
    }


    private string BuildAlbumLogDescription(
        int requestedCost,
        int actualPaid)
    {
        string paidText =
            $"-${Mathf.Max(0, actualPaid)}";


        if (GameLogManager.Instance != null)
        {
            paidText =
                GameLogManager.Instance
                    .MoneyText(
                        paidText
                    );
        }


        if (actualPaid >= requestedCost)
        {
            return
                "Pay " +
                paidText;
        }


        return
            "Pay " +
            paidText +
            $" (${Mathf.Max(0, requestedCost)} due)";
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


    /*
     * Available custom tokens:
     *
     * {missingBloodPercent} = configured recovery percentage
     * {currentRecovery}     = Blood this sticker would restore right now
     * {albumCost}           = configured Album payment
     *
     * Suggested tooltips:
     *
     * Winning Segment:
     * Recover {missingBloodPercent}% of your missing Blood.
     *
     * Album:
     * Pay ${albumCost} after every valid spin.
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


        int currentRecovery =
            0;


        if (BloodManager.Instance != null)
        {
            int missingBlood =
                Mathf.Max(
                    0,
                    BloodManager.Instance.maxBlood -
                    BloodManager.Instance.currentBlood
                );

            currentRecovery =
                CalculateRecoveryAmount(
                    missingBlood
                );
        }


        return
            resolved
                .Replace(
                    "{missingBloodPercent}",
                    Mathf.Clamp(
                        missingBloodRecoveryPercent,
                        0f,
                        100f
                    )
                    .ToString("0.##")
                )
                .Replace(
                    "{currentRecovery}",
                    currentRecovery.ToString()
                )
                .Replace(
                    "{albumCost}",
                    Mathf.Max(
                        0,
                        albumCost
                    )
                    .ToString()
                );
    }
}
