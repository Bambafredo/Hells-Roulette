using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StickerBuckler",
    menuName = "Stickers/Sticker Buckler"
)]
public class StickerBuckler : StickerEffect
{
    // =========================================================
    // CONFIGURATION
    // =========================================================

    [Header("Buckler")]

    [Tooltip(
        "Damage prevented during this spin when Buckler is on the winning " +
        "segment and the spin is NOT a Lucky Shot."
    )]
    [Min(0)]
    public int blockAmount =
        3;


    // =========================================================
    // PREPARATION
    // =========================================================

    /// <summary>
    /// Winning Segment only.
    ///
    /// Manual / Power:
    ///     Registers a normal cumulative Block pool.
    ///
    /// Lucky Shot:
    ///     Registers a one-shot parry instead. The first enemy attack that
    ///     reaches this registration is fully redirected to its attacker.
    ///     The parry then disappears, so a second enemy attack resolves
    ///     normally (subject to any other blockers).
    ///
    /// Because both variants register through BloodManager's shared ordered
    /// protection list, Buckler respects the exact same sticker activation
    /// order as Shield and Knight's Helmet.
    /// </summary>
    public override void PrepareSpinLocation(
        BaseSticker owner,
        StickerSpinLocation location)
    {
        if (RoundManager.Instance != null &&
            !RoundManager.Instance.WasLastSpinValid)
        {
            return;
        }


        if (owner == null ||
            BloodManager.Instance == null ||
            location != StickerSpinLocation.WinningSegment)
        {
            return;
        }


        if (IsLuckyShot())
        {
            BloodManager.Instance
                .RegisterSpinDamageRedirector(
                    owner,
                    redirectEvent =>
                        HandleDamageRedirected(
                            owner,
                            redirectEvent
                        )
                );

            return;
        }


        int capacity =
            Mathf.Max(
                0,
                blockAmount
            );


        if (capacity <= 0)
            return;


        BloodManager.Instance
            .RegisterSpinDamageBlocker(
                owner,
                capacity,
                blockEvent =>
                    HandleDamagePrevented(
                        owner,
                        blockEvent
                    )
            );
    }


    // =========================================================
    // NORMAL RESOLUTION
    // =========================================================

    /// <summary>
    /// Reactive sticker: registration happens during PrepareSpinLocation and
    /// the actual activation happens only if incoming damage reaches it.
    /// </summary>
    public override void ResolveSpinLocation(
        BaseSticker owner,
        StickerSpinLocation location)
    {
    }


    // =========================================================
    // NORMAL BLOCK CALLBACK
    // =========================================================

    private void HandleDamagePrevented(
        BaseSticker owner,
        BloodManager.DamageBlockEvent blockEvent)
    {
        if (owner == null ||
            blockEvent.preventedDamage <= 0)
        {
            return;
        }


        string safeStickerName =
            GetSafeStickerName();

        int preventedDamage =
            blockEvent.preventedDamage;

        int remainingCapacity =
            blockEvent.remainingCapacity;


        /*
         * Reactive ordering:
         *
         * Damage source
         * Buckler blocks...
         * Burnout exhausts Buckler... (if relevant)
         * Buckler uses remaining...
         */
        BloodManager.Instance?
            .QueueDeferredDamageFeedback(
                () =>
                {
                    if (GameLogManager.Instance == null)
                        return;


                    string preventedText =
                        GameLogManager.Instance
                            .ProtectionText(
                                $"{preventedDamage} damage"
                            );

                    string remainingText =
                        GameLogManager.Instance
                            .ProtectionText(
                                $"{remainingCapacity} block"
                            );


                    GameLogManager.Instance
                        .AddGameplayLine(
                            GameLogManager.Instance
                                .StickerText(
                                    safeStickerName
                                ) +
                            " blocks: " +
                            preventedText +
                            " (" +
                            remainingText +
                            " remaining this spin)"
                        );
                }
            );


        if (blockEvent.firstPreventionForBlocker &&
            HasLimitedUses)
        {
            owner.ConsumeUseAfterActivation(
                false
            );


            int usesRemainingAfterActivation =
                owner.RemainingUses;


            QueueDeferredUsesRemaining(
                safeStickerName,
                usesRemainingAfterActivation
            );
        }
    }


