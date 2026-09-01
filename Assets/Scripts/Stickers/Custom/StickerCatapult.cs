using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StickerCatapult",
    menuName = "Stickers/Sticker Catapult"
)]
public class StickerCatapult : StickerEffect
{
    [Header("Catapult")]

    [Tooltip(
        "Damage dealt to the first living enemy when scanning CurrentRow " +
        "from RIGHT to LEFT."
    )]
    [Min(0)]
    public int damageAmount = 5;

    [Tooltip(
        "Because the rule says 'a random sticker in this segment', Catapult " +
        "itself is a valid sacrifice by default. Disable only if the design " +
        "changes to 'another sticker'."
    )]
    public bool canDestroySelf = true;


    // Structural destruction resolves before ordinary priority-0 stickers.
    // Coffee is -100, so Coffee explains/establishes its modifier first.
    public override int SpinResolutionPriority => -50;


    public override void ResolveSpinLocation(
        BaseSticker owner,
        StickerSpinLocation location)
    {
        if (location != StickerSpinLocation.WinningSegment)
            return;

        if (RoundManager.Instance != null &&
            !RoundManager.Instance.WasLastSpinValid)
        {
            return;
        }

        if (owner == null ||
            owner.currentSegment == null)
        {
            RegisterActivation(
                owner,
                location,
                "No valid sticker to destroy",
                0,
                null
            );
            return;
        }

        BaseSticker sacrifice =
            GetRandomStickerToDestroy(owner);

        EnemyPanelManager enemyPanel =
            Object.FindObjectOfType<EnemyPanelManager>();

        BaseEnemy targetEnemy =
            enemyPanel != null
                ? enemyPanel.GetRightmostAliveEnemy()
                : null;

        string description =
            BuildActivationDescription(
                sacrifice,
                targetEnemy
            );

        RegisterActivation(
            owner,
            location,
            description,
            0,
            null
        );

        if (sacrifice != null)
        {
            sacrifice.DestroyFromGameplay(
                "destroyed by Catapult"
            );
        }

        if (targetEnemy != null &&
            damageAmount > 0)
        {
            targetEnemy.TakeDamage(damageAmount);
        }
    }


    private BaseSticker GetRandomStickerToDestroy(
        BaseSticker owner)
    {
        if (owner == null ||
            owner.currentSegment == null)
        {
            return null;
        }

        BaseSticker[] stickers =
            owner.currentSegment
                .GetComponentsInChildren<BaseSticker>(true);

        List<BaseSticker> candidates =
            new List<BaseSticker>();

        foreach (BaseSticker candidate in stickers)
        {
            if (candidate == null ||
                candidate.IsConsumed ||
                candidate.IsPendingGameplayDestruction ||
                !candidate.isPlaced ||
                candidate.currentSegment != owner.currentSegment)
            {
                continue;
            }

            if (!canDestroySelf &&
                candidate == owner)
            {
                continue;
            }

            candidates.Add(candidate);
        }

        if (candidates.Count <= 0)
            return null;

        return candidates[
            Random.Range(0, candidates.Count)
        ];
    }


    private string BuildActivationDescription(
        BaseSticker sacrifice,
        BaseEnemy targetEnemy)
    {
        string destroyText;

        if (sacrifice != null)
        {
            string sacrificeName =
                GetStickerDisplayName(sacrifice);

            if (GameLogManager.Instance != null)
            {
                sacrificeName =
                    GameLogManager.Instance
                        .StickerText(sacrificeName);
            }

            destroyText =
                "Destroy " + sacrificeName;
        }
        else
        {
            destroyText =
                "No valid sticker to destroy";
        }

        if (targetEnemy == null)
        {
            return destroyText +
                ". No valid enemy target";
        }

        string enemyName =
            targetEnemy.EnemyName;

        if (GameLogManager.Instance != null)
        {
            enemyName =
                GameLogManager.Instance
                    .EnemyText(enemyName);
        }

        return destroyText +
            $". Deal {damageAmount} damage to " +
            enemyName;
    }


    private string GetStickerDisplayName(
        BaseSticker sticker)
    {
        if (sticker == null)
            return "Sticker";

        if (sticker.effect != null &&
            !string.IsNullOrWhiteSpace(
                sticker.effect.stickerName
            ))
        {
            return sticker.effect.stickerName;
        }

        return sticker.gameObject.name;
    }


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

        return resolved.Replace(
            "{damage}",
            Mathf.Max(0, damageAmount).ToString()
        );
    }
}
