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
        "Maximum dollars Limbo takes after unlocking all segments. " +
        "If the player has less, Limbo takes everything and leaves $0."
    )]
    [Min(0)]
    public int dollarsToCollect =
        5;


    // =========================================================
    // TOOLTIP
    // =========================================================

    public override string GetTooltipDescription(
        BaseEnemy enemy)
    {
        string authored =
            base.GetTooltipDescription(
                enemy
            );


        if (!string.IsNullOrWhiteSpace(
                authored))
        {
            return
                authored.Replace(
                    "{money}",
                    Mathf.Max(
                        0,
                        dollarsToCollect
                    )
                    .ToString()
                );
        }


        return
            $"Unlocks all segments and takes up to " +
            $"${Mathf.Max(0, dollarsToCollect)}.";
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
            dollarsToCollect
        );
    }
}
