using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StickerRevolver",
    menuName = "Stickers/Sticker Revolver"
)]
public class StickerRevolver : StickerEffect
{
    // =========================================================
    // CONFIGURATION
    // =========================================================

    [Header("Revolver")]

    [Min(0)]
    public int damageAmount =
        5;


    // =========================================================
    // EFFECT
    // =========================================================

    public override void ApplyEffect(
        BaseSticker owner)
    {
        if (RoundManager.Instance != null &&
            !RoundManager.Instance.WasLastSpinValid)
        {
            return;
        }


        EnemyPanelManager enemyPanel =
            Object.FindObjectOfType<EnemyPanelManager>();


        if (enemyPanel == null)
        {
            Debug.LogWarning(
                "StickerRevolver: EnemyPanelManager was not found."
            );

            return;
        }


        /*
         * Revolver is NATIVELY rightmost-targeting.
         *
         * Do not route this through StickerTargetingUtility:
         * Scope only modifies attacks that naturally start from the left,
         * so Revolver must be ignored by Scope.
         *
         * GetRightmostAliveEnemy() still preserves the generic
         * single-target rules such as Untouchable.
         */
        BaseEnemy target =
            enemyPanel.GetRightmostAliveEnemy();


        if (target == null)
        {
            RegisterActivation(
                owner,
                StickerSpinLocation.WinningSegment,
                "No valid target",
                0,
                null
            );

            return;
        }


        int damage =
            Mathf.Max(
                0,
                damageAmount
            );


        string targetName =
            target.enemyName;


        if (GameLogManager.Instance != null)
        {
            targetName =
                GameLogManager.Instance
                    .EnemyText(
                        target.enemyName
                    );
        }


        /*
         * Revolver is a damage-first limited-use sticker.
         *
         * Important for enemy Curses such as Burnout:
         * if this shot kills the enemy that owns Burnout, BaseEnemy removes
         * that Curse immediately inside Die(). Therefore the use must be spent
         * AFTER damage resolves, not inside RegisterActivation().
         */
        bool shouldConsumeUse =
            base.ShouldConsumeUseOnActivation(
                StickerSpinLocation.WinningSegment
            );


        RegisterActivation(
            owner,
            StickerSpinLocation.WinningSegment,
            $"Deal {damage} damage to {targetName}",
            0,
            false
        );


        if (damage > 0)
        {
            target.TakeDamage(
                damage
            );
        }


        if (shouldConsumeUse &&
            owner != null &&
            !owner.IsConsumed &&
            !owner.IsPendingGameplayDestruction)
        {
            owner.ConsumeUseAfterActivation();
        }
    }


    // =========================================================
    // TOOLTIP
    // =========================================================

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
            resolved.Replace(
                "{damage}",
                Mathf.Max(
                    0,
                    damageAmount
                ).ToString()
            );
    }
}
