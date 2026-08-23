using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(100)]
public class RewardStickerOffer : MonoBehaviour
{
    // =========================================================
    // RUNTIME REFERENCES
    // =========================================================

    private RewardManager manager;
    private BaseSticker sticker;

    private GameObject offerObject;
    private Transform rewardSlot;

    private bool purchased = false;


    // =========================================================
    // PUBLIC STATE
    // =========================================================

    public bool Purchased
    {
        get { return purchased; }
    }

    /// <summary>
    /// Precio que tendría este sticker si se comprara AHORA.
    ///
    /// Cambia automáticamente cuando el jugador compra
    /// otros stickers durante la misma Reward Phase.
    /// </summary>
    public int CurrentPrice
    {
        get
        {
            if (manager == null ||
                sticker == null)
            {
                return 0;
            }

            return
                manager.GetCurrentPurchasePrice(
                    sticker
                );
        }
    }


    // =========================================================
    // INITIALIZATION
    // =========================================================

    public void Initialize(
        RewardManager rewardManager,
        GameObject spawnedOfferObject,
        Transform originSlot)
    {
        manager =
            rewardManager;

        offerObject =
            spawnedOfferObject;

        rewardSlot =
            originSlot;


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
                "[REWARD OFFER] Spawned reward has no BaseSticker."
            );
        }
    }


    // =========================================================
    // UNITY
    // =========================================================

    private void LateUpdate()
    {
#if UNITY_EDITOR || UNITY_STANDALONE

        if (purchased)
            return;

        if (manager == null ||
            sticker == null ||
            rewardSlot == null)
        {
            return;
        }


        if (!manager.RewardPhaseActive)
            return;


        /*
         * Esperamos hasta el mouse-up.
         *
         * BaseSticker tiene execution order normal,
         * mientras este script tiene +100.
         *
         * Por tanto BaseSticker ya habrá decidido:
         *
         * - Album válido
         * - Roulette válida
         * - o ReturnToOrigin()
         *
         * antes de que nosotros comprobemos la compra.
         */
        if (!Input.GetMouseButtonUp(0))
            return;


        TryResolvePurchase();

#endif
    }


    // =========================================================
    // PURCHASE
    // =========================================================

    private void TryResolvePurchase()
    {
        if (purchased)
            return;


        /*
         * Solo consideramos una compra si BaseSticker
         * ha terminado en uno de los DOS destinos válidos
         * para esta Reward Screen:
         *
         * 1. Album
         * 2. Roulette
         */
        bool placedInAlbum =
            sticker.currentAlbumZone != null;


        bool placedOnWheel =
            sticker.isPlaced &&
            sticker.currentSegment != null;


        if (!placedInAlbum &&
            !placedOnWheel)
        {
            /*
             * BaseSticker ya se habrá encargado de devolverlo
             * si simplemente hicimos un drop inválido.
             */
            return;
        }


        int price =
            manager.GetCurrentPurchasePrice(
                sticker
            );


        // -----------------------------------------------------
        // TRY PAY
        // -----------------------------------------------------

        bool purchaseSuccessful =
            manager.TryPurchaseOffer(
                offerObject,
                sticker
            );


        if (purchaseSuccessful)
        {
            purchased = true;


            Debug.Log(
                $"[REWARD OFFER] " +
                $"'{GetStickerName()}' purchased " +
                $"successfully for ${price}."
            );


            /*
             * A partir de aquí este sticker es simplemente
             * un BaseSticker normal propiedad del jugador.
             *
             * Ya no necesita lógica de tienda.
             */
            Destroy(this);

            return;
        }


        // -----------------------------------------------------
        // CANNOT AFFORD
        // -----------------------------------------------------

        Debug.Log(
            $"[REWARD OFFER] Cannot afford " +
            $"'{GetStickerName()}' (${price}). " +
            $"Returning to reward slot."
        );


        ReturnToRewardSlot();
    }


    // =========================================================
    // RETURN TO SHOP
    // =========================================================

    private void ReturnToRewardSlot()
    {
        if (sticker == null ||
            rewardSlot == null)
        {
            return;
        }


        Transform root =
            sticker.stickerRoot != null
                ? sticker.stickerRoot
                : sticker.transform;


        /*
         * Sacamos el sticker del Album / segmento.
         */
        root.SetParent(
            null,
            true
        );


        // -----------------------------------------------------
        // TRANSFORM
        // -----------------------------------------------------

        root.position =
            rewardSlot.position;

        root.rotation =
            rewardSlot.rotation;


        // -----------------------------------------------------
        // LOGICAL STATE
        // -----------------------------------------------------

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
            !string.IsNullOrEmpty(
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
