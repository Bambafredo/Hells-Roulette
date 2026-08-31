using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StickerCoffee",
    menuName = "Stickers/Sticker Coffee"
)]
public class StickerCoffee : StickerEffect
{
    // =========================================================
    // CONFIGURATION
    // =========================================================

    [Header("Coffee")]

    [Tooltip(
        "Total number of times OTHER ordinary stickers in this wheel segment " +
        "resolve during the spin. Coffee itself does not receive activation " +
        "count modifiers, preventing recursive Coffee loops."
    )]
    [Min(1)]
    public int stickerActivations =
        2;


    public int StickerActivationCount =>
        Mathf.Max(
            1,
            stickerActivations
        );


    // =========================================================
    // GENERIC RESOLUTION MODIFIER
    // =========================================================

    /*
     * Coffee explains why the following stickers are resolving multiple
     * times, so let it resolve before ordinary priority-0 effects.
     *
     * RouletteController only knows the generic priority value. It has no
     * knowledge that this effect is Coffee.
     */
    public override int SpinResolutionPriority =>
        -100;


    /*
     * Coffee must not be multiplied by Coffee (or another future activation
     * modifier), otherwise two Coffees could recursively amplify each other.
     */
    public override bool ReceivesActivationCountModifiers =>
        false;


    public override int ModifyStickerActivationCount(
        BaseSticker modifierOwner,
        BaseSticker target,
        StickerSpinLocation location,
        int currentActivationCount)
    {
        if (modifierOwner == null ||
            target == null ||
            modifierOwner == target)
        {
            return
                currentActivationCount;
        }


        /*
         * Coffee is a WINNING-SEGMENT effect only.
         *
         * When Coffee sits in any non-winning segment it is completely inert:
         * it does not multiply neighbouring stickers and it does not activate.
         */
        if (location !=
            StickerSpinLocation.WinningSegment)
        {
            return
                currentActivationCount;
        }


        /*
         * The source and target must belong to the same physical wheel
         * segment in the captured pre-resolution state.
         */
        if (modifierOwner.currentSegment == null ||
            target.currentSegment == null ||
            modifierOwner.currentSegment !=
                target.currentSegment)
        {
            return
                currentActivationCount;
        }


        /*
         * Absolute "activate N times" semantics.
         *
         * Multiple Coffees therefore do not multiply each other: x2 + x2
         * remains x2 because both ask for an absolute minimum of 2.
         *
         * A future modifier can choose a different generic behaviour by
         * overriding this same method (for example current + 1).
         */
        return
            Mathf.Max(
                currentActivationCount,
                StickerActivationCount
            );
    }


    // =========================================================
    // RESOLUTION
    // =========================================================

    /// <summary>
    /// Coffee's modifier is evaluated while the generic spin snapshot is
    /// built. This resolution method is only Coffee's own feedback/use event.
    /// </summary>
    public override void ResolveSpinLocation(
        BaseSticker owner,
        StickerSpinLocation location)
    {
        if (location !=
            StickerSpinLocation.WinningSegment)
        {
            return;
        }


        if (RoundManager.Instance != null &&
            !RoundManager.Instance.WasLastSpinValid)
        {
            return;
        }


        RegisterActivation(
            owner,
            location,
            GetLogDescription(owner),
            0,
            null
        );
    }


    // =========================================================
    // LOG
    // =========================================================

    public override string GetLogDescription(
        BaseSticker owner)
    {
        if (!string.IsNullOrWhiteSpace(
                logDescription))
        {
            return
                ReplaceCoffeeTokens(
                    logDescription
                );
        }


        return
            $"Stickers in this segment activate " +
            $"{StickerActivationCount} times";
    }


    // =========================================================
    // TOOLTIP
    // =========================================================

    protected override bool SupportsTooltipLocation(
        StickerSpinLocation location)
    {
        return
            location ==
                StickerSpinLocation.WinningSegment;
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
            ReplaceCoffeeTokens(
                resolved
            );
    }


    private string ReplaceCoffeeTokens(
        string source)
    {
        if (string.IsNullOrEmpty(source))
            return "";


        return
            source
                .Replace(
                    "{activations}",
                    StickerActivationCount
                        .ToString()
                );
    }
}
