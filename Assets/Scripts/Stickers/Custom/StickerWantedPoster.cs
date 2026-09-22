using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[CreateAssetMenu(
    fileName = "StickerWantedPoster",
    menuName = "Stickers/Sticker Wanted Poster"
)]
public class StickerWantedPoster : StickerEffect
{
    public enum KillScalingMode
    {
        OnceIfAnyEnemyDied,
        PerEnemyKilled
    }


    // =========================================================
    // CONFIGURATION
    // =========================================================

    [Header("Wanted Poster")]

    [Tooltip(
        "Money this physical Wanted Poster starts at. " +
        "Each physical copy keeps its own current value."
    )]
    [Min(0)]
    public int startingReward = 3;

    [Tooltip(
        "Amount added to this physical Wanted Poster's value when its losing " +
        "condition is met."
    )]
    [Min(0)]
    public int rewardIncrease = 1;

    [Tooltip(
        "Once If Any Enemy Died: increase X once if one or more enemies die. " +
        "Per Enemy Killed: increase X once for every enemy that dies this spin."
    )]
    public KillScalingMode killScalingMode =
        KillScalingMode.OnceIfAnyEnemyDied;


    private const string CurrentRewardKey =
        "WantedPoster.CurrentReward";

    private const string InitializedKey =
        "WantedPoster.Initialized";


    // =========================================================
    // SPIN PREPARATION
    // =========================================================

    public override void PrepareSpinLocation(
        BaseSticker owner,
        StickerSpinLocation location)
    {
        if (owner == null)
            return;


        EnsureInitialized(
            owner
        );


        /*
         * Winning resolves immediately during sticker resolution.
         *
         * Losing is different: "if an enemy died THIS spin" cannot be known
         * safely when this sticker reaches its normal resolution order.
         * A later losing sticker (for example Rat Poison) or an enemy action
         * can still kill something afterwards.
         *
         * RoundManager already exposes exactly the post-gameplay hook we need:
         * OnGameplaySpinResolutionCompleted fires after ALL stickers and enemy
         * actions have resolved, but before Debt / Rewards.
         *
         * We snapshot the living current-row enemies now and inspect that same
         * snapshot at the end of gameplay resolution.
         */
        if (location !=
            StickerSpinLocation.NonWinningSegment)
        {
            return;
        }


        RoundManager roundManager =
            RoundManager.Instance;

        if (roundManager == null)
            return;


        List<BaseEnemy> enemiesAtSpinResolutionStart =
            GetLivingCurrentRowEnemies();


        Action callback =
            null;


        callback =
            () =>
            {
                /*
                 * One-shot subscription. StickerEffect assets are shared, so
                 * every physical Wanted Poster owns its own closure and removes
                 * only that closure after this spin.
                 */
                if (roundManager != null)
                {
                    roundManager
                        .OnGameplaySpinResolutionCompleted -=
                        callback;
                }


                if (owner == null)
                    return;


                int enemiesKilledThisSpin =
                    CountSnapshottedEnemyDeaths(
                        enemiesAtSpinResolutionStart
                    );


                if (enemiesKilledThisSpin <= 0)
                    return;


                int increaseTriggers =
                    killScalingMode ==
                        KillScalingMode.PerEnemyKilled
                        ? enemiesKilledThisSpin
                        : 1;


                IncreaseReward(
                    owner,
                    increaseTriggers,
                    enemiesKilledThisSpin
                );
            };


        roundManager
            .OnGameplaySpinResolutionCompleted +=
            callback;
    }


    // =========================================================
    // NORMAL RESOLUTION
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
        {
            Debug.LogWarning(
                "StickerWantedPoster: BaseSticker owner was not provided."
            );

            return;
        }


        EnsureInitialized(
            owner
        );


