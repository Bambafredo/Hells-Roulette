using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Presentation data that can replace the default Segment Block tooltip
/// for a specific blocked segment.
///
/// Gameplay state remains owned by WheelGenerator. Providers only describe
/// how that already-blocked segment should be presented to the player.
/// </summary>
public struct SegmentBlockTooltipOverrideData
{
    public string title;
    public string description;
    public string status;

    public SegmentBlockTooltipOverrideData(
        string title,
        string description,
        string status)
    {
        this.title = title;
        this.description = description;
        this.status = status;
    }
}


/// <summary>
/// Generic extension point for encounters/effects that give a normal
/// Segment Block extra semantics.
///
/// SegmentBlockTooltipManager never needs to know which boss/effect owns it.
/// </summary>
public interface ISegmentBlockTooltipOverrideProvider
{
    bool TryGetSegmentBlockTooltipOverride(
        int segmentIndex,
        out SegmentBlockTooltipOverrideData data);
}


/// <summary>
/// Small runtime registry used by SegmentBlockTooltipManager.
/// Providers register themselves while alive/enabled.
/// </summary>
public static class SegmentBlockTooltipOverrideRegistry
{
    private static readonly List<ISegmentBlockTooltipOverrideProvider>
        providers =
            new List<ISegmentBlockTooltipOverrideProvider>();


    public static void Register(
        ISegmentBlockTooltipOverrideProvider provider)
    {
        if (provider == null ||
            providers.Contains(provider))
        {
            return;
        }

        providers.Add(provider);
    }


    public static void Unregister(
        ISegmentBlockTooltipOverrideProvider provider)
    {
        if (provider == null)
            return;

        providers.Remove(provider);
    }


    public static bool TryGetOverride(
        int segmentIndex,
        out SegmentBlockTooltipOverrideData data)
    {
        for (int i = providers.Count - 1;
             i >= 0;
             i--)
        {
            ISegmentBlockTooltipOverrideProvider provider =
                providers[i];

            UnityEngine.Object unityObject =
                provider as UnityEngine.Object;

            if (provider == null ||
                unityObject == null)
            {
                providers.RemoveAt(i);
                continue;
            }

            if (provider.TryGetSegmentBlockTooltipOverride(
                    segmentIndex,
                    out data))
            {
                return true;
            }
        }

        data =
            default;

        return false;
    }
}
