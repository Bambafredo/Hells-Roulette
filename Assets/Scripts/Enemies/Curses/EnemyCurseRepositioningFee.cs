using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "EnemyCurse_RepositioningFee",
    menuName = "Hell's Roulette/Enemy Curses/Repositioning Fee"
)]
public class EnemyCurseRepositioningFee : EnemyCurse
{
    // =========================================================
    // CONFIG
    // =========================================================

    public enum CostResource
    {
        Blood,
        Money
    }


    [Header("Repositioning Fee")]

    [Tooltip(
        "Resource lost whenever a sticker is manually moved " +
        "from one wheel segment to another, or from the wheel " +
        "back to the Album."
    )]
    [SerializeField]
    private CostResource costResource =
        CostResource.Blood;


    [Header("Safe Phases")]

    [Tooltip(
        "If enabled, Repositioning Fee is temporarily disabled while the " +
        "Infestation Panel is open AND the player has no spins remaining. " +
        "Infestations that happen while spins still remain are still penalized."
    )]
    [SerializeField]
    private bool disableDuringFinalInfestation =
        true;


    // =========================================================
    // RUNTIME REGISTRATIONS
    // =========================================================

    private readonly Dictionary<BaseEnemy, int>
        activeOwners =
            new Dictionary<BaseEnemy, int>();


    private readonly Dictionary<BaseSticker, Transform>
        dragOriginSegments =
            new Dictionary<BaseSticker, Transform>();


    private bool listeningToStickerDrags =
        false;


    private DraftManager draftManager;


    // =========================================================
    // LIFECYCLE
    // =========================================================

