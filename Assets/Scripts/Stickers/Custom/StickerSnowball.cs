using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StickerSnowball",
    menuName = "Stickers/Sticker Snowball"
)]
public class StickerSnowball : StickerEffect
{
    public enum DamageScalingMode
    {
        Multiply,
        Sequence
    }


    // =========================================================
    // CONFIGURATION
    // =========================================================

    [Header("Snowball")]

    [Tooltip(
        "Trample damage of a fresh physical Snowball. " +
        "Each physical copy keeps its own current damage."
    )]
    [Min(0)]
    public int startingDamage =
        1;

    [Tooltip(
        "How Snowball chooses its next damage value after a winning activation."
    )]
    public DamageScalingMode damageScalingMode =
        DamageScalingMode.Multiply;

    [Tooltip(
        "Used in Multiply mode. Multiplier applied to this physical Snowball's " +
        "damage after every winning activation. 2 = double its current damage."
    )]
    [Min(1)]
    public int damageMultiplier =
        2;

    [Tooltip(
        "Used in Sequence mode. Damage values used after the starting damage, " +
        "in order. Example with Starting Damage = 1 and Sequence = 2, 4, 7, 11, 16: " +
        "1 -> 2 -> 4 -> 7 -> 11 -> 16. Once the last value is reached, damage stays there."
    )]
    public int[] damageSequence =
        new int[]
        {
            2,
            4,
            7,
            11,
            16
        };

    [Tooltip(
        "Percentage by which the physical sticker grows after every winning " +
        "activation. Example: 20 means current scale x 1.20."
    )]
    [Min(0f)]
    public float sizeGrowthPercent =
        20f;


    private const string CurrentDamageKey =
        "Snowball.CurrentDamage";

    private const string InitializedKey =
        "Snowball.Initialized";

