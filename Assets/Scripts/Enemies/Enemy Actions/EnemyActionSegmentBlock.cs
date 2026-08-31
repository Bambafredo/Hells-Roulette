using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "EnemyAction_SegmentBlock",
    menuName = "Hell's Roulette/Enemy Actions/Segment Block"
)]
public class EnemyActionSegmentBlock : EnemyAction
{
    public enum SegmentBlockTargetMode
    {
        WinningSegment,
        RandomSegment
    }


    // =========================================================
    // CONFIGURATION
    // =========================================================

    [Header("Segment Block")]

    [Tooltip(
        "How many FUTURE VALID spins one application of this action adds. " +
        "The spin that creates the block does not consume one of these turns."
    )]
    [Min(1)]
    public int blockDurationSpins =
        1;


    [Tooltip(
        "Winning Segment targets the segment where the current spin landed. " +
        "Random Segment rolls one segment uniformly from the whole wheel."
    )]
    public SegmentBlockTargetMode targetMode =
        SegmentBlockTargetMode.WinningSegment;


    [Header("Stacking")]

    [Tooltip(
        "If enabled, targeting an already blocked segment ADDS more turns to " +
        "its existing block counter instead of failing. Each stack adds " +
        "Block Duration Spins."
    )]
    public bool stackable =
        false;


    [Tooltip(
        "Maximum effective stacks that can be stored in one segment. " +
        "The maximum remaining block duration is: " +
        "Block Duration Spins x Max Stacks. Ignored when Stackable is disabled."
    )]
    [Min(1)]
    public int maxStacks =
        3;


    [Header("Winning Segment Fallback")]

    [Tooltip(
        "Only used in Winning Segment mode when Stackable is DISABLED. " +
        "If the winning segment is already blocked, choose a random UNBLOCKED " +
        "segment instead. When Stackable is enabled, the winning segment is " +
        "stacked instead and this option is ignored."
    )]
    public bool fallbackToRandomUnblockedIfWinningAlreadyBlocked =
        false;


    // =========================================================
    // PUBLIC DERIVED VALUES
    // =========================================================

    public int MaxStackedDurationSpins
    {
        get
        {
            long duration =
                Mathf.Max(
                    1,
                    blockDurationSpins
                );

            long stacks =
                Mathf.Max(
                    1,
                    maxStacks
                );

            long total =
                duration *
                stacks;


            return
                total >= int.MaxValue
                    ? int.MaxValue
                    : (int)total;
        }
    }


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


        if (!string.IsNullOrWhiteSpace(
                authored))
        {
            return
                ReplaceTokens(
                    authored
                );
        }


        string duration =
            blockDurationSpins == 1
                ? "1 spin"
                : $"{blockDurationSpins} spins";


        string stackingText =
            stackable
                ? $" Repeated hits add {duration}, up to {Mathf.Max(1, maxStacks)} stacks."
                : "";


        if (targetMode ==
            SegmentBlockTargetMode.RandomSegment)
        {
            if (stackable)
            {
                return
                    $"Blocks a random segment for {duration}." +
                    stackingText;
            }


            return
                $"Attempts to block a random segment for {duration}. " +
                "If that segment is already blocked, nothing happens.";
        }


        if (stackable)
        {
            return
                $"Blocks this spin's winning segment for {duration}." +
                stackingText;
        }


        if (fallbackToRandomUnblockedIfWinningAlreadyBlocked)
        {
            return
                $"Blocks this spin's winning segment for {duration}. " +
                "If it is already blocked, blocks a random unblocked " +
                "segment instead.";
        }


