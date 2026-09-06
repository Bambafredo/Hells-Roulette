using UnityEngine;

[CreateAssetMenu(
    fileName = "EnemyAction_LimboPermanentBlock",
    menuName = "Hell's Roulette/Enemy Actions/Bosses/Limbo/Permanent Block"
)]
public class EnemyActionLimboPermanentBlock : EnemyAction
{
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
            return authored;
        }


        return
            "Permanently blocks a random unblocked segment. " +
            "Limbo's permanent blocks have a special landing effect.";
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
                "[LIMBO] Permanent Block action requires LimboBossController " +
                "on the same GameObject as BaseEnemy."
            );

            return;
        }


        /*
         * The block under the winning segment resolves before Limbo performs
         * the authored Enemy Action for this turn.
         */
        limbo.ResolvePendingBlockEffect();


        if (enemy.IsDead)
            return;


        limbo.ExecutePermanentBlock();
    }
}
