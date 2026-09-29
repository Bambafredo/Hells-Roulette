using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[CreateAssetMenu(
    fileName = "StickerMidasHand",
    menuName = "Stickers/Sticker Midas Hand"
)]
public class StickerMidasHand : StickerEffect
{
    // =========================================================
    // CONFIGURATION
    // =========================================================

    [Header("Midas Hand")]

    [Tooltip(
        "Exact physical Golden Nugget sticker prefab used by every Midas Hand effect."
    )]
    public GameObject goldenNuggetPrefab;


    [Tooltip(
        "Losing-segment chance, from 0 to 100, to transmute one random eligible " +
        "sticker in Midas Hand's own segment into a Golden Nugget."
    )]
    [Range(0f, 100f)]
    public float losingTransmuteChance =
        25f;


    [Tooltip(
        "Album chance, from 0 to 100, to transmute one random eligible sticker " +
        "anywhere on the roulette into a Golden Nugget."
    )]
    [Range(0f, 100f)]
    public float albumTransmuteChance =
        10f;


    private const string WinningArmedKey =
        "MidasHand.WinningArmed";


    // =========================================================
    // SPIN PREPARATION
    // =========================================================

    public override void PrepareSpinLocation(
        BaseSticker owner,
        StickerSpinLocation location)
    {
        if (owner == null ||
            location !=
                StickerSpinLocation.WinningSegment)
        {
            return;
        }


        /*
         * Winning depends on EVERYTHING that dies during this spin, including
         * enemies killed by stickers or enemy-side consequences that resolve
         * after Midas Hand's normal position in sticker order.
         *
         * Snapshot the living current-row enemies now and evaluate the same
         * snapshot only after all sticker + enemy resolution has completed.
         */
        List<BaseEnemy> enemiesAtSpinResolutionStart =
            GetLivingCurrentRowEnemies();


        owner.SetRuntimeInt(
            WinningArmedKey,
            0
        );


        RoundManager roundManager =
            RoundManager.Instance;


        if (roundManager == null)
            return;


        Action callback =
            null;


        callback =
            () =>
            {
                if (roundManager != null)
                {
                    roundManager
                        .OnGameplaySpinResolutionCompleted -=
                        callback;
                }


                if (owner == null)
                    return;


                bool activationWasReached =
                    owner.GetRuntimeInt(
                        WinningArmedKey,
                        0
                    ) != 0;


                owner.SetRuntimeInt(
                    WinningArmedKey,
                    0
                );


                /*
                 * A previous sticker can destroy / consume Midas Hand before
                 * its own winning activation is reached. Preparing the spin
                 * alone must never grant rewards in that case.
                 */
                if (!activationWasReached)
                    return;


                ResolveWinningAfterGameplay(
                    owner,
                    enemiesAtSpinResolutionStart
                );
            };


        roundManager
            .OnGameplaySpinResolutionCompleted +=
            callback;
    }


    // =========================================================
    // NORMAL SPIN RESOLUTION
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
                /*
                 * Actual payout is delayed until the post-gameplay callback,
                 * but this flag proves Midas Hand really reached its activation
                 * in the resolved sticker snapshot.
                 */
                owner.SetRuntimeInt(
                    WinningArmedKey,
                    1
                );
                break;


