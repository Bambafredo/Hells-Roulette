using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// Single source of truth for the physical resolution order of stickers that
/// share one roulette segment.
///
/// Ordinary stickers resolve INSIDE -> OUTSIDE. "Inside" is intentionally NOT
/// based on Transform.position, pivot or sprite bounds: it is the shortest
/// distance from the wheel centre to the sticker's real Collider2D silhouette.
///
/// Existing StickerEffect.SpinResolutionPriority remains the first sorting key
/// so special generic systems that explicitly require earlier / later resolution
/// keep their authored behaviour.
/// </summary>
public static class StickerResolutionOrderUtility
{
    private const float DistanceTieEpsilon =
        0.000001f;


    /// <summary>
    /// Builds a deterministic resolution order without mutating the supplied
    /// collection.
    ///
    /// Arrays and Lists both implement IList, so gameplay and presentation can
    /// share this method without converting between collection types.
    /// </summary>
    public static List<BaseSticker> BuildStableResolutionOrder(
        IList<BaseSticker> stickers,
        Transform wheelCenter)
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
                        wheelCenter))
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
    /// This is also the best visual anchor for the optional order-number overlay,
    /// because it shows the player what the ordering rule is actually measuring.
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


        /*
         * Defensive fallback only. Every normal gameplay sticker already owns a
         * real Collider2D, but a transform fallback prevents one incomplete test
         * prefab from breaking the entire ordering pass.
         */
        Transform fallbackTransform =
            sticker.stickerRoot != null
                ? sticker.stickerRoot
                : sticker.transform;


        return
            fallbackTransform != null
                ? (Vector2)fallbackTransform.position
                : Vector2.zero;
    }


    /// <summary>
    /// Squared distance is enough for comparisons and avoids an unnecessary
    /// square root. With a handful of stickers per segment this cost is tiny.
    /// </summary>
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


    private static bool ComesBefore(
        BaseSticker candidate,
        BaseSticker existing,
        Transform wheelCenter)
    {
        if (candidate == null)
            return false;

        if (existing == null)
            return true;


        int candidatePriority =
            candidate.effect != null
                ? candidate.effect
                    .SpinResolutionPriority
                : 0;

        int existingPriority =
            existing.effect != null
                ? existing.effect
                    .SpinResolutionPriority
                : 0;


        if (candidatePriority !=
            existingPriority)
        {
            return
                candidatePriority <
                existingPriority;
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
         * Exact radial ties are rare, but they must still resolve identically in
         * gameplay and in the visual overlay. Instance ID is deterministic for
         * the lifetime of the current scene and avoids depending on discovery
         * order from two different callers.
         */
        return
            candidate.GetInstanceID() <
            existing.GetInstanceID();
    }
}