    public override void Activate(
        BaseEnemy enemy,
        int value)
    {
        if (enemy == null)
            return;


        activeOwners[enemy] =
            Mathf.Max(
                0,
                value
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
        dragOriginSegments.Clear();

        draftManager =
            null;
    }


    // =========================================================
    // GAMEPLAY STATE
    // =========================================================

    private bool CanChargeRepositioningFee()
    {
        /*
         * Reward Phase is always a safe setup zone.
         */
        if (RewardManager.Instance != null &&
            RewardManager.Instance.RewardPhaseActive)
        {
            return false;
        }


        /*
         * Starting Draft is always a safe setup zone.
         *
         * DraftManager currently has no singleton, so cache the scene instance
         * instead of changing its architecture just for this Curse.
         */
        if (draftManager == null)
        {
            draftManager =
                FindObjectOfType<DraftManager>();
        }


        if (draftManager != null &&
            draftManager.DraftActive)
        {
            return false;
        }


        /*
         * OPTIONAL DESIGN EXCEPTION:
         *
         * If the Infestation Panel is open after the final spin has already
         * been consumed, the round is effectively over. With this option
         * enabled, the player may reorganize stickers from that panel without
         * continuing to lose Blood / Money.
         *
         * IMPORTANT:
         * - Infestation panel + spins remaining -> Curse stays active.
         * - No spins remaining but no active Infestation panel -> Curse active.
         * - Only BOTH conditions together suppress the Curse.
         *
         * This is configurable because future panel-hiding / enemy-view
         * behaviour may make a different design preferable.
         */
        if (disableDuringFinalInfestation &&
            InfestationManager.Instance != null &&
            InfestationManager.Instance.InfestationActive &&
            RoundManager.Instance != null &&
            RoundManager.Instance.TokensRemaining <= 0)
        {
            return false;
        }


        return true;
    }


    // =========================================================
    // LISTENING
    // =========================================================

    private void EnsureListening()
    {
        if (listeningToStickerDrags)
            return;


        BaseSticker.OnAnyStickerDragStarted +=
            OnStickerDragStarted;

        BaseSticker.OnAnyStickerDragEnded +=
            OnStickerDragEnded;


        listeningToStickerDrags =
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
        if (!listeningToStickerDrags)
            return;


        BaseSticker.OnAnyStickerDragStarted -=
            OnStickerDragStarted;

        BaseSticker.OnAnyStickerDragEnded -=
            OnStickerDragEnded;


        listeningToStickerDrags =
            false;

        dragOriginSegments.Clear();
    }


    // =========================================================
    // DRAG TRACKING
    // =========================================================

    private void OnStickerDragStarted(
        BaseSticker sticker)
    {
        if (sticker == null)
            return;


        /*
         * Safe phase: do not even remember the origin.
         *
         * This guarantees that a drag started during Reward, Draft or an
         * optionally-safe final Infestation cannot become a pending Curse
         * trigger after that phase closes.
         */
        if (!CanChargeRepositioningFee())
        {
            dragOriginSegments.Remove(
                sticker
            );

            return;
        }


        CleanupInvalidOwners();


        if (activeOwners.Count == 0)
        {
            StopListeningIfUnused();
            return;
        }


        /*
         * BaseSticker fires OnAnyStickerDragStarted BEFORE it temporarily
         * clears isPlaced/currentSegment.
         *
         * Therefore this is the clean point to remember whether the sticker
         * genuinely started this manual drag on the wheel.
         */
        if (sticker.isPlaced &&
            sticker.currentSegment != null)
        {
            dragOriginSegments[sticker] =
                sticker.currentSegment;
        }
        else
        {
            /*
             * The drag started somewhere other than the wheel
             * (for example the Album).
             *
             * Album -> Wheel is deliberately free.
             */
            dragOriginSegments.Remove(
                sticker
            );
        }
    }


    private void OnStickerDragEnded(
        BaseSticker sticker)
    {
        if (sticker == null)
            return;


        Transform originSegment;


        if (!dragOriginSegments.TryGetValue(
                sticker,
                out originSegment))
        {
            return;
        }


        /*
         * The drag has finished, so its temporary origin state must never
         * survive into a future drag.
         */
        dragOriginSegments.Remove(
            sticker
        );


        /*
         * Check safe-phase state again in case a panel opened between drag
         * start and drag end.
         */
        if (!CanChargeRepositioningFee())
            return;


        CleanupInvalidOwners();


        if (activeOwners.Count == 0)
        {
            StopListeningIfUnused();
            return;
        }


        bool movedToDifferentSegment =
            sticker.isPlaced &&
            sticker.currentSegment != null &&
            sticker.currentSegment != originSegment;


        bool movedToAlbum =
            !sticker.isPlaced &&
            sticker.currentAlbumZone != null;


        /*
         * These cases deliberately cost nothing:
         *
         * Segment A -> Segment A
         * Segment A -> invalid drop -> returned to Segment A
         * Album -> Segment
         * Album -> Album
         * Any movement during Reward Phase
         * Any movement during Draft
         * Any movement during a safe final Infestation, when enabled
         */
        if (!movedToDifferentSegment &&
            !movedToAlbum)
        {
            return;
        }


        ApplyRepositioningFee(
            sticker,
            movedToAlbum
        );
    }


    // =========================================================
    // COST
    // =========================================================

    private void ApplyRepositioningFee(
        BaseSticker sticker,
        bool movedToAlbum)
    {
        /*
         * Final safety check. Applying the cost must never sneak through if
         * gameplay entered one of the safe phases during the drag.
         */
        if (!CanChargeRepositioningFee())
            return;


        List<KeyValuePair<BaseEnemy, int>> owners =
            new List<KeyValuePair<BaseEnemy, int>>(
                activeOwners
            );


        foreach (KeyValuePair<BaseEnemy, int> entry in owners)
        {
            BaseEnemy owner =
                entry.Key;


            if (owner == null ||
                owner.IsDead ||
                !owner.CombatActive)
            {
                continue;
            }


            int requestedCost =
                Mathf.Max(
                    0,
                    entry.Value
                );


            if (requestedCost <= 0)
                continue;


            int actualLoss =
                0;


            switch (costResource)
            {
                case CostResource.Blood:
                {
                    if (BloodManager.Instance == null)
                        continue;


                    int before =
                        Mathf.Max(
                            0,
                            BloodManager.Instance.currentBlood
                        );


                    /*
                     * This is a COST, not damage.
                     * Shield and damage blockers must not prevent it.
                     */
                    BloodManager.Instance
                        .ConsumeBlood(
                            requestedCost
                        );


                    int after =
                        Mathf.Max(
                            0,
                            BloodManager.Instance.currentBlood
                        );


                    actualLoss =
                        Mathf.Max(
                            0,
                            before - after
                        );

                    break;
                }


                case CostResource.Money:
                {
                    if (CurrencyManager.Instance == null)
                        continue;


                    /*
                     * This is a penalty, not a purchase. If the player has less
                     * than the requested amount, they simply lose what remains.
                     */
                    actualLoss =
                        CurrencyManager.Instance
                            .LoseDollars(
                                requestedCost
                            );

                    break;
                }
            }


            LogRepositioningFee(
                owner,
                sticker,
                requestedCost,
                actualLoss,
                movedToAlbum
            );
        }
    }


    // =========================================================
    // OWNER CLEANUP
    // =========================================================

    private void CleanupInvalidOwners()
    {
        if (activeOwners.Count == 0)
            return;


        List<BaseEnemy> stale =
            null;


        foreach (KeyValuePair<BaseEnemy, int> entry in
                 activeOwners)
        {
            BaseEnemy owner =
                entry.Key;


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

    private void LogRepositioningFee(
        BaseEnemy owner,
        BaseSticker sticker,
        int requestedCost,
        int actualLoss,
        bool movedToAlbum)
    {
        string stickerName =
            sticker != null &&
            sticker.effect != null &&
            !string.IsNullOrWhiteSpace(
                sticker.effect.stickerName)
                ? sticker.effect.stickerName
                : sticker != null
                    ? sticker.name
                    : "Sticker";


        string destinationText =
            movedToAlbum
                ? "Album"
                : "another segment";


        if (GameLogManager.Instance != null)
        {
            string costText;


            if (costResource ==
                CostResource.Blood)
            {
                costText =
                    GameLogManager.Instance
                        .BloodText(
                            actualLoss > 0
                                ? $"-{actualLoss} Blood"
                                : "0 Blood"
                        );
            }
            else
            {
                costText =
                    GameLogManager.Instance
                        .MoneyText(
                            actualLoss > 0
                                ? $"-${actualLoss}"
                                : "$0"
                        );
            }


            string requestedSuffix =
                actualLoss < requestedCost
                    ? $" (cost {requestedCost})"
                    : "";


            GameLogManager.Instance
                .AddGameplayLine(
                    "Repositioning Fee: " +
                    GameLogManager.Instance
                        .StickerText(
                            stickerName
                        ) +
                    $" moved to {destinationText}: " +
                    costText +
                    requestedSuffix
                );
        }


        string resourceName =
            costResource ==
            CostResource.Blood
                ? "Blood"
                : "Money";


        Debug.Log(
            $"[REPOSITIONING FEE] " +
            $"{owner?.EnemyName ?? "Enemy"}: " +
            $"'{stickerName}' moved to {destinationText}. " +
            $"Cost {requestedCost} {resourceName}; " +
            $"actual loss {actualLoss}."
        );
    }


    // =========================================================
    // TOOLTIP
    // =========================================================

    public override string GetTooltipDescription(
        BaseEnemy enemy,
        int value)
    {
        string authored =
            base.GetTooltipDescription(
                enemy,
                value
            );


        int cost =
            Mathf.Max(
                0,
                value
            );


        string costText =
            costResource ==
            CostResource.Blood
                ? $"{cost} Blood"
                : $"${cost}";


        if (!string.IsNullOrWhiteSpace(
                authored))
        {
            return
                authored.Replace(
                    "{cost}",
                    costText
                );
        }


        return
            "Moving a sticker from one wheel segment to another, " +
            $"or back to the Album, costs {costText}. " +
            "Moving stickers from the Album to the wheel is free.";
    }
}
