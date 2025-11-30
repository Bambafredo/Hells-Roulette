using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "StickerWheelShifter", menuName = "Stickers/Sticker Wheel Shifter")]
public class StickerWheelShifter : StickerEffect
{
    [Header("Wheel Shifter Values")]
    public float selfIncrease = 10f;
    public float adjacentDecrease = 5f;

    public override void ApplyEffect()
    {
        // 1. Validación de ronda
        if (RoundManager.Instance != null && !RoundManager.Instance.WasLastSpinValid)
        {
            Debug.Log("❌ StickerWheelShifter no activa porque la ronda no fue válida.");
            return;
        }

        // 2. Encontrar el sticker que lo activó
        // Como funciona 1 sticker = 1 SegmentWin = 1 ApplyEffect(),
        // el que lo active será el que está en el segmento ganador
        BaseSticker sticker = FindTriggeringSticker();

        if (sticker == null)
        {
            Debug.LogWarning("StickerWheelShifter: No se pudo identificar el sticker que lo activó.");
            return;
        }

        WheelGenerator generator = sticker.generator;

        if (generator == null)
        {
            Debug.LogError("StickerWheelShifter: No se encontró WheelGenerator.");
            return;
        }

        if (!sticker.isPlaced || sticker.currentSegment == null)
        {
            Debug.LogWarning("StickerWheelShifter: Sticker no está colocado en la ruleta.");
            return;
        }

        // 3. Obtener índice del segmento actual
        int index = GetSegmentIndex(generator, sticker.currentSegment);
        if (index < 0)
        {
            Debug.LogError("StickerWheelShifter: No se pudo identificar el segmento del sticker.");
            return;
        }

        int leftIndex = (index - 1 + generator.segmentCount) % generator.segmentCount;
        int rightIndex = (index + 1) % generator.segmentCount;

        // 4. Aplicar cambios
        generator.segmentAngles[index] += selfIncrease;
        generator.segmentAngles[leftIndex] -= adjacentDecrease;
        generator.segmentAngles[rightIndex] -= adjacentDecrease;

        Debug.Log(
            $"🔄 StickerWheelShifter activado en segmento {index}: +" +
            $"{selfIncrease}, -{adjacentDecrease} a {leftIndex} y {rightIndex}"
        );

        // 5. Regenerar la ruleta
        generator.GenerateWheel();
    }

    private BaseSticker FindTriggeringSticker()
    {
        // Busca todos los stickers en escena
        BaseSticker[] all = GameObject.FindObjectsOfType<BaseSticker>();

        foreach (var s in all)
        {
            if (s.isPlaced && s.currentSegment != null)
            {
                // Si está en el segmento ganador, la ruleta ya habrá disparado OnSegmentWin en él
                // y ApplyEffect se está ejecutando para su propio ScriptableObject
                if (s.effect == this)
                    return s;
            }
        }
        return null;
    }

    private int GetSegmentIndex(WheelGenerator gen, Transform segmentTransform)
    {
        for (int i = 0; i < gen.segments.Count; i++)
        {
            if (gen.segments[i].collider != null &&
                gen.segments[i].collider.transform == segmentTransform)
                return i;
        }
        return -1;
    }
}
