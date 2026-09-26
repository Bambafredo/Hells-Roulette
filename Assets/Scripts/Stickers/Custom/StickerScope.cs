using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StickerScope",
    menuName = "Stickers/Sticker Scope"
)]
public class StickerScope : StickerEffect
{
    /*
     * Scope establishes the targeting rule used by the other stickers in the
     * winning segment, so it resolves before ordinary priority-0 effects.
     *
     * Same generic priority as Coffee.
     */
    public override int SpinResolutionPriority =>
        -100;


    /*
     * Runtime "already spent this spin" state must be tracked per physical
     * sticker because this StickerEffect ScriptableObject is shared.
     *
     * Each Scope removes itself from this set when its Winning activation
     * begins. The first real Scope-affected attack can then spend one use.
     */
    private readonly HashSet<int> scopesThatSpentUseThisSpin =
        new HashSet<int>();


    public override void ResolveSpinLocation(
        BaseSticker owner,
        StickerSpinLocation location)
    {
        if (owner == null ||
            location != StickerSpinLocation.WinningSegment)
        {
            return;
        }


        scopesThatSpentUseThisSpin.Remove(
            owner.GetInstanceID()
        );


        /*
         * Scope still logs its activation before the stickers whose targeting
         * it modifies, but use consumption is deferred until one of those
         * stickers actually acquires an enemy target.
         */
        RegisterActivation(
            owner,
            location,
            null,
            null,
            false
        );
    }


    /// <summary>
    /// Called by StickerTargetingUtility only after a Scope-affected sticker
    /// has actually found an enemy to attack.
    ///
    /// At most one use is spent per physical Scope per spin.
    /// Lucky Shot is the free "360 no-scope" case.
    /// </summary>
    public void NotifyScopeAffectedAttack(
        BaseSticker scopeOwner)
    {
        if (scopeOwner == null ||
            scopeOwner.IsConsumed ||
            scopeOwner.IsPendingGameplayDestruction ||
            !scopeOwner.HasLimitedUses)
        {
            return;
        }


        int ownerId =
            scopeOwner.GetInstanceID();

        if (scopesThatSpentUseThisSpin.Contains(ownerId))
            return;


        if (IsLuckyShot())
            return;


        /*
         * Respect the authored generic "Consume Use On Winning" toggle.
         * Calling base avoids our conditional override below.
         */
        if (!base.ShouldConsumeUseOnActivation(
                StickerSpinLocation.WinningSegment
            ))
        {
            return;
        }


        scopesThatSpentUseThisSpin.Add(
            ownerId
        );

        scopeOwner.ConsumeUseAfterActivation();
    }


    /*
     * Never let RegisterActivation consume Scope automatically.
     * Consumption is conditional and happens only through
     * NotifyScopeAffectedAttack().
     */
    public override bool ShouldConsumeUseOnActivation(
        StickerSpinLocation location)
    {
        return false;
    }


    protected override bool DefaultShowsUseConsumptionTag(
        StickerSpinLocation location)
    {
        /*
         * The generic "[Consumes 1 use]" tag would imply unconditional
         * consumption. Scope spends a use only if its targeting modifier is
         * actually used, and never on Lucky Shot.
         */
        if (location == StickerSpinLocation.WinningSegment)
            return false;

        return
            base.DefaultShowsUseConsumptionTag(
                location
            );
    }


    protected override bool SupportsTooltipLocation(
        StickerSpinLocation location)
    {
        return
            location ==
            StickerSpinLocation.WinningSegment;
    }


    private bool IsLuckyShot()
    {
        RouletteController roulette =
            RouletteController.Instance;

        return
            roulette != null &&
            roulette.SpinInProgress &&
            roulette.CurrentSpinMethod ==
                RouletteController.SpinMethod.LuckyShot;
    }
}
