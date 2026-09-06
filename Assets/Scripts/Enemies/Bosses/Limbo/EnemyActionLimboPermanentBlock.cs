using UnityEngine;

[CreateAssetMenu(
    fileName = "EnemyAction_LimboPermanentBlock",
    menuName = "Hell's Roulette/Enemy Actions/Bosses/Limbo/Permanent Block"
)]
public class EnemyActionLimboPermanentBlock : EnemyAction
{
    // =========================================================
    // CONFIGURATION
    // =========================================================

    [Header("Limbo Permanent Block")]

    [Tooltip(
        "What happens when the player later lands on the segment blocked by " +
        "this Enemy Action. The visual procedural pattern is selected from " +
        "LimboBossController according to this type."
    )]
    public LimboBossController.LimboBlockEffectType blockType =
        LimboBossController.LimboBlockEffectType.DivideMoney;


    [Tooltip(
        "Numeric value of this block. " +
        "Damage Player = Blood damage; " +
        "Divide Money = divisor; " +
        "Damage Boss = damage dealt to Limbo."
    )]
    [Min(0)]
    public int effectValue =
        2;


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


        string authored =
            base.GetTooltipDescription(
                enemy
            );


        string effectText =
            limbo != null
                ? limbo.GetBlockEffectDescription(
                    blockType,
                    effectValue,
                    sentenceCase: true
                )
                : BuildFallbackEffectDescription();


        string targetText =
            "Target: unavailable.";


        if (limbo != null &&
            limbo.TryGetNextPermanentBlockTarget(
                out int segmentIndex))
        {
            targetText =
                $"Target: Segment {segmentIndex + 1}.";
        }


        string dynamicText =
            targetText +
            " " +
            effectText +
            ".";


        if (!string.IsNullOrWhiteSpace(
                authored))
        {
            return
                authored +
                "\n" +
                dynamicText;
        }


        return
            "Permanently blocks a segment.\n" +
            dynamicText;
    }


    private string BuildFallbackEffectDescription()
    {
        int safeValue =
            blockType ==
                LimboBossController.LimboBlockEffectType.DivideMoney
                ? Mathf.Max(
                    1,
                    effectValue
                )
                : Mathf.Max(
                    0,
                    effectValue
                );


        switch (blockType)
        {
            case LimboBossController.LimboBlockEffectType.DamagePlayer:
                return
                    $"Landing here deals {safeValue} Blood damage";

            case LimboBossController.LimboBlockEffectType.DivideMoney:
                return
                    $"Landing here divides current money by {safeValue}";

            case LimboBossController.LimboBlockEffectType.DamageBoss:
                return
                    $"Landing here deals {safeValue} damage to Limbo";

            default:
                return
                    "Landing here triggers a special effect";
        }
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
         * Resolve the special block landed on this spin first. A Damage Boss
         * block may kill Limbo; in that case this new Permanent Block is skipped.
         */
        limbo.ResolvePendingBlockEffect();


        if (enemy.IsDead)
            return;


        limbo.ExecutePermanentBlock(
            blockType,
            effectValue
        );
    }
}
