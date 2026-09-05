using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StickerStone",
    menuName = "Stickers/Sticker Stone"
)]
public class StickerStone : StickerEffect
{
    // =========================================================
    // CONFIGURATION
    // =========================================================

    [Header("Stone")]

    [Tooltip(
        "Damage dealt to one random living enemy in CurrentRow " +
        "when Stone lands on the winning segment."
    )]
    [Min(0)]
    public int damageAmount =
        3;


    // =========================================================
    // EFFECT
    // =========================================================

    public override void ApplyEffect(
        BaseSticker owner)
    {
        if (RoundManager.Instance != null &&
            !RoundManager.Instance.WasLastSpinValid)
        {
            Debug.Log(
                "StickerStone did not activate because the spin was invalid."
            );

            return;
        }


        EnemyPanelManager enemyPanel =
            Object.FindObjectOfType<EnemyPanelManager>();


        if (enemyPanel == null)
        {
            Debug.LogWarning(
                "StickerStone: EnemyPanelManager was not found."
            );

            return;
        }


        BaseEnemy[] targets =
            enemyPanel.GetAllAliveEnemies();


        if (targets == null ||
            targets.Length <= 0)
        {
            RegisterActivation(
                owner,
                "No valid target"
            );


            Debug.Log(
                "StickerStone: No living enemy in CurrentRow."
            );

            return;
        }


        BaseEnemy target =
            targets[
                Random.Range(
                    0,
                    targets.Length
                )
            ];


        if (target == null)
        {
            RegisterActivation(
                owner,
                "No valid target"
            );

            return;
        }


        int damage =
            Mathf.Max(
                0,
                damageAmount
            );


        string targetName =
            target.enemyName;


        if (GameLogManager.Instance != null)
        {
            targetName =
                GameLogManager.Instance
                    .EnemyText(
                        target.enemyName
                    );
        }


        RegisterActivation(
            owner,
            $"Deal {damage} damage to {targetName}"
        );


        if (damage > 0)
        {
            target.TakeDamage(
                damage
            );
        }


        Debug.Log(
            $"[STONE] Deals {damage} damage to random target " +
            $"'{target.enemyName}'."
        );
    }


    // =========================================================
    // TOOLTIP
    // =========================================================

    /*
     * Recommended:
     *
     * Winning Segment Tooltip:
     * Deal {damage} damage to a random enemy.
     *
     * Losing Segment Tooltip:
     * [EMPTY]
     *
     * Album Tooltip:
     * [EMPTY]
     */
    protected override string ResolveTooltipTokens(
        BaseSticker owner,
        StickerSpinLocation location,
        string template)
    {
        string resolved =
            base.ResolveTooltipTokens(
                owner,
                location,
                template
            );


        return
            resolved.Replace(
                "{damage}",
                Mathf.Max(
                    0,
                    damageAmount
                )
                .ToString()
            );
    }
}
