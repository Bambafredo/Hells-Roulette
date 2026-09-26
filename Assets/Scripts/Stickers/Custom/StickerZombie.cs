using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StickerZombie",
    menuName = "Stickers/Sticker Zombie"
)]
public class StickerZombie : StickerEffect
{
    // =========================================================
    // CONFIGURATION
    // =========================================================

    [Header("Zombie")]

    [Tooltip(
        "Damage dealt to the leftmost enemy on Winning / Losing, or blockable " +
        "damage dealt to the player while Zombie is in the Album."
    )]
    [Min(0)]
    public int damageAmount =
        1;

    [Tooltip(
        "Chance, from 0 to 100, to transmute one eligible sticker into Zombie."
    )]
    [Range(0f, 100f)]
    public float conversionChance =
        25f;

    [Tooltip(
        "Money gained when this Zombie is destroyed. The payout repeats once " +
        "for every other Zombie currently in the same roulette segment."
    )]
    [Min(0)]
    public int destroyedDollarReward =
        1;

    [Tooltip(
        "Physical Zombie sticker prefab used when another sticker is transmuted."
    )]
    public GameObject zombiePrefab;


    // =========================================================
    // SPIN RESOLUTION
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


        switch (location)
        {
            case StickerSpinLocation.WinningSegment:
            case StickerSpinLocation.NonWinningSegment:
                ResolveWheel(
                    owner,
                    location
                );
                break;


            case StickerSpinLocation.Album:
                ResolveAlbum(
                    owner
                );
                break;
        }
    }


    // =========================================================
    // WHEEL
    // =========================================================

    private void ResolveWheel(
        BaseSticker owner,
        StickerSpinLocation location)
    {
        bool luckyShot =
            RouletteController.Instance != null &&
            RouletteController.Instance.CurrentSpinMethod ==
                RouletteController.SpinMethod.LuckyShot;


        if (location == StickerSpinLocation.WinningSegment &&
            luckyShot)
        {
            RegisterActivation(
                owner,
                location,
                "Lucky Shot destroys Zombie",
                0,
                false
            );


            owner.DestroyFromGameplay(
                "Lucky Shot"
            );

            return;
        }


        DealEnemyDamage(
            owner,
            location
        );


        if (!RollConversion())
            return;


        BaseSticker target =
            PickWheelConversionTarget(
                owner
            );


        if (target == null)
            return;


        string targetName =
            GetStickerName(
                target
            );


        BaseSticker replacement =
            TransmuteTarget(
                target,
                "Zombie infection"
            );


        if (replacement == null)
            return;


        RegisterActivation(
            owner,
            location,
            $"Transmute {targetName} into Zombie",
            0,
            false
        );
    }


    private void DealEnemyDamage(
        BaseSticker owner,
        StickerSpinLocation location)
    {
        EnemyPanelManager enemyPanel =
            Object.FindObjectOfType<EnemyPanelManager>();


        BaseEnemy target =
            enemyPanel != null
                ? StickerTargetingUtility.GetFirstDamageTarget(
                    enemyPanel,
                    owner,
                    location
                )
                : null;


        if (target == null)
        {
            RegisterActivation(
                owner,
                location,
                "No valid target",
                0,
                false
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
            location,
            $"Deal {Mathf.Max(0, damageAmount)} damage to {targetName}",
            0,
            false
        );


        target.TakeDamage(
            Mathf.Max(
                0,
                damageAmount
            )
        );
    }


    // =========================================================
    // ALBUM
    // =========================================================

    private void ResolveAlbum(
        BaseSticker owner)
    {
        int safeDamage =
            Mathf.Max(
                0,
                damageAmount
            );


        RegisterActivation(
            owner,
            StickerSpinLocation.Album,
            $"Deal {safeDamage} blockable damage to you",
            0,
            false
        );


        if (BloodManager.Instance != null &&
            safeDamage > 0)
        {
            BloodManager.Instance
                .TakeDamage(
                    safeDamage
                );
        }


        if (!RollConversion())
            return;


        BaseSticker target =
            PickAlbumConversionTarget(
                owner
            );


        if (target == null)
            return;


        string targetName =
            GetStickerName(
                target
            );


        BaseSticker replacement =
            TransmuteTarget(
                target,
                "Zombie infection in Album"
            );


        if (replacement == null)
            return;


        RegisterActivation(
            owner,
            StickerSpinLocation.Album,
            $"Transmute {targetName} into Zombie",
            0,
            false
        );
    }


    // =========================================================
    // TRANSMUTATION
    // =========================================================

    private BaseSticker TransmuteTarget(
        BaseSticker target,
        string reason)
    {
        if (target == null ||
            target.effect is StickerZombie ||
            zombiePrefab == null)
        {
            return null;
        }


        bool wasOnWheel =
            target.currentSegment != null;


        BaseSticker replacement =
            target.TransmuteTo(
                zombiePrefab,
                reason
            );


        if (replacement == null)
            return null;


        /*
         * A replacement can have a different collider / size than the sticker
         * it replaced. Re-run the project's existing wheel placement authority
         * immediately so out-of-segment or overlap states hard-lock input in
         * exactly the same way as any other invalid placement.
         */
        if (wasOnWheel)
        {
            Physics2D.SyncTransforms();

            if (StickerPlacementValidator.Instance != null)
            {
                StickerPlacementValidator.Instance
                    .NotifyStickerDropped(
                        replacement
                    );
            }
        }


        return replacement;
    }


    private BaseSticker PickWheelConversionTarget(
        BaseSticker owner)
    {
        if (owner == null ||
            owner.currentSegment == null)
        {
            return null;
        }


        WheelGenerator generator =
            Object.FindObjectOfType<WheelGenerator>();


        if (generator == null ||
            generator.segments == null ||
            generator.segments.Count == 0)
        {
            return null;
        }


        int ownerIndex =
            generator.GetSegmentIndex(
                owner.currentSegment
            );


        if (ownerIndex < 0)
            return null;


        int segmentCount =
            generator.segments.Count;


        HashSet<int> eligibleIndices =
            new HashSet<int>
            {
                ownerIndex,
                (ownerIndex - 1 + segmentCount) % segmentCount,
                (ownerIndex + 1) % segmentCount
            };


        List<BaseSticker> candidates =
            new List<BaseSticker>();


        foreach (int index in eligibleIndices)
        {
            if (index < 0 ||
                index >= generator.segments.Count)
            {
                continue;
            }


            WheelSegmentData data =
                generator.segments[index];


            if (data == null ||
                data.collider == null)
            {
                continue;
            }


            BaseSticker[] stickers =
                data.collider.transform
                    .GetComponentsInChildren<BaseSticker>(
                        true
                    );


            foreach (BaseSticker sticker in stickers)
            {
                if (!IsEligibleConversionTarget(
                        owner,
                        sticker
                    ))
                {
                    continue;
                }


                if (sticker.currentSegment !=
                    data.collider.transform)
                {
                    continue;
                }


                candidates.Add(
                    sticker
                );
            }
        }


        return PickRandom(
            candidates
        );
    }


    private BaseSticker PickAlbumConversionTarget(
        BaseSticker owner)
    {
        if (AlbumManager.Instance == null ||
            AlbumManager.Instance.albumZone == null)
        {
            return null;
        }


        Transform contentRoot =
            AlbumManager.Instance.albumZone
                .GetContentRoot();


        if (contentRoot == null)
            return null;


        BaseSticker[] stickers =
            contentRoot.GetComponentsInChildren<BaseSticker>(
                true
            );


        List<BaseSticker> candidates =
            new List<BaseSticker>();


        foreach (BaseSticker sticker in stickers)
        {
            if (!IsEligibleConversionTarget(
                    owner,
                    sticker
                ))
            {
                continue;
            }


            if (!AlbumManager.Instance
                .IsStickerInAlbum(
                    sticker
                ))
            {
                continue;
            }


            candidates.Add(
                sticker
            );
        }


        return PickRandom(
            candidates
        );
    }


    private bool IsEligibleConversionTarget(
        BaseSticker owner,
        BaseSticker candidate)
    {
        if (candidate == null ||
            candidate == owner ||
            candidate.IsPendingGameplayDestruction ||
            candidate.IsConsumed ||
            candidate.effect is StickerZombie)
        {
            return false;
        }


        return true;
    }


    private BaseSticker PickRandom(
        List<BaseSticker> candidates)
    {
        if (candidates == null ||
            candidates.Count == 0)
        {
            return null;
        }


        return
            candidates[
                Random.Range(
                    0,
                    candidates.Count
                )
            ];
    }


    private bool RollConversion()
    {
        float chance =
            Mathf.Clamp(
                conversionChance,
                0f,
                100f
            );


        if (chance <= 0f)
            return false;

        if (chance >= 100f)
            return true;


        return
            Random.value <
            chance / 100f;
    }


    // =========================================================
    // DESTROYED
    // =========================================================

    public override void OnDestroyedFromGameplay(
        BaseSticker owner,
        string reason)
    {
        int zombieCountInSegment =
            1;


        if (owner != null &&
            owner.currentSegment != null)
        {
            BaseSticker[] segmentStickers =
                owner.currentSegment
                    .GetComponentsInChildren<BaseSticker>(
                        true
                    );


            foreach (BaseSticker sticker in segmentStickers)
            {
                if (sticker == null ||
                    sticker == owner ||
                    sticker.IsPendingGameplayDestruction ||
                    sticker.IsConsumed ||
                    !(sticker.effect is StickerZombie))
                {
                    continue;
                }


                zombieCountInSegment++;
            }
        }


        int payout =
            Mathf.Max(
                0,
                destroyedDollarReward
            ) *
            zombieCountInSegment;


        if (CurrencyManager.Instance != null &&
            payout > 0)
        {
            CurrencyManager.Instance
                .AddDollar(
                    payout
                );
        }


        Debug.Log(
            $"[ZOMBIE] Destroyed: gain ${payout} " +
            $"({zombieCountInSegment} Zombie effect(s))."
        );


        base.OnDestroyedFromGameplay(
            owner,
            reason
        );
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


        return
            resolved
                .Replace(
                    "{damage}",
                    Mathf.Max(
                        0,
                        damageAmount
                    )
                    .ToString()
                )
                .Replace(
                    "{chance}",
                    Mathf.Clamp(
                        conversionChance,
                        0f,
                        100f
                    )
                    .ToString("0.##")
                )
                .Replace(
                    "{reward}",
                    Mathf.Max(
                        0,
                        destroyedDollarReward
                    )
                    .ToString()
                );
    }


    protected override string ResolveDestroyedTooltipTokens(
        BaseSticker owner,
        string template)
    {
        string resolved =
            base.ResolveDestroyedTooltipTokens(
                owner,
                template
            );


        return
            resolved
                .Replace(
                    "{reward}",
                    Mathf.Max(
                        0,
                        destroyedDollarReward
                    )
                    .ToString()
                );
    }


    private string GetStickerName(
        BaseSticker sticker)
    {
        if (sticker == null)
            return "Sticker";


        if (sticker.effect != null &&
            !string.IsNullOrWhiteSpace(
                sticker.effect.stickerName
            ))
        {
            return
                sticker.effect.stickerName;
        }


        return
            sticker.gameObject.name;
    }
}