    private const string GrowthCountKey =
        "Snowball.GrowthCount";


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
        {
            Debug.LogWarning(
                "StickerSnowball: BaseSticker owner was not provided."
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
                ResolveNonWinningSegment(
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
    // WINNING SEGMENT
    // =========================================================

    private void ResolveWinningSegment(
        BaseSticker owner)
    {
        int damageBeforeGrowth =
            GetCurrentDamage(
                owner
            );


        int actualDamageDealt =
            DealTrampleDamage(
                owner,
                damageBeforeGrowth
            );


        int nextDamage =
            GetNextDamage(
                owner,
                damageBeforeGrowth
            );


        owner.SetRuntimeInt(
            CurrentDamageKey,
            nextDamage
        );


        /*
         * Grow the physical sticker itself. Because BaseSticker's placement
         * collider lives on the same physical sticker hierarchy, the collider
         * grows with the visual instead of becoming desynchronised.
         */
        GrowPhysicalSticker(
            owner
        );

        owner.SetRuntimeInt(
            GrowthCountKey,
            GetGrowthCount(owner) + 1
        );


        RegisterActivation(
            owner,
            StickerSpinLocation.WinningSegment,
            $"Deal {actualDamageDealt} Trample damage. " +
            $"Snowball grows by {Mathf.Max(0f, sizeGrowthPercent):0.##}% " +
            $"and damage increases to {nextDamage}",
            0,
            false
        );


        /*
         * Winning-use consumption is optional and uses the existing generic
         * StickerEffect Inspector toggle.
         *
         * Default design: Consume Use On Winning = OFF.
         * Turn it ON on the SO to test the alternate version without code.
         */
        if (ShouldConsumeUseOnActivation(
                StickerSpinLocation.WinningSegment
            ))
        {
            owner.ConsumeUseAfterActivation();
        }


        /*
         * Growth can make a previously valid placement invalid by crossing
         * the segment boundary or overlapping another sticker.
         *
         * Reuse the project's existing global placement authority instead of
         * implementing Snowball-specific geometry. This will hard-lock normal
         * roulette input exactly like any other invalid sticker placement until
         * the player fixes it.
         */
        if (!owner.IsPendingGameplayDestruction &&
            (!owner.HasLimitedUses ||
             owner.RemainingUses > 0))
        {
            Physics2D.SyncTransforms();

            if (StickerPlacementValidator.Instance != null)
            {
                StickerPlacementValidator.Instance
                    .NotifyStickerDropped(
                        owner
                    );
            }
        }


        Debug.Log(
            $"[SNOWBALL] Dealt {actualDamageDealt} Trample damage. " +
            $"Damage: {damageBeforeGrowth} -> {nextDamage}."
        );

        Debug.Log(
            $"[SNOWBALL] Snowball grows by " +
            $"{Mathf.Max(0f, sizeGrowthPercent):0.##}%."
        );
    }


    // =========================================================
    // LOSING SEGMENT
    // =========================================================

    private void ResolveNonWinningSegment(
        BaseSticker owner)
    {
        ResolveUnsafeStorageLocation(
            owner,
            StickerSpinLocation.NonWinningSegment
        );
    }


    // =========================================================
    // ALBUM
    // =========================================================

    private void ResolveAlbum(
        BaseSticker owner)
    {
        ResolveUnsafeStorageLocation(
            owner,
            StickerSpinLocation.Album
        );
    }


    /*
     * A fresh Snowball is safe in Losing / Album.
     *
     * Once it has grown at least once, however, parking it outside the winning
     * segment would allow the player to bank a highly scaled Snowball for a
     * later round or boss. In either Losing or Album, a grown Snowball therefore
     * loses all remaining uses.
     */
    private void ResolveUnsafeStorageLocation(
        BaseSticker owner,
        StickerSpinLocation location)
    {
        if (owner == null ||
            !HasGrown(owner))
        {
            return;
        }


        int usesBefore =
            owner.HasLimitedUses
                ? Mathf.Max(
                    0,
                    owner.RemainingUses
                )
                : 0;


        RegisterActivation(
            owner,
            location,
            owner.HasLimitedUses
                ? $"Grown Snowball loses all remaining uses ({usesBefore})"
                : "Grown Snowball loses all remaining uses",
            0,
            false
        );


        ConsumeAllRemainingUses(
            owner
        );
    }


    // =========================================================
    // TRAMPLE DAMAGE
    // =========================================================

    private int DealTrampleDamage(
        BaseSticker owner,
        int requestedDamage)
    {
        EnemyPanelManager enemyPanel =
            Object.FindObjectOfType<EnemyPanelManager>();


        if (enemyPanel == null)
        {
            Debug.LogWarning(
                "StickerSnowball: EnemyPanelManager was not found."
            );

            return 0;
        }


        int remainingDamage =
            Mathf.Max(
                0,
                requestedDamage
            );

        int totalDamageDealt =
            0;


        while (remainingDamage > 0)
        {
            BaseEnemy target =
                StickerTargetingUtility.GetFirstDamageTarget(
                    enemyPanel,
                    owner,
                    StickerSpinLocation.WinningSegment
                );


            if (target == null ||
                target.IsDead)
            {
                break;
            }


            int targetHP =
                Mathf.Max(
                    0,
                    target.CurrentHP
                );


            if (targetHP <= 0)
                break;


            int damageToThisEnemy =
                Mathf.Min(
                    remainingDamage,
                    targetHP
                );


            target.TakeDamage(
                damageToThisEnemy
            );


            remainingDamage -=
                damageToThisEnemy;

            totalDamageDealt +=
                damageToThisEnemy;


            /*
             * If the target survived, every point of available Snowball damage
             * was spent on it. If it died, the loop asks the existing targeting
             * system for the next leftmost eligible enemy and carries on.
             */
            if (!target.IsDead)
                break;
        }


        return totalDamageDealt;
    }


    // =========================================================
    // GROWTH
    // =========================================================

    private void GrowPhysicalSticker(
        BaseSticker owner)
    {
        if (owner == null)
            return;


        Transform root =
            owner.stickerRoot != null
                ? owner.stickerRoot
                : owner.transform;


        if (root == null)
            return;


        float safeGrowthPercent =
            Mathf.Max(
                0f,
                sizeGrowthPercent
            );


        float scaleMultiplier =
            1f +
            safeGrowthPercent / 100f;


        root.localScale *=
            scaleMultiplier;
    }


    // =========================================================
    // USES
    // =========================================================

    private void ConsumeAllRemainingUses(
        BaseSticker owner)
    {
        if (owner == null)
            return;


        if (!owner.HasLimitedUses)
        {
            /*
             * Snowball is designed as a limited-use sticker. If the SO is
             * accidentally configured as unlimited, Losing still fulfils its
             * design promise by removing the sticker.
             */
            owner.DestroyFromGameplay(
                "Snowball lost while configured with unlimited uses"
            );

            return;
        }


        while (owner.RemainingUses > 0 &&
               !owner.IsPendingGameplayDestruction)
        {
            owner.ConsumeUseAfterActivation(
                false
            );
        }
    }


    // =========================================================
    // RUNTIME DAMAGE
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
            CurrentDamageKey,
            Mathf.Max(
                0,
                startingDamage
            )
        );


        owner.SetRuntimeInt(
            GrowthCountKey,
            0
        );


        owner.SetRuntimeInt(
            InitializedKey,
            1
        );
    }


    private int GetNextDamage(
        BaseSticker owner,
        int currentDamage)
    {
        int safeCurrentDamage =
            Mathf.Max(
                0,
                currentDamage
            );


        if (damageScalingMode ==
            DamageScalingMode.Multiply)
        {
            return
                Mathf.Max(
                    0,
                    safeCurrentDamage *
                    Mathf.Max(
                        1,
                        damageMultiplier
                    )
                );
        }


        int growthCount =
            GetGrowthCount(
                owner
            );


        if (damageSequence == null ||
            damageSequence.Length == 0)
        {
            return safeCurrentDamage;
        }


        int sequenceIndex =
            Mathf.Clamp(
                growthCount,
                0,
                damageSequence.Length - 1
            );


        return
            Mathf.Max(
                0,
                damageSequence[
                    sequenceIndex
                ]
            );
    }


    private int GetGrowthCount(
        BaseSticker owner)
    {
        if (owner == null)
            return 0;


        EnsureInitialized(
            owner
        );


        return
            Mathf.Max(
                0,
                owner.GetRuntimeInt(
                    GrowthCountKey,
                    0
                )
            );
    }


    private bool HasGrown(
        BaseSticker owner)
    {
        return
            GetGrowthCount(owner) > 0;
    }


    private int GetCurrentDamage(
        BaseSticker owner)
    {
        if (owner == null)
        {
            return
                Mathf.Max(
                    0,
                    startingDamage
                );
        }


        EnsureInitialized(
            owner
        );


        return
            Mathf.Max(
                0,
                owner.GetRuntimeInt(
                    CurrentDamageKey,
                    startingDamage
                )
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


        int currentDamage =
            GetCurrentDamage(
                owner
            );

        int nextDamage =
            GetNextDamage(
                owner,
                currentDamage
            );


        return
            resolved
                .Replace(
                    "{damage}",
                    currentDamage
                        .ToString()
                )
                .Replace(
                    "{nextDamage}",
                    nextDamage
                        .ToString()
                )
                .Replace(
                    "{multiplier}",
                    Mathf.Max(
                        1,
                        damageMultiplier
                    )
                    .ToString()
                )
                .Replace(
                    "{growth}",
                    Mathf.Max(
                        0f,
                        sizeGrowthPercent
                    )
                    .ToString("0.##")
                );
    }
}
