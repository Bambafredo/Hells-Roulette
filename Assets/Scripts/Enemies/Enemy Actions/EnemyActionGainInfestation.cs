using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "EnemyAction_GainInfestation",
    menuName = "Hell's Roulette/Enemy Actions/Gain Infestation"
)]
public class EnemyActionGainInfestation : EnemyAction
{
    // =========================================================
    // CONFIGURATION
    // =========================================================

    [Header("Gain Infestation")]

    [Tooltip(
        "Exact Infestation sticker prefab granted when this enemy action resolves."
    )]
    public GameObject infestationPrefab;


    [Tooltip(
        "Number of copies of this Infestation granted when the action resolves."
    )]
    [Range(1, 3)]
    public int infestationCount =
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


        int count =
            Mathf.Clamp(
                infestationCount,
                1,
                3
            );


        string infestationName =
            GetInfestationDisplayName(
                infestationPrefab
            );


        /*
         * Supported tokens:
         *
         * {count}
         * {infestation}
         *
         * Recommended authored tooltip:
         *
         * Gain {count}x {infestation}.
         */
        if (!string.IsNullOrWhiteSpace(
                authored))
        {
            return
                authored
                    .Replace(
                        "{count}",
                        count.ToString()
                    )
                    .Replace(
                        "{infestation}",
                        infestationName
                    );
        }


        return
            count == 1
                ? $"Gain a {infestationName}."
                : $"Gain {count}x {infestationName}.";
    }


    // =========================================================
    // EXECUTION
    // =========================================================

    public override void Execute(
        BaseEnemy enemy)
    {
        int count =
            Mathf.Clamp(
                infestationCount,
                1,
                3
            );


        if (infestationPrefab == null)
        {
            Debug.LogWarning(
                "[GAIN INFESTATION] No Infestation Prefab is assigned."
            );

            return;
        }


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
                "[GAIN INFESTATION] InfestationManager was not found."
            );

            return;
        }


        bool requested =
            manager.RequestInfestation(
                infestationPrefab,
                count
            );


        if (!requested)
        {
            Debug.LogWarning(
                $"[GAIN INFESTATION] Could not grant {count}x " +
                $"'{infestationPrefab.name}'."
            );

            return;
        }


        string enemyName =
            enemy != null &&
            !string.IsNullOrWhiteSpace(
                enemy.EnemyName
            )
                ? enemy.EnemyName
                : "Enemy";


        string infestationName =
            GetInfestationDisplayName(
                infestationPrefab
            );


        LogInfestation(
            enemyName,
            infestationName,
            count
        );


        Debug.Log(
            $"[GAIN INFESTATION] {enemyName} grants " +
            $"{count}x '{infestationName}'."
        );
    }


    // =========================================================
    // DISPLAY NAME
    // =========================================================

    private string GetInfestationDisplayName(
        GameObject prefab)
    {
        if (prefab == null)
            return "Infestation";


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


    // =========================================================
    // GAME LOG
    // =========================================================

    private void LogInfestation(
        string enemyName,
        string infestationName,
        int count)
    {
        if (GameLogManager.Instance == null)
            return;


        string infestationText =
            count <= 1
                ? infestationName
                : $"{count}x {infestationName}";


        GameLogManager.Instance
            .AddGameplayLine(
                GameLogManager.Instance
                    .EnemyText(
                        enemyName
                    ) +
                " infests you with " +
                GameLogManager.Instance
                    .StickerText(
                        infestationText
                    )
            );
    }
}
