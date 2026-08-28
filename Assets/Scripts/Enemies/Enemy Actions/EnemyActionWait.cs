using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "EnemyAction_Wait",
    menuName = "Hell's Roulette/Enemy Actions/Wait"
)]
public class EnemyActionWait : EnemyAction
{
    // =========================================================
    // EXECUTION
    // =========================================================

    public override void Execute(
        BaseEnemy enemy)
    {
        if (enemy == null)
            return;


        /*
         * WAIT is intentionally a real action asset rather than a null /
         * special case. It participates in the same sequence, icon and
         * future tooltip systems as every other action.
         */
        Debug.Log(
            $"[ENEMY] {enemy.EnemyName} waits."
        );
    }
}
