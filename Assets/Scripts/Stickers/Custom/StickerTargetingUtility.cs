using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class StickerTargetingUtility
{
    /// <summary>
    /// Returns the first eligible enemy for a damage effect that normally
    /// starts from the left.
    ///
    /// Scope only changes targeting while its own segment is the WINNING
    /// segment. A Scope sitting in a losing segment therefore does not alter
    /// losing-segment damage such as Zombie.
    /// </summary>
    public static BaseEnemy GetFirstDamageTarget(
        EnemyPanelManager enemyPanel,
        BaseSticker attackingSticker,
        StickerSpinLocation location)
    {
        if (enemyPanel == null)
            return null;

        bool startsFromRight =
            ShouldStartFromRight(
                attackingSticker,
                location
            );

        BaseEnemy target =
            startsFromRight
                ? enemyPanel.GetRightmostAliveEnemy()
                : enemyPanel.GetLeftmostAliveEnemy();

        /*
         * A Scope use is only meaningful if:
         * - Scope actually changed this sticker's native left-start targeting;
         * - and there is a real enemy target to attack.
         *
         * Rightmost-native stickers never call this left-start helper, so Scope
         * continues to ignore them completely.
         */
        if (startsFromRight &&
            target != null)
        {
            NotifyScopesOfAffectedAttack(
                attackingSticker
            );
        }

        return target;
    }


    private static void NotifyScopesOfAffectedAttack(
        BaseSticker attackingSticker)
    {
        if (attackingSticker == null ||
            attackingSticker.currentSegment == null)
        {
            return;
        }

        BaseSticker[] stickersInSegment =
            attackingSticker.currentSegment
                .GetComponentsInChildren<BaseSticker>(
                    true
                );

        foreach (BaseSticker sticker in stickersInSegment)
        {
            if (sticker == null ||
                sticker.IsConsumed ||
                sticker.IsPendingGameplayDestruction ||
                !sticker.isPlaced ||
                sticker.currentSegment !=
                    attackingSticker.currentSegment ||
                sticker.effect == null)
            {
                continue;
            }

            if (sticker.effect is StickerScope scope)
            {
                scope.NotifyScopeAffectedAttack(
                    sticker
                );
            }
        }
    }


    public static bool ShouldStartFromRight(
        BaseSticker attackingSticker,
        StickerSpinLocation location)
    {
        if (attackingSticker == null ||
            attackingSticker.currentSegment == null ||
            location != StickerSpinLocation.WinningSegment)
        {
            return false;
        }

        BaseSticker[] stickersInSegment =
            attackingSticker.currentSegment
                .GetComponentsInChildren<BaseSticker>(
                    true
                );

        foreach (BaseSticker sticker in stickersInSegment)
        {
            if (sticker == null ||
                sticker.IsConsumed ||
                sticker.IsPendingGameplayDestruction ||
                !sticker.isPlaced ||
                sticker.currentSegment !=
                    attackingSticker.currentSegment ||
                sticker.effect == null)
            {
                continue;
            }

            if (sticker.effect is StickerScope)
                return true;
        }

        return false;
    }
}
