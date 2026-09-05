using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StickerDealersShotgun",
    menuName = "Stickers/Sticker Dealers Shotgun"
)]
public class StickerDealersShotgun : StickerEffect
{
    // =========================================================
    // CONFIGURATION
    // =========================================================

    [Header("Dealer's Shotgun")]

    [Tooltip(
        "Chance, on a normal winning spin, that the shotgun hits the " +
        "leftmost living enemy. The remaining percentage is the chance " +
        "that the shot backfires and damages the player instead. " +
        "Lucky Shot always forces the enemy-hit result."
    )]
    [Range(0, 100)]
    public int enemyHitChancePercent =
        50;


    [Tooltip(
        "Damage dealt to the enemy when the favorable outcome is rolled."
    )]
    [Min(0)]
    public int enemyDamageAmount =
        5;


    [Tooltip(
        "Damage dealt to the player when the shotgun backfires. " +
        "This is real damage and therefore goes through Shield / damage protection."
    )]
    [Min(0)]
    public int playerDamageAmount =
        5;


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
                "StickerDealersShotgun did not activate because the spin was invalid."
            );

            return;
        }


        bool luckyShotUsed =
            RouletteController.Instance != null &&
            RouletteController.Instance.CurrentSpinMethod ==
                RouletteController.SpinMethod.LuckyShot;


        int enemyChance =
            Mathf.Clamp(
                enemyHitChancePercent,
                0,
                100
            );


        bool hitEnemy =
            luckyShotUsed ||
            Random.Range(
                0,
                100
            ) < enemyChance;


        if (hitEnemy)
        {
            ResolveEnemyShot(
                owner,
                luckyShotUsed
            );

            return;
        }


        ResolveBackfire(
            owner,
            enemyChance
        );
    }


    // =========================================================
    // ENEMY OUTCOME
    // =========================================================

    private void ResolveEnemyShot(
        BaseSticker owner,
        bool luckyShotUsed)
    {
        EnemyPanelManager enemyPanel =
            Object.FindObjectOfType<EnemyPanelManager>();


        if (enemyPanel == null)
        {
            RegisterActivation(
                owner,
                luckyShotUsed
                    ? "Lucky Shot guarantees an enemy hit, but there is no valid target"
                    : "Enemy shot succeeded, but there is no valid target"
            );


            Debug.LogWarning(
                "StickerDealersShotgun: EnemyPanelManager was not found."
            );

            return;
        }


        BaseEnemy target =
            enemyPanel.GetLeftmostAliveEnemy();


        if (target == null)
        {
            RegisterActivation(
                owner,
                luckyShotUsed
                    ? "Lucky Shot guarantees an enemy hit, but there is no valid target"
                    : "Enemy shot succeeded, but there is no valid target"
            );


            Debug.Log(
                "[DEALER'S SHOTGUN] Enemy outcome rolled, but no living enemy exists."
            );

            return;
        }


        int damage =
            Mathf.Max(
                0,
                enemyDamageAmount
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


        string description =
            luckyShotUsed
                ? $"Lucky Shot guarantees the hit: deal {damage} damage to {targetName}"
                : $"Shot hits the enemy: deal {damage} damage to {targetName}";


        RegisterActivation(
            owner,
            StickerSpinLocation.WinningSegment,
            description,
            0,
            null
        );


        if (damage > 0)
        {
            target.TakeDamage(
                damage
            );
        }


        Debug.Log(
            luckyShotUsed
                ? $"[DEALER'S SHOTGUN] Lucky Shot: guaranteed {damage} damage to {target.enemyName}."
                : $"[DEALER'S SHOTGUN] Enemy outcome: {damage} damage to {target.enemyName}."
        );
    }


    // =========================================================
    // BACKFIRE OUTCOME
    // =========================================================

    private void ResolveBackfire(
        BaseSticker owner,
        int enemyChance)
    {
        int selfChance =
            100 - enemyChance;


        int damage =
            Mathf.Max(
                0,
                playerDamageAmount
            );


        string damageText =
            $"take {damage} damage";


        if (GameLogManager.Instance != null)
        {
            damageText =
                "take " +
                GameLogManager.Instance
                    .BloodText(
                        $"-{damage} Blood"
                    );
        }


        RegisterActivation(
            owner,
            StickerSpinLocation.WinningSegment,
            $"Shot backfires ({selfChance}%): {damageText}",
            0,
            null
        );


        if (BloodManager.Instance == null)
        {
            Debug.LogWarning(
                "StickerDealersShotgun: BloodManager was not found."
            );

            return;
        }


        BloodManager.DamageResult damageResult =
            damage > 0
                ? BloodManager.Instance
                    .TakeDamage(
                        damage
                    )
                : new BloodManager.DamageResult(
                    0,
                    0,
                    0
                );


        /*
         * Keep reactive Shield / mitigation feedback immediately below the
         * shotgun activation line, just like the other sticker damage sources.
         */
        BloodManager.Instance
            .FlushDeferredDamageFeedback();


        Debug.Log(
            $"[DEALER'S SHOTGUN] Backfire ({selfChance}%): attempted {damage} damage. " +
            $"Prevented = {damageResult.preventedDamage}, " +
            $"Blood lost = {damageResult.bloodLost}."
        );
    }


    // =========================================================
    // TOOLTIP
    // =========================================================

    /*
     * Recommended Winning Segment Tooltip:
     *
     * {enemyChance}%: deal {enemyDamage} damage to the leftmost enemy.
     * {selfChance}%: take {playerDamage} damage.
     * Lucky Shot: always hit the enemy.
     *
     * Custom tokens:
     * {enemyChance}
     * {selfChance}
     * {enemyDamage}
     * {playerDamage}
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


        int enemyChance =
            Mathf.Clamp(
                enemyHitChancePercent,
                0,
                100
            );


        int selfChance =
            100 - enemyChance;


        return
            resolved
                .Replace(
                    "{enemyChance}",
                    enemyChance.ToString()
                )
                .Replace(
                    "{selfChance}",
                    selfChance.ToString()
                )
                .Replace(
                    "{enemyDamage}",
                    Mathf.Max(
                        0,
                        enemyDamageAmount
                    )
                    .ToString()
                )
                .Replace(
                    "{playerDamage}",
                    Mathf.Max(
                        0,
                        playerDamageAmount
                    )
                    .ToString()
                );
    }
}
