using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "EnemyCurse_DebtIncrease",
    menuName = "Hell's Roulette/Enemy Curses/Debt Increase"
)]
public class EnemyCurseDebtIncrease : EnemyCurse
{
    // =========================================================
    // LIFECYCLE
    // =========================================================

    public override void Activate(
        BaseEnemy enemy,
        int value)
    {
        if (enemy == null ||
            value <= 0 ||
            RoundManager.Instance == null)
        {
            return;
        }


        RoundManager.Instance
            .RegisterEnemyDebtCurse(
                enemy,
                value
            );
    }


    public override void Deactivate(
        BaseEnemy enemy,
        int value)
    {
        if (enemy == null ||
            RoundManager.Instance == null)
        {
            return;
        }


        RoundManager.Instance
            .UnregisterEnemyDebtCurse(
                enemy
            );
    }


    // =========================================================
    // TOOLTIP
    // =========================================================

    public override string GetTooltipDescription(
        BaseEnemy enemy,
        int value)
    {
        string authored =
            base.GetTooltipDescription(
                enemy,
                value
            );


        if (!string.IsNullOrWhiteSpace(
            authored))
        {
            return authored;
        }


        return
            $"Increase this round's Debt by {value}% " +
            $"while this enemy is alive.";
    }
}
