using UnityEngine;

[CreateAssetMenu(
    fileName = "EnemyAction_LimboCollectUnlock",
    menuName = "Hell's Roulette/Enemy Actions/Bosses/Limbo/Collect + Unlock"
)]
public class EnemyActionLimboCollectUnlock : EnemyAction
{
    // =========================================================
    // CONFIGURATION
    // =========================================================

    [Header("Collect + Unlock")]

    [Tooltip(
        "Base dollars Limbo takes after unlocking all segments. " +
        "If the player has less, Limbo takes everything and leaves $0."
    )]
    [Min(0)]
    public int dollarsToCollect =
        5;


    [Header("Optional Escalation")]

    [Tooltip(
        "If enabled, this Collect action becomes more expensive every time " +
        "it successfully executes during the current Limbo encounter."
    )]
    public bool increaseCollectValueAfterUse =
        false;


    [Tooltip(
        "Amount added to the NEXT Collect value after each use. " +
        "Example: base $20 and increase $10 -> $20, $30, $40..."
    )]
    [Min(0)]
    public int collectIncreasePerUse =
        10;


    // =========================================================
    // TOOLTIP
    // =========================================================

    public override string GetTooltipDescription(
        BaseEnemy enemy)
    {
        LimboBossController limbo =
            enemy != null
                ? enemy.GetComponent<LimboBossController>()
                : null;


        int currentCollect =
            limbo != null
                ? limbo.GetCurrentCollectValue(
                    this,
                    dollarsToCollect,
                    increaseCollectValueAfterUse,
                    collectIncreasePerUse
                )
                : Mathf.Max(
                    0,
                    dollarsToCollect
                );


        int safeIncrease =
            Mathf.Max(
                0,
                collectIncreasePerUse
            );

        int nextCollect =
            currentCollect;

        if (increaseCollectValueAfterUse &&
            safeIncrease > 0)
        {
            long calculated =
                (long)currentCollect +
                safeIncrease;

            nextCollect =
                calculated >= int.MaxValue
                    ? int.MaxValue
                    : (int)calculated;
        }


        string authored =
            base.GetTooltipDescription(
                enemy
            );

        string mainText;


        if (!string.IsNullOrWhiteSpace(
                authored))
        {
            mainText =
                authored
                    .Replace(
                        "{money}",
                        currentCollect.ToString()
                    )
                    .Replace(
                        "{increase}",
                        safeIncrease.ToString()
                    )
                    .Replace(
                        "{nextMoney}",
                        nextCollect.ToString()
                    );
        }
        else
        {
            mainText =
                $"Unlocks all segments and takes up to " +
                $"${currentCollect}.";
        }


        if (increaseCollectValueAfterUse &&
            safeIncrease > 0)
        {
            return
                mainText +
                "\nAfter use, the next Collect value increases by " +
                $"${safeIncrease} to ${nextCollect}.";
        }


        return
            mainText;
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


        LimboBossController limbo =
            enemy.GetComponent<LimboBossController>();


        if (limbo == null)
        {
            Debug.LogWarning(
                "[LIMBO] Collect + Unlock action requires LimboBossController " +
                "on the same GameObject as BaseEnemy."
            );

            return;
        }


        /*
         * Resolve the special block landed on this spin first. A Damage Boss
         * block may kill Limbo; in that case Collect + Unlock never executes.
         */
        limbo.ResolvePendingBlockEffect();


        if (enemy.IsDead)
            return;


        limbo.ExecuteCollectUnlock(
            this,
            dollarsToCollect,
            increaseCollectValueAfterUse,
            collectIncreasePerUse
        );
    }
}
