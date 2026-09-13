using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Presentation data supplied by a temporary segment-mark effect.
///
/// This is intentionally separate from SegmentBlockTooltipOverrideData:
/// - SegmentBlockTooltipOverrideRegistry still describes SPECIAL BLOCKS such
///   as Limbo's permanent blocks.
/// - SegmentMarkTooltipRegistry describes ADDITIONAL temporary segment rules
///   such as Dominate.
///
/// A segment may therefore be both blocked and marked at the same time.
/// </summary>
public struct SegmentMarkTooltipData
{
    public string title;
    public string description;
    public string status;


    public SegmentMarkTooltipData(
        string title,
        string description,
        string status)
    {
        this.title =
            title;

        this.description =
            description;

        this.status =
            status;
    }
}


/// <summary>
/// Generic extension point for any system that temporarily marks wheel
/// segments with extra gameplay semantics.
/// </summary>
public interface ISegmentMarkTooltipProvider
{
    bool TryGetSegmentMarkTooltip(
        int segmentIndex,
        out SegmentMarkTooltipData data);
}


/// <summary>
/// Runtime registry queried by SegmentBlockTooltipManager.
///
/// The existing tooltip manager remains the single segment-tooltip presenter;
/// this registry only contributes extra sections when a segment is marked.
/// </summary>
public static class SegmentMarkTooltipRegistry
{
    private static readonly List<ISegmentMarkTooltipProvider>
        providers =
            new List<ISegmentMarkTooltipProvider>();


    public static void Register(
        ISegmentMarkTooltipProvider provider)
    {
        if (provider == null ||
            providers.Contains(provider))
        {
            return;
        }


        providers.Add(
            provider
        );
    }


    public static void Unregister(
        ISegmentMarkTooltipProvider provider)
    {
        if (provider == null)
            return;


        providers.Remove(
            provider
        );
    }


    /// <summary>
    /// Collects every mark section that applies to one segment.
    ///
    /// Identical sections are deduplicated. This is useful for several enemies
    /// using the same Dominate action: gameplay still resolves once per enemy,
    /// but the player does not need to read the exact same tooltip 3 times.
    /// </summary>
    public static void CollectTooltips(
        int segmentIndex,
        List<SegmentMarkTooltipData> results)
    {
        if (results == null)
            return;


        results.Clear();


        for (int i = providers.Count - 1;
             i >= 0;
             i--)
        {
            ISegmentMarkTooltipProvider provider =
                providers[i];

            UnityEngine.Object unityObject =
                provider as UnityEngine.Object;


            if (provider == null ||
                unityObject == null)
            {
                providers.RemoveAt(i);
                continue;
            }


            if (!provider.TryGetSegmentMarkTooltip(
                    segmentIndex,
                    out SegmentMarkTooltipData data))
            {
                continue;
            }


            if (ContainsEquivalent(
                    results,
                    data))
            {
                continue;
            }


            results.Add(
                data
            );
        }
    }


    private static bool ContainsEquivalent(
        List<SegmentMarkTooltipData> results,
        SegmentMarkTooltipData candidate)
    {
        for (int i = 0;
             i < results.Count;
             i++)
        {
            SegmentMarkTooltipData existing =
                results[i];


            if (existing.title == candidate.title &&
                existing.description == candidate.description &&
                existing.status == candidate.status)
            {
                return true;
            }
        }


        return false;
    }
}
