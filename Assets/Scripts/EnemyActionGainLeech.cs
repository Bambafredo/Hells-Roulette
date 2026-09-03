using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "EnemyAction_GainLeech",
    menuName = "Hell's Roulette/Enemy Actions/Gain Leech"
)]
public class EnemyActionGainLeech : EnemyAction
{
    // =========================================================
    // CONFIGURATION
    // =========================================================

    [Header("Gain Leech")]

    [Tooltip(
        "Number of forced Leeches granted when this enemy action resolves."
    )]
    [Range(1, 3)]
    public int leechCount =
        1;


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
                authored
                    .Replace(
                        "{leeches}",
                        Mathf.Clamp(
                            leechCount,
                            1,
                            3
                        )
                        .ToString()
                    );
        }


        int count =
            Mathf.Clamp(
                leechCount,
                1,
                3
            );


        return
            count == 1
                ? "Gain a Leech."
                : $"Gain {count} Leeches.";
    }


    // =========================================================
    // EXECUTION
    // =========================================================

    public override void Execute(
        BaseEnemy enemy)
    {
        int count =
            Mathf.Clamp(
                leechCount,
                1,
                3
            );


        InfestationManager manager =
            InfestationManager.Instance;


        if (manager == null)
        {
            manager =
                Object.FindObjectOfType<InfestationManager>(
                    true
                );
        }


        if (manager == null)
        {
            Debug.LogWarning(
                "[GAIN LEECH] InfestationManager was not found."
            );

            return;
        }


        bool requested =
            manager.RequestInfestation(
                count
            );


        if (!requested)
        {
            Debug.LogWarning(
                $"[GAIN LEECH] Could not grant {count} Leech(es)."
            );

            return;
        }


        string enemyName =
            enemy != null
                ? enemy.EnemyName
                : "Enemy";


        Debug.Log(
            $"[GAIN LEECH] {enemyName} grants {count} forced Leech(es)."
        );
    }
}