        return
            $"Blocks this spin's winning segment for {duration}. " +
            "If it is already blocked, nothing happens.";
    }


    private string ReplaceTokens(
        string source)
    {
        if (string.IsNullOrEmpty(
                source))
        {
            return "";
        }


        return
            source
                .Replace(
                    "{spins}",
                    blockDurationSpins
                        .ToString()
                )
                .Replace(
                    "{maxStacks}",
                    Mathf.Max(
                        1,
                        maxStacks
                    )
                    .ToString()
                )
                .Replace(
                    "{maxTurns}",
                    MaxStackedDurationSpins
                        .ToString()
                );
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


        WheelGenerator generator =
            roulette.generator;


        int targetIndex =
            ResolveTargetSegmentIndex(
                roulette,
                generator
            );


        if (targetIndex < 0)
        {
            LogNoValidTarget(
                enemy
            );

            return;
        }


        bool alreadyBlocked =
            generator.IsSegmentBlocked(
                targetIndex
            );


        // -----------------------------------------------------
        // EXISTING BLOCK
        // -----------------------------------------------------

        if (alreadyBlocked)
        {
            if (!stackable)
            {
                LogAlreadyBlocked(
                    enemy,
                    generator,
                    targetIndex
                );

                return;
            }


            int actuallyAdded =
                generator.AddSegmentBlockSpins(
                    targetIndex,
                    Mathf.Max(
                        1,
                        blockDurationSpins
                    ),
                    MaxStackedDurationSpins
                );


            if (actuallyAdded <= 0)
            {
                LogAtMaxStacks(
                    enemy,
                    generator,
                    targetIndex
                );

                return;
            }


            LogSuccessfulStack(
                enemy,
                generator,
                targetIndex,
                actuallyAdded
            );


            return;
        }


        // -----------------------------------------------------
        // NEW BLOCK
        // -----------------------------------------------------

        bool blocked =
            generator.BlockSegment(
                targetIndex,
                Mathf.Max(
                    1,
                    blockDurationSpins
                )
            );


        if (!blocked)
        {
            /*
             * Extremely defensive fallback in case another system changed
             * the segment state between targeting and application.
             */
            if (stackable &&
                generator.IsSegmentBlocked(
                    targetIndex
                ))
            {
                int actuallyAdded =
                    generator.AddSegmentBlockSpins(
                        targetIndex,
                        Mathf.Max(
                            1,
                            blockDurationSpins
                        ),
                        MaxStackedDurationSpins
                    );


                if (actuallyAdded > 0)
                {
                    LogSuccessfulStack(
                        enemy,
                        generator,
                        targetIndex,
                        actuallyAdded
                    );

                    return;
                }


                LogAtMaxStacks(
                    enemy,
                    generator,
                    targetIndex
                );

                return;
            }


            LogAlreadyBlocked(
                enemy,
                generator,
                targetIndex
            );

            return;
        }


        LogSuccessfulBlock(
            enemy,
            generator,
            targetIndex
        );
    }


    // =========================================================
    // TARGETING
    // =========================================================

    private int ResolveTargetSegmentIndex(
        RouletteController roulette,
        WheelGenerator generator)
    {
        if (generator.segmentCount <= 0)
            return -1;


        // -----------------------------------------------------
        // RANDOM MODE
        // -----------------------------------------------------

        if (targetMode ==
            SegmentBlockTargetMode.RandomSegment)
        {
            /*
             * ALL segments are always in the random pool.
             *
             * - Unblocked result -> creates a new block.
             * - Blocked + Stackable -> adds turns.
             * - Blocked + not Stackable -> action whiffs.
             *
             * There is NEVER a reroll.
             */
            return
                Random.Range(
                    0,
                    generator.segmentCount
                );
        }


        // -----------------------------------------------------
        // WINNING SEGMENT MODE
        // -----------------------------------------------------

        int winningIndex =
            roulette.LastResolvedSegmentIndex;


        if (winningIndex < 0 ||
            winningIndex >=
                generator.segmentCount)
        {
            return -1;
        }


        if (!generator.IsSegmentBlocked(
                winningIndex))
        {
            return
                winningIndex;
        }


        /*
         * Stackable Winning Segment ALWAYS keeps the winning segment as its
         * target. The whole point is to extend that same segment's counter.
         *
         * The random fallback is therefore intentionally ignored.
         */
        if (stackable)
        {
            return
                winningIndex;
        }


        if (!fallbackToRandomUnblockedIfWinningAlreadyBlocked)
        {
            return
                winningIndex;
        }


        /*
         * Non-stackable Winning fallback:
         * choose uniformly among currently UNBLOCKED segments.
         */
        List<int> candidates =
            new List<int>();


        for (int i = 0;
             i < generator.segmentCount;
             i++)
        {
            if (!generator.IsSegmentBlocked(i))
            {
                candidates.Add(
                    i
                );
            }
        }


        if (candidates.Count == 0)
            return -1;


        return
            candidates[
                Random.Range(
                    0,
                    candidates.Count
                )
            ];
    }


    // =========================================================
    // LOGGING
    // =========================================================

    private void LogSuccessfulBlock(
        BaseEnemy enemy,
        WheelGenerator generator,
        int segmentIndex)
    {
        int duration =
            Mathf.Max(
                1,
                blockDurationSpins
            );


        if (GameLogManager.Instance != null)
        {
            string enemyLabel =
                GameLogManager.Instance
                    .EnemyText(
                        enemy.EnemyName
                    );


            string segmentLabel =
                BuildSegmentLabel(
                    generator,
                    segmentIndex
                );


            string durationLabel =
                duration == 1
                    ? "1 spin"
                    : $"{duration} spins";


            GameLogManager.Instance
                .AddGameplayLine(
                    enemyLabel +
                    " blocks " +
                    segmentLabel +
                    $" for {durationLabel}"
                );
        }


        Debug.Log(
            $"[ENEMY] {enemy.EnemyName} uses Segment Block on " +
            $"segment {segmentIndex + 1} for {duration} future valid spin(s)."
        );
    }


    private void LogSuccessfulStack(
        BaseEnemy enemy,
        WheelGenerator generator,
        int segmentIndex,
        int actuallyAdded)
    {
        int remaining =
            generator
                .GetSegmentBlockRemainingSpins(
                    segmentIndex
                );


        if (GameLogManager.Instance != null)
        {
            string enemyLabel =
                GameLogManager.Instance
                    .EnemyText(
                        enemy.EnemyName
                    );


            string segmentLabel =
                BuildSegmentLabel(
                    generator,
                    segmentIndex
                );


            string addedLabel =
                actuallyAdded == 1
                    ? "+1 spin"
                    : $"+{actuallyAdded} spins";


            GameLogManager.Instance
                .AddGameplayLine(
                    enemyLabel +
                    " stacks " +
                    segmentLabel +
                    $": {addedLabel} ({remaining} remaining)"
                );
        }


        Debug.Log(
            $"[ENEMY] {enemy.EnemyName} stacks Segment Block on " +
            $"segment {segmentIndex + 1}: +{actuallyAdded}, " +
            $"{remaining} remaining / {MaxStackedDurationSpins} max."
        );
    }


    private void LogAtMaxStacks(
        BaseEnemy enemy,
        WheelGenerator generator,
        int segmentIndex)
    {
        if (GameLogManager.Instance != null)
        {
            string enemyLabel =
                GameLogManager.Instance
                    .EnemyText(
                        enemy.EnemyName
                    );


            string segmentLabel =
                BuildSegmentLabel(
                    generator,
                    segmentIndex
                );


            GameLogManager.Instance
                .AddGameplayLine(
                    enemyLabel +
                    " targets " +
                    segmentLabel +
                    ", but Segment Block is at max stacks"
                );
        }


        Debug.Log(
            $"[ENEMY] {enemy.EnemyName}'s Segment Block cannot stack " +
            $"segment {segmentIndex + 1}: max {Mathf.Max(1, maxStacks)} " +
            $"stack(s) / {MaxStackedDurationSpins} remaining turns reached."
        );
    }


    private void LogAlreadyBlocked(
        BaseEnemy enemy,
        WheelGenerator generator,
        int segmentIndex)
    {
        if (GameLogManager.Instance != null)
        {
            string enemyLabel =
                GameLogManager.Instance
                    .EnemyText(
                        enemy.EnemyName
                    );


            string segmentLabel =
                BuildSegmentLabel(
                    generator,
                    segmentIndex
                );


            GameLogManager.Instance
                .AddGameplayLine(
                    enemyLabel +
                    " targets " +
                    segmentLabel +
                    ", but it is already blocked"
                );
        }


        Debug.Log(
            $"[ENEMY] {enemy.EnemyName}'s Segment Block fails because " +
            $"segment {segmentIndex + 1} is already blocked."
        );
    }


    private void LogNoValidTarget(
        BaseEnemy enemy)
    {
        if (GameLogManager.Instance != null)
        {
            string enemyLabel =
                GameLogManager.Instance
                    .EnemyText(
                        enemy.EnemyName
                    );


            GameLogManager.Instance
                .AddGameplayLine(
                    enemyLabel +
                    " has no available segment to block"
                );
        }


        Debug.Log(
            $"[ENEMY] {enemy.EnemyName}'s Segment Block has no valid target."
        );
    }


    private string BuildSegmentLabel(
        WheelGenerator generator,
        int segmentIndex)
    {
        if (GameLogManager.Instance == null)
        {
            return
                $"Segment {segmentIndex + 1}";
        }


        bool hasColor =
            generator != null &&
            generator.segments != null &&
            segmentIndex >= 0 &&
            segmentIndex < generator.segments.Count &&
            generator.segments[segmentIndex] != null &&
            generator.segments[segmentIndex]
                .meshComponent != null;


        if (hasColor)
        {
            return
                GameLogManager.Instance
                    .SegmentText(
                        $"Segment {segmentIndex + 1}",
                        generator.segments[segmentIndex]
                            .meshComponent.color
                    );
        }


        return
            GameLogManager.Instance
                .SegmentText(
                    $"Segment {segmentIndex + 1}"
                );
    }
}
