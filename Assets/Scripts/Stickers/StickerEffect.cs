using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewStickerEffect", menuName = "Stickers/StickerEffect")]
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
    [Min(0)]
    public int basePurchaseCost = 5;


    // =========================================================
    // EFFECT VALUES
    // =========================================================

    [Header("Reward")]
    public int dollarReward = 0;


    // =========================================================
    // GAME LOG
    // =========================================================

    [Header("Game Log")]

    [Tooltip(
        "Short English description shown after '<Sticker> activates:'. " +
        "Leave empty for money-only stickers because dollarReward is logged automatically."
    )]
    [TextArea(2, 4)]
    public string logDescription = "";


    // =========================================================
    // CLASSIC API (NO OWNER)
    // =========================================================

    public virtual void ApplyEffect()
    {
        // Invalid spins never activate sticker effects.
        if (RoundManager.Instance != null &&
            !RoundManager.Instance.WasLastSpinValid)
        {
            Debug.Log(
                $"Sticker '{stickerName}' did not activate: invalid spin."
            );

            return;
        }


        /*
         * Log BEFORE applying the gameplay result.
         *
         * This preserves causal order in the Game Log:
         *
         * Sticker activates
         * -> currency changes / enemy damage / death / etc.
         */
        LogActivation(null);


        if (CurrencyManager.Instance != null &&
            dollarReward != 0)
        {
            CurrencyManager.Instance
                .AddDollar(
                    dollarReward
                );


            Debug.Log(
                $"Sticker '{stickerName}' gave ${dollarReward}."
            );
        }
    }


    // =========================================================
    // API WITH STICKER OWNER CONTEXT
    // =========================================================

    public virtual void ApplyEffect(
        BaseSticker owner)
    {
        /*
         * Keep backwards compatibility with effects that override
         * the classic no-owner ApplyEffect() method.
         */
        ApplyEffect();
    }


    // =========================================================
    // LOG DESCRIPTION
    // =========================================================

    /// <summary>
    /// Description shown after the sticker name in the Game Log.
    /// Custom effects can override this when their text depends on
    /// runtime or inspector values.
    /// </summary>
    public virtual string GetLogDescription(
        BaseSticker owner)
    {
        return logDescription;
    }


    // =========================================================
    // LOG ACTIVATION HELPER
    // =========================================================

    /// <summary>
    /// Shared route used by this effect and custom StickerEffect subclasses.
    /// dollarReward is appended automatically by GameLogManager.
    /// </summary>
    protected void LogActivation(
        BaseSticker owner,
        string overrideDescription = null)
    {
        if (GameLogManager.Instance == null)
            return;


        string description =
            overrideDescription != null
                ? overrideDescription
                : GetLogDescription(owner);


        GameLogManager.Instance
            .LogStickerActivation(
                stickerName,
                description,
                dollarReward
            );
    }
}