        switch (location)
        {
            case StickerSpinLocation.WinningSegment:
                ResolveWinningSegment(
                    owner
                );
                break;


            case StickerSpinLocation.NonWinningSegment:
                /*
                 * Intentionally delayed.
                 *
                 * PrepareSpinLocation registered a one-shot callback that will
                 * evaluate the condition after every sticker AND enemy action
                 * has finished resolving.
                 */
                break;
        }
    }


    // =========================================================
    // WINNING SEGMENT - CASH OUT CURRENT X
    // =========================================================

    private void ResolveWinningSegment(
        BaseSticker owner)
    {
        int payout =
            GetCurrentReward(
                owner
            );


        RegisterActivation(
            owner,
            StickerSpinLocation.WinningSegment,
            "Collect bounty",
            payout,
            null
        );


        if (CurrencyManager.Instance != null &&
            payout > 0)
        {
            CurrencyManager.Instance
                .AddDollar(
                    payout
                );
        }


        Debug.Log(
            $"[WANTED POSTER] Collected ${payout}."
        );
    }


    // =========================================================
    // LOSING SEGMENT - GROW X AFTER A KILL
    // =========================================================

    private void IncreaseReward(
        BaseSticker owner,
        int increaseTriggers,
        int enemiesKilledThisSpin)
    {
        int increasePerTrigger =
            Mathf.Max(
                0,
                rewardIncrease
            );


        int safeTriggers =
            Mathf.Max(
                0,
                increaseTriggers
            );


        int totalIncrease =
            increasePerTrigger *
            safeTriggers;


        if (totalIncrease <= 0)
            return;


        int oldReward =
            GetCurrentReward(
                owner
            );


        int newReward =
            oldReward +
            totalIncrease;


        owner.SetRuntimeInt(
            CurrentRewardKey,
            newReward
        );


        string description =
            BuildIncreaseDescription(
                totalIncrease,
                newReward,
                enemiesKilledThisSpin
            );


        RegisterActivation(
            owner,
            StickerSpinLocation.NonWinningSegment,
            description,
            0,
            null
        );


        Debug.Log(
            $"[WANTED POSTER] {enemiesKilledThisSpin} enemy/enemies died this spin. " +
            $"Reward increased from ${oldReward} to ${newReward}."
        );
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
            CurrentRewardKey,
            Mathf.Max(
                0,
                startingReward
            )
        );


        owner.SetRuntimeInt(
            InitializedKey,
            1
        );
    }


    private int GetCurrentReward(
        BaseSticker owner)
    {
        if (owner == null)
        {
            return
                Mathf.Max(
                    0,
                    startingReward
                );
        }


        EnsureInitialized(
            owner
        );


        return
            Mathf.Max(
                0,
                owner.GetRuntimeInt(
                    CurrentRewardKey,
                    startingReward
                )
            );
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
            enemiesAtStart.Count == 0)
        {
            return 0;
        }


        int deaths = 0;


        foreach (BaseEnemy enemy in enemiesAtStart)
        {
            /*
             * Unity's destroyed-object null semantics are useful here:
             * either the enemy is still present and marked dead, or its object
             * has already been destroyed by the time this callback executes.
             */
            if (enemy == null ||
                enemy.IsDead)
            {
                deaths++;
            }
        }


        return deaths;
    }


    // =========================================================
    // LOG
    // =========================================================

    private string BuildIncreaseDescription(
        int increase,
        int newReward,
        int enemiesKilledThisSpin)
    {
        if (GameLogManager.Instance != null)
        {
            return
                "Bounty increases by " +
                GameLogManager.Instance
                    .MoneyText(
                        $"${increase}"
                    ) +
                " (now " +
                GameLogManager.Instance
                    .MoneyText(
                        $"${newReward}"
                    ) +
                ")";
        }


        return
            $"Bounty increases by ${increase} (now ${newReward})";
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
                    "{reward}",
                    GetCurrentReward(owner)
                        .ToString()
                )
                .Replace(
                    "{increase}",
                    Mathf.Max(
                        0,
                        rewardIncrease
                    )
                    .ToString()
                )
                .Replace(
                    "{killCondition}",
                    killScalingMode ==
                        KillScalingMode.PerEnemyKilled
                        ? "for each enemy killed"
                        : "if an enemy died"
                );
    }
}
