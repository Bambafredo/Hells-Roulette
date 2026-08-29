using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyEncounterGenerator : MonoBehaviour
{
    // =========================================================
    // GENERATION MODE
    // =========================================================

    public enum GenerationMode
    {
        EnemyPool,
        PresetPool
    }


    [Header("Generation Mode")]

    public GenerationMode generationMode =
        GenerationMode.EnemyPool;


    // =========================================================
    // ROUND WEIGHT SCHEDULE
    // =========================================================

    [System.Serializable]
    public class RoundWeightStage
    {
        [Tooltip(
            "Playable round from which this weight becomes active. " +
            "1 = first round / UI R-0, 2 = second / UI R-1, etc."
        )]
        [Min(1)]
        public int fromRound =
            1;


        [Tooltip(
            "Relative selection weight from this round onward, until a later " +
            "stage overrides it. Set Weight = 0 to remove this entry from " +
            "the pool from that round onward."
        )]
        [Min(0f)]
        public float weight =
            1f;
    }


    // =========================================================
    // ENEMY POOL MODE
    // =========================================================

    [System.Serializable]
    public class EnemyPoolEntry
    {
        [Tooltip("Enemy prefab that can be rolled.")]
        public GameObject enemyPrefab;


        [Tooltip(
            "Weight progression for this enemy. The latest stage whose " +
            "From Round has been reached determines the current weight. " +
            "Before the first stage, the enemy is not in the pool."
        )]
        public List<RoundWeightStage> weightSchedule =
            new List<RoundWeightStage>()
            {
                new RoundWeightStage()
            };
    }


    [Header("Enemy Pool Mode")]

    public List<EnemyPoolEntry> enemyPool =
        new List<EnemyPoolEntry>();


    // =========================================================
    // PRESET POOL MODE
    // =========================================================

    [System.Serializable]
    public class PresetPoolEntry
    {
        [Tooltip(
            "Authored encounter formation. Its Left / Center / Right order " +
            "is preserved exactly."
        )]
        public EnemyEncounterPreset preset;


        [Tooltip(
            "Weight progression for this preset. The latest stage whose " +
            "From Round has been reached determines the current weight. " +
            "Before the first stage, the preset is unavailable."
        )]
        public List<RoundWeightStage> weightSchedule =
            new List<RoundWeightStage>()
            {
                new RoundWeightStage()
            };
    }


    [Header("Preset Pool Mode")]

    public List<PresetPoolEntry> presetPool =
        new List<PresetPoolEntry>();


    // =========================================================
    // RESULT
    // =========================================================

    public class GeneratedEncounter
    {
        public GameObject[] enemies;

        /*
         * Enemy Pool:
         * false -> Corridor applies RowPlacementPriority.
         *
         * Preset Pool:
         * true -> authored Left / Center / Right order is preserved.
         */
        public bool preserveSlotOrder;
    }


    // =========================================================
    // PUBLIC API
    // =========================================================

    public GeneratedEncounter GenerateEncounter(
        int playableRound,
        int slotCount)
    {
        int safeRound =
            Mathf.Max(
                1,
                playableRound
            );


        int safeSlotCount =
            Mathf.Max(
                0,
                slotCount
            );


        switch (generationMode)
        {
            case GenerationMode.PresetPool:

                return
                    GenerateFromPresetPool(
                        safeRound,
                        safeSlotCount
                    );


            case GenerationMode.EnemyPool:
            default:

                return
                    GenerateFromEnemyPool(
                        safeRound,
                        safeSlotCount
                    );
        }
    }


    // =========================================================
    // ENEMY POOL
    // =========================================================

    private GeneratedEncounter GenerateFromEnemyPool(
        int playableRound,
        int slotCount)
    {
        GameObject[] enemies =
            new GameObject[
                slotCount
            ];


        for (int i = 0;
             i < slotCount;
             i++)
        {
            enemies[i] =
                RollEnemyPrefab(
                    playableRound
                );
        }


        return
            new GeneratedEncounter
            {
                enemies =
                    enemies,

                preserveSlotOrder =
                    false
            };
    }


    private GameObject RollEnemyPrefab(
        int playableRound)
    {
        if (enemyPool == null ||
            enemyPool.Count == 0)
        {
            Debug.LogWarning(
                "[ENCOUNTER GENERATOR] Enemy Pool is empty."
            );

            return null;
        }


        List<WeightedEnemyEntry> eligible =
            new List<WeightedEnemyEntry>();


        foreach (EnemyPoolEntry entry in
                 enemyPool)
        {
            if (entry == null ||
                entry.enemyPrefab == null)
            {
                continue;
            }


            float activeWeight =
                GetWeightForRound(
                    entry.weightSchedule,
                    playableRound
                );


            if (activeWeight <= 0f)
                continue;


            eligible.Add(
                new WeightedEnemyEntry
                {
                    entry =
                        entry,

                    weight =
                        activeWeight
                }
            );
        }


        if (eligible.Count == 0)
        {
            Debug.LogWarning(
                $"[ENCOUNTER GENERATOR] No Enemy Pool entry has positive " +
                $"weight for playable round {playableRound} " +
                $"(UI R-{playableRound - 1})."
            );

            return null;
        }


        WeightedEnemyEntry rolled =
            WeightedRoll(
                eligible,
                item => item.weight
            );


        return
            rolled != null &&
            rolled.entry != null
                ? rolled.entry.enemyPrefab
                : null;
    }


    private class WeightedEnemyEntry
    {
        public EnemyPoolEntry entry;
        public float weight;
    }


    // =========================================================
    // PRESET POOL
    // =========================================================

    private GeneratedEncounter GenerateFromPresetPool(
        int playableRound,
        int slotCount)
    {
        if (presetPool == null ||
            presetPool.Count == 0)
        {
            Debug.LogWarning(
                "[ENCOUNTER GENERATOR] Preset Pool is empty."
            );

            return
                EmptyEncounter(
                    slotCount,
                    true
                );
        }


        List<WeightedPresetEntry> eligible =
            new List<WeightedPresetEntry>();


        foreach (PresetPoolEntry entry in
                 presetPool)
        {
            if (entry == null ||
                entry.preset == null)
            {
                continue;
            }


            float activeWeight =
                GetWeightForRound(
                    entry.weightSchedule,
                    playableRound
                );


            if (activeWeight <= 0f)
                continue;


            eligible.Add(
                new WeightedPresetEntry
                {
                    entry =
                        entry,

                    weight =
                        activeWeight
                }
            );
        }


        if (eligible.Count == 0)
        {
            Debug.LogWarning(
                $"[ENCOUNTER GENERATOR] No Preset Pool entry has positive " +
                $"weight for playable round {playableRound} " +
                $"(UI R-{playableRound - 1})."
            );

            return
                EmptyEncounter(
                    slotCount,
                    true
                );
        }


        WeightedPresetEntry rolled =
            WeightedRoll(
                eligible,
                item => item.weight
            );


        if (rolled == null ||
            rolled.entry == null ||
            rolled.entry.preset == null)
        {
            return
                EmptyEncounter(
                    slotCount,
                    true
                );
        }


        GameObject[] authored =
            rolled.entry.preset
                .GetEnemies();


        GameObject[] result =
            new GameObject[
                slotCount
            ];


        int copyCount =
            Mathf.Min(
                slotCount,
                authored.Length
            );


        for (int i = 0;
             i < copyCount;
             i++)
        {
            result[i] =
                authored[i];
        }


        Debug.Log(
            $"[ENCOUNTER GENERATOR] Preset " +
            $"'{rolled.entry.preset.name}' selected for playable round " +
            $"{playableRound} (UI R-{playableRound - 1}) " +
            $"with active weight {rolled.weight}."
        );


        return
            new GeneratedEncounter
            {
                enemies =
                    result,

                preserveSlotOrder =
                    true
            };
    }


    private class WeightedPresetEntry
    {
        public PresetPoolEntry entry;
        public float weight;
    }


    // =========================================================
    // ROUND WEIGHT SCHEDULE
    // =========================================================

    private float GetWeightForRound(
        List<RoundWeightStage> schedule,
        int playableRound)
    {
        if (schedule == null ||
            schedule.Count == 0)
        {
            return 0f;
        }


        /*
         * The ACTIVE weight is the stage with the highest From Round that
         * has already been reached.
         *
         * The Inspector list does NOT need to be manually sorted.
         *
         * Example:
         *
         * From 1 -> Weight 1.0
         * From 4 -> Weight 2.0
         * From 8 -> Weight 0.0
         *
         * Rounds 1-3: weight 1
         * Rounds 4-7: weight 2
         * Round 8+:   unavailable
         */
        RoundWeightStage activeStage =
            null;

        int activeFromRound =
            int.MinValue;


        foreach (RoundWeightStage stage in
                 schedule)
        {
            if (stage == null)
                continue;


            int stageRound =
                Mathf.Max(
                    1,
                    stage.fromRound
                );


            if (stageRound >
                playableRound)
            {
                continue;
            }


            if (stageRound <
                activeFromRound)
            {
                continue;
            }


            activeStage =
                stage;

            activeFromRound =
                stageRound;
        }


        if (activeStage == null)
            return 0f;


        return
            Mathf.Max(
                0f,
                activeStage.weight
            );
    }


    // =========================================================
    // WEIGHTED ROLL
    // =========================================================

    private T WeightedRoll<T>(
        List<T> entries,
        System.Func<T, float> getWeight)
        where T : class
    {
        if (entries == null ||
            entries.Count == 0)
        {
            return null;
        }


        float totalWeight =
            0f;


        foreach (T entry in entries)
        {
            totalWeight +=
                Mathf.Max(
                    0f,
                    getWeight(
                        entry
                    )
                );
        }


        if (totalWeight <= 0f)
            return null;


        float roll =
            Random.Range(
                0f,
                totalWeight
            );


        float cumulative =
            0f;


        foreach (T entry in entries)
        {
            cumulative +=
                Mathf.Max(
                    0f,
                    getWeight(
                        entry
                    )
                );


            if (roll <=
                cumulative)
            {
                return entry;
            }
        }


        return
            entries[
                entries.Count - 1
            ];
    }


    // =========================================================
    // HELPERS
    // =========================================================

    private GeneratedEncounter EmptyEncounter(
        int slotCount,
        bool preserveSlotOrder)
    {
        return
            new GeneratedEncounter
            {
                enemies =
                    new GameObject[
                        Mathf.Max(
                            0,
                            slotCount
                        )
                    ],

                preserveSlotOrder =
                    preserveSlotOrder
            };
    }
}
