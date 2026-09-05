using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StickerSlingshot",
    menuName = "Stickers/Sticker Slingshot"
)]
public class StickerSlingshot : StickerEffect
{
    // =========================================================
    // CONFIGURATION
    // =========================================================

    [Header("Slingshot")]

    [Tooltip(
        "Base damage dealt to the rightmost living enemy on a winning segment. " +
        "The same amount is added once for every Stone sticker currently " +
        "placed in the wheel."
    )]
    [Min(0)]
    public int damageAmount =
        2;


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
                "StickerSlingshot did not activate because the spin was invalid."
            );

            return;
        }


        EnemyPanelManager enemyPanel =
            Object.FindObjectOfType<EnemyPanelManager>();


        if (enemyPanel == null)
        {
            Debug.LogWarning(
                "StickerSlingshot: EnemyPanelManager was not found."
            );

            return;
        }


        BaseEnemy target =
            enemyPanel.GetRightmostAliveEnemy();


        int stoneCount =
            CountStonesInWheel();


        int baseDamage =
            Mathf.Max(
                0,
                damageAmount
            );


        long calculatedDamage =
            (long)baseDamage *
            (
                1L +
                stoneCount
            );


        int totalDamage =
            calculatedDamage >
            int.MaxValue
                ? int.MaxValue
                : (int)calculatedDamage;


        if (target == null)
        {
            RegisterActivation(
                owner,
                $"No valid target ({stoneCount} Stone" +
                $"{(stoneCount == 1 ? "" : "s")} in wheel)"
            );


            Debug.Log(
                $"[SLINGSHOT] No living rightmost enemy. " +
                $"Stone count = {stoneCount}."
            );

            return;
        }


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
            $"Deal {totalDamage} damage to {targetName} " +
            $"({stoneCount} Stone" +
            $"{(stoneCount == 1 ? "" : "s")})"
        );


        if (totalDamage > 0)
        {
            target.TakeDamage(
                totalDamage
            );
        }


        Debug.Log(
            $"[SLINGSHOT] Deals {totalDamage} damage to rightmost enemy " +
            $"'{target.enemyName}'. Base = {baseDamage}, " +
            $"Stones in wheel = {stoneCount}."
        );
    }


    // =========================================================
    // STONE COUNT
    // =========================================================

    private int CountStonesInWheel()
    {
        BaseSticker[] allStickers =
            Object.FindObjectsOfType<BaseSticker>(
                true
            );


        int count =
            0;


        foreach (BaseSticker sticker in
                 allStickers)
        {
            if (sticker == null ||
                sticker.IsPendingGameplayDestruction ||
                !sticker.isPlaced ||
                sticker.currentSegment == null ||
                sticker.effect == null)
            {
                continue;
            }


            if (sticker.effect is StickerStone)
            {
                count++;
            }
        }


        return
            count;
    }


    // =========================================================
    // TOOLTIP
    // =========================================================

    /*
     * Recommended:
     *
     * Winning Segment Tooltip:
     * Deal {damage} damage to the rightmost enemy +{damage} for every Stone
     * in the wheel. Current total: {totalDamage}.
     *
     * Losing Segment Tooltip:
     * [EMPTY]
     *
     * Album Tooltip:
     * [EMPTY]
     *
     * Custom tokens:
     * {damage}
     * {stoneCount}
     * {totalDamage}
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


        int stoneCount =
            CountStonesInWheel();


        int baseDamage =
            Mathf.Max(
                0,
                damageAmount
            );


        long calculatedDamage =
            (long)baseDamage *
            (
                1L +
                stoneCount
            );


        int totalDamage =
            calculatedDamage >
            int.MaxValue
                ? int.MaxValue
                : (int)calculatedDamage;


        return
            resolved
                .Replace(
                    "{damage}",
                    baseDamage.ToString()
                )
                .Replace(
                    "{stoneCount}",
                    stoneCount.ToString()
                )
                .Replace(
                    "{totalDamage}",
                    totalDamage.ToString()
                );
    }
}
