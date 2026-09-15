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

    /// <summary>
    /// Returns the leftmost enemy that is currently eligible for a
    /// SINGLE-TARGET attack.
    ///
    /// During the spin method configured by an active targeting Curse
    /// (for example Untouchable on Power Spin or Lucky Shot), a left-to-right
    /// attack naturally "passes through" that enemy and reaches the next valid
    /// enemy instead.
    /// </summary>
    public BaseEnemy GetLeftmostAliveEnemy()
    {
        if (corridorController != null &&
            corridorController.CurrentRow != null)
        {
            return
                GetLeftmostAliveEnemyInRow(
                    corridorController.CurrentRow
                );
        }


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


            if (!IsLivingCombatEnemy(
                    enemy))
            {
                continue;
            }


            if (IsSpinProtectedSingleTarget(
                    enemy))
            {
                LogUntouchablePassThrough(
                    enemy
                );

                continue;
            }


            return enemy;
        }


        return null;
    }


    /// <summary>
    /// Returns every living combat-active enemy in CurrentRow.
    ///
    /// IMPORTANT: this is deliberately UNFILTERED by single-target immunity.
    /// Area/all-enemy effects such as Rat Poison or Catapult(All Enemies) use
    /// this method and therefore still damage Untouchable enemies.
    /// </summary>
    public BaseEnemy[] GetAllAliveEnemies()
    {
        if (corridorController != null &&
            corridorController.CurrentRow != null)
        {
            BaseEnemy[] rowEnemies =
                corridorController.CurrentRow
                    .GetComponentsInChildren<BaseEnemy>(
                        true
                    );

            List<BaseEnemy> alive =
                new List<BaseEnemy>();


            foreach (BaseEnemy enemy in rowEnemies)
            {
                if (!IsLivingCombatEnemy(
                        enemy))
                {
                    continue;
                }


                alive.Add(
                    enemy
                );
            }


            return alive.ToArray();
        }


        if (enemySlots == null)
            return new BaseEnemy[0];


        List<BaseEnemy> fallbackAlive =
            new List<BaseEnemy>();


        foreach (Transform slot in enemySlots)
        {
            if (slot == null)
                continue;


            BaseEnemy enemy =
                slot.GetComponentInChildren<BaseEnemy>(
                    true
                );


            if (IsLivingCombatEnemy(
                    enemy))
            {
                fallbackAlive.Add(
                    enemy
                );
            }
        }


        return fallbackAlive.ToArray();
    }


    /// <summary>
    /// Returns every enemy that can currently be chosen by a SINGLE-TARGET
    /// effect.
    ///
    /// This exists primarily for non-directional target selection such as Stone.
    /// Untouchable enemies are removed from the random pool only during the
    /// spin method configured by that Curse. Manual spins remain unaffected.
    /// </summary>
    public BaseEnemy[] GetAllSingleTargetEligibleEnemies()
    {
        BaseEnemy[] alive =
            GetAllAliveEnemies();


        if (alive == null ||
            alive.Length <= 0)
        {
            return new BaseEnemy[0];
        }


        List<BaseEnemy> eligible =
            new List<BaseEnemy>();


        foreach (BaseEnemy enemy in alive)
        {
            if (!IsEligibleSingleTargetEnemy(
                    enemy))
            {
                continue;
            }


            eligible.Add(
                enemy
            );
        }


        return eligible.ToArray();
    }


    /// <summary>
    /// Returns the rightmost enemy that is currently eligible for a
    /// SINGLE-TARGET attack.
    /// </summary>
    public BaseEnemy GetRightmostAliveEnemy()
    {
        if (corridorController != null &&
            corridorController.CurrentRow != null)
        {
            return
                GetRightmostAliveEnemyInRow(
                    corridorController.CurrentRow
                );
        }


        if (enemySlots == null)
            return null;


        for (int i = enemySlots.Length - 1;
             i >= 0;
             i--)
        {
            Transform slot =
                enemySlots[i];


            if (slot == null)
                continue;


            BaseEnemy enemy =
                slot.GetComponentInChildren<BaseEnemy>(
                    true
                );


            if (!IsLivingCombatEnemy(
                    enemy))
            {
                continue;
            }


            if (IsSpinProtectedSingleTarget(
                    enemy))
            {
                LogUntouchablePassThrough(
                    enemy
                );

                continue;
            }


            return enemy;
        }


        return null;
    }


    // =========================================================
    // TARGET ELIGIBILITY
    // =========================================================

    private bool IsLivingCombatEnemy(
        BaseEnemy enemy)
    {
        return
            enemy != null &&
            !enemy.IsDead &&
            enemy.CombatActive &&
            enemy.gameObject.activeInHierarchy;
    }


    private bool IsEligibleSingleTargetEnemy(
        BaseEnemy enemy)
    {
        if (!IsLivingCombatEnemy(
                enemy))
        {
            return false;
        }


        return
            !IsSpinProtectedSingleTarget(
                enemy
            );
    }


    private bool IsSpinProtectedSingleTarget(
        BaseEnemy enemy)
    {
        if (enemy == null)
            return false;


        RouletteController.SpinMethod spinMethod;


        if (!TryGetResolvingProtectedSpinMethod(
                out spinMethod))
        {
            return false;
        }


        return
            enemy.BlocksSingleTargetingForSpin(
                spinMethod
            );
    }


    private void LogUntouchablePassThrough(
        BaseEnemy enemy)
    {
        if (enemy == null ||
            GameLogManager.Instance == null)
        {
            return;
        }


        string enemyName =
            string.IsNullOrWhiteSpace(
                enemy.EnemyName
            )
                ? "Enemy"
                : enemy.EnemyName;


        GameLogManager.Instance
            .AddGameplayLine(
                "Attack passes through " +
                GameLogManager.Instance
                    .EnemyText(
                        enemyName
                    ) +
                " (Untouchable)"
            );
    }


    private bool TryGetResolvingProtectedSpinMethod(
        out RouletteController.SpinMethod spinMethod)
    {
        spinMethod =
            RouletteController.SpinMethod.Manual;


        RouletteController roulette =
            RouletteController.Instance != null
                ? RouletteController.Instance
                : Object.FindObjectOfType<RouletteController>();


        if (roulette == null ||
            !roulette.SpinInProgress)
        {
            return false;
        }


        /*
         * SpinInProgress deliberately remains true through the COMPLETE valid
         * spin-resolution pass, including sticker effects. This prevents the
         * previous spin method from influencing targeting later in Rewards or
         * other non-spin flows merely because CurrentSpinMethod remembers the
         * last launch method.
         */
        spinMethod =
            roulette.CurrentSpinMethod;


        return
            spinMethod ==
                RouletteController.SpinMethod.Power ||
            spinMethod ==
                RouletteController.SpinMethod.LuckyShot;
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


        List<BaseEnemy> ordered =
            new List<BaseEnemy>();


        foreach (BaseEnemy enemy in enemies)
        {
            if (IsLivingCombatEnemy(
                    enemy))
            {
                ordered.Add(
                    enemy
                );
            }
        }


        ordered.Sort(
            (a, b) =>
                a.transform.position.x
                    .CompareTo(
                        b.transform.position.x
                    )
        );


        foreach (BaseEnemy enemy in ordered)
        {
            if (IsSpinProtectedSingleTarget(
                    enemy))
            {
                LogUntouchablePassThrough(
                    enemy
                );

                continue;
            }


            return enemy;
        }


        return null;
    }


    private BaseEnemy GetRightmostAliveEnemyInRow(
        Transform row)
    {
        if (row == null)
            return null;


        BaseEnemy[] enemies =
            row.GetComponentsInChildren<BaseEnemy>(
                true
            );


        List<BaseEnemy> ordered =
            new List<BaseEnemy>();


        foreach (BaseEnemy enemy in enemies)
        {
            if (IsLivingCombatEnemy(
                    enemy))
            {
                ordered.Add(
                    enemy
                );
            }
        }


        ordered.Sort(
            (a, b) =>
                b.transform.position.x
                    .CompareTo(
                        a.transform.position.x
                    )
        );


        foreach (BaseEnemy enemy in ordered)
        {
            if (IsSpinProtectedSingleTarget(
                    enemy))
            {
                LogUntouchablePassThrough(
                    enemy
                );

                continue;
            }


            return enemy;
        }


        return null;
    }
}
