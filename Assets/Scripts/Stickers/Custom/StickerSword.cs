using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "StickerSword", menuName = "Stickers/Sticker Sword")]
public class StickerSword : StickerEffect
{
    [Header("Damage")]
    public int damageAmount = 5;


    public override void ApplyEffect()
    {
        // -----------------------------------------------------
        // VALID SPIN
        // -----------------------------------------------------

        if (RoundManager.Instance != null &&
            !RoundManager.Instance.WasLastSpinValid)
        {
            Debug.Log(
                "StickerSword did not activate because the spin was invalid."
            );

            return;
        }


        // -----------------------------------------------------
        // ENEMY PANEL
        // -----------------------------------------------------

        EnemyPanelManager enemyPanel =
            Object.FindObjectOfType<EnemyPanelManager>();


        if (enemyPanel == null)
        {
            Debug.LogWarning(
                "StickerSword: EnemyPanelManager was not found."
            );

            return;
        }


        // -----------------------------------------------------
        // TARGET
        // -----------------------------------------------------

        BaseEnemy target =
            enemyPanel.GetLeftmostAliveEnemy();


        if (target == null)
        {
            /*
             * The sticker genuinely activated, but had no valid target.
             * This is still relevant gameplay information.
             */
            LogActivation(
                null,
                "No valid target"
            );


            Debug.Log(
                "StickerSword: No living enemy to attack."
            );

            return;
        }


        // -----------------------------------------------------
        // GAME LOG
        // -----------------------------------------------------

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


        LogActivation(
            null,
            $"Deal {damageAmount} damage to {targetName}"
        );


        // -----------------------------------------------------
        // DAMAGE
        // -----------------------------------------------------

        target.TakeDamage(
            damageAmount
        );


        Debug.Log(
            $"StickerSword deals {damageAmount} damage to {target.enemyName}."
        );
    }
}
