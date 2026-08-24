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
        "How many use-consuming activations this sticker survives before being destroyed. " +
        "0 = unlimited uses. 1 = destroyed after its first consumed use."
    )]
    [Min(0)]
    public int maxUses = 0;


    [Header("Use Consumption")]

    [Tooltip(
        "If enabled, an activation while this sticker is on the winning segment consumes one use."
    )]
    public bool consumeUseOnWinningActivation = true;

    [Tooltip(
        "If enabled, an activation while this sticker is on a non-winning roulette segment consumes one use."
    )]
    public bool consumeUseOnNonWinningActivation = false;

    [Tooltip(
        "If enabled, an activation while this sticker is in the Album consumes one use."
    )]
    public bool consumeUseOnAlbumActivation = false;


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
    // SPIN LOCATION RESOLUTION
    // =========================================================

    /// <summary>
    /// Generic location-aware entry point called once for this sticker
    /// when a valid spin finishes.
    ///
    /// Default behaviour intentionally preserves every sticker we already
    /// have: ordinary stickers only activate on the winning segment.
    ///
    /// Stateful / conditional stickers can override this method and react
    /// differently to WinningSegment, NonWinningSegment or Album.
    /// </summary>
    public virtual void ResolveSpinLocation(
        BaseSticker owner,
        StickerSpinLocation location)
    {
        if (location !=
            StickerSpinLocation.WinningSegment)
        {
            return;
        }

        ApplyEffect(owner);
    }


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
         * Ordinary StickerEffects are winning-segment effects.
         * RegisterActivation writes the effect first, then consumes a use
         * according to the winning-location consumption setting.
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
    // USE CONSUMPTION QUERY
    // =========================================================

    public bool ShouldConsumeUseOnActivation(
        StickerSpinLocation location)
    {
        switch (location)
        {
            case StickerSpinLocation.WinningSegment:
                return consumeUseOnWinningActivation;

            case StickerSpinLocation.NonWinningSegment:
                return consumeUseOnNonWinningActivation;

            case StickerSpinLocation.Album:
                return consumeUseOnAlbumActivation;
        }

        return false;
    }


    // =========================================================
    // ACTIVATION REGISTRATION - BACKWARDS-COMPATIBLE
    // =========================================================

    /// <summary>
    /// Existing custom stickers call RegisterActivation(owner, description).
    /// Keep that API intact and interpret it as a winning-segment activation.
    /// </summary>
    protected void RegisterActivation(
        BaseSticker owner,
        string overrideDescription = null)
    {
        RegisterActivation(
            owner,
            StickerSpinLocation.WinningSegment,
            overrideDescription,
            null,
            null
        );
    }


    // =========================================================
    // ACTIVATION REGISTRATION - LOCATION AWARE
    // =========================================================

    /// <summary>
    /// Shared location-aware activation route.
    ///
    /// Exact order:
    /// 1. Write the activation to the Game Log.
    /// 2. Optionally consume one use from the physical sticker instance.
    /// 3. The custom effect continues and applies its gameplay consequence.
    ///
    /// overrideDollarReward lets stateful stickers log a dynamic money value
    /// without putting that value in the shared ScriptableObject.
    ///
    /// consumeUseOverride is optional. If null, the Inspector toggle for the
    /// supplied location decides whether this activation consumes a use.
    /// </summary>
    protected void RegisterActivation(
        BaseSticker owner,
        StickerSpinLocation location,
        string overrideDescription = null,
        int? overrideDollarReward = null,
        bool? consumeUseOverride = null)
    {
        string description =
            overrideDescription != null
                ? overrideDescription
                : GetLogDescription(owner);

        int logDollarReward =
            overrideDollarReward.HasValue
                ? overrideDollarReward.Value
                : dollarReward;


        if (GameLogManager.Instance != null)
        {
            GameLogManager.Instance
                .LogStickerActivation(
                    stickerName,
                    description,
                    logDollarReward
                );
        }


        bool shouldConsumeUse =
            consumeUseOverride.HasValue
                ? consumeUseOverride.Value
                : ShouldConsumeUseOnActivation(
                    location
                );


        if (shouldConsumeUse)
        {
            owner?
                .ConsumeUseAfterActivation();
        }
    }


    // =========================================================
    // BACKWARDS-COMPATIBLE ACTIVATION ALIAS
    // =========================================================

    /// <summary>
    /// Existing custom stickers that use LogActivation() continue to compile.
    /// It remains a winning-segment activation by default.
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
