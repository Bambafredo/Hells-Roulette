using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StickerRat",
    menuName = "Stickers/Sticker Rat"
)]
public class StickerRat : StickerEffect
{
    // =========================================================
    // DAMAGE
    // =========================================================

    [Header("Rat Damage")]

    [Tooltip(
        "Base damage dealt when this Rat activates on the winning segment."
    )]
    [Min(0)]
    public int baseDamage =
        2;


    [Tooltip(
        "Additional damage for every OTHER Rat currently placed anywhere " +
        "on the wheel."
    )]
    [Min(0)]
    public int damagePerOtherRat =
        2;


    // =========================================================
    // RAT POISON INTERACTION
    // =========================================================

    [Header("Rat Poison Interaction")]

    [Tooltip(
        "If enabled, after every VALID spin this Rat is destroyed when it " +
        "shares the same roulette segment with a Rat Poison sticker. " +
        "This check happens after the Rat's normal winning-segment effect, " +
        "so a Rat on the winning segment still gets its attack before dying."
    )]
    public bool destroyWhenSharingSegmentWithRatPoison =
        true;


    // =========================================================
    // SPIN RESOLUTION
    // =========================================================

    public override void ResolveSpinLocation(
        BaseSticker owner,
        StickerSpinLocation location)
    {
        if (owner == null)
            return;


        if (RoundManager.Instance != null &&
            !RoundManager.Instance.WasLastSpinValid)
        {
            return;
        }


        /*
         * Normal Rat gameplay effect:
         * only the winning-segment Rat attacks.
         *
         * This preserves the game's normal StickerEffect convention while
         * still letting the Rat Poison interaction happen from ANY wheel
         * segment after a valid spin.
         */
        if (location ==
            StickerSpinLocation.WinningSegment)
        {
            ResolveWinningAttack(
                owner
            );
        }


        /*
         * Album Rats are not "in the wheel", so Rat Poison cannot destroy
         * them through this rule.
         */
        if (location !=
                StickerSpinLocation.Album &&
            destroyWhenSharingSegmentWithRatPoison &&
            SharesSegmentWithRatPoison(
                owner
            ))
        {
            DestroyRatByPoison(
                owner
            );
        }
    }


    // =========================================================
    // ATTACK
    // =========================================================

