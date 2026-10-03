using UnityEngine;

/// <summary>
/// Marker implemented by StickerEffect classes that mechanically count as Ants.
///
/// This is a gameplay trait only. It does not need to exist in the scene.
/// </summary>
public interface IAntSticker
{
}


public static class StickerAntUtility
{
    /// <summary>
    /// Returns true when this physical sticker's effect mechanically counts as an Ant.
    /// </summary>
    public static bool IsAnt(
        BaseSticker sticker)
    {
        return
            sticker != null &&
            sticker.effect is IAntSticker;
    }


    /// <summary>
    /// Counts functional Ant stickers currently placed anywhere on the roulette.
    ///
    /// Uses WheelGenerator's segment list as the authority instead of a global
    /// FindObjectsOfType scan. This matches the rest of the roulette code and
    /// guarantees that only stickers actually owned by roulette segments count.
    /// </summary>
    public static int CountAntsOnWheel()
    {
        WheelGenerator generator =
            Object.FindObjectOfType<WheelGenerator>();


        if (generator == null ||
            generator.segments == null)
        {
            return 0;
        }


        int count =
            0;


        foreach (WheelSegmentData segmentData in
                 generator.segments)
        {
            if (segmentData == null ||
                segmentData.collider == null)
            {
                continue;
            }


            Transform segment =
                segmentData.collider.transform;


            BaseSticker[] stickers =
                segment.GetComponentsInChildren<BaseSticker>(
                    true
                );


            foreach (BaseSticker sticker in stickers)
            {
                if (sticker == null ||
                    !sticker.isPlaced ||
                    sticker.currentSegment != segment ||
                    sticker.IsConsumed ||
                    sticker.IsPendingGameplayDestruction ||
                    !IsAnt(sticker))
                {
                    continue;
                }


                count++;
            }
        }


        return count;
    }


    /// <summary>
    /// Counts functional Ant stickers on the roulette, excluding one physical
    /// sticker if supplied. Useful for effects such as Ant Queen that scale
    /// from OTHER Ants only.
    /// </summary>
    public static int CountOtherAntsOnWheel(
        BaseSticker excludedSticker)
    {
        WheelGenerator generator =
            Object.FindObjectOfType<WheelGenerator>();


        if (generator == null ||
            generator.segments == null)
        {
            return 0;
        }


        int count =
            0;


        foreach (WheelSegmentData segmentData in
                 generator.segments)
        {
            if (segmentData == null ||
                segmentData.collider == null)
            {
                continue;
            }


            Transform segment =
                segmentData.collider.transform;


            BaseSticker[] stickers =
                segment.GetComponentsInChildren<BaseSticker>(
                    true
                );


            foreach (BaseSticker sticker in stickers)
            {
                if (sticker == null ||
                    sticker == excludedSticker ||
                    !sticker.isPlaced ||
                    sticker.currentSegment != segment ||
                    sticker.IsConsumed ||
                    sticker.IsPendingGameplayDestruction ||
                    !IsAnt(sticker))
                {
                    continue;
                }


                count++;
            }
        }


        return count;
    }


    /// <summary>
    /// Counts functional Ant stickers in the Album, excluding one physical
    /// sticker if supplied.
    /// </summary>
    public static int CountOtherAntsInAlbum(
        BaseSticker excludedSticker)
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


        BaseSticker[] stickers =
            contentRoot
                .GetComponentsInChildren<BaseSticker>(
                    true
                );


        int count =
            0;


        foreach (BaseSticker sticker in stickers)
        {
            if (sticker == null ||
                sticker == excludedSticker ||
                sticker.IsConsumed ||
                sticker.IsPendingGameplayDestruction ||
                !IsAnt(sticker))
            {
                continue;
            }


            if (!AlbumManager.Instance
                .IsStickerInAlbum(
                    sticker
                ))
            {
                continue;
            }


            count++;
        }


        return count;
    }
}