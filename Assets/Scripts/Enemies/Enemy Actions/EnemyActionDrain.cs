using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "EnemyAction_Drain",
    menuName = "Hell's Roulette/Enemy Actions/Drain"
)]
public class EnemyActionDrain : EnemyAction
{
    // =========================================================
    // DRAIN
    // =========================================================

    [Header("Drain")]

    [Tooltip(
        "Damage dealt to the player through the normal enemy attack pipeline. " +
        "Shield and other damage blockers can prevent it."
    )]
    [Min(0)]
    public int damage =
        2;


    [Tooltip(
        "HP this enemy attempts to restore after attacking. " +
        "Healing is capped at the enemy's Max HP."
    )]
    [Min(0)]
    public int healAmount =
        2;


    // =========================================================
    // TOOLTIP
    // =========================================================

    public override string GetTooltipDescription(
        BaseEnemy enemy)
    {
        string description =
            base.GetTooltipDescription(
                enemy
            );


        if (string.IsNullOrWhiteSpace(
                description))
        {
            return
                $"Deal {damage} damage and heal {healAmount} HP.";
        }


        return
            description
                .Replace(
                    "{damage}",
                    damage.ToString()
                )
                .Replace(
                    "{heal}",
                    healAmount.ToString()
                );
    }


    // =========================================================
    // EXECUTION
    // =========================================================

    public override void Execute(
        BaseEnemy enemy)
    {
        if (enemy == null ||
            enemy.IsDead)
        {
            return;
        }


        /*
         * Use the normal attack pipeline so Shield, Knight's Helmet and any
         * future damage blockers interact with Drain exactly like Attack.
         */
        if (damage > 0)
        {
            enemy.PerformBloodAttack(
                damage
            );
        }


        /*
         * Healing is an independent authored value.
         *
         * It does not depend on how much Blood the player actually lost after
         * mitigation. This lets Damage and Heal Amount be balanced separately.
         */
        if (healAmount > 0 &&
            !enemy.IsDead)
        {
            enemy.Heal(
                healAmount
            );
        }
    }
}
