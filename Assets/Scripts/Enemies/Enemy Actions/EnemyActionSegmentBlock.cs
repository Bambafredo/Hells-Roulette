using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "EnemyAction_SegmentBlock",
    menuName = "Hell's Roulette/Enemy Actions/Segment Block"
)]
public class EnemyActionSegmentBlock : EnemyAction
{
    // =========================================================
    // TOOLTIP
    // =========================================================

    public override string GetTooltipDescription(
        BaseEnemy enemy)
    {
        string description =
            base.GetTooltipDescription(
                enemy
            );


        if (!string.IsNullOrWhiteSpace(
                description))
        {
            return
                description;
        }


        return
            "Blocks this spin's winning segment until the end of the round. " +
            "Stickers inside a blocked segment cannot activate or be moved.";
    }


    // =========================================================
    // EXECUTION
    // =========================================================

    public override void Execute(
        BaseEnemy enemy)
    {
        if (enemy == null)
            return;


        RouletteController roulette =
            Object.FindObjectOfType<RouletteController>();


        if (roulette == null ||
            roulette.generator == null)
        {
            Debug.LogWarning(
                "[SEGMENT BLOCK] RouletteController / WheelGenerator was not found."
            );

            return;
        }


        int segmentIndex =
            roulette.LastResolvedSegmentIndex;


        if (segmentIndex < 0 ||
            segmentIndex >=
                roulette.generator.segmentCount)
        {
            Debug.LogWarning(
                "[SEGMENT BLOCK] Could not identify the completed spin's segment."
            );

            return;
        }


        bool newlyBlocked =
            roulette.generator
                .BlockSegment(
                    segmentIndex
                );


        // -----------------------------------------------------
        // GAME LOG
        // -----------------------------------------------------

        if (GameLogManager.Instance != null)
        {
            int displaySegmentNumber =
                segmentIndex + 1;


            string segmentLabel;

            bool hasSegmentColor =
                roulette.generator.segments != null &&
                segmentIndex <
                    roulette.generator.segments.Count &&
                roulette.generator.segments[segmentIndex] != null &&
                roulette.generator.segments[segmentIndex]
                    .meshComponent != null;


            if (hasSegmentColor)
            {
                segmentLabel =
                    GameLogManager.Instance
                        .SegmentText(
                            $"Segment {displaySegmentNumber}",
                            roulette.generator
                                .segments[segmentIndex]
                                .meshComponent.color
                        );
            }
            else
            {
                segmentLabel =
                    GameLogManager.Instance
                        .SegmentText(
                            $"Segment {displaySegmentNumber}"
                        );
            }


            string enemyLabel =
                GameLogManager.Instance
                    .EnemyText(
                        enemy.EnemyName
                    );


            if (newlyBlocked)
            {
                GameLogManager.Instance
                    .AddGameplayLine(
                        enemyLabel +
                        " blocks " +
                        segmentLabel +
                        " until the end of the round"
                    );
            }
            else
            {
                GameLogManager.Instance
                    .AddGameplayLine(
                        enemyLabel +
                        " targets " +
                        segmentLabel +
                        ", but it is already blocked"
                    );
            }
        }


        Debug.Log(
            $"[ENEMY] {enemy.EnemyName} uses Segment Block on " +
            $"segment {segmentIndex + 1}. New block = {newlyBlocked}."
        );
    }
}
