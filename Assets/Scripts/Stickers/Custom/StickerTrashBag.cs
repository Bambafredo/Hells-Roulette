using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StickerTrashBag",
    menuName = "Stickers/Sticker Trash Bag"
)]
public class StickerTrashBag : StickerEffect
{
    // =========================================================
    // CONFIGURATION
    // =========================================================

    [Header("Trash Bag")]

    [Tooltip(
        "Starting X value for a fresh physical Trash Bag. " +
        "Each physical copy keeps its own X."
    )]
    [Min(0)]
    public int startingValue = 1;

    [Tooltip(
        "Amount added to this physical Trash Bag's X after every " +
        "winning activation."
    )]
    [Min(0)]
    public int valueIncrease = 1;

    [Tooltip(
        "Percentage by which the physical sticker grows after every " +
        "winning activation. Example: 20 means current scale x 1.20."
    )]
    [Min(0f)]
    public float sizeGrowthPercent = 20f;


    private const string CurrentValueKey =
        "TrashBag.CurrentValue";

    private const string InitializedKey =
        "TrashBag.Initialized";


    // =========================================================
    // LOCATION-AWARE RESOLUTION
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

        if (owner == null)
            return;

        EnsureInitialized(owner);

        switch (location)
        {
            case StickerSpinLocation.WinningSegment:
                ResolveWinning(owner);
                break;

            case StickerSpinLocation.NonWinningSegment:
                ResolveLosing(owner);
                break;

            case StickerSpinLocation.Album:
                ResolveAlbum(owner);
                break;
        }
    }


    // =========================================================
    // WIN
    // =========================================================

    private void ResolveWinning(
        BaseSticker owner)
    {
        int valueBefore =
            GetCurrentValue(owner);

        BaseSticker sacrifice =
            GetRandomOtherStickerInSegment(owner);

        bool destroyed =
            sacrifice != null &&
            sacrifice.DestroyFromGameplay(
                "destroyed by Trash Bag"
            );

        int valueAfter =
            valueBefore;

        /*
         * Trash Bag only progresses if it actually manages to destroy
         * another sticker. Winning while alone in the segment does not
         * increase X and does not grow the physical sticker.
         */
        if (destroyed)
        {
            valueAfter =
                valueBefore +
                Mathf.Max(0, valueIncrease);

            owner.SetRuntimeInt(
                CurrentValueKey,
                valueAfter
            );

            GrowPhysicalSticker(owner);
        }

        string destroyedName =
            destroyed && sacrifice.effect != null
                ? sacrifice.effect.stickerName
                : destroyed
                    ? sacrifice.name
                    : "no sticker";

        RegisterActivation(
            owner,
            StickerSpinLocation.WinningSegment,
            destroyed
                ? $"Destroyed {destroyedName}. X increased to {valueAfter}"
                : "No sticker to destroy. X unchanged",
            0,
            null
        );

        /*
         * Only a successful destruction can grow Trash Bag, so placement
         * needs to be revalidated only in that case.
         */
        if (destroyed &&
            !owner.IsPendingGameplayDestruction &&
            (!owner.HasLimitedUses ||
             owner.RemainingUses > 0))
        {
            Physics2D.SyncTransforms();

            if (StickerPlacementValidator.Instance != null)
            {
                StickerPlacementValidator.Instance
                    .NotifyStickerDropped(owner);
            }
        }
    }

    // =========================================================
    // LOSE
    // =========================================================

    private void ResolveLosing(
        BaseSticker owner)
    {
        int requestedDamage =
            GetCurrentValue(owner);

        string damageDescription =
            DealRandomlyDistributedDamage(
                requestedDamage
            );

        RegisterActivation(
            owner,
            StickerSpinLocation.NonWinningSegment,
            damageDescription,
            0,
            null
        );
    }


    /*
     * Each point of X is assigned independently to a random valid living
     * single-target enemy. Damage is accumulated per enemy so the Game Log
     * reports exactly who was hit and for how much.
     */
    private string DealRandomlyDistributedDamage(
        int amount)
    {
        int remaining =
            Mathf.Max(0, amount);

        EnemyPanelManager enemyPanel =
            Object.FindObjectOfType<EnemyPanelManager>();

        if (enemyPanel == null)
            return "No valid target";

        Dictionary<BaseEnemy, int> damageByEnemy =
            new Dictionary<BaseEnemy, int>();

        while (remaining > 0)
        {
            BaseEnemy[] targets =
                enemyPanel
                    .GetAllSingleTargetEligibleEnemies();

            if (targets == null ||
                targets.Length <= 0)
            {
                break;
            }

            BaseEnemy target =
                targets[
                    Random.Range(
                        0,
                        targets.Length
                    )
                ];

            if (target == null ||
                target.IsDead)
            {
                continue;
            }

            int hpBefore =
                Mathf.Max(
                    0,
                    target.CurrentHP
                );

            target.TakeDamage(1);

            int hpAfter =
                Mathf.Max(
                    0,
                    target.CurrentHP
                );

            int actualDamage =
                Mathf.Max(
                    0,
                    hpBefore - hpAfter
                );

            if (actualDamage > 0)
            {
                if (!damageByEnemy.ContainsKey(target))
                {
                    damageByEnemy[target] = 0;
                }

                damageByEnemy[target] +=
                    actualDamage;
            }

            remaining--;
        }

        if (damageByEnemy.Count <= 0)
            return "No valid target";

        List<string> parts =
            new List<string>();

        foreach (KeyValuePair<BaseEnemy, int> entry
                 in damageByEnemy)
        {
            string enemyName =
                entry.Key != null
                    ? entry.Key.enemyName
                    : "Enemy";

            if (GameLogManager.Instance != null)
            {
                enemyName =
                    GameLogManager.Instance
                        .EnemyText(enemyName);
            }

            parts.Add(
                $"{entry.Value} damage to {enemyName}"
            );
        }

        return
            "Deal " +
            string.Join(
                ", ",
                parts
            );
    }

    // =========================================================
    // ALBUM
    // =========================================================

    private void ResolveAlbum(
        BaseSticker owner)
    {
        int bloodCost =
            GetCurrentValue(owner);

        RegisterActivation(
            owner,
            StickerSpinLocation.Album,
            $"Lose {bloodCost} Blood",
            0,
            null
        );

        if (BloodManager.Instance != null &&
            bloodCost > 0)
        {
            BloodManager.Instance
                .ConsumeBlood(
                    bloodCost
                );
        }
    }


    // =========================================================
    // DESTRUCTION
    // =========================================================

    private BaseSticker GetRandomOtherStickerInSegment(
        BaseSticker owner)
    {
        if (owner == null ||
            owner.currentSegment == null)
        {
            return null;
        }

        BaseSticker[] source =
            owner.currentSegment
                .GetComponentsInChildren<BaseSticker>(
                    true
                );

        if (source == null ||
            source.Length <= 0)
        {
            return null;
        }

        List<BaseSticker> candidates =
            new List<BaseSticker>();

        foreach (BaseSticker candidate in source)
        {
            if (candidate == null ||
                candidate == owner ||
                candidate.IsConsumed ||
                candidate.IsPendingGameplayDestruction ||
                !candidate.isPlaced ||
                candidate.currentSegment !=
                    owner.currentSegment)
            {
                continue;
            }

            candidates.Add(candidate);
        }

        if (candidates.Count <= 0)
            return null;

        return
            candidates[
                Random.Range(
                    0,
                    candidates.Count
                )
            ];
    }


    // =========================================================
    // GROWTH
    // =========================================================

    private void GrowPhysicalSticker(
        BaseSticker owner)
    {
        Transform root =
            owner != null && owner.stickerRoot != null
                ? owner.stickerRoot
                : owner != null
                    ? owner.transform
                    : null;

        if (root == null)
            return;

        float multiplier =
            1f +
            Mathf.Max(
                0f,
                sizeGrowthPercent
            ) / 100f;

        root.localScale *=
            multiplier;
    }


    // =========================================================
    // RUNTIME VALUE
    // =========================================================

    private void EnsureInitialized(
        BaseSticker owner)
    {
        if (owner.GetRuntimeInt(
                InitializedKey,
                0
            ) != 0)
        {
            return;
        }

        owner.SetRuntimeInt(
            CurrentValueKey,
            Mathf.Max(
                0,
                startingValue
            )
        );

        owner.SetRuntimeInt(
            InitializedKey,
            1
        );
    }


    private int GetCurrentValue(
        BaseSticker owner)
    {
        EnsureInitialized(owner);

        return
            Mathf.Max(
                0,
                owner.GetRuntimeInt(
                    CurrentValueKey,
                    startingValue
                )
            );
    }


    // =========================================================
    // TOOLTIP
    // =========================================================

    /*
     * Recommended:
     *
     * W:
     * Destroy a random sticker in this segment. Increase X by {increase}
     * and grow by {growth}%. Current X: {x}.
     *
     * L:
     * Deal {x} damage randomly distributed among enemies.
     *
     * AL:
     * Lose {x} Blood.
     */
    protected override bool SupportsTooltipLocation(
        StickerSpinLocation location)
    {
        return
            location ==
                StickerSpinLocation.WinningSegment ||
            location ==
                StickerSpinLocation.NonWinningSegment ||
            location ==
                StickerSpinLocation.Album;
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

        int currentValue =
            owner != null
                ? GetCurrentValue(owner)
                : Mathf.Max(0, startingValue);

        return
            resolved
                .Replace(
                    "{x}",
                    currentValue.ToString()
                )
                .Replace(
                    "{increase}",
                    Mathf.Max(
                        0,
                        valueIncrease
                    ).ToString()
                )
                .Replace(
                    "{growth}",
                    Mathf.Max(
                        0f,
                        sizeGrowthPercent
                    ).ToString("0.##")
                );
    }
}