    private void ResolveWinningAttack(
        BaseSticker owner)
    {
        int otherRatCount =
            CountOtherRatsInWheel(
                owner
            );


        int damage =
            Mathf.Max(
                0,
                baseDamage
            ) +
            (
                Mathf.Max(
                    0,
                    damagePerOtherRat
                ) *
                otherRatCount
            );


        EnemyPanelManager enemyPanel =
            Object.FindObjectOfType<EnemyPanelManager>();


        if (enemyPanel == null)
        {
            RegisterActivation(
                owner,
                StickerSpinLocation.WinningSegment,
                "No valid target",
                0,
                null
            );


            Debug.LogWarning(
                "StickerRat: EnemyPanelManager was not found."
            );

            return;
        }


        BaseEnemy target =
            enemyPanel.GetLeftmostAliveEnemy();


        if (target == null)
        {
            RegisterActivation(
                owner,
                StickerSpinLocation.WinningSegment,
                "No valid target",
                0,
                null
            );


            Debug.Log(
                "[RAT] No living enemy to attack."
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


        string ratBonusText =
            otherRatCount == 1
                ? "1 other Rat"
                : $"{otherRatCount} other Rats";


        RegisterActivation(
            owner,
            StickerSpinLocation.WinningSegment,
            $"Deal {damage} damage to {targetName} " +
            $"({ratBonusText})",
            0,
            null
        );


        target.TakeDamage(
            damage
        );


        Debug.Log(
            $"[RAT] Deals {damage} damage to {target.enemyName}. " +
            $"Other Rats in wheel: {otherRatCount}."
        );
    }


    // =========================================================
    // RAT COUNT
    // =========================================================

    private int CountOtherRatsInWheel(
        BaseSticker owner)
    {
        int count =
            0;


        BaseSticker[] allStickers =
            Object.FindObjectsOfType<BaseSticker>(
                true
            );


        foreach (BaseSticker sticker in
                 allStickers)
        {
            if (sticker == null ||
                sticker == owner)
            {
                continue;
            }


            /*
             * "In the wheel" means physically placed on a roulette segment.
             *
             * Album / reward / unplaced copies do not contribute.
             */
            if (!sticker.isPlaced ||
                sticker.currentSegment == null)
            {
                continue;
            }


            if (!(sticker.effect is StickerRat))
                continue;


            count++;
        }


        return count;
    }


    // =========================================================
    // RAT POISON
    // =========================================================

    private bool SharesSegmentWithRatPoison(
        BaseSticker owner)
    {
        if (owner == null ||
            !owner.isPlaced ||
            owner.currentSegment == null)
        {
            return false;
        }


        BaseSticker[] stickersInSegment =
            owner.currentSegment
                .GetComponentsInChildren<BaseSticker>(
                    true
                );


        foreach (BaseSticker sticker in
                 stickersInSegment)
        {
            if (sticker == null ||
                sticker == owner)
            {
                continue;
            }


            if (!sticker.isPlaced ||
                sticker.currentSegment !=
                    owner.currentSegment)
            {
                continue;
            }


            if (sticker.effect is StickerRatPoison)
            {
                return true;
            }
        }


        return false;
    }


    private void DestroyRatByPoison(
        BaseSticker owner)
    {
        if (owner == null)
            return;


        // -----------------------------------------------------
        // GAME LOG
        // -----------------------------------------------------

        if (GameLogManager.Instance != null)
        {
            GameLogManager.Instance
                .AddGameplayLine(
                    GameLogManager.Instance
                        .StickerText(
                            stickerName
                        ) +
                    " is destroyed by " +
                    GameLogManager.Instance
                        .StickerText(
                            "Rat Poison"
                        )
                );
        }


        // -----------------------------------------------------
        // DESTROY PHYSICAL STICKER INSTANCE
        // -----------------------------------------------------

        GameObject objectToDestroy =
            owner.stickerRoot != null
                ? owner.stickerRoot.gameObject
                : owner.gameObject;


        Debug.Log(
            $"[RAT] '{stickerName}' shared a segment with Rat Poison " +
            "and was destroyed."
        );


        /*
         * Unity destruction is deferred until end-of-frame.
         *
         * That is useful here: all Rats resolve using the board state that
         * existed when the valid spin ended, so one Rat being poisoned does
         * not incorrectly reduce another Rat's damage during this same
         * resolution.
         */
        Object.Destroy(
            objectToDestroy
        );
    }


    // =========================================================
    // TOOLTIP
    // =========================================================

    protected override bool SupportsTooltipLocation(
        StickerSpinLocation location)
    {
        /*
         * Main attack belongs to the winning segment.
         *
         * Non-winning support is also allowed so you can optionally author
         * a line explaining the Rat Poison destruction rule there.
         * Leaving the Losing Segment Tooltip empty hides that line normally.
         */
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


        int otherRats =
            owner != null
                ? CountOtherRatsInWheel(
                    owner
                )
                : 0;


        int totalDamage =
            Mathf.Max(
                0,
                baseDamage
            ) +
            (
                Mathf.Max(
                    0,
                    damagePerOtherRat
                ) *
                otherRats
            );


        return
            resolved
                .Replace(
                    "{baseDamage}",
                    Mathf.Max(
                        0,
                        baseDamage
                    ).ToString()
                )
                .Replace(
                    "{perRatDamage}",
                    Mathf.Max(
                        0,
                        damagePerOtherRat
                    ).ToString()
                )
                .Replace(
                    "{otherRats}",
                    otherRats.ToString()
                )
                .Replace(
                    "{totalDamage}",
                    totalDamage.ToString()
                );
    }


    // =========================================================
    // EDITOR SAFETY
    // =========================================================

    private void OnValidate()
    {
        baseDamage =
            Mathf.Max(
                0,
                baseDamage
            );

        damagePerOtherRat =
            Mathf.Max(
                0,
                damagePerOtherRat
            );
    }
}
