using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(100)]
public class InfestationStickerOffer : MonoBehaviour
{
    // =========================================================
    // RUNTIME REFERENCES
    // =========================================================

    private InfestationManager manager;
    private BaseSticker sticker;

    private GameObject offerObject;
    private Transform originSlot;

    private bool claimed =
        false;


    // =========================================================
    // INITIALIZATION
    // =========================================================

    public void Initialize(
        InfestationManager infestationManager,
        GameObject spawnedOfferObject,
        Transform infestationSlot)
    {
        manager =
            infestationManager;

        offerObject =
            spawnedOfferObject;

        originSlot =
            infestationSlot;


        if (offerObject != null)
        {
            sticker =
                offerObject
                    .GetComponentInChildren<BaseSticker>(
                        true
                    );
        }


        if (sticker == null)
        {
            Debug.LogError(
                "[INFESTATION OFFER] Spawned Leech has no BaseSticker."
            );
        }
    }


    // =========================================================
    // UNITY
    // =========================================================

    private void LateUpdate()
    {
#if UNITY_EDITOR || UNITY_STANDALONE

        if (claimed)
            return;


        if (manager == null ||
            !manager.InfestationActive ||
            sticker == null ||
            originSlot == null)
        {
            return;
        }


        if (!Input.GetMouseButtonUp(0))
            return;


        TryResolveClaim();

#endif
    }


    // =========================================================
    // CLAIM
    // =========================================================

    private void TryResolveClaim()
    {
        if (claimed)
            return;


        bool placedInAlbum =
            sticker.currentAlbumZone != null;


        bool placedOnWheel =
            sticker.isPlaced &&
            sticker.currentSegment != null;


        /*
         * Infestation only forces the player to TAKE the Leech.
         *
         * The player may immediately choose its destination:
         * - Album  -> Leech starts draining Blood from there.
         * - Wheel  -> avoids the Album penalty, but consumes wheel space.
         *
         * This avoids the pointless extra interaction of placing it in Album
         * first and immediately dragging it back out.
         */
        if (!placedInAlbum &&
            !placedOnWheel)
        {
            return;
        }


        bool accepted =
            manager.TryClaimOffer(
                offerObject,
                sticker
            );


        if (accepted)
        {
            claimed =
                true;


            string destination =
                placedInAlbum
                    ? "Album"
                    : "Roulette";


            Debug.Log(
                $"[INFESTATION OFFER] '{GetStickerName()}' claimed into " +
                $"{destination}."
            );


            /*
             * It is now an ordinary player-owned Leech.
             */
            Destroy(
                this
            );

            return;
        }


        ReturnToInfestationSlot();
    }


    // =========================================================
    // RETURN TO SLOT
    // =========================================================

    private void ReturnToInfestationSlot()
    {
        if (sticker == null ||
            originSlot == null)
        {
            return;
        }


        Transform root =
            sticker.stickerRoot != null
                ? sticker.stickerRoot
                : sticker.transform;


        root.SetParent(
            null,
            true
        );


        root.position =
            originSlot.position;

        root.rotation =
            originSlot.rotation;


        sticker.isPlaced =
            false;

        sticker.currentSegment =
            null;

        sticker.currentAlbumZone =
            null;

        sticker.currentBagZone =
            null;

        sticker.currentGameplayZone =
            null;


        Physics2D.SyncTransforms();
    }


    // =========================================================
    // HELPERS
    // =========================================================

    private string GetStickerName()
    {
        if (sticker == null)
            return "Unknown";


        if (sticker.effect != null &&
            !string.IsNullOrWhiteSpace(
                sticker.effect.stickerName
            ))
        {
            return
                sticker.effect.stickerName;
        }


        return
            sticker.name;
    }
}
