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
    // ENEMY POOL MODE
    // =========================================================

    [System.Serializable]
    public class EnemyPoolEntry
    {
        [Tooltip("Enemy prefab that can be rolled.")]
        public GameObject enemyPrefab;

        [Tooltip(
            "First PLAYABLE round in which this enemy enters the pool. " +
            "1 = first round / UI R-0, 2 = second / UI R-1, etc."
        )]
        [Min(1)]
        public int firstRound =
            1;

        [Tooltip(
            "Relative chance of this enemy being rolled once it is eligible."
        )]
        [Min(0f)]
        public float weight =
            1f;
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
            "First PLAYABLE round in which this preset can be selected. " +
            "1 = first round / UI R-0."
        )]
        [Min(1)]
        public int firstRound =
            1;

        [Tooltip(
            "Last PLAYABLE round in which this preset can be selected. " +
            "0 = no maximum."
        )]
        [Min(0)]
        public int lastRound =
            0;

        [Tooltip(
            "Relative chance of this preset being selected while eligible."
        )]
        [Min(0f)]
        public float weight =
            1f;
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


        List<EnemyPoolEntry> eligible =
            new List<EnemyPoolEntry>();


        foreach (EnemyPoolEntry entry in
                 enemyPool)
        {
            if (entry == null ||
                entry.enemyPrefab == null ||
                entry.weight <= 0f ||
                playableRound <
                    Mathf.Max(
                        1,
                        entry.firstRound
                    ))
            {
                continue;
            }


            eligible.Add(
                entry
            );
        }


        if (eligible.Count == 0)
        {
            Debug.LogWarning(
                $"[ENCOUNTER GENERATOR] No Enemy Pool entry is eligible " +
                $"for playable round {playableRound} " +
                $"(UI R-{playableRound - 1})."
            );

            return null;
        }


        EnemyPoolEntry rolled =
            WeightedRoll(
                eligible,
                entry => entry.weight
            );


        return
            rolled != null
                ? rolled.enemyPrefab
                : null;
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


        List<PresetPoolEntry> eligible =
            new List<PresetPoolEntry>();


        foreach (PresetPoolEntry entry in
                 presetPool)
        {
            if (entry == null ||
                entry.preset == null ||
                entry.weight <= 0f)
            {
                continue;
            }


            int first =
                Mathf.Max(
                    1,
                    entry.firstRound
                );


            int last =
                Mathf.Max(
                    0,
                    entry.lastRound
                );


            if (playableRound <
                first)
            {
                continue;
            }


            if (last > 0 &&
                playableRound >
                last)
            {
                continue;
            }


            eligible.Add(
                entry
            );
        }


        if (eligible.Count == 0)
        {
            Debug.LogWarning(
                $"[ENCOUNTER GENERATOR] No Preset Pool entry is eligible " +
                $"for playable round {playableRound} " +
                $"(UI R-{playableRound - 1})."
            );

            return
                EmptyEncounter(
                    slotCount,
                    true
                );
        }


        PresetPoolEntry rolled =
            WeightedRoll(
                eligible,
                entry => entry.weight
            );


        if (rolled == null ||
            rolled.preset == null)
        {
            return
                EmptyEncounter(
                    slotCount,
                    true
                );
        }


        GameObject[] authored =
            rolled.preset.GetEnemies();


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
            $"[ENCOUNTER GENERATOR] Preset '{rolled.preset.name}' selected " +
            $"for playable round {playableRound} " +
            $"(UI R-{playableRound - 1})."
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
