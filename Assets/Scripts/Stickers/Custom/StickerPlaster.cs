using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StickerPlaster",
    menuName = "Stickers/Sticker Plaster"
)]
public class StickerPlaster : StickerEffect
{
    // =========================================================
    // CONFIGURATION
    // =========================================================

    [Header("Plaster")]

    [Tooltip(
        "Blood restored when Plaster activates on a non-winning segment."
    )]
    [Min(0)]
    public int healAmount = 2;


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
                "StickerPlaster: BaseSticker owner was not provided."
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
                ResolveAlbum(
                    owner
                );
                break;
        }
    }


    // =========================================================
    // WINNING SEGMENT - LOSE 1 USE
    // =========================================================

    private void ResolveWinningSegment(
        BaseSticker owner)
    {
        /*
         * Plaster's winning result is deliberately just wear:
         * it loses one use and provides no healing.
         *
         * Use consumption is forced here because it is the mechanic itself,
         * rather than an optional Inspector-authored activation cost.
         */
        RegisterActivation(
            owner,
            StickerSpinLocation.WinningSegment,
            "Lose 1 use",
            0,
            true
        );
    }


    // =========================================================
    // NON-WINNING SEGMENT - HEAL
    // =========================================================

    private void ResolveNonWinningSegment(
        BaseSticker owner)
    {
        if (BloodManager.Instance == null)
        {
            Debug.LogWarning(
                "StickerPlaster: BloodManager.Instance is missing."
            );

            return;
        }


        int safeHealAmount =
            Mathf.Max(
                0,
                healAmount
            );


        /*
         * Healing on a losing segment does NOT consume a use.
         * The Plaster only wears out when its own segment wins.
         */
        RegisterActivation(
            owner,
            StickerSpinLocation.NonWinningSegment,
            $"Heal {safeHealAmount} Blood",
            0,
            false
        );


        if (safeHealAmount > 0)
        {
            BloodManager.Instance
                .HealBlood(
                    safeHealAmount
                );
        }
    }


    // =========================================================
    // ALBUM - LOSE 1 USE
    // =========================================================

    private void ResolveAlbum(
        BaseSticker owner)
    {
        RegisterActivation(
            owner,
            StickerSpinLocation.Album,
            "Lose 1 use",
            0,
            true
        );
    }


    // =========================================================
    // USE CONSUMPTION
    // =========================================================

    public override bool ShouldConsumeUseOnActivation(
        StickerSpinLocation location)
    {
        return
            location ==
                StickerSpinLocation.WinningSegment ||
            location ==
                StickerSpinLocation.Album;
    }



    // =========================================================
    // TOOLTIP LOCATIONS
    // =========================================================

    protected override bool SupportsTooltipLocation(
        StickerSpinLocation location)
    {
        return
            location ==
                StickerSpinLocation.WinningSegment ||
            location ==
                StickerSpinLocation.NonWinningSegment ||
            location ==
                StickerSpinLocation.Album;
    }


    // =========================================================
    // TOOLTIP TOKENS
    // =========================================================

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
                "{heal}",
                Mathf.Max(
                    0,
                    healAmount
                )
                .ToString()
            );
    }
}
