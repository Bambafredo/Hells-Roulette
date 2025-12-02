using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "StickerWheelShifter", menuName = "Stickers/Sticker Wheel Shifter")]
public class StickerWheelShifter : StickerEffect
{
    [Header("Wheel Shifter Values")]
    public float selfIncrease = 10f;
    public float adjacentDecrease = 5f;

    public override void ApplyEffect(BaseSticker owner)
    {
        // 1. Validación de ronda (como antes)
        if (RoundManager.Instance != null && !RoundManager.Instance.WasLastSpinValid)
        {
            Debug.Log("❌ StickerWheelShifter no activa porque la ronda no fue válida.");
            return;
        }

        if (owner == null)
        {
            Debug.LogWarning("StickerWheelShifter: owner == null, no hay contexto del sticker.");
            return;
        }

        // 2. Obtener el WheelGenerator desde el owner
        WheelGenerator generator = owner.generator;
        if (generator == null)
        {
            generator = Object.FindObjectOfType<WheelGenerator>();
        }

        if (generator == null)
        {
            Debug.LogError("StickerWheelShifter: No se encontró WheelGenerator.");
            return;
        }

        if (!owner.isPlaced || owner.currentSegment == null)
        {
            Debug.LogWarning("StickerWheelShifter: Sticker no está colocado en la ruleta.");
            return;
        }

        // 3. Obtener índice del segmento actual
        int index = GetSegmentIndex(generator, owner.currentSegment);
        if (index < 0)
        {
            Debug.LogError("StickerWheelShifter: No se pudo identificar el segmento del sticker.");
            return;
        }

        int segCount = generator.segmentCount;
        if (segCount <= 0 || generator.segmentAngles == null ||
            generator.segmentAngles.Count != segCount)
        {
            Debug.LogError("StickerWheelShifter: segmentAngles no está bien configurado.");
            return;
        }

        int leftIndex = (index - 1 + segCount) % segCount;
        int rightIndex = (index + 1) % segCount;

        // 4. Aplicar cambios de ángulo
        generator.segmentAngles[index] += selfIncrease;
        generator.segmentAngles[leftIndex] -= adjacentDecrease;
        generator.segmentAngles[rightIndex] -= adjacentDecrease;

        Debug.Log(
            $"🔄 StickerWheelShifter activado en segmento {index}: +" +
            $"{selfIncrease}, -{adjacentDecrease} a {leftIndex} y {rightIndex}"
        );

        // 5. Regenerar la ruleta (esto ya hace NormalizeAngles internamente)
        generator.GenerateWheel();
    }

    private int GetSegmentIndex(WheelGenerator gen, Transform segmentTransform)
    {
        if (gen == null || gen.segments == null) return -1;

        for (int i = 0; i < gen.segments.Count; i++)
        {
            if (gen.segments[i].collider != null &&
                gen.segments[i].collider.transform == segmentTransform)
                return i;
        }
        return -1;
    }
}