            case StickerSpinLocation.NonWinningSegment:
                ResolveLosing(
                    owner
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
    // WINNING - ONE GOLDEN NUGGET PER ENEMY DEATH
    // =========================================================

    private void ResolveWinningAfterGameplay(
        BaseSticker owner,
        List<BaseEnemy> enemiesAtSpinResolutionStart)
    {
        int enemiesKilledThisSpin =
            CountSnapshottedEnemyDeaths(
                enemiesAtSpinResolutionStart
            );


        if (enemiesKilledThisSpin <= 0)
        {
            RegisterActivation(
                owner,
                StickerSpinLocation.WinningSegment,
                "No enemy died this spin",
                0,
                null
            );

            return;
        }


        if (goldenNuggetPrefab == null)
        {
            RegisterActivation(
                owner,
                StickerSpinLocation.WinningSegment,
                "Golden Nugget prefab missing",
                0,
                null
            );

            Debug.LogWarning(
                "[MIDAS HAND] Golden Nugget prefab is missing."
            );

            return;
        }


        bool queued =
            RewardManager.Instance != null &&
            RewardManager.Instance
                .RequestMultipleFreeStickerRewards(
                    goldenNuggetPrefab,
                    enemiesKilledThisSpin,
                    stickerName
                );


        RegisterActivation(
            owner,
            StickerSpinLocation.WinningSegment,
            queued
                ? $"Gain {enemiesKilledThisSpin} {GetGoldenNuggetPluralName(enemiesKilledThisSpin)}"
                : $"Could not grant {enemiesKilledThisSpin} {GetGoldenNuggetPluralName(enemiesKilledThisSpin)}",
            0,
            null
        );


        if (queued)
        {
            /*
             * RewardManager's own post-gameplay listener can execute before
             * this delayed callback. Ask it to begin now; if Infestation is
             * pending, it safely defers and Infestation will hand off the
             * existing flow lock afterwards.
             */
            RewardManager.Instance
                .TryBeginQueuedGameplayModalAfterResolution();
        }
    }


    // =========================================================
    // LOSING
    // =========================================================

    private void ResolveLosing(
        BaseSticker owner)
    {
        if (!RollChance(
                losingTransmuteChance))
        {
            RegisterActivation(
                owner,
                StickerSpinLocation.NonWinningSegment,
                "Transmutation failed",
                0,
                null
            );

            return;
        }


        BaseSticker target =
            PickRandomStickerInSegment(
                owner
            );


        if (target == null)
        {
            RegisterActivation(
                owner,
                StickerSpinLocation.NonWinningSegment,
                "No valid sticker to transmute",
                0,
                null
            );

            return;
        }


        string targetName =
            GetStickerName(
                target
            );


        BaseSticker replacement =
            TransmuteToGoldenNugget(
                target,
                "Midas Hand losing transmutation"
            );


        RegisterActivation(
            owner,
            StickerSpinLocation.NonWinningSegment,
            replacement != null
                ? $"Transmute {FormatStickerName(targetName)} into {GetGoldenNuggetName()}"
                : $"Could not transmute {FormatStickerName(targetName)}",
            0,
            null
        );
    }


    // =========================================================
    // ALBUM
    // =========================================================

    private void ResolveAlbum(
        BaseSticker owner)
    {
        if (!RollChance(
                albumTransmuteChance))
        {
            RegisterActivation(
                owner,
                StickerSpinLocation.Album,
                "Transmutation failed",
                0,
                null
            );

            return;
        }


        BaseSticker target =
            PickRandomStickerOnRoulette();


        if (target == null)
        {
            RegisterActivation(
                owner,
                StickerSpinLocation.Album,
                "No valid sticker to transmute",
                0,
                null
            );

            return;
        }


        string targetName =
            GetStickerName(
                target
            );


        BaseSticker replacement =
            TransmuteToGoldenNugget(
                target,
                "Midas Hand Album transmutation"
            );


        RegisterActivation(
            owner,
            StickerSpinLocation.Album,
            replacement != null
                ? $"Transmute {FormatStickerName(targetName)} into {GetGoldenNuggetName()}"
                : $"Could not transmute {FormatStickerName(targetName)}",
            0,
            null
        );
    }


    // =========================================================
    // TRANSMUTATION
    // =========================================================

    private BaseSticker TransmuteToGoldenNugget(
        BaseSticker target,
        string reason)
    {
        if (!IsEligibleTransmutationTarget(
                target) ||
            goldenNuggetPrefab == null)
        {
            return null;
        }


        bool wasOnWheel =
            target.currentSegment != null;


        BaseSticker replacement =
            target.TransmuteTo(
                goldenNuggetPrefab,
                reason
            );


        if (replacement == null)
            return null;


        /*
         * Golden Nugget can have a different collider / size from the sticker
         * it replaced. Re-run the project's normal placement authority.
         */
        if (wasOnWheel)
        {
            Physics2D.SyncTransforms();

            StickerPlacementValidator.Instance?
                .NotifyStickerDropped(
                    replacement
                );
        }


        return replacement;
    }


    // =========================================================
    // TARGETING
    // =========================================================

    private BaseSticker PickRandomStickerInSegment(
        BaseSticker owner)
    {
        if (owner == null ||
            owner.currentSegment == null)
        {
            return null;
        }


        BaseSticker[] stickers =
            owner.currentSegment
                .GetComponentsInChildren<BaseSticker>(
                    true
                );


        List<BaseSticker> candidates =
            new List<BaseSticker>();


        foreach (BaseSticker sticker in stickers)
        {
            if (sticker == owner ||
                sticker.currentSegment !=
                    owner.currentSegment ||
                !IsEligibleTransmutationTarget(
                    sticker))
            {
                continue;
            }


            candidates.Add(
                sticker
            );
        }


        return
            PickRandom(
                candidates
            );
    }


    private BaseSticker PickRandomStickerOnRoulette()
    {
        WheelGenerator generator =
            UnityEngine.Object
                .FindObjectOfType<WheelGenerator>();


        if (generator == null ||
            generator.segments == null)
        {
            return null;
        }


        List<BaseSticker> candidates =
            new List<BaseSticker>();


        foreach (WheelSegmentData segmentData in
                 generator.segments)
        {
            if (segmentData == null ||
                segmentData.collider == null)
            {
                continue;
            }


            Transform segment =
                segmentData.collider.transform;


            BaseSticker[] stickers =
                segment.GetComponentsInChildren<BaseSticker>(
                    true
                );


            foreach (BaseSticker sticker in stickers)
            {
                if (sticker == null ||
                    sticker.currentSegment != segment ||
                    !sticker.isPlaced ||
                    !IsEligibleTransmutationTarget(
                        sticker))
                {
                    continue;
                }


                candidates.Add(
                    sticker
                );
            }
        }


        return
            PickRandom(
                candidates
            );
    }


    private bool IsEligibleTransmutationTarget(
        BaseSticker candidate)
    {
        if (candidate == null ||
            candidate.IsPendingGameplayDestruction ||
            candidate.IsConsumed)
        {
            return false;
        }


        StickerEffect goldenNuggetEffect =
            GetGoldenNuggetEffect();


        if (goldenNuggetEffect != null &&
            candidate.effect ==
                goldenNuggetEffect)
        {
            return false;
        }


        return true;
    }


    private BaseSticker PickRandom(
        List<BaseSticker> candidates)
    {
        if (candidates == null ||
            candidates.Count <= 0)
        {
            return null;
        }


        return
            candidates[
                UnityEngine.Random.Range(
                    0,
                    candidates.Count
                )
            ];
    }


    // =========================================================
    // ENEMY DEATH DETECTION
    // =========================================================

    private List<BaseEnemy> GetLivingCurrentRowEnemies()
    {
        List<BaseEnemy> enemies =
            new List<BaseEnemy>();


        EnemyCorridorController corridor =
            UnityEngine.Object
                .FindObjectOfType<EnemyCorridorController>();


        if (corridor == null ||
            corridor.CurrentRow == null)
        {
            return enemies;
        }


        BaseEnemy[] rowEnemies =
            corridor.CurrentRow
                .GetComponentsInChildren<BaseEnemy>(
                    true
                );


        foreach (BaseEnemy enemy in rowEnemies)
        {
            if (enemy == null ||
                enemy.IsDead ||
                !enemy.CombatActive ||
                !enemy.gameObject.activeInHierarchy)
            {
                continue;
            }


            enemies.Add(
                enemy
            );
        }


        return enemies;
    }


    private int CountSnapshottedEnemyDeaths(
        List<BaseEnemy> enemiesAtStart)
    {
        if (enemiesAtStart == null ||
            enemiesAtStart.Count <= 0)
        {
            return 0;
        }


        int deaths =
            0;


        foreach (BaseEnemy enemy in enemiesAtStart)
        {
            if (enemy == null ||
                enemy.IsDead)
            {
                deaths++;
            }
        }


        return deaths;
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
                    "{losingChance}",
                    Mathf.Clamp(
                        losingTransmuteChance,
                        0f,
                        100f
                    )
                    .ToString("0.##")
                )
                .Replace(
                    "{albumChance}",
                    Mathf.Clamp(
                        albumTransmuteChance,
                        0f,
                        100f
                    )
                    .ToString("0.##")
                )
                .Replace(
                    "{goldenNugget}",
                    GetGoldenNuggetName()
                );
    }


