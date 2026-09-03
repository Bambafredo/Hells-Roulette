using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StickerLeech",
    menuName = "Stickers/Sticker Leech"
)]
public class StickerLeech : StickerEffect
{
    // =========================================================
    // CONFIGURATION
    // =========================================================

    [Header("Leech")]

    [Tooltip(
        "Blood lost whenever Leech activates from the Album."
    )]
    [Min(0)]
    public int bloodLoss =
        2;


    // =========================================================
    // EFFECT
    // =========================================================

    public override void ResolveSpinLocation(
        BaseSticker owner,
        StickerSpinLocation location)
    {
        if (location !=
            StickerSpinLocation.Album)
        {
            return;
        }


        if (RoundManager.Instance != null &&
            !RoundManager.Instance.WasLastSpinValid)
        {
            return;
        }


        if (BloodManager.Instance == null)
        {
            Debug.LogWarning(
                "StickerLeech: BloodManager.Instance is missing."
            );

            return;
        }


        int configuredLoss =
            Mathf.Max(
                0,
                bloodLoss
            );


        if (configuredLoss <= 0 ||
            BloodManager.Instance.currentBlood <= 0)
        {
            return;
        }


        /*
         * Leech is BLOOD LOSS, not damage.
         *
         * Therefore it intentionally uses ConsumeBlood() rather than
         * TakeDamage():
         *
         * - Shield cannot prevent it.
         * - It is still counted by "Total blood lost this spin".
         *
         * This keeps the semantic distinction already used by Power Spin,
         * braking and other Blood costs.
         */
        int actualLoss =
            Mathf.Min(
                configuredLoss,
                BloodManager.Instance.currentBlood
            );


        string description =
            BuildLogDescription(
                actualLoss
            );


        /*
         * Register first so the log reads:
         *
         * Leech activates: Lose 2 Blood
         *
         * Then apply the Blood loss immediately afterwards.
         *
         * Album use consumption remains fully configurable through the normal
         * StickerEffect "Consume Use On Album" checkbox.
         */
        RegisterActivation(
            owner,
            StickerSpinLocation.Album,
            description,
            0,
            null
        );


        BloodManager.Instance
            .ConsumeBlood(
                configuredLoss
            );
    }


    // =========================================================
    // LOG
    // =========================================================

    private string BuildLogDescription(
        int actualLoss)
    {
        string bloodText =
            $"{actualLoss} Blood";


        if (GameLogManager.Instance != null)
        {
            bloodText =
                GameLogManager.Instance
                    .BloodText(
                        bloodText
                    );
        }


        return
            "Lose " +
            bloodText;
    }


    // =========================================================
    // TOOLTIP
    // =========================================================

    protected override bool SupportsTooltipLocation(
        StickerSpinLocation location)
    {
        return
            location ==
                StickerSpinLocation.Album;
    }


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
                    "{bloodLoss}",
                    Mathf.Max(
                        0,
                        bloodLoss
                    )
                    .ToString()
                );
    }
}
