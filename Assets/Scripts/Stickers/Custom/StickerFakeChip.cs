using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StickerFakeChip",
    menuName = "Stickers/Sticker Fake Chip"
)]
public class StickerFakeChip : StickerEffect
{
    // =========================================================
    // EFFECT
    // =========================================================

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
                "StickerFakeChip did not activate because the spin was invalid."
            );

            return;
        }


        // -----------------------------------------------------
        // REQUIRED CONTEXT
        // -----------------------------------------------------

        if (owner == null)
        {
            Debug.LogWarning(
                "StickerFakeChip: BaseSticker owner was not provided."
            );

            return;
        }


        if (RoundManager.Instance == null)
        {
            Debug.LogWarning(
                "StickerFakeChip: RoundManager was not found."
            );

            return;
        }


        // -----------------------------------------------------
        // ACTIVATION + USE
        // -----------------------------------------------------

        /*
         * RegisterActivation logs the effect first and then consumes one
         * use from this physical sticker instance.
         *
         * For Fake Chip, configure maxUses = 1 in its StickerEffect asset.
         */
        RegisterActivation(
            owner,
            "Gain 1 extra spin"
        );


        // -----------------------------------------------------
        // EXTRA SPIN
        // -----------------------------------------------------

        RoundManager.Instance
            .AddTokens(1);


        Debug.Log(
            "StickerFakeChip granted 1 extra spin."
        );


        /*
         * No Destroy() here.
         *
         * BaseSticker owns the generic limited-use lifecycle and destroys
         * the physical sticker automatically when remainingUses reaches 0.
         */
    }
}
