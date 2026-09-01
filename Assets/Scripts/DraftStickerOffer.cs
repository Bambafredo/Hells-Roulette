using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(100)]
public class DraftStickerOffer : MonoBehaviour
{
    // =========================================================
    // RUNTIME REFERENCES
    // =========================================================

    private DraftManager manager;
    private BaseSticker sticker;

    private GameObject offerObject;
    private Transform originSlot;

    private bool claimed =
        false;


    // =========================================================
    // INITIALIZATION
    // =========================================================

    public void Initialize(
        DraftManager draftManager,
        GameObject spawnedOfferObject,
        Transform draftSlot)
    {
        manager =
            draftManager;

        offerObject =
            spawnedOfferObject;

        originSlot =
            draftSlot;


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
                "[DRAFT OFFER] Spawned draft offer has no BaseSticker."
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
            !manager.DraftActive ||
            sticker == null ||
            originSlot == null)
        {
            return;
        }


        /*
         * BaseSticker resolves its drop first.
         * This component runs later (+100) and evaluates the resulting location.
         */
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


        // Valid draft destination: Album ONLY.
        if (placedInAlbum)
        {
            bool accepted =
                manager.TryClaimOffer(
                    offerObject,
                    sticker
                );


            if (accepted)
            {
                claimed =
                    true;


                Debug.Log(
                    $"[DRAFT OFFER] '{GetStickerName()}' claimed into Album."
                );


                /*
                 * It is now a normal player-owned BaseSticker.
                 */
                Destroy(
                    this
                );

                return;
            }


            ReturnToDraftSlot();
            return;
        }


        /*
         * Roulette placement is valid for BaseSticker itself, but not for the
         * starting draft. Explicitly return it to its draft slot.
         */
        if (placedOnWheel)
        {
            Debug.Log(
                $"[DRAFT OFFER] '{GetStickerName()}' must be placed in Album. " +
                $"Returning to draft slot."
            );


            ReturnToDraftSlot();
        }
    }


    // =========================================================
    // RETURN TO SLOT
    // =========================================================

    private void ReturnToDraftSlot()
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
