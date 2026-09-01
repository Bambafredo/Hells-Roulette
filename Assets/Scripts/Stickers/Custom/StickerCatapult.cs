using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum CatapultDestroyScope
{
    ThisSegment,
    WholeWheel
}


public enum CatapultDamageTargetMode
{
    RightmostEnemy,
    AllEnemies
}


[CreateAssetMenu(
    fileName = "StickerCatapult",
    menuName = "Stickers/Sticker Catapult"
)]
public class StickerCatapult : StickerEffect
{
    // =========================================================
    // CONFIGURATION
    // =========================================================

    [Header("Damage")]

    [Tooltip(
        "Damage dealt when Catapult is allowed to fire."
    )]
    [Min(0)]
    public int damageAmount =
        5;


    [Tooltip(
        "Rightmost Enemy = hit the first living enemy when scanning the " +
        "active row from RIGHT to LEFT. All Enemies = deal the configured " +
        "damage to every living combat-active enemy in CurrentRow."
    )]
    public CatapultDamageTargetMode damageTargetMode =
        CatapultDamageTargetMode.RightmostEnemy;


    [Header("Sticker Destruction")]

    [Tooltip(
        "Chance for Catapult to destroy a sticker when it activates. " +
        "Set to 100 for guaranteed destruction."
    )]
    [Range(0f, 100f)]
    public float destroyChancePercent =
        100f;


    [Tooltip(
        "This Segment = choose only among stickers in Catapult's segment. " +
        "Whole Wheel = choose among every sticker currently placed on the roulette."
    )]
    public CatapultDestroyScope destroyScope =
        CatapultDestroyScope.ThisSegment;


    [Tooltip(
        "If enabled, Catapult itself can be selected as the random sticker. " +
        "If disabled, only other valid stickers can be destroyed."
    )]
    public bool canDestroySelf =
        true;


    [Tooltip(
        "If enabled, Catapult deals NO damage unless it actually destroys a " +
        "sticker during this activation. A failed destruction roll or having " +
        "no valid sticker target means the shot fails completely."
    )]
    public bool destructionRequiredToDealDamage =
        true;


    // =========================================================
    // RESOLUTION ORDER
    // =========================================================

    /*
     * Structural destruction resolves before ordinary priority-0 stickers.
     *
     * Coffee uses a higher generic priority (-100), so its activation-count
     * modifier is established first. RouletteController still understands
     * only generic priorities, never Catapult.
     */
    public override int SpinResolutionPriority =>
        -50;


    // =========================================================
    // EFFECT
    // =========================================================

    public override void ResolveSpinLocation(
        BaseSticker owner,
        StickerSpinLocation location)
    {
        if (location !=
            StickerSpinLocation.WinningSegment)
        {
            return;
        }


        if (RoundManager.Instance != null &&
            !RoundManager.Instance.WasLastSpinValid)
        {
            return;
        }


        if (owner == null ||
            owner.currentSegment == null)
        {
            return;
        }


        // -----------------------------------------------------
        // DESTRUCTION
        // -----------------------------------------------------

        bool destructionRollSucceeded =
            RollDestructionChance();


        BaseSticker sacrifice =
            destructionRollSucceeded
                ? GetRandomStickerToDestroy(
                    owner
                )
                : null;


        /*
         * "Destroyed successfully" means a REAL sticker was selected and
         * DestroyFromGameplay accepted the destruction.
         *
         * This is intentionally stronger than simply passing the percentage
         * roll, because when destruction is required we do not want Catapult
         * to fire for free just because there happened to be no valid sticker.
         */
        bool destroyedSticker =
            false;


        if (destructionRollSucceeded &&
            sacrifice != null)
        {
            destroyedSticker =
                sacrifice.DestroyFromGameplay(
                    "destroyed by Catapult"
                );
        }


        // -----------------------------------------------------
        // DAMAGE CONDITION
        // -----------------------------------------------------

        bool canDealDamage =
            !destructionRequiredToDealDamage ||
            destroyedSticker;


        EnemyPanelManager enemyPanel =
            Object.FindObjectOfType<EnemyPanelManager>();


        List<BaseEnemy> damagedEnemies =
            new List<BaseEnemy>();


        if (canDealDamage &&
            enemyPanel != null &&
            damageAmount > 0)
        {
            damagedEnemies =
                DealConfiguredDamage(
                    enemyPanel
                );
        }


        // -----------------------------------------------------
        // ACTIVATION / LOG
        // -----------------------------------------------------

        string description =
            BuildActivationDescription(
                sacrifice,
                destructionRollSucceeded,
                destroyedSticker,
                canDealDamage,
                damagedEnemies
            );


        RegisterActivation(
            owner,
            location,
            description,
            0,
            null
        );
    }


