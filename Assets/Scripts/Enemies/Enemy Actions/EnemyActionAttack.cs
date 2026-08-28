using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "EnemyAction_Attack",
    menuName = "Hell's Roulette/Enemy Actions/Attack"
)]
public class EnemyActionAttack : EnemyAction
{
    // =========================================================
    // ATTACK
    // =========================================================

    [Header("Attack")]

    [Min(0)]
    public int bloodDamage =
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
                $"Lose {bloodDamage} Blood.";
        }


        return
            description.Replace(
                "{damage}",
                bloodDamage.ToString()
            );
    }


    // =========================================================
    // EXECUTION
    // =========================================================

    public override void Execute(
        BaseEnemy enemy)
    {
        if (enemy == null)
            return;


        enemy.PerformBloodAttack(
            bloodDamage
        );
    }
}
