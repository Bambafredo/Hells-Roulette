using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StickerWashingMachine",
    menuName = "Stickers/Sticker Washing Machine"
)]
public class StickerWashingMachine : StickerEffect
{
    // =========================================================
    // CONFIG
    // =========================================================

    [Header("Washing Machine")]

    [Tooltip(
        "Percentage by which every OTHER sticker currently in the Album " +
        "shrinks whenever this sticker activates. " +
        "The reduction is applied to the sticker's current size, so repeated " +
        "activations stack multiplicatively."
    )]
    [Range(0f, 95f)]
    public float shrinkPercentPerActivation = 20f;


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
                "StickerWashingMachine: BaseSticker owner was not provided."
            );

            return;
        }


        /*
         * Capture the targets before registering the activation.
         *
         * RegisterActivation can consume the Washing Machine's final use
         * and schedule its destruction. Destroy() is deferred, but keeping
         * target discovery separate makes the intended order explicit.
         */
        List<BaseSticker> targets =
            GetAlbumTargets(owner);


        string description =
            BuildLogDescription(
                targets.Count
            );


        /*
         * Album activation:
         *
         * - writes the Washing Machine effect to the Game Log
         * - consumes a use if consumeUseOnAlbumActivation is enabled
         * - logs uses remaining
         *
         * The actual shrinking happens immediately afterwards.
         */
        RegisterActivation(
            owner,
            StickerSpinLocation.Album,
            description,
            0,
            null
        );


        ShrinkTargets(
            targets
        );
    }


    // =========================================================
    // TARGET DISCOVERY
    // =========================================================

    private List<BaseSticker> GetAlbumTargets(
        BaseSticker owner)
    {
        List<BaseSticker> targets =
            new List<BaseSticker>();


        if (AlbumManager.Instance == null ||
            AlbumManager.Instance.albumZone == null)
        {
            return targets;
        }


        Transform contentRoot =
            AlbumManager.Instance
                .albumZone
                .GetContentRoot();


        if (contentRoot == null)
            return targets;


        BaseSticker[] albumStickers =
            contentRoot
                .GetComponentsInChildren<BaseSticker>(
                    true
                );


        foreach (BaseSticker candidate in albumStickers)
        {
            if (candidate == null ||
                candidate == owner)
            {
                continue;
            }


            /*
             * Be explicit about Album membership.
             *
             * This matches the same logic RouletteController already uses
             * when it builds its Album sticker snapshot.
             */
            if (!AlbumManager.Instance
                .IsStickerInAlbum(candidate))
            {
                continue;
            }


            /*
             * A limited sticker at 0 uses has already been logically
             * consumed, even if Unity's deferred Destroy() has not removed
             * the GameObject yet.
             */
            if (candidate.HasLimitedUses &&
                candidate.RemainingUses <= 0)
            {
                continue;
            }


            targets.Add(
                candidate
            );
        }


        return targets;
    }


    // =========================================================
    // SHRINK
    // =========================================================

    private void ShrinkTargets(
        List<BaseSticker> targets)
    {
        if (targets == null ||
            targets.Count == 0)
        {
            return;
        }


        float multiplier =
            1f -
            Mathf.Clamp(
                shrinkPercentPerActivation,
                0f,
                95f
            ) /
            100f;


        foreach (BaseSticker sticker in targets)
        {
            if (sticker == null)
                continue;


            Transform root =
                sticker.stickerRoot != null
                    ? sticker.stickerRoot
                    : sticker.transform;


            if (root == null)
                continue;


            Vector3 currentScale =
                root.localScale;


            root.localScale =
                new Vector3(
                    currentScale.x * multiplier,
                    currentScale.y * multiplier,
                    currentScale.z
                );
        }


        /*
         * Sticker colliders can live on child GameObjects.
         * Sync immediately so the resized collider geometry is available
         * to placement / overlap checks without waiting for the next
         * physics step.
         */
        Physics2D.SyncTransforms();


        Debug.Log(
            $"[WASHING MACHINE] Shrunk {targets.Count} Album sticker(s) " +
            $"by {shrinkPercentPerActivation:0.##}%."
        );
    }


    // =========================================================
    // LOG
    // =========================================================

    private string BuildLogDescription(
        int targetCount)
    {
        string stickerWord =
            targetCount == 1
                ? "sticker"
                : "stickers";


        if (targetCount <= 0)
        {
            return
                "No other Album stickers to shrink";
        }


        return
            $"Shrinks {targetCount} Album {stickerWord} " +
            $"by {shrinkPercentPerActivation:0.##}%";
    }
}