    // =========================================================
    // DAMAGE
    // =========================================================

    private List<BaseEnemy> DealConfiguredDamage(
        EnemyPanelManager enemyPanel)
    {
        List<BaseEnemy> hit =
            new List<BaseEnemy>();


        switch (damageTargetMode)
        {
            case CatapultDamageTargetMode.AllEnemies:
            {
                BaseEnemy[] targets =
                    enemyPanel
                        .GetAllAliveEnemies();


                if (targets == null)
                    return hit;


                foreach (BaseEnemy enemy in targets)
                {
                    if (enemy == null ||
                        enemy.IsDead)
                    {
                        continue;
                    }


                    enemy.TakeDamage(
                        damageAmount
                    );

                    hit.Add(
                        enemy
                    );
                }


                break;
            }


            case CatapultDamageTargetMode.RightmostEnemy:
            default:
            {
                BaseEnemy target =
                    enemyPanel
                        .GetRightmostAliveEnemy();


                if (target != null)
                {
                    target.TakeDamage(
                        damageAmount
                    );

                    hit.Add(
                        target
                    );
                }


                break;
            }
        }


        return hit;
    }


    // =========================================================
    // DESTRUCTION CHANCE
    // =========================================================

    private bool RollDestructionChance()
    {
        float chance =
            Mathf.Clamp(
                destroyChancePercent,
                0f,
                100f
            );


        if (chance <= 0f)
            return false;


        if (chance >= 100f)
            return true;


        return
            Random.value * 100f <
            chance;
    }


    // =========================================================
    // RANDOM STICKER TARGET
    // =========================================================

