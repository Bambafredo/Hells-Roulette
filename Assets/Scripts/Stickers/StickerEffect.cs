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
    // TOOLTIP AUTHORING
    // =========================================================

    [Header("Tooltip")]

    [Tooltip(
        "English text shown for this sticker when it is on the WINNING segment. " +
        "Leave empty to hide this line. " +
        "Generic tokens available: {stickerName}, {dollarReward}."
    )]
    [TextArea(2, 5)]
    public string winningSegmentTooltip = "";

    [Tooltip(
        "English text shown for this sticker when it is on a LOSING roulette segment. " +
        "Leave empty to hide this line. Custom stickers may expose extra dynamic tokens."
    )]
    [TextArea(2, 5)]
    public string losingSegmentTooltip = "";

    [Tooltip(
        "English text shown for this sticker when it is in the ALBUM. " +
        "Leave empty to hide this line. Custom stickers may expose extra dynamic tokens."
    )]
    [TextArea(2, 5)]
    public string albumTooltip = "";


    // =========================================================
    // SPIN LOCATION PREPARATION
    // =========================================================

    /// <summary>
    /// Optional pre-resolution phase called for EVERY sticker in the spin
    /// snapshot before ANY normal sticker effect resolves.
    ///
    /// Most stickers do nothing here.
    ///
    /// Passive reaction effects such as Shield use this phase so their
    /// protection is already active before another sticker (for example Rat
    /// Poison) can deal damage during normal sticker resolution.
    /// </summary>
    public virtual void PrepareSpinLocation(
        BaseSticker owner,
        StickerSpinLocation location)
    {
    }


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
    // TOOLTIP API
    // =========================================================

    /// <summary>
    /// Ordinary StickerEffects only have gameplay behaviour on the
    /// winning segment. Location-aware custom stickers override this.
    /// </summary>
    protected virtual bool SupportsTooltipLocation(
        StickerSpinLocation location)
    {
        return
            location ==
            StickerSpinLocation.WinningSegment;
    }


    /// <summary>
    /// Returns the author-written template for a location.
    /// Empty text means that line is intentionally hidden.
    /// </summary>
    public string GetTooltipTemplate(
        StickerSpinLocation location)
    {
        switch (location)
        {
            case StickerSpinLocation.WinningSegment:
                return winningSegmentTooltip;

            case StickerSpinLocation.NonWinningSegment:
                return losingSegmentTooltip;

            case StickerSpinLocation.Album:
                return albumTooltip;
        }

        return "";
    }


    public virtual bool HasTooltipEffect(
        BaseSticker owner,
        StickerSpinLocation location)
    {
        if (!SupportsTooltipLocation(location))
            return false;

        return
            !string.IsNullOrWhiteSpace(
                GetTooltipTemplate(location)
            );
    }


    /// <summary>
    /// Gets the final tooltip description after replacing dynamic tokens.
    ///
    /// The wording itself stays editable in the ScriptableObject.
    /// Custom sticker classes only provide the VALUES for their tokens.
    /// </summary>
    public virtual string GetTooltipDescription(
        BaseSticker owner,
        StickerSpinLocation location)
    {
        if (!HasTooltipEffect(
            owner,
            location))
        {
            return "";
        }

        string template =
            GetTooltipTemplate(location);

        return
            ResolveTooltipTokens(
                owner,
                location,
                template
            );
    }


    /// <summary>
    /// Generic tokens shared by every sticker.
    ///
    /// Custom stickers should call base and then replace their own tokens.
    /// Unknown tokens are deliberately left untouched, which makes authoring
    /// mistakes visible instead of silently deleting information.
    /// </summary>
    protected virtual string ResolveTooltipTokens(
        BaseSticker owner,
        StickerSpinLocation location,
        string template)
    {
        if (string.IsNullOrEmpty(template))
            return "";

        return
            template
                .Replace(
                    "{stickerName}",
                    stickerName ?? ""
                )
                .Replace(
                    "{dollarReward}",
                    dollarReward.ToString()
                );
    }


    // =========================================================
    // USE CONSUMPTION QUERY
    // =========================================================

    public virtual bool ShouldConsumeUseOnActivation(
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
