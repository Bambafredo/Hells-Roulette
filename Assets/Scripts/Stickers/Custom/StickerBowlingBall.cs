using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StickerBowlingBall",
    menuName = "Stickers/Sticker Bowling Ball"
)]
public class StickerBowlingBall : StickerEffect
{
    // =========================================================
    // CONFIGURATION
    // =========================================================

    [Header("Bowling Ball")]

    [Tooltip(
        "Maximum number of accepted Flag Pin -> flapper hits allowed during " +
        "the spin for Bowling Ball to score a hit. Going above this is a Miss."
    )]
    [Min(0)]
    public int maxFlapperHits = 3;

    [Tooltip(
        "Damage dealt from left to right when Bowling Ball scores a hit. " +
        "Excess damage after killing an enemy carries over to the next one."
    )]
    [Min(0)]
    public int damageAmount = 10;


    // =========================================================
    // LOCATION-AWARE RESOLUTION
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
                "StickerBowlingBall: BaseSticker owner was not provided."
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
        }
    }


    // =========================================================
    // WINNING SEGMENT
    // =========================================================

    private void ResolveWinningSegment(
        BaseSticker owner)
    {
        int flapperHits =
            RoundManager.Instance != null
                ? Mathf.Max(
                    0,
                    RoundManager.Instance
                        .FlapperHitsThisSpin
                )
                : 0;


        int safeMaxHits =
            Mathf.Max(
                0,
                maxFlapperHits
            );


        bool scoredHit =
            flapperHits <=
            safeMaxHits;


        if (scoredHit)
        {
            int damageDealt =
                DealTrampleDamage(
                    owner
                );


            string description =
                damageDealt > 0
                    ? $"Strike! Deal {damageDealt} damage with Trample"
                    : "Strike! No valid enemy";


            RegisterActivation(
                owner,
                StickerSpinLocation.WinningSegment,
                description,
                0,
                true
            );
        }
        else
        {
            /*
             * A Miss still costs one use.
             */
            RegisterActivation(
                owner,
                StickerSpinLocation.WinningSegment,
                $"Miss ({flapperHits} flapper hits; maximum {safeMaxHits})",
                0,
                true
            );
        }
    }


    // =========================================================
    // LOSING SEGMENT
    // =========================================================

    private void ResolveNonWinningSegment(
        BaseSticker owner)
    {
        /*
         * The losing result has no additional gameplay effect: it simply
         * consumes one use through the normal generic use pipeline.
         */
        RegisterActivation(
            owner,
            StickerSpinLocation.NonWinningSegment,
            "Lose 1 use",
            0,
            true
        );
    }


    // =========================================================
    // TRAMPLE DAMAGE
    // =========================================================

    /// <summary>
    /// Deals Bowling Ball's damage from left to right.
    ///
    /// Only the HP actually needed to kill the current enemy is spent.
    /// Any excess continues into the next eligible enemy.
    ///
    /// Example:
    /// 10 damage against 4 / 4 / 4 HP -> 0 / 0 / 2 HP.
    ///
    /// We deliberately ask EnemyPanelManager for the leftmost eligible target
    /// again after each kill. This preserves the project's existing
    /// single-target rules (including Untouchable pass-through) instead of
    /// introducing a second targeting implementation inside the sticker.
    /// </summary>
    private int DealTrampleDamage(
        BaseSticker owner)
    {
        EnemyPanelManager enemyPanel =
            Object.FindObjectOfType<EnemyPanelManager>();


        if (enemyPanel == null)
        {
            Debug.LogWarning(
                "StickerBowlingBall: EnemyPanelManager was not found."
            );

            return 0;
        }


        int remainingDamage =
            Mathf.Max(
                0,
                damageAmount
            );


        int totalDamageDealt = 0;


        while (remainingDamage > 0)
        {
            BaseEnemy target =
                StickerTargetingUtility.GetFirstDamageTarget(
                    enemyPanel,
                    owner,
                    StickerSpinLocation.WinningSegment
                );


            if (target == null ||
                target.IsDead)
            {
                break;
            }


            int targetHP =
                Mathf.Max(
                    0,
                    target.CurrentHP
                );


            if (targetHP <= 0)
            {
                break;
            }


            int damageToThisEnemy =
                Mathf.Min(
                    remainingDamage,
                    targetHP
                );


            target.TakeDamage(
                damageToThisEnemy
            );


            remainingDamage -=
                damageToThisEnemy;


            totalDamageDealt +=
                damageToThisEnemy;


            /*
             * If the enemy survived, all available Bowling Ball damage has
             * necessarily been spent on it, so propagation ends.
             */
            if (!target.IsDead)
            {
                break;
            }
        }


        return totalDamageDealt;
    }


    // =========================================================
    // TOOLTIP
    // =========================================================

    protected override bool SupportsTooltipLocation(
        StickerSpinLocation location)
    {
        return
            location ==
                StickerSpinLocation.WinningSegment ||
            location ==
                StickerSpinLocation.NonWinningSegment;
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
            resolved
                .Replace(
                    "{damage}",
                    Mathf.Max(
                        0,
                        damageAmount
                    )
                    .ToString()
                )
                .Replace(
                    "{maxHits}",
                    Mathf.Max(
                        0,
                        maxFlapperHits
                    )
                    .ToString()
                );
    }
}
