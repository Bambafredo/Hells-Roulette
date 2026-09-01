using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyPanelManager : MonoBehaviour
{
    // =========================================================
    // CORRIDOR
    // =========================================================

    [Header("Enemy Corridor")]

    [Tooltip(
        "If assigned, targeting uses the corridor CurrentRow instead of " +
        "the legacy Enemy Slots below."
    )]
    public EnemyCorridorController corridorController;


    // =========================================================
    // LEGACY
    // =========================================================

    [Header("Legacy Enemy Slots (fallback)")]

    [Tooltip(
        "Kept for backwards compatibility. Used only when no " +
        "EnemyCorridorController is assigned."
    )]
    public Transform[] enemySlots;


    // =========================================================
    // TARGETING
    // =========================================================

    public BaseEnemy GetLeftmostAliveEnemy()
    {
        /*
         * New corridor mode.
         *
         * Only enemies that have physically reached CurrentRow and whose
         * CombatActive flag is true can be targeted.
         */
        if (corridorController != null &&
            corridorController.CurrentRow != null)
        {
            return
                GetLeftmostAliveEnemyInRow(
                    corridorController.CurrentRow
                );
        }


        /*
         * Legacy fallback so existing scenes/prefabs do not break if the
         * corridor reference has not been assigned yet.
         */
        if (enemySlots == null)
            return null;


        foreach (Transform slot in enemySlots)
        {
            if (slot == null)
                continue;


            BaseEnemy enemy =
                slot.GetComponentInChildren<BaseEnemy>(
                    true
                );


            if (enemy != null &&
                enemy.gameObject.activeInHierarchy &&
                !enemy.IsDead)
            {
                return enemy;
            }
        }


        return null;
    }


    public BaseEnemy GetRightmostAliveEnemy()
    {
        if (corridorController != null &&
            corridorController.CurrentRow != null)
        {
            return GetRightmostAliveEnemyInRow(
                corridorController.CurrentRow
            );
        }

        if (enemySlots == null)
            return null;

        for (int i = enemySlots.Length - 1; i >= 0; i--)
        {
            Transform slot = enemySlots[i];
            if (slot == null)
                continue;

            BaseEnemy enemy =
                slot.GetComponentInChildren<BaseEnemy>(true);

            if (enemy != null &&
                enemy.gameObject.activeInHierarchy &&
                !enemy.IsDead)
            {
                return enemy;
            }
        }

        return null;
    }


    // =========================================================
    // CORRIDOR TARGETING
    // =========================================================

    private BaseEnemy GetLeftmostAliveEnemyInRow(
        Transform row)
    {
        if (row == null)
            return null;


        BaseEnemy[] enemies =
            row.GetComponentsInChildren<BaseEnemy>(
                true
            );


        BaseEnemy leftmost =
            null;


        foreach (BaseEnemy enemy in enemies)
        {
            if (enemy == null ||
                enemy.IsDead ||
                !enemy.CombatActive ||
                !enemy.gameObject.activeInHierarchy)
            {
                continue;
            }


            if (leftmost == null ||
                enemy.transform.position.x <
                leftmost.transform.position.x)
            {
                leftmost =
                    enemy;
            }
        }


        return leftmost;
    }


    private BaseEnemy GetRightmostAliveEnemyInRow(
        Transform row)
    {
        if (row == null)
            return null;

        BaseEnemy[] enemies =
            row.GetComponentsInChildren<BaseEnemy>(true);

        BaseEnemy rightmost = null;

        foreach (BaseEnemy enemy in enemies)
        {
            if (enemy == null ||
                enemy.IsDead ||
                !enemy.CombatActive ||
                !enemy.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (rightmost == null ||
                enemy.transform.position.x > rightmost.transform.position.x)
            {
                rightmost = enemy;
            }
        }

        return rightmost;
    }
}
