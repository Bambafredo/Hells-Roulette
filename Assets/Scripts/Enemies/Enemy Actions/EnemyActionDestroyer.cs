using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "EnemyAction_Destroyer",
    menuName = "Hell's Roulette/Enemy Actions/Destroyer"
)]
public class EnemyActionDestroyer : EnemyAction
{
    public enum DestroyTargetMode
    {
        RandomWheelSticker,
        RandomWinningSegmentSticker
    }


    // =========================================================
    // CONFIGURATION
    // =========================================================

    [Header("Destroyer")]

    [Tooltip(
        "Random Wheel Sticker = destroys one random sticker currently placed " +
        "anywhere on the roulette. " +
        "Random Winning Segment Sticker = destroys one random sticker currently " +
        "placed in the segment where this spin landed."
    )]
    public DestroyTargetMode targetMode =
        DestroyTargetMode.RandomWheelSticker;


    // =========================================================
    // TOOLTIP
    // =========================================================

    public override string GetTooltipDescription(
        BaseEnemy enemy)
    {
        string authored =
            base.GetTooltipDescription(
                enemy
            );


        string targetDescription =
            GetTargetDescription();


        /*
         * Optional authored token:
         *
         * {target}
         *
         * Random Wheel Sticker:
         * "a random sticker on the wheel"
         *
         * Random Winning Segment Sticker:
         * "a random sticker on this spin's winning segment"
         */
        if (!string.IsNullOrWhiteSpace(
                authored))
        {
            return
                authored.Replace(
                    "{target}",
                    targetDescription
                );
        }


        return
            $"Destroy {targetDescription}.";
    }


    private string GetTargetDescription()
    {
        switch (targetMode)
        {
            case DestroyTargetMode.RandomWinningSegmentSticker:
                return
                    "a random sticker on this spin's winning segment";


            case DestroyTargetMode.RandomWheelSticker:
            default:
                return
                    "a random sticker on the wheel";
        }
    }


    // =========================================================
    // EXECUTION
    // =========================================================

    public override void Execute(
        BaseEnemy enemy)
    {
        if (enemy == null ||
            enemy.IsDead)
        {
            return;
        }


        RouletteController roulette =
            RouletteController.Instance != null
                ? RouletteController.Instance
                : Object.FindObjectOfType<RouletteController>();


        if (roulette == null ||
            roulette.generator == null)
        {
            Debug.LogWarning(
                "[DESTROYER] RouletteController / WheelGenerator was not found."
            );

            return;
        }


        WheelGenerator generator =
            roulette.generator;


        int winningSegmentIndex =
            roulette.LastResolvedSegmentIndex;


        /*
         * Winning Segment mode depends on the physical segment resolved by the
         * spin that just finished.
         */
        if (targetMode ==
                DestroyTargetMode.RandomWinningSegmentSticker &&
            (winningSegmentIndex < 0 ||
             winningSegmentIndex >= generator.segmentCount))
        {
            Debug.LogWarning(
                $"[DESTROYER] {enemy.EnemyName}: winning segment index " +
                $"{winningSegmentIndex} is invalid."
            );

            return;
        }


        List<BaseSticker> candidates =
            CollectCandidates(
                generator,
                winningSegmentIndex
            );


        if (candidates.Count <= 0)
        {
            LogNoTarget(
                enemy,
                winningSegmentIndex
            );

            return;
        }


        BaseSticker target =
            candidates[
                Random.Range(
                    0,
                    candidates.Count
                )
            ];


        string stickerName =
            GetStickerDisplayName(
                target
            );


        bool destroyed =
            target.DestroyFromGameplay(
                $"{enemy.EnemyName}'s Destroyer"
            );


        /*
         * Extremely defensive: another gameplay system may have marked the
         * chosen sticker for destruction between candidate collection and the
         * actual call.
         */
        if (!destroyed)
        {
            Debug.Log(
                $"[DESTROYER] {enemy.EnemyName}: selected '{stickerName}', " +
                "but it was no longer a valid destruction target."
            );

            return;
        }


        LogSuccessfulDestruction(
            enemy,
            stickerName,
            winningSegmentIndex
        );
    }


    // =========================================================
    // TARGET COLLECTION
    // =========================================================

    private List<BaseSticker> CollectCandidates(
        WheelGenerator generator,
        int winningSegmentIndex)
    {
        List<BaseSticker> candidates =
            new List<BaseSticker>();


        /*
         * Gather broadly, then filter by the sticker's existing logical
         * placement state.
         *
         * This deliberately avoids coupling Destroyer to wheel hierarchy
         * details and automatically excludes Album / Bag / Draft / Reward /
         * Infestation-offer stickers.
         */
        BaseSticker[] allStickers =
            Object.FindObjectsOfType<BaseSticker>(
                true
            );


        foreach (BaseSticker sticker in allStickers)
        {
            if (!IsValidWheelSticker(
                    sticker))
            {
                continue;
            }


            if (targetMode ==
                DestroyTargetMode.RandomWinningSegmentSticker)
            {
                int stickerSegmentIndex =
                    generator.GetSegmentIndex(
                        sticker.currentSegment
                    );


                if (stickerSegmentIndex !=
                    winningSegmentIndex)
                {
                    continue;
                }
            }


            candidates.Add(
                sticker
            );
        }


        return
            candidates;
    }


    private bool IsValidWheelSticker(
        BaseSticker sticker)
    {
        if (sticker == null)
            return false;


        if (sticker.IsConsumed ||
            sticker.IsPendingGameplayDestruction)
        {
            return false;
        }


        /*
         * isPlaced + currentSegment is the project's existing logical
         * definition of a sticker currently living on the roulette.
         */
        if (!sticker.isPlaced ||
            sticker.currentSegment == null)
        {
            return false;
        }


        return true;
    }


    // =========================================================
    // LOGGING
    // =========================================================

    private void LogSuccessfulDestruction(
        BaseEnemy enemy,
        string stickerName,
        int winningSegmentIndex)
    {
        string enemyName =
            enemy != null &&
            !string.IsNullOrWhiteSpace(
                enemy.EnemyName
            )
                ? enemy.EnemyName
                : "Enemy";


        if (GameLogManager.Instance != null)
        {
            GameLogManager.Instance
                .AddGameplayLine(
                    GameLogManager.Instance
                        .EnemyText(
                            enemyName
                        ) +
                    " destroys " +
                    GameLogManager.Instance
                        .StickerText(
                            stickerName
                        )
                );
        }


        string targetText =
            targetMode ==
                DestroyTargetMode.RandomWinningSegmentSticker
                ? $"winning Segment {winningSegmentIndex + 1}"
                : "the wheel";


        Debug.Log(
            $"[DESTROYER] {enemyName} destroys '{stickerName}' from {targetText}."
        );
    }


    private void LogNoTarget(
        BaseEnemy enemy,
        int winningSegmentIndex)
    {
        string enemyName =
            enemy != null &&
            !string.IsNullOrWhiteSpace(
                enemy.EnemyName
            )
                ? enemy.EnemyName
                : "Enemy";


        if (targetMode ==
            DestroyTargetMode.RandomWinningSegmentSticker)
        {
            Debug.Log(
                $"[DESTROYER] {enemyName}: no valid sticker exists on " +
                $"winning Segment {winningSegmentIndex + 1}. Nothing happens."
            );

            return;
        }


        Debug.Log(
            $"[DESTROYER] {enemyName}: no valid sticker exists on the wheel. " +
            "Nothing happens."
        );
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
}
