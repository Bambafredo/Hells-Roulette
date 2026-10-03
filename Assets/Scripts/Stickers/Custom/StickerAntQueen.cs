using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StickerAntQueen",
    menuName = "Stickers/Sticker Ant Queen"
)]
public class StickerAntQueen : StickerEffect, IAntSticker
{
    // =========================================================
    // CONFIGURATION
    // =========================================================

    [Header("Ant Queen - Album")]

    [Tooltip(
        "Base chance, from 0 to 100, to gain a random Ant sticker while Ant Queen is in the Album."
    )]
    [Range(0f, 100f)]
    public float baseAntRewardChance =
        10f;


    [Tooltip(
        "Additional chance added for every OTHER Ant currently in the Album."
    )]
    [Min(0f)]
    public float chanceIncreasePerOtherAlbumAnt =
        5f;


    [Tooltip(
        "Exact physical Ant sticker prefabs Ant Queen may generate. " +
        "Only prefabs whose StickerEffect implements IAntSticker are eligible."
    )]
    public GameObject[] antRewardPrefabs;


    [Header("Ant Queen - Winning")]

    [Tooltip(
        "Damage dealt per OTHER Ant currently on the roulette."
    )]
    [Min(0)]
    public int damagePerOtherAnt =
        1;


    // =========================================================
    // RESOLUTION
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
                ResolveWinning(
                    owner
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
    // WINNING
    // =========================================================

    private void ResolveWinning(
        BaseSticker owner)
    {
        int otherAnts =
            StickerAntUtility
                .CountOtherAntsOnWheel(
                    owner
                );


        int damage =
            CalculateWinningDamage(
                otherAnts
            );


        EnemyPanelManager enemyPanel =
            Object.FindObjectOfType<EnemyPanelManager>();


        BaseEnemy target =
            enemyPanel != null
                ? StickerTargetingUtility
                    .GetFirstDamageTarget(
                        enemyPanel,
                        owner,
                        StickerSpinLocation.WinningSegment
                    )
                : null;


        /*
         * Ant Queen is naturally leftmost-targeting, so it goes through the
         * generic targeting utility. Scope may therefore redirect it to the
         * rightmost enemy, exactly like other left-starting attacks.
         */
        if (target == null)
        {
            RegisterActivation(
                owner,
                StickerSpinLocation.WinningSegment,
                $"No valid target ({otherAnts} other Ant" +
                $"{(otherAnts == 1 ? "" : "s")})",
                0,
                null
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


        /*
         * Damage resolves before limited-use consumption.
         * If this hit kills an enemy carrying Burnout, that Curse is gone
         * before Ant Queen spends its normal use.
         */
        bool shouldConsumeUse =
            base.ShouldConsumeUseOnActivation(
                StickerSpinLocation.WinningSegment
            );


        RegisterActivation(
            owner,
            StickerSpinLocation.WinningSegment,
            $"Deal {damage} damage to {targetName} " +
            $"({otherAnts} other Ant{(otherAnts == 1 ? "" : "s")})",
            0,
            false
        );


        if (damage > 0)
        {
            target.TakeDamage(
                damage
            );
        }


        if (shouldConsumeUse &&
            owner != null &&
            !owner.IsConsumed &&
            !owner.IsPendingGameplayDestruction)
        {
            owner.ConsumeUseAfterActivation();
        }
    }


    // =========================================================
    // LOSING
    // =========================================================

    private void ResolveLosing(
        BaseSticker owner)
    {
        int otherAnts =
            StickerAntUtility
                .CountOtherAntsOnWheel(
                    owner
                );


        int money =
            Mathf.Max(
                0,
                otherAnts
            );


        RegisterActivation(
            owner,
            StickerSpinLocation.NonWinningSegment,
            $"{otherAnts} other Ant{(otherAnts == 1 ? "" : "s")} on the wheel",
            money,
            null
        );


        if (CurrencyManager.Instance != null &&
            money > 0)
        {
            CurrencyManager.Instance
                .AddDollar(
                    money
                );
        }
    }


    // =========================================================
    // ALBUM
    // =========================================================

    private void ResolveAlbum(
        BaseSticker owner)
    {
        int otherAlbumAnts =
            StickerAntUtility
                .CountOtherAntsInAlbum(
                    owner
                );


        float currentChance =
            CalculateAlbumRewardChance(
                otherAlbumAnts
            );


        bool wonAnt =
            RollChance(
                currentChance
            );


        if (!wonAnt)
        {
            RegisterActivation(
                owner,
                StickerSpinLocation.Album,
                $"Ant reward failed ({FormatChance(currentChance)}% chance)",
                0,
                null
            );

            return;
        }


        GameObject antPrefab =
            GetRandomValidAntRewardPrefab();


        if (antPrefab == null)
        {
            RegisterActivation(
                owner,
                StickerSpinLocation.Album,
                "Ant reward succeeded, but no valid Ant prefab is configured",
                0,
                null
            );


            Debug.LogWarning(
                "[ANT QUEEN] Reward roll succeeded, but no valid Ant reward prefab is configured."
            );

            return;
        }


        bool queued =
            RewardManager.Instance != null &&
            RewardManager.Instance
                .RequestFreeStickerReward(
                    antPrefab,
                    stickerName
                );


        string antName =
            GetStickerPrefabName(
                antPrefab
            );


        RegisterActivation(
            owner,
            StickerSpinLocation.Album,
            queued
                ? $"Gain {antName} ({FormatChance(currentChance)}% chance)"
                : $"Could not grant {antName}",
            0,
            null
        );
    }


    // =========================================================
    // ANT REWARD POOL
    // =========================================================

    private GameObject GetRandomValidAntRewardPrefab()
    {
        if (antRewardPrefabs == null ||
            antRewardPrefabs.Length <= 0)
        {
            return null;
        }


        List<GameObject> validPrefabs =
            new List<GameObject>();


        foreach (GameObject prefab in
                 antRewardPrefabs)
        {
            if (prefab == null)
                continue;


            BaseSticker sticker =
                prefab.GetComponentInChildren<BaseSticker>(
                    true
                );


            if (sticker == null ||
                !(sticker.effect is IAntSticker))
            {
                continue;
            }


            validPrefabs.Add(
                prefab
            );
        }


        if (validPrefabs.Count <= 0)
            return null;


        return
            validPrefabs[
                Random.Range(
                    0,
                    validPrefabs.Count
                )
            ];
    }


    // =========================================================
    // CALCULATIONS
    // =========================================================

    private int CalculateWinningDamage(
        int otherAnts)
    {
        long calculatedDamage =
            (long)Mathf.Max(
                0,
                damagePerOtherAnt
            ) *
            Mathf.Max(
                0,
                otherAnts
            );


        return
            calculatedDamage > int.MaxValue
                ? int.MaxValue
                : (int)calculatedDamage;
    }


    private float CalculateAlbumRewardChance(
        int otherAlbumAnts)
    {
        float calculatedChance =
            Mathf.Max(
                0f,
                baseAntRewardChance
            ) +
            Mathf.Max(
                0,
                otherAlbumAnts
            ) *
            Mathf.Max(
                0f,
                chanceIncreasePerOtherAlbumAnt
            );


        return
            Mathf.Clamp(
                calculatedChance,
                0f,
                100f
            );
    }


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
            Random.value <
            safeChance / 100f;
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


    /*
     * Suggested tooltips:
     *
     * W:
     * Deal {damagePerAnt} damage to the leftmost enemy for each other Ant
     * on the wheel. Current damage: {winningDamage}.
     *
     * L:
     * Gain $1 for each other Ant on the wheel.
     * Current gain: ${losingMoney}.
     *
     * AL:
     * {albumChance}% chance to gain a random Ant sticker.
     * Chance increases by {chancePerAlbumAnt}% for each other Ant in the Album.
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


        int otherWheelAnts =
            StickerAntUtility
                .CountOtherAntsOnWheel(
                    owner
                );


        int otherAlbumAnts =
            StickerAntUtility
                .CountOtherAntsInAlbum(
                    owner
                );


        int winningDamage =
            CalculateWinningDamage(
                otherWheelAnts
            );


        int losingMoney =
            Mathf.Max(
                0,
                otherWheelAnts
            );


        float albumChance =
            CalculateAlbumRewardChance(
                otherAlbumAnts
            );


        return
            resolved
                .Replace(
                    "{damagePerAnt}",
                    Mathf.Max(
                        0,
                        damagePerOtherAnt
                    ).ToString()
                )
                .Replace(
                    "{otherWheelAnts}",
                    otherWheelAnts.ToString()
                )
                .Replace(
                    "{winningDamage}",
                    winningDamage.ToString()
                )
                .Replace(
                    "{losingMoney}",
                    losingMoney.ToString()
                )
                .Replace(
                    "{baseAlbumChance}",
                    FormatChance(
                        Mathf.Clamp(
                            baseAntRewardChance,
                            0f,
                            100f
                        )
                    )
                )
                .Replace(
                    "{chancePerAlbumAnt}",
                    FormatChance(
                        Mathf.Max(
                            0f,
                            chanceIncreasePerOtherAlbumAnt
                        )
                    )
                )
                .Replace(
                    "{otherAlbumAnts}",
                    otherAlbumAnts.ToString()
                )
                .Replace(
                    "{albumChance}",
                    FormatChance(
                        albumChance
                    )
                );
    }


    // =========================================================
    // HELPERS
    // =========================================================

    private string GetStickerPrefabName(
        GameObject prefab)
    {
        if (prefab == null)
            return "Ant";


        BaseSticker sticker =
            prefab.GetComponentInChildren<BaseSticker>(
                true
            );


        if (sticker != null &&
            sticker.effect != null &&
            !string.IsNullOrWhiteSpace(
                sticker.effect.stickerName
            ))
        {
            return
                sticker.effect.stickerName;
        }


        return
            prefab.name;
    }


    private string FormatChance(
        float value)
    {
        return
            Mathf.Clamp(
                value,
                0f,
                100f
            )
            .ToString(
                "0.##"
            );
    }
}
