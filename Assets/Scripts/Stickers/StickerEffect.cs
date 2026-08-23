using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "NewStickerEffect",
    menuName = "Stickers/StickerEffect"
)]
public class StickerEffect : ScriptableObject
{
    // =========================================================
    // INFO
    // =========================================================

    [Header("Info")]
    public string stickerName = "Unnamed Sticker";
    public Sprite icon;


    // =========================================================
    // SHOP
    // =========================================================

    [Header("Shop")]

    [Tooltip(
        "Precio base del sticker en Reward Screen. " +
        "El precio real puede ser multiplicado según " +
        "cuántos stickers se hayan comprado durante esta fase."
    )]
    [Min(0)]
    public int basePurchaseCost = 5;


    // =========================================================
    // REWARD
    // =========================================================

    [Header("Reward")]
    public int dollarReward = 0;


    // =========================================================
    // CLASSIC API
    // =========================================================

    public virtual void ApplyEffect()
    {
        // Si hay RoundManager y la última tirada NO es válida,
        // no damos recompensa.
        if (RoundManager.Instance != null &&
            !RoundManager.Instance.WasLastSpinValid)
        {
            Debug.Log(
                $"Sticker '{stickerName}' no otorga recompensa: " +
                $"ronda no válida."
            );

            return;
        }

        if (CurrencyManager.Instance != null &&
            dollarReward != 0)
        {
            CurrencyManager.Instance.AddDollar(
                dollarReward
            );

            Debug.Log(
                $"💵 Sticker '{stickerName}' dio " +
                $"{dollarReward}$!"
            );
        }
    }


    // =========================================================
    // OWNER API
    // =========================================================

    public virtual void ApplyEffect(
        BaseSticker owner)
    {
        /*
         * Implementación por defecto:
         * ignoramos owner y usamos el comportamiento clásico.
         */
        ApplyEffect();
    }
}
