using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "EnemyAction_Dominate",
    menuName = "Hell's Roulette/Enemy Actions/Dominate"
)]
public class EnemyActionDominate : EnemyAction
{
    public enum FailureEffect
    {
        RandomInfestation,
        BloodDamage
    }


    public enum MarkedSegmentRule
    {
        MustLandOnMarked,
        MustAvoidMarked
    }


    // =========================================================
    // GAMEPLAY
    // =========================================================

    [Header("Dominate")]

    [Tooltip(
        "Defines what the three marked segments mean. " +
        "Must Land On Marked = landing outside them triggers Failure. " +
        "Must Avoid Marked = landing on one of them triggers Failure."
    )]
    public MarkedSegmentRule markedSegmentRule =
        MarkedSegmentRule.MustLandOnMarked;


    [Tooltip(
        "Consequence applied when Dominate's marked-segment rule is failed."
    )]
    public FailureEffect failureEffect =
        FailureEffect.RandomInfestation;


    [Tooltip(
        "Used only when Failure Effect = Blood Damage."
    )]
    [Min(0)]
    public int bloodDamage =
        5;


    [Tooltip(
        "Possible infestation prefabs when Failure Effect = Random Infestation. " +
        "One valid entry is selected at random each time Dominate fails. " +
        "For now this can contain only Leech; add future infestation prefabs here."
    )]
    public GameObject[] infestationPool =
        new GameObject[0];


    // =========================================================
    // SEGMENT MARK PRESENTATION
    // =========================================================

    [Header("Dominate Segment Mark")]

    [Tooltip(
        "Color used by Dominate's temporary pattern. " +
        "This mark is a separate visual layer and can coexist with Segment Block."
    )]
    public Color highlightColor =
        new Color(
            0.7f,
            0.2f,
            0.95f,
            1f
        );


    [Range(0f, 1f)]
    [Tooltip(
        "Opacity / strength of Dominate's temporary pattern."
    )]
    public float highlightStrength =
        0.55f;


    [Min(0.01f)]
    [Tooltip(
        "Density of the procedural Dominate pattern."
    )]
    public float markDensity =
        8f;


    [Range(0.01f, 0.45f)]
    [Tooltip(
        "Thickness of Dominate's procedural pattern."
    )]
    public float markWidth =
        0.12f;


    [Tooltip(
        "Pattern used when the marked segments are the ones the player SHOULD land on."
    )]
    public SegmentMesh.MarkPatternType mustLandPattern =
        SegmentMesh.MarkPatternType.Chevrons;


    [Tooltip(
        "Pattern used when the marked segments are the ones the player must AVOID."
    )]
    public SegmentMesh.MarkPatternType mustAvoidPattern =
        SegmentMesh.MarkPatternType.Checker;


    public SegmentMesh.MarkPatternType GetActiveMarkPattern()
    {
        return
            markedSegmentRule ==
                MarkedSegmentRule.MustAvoidMarked
                ? mustAvoidPattern
                : mustLandPattern;
    }


    public string GetMarkedSegmentDescription()
    {
        /*
         * Segment tooltip copy should tell the player WHAT TO DO first.
         *
         * Avoid repeating "landing here triggers / avoids Dominate" and then
         * saying AVOID / LAND HERE again on the next line. The pattern already
         * tells the player that this segment is special; the tooltip now gives
         * one direct instruction plus one explicit consequence.
         */
        switch (markedSegmentRule)
        {
            case MarkedSegmentRule.MustAvoidMarked:
                return
                    "Avoid this segment.";

            case MarkedSegmentRule.MustLandOnMarked:
            default:
                return
                    "Land on any marked segment.";
        }
    }


    public string GetMarkedSegmentStatus()
    {
        string consequence =
            GetFailureDescription();


        switch (markedSegmentRule)
        {
            case MarkedSegmentRule.MustAvoidMarked:
                return
                    $"Landing here: {consequence}.";

            case MarkedSegmentRule.MustLandOnMarked:
            default:
                return
                    $"Missing all marked segments: {consequence}.";
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


        string trigger =
            GetFailureTriggerDescription();


        string consequence =
            GetFailureDescription();


        if (!string.IsNullOrWhiteSpace(
                authored))
        {
            return
                authored
                    .Replace(
                        "{trigger}",
                        trigger
                    )
                    .Replace(
                        "{failure}",
                        consequence
                    )
                    .Replace(
                        "{blood}",
                        Mathf.Max(
                            0,
                            bloodDamage
                        )
                        .ToString()
                    );
        }


        return
            "Marks 3 segments. " +
            $"{trigger}, {consequence}.";
    }


    public string GetFailureTriggerDescription()
    {
        switch (markedSegmentRule)
        {
            case MarkedSegmentRule.MustAvoidMarked:
                return
                    "If you land on one of them";


            case MarkedSegmentRule.MustLandOnMarked:
            default:
                return
                    "If you do not land on one of them";
        }
    }


    private string GetFailureDescription()
    {
        switch (failureEffect)
        {
            case FailureEffect.BloodDamage:
                return
                    $"lose {Mathf.Max(0, bloodDamage)} Blood";


            case FailureEffect.RandomInfestation:
            default:
                return
                    "gain a random Infestation";
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


        EnemyActionDominateController controller =
            enemy.GetComponent<EnemyActionDominateController>();


        if (controller == null)
        {
            Debug.LogWarning(
                $"[DOMINATE] '{enemy.EnemyName}' needs an " +
                "EnemyActionDominateController on the same GameObject."
            );

            return;
        }


        controller.ResolveDominate(
            this
        );
    }


    // =========================================================
    // INFESTATION PICK
    // =========================================================

    public GameObject GetRandomInfestationPrefab()
    {
        if (infestationPool == null ||
            infestationPool.Length <= 0)
        {
            return null;
        }


        int validCount =
            0;


        for (int i = 0;
             i < infestationPool.Length;
             i++)
        {
            if (infestationPool[i] != null)
            {
                validCount++;
            }
        }


        if (validCount <= 0)
            return null;


        int selectedValidIndex =
            Random.Range(
                0,
                validCount
            );


        for (int i = 0;
             i < infestationPool.Length;
             i++)
        {
            if (infestationPool[i] == null)
                continue;


            if (selectedValidIndex == 0)
            {
                return
                    infestationPool[i];
            }


            selectedValidIndex--;
        }


        return null;
    }
}
