using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewStickerEffect", menuName = "Stickers/StickerEffect")]
public class StickerEffect : ScriptableObject
{
    [Header("Info")]
    public string stickerName = "Unnamed Sticker";
    public Sprite icon;

    [Header("Reward")]
    public int dollarReward = 0;

    // Aquí puedes añadir más propiedades (XP, multiplicadores, etc.)

    public virtual void ApplyEffect()
    {
        if (CurrencyManager.Instance != null && dollarReward != 0)
        {
            CurrencyManager.Instance.AddDollar(dollarReward);
            Debug.Log($"💵 Sticker '{stickerName}' dio {dollarReward}$!");
        }
    }
}