    // =========================================================
    // HELPERS
    // =========================================================

    private bool RollChance(
        float chance)
    {
        float safeChance =
            Mathf.Clamp(
                chance,
                0f,
                100f
            );


        if (safeChance <= 0f)
            return false;

        if (safeChance >= 100f)
            return true;


        return
            UnityEngine.Random.value <
            safeChance / 100f;
    }


    private StickerEffect GetGoldenNuggetEffect()
    {
        if (goldenNuggetPrefab == null)
            return null;


        BaseSticker sticker =
            goldenNuggetPrefab
                .GetComponentInChildren<BaseSticker>(
                    true
                );


        return
            sticker != null
                ? sticker.effect
                : null;
    }


    private string GetGoldenNuggetName()
    {
        StickerEffect effect =
            GetGoldenNuggetEffect();


        if (effect != null &&
            !string.IsNullOrWhiteSpace(
                effect.stickerName
            ))
        {
            return
                effect.stickerName;
        }


        return
            goldenNuggetPrefab != null
                ? goldenNuggetPrefab.name
                : "Golden Nugget";
    }


    private string GetGoldenNuggetPluralName(
        int count)
    {
        string name =
            GetGoldenNuggetName();


        if (count == 1)
            return name;


        return
            name.EndsWith(
                "s",
                StringComparison.OrdinalIgnoreCase
            )
                ? name
                : name + "s";
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
            sticker.name;
    }


    private string FormatStickerName(
        string stickerDisplayName)
    {
        if (GameLogManager.Instance == null)
            return stickerDisplayName;


        return
            GameLogManager.Instance
                .StickerText(
                    stickerDisplayName
                );
    }
}
