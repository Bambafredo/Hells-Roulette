using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "StickerWheelShifter", menuName = "Stickers/Sticker Wheel Shifter")]
public class StickerWheelShifter : StickerEffect
{
    [Header("Wheel Shifter Values")]
    public float selfIncrease = 10f;
    public float adjacentDecrease = 5f;


    public override void ApplyEffect(
        BaseSticker owner)
    {
        // -----------------------------------------------------
        // VALID SPIN
        // -----------------------------------------------------

        if (RoundManager.Instance != null &&
            !RoundManager.Instance.WasLastSpinValid)
        {
            Debug.Log(
                "StickerWheelShifter did not activate because the spin was invalid."
            );

            return;
        }


        if (owner == null)
        {
            Debug.LogWarning(
                "StickerWheelShifter: owner is null."
            );

            return;
        }


        // -----------------------------------------------------
        // GENERATOR
        // -----------------------------------------------------

        WheelGenerator generator =
            owner.generator;


        if (generator == null)
        {
            generator =
                Object.FindObjectOfType<WheelGenerator>();
        }


        if (generator == null)
        {
            Debug.LogError(
                "StickerWheelShifter: WheelGenerator was not found."
            );

            return;
        }


        if (!owner.isPlaced ||
            owner.currentSegment == null)
        {
            Debug.LogWarning(
                "StickerWheelShifter: Sticker is not placed on the roulette."
            );

            return;
        }


        // -----------------------------------------------------
        // SEGMENT INDEX
        // -----------------------------------------------------

        int index =
            GetSegmentIndex(
                generator,
                owner.currentSegment
            );


        if (index < 0)
        {
            Debug.LogError(
                "StickerWheelShifter: Could not identify its segment."
            );

            return;
        }


        int segCount =
            generator.segmentCount;


        if (segCount <= 0 ||
            generator.segmentAngles == null ||
            generator.segmentAngles.Count != segCount)
        {
            Debug.LogError(
                "StickerWheelShifter: segmentAngles are not correctly configured."
            );

            return;
        }


        int leftIndex =
            (index - 1 + segCount) %
            segCount;


        int rightIndex =
            (index + 1) %
            segCount;


        // -----------------------------------------------------
        // GAME LOG
        // -----------------------------------------------------

        LogActivation(
            owner,
            $"Segment +{FormatAngle(selfIncrease)}°, " +
            $"adjacent segments -{FormatAngle(adjacentDecrease)}°"
        );


        // -----------------------------------------------------
        // APPLY ANGLE CHANGES
        // -----------------------------------------------------

        generator.segmentAngles[index] +=
            selfIncrease;

        generator.segmentAngles[leftIndex] -=
            adjacentDecrease;

        generator.segmentAngles[rightIndex] -=
            adjacentDecrease;


        Debug.Log(
            $"StickerWheelShifter activated on segment {index}: " +
            $"+{selfIncrease}, -{adjacentDecrease} on {leftIndex} and {rightIndex}."
        );


        // -----------------------------------------------------
        // REGENERATE WHEEL
        // -----------------------------------------------------

        generator.GenerateWheel();
    }


    // =========================================================
    // SEGMENT INDEX
    // =========================================================

    private int GetSegmentIndex(
        WheelGenerator gen,
        Transform segmentTransform)
    {
        if (gen == null ||
            gen.segments == null)
        {
            return -1;
        }


        for (int i = 0;
             i < gen.segments.Count;
             i++)
        {
            if (gen.segments[i].collider != null &&
                gen.segments[i].collider.transform == segmentTransform)
            {
                return i;
            }
        }


        return -1;
    }


    // =========================================================
    // DISPLAY
    // =========================================================

    private string FormatAngle(
        float value)
    {
        return
            Mathf.Approximately(
                value,
                Mathf.Round(value)
            )
                ? Mathf.RoundToInt(value).ToString()
                : value.ToString("0.##");
    }
}
