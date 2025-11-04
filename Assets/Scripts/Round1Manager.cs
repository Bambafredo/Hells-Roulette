using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Round1Manager : MonoBehaviour
{
    [Header("Refs")]
    public RouletteController controller; // Arrastra el de la escena
    public FlagPin flagPin;               // Tu pin manual (uno en Round1)

    [Header("Goal")]
    public int minHits = 4;
    public int maxHits = 8;

    private int hitsThisSpin = 0;
    private bool counting = false;

    void Awake()
    {
        if (controller != null)
        {
            controller.OnSpinStart += HandleSpinStart;
            controller.OnSpinEnd += HandleSpinEnd;
        }

        if (flagPin != null)
            flagPin.round = this; // Cablear automáticamente
    }

    void OnDestroy()
    {
        if (controller != null)
        {
            controller.OnSpinStart -= HandleSpinStart;
            controller.OnSpinEnd -= HandleSpinEnd;
        }
    }

    void HandleSpinStart()
    {
        hitsThisSpin = 0;
        counting = true;
        Debug.Log("⏱️ Nueva tirada: empezamos a contar impactos al FlagPin");
    }

    void HandleSpinEnd()
    {
        counting = false;
        bool win = hitsThisSpin >= minHits && hitsThisSpin <= maxHits;

        if (win)
            Debug.Log($"✅ WIN: {hitsThisSpin} impactos (objetivo {minHits}-{maxHits})");
        else
            Debug.Log($"❌ LOSE: {hitsThisSpin} impactos (objetivo {minHits}-{maxHits})");

        // Aquí ya podrías avanzar de ronda, mostrar UI, etc.
    }

    public void RegisterPinHit(FlagPin p)
    {
        if (!counting) return; // Solo cuenta durante la tirada
        hitsThisSpin++;

        // Opcional: feedback por cada impacto
        // Debug.Log($"💥 Hit #{hitsThisSpin} en {p.name}");
    }
}