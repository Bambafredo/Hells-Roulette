using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "EnemyCurse_PowerSpinCost",
    menuName = "Hell's Roulette/Enemy Curses/Power Spin Cost"
)]
public class EnemyCursePowerSpinCost : EnemyCurse
{
    // =========================================================
    // CONFIG
    // =========================================================

    [Header("Power Spin Cost")]

    [Tooltip(
        "Additional Blood added to the effective Power Spin cost while " +
        "this enemy is alive in CurrentRow."
    )]
    [Min(0)]
    public int additionalBloodCost =
        0;


    [Tooltip(
        "Additional Coin added to the effective Power Spin cost while " +
        "this enemy is alive in CurrentRow."
    )]
    [Min(0)]
    public int additionalCoinCost =
        0;


    // =========================================================
    // LIFECYCLE
    // =========================================================

    public override void Activate(
        BaseEnemy enemy,
        int value)
    {
        if (enemy == null)
            return;


        PowerSpinController powerSpin =
            PowerSpinController.Instance;


        if (powerSpin == null)
        {
            powerSpin =
                Object.FindObjectOfType<PowerSpinController>();
        }


        if (powerSpin == null)
        {
            Debug.LogWarning(
                "[ENEMY CURSE] Power Spin Cost could not activate because " +
                "PowerSpinController was not found."
            );

            return;
        }


        powerSpin.RegisterCostModifier(
            enemy,
            Mathf.Max(
                0,
                additionalBloodCost
            ),
            Mathf.Max(
                0,
                additionalCoinCost
            )
        );
    }


    public override void Deactivate(
        BaseEnemy enemy,
        int value)
    {
        if (enemy == null)
            return;


        PowerSpinController powerSpin =
            PowerSpinController.Instance;


        if (powerSpin == null)
        {
            powerSpin =
                Object.FindObjectOfType<PowerSpinController>();
        }


        if (powerSpin == null)
            return;


        /*
         * The modifier is keyed by the physical enemy instance.
         *
         * Therefore:
         * - several enemies with this Curse stack additively;
         * - killing one removes only its own contribution;
         * - the authored Base Power Spin cost is never touched.
         */
        powerSpin.UnregisterCostModifier(
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


        int blood =
            Mathf.Max(
                0,
                additionalBloodCost
            );

        int coin =
            Mathf.Max(
                0,
                additionalCoinCost
            );


        if (!string.IsNullOrWhiteSpace(
            authored))
        {
            return
                authored
                    .Replace(
                        "{blood}",
                        blood.ToString()
                    )
                    .Replace(
                        "{coin}",
                        coin.ToString()
                    );
        }


        if (blood > 0 &&
            coin > 0)
        {
            return
                $"Power Spins cost {blood} more Blood and " +
                $"${coin} more while this enemy is alive.";
        }


        if (blood > 0)
        {
            return
                $"Power Spins cost {blood} more Blood " +
                $"while this enemy is alive.";
        }


        if (coin > 0)
        {
            return
                $"Power Spins cost ${coin} more " +
                $"while this enemy is alive.";
        }


        return
            "This enemy currently adds no Power Spin cost.";
    }


    // =========================================================
    // EDITOR SAFETY
    // =========================================================

    private void OnValidate()
    {
        additionalBloodCost =
            Mathf.Max(
                0,
                additionalBloodCost
            );

        additionalCoinCost =
            Mathf.Max(
                0,
                additionalCoinCost
            );
    }
}
