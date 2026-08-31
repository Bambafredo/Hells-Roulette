using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StickerRatPoison",
    menuName = "Stickers/Sticker Rat Poison"
)]
public class StickerRatPoison : StickerEffect
{
    // =========================================================
    // CONFIG
    // =========================================================

    [Header("Rat Poison")]

    [Tooltip(
        "Damage dealt to the player when Rat Poison lands on the winning segment."
    )]
    [Min(0)]
    public int playerDamageAmount =
        3;


    [Tooltip(
        "Damage dealt to every living enemy in CurrentRow when Rat Poison " +
        "is on a non-winning segment."
    )]
    [Min(0)]
    public int enemyDamageAmount =
        3;


    // =========================================================
    // LOCATION-AWARE EFFECT
    // =========================================================

    public override void ResolveSpinLocation(
        BaseSticker owner,
        StickerSpinLocation location)
    {
        if (RoundManager.Instance != null &&
            !RoundManager.Instance.WasLastSpinValid)
        {
            return;
        }


        switch (location)
        {
            case StickerSpinLocation.WinningSegment:

                ResolveWinningSegment(
                    owner
                );

                break;


            case StickerSpinLocation.NonWinningSegment:

                ResolveNonWinningSegment(
                    owner
                );

                break;


            case StickerSpinLocation.Album:

                /*
                 * Rat Poison currently has no Album effect.
                 */
                break;
        }
    }


    // =========================================================
    // WINNING SEGMENT
    // =========================================================

    private void ResolveWinningSegment(
        BaseSticker owner)
    {
        if (BloodManager.Instance == null)
        {
            Debug.LogWarning(
                "StickerRatPoison: BloodManager was not found."
            );

            return;
        }


        int damage =
            Mathf.Max(
                0,
                playerDamageAmount
            );


        /*
         * Calculate the actual Blood loss beforehand so the log remains
         * truthful if the player has less Blood than the configured damage.
         */
        int actualDamage =
            Mathf.Min(
                damage,
                Mathf.Max(
                    0,
                    BloodManager.Instance.currentBlood
                )
            );


        string description =
            BuildPlayerDamageLogDescription(
                actualDamage
            );


        RegisterActivation(
            owner,
            StickerSpinLocation.WinningSegment,
            description,
            0,
            null
        );


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
         * Rat Poison's activation line was written just before TakeDamage().
         * Put any Shield response immediately underneath it.
         */
        BloodManager.Instance
            .FlushDeferredDamageFeedback();


        Debug.Log(
            $"[RAT POISON] Winning segment: attempted {damage} damage. " +
            $"Prevented = {damageResult.preventedDamage}, " +
            $"Blood lost = {damageResult.bloodLost}."
        );
    }


    // =========================================================
    // NON-WINNING SEGMENT
    // =========================================================

    private void ResolveNonWinningSegment(
        BaseSticker owner)
    {
        List<BaseEnemy> targets =
            GetLivingCurrentRowEnemies();


        string description =
            BuildEnemyDamageLogDescription(
                targets.Count
            );


        RegisterActivation(
            owner,
            StickerSpinLocation.NonWinningSegment,
            description,
            0,
            null
        );


        int damage =
            Mathf.Max(
                0,
                enemyDamageAmount
            );


        if (damage <= 0)
            return;


        foreach (BaseEnemy enemy in targets)
        {
            if (enemy == null ||
                enemy.IsDead)
            {
                continue;
            }


            enemy.TakeDamage(
                damage
            );
        }


        Debug.Log(
            $"[RAT POISON] Losing segment: dealt {damage} damage " +
            $"to {targets.Count} living enemy/enemies."
        );
    }


    // =========================================================
    // TARGETING
    // =========================================================

    private List<BaseEnemy> GetLivingCurrentRowEnemies()
    {
        List<BaseEnemy> targets =
            new List<BaseEnemy>();


        EnemyCorridorController corridor =
            Object.FindObjectOfType<EnemyCorridorController>();


        if (corridor == null ||
            corridor.CurrentRow == null)
        {
            return targets;
        }


        BaseEnemy[] enemies =
            corridor.CurrentRow
                .GetComponentsInChildren<BaseEnemy>(
                    true
                );


        foreach (BaseEnemy enemy in enemies)
        {
            if (enemy == null ||
                enemy.IsDead ||
                !enemy.CombatActive ||
                !enemy.gameObject.activeInHierarchy)
            {
                continue;
            }


            targets.Add(
                enemy
            );
        }


        return targets;
    }


    // =========================================================
    // GAME LOG
    // =========================================================

    private string BuildPlayerDamageLogDescription(
        int actualDamage)
    {
        if (GameLogManager.Instance != null)
        {
            return
                "Take " +
                GameLogManager.Instance
                    .BloodText(
                        $"-{actualDamage} Blood"
                    );
        }


        return
            $"Take {actualDamage} damage";
    }


    private string BuildEnemyDamageLogDescription(
        int targetCount)
    {
        if (targetCount <= 0)
        {
            return
                "No living enemies to damage";
        }


        return
            $"Deal {Mathf.Max(0, enemyDamageAmount)} damage to all enemies";
    }


    // =========================================================
    // TOOLTIP
    // =========================================================

    protected override bool SupportsTooltipLocation(
        StickerSpinLocation location)
    {
        return
            location ==
                StickerSpinLocation.WinningSegment ||
            location ==
                StickerSpinLocation.NonWinningSegment;
    }


    /*
     * Available custom tokens:
     *
     * {playerDamage}
     * {enemyDamage}
     *
     * Recommended authoring:
     *
     * Winning Segment Tooltip:
     * Take {playerDamage} damage
     *
     * Losing Segment Tooltip:
     * Deal {enemyDamage} damage to all enemies
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


        resolved =
            resolved.Replace(
                "{playerDamage}",
                Mathf.Max(
                    0,
                    playerDamageAmount
                ).ToString()
            );


        resolved =
            resolved.Replace(
                "{enemyDamage}",
                Mathf.Max(
                    0,
                    enemyDamageAmount
                ).ToString()
            );


        return
            resolved;
    }
}
