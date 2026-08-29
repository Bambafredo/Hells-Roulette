using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "EncounterPreset",
    menuName = "Hell's Roulette/Enemy Encounters/Preset"
)]
public class EnemyEncounterPreset : ScriptableObject
{
    // =========================================================
    // FORMATION
    // =========================================================

    [Header("Formation - Left to Right")]

    [Tooltip(
        "Enemy spawned in Slot_L. Leave empty for no enemy in this slot."
    )]
    public GameObject leftEnemy;

    [Tooltip(
        "Enemy spawned in Slot_C. Leave empty for no enemy in this slot."
    )]
    public GameObject centerEnemy;

    [Tooltip(
        "Enemy spawned in Slot_R. Leave empty for no enemy in this slot."
    )]
    public GameObject rightEnemy;


    // =========================================================
    // PUBLIC API
    // =========================================================

    public GameObject[] GetEnemies()
    {
        return
            new GameObject[]
            {
                leftEnemy,
                centerEnemy,
                rightEnemy
            };
    }
}
