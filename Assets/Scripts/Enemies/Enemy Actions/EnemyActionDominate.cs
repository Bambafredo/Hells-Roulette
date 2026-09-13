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
    // TELEGRAPH
    // =========================================================

    [Header("Dominate Telegraph")]

    [Tooltip(
        "Color the three marked segments pulse towards."
    )]
    public Color highlightColor =
        Color.white;


    [Range(0f, 1f)]
    public float highlightStrength =
        0.18f;


    [Min(0.01f)]
    public float pulseSpeed =
        1.2f;


    [Tooltip(
        "Temporarily hide Dominate's safe-segment pulses while a sticker is dragged."
    )]
    public bool hideWhileDraggingSticker =
        true;


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