    private BaseSticker GetRandomStickerToDestroy(
        BaseSticker owner)
    {
        if (owner == null ||
            owner.currentSegment == null)
        {
            return null;
        }


        BaseSticker[] source =
            GetCandidateSource(
                owner
            );


        if (source == null ||
            source.Length <= 0)
        {
            return null;
        }


        List<BaseSticker> candidates =
            new List<BaseSticker>();


        foreach (BaseSticker candidate in
                 source)
        {
            if (!IsValidDestructionCandidate(
                    owner,
                    candidate))
            {
                continue;
            }


            candidates.Add(
                candidate
            );
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


    private BaseSticker[] GetCandidateSource(
        BaseSticker owner)
    {
        switch (destroyScope)
        {
            case CatapultDestroyScope.WholeWheel:
                /*
                 * Gather broadly, then filter by isPlaced/currentSegment.
                 * This excludes Album, Bag and Reward-offer stickers without
                 * coupling Catapult to WheelGenerator internals.
                 */
                return
                    Object.FindObjectsOfType<BaseSticker>(
                        true
                    );


            case CatapultDestroyScope.ThisSegment:
            default:
                return
                    owner.currentSegment
                        .GetComponentsInChildren<BaseSticker>(
                            true
                        );
        }
    }


    private bool IsValidDestructionCandidate(
        BaseSticker owner,
        BaseSticker candidate)
    {
        if (candidate == null ||
            candidate.IsConsumed ||
            candidate.IsPendingGameplayDestruction ||
            !candidate.isPlaced ||
            candidate.currentSegment == null)
        {
            return false;
        }


        if (!canDestroySelf &&
            candidate == owner)
        {
            return false;
        }


        if (destroyScope ==
            CatapultDestroyScope.ThisSegment)
        {
            return
                candidate.currentSegment ==
                owner.currentSegment;
        }


        /*
         * WholeWheel:
         * any currently placed roulette sticker is valid regardless of segment.
         */
        return true;
    }


    // =========================================================
    // LOG DESCRIPTION
    // =========================================================

    private string BuildActivationDescription(
        BaseSticker sacrifice,
        bool destructionRollSucceeded,
        bool destroyedSticker,
        bool canDealDamage,
        List<BaseEnemy> damagedEnemies)
    {
        string destructionText =
            BuildDestructionLogText(
                sacrifice,
                destructionRollSucceeded,
                destroyedSticker
            );


        string damageText =
            BuildDamageLogText(
                canDealDamage,
                damagedEnemies
            );


        if (string.IsNullOrWhiteSpace(
                damageText))
        {
            return
                destructionText;
        }


        return
            destructionText +
            ". " +
            damageText;
    }


    private string BuildDestructionLogText(
        BaseSticker sacrifice,
        bool destructionRollSucceeded,
        bool destroyedSticker)
    {
        if (!destructionRollSucceeded)
        {
            return
                $"Destruction failed " +
                $"({FormatChance()}% chance)";
        }


        if (sacrifice == null)
        {
            return
                "No valid sticker to destroy";
        }


        if (!destroyedSticker)
        {
            return
                "Sticker destruction failed";
        }


        string sacrificeName =
            GetStickerDisplayName(
                sacrifice
            );


        if (GameLogManager.Instance != null)
        {
            sacrificeName =
                GameLogManager.Instance
                    .StickerText(
                        sacrificeName
                    );
        }


        return
            "Destroy " +
            sacrificeName;
    }


    private string BuildDamageLogText(
        bool canDealDamage,
        List<BaseEnemy> damagedEnemies)
    {
        if (!canDealDamage)
        {
            return
                "No shot";
        }


        if (damagedEnemies == null ||
            damagedEnemies.Count <= 0)
        {
            return
                "No valid enemy target";
        }


        if (damageTargetMode ==
            CatapultDamageTargetMode.AllEnemies)
        {
            return
                $"Deal {damageAmount} damage to all enemies";
        }


        string enemyName =
            damagedEnemies[0] != null
                ? damagedEnemies[0].EnemyName
                : "enemy";


        if (GameLogManager.Instance != null)
        {
            enemyName =
                GameLogManager.Instance
                    .EnemyText(
                        enemyName
                    );
        }


        return
            $"Deal {damageAmount} damage to " +
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
            return
                sticker.effect.stickerName;
        }


        return
            sticker.gameObject.name;
    }


    // =========================================================
    // TOOLTIP
    // =========================================================

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
                    "{destroyChance}",
                    FormatChance()
                )
                .Replace(
                    "{scope}",
                    GetScopeTooltipText()
                )
                .Replace(
                    "{damageTarget}",
                    GetDamageTargetTooltipText()
                );
    }


    private string FormatChance()
    {
        float chance =
            Mathf.Clamp(
                destroyChancePercent,
                0f,
                100f
            );


        return
            chance.ToString(
                "0.##"
            );
    }


    private string GetScopeTooltipText()
    {
        switch (destroyScope)
        {
            case CatapultDestroyScope.WholeWheel:
                return
                    "the wheel";


            case CatapultDestroyScope.ThisSegment:
            default:
                return
                    "this segment";
        }
    }


    private string GetDamageTargetTooltipText()
    {
        switch (damageTargetMode)
        {
            case CatapultDamageTargetMode.AllEnemies:
                return
                    "all enemies";


            case CatapultDamageTargetMode.RightmostEnemy:
            default:
                return
                    "the first enemy from the right";
        }
    }
}
