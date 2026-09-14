using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "EnemyCurse_Burnout",
    menuName = "Hell's Roulette/Enemy Curses/Burnout"
)]
public class EnemyCurseBurnout : EnemyCurse
{
    // =========================================================
    // RUNTIME REGISTRATIONS
    // =========================================================

    /*
     * EnemyCurse assets are ScriptableObjects and may be shared by several
     * physical enemy instances.
     *
     * Keep registrations per owner so one enemy dying removes only its own
     * contribution, while the Curse stays active if another Burnout enemy is
     * still combat-active.
     */
    private readonly HashSet<BaseEnemy>
        activeOwners =
            new HashSet<BaseEnemy>();


    private bool listeningToUseConsumption =
        false;


    // =========================================================
    // LIFECYCLE
    // =========================================================

    public override void Activate(
        BaseEnemy enemy,
        int value)
    {
        if (enemy == null)
            return;


        activeOwners.Add(
            enemy
        );


        EnsureListening();
    }


    public override void Deactivate(
        BaseEnemy enemy,
        int value)
    {
        if (enemy != null)
        {
            activeOwners.Remove(
                enemy
            );
        }


        StopListeningIfUnused();
    }


    private void OnDisable()
    {
        StopListening();

        activeOwners.Clear();
    }


    // =========================================================
    // USE CONSUMPTION MODIFIER
    // =========================================================

    private int ModifyUseConsumption(
        BaseSticker sticker,
        int requestedUses)
    {
        CleanupInvalidOwners();


        if (activeOwners.Count <= 0)
        {
            StopListeningIfUnused();

            return
                requestedUses;
        }


        if (sticker == null ||
            !sticker.HasLimitedUses ||
            sticker.IsConsumed ||
            sticker.RemainingUses <= 0)
        {
            return
                requestedUses;
        }


        int allRemainingUses =
            sticker.RemainingUses;


        if (allRemainingUses >
            requestedUses)
        {
            Debug.Log(
                $"[BURNOUT] '{GetStickerDisplayName(sticker)}' would consume " +
                $"{requestedUses} use(s); Burnout consumes all " +
                $"{allRemainingUses} remaining use(s) instead."
            );
        }


        return
            Mathf.Max(
                requestedUses,
                allRemainingUses
            );
    }


    // =========================================================
    // LISTENING
    // =========================================================

    private void EnsureListening()
    {
        if (listeningToUseConsumption)
            return;


        BaseSticker.OnModifyUseConsumption +=
            ModifyUseConsumption;

        BaseSticker.OnUseConsumptionResolved +=
            HandleUseConsumptionResolved;


        listeningToUseConsumption =
            true;
    }


    private void StopListeningIfUnused()
    {
        CleanupInvalidOwners();


        if (activeOwners.Count > 0)
            return;


        StopListening();
    }


    private void StopListening()
    {
        if (!listeningToUseConsumption)
            return;


        BaseSticker.OnModifyUseConsumption -=
            ModifyUseConsumption;

        BaseSticker.OnUseConsumptionResolved -=
            HandleUseConsumptionResolved;


        listeningToUseConsumption =
            false;
    }


    // =========================================================
    // OWNER CLEANUP
    // =========================================================

    private void CleanupInvalidOwners()
    {
        if (activeOwners.Count <= 0)
            return;


        List<BaseEnemy> stale =
            null;


        foreach (BaseEnemy owner in
                 activeOwners)
        {
            if (owner != null &&
                !owner.IsDead &&
                owner.CombatActive)
            {
                continue;
            }


            if (stale == null)
            {
                stale =
                    new List<BaseEnemy>();
            }


            stale.Add(
                owner
            );
        }


        if (stale == null)
            return;


        foreach (BaseEnemy owner in stale)
        {
            activeOwners.Remove(
                owner
            );
        }
    }


    // =========================================================
    // GAME LOG
    // =========================================================

    private void HandleUseConsumptionResolved(
        BaseSticker.UseConsumptionEvent consumption)
    {
        CleanupInvalidOwners();


        if (activeOwners.Count <= 0)
        {
            StopListeningIfUnused();
            return;
        }


        /*
         * Burnout only deserves a log line when it ACTUALLY increased the
         * amount consumed.
         *
         * Example:
         * 3 uses remaining, normal cost 1 -> Burnout matters.
         * 1 use remaining, normal cost 1 -> same outcome, no Burnout line.
         */
        if (consumption.sticker == null ||
            consumption.consumedUses <=
                consumption.requestedUses)
        {
            return;
        }


        System.Action feedback =
            () =>
            {
                LogBurnout(
                    consumption.sticker,
                    consumption.remainingUsesBefore,
                    consumption.remainingUsesAfter
                );
            };


        /*
         * Reactive stickers such as Shield deliberately suppress their normal
         * immediate "uses remaining" feedback because the DAMAGE SOURCE must
         * appear first in the log.
         *
         * Queue Burnout into that same deferred feedback stream.
         */
        if (!consumption.logRemainingUses &&
            BloodManager.Instance != null)
        {
            BloodManager.Instance
                .QueueDeferredDamageFeedback(
                    feedback
                );

            return;
        }


        feedback();
    }


    private void LogBurnout(
        BaseSticker sticker,
        int usesBefore,
        int usesAfter)
    {
        if (GameLogManager.Instance == null)
            return;


        string stickerName =
            GetStickerDisplayName(
                sticker
            );


        GameLogManager.Instance
            .AddGameplayLine(
                "Burnout exhausts " +
                GameLogManager.Instance
                    .StickerText(
                        stickerName
                    ) +
                $": {usesBefore} → {usesAfter} uses"
            );
    }


    // =========================================================
    // HELPERS
    // =========================================================

    private string GetStickerDisplayName(
        BaseSticker sticker)
    {
        if (sticker == null)
            return "Sticker";


        if (sticker.effect != null &&
            !string.IsNullOrWhiteSpace(
                sticker.effect.stickerName
            ))
        {
            return
                sticker.effect.stickerName;
        }


        return
            sticker.gameObject.name;
    }
}