    // =========================================================
    // LUCKY SHOT PARRY CALLBACK
    // =========================================================

    private void HandleDamageRedirected(
        BaseSticker owner,
        BloodManager.DamageRedirectEvent redirectEvent)
    {
        if (owner == null ||
            redirectEvent.redirectedDamage <= 0 ||
            redirectEvent.attacker == null)
        {
            return;
        }


        string safeStickerName =
            GetSafeStickerName();

        int redirectedDamage =
            redirectEvent.redirectedDamage;

        string attackerName =
            string.IsNullOrWhiteSpace(
                redirectEvent.attacker.EnemyName
            )
                ? "Enemy"
                : redirectEvent.attacker.EnemyName;


        /*
         * Gameplay reflection already happened inside BloodManager, but the
         * presentation waits until BaseEnemy has logged the original attack.
         * This preserves the existing causal log order used by Shield/Helmet.
         */
        BloodManager.Instance?
            .QueueDeferredDamageFeedback(
                () =>
                {
                    if (GameLogManager.Instance == null)
                        return;


                    GameLogManager.Instance
                        .AddGameplayLine(
                            GameLogManager.Instance
                                .StickerText(
                                    safeStickerName
                                ) +
                            " parries: " +
                            GameLogManager.Instance
                                .ProtectionText(
                                    $"{redirectedDamage} damage"
                                ) +
                            " redirected to " +
                            GameLogManager.Instance
                                .EnemyText(
                                    attackerName
                                )
                        );
                }
            );


        /*
         * The parry is one-shot, so this callback can only happen once for this
         * registration. Consume the sticker use only when an enemy attack was
         * actually redirected.
         */
        if (HasLimitedUses)
        {
            owner.ConsumeUseAfterActivation(
                false
            );


            int usesRemainingAfterActivation =
                owner.RemainingUses;


            QueueDeferredUsesRemaining(
                safeStickerName,
                usesRemainingAfterActivation
            );
        }
    }


    private void QueueDeferredUsesRemaining(
        string safeStickerName,
        int usesRemaining)
    {
        BloodManager.Instance?
            .QueueDeferredDamageFeedback(
                () =>
                {
                    if (GameLogManager.Instance == null)
                        return;


                    GameLogManager.Instance
                        .AddGameplayLine(
                            GameLogManager.Instance
                                .StickerText(
                                    safeStickerName
                                ) +
                            $" uses remaining: {usesRemaining}"
                        );
                }
            );
    }


    // =========================================================
    // USE CONSUMPTION
    // =========================================================

    public override bool ShouldConsumeUseOnActivation(
        StickerSpinLocation location)
    {
        return
            location ==
                StickerSpinLocation.WinningSegment;
    }


    protected override bool DefaultShowsUseConsumptionTag(
        StickerSpinLocation location)
    {
        if (location ==
            StickerSpinLocation.WinningSegment)
        {
            /*
             * Buckler only spends a use if it actually blocks or parries
             * incoming damage, so the generic unconditional tag would be
             * misleading.
             */
            return false;
        }


        return
            base.DefaultShowsUseConsumptionTag(
                location
            );
    }


    // =========================================================
    // HELPERS
    // =========================================================

    private bool IsLuckyShot()
    {
        RouletteController roulette =
            RouletteController.Instance;


        return
            roulette != null &&
            roulette.SpinInProgress &&
            roulette.CurrentSpinMethod ==
                RouletteController.SpinMethod.LuckyShot;
    }


    private string GetSafeStickerName()
    {
        return
            string.IsNullOrWhiteSpace(
                stickerName
            )
                ? "Buckler"
                : stickerName;
    }


    // =========================================================
    // TOOLTIP
    // =========================================================

    /*
     * Recommended Winning tooltip:
     *
     * Block {block}. On Lucky Shot, instead parry the next enemy attack back
     * to its attacker.
     */
    protected override string ResolveTooltipTokens(
        BaseSticker owner,
        StickerSpinLocation location,
        string template)
    {
        string resolved =
            base.ResolveTooltipTokens(
                owner,
                location,
                template
            );


        return
            resolved.Replace(
                "{block}",
                Mathf.Max(
                    0,
                    blockAmount
                )
                .ToString()
            );
    }
}
