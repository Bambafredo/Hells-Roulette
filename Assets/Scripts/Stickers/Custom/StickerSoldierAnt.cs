using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StickerSoldierAnt",
    menuName = "Stickers/Sticker Soldier Ant"
)]
public class StickerSoldierAnt : StickerEffect
{
    [Header("Soldier Ant Damage")]
    [Min(0)]
    public int baseDamage = 2;

    [Min(0)]
    public int damagePerOtherSticker = 1;


    [Header("Same-Segment Counting")]

    [Tooltip(
        "If enabled, stickers that have already been marked for gameplay destruction " +
        "during this same spin still count toward Soldier Ant's bonus until Unity " +
        "physically destroys them at end of frame. " +
        "If disabled (recommended), a sticker that resolves earlier and destroys " +
        "itself no longer contributes to Soldier Ant."
    )]
    public bool countPendingGameplayDestruction = false;


    public override void ApplyEffect(BaseSticker owner)
    {
        if (RoundManager.Instance != null &&
            !RoundManager.Instance.WasLastSpinValid)
        {
            return;
        }

        int otherStickerCount =
            CountOtherStickersInSameSegment(owner);

        int damage =
            CalculateDamage(otherStickerCount);

        EnemyPanelManager enemyPanel =
            Object.FindObjectOfType<EnemyPanelManager>();

        if (enemyPanel == null)
        {
            RegisterActivation(
                owner,
                $"No valid target ({otherStickerCount} other sticker" +
                $"{(otherStickerCount == 1 ? "" : "s")} in segment)"
            );

            Debug.LogWarning(
                "StickerSoldierAnt: EnemyPanelManager was not found."
            );

            return;
        }

        BaseEnemy target =
            enemyPanel.GetLeftmostAliveEnemy();

        if (target == null)
        {
            RegisterActivation(
                owner,
                $"No valid target ({otherStickerCount} other sticker" +
                $"{(otherStickerCount == 1 ? "" : "s")} in segment)"
            );

            return;
        }

        string targetName =
            target.enemyName;

        if (GameLogManager.Instance != null)
        {
            targetName =
                GameLogManager.Instance
                    .EnemyText(target.enemyName);
        }

        RegisterActivation(
            owner,
            $"Deal {damage} damage to {targetName} " +
            $"({otherStickerCount} other sticker" +
            $"{(otherStickerCount == 1 ? "" : "s")} in segment)"
        );

        if (damage > 0)
        {
            target.TakeDamage(damage);
        }

        Debug.Log(
            $"[SOLDIER ANT] Deals {damage} damage to '{target.enemyName}'. " +
            $"Base = {Mathf.Max(0, baseDamage)}, " +
            $"bonus per other sticker = {Mathf.Max(0, damagePerOtherSticker)}, " +
            $"other stickers in segment = {otherStickerCount}."
        );
    }


    private int CountOtherStickersInSameSegment(
        BaseSticker owner)
    {
        if (owner == null ||
            !owner.isPlaced ||
            owner.currentSegment == null)
        {
            return 0;
        }

        BaseSticker[] stickersInSegment =
            owner.currentSegment
                .GetComponentsInChildren<BaseSticker>(true);

        int count = 0;

        foreach (BaseSticker sticker in stickersInSegment)
        {
            if (sticker == null ||
                sticker == owner)
            {
                continue;
            }

            /*
             * Count only stickers that are logically placed in this exact
             * segment. Album / reward / unplaced objects do not contribute.
             */
            if (!sticker.isPlaced ||
                sticker.currentSegment != owner.currentSegment)
            {
                continue;
            }

            /*
             * Resolution order matters.
             *
             * By default, if another sticker resolved earlier this spin and
             * marked itself for gameplay destruction, Soldier Ant treats it as
             * already gone even though Unity's Object.Destroy is deferred until
             * end of frame.
             *
             * The authored toggle above exists in case a future Soldier Ant
             * variant deliberately wants snapshot-style counting instead.
             */
            if (!countPendingGameplayDestruction &&
                sticker.IsPendingGameplayDestruction)
            {
                continue;
            }

            count++;
        }

        return count;
    }


    private int CalculateDamage(
        int otherStickerCount)
    {
        int safeBaseDamage =
            Mathf.Max(0, baseDamage);

        int safeBonus =
            Mathf.Max(0, damagePerOtherSticker);

        long calculatedDamage =
            (long)safeBaseDamage +
            (long)safeBonus *
            Mathf.Max(0, otherStickerCount);

        return
            calculatedDamage > int.MaxValue
                ? int.MaxValue
                : (int)calculatedDamage;
    }


    /*
     * Recommended Winning Segment Tooltip:
     *
     * Deal {baseDamage} damage +{bonusDamage} for each other sticker
     * in this segment. Current total: {totalDamage}.
     *
     * Custom tokens:
     * {baseDamage}
     * {bonusDamage}
     * {otherStickerCount}
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

        int otherStickerCount =
            CountOtherStickersInSameSegment(owner);

        int totalDamage =
            CalculateDamage(otherStickerCount);

        return
            resolved
                .Replace(
                    "{baseDamage}",
                    Mathf.Max(0, baseDamage).ToString()
                )
                .Replace(
                    "{bonusDamage}",
                    Mathf.Max(0, damagePerOtherSticker).ToString()
                )
                .Replace(
                    "{otherStickerCount}",
                    otherStickerCount.ToString()
                )
                .Replace(
                    "{totalDamage}",
                    totalDamage.ToString()
                );
    }
}
