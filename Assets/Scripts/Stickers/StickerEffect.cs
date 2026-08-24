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
    // USES
    // =========================================================

    [Header("Uses")]

    [Tooltip(
        "How many activations this sticker survives before being destroyed. " +
        "0 = unlimited uses. 1 = consumed after its first activation."
    )]
    [Min(0)]
    public int maxUses = 0;


    public bool HasLimitedUses =>
        maxUses > 0;


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
        ApplyDefaultEffect(
            null
        );
    }


    // =========================================================
    // API WITH STICKER OWNER CONTEXT
    // =========================================================

    public virtual void ApplyEffect(
        BaseSticker owner)
    {
        ApplyDefaultEffect(
            owner
        );
    }


    // =========================================================
    // DEFAULT EFFECT
    // =========================================================

    private void ApplyDefaultEffect(
        BaseSticker owner)
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
         * Register the activation BEFORE applying the gameplay result.
         *
         * This gives us the causal log order:
         *
         * Sticker activates
         * Sticker uses remaining
         * -> currency / damage / other consequences
         */
        RegisterActivation(
            owner
        );


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
    // ACTIVATION REGISTRATION
    // =========================================================

    /// <summary>
    /// Shared route used by this effect and custom StickerEffect subclasses.
    ///
    /// It does two things, in this exact order:
    ///
    /// 1. Writes the activation to the Game Log.
    /// 2. Consumes one use from the physical BaseSticker instance.
    ///
    /// Custom effects should call this ONCE when they have confirmed that
    /// the sticker genuinely activates.
    /// </summary>
    protected void RegisterActivation(
        BaseSticker owner,
        string overrideDescription = null)
    {
        string description =
            overrideDescription != null
                ? overrideDescription
                : GetLogDescription(owner);


        if (GameLogManager.Instance != null)
        {
            GameLogManager.Instance
                .LogStickerActivation(
                    stickerName,
                    description,
                    dollarReward
                );
        }


        /*
         * Use state belongs to the physical BaseSticker instance,
         * never to this ScriptableObject.
         */
        owner?
            .ConsumeUseAfterActivation();
    }


    // =========================================================
    // BACKWARDS-COMPATIBLE ACTIVATION ALIAS
    // =========================================================

    /// <summary>
    /// Kept so existing custom stickers that already used LogActivation()
    /// continue to compile. It now routes through RegisterActivation(),
    /// so limited-use stickers also work correctly.
    ///
    /// New custom effects should prefer RegisterActivation().
    /// </summary>
    protected void LogActivation(
        BaseSticker owner,
        string overrideDescription = null)
    {
        RegisterActivation(
            owner,
            overrideDescription
        );
    }
}
