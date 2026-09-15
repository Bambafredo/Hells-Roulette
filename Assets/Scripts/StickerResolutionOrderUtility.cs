using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// Single source of truth for roulette sticker ordering.
///
/// Two different orders intentionally exist:
///
/// SEGMENT RESOLUTION ORDER
/// - Used by the winning segment.
/// - SpinResolutionPriority first.
/// - Then INSIDE -> OUTSIDE by physical collider distance.
///
/// RADIAL ORDER
/// - Used by the global non-winning roulette phase.
/// - Pure INSIDE -> OUTSIDE across the whole wheel.
/// - SpinResolutionPriority is deliberately ignored because priority overrides
///   currently describe special winning-resolution behaviour.
///
/// Both use the closest point on the real Collider2D silhouette rather than
/// transform pivots, so gameplay and planning overlays measure the same thing.
/// </summary>
public static class StickerResolutionOrderUtility
{
    private const float DistanceTieEpsilon =
        0.000001f;


    /// <summary>
    /// Backwards-compatible name for the existing winning/segment order.
    /// Priority first, then radial distance.
    /// </summary>
    public static List<BaseSticker> BuildStableResolutionOrder(
        IList<BaseSticker> stickers,
        Transform wheelCenter)
    {
        return
            BuildOrderedList(
                stickers,
                wheelCenter,
                true
            );
    }


    /// <summary>
    /// Explicit alias used by presentation code when it wants to communicate
    /// that this is the per-segment / winning-resolution rule.
    /// </summary>
    public static List<BaseSticker> BuildSegmentResolutionOrder(
        IList<BaseSticker> stickers,
        Transform wheelCenter)
    {
        return
            BuildStableResolutionOrder(
                stickers,
                wheelCenter
            );
    }


    /// <summary>
    /// Builds one global INSIDE -> OUTSIDE order without applying
    /// SpinResolutionPriority.
    ///
    /// This is the real non-winning roulette resolution order.
    /// </summary>
    public static List<BaseSticker> BuildRadialResolutionOrder(
        IList<BaseSticker> stickers,
        Transform wheelCenter)
    {
        return
            BuildOrderedList(
                stickers,
                wheelCenter,
                false
            );
    }


    private static List<BaseSticker> BuildOrderedList(
        IList<BaseSticker> stickers,
        Transform wheelCenter,
        bool includeSpinResolutionPriority)
    {
        List<BaseSticker> ordered =
            new List<BaseSticker>();


        if (stickers == null)
            return ordered;


        foreach (BaseSticker sticker in stickers)
        {
            if (sticker == null)
                continue;


            int insertIndex =
                ordered.Count;


            for (int i = 0;
                 i < ordered.Count;
                 i++)
            {
                if (ComesBefore(
                        sticker,
                        ordered[i],
                        wheelCenter,
                        includeSpinResolutionPriority))
                {
                    insertIndex =
                        i;

                    break;
                }
            }


            ordered.Insert(
                insertIndex,
                sticker
            );
        }


        return ordered;
    }


    /// <summary>
    /// Returns the exact world-space point on the sticker silhouette that is
    /// closest to the wheel centre.
    ///
    /// The order-number overlay also anchors here so the UI visually explains
    /// the same measurement gameplay uses.
    /// </summary>
    public static Vector2 GetClosestPointToWheelCenter(
        BaseSticker sticker,
        Transform wheelCenter)
    {
        if (sticker == null)
            return Vector2.zero;


        Vector2 center =
            wheelCenter != null
                ? (Vector2)wheelCenter.position
                : Vector2.zero;


        Collider2D stickerCollider =
            sticker.StickerCollider;


        if (stickerCollider != null)
        {
            return
                stickerCollider.ClosestPoint(
                    center
                );
        }


        Transform fallbackTransform =
            sticker.stickerRoot != null
                ? sticker.stickerRoot
                : sticker.transform;


        return
            fallbackTransform != null
                ? (Vector2)fallbackTransform.position
                : Vector2.zero;
    }


    public static float GetDistanceToWheelCenterSqr(
        BaseSticker sticker,
        Transform wheelCenter)
    {
        Vector2 center =
            wheelCenter != null
                ? (Vector2)wheelCenter.position
                : Vector2.zero;


        Vector2 closestPoint =
            GetClosestPointToWheelCenter(
                sticker,
                wheelCenter
            );


        return
            (closestPoint - center)
                .sqrMagnitude;
    }


    public static int GetSpinResolutionPriority(
        BaseSticker sticker)
    {
        return
            sticker != null &&
            sticker.effect != null
                ? sticker.effect
                    .SpinResolutionPriority
                : 0;
    }


    public static bool HasSpinResolutionPriorityOverride(
        BaseSticker sticker)
    {
        return
            GetSpinResolutionPriority(
                sticker
            ) != 0;
    }


    private static bool ComesBefore(
        BaseSticker candidate,
        BaseSticker existing,
        Transform wheelCenter,
        bool includeSpinResolutionPriority)
    {
        if (candidate == null)
            return false;

        if (existing == null)
            return true;


        if (includeSpinResolutionPriority)
        {
            int candidatePriority =
                GetSpinResolutionPriority(
                    candidate
                );

            int existingPriority =
                GetSpinResolutionPriority(
                    existing
                );


            if (candidatePriority !=
                existingPriority)
            {
                return
                    candidatePriority <
                    existingPriority;
            }
        }


        float candidateDistance =
            GetDistanceToWheelCenterSqr(
                candidate,
                wheelCenter
            );

        float existingDistance =
            GetDistanceToWheelCenterSqr(
                existing,
                wheelCenter
            );


        float distanceDifference =
            candidateDistance -
            existingDistance;


        if (Mathf.Abs(
                distanceDifference) >
            DistanceTieEpsilon)
        {
            return
                distanceDifference < 0f;
        }


        /*
         * Exact radial ties are rare, but gameplay and UI must still agree.
         * Instance ID is stable for the lifetime of the current scene.
         */
        return
            candidate.GetInstanceID() <
            existing.GetInstanceID();
    }
}
