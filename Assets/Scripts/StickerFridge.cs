using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StickerFridge",
    menuName = "Stickers/Sticker Fridge"
)]
public class StickerFridge : StickerEffect
{
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


        if (location != StickerSpinLocation.Album)
            return;


        if (owner == null)
        {
            Debug.LogWarning(
                "StickerFridge: BaseSticker owner was not provided."
            );

            return;
        }


        /*
         * Fridge heals 1 Blood for every OTHER functional sticker currently
         * in the Album.
         *
         * Fridge never counts itself.
         */
        int otherStickerCount =
            CountOtherAlbumStickers(
                owner
            );


        int requestedBloodGain =
            Mathf.Max(
                0,
                otherStickerCount
            );


        int actualBloodGain =
            CalculateActualBloodGain(
                requestedBloodGain
            );


        /*
         * Same empty-Album rule as Washing Machine:
         *
         * If Fridge is the only functional sticker in the Album, there is
         * nothing for its effect to scale from, so the activation does NOT
         * consume a use.
         *
         * As soon as at least one other valid Album sticker exists, normal
         * Album-use consumption applies exactly as authored in the SO.
         */
        bool shouldConsumeUse =
            otherStickerCount > 0
                ? ShouldConsumeUseOnActivation(
                    StickerSpinLocation.Album
                )
                : false;


        RegisterActivation(
            owner,
            StickerSpinLocation.Album,
            BuildLogDescription(
                otherStickerCount,
                actualBloodGain
            ),
            0,
            shouldConsumeUse
        );


        if (requestedBloodGain > 0 &&
            BloodManager.Instance != null)
        {
            BloodManager.Instance
                .HealBlood(
                    requestedBloodGain
                );
        }


        Debug.Log(
            $"[FRIDGE] Other Album stickers = {otherStickerCount}. " +
            $"Requested Blood gain = {requestedBloodGain}. " +
            $"Actual Blood restored = {actualBloodGain}."
        );
    }


    // =========================================================
    // ALBUM COUNT
    // =========================================================

    private int CountOtherAlbumStickers(
        BaseSticker owner)
    {
        if (AlbumManager.Instance == null ||
            AlbumManager.Instance.albumZone == null)
        {
            return 0;
        }


        Transform contentRoot =
            AlbumManager.Instance
                .albumZone
                .GetContentRoot();


        if (contentRoot == null)
            return 0;


        BaseSticker[] albumStickers =
            contentRoot
                .GetComponentsInChildren<BaseSticker>(
                    true
                );


        int count =
            0;


        foreach (BaseSticker candidate in albumStickers)
        {
            if (candidate == null ||
                candidate == owner)
            {
                continue;
            }


            /*
             * Count only stickers that are logically inside the Album.
             */
            if (!AlbumManager.Instance
                .IsStickerInAlbum(candidate))
            {
                continue;
            }


            /*
             * A limited-use sticker already at 0 uses is logically consumed,
             * even if Unity's deferred Destroy() has not removed its GameObject
             * yet. Match Washing Machine's target-counting semantics.
             */
            if (candidate.HasLimitedUses &&
                candidate.RemainingUses <= 0)
            {
                continue;
            }


            count++;
        }


        return count;
    }


    // =========================================================
    // BLOOD
    // =========================================================

    private int CalculateActualBloodGain(
        int requestedBloodGain)
    {
        if (BloodManager.Instance == null)
            return 0;


        int missingBlood =
            Mathf.Max(
                0,
                BloodManager.Instance.maxBlood -
                BloodManager.Instance.currentBlood
            );


        return
            Mathf.Min(
                Mathf.Max(
                    0,
                    requestedBloodGain
                ),
                missingBlood
            );
    }


    // =========================================================
    // GAME LOG
    // =========================================================

    private string BuildLogDescription(
        int otherStickerCount,
        int actualBloodGain)
    {
        if (otherStickerCount <= 0)
        {
            return
                "No other Album stickers: +0 Blood";
        }


        string bloodText =
            $"+{Mathf.Max(0, actualBloodGain)} Blood";


        if (GameLogManager.Instance != null)
        {
            bloodText =
                GameLogManager.Instance
                    .BloodText(
                        bloodText
                    );
        }


        string stickerWord =
            otherStickerCount == 1
                ? "sticker"
                : "stickers";


        return
            $"{otherStickerCount} other Album {stickerWord}: " +
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


    /*
     * Suggested Album Tooltip:
     *
     * Gain 1 Blood for each other sticker in the Album.
     * Current gain: {bloodGain} Blood.
     *
     * Custom tokens:
     * {otherStickerCount}
     * {bloodGain}
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


        int otherStickerCount =
            CountOtherAlbumStickers(
                owner
            );


        return
            resolved
                .Replace(
                    "{otherStickerCount}",
                    otherStickerCount.ToString()
                )
                .Replace(
                    "{bloodGain}",
                    otherStickerCount.ToString()
                );
    }
}
