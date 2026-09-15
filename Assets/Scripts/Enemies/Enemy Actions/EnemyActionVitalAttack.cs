using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "EnemyAction_VitalAttack",
    menuName = "Hell's Roulette/Enemy Actions/Vital Attack"
)]
public class EnemyActionVitalAttack : EnemyAction
{
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


        int currentHealth =
            enemy != null
                ? Mathf.Max(
                    0,
                    enemy.CurrentHP
                )
                : 0;


        /*
         * Supported token:
         *
         * {health}
         *
         * This is resolved from the enemy's CURRENT HP, so the tooltip updates
         * as the enemy takes damage.
         */
        if (!string.IsNullOrWhiteSpace(
                description))
        {
            return
                description.Replace(
                    "{health}",
                    currentHealth.ToString()
                );
        }


        return
            $"Deal damage equal to this enemy's current HP ({currentHealth}).";
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


        int damage =
            Mathf.Max(
                0,
                enemy.CurrentHP
            );


        if (damage <= 0)
            return;


        /*
         * Use the normal enemy attack pipeline so Shield, Knight's Helmet and
         * any future damage blockers can mitigate this attack normally.
         */
        enemy.PerformBloodAttack(
            damage
        );
    }
}
