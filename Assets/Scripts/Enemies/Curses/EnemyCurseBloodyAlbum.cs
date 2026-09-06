using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "EnemyCurse_BloodyAlbum",
    menuName = "Hell's Roulette/Enemy Curses/Bloody Album"
)]
public class EnemyCurseBloodyAlbum : EnemyCurse
{
    // =========================================================
    // RUNTIME REGISTRATIONS
    // =========================================================

    /*
     * EnemyCurse assets are ScriptableObjects and may be shared by several
     * enemy instances. Keep the active owner/value pairs explicitly rather
     * than storing one current enemy on the asset.
     */
    private readonly Dictionary<BaseEnemy, int>
        activeOwners =
            new Dictionary<BaseEnemy, int>();

    private bool listeningToStickerPlacements =
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
            activeOwners.Remove(enemy);
        }


        StopListeningIfUnused();
    }


    private void OnDisable()
    {
        StopListening();
        activeOwners.Clear();
    }


    private void EnsureListening()
    {
        if (listeningToStickerPlacements)
            return;


        BaseSticker.OnStickerPlacedFromAlbumToWheel +=
            OnStickerPlacedFromAlbumToWheel;

        listeningToStickerPlacements =
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
        if (!listeningToStickerPlacements)
            return;


        BaseSticker.OnStickerPlacedFromAlbumToWheel -=
            OnStickerPlacedFromAlbumToWheel;

        listeningToStickerPlacements =
            false;
    }


    // =========================================================
    // ALBUM -> WHEEL COST
    // =========================================================

    private void OnStickerPlacedFromAlbumToWheel(
        BaseSticker sticker)
    {
        CleanupInvalidOwners();


        if (activeOwners.Count == 0 ||
            BloodManager.Instance == null)
        {
            StopListeningIfUnused();
            return;
        }


        /*
         * Snapshot registrations because ConsumeBlood() may kill the player or
         * trigger other gameplay transitions while this event is resolving.
         */
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


            int before =
                Mathf.Max(
                    0,
                    BloodManager.Instance.currentBlood
                );


            /*
             * IMPORTANT: Bloody Album is a COST, not damage.
             * ConsumeBlood() intentionally bypasses Shield and every registered
             * damage blocker in BloodManager.
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

            int actualBloodLost =
                Mathf.Max(
                    0,
                    before - after
                );


            LogBloodyAlbumCost(
                owner,
                sticker,
                requestedCost,
                actualBloodLost
            );
        }
    }


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


            stale.Add(owner);
        }


        if (stale == null)
            return;


        foreach (BaseEnemy owner in stale)
        {
            activeOwners.Remove(owner);
        }
    }


    private void LogBloodyAlbumCost(
        BaseEnemy owner,
        BaseSticker sticker,
        int requestedCost,
        int actualBloodLost)
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


        if (GameLogManager.Instance != null)
        {
            string bloodText =
                actualBloodLost > 0
                    ? GameLogManager.Instance
                        .BloodText(
                            $"-{actualBloodLost} Blood"
                        )
                    : GameLogManager.Instance
                        .BloodText(
                            "0 Blood"
                        );


            string requestedSuffix =
                actualBloodLost < requestedCost
                    ? $" (cost {requestedCost})"
                    : "";


            GameLogManager.Instance
                .AddGameplayLine(
                    $"Bloody Album: " +
                    GameLogManager.Instance.StickerText(
                        stickerName
                    ) +
                    " placed from Album to wheel: " +
                    bloodText +
                    requestedSuffix
                );
        }


        Debug.Log(
            $"[BLOODY ALBUM] {owner?.EnemyName ?? "Enemy"}: " +
            $"'{stickerName}' moved Album -> Wheel. " +
            $"Cost {requestedCost}; actual Blood lost {actualBloodLost}."
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


        if (!string.IsNullOrWhiteSpace(
                authored))
        {
            return authored;
        }


        int bloodCost =
            Mathf.Max(
                0,
                value
            );


        return
            $"Whenever you place a sticker on the wheel from the Album, " +
            $"lose {bloodCost} Blood.";
    }
}
