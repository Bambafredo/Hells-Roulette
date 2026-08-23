using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class RewardManager : MonoBehaviour
{
    public static RewardManager Instance;

    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("Reward Screen")]
    public GameObject rewardPanel;

    public Transform rewardSlotA;
    public Transform rewardSlotB;

    [Header("Buttons")]
    public Collider2D rerollButtonCollider;
    public Collider2D skipButtonCollider;


    // =========================================================
    // REWARD POOL
    // =========================================================

    [Header("Reward Pool")]
    public GameObject[] stickerPrefabs;


    // =========================================================
    // REROLL
    // =========================================================

    [Header("Reroll")]
    [Min(0)]
    public int rerollBloodCost = 1;


    // =========================================================
    // STATE
    // =========================================================

    public bool RewardPhaseActive
    {
        get;
        private set;
    }

    public int PurchasesThisPhase
    {
        get;
        private set;
    }

    /// <summary>
    /// Primera compra = x1
    /// Segunda compra = x2
    /// Tercera compra = x3
    /// etc.
    /// </summary>
    public int CurrentPurchaseMultiplier
    {
        get
        {
            return PurchasesThisPhase + 1;
        }
    }

    private GameObject currentOfferA;
    private GameObject currentOfferB;

    private Camera cam;


    // =========================================================
    // EVENTS
    // =========================================================

    public event Action OnRewardPhaseCompleted;

    public event Action<int> OnPurchaseCountChanged;


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        cam = Camera.main;
    }


    private void Start()
    {
        if (rewardPanel != null)
        {
            rewardPanel.SetActive(false);
        }

        RewardPhaseActive = false;
        PurchasesThisPhase = 0;
    }


    private void Update()
    {
#if UNITY_EDITOR || UNITY_STANDALONE

        if (!RewardPhaseActive)
            return;

        if (!Input.GetMouseButtonDown(0))
            return;

        if (cam == null)
            cam = Camera.main;

        if (cam == null)
            return;

        Vector2 mouseWorld =
            cam.ScreenToWorldPoint(
                Input.mousePosition
            );


        // -----------------------------------------------------
        // REROLL
        // -----------------------------------------------------

        if (rerollButtonCollider != null &&
            rerollButtonCollider.OverlapPoint(mouseWorld))
        {
            TryReroll();
            return;
        }


        // -----------------------------------------------------
        // SKIP
        // -----------------------------------------------------

        if (skipButtonCollider != null &&
            skipButtonCollider.OverlapPoint(mouseWorld))
        {
            SkipReward();
            return;
        }

#endif
    }


    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }


    // =========================================================
    // BEGIN REWARD PHASE
    // =========================================================

    public void BeginRewardPhase()
    {
        if (RewardPhaseActive)
            return;

        if (rewardPanel == null)
        {
            Debug.LogError(
                "[REWARD] Reward Panel reference missing."
            );

            return;
        }

        RewardPhaseActive = true;

        /*
         * El multiplicador se reinicia únicamente
         * al entrar en una NUEVA Reward Phase.
         */
        PurchasesThisPhase = 0;

        OnPurchaseCountChanged?
            .Invoke(PurchasesThisPhase);

        rewardPanel.SetActive(true);

        GenerateOffers();

        Debug.Log(
            "[REWARD] Reward phase started. " +
            "Next purchase multiplier: x1."
        );
    }


    // =========================================================
    // PRICE
    // =========================================================

    /// <summary>
    /// Devuelve el precio que tendría ESTE sticker
    /// si se comprara ahora mismo.
    /// </summary>
    public int GetCurrentPurchasePrice(
        BaseSticker sticker)
    {
        if (sticker == null ||
            sticker.effect == null)
        {
            return 0;
        }

        int baseCost =
            Mathf.Max(
                0,
                sticker.effect.basePurchaseCost
            );

        return
            baseCost *
            CurrentPurchaseMultiplier;
    }


    /// <summary>
    /// RewardStickerOffer llamará aquí DESPUÉS de que
    /// BaseSticker haya conseguido una colocación válida.
    ///
    /// Devuelve false si no podemos pagar.
    /// </summary>
    public bool TryPurchaseOffer(
        GameObject offerObject,
        BaseSticker sticker)
    {
        if (!RewardPhaseActive)
            return false;

        if (offerObject == null ||
            sticker == null)
        {
            return false;
        }

        /*
         * Solo podemos comprar objetos que realmente
         * pertenecen actualmente a la tienda.
         */
        bool isOfferA =
            currentOfferA == offerObject;

        bool isOfferB =
            currentOfferB == offerObject;

        if (!isOfferA &&
            !isOfferB)
        {
            return false;
        }


        int price =
            GetCurrentPurchasePrice(
                sticker
            );


        // -----------------------------------------------------
        // MONEY
        // -----------------------------------------------------

        if (CurrencyManager.Instance == null)
        {
            Debug.LogWarning(
                "[REWARD] CurrencyManager missing."
            );

            return false;
        }


        if (!CurrencyManager.Instance.CanAfford(price))
        {
            Debug.Log(
                $"[REWARD] Cannot buy " +
                $"'{GetStickerName(sticker)}'. " +
                $"Need ${price}, " +
                $"have ${CurrencyManager.Instance.dollars}."
            );

            return false;
        }


        bool paid =
            CurrencyManager.Instance
                .Spend(price);

        if (!paid)
            return false;


        // -----------------------------------------------------
        // REMOVE FROM STORE
        // -----------------------------------------------------

        /*
         * IMPORTANTÍSIMO:
         *
         * NO destruimos el sticker comprado.
         * Ya está colocado en Album o Roulette
         * y ahora pertenece al jugador.
         */

        if (isOfferA)
            currentOfferA = null;

        if (isOfferB)
            currentOfferB = null;


        // -----------------------------------------------------
        // ADVANCE MULTIPLIER
        // -----------------------------------------------------

        PurchasesThisPhase++;

        OnPurchaseCountChanged?
            .Invoke(PurchasesThisPhase);


        Debug.Log(
            $"[REWARD] Purchased " +
            $"'{GetStickerName(sticker)}' " +
            $"for ${price}. " +
            $"Purchases this phase: " +
            $"{PurchasesThisPhase}. " +
            $"Next multiplier: " +
            $"x{CurrentPurchaseMultiplier}."
        );


        /*
         * La Reward Phase NO termina.
         *
         * El jugador puede:
         *
         * - comprar la otra oferta
         * - hacer reroll
         * - hacer skip
         */
        return true;
    }


    // =========================================================
    // GENERATE OFFERS
    // =========================================================

    private void GenerateOffers()
    {
        /*
         * Reroll siempre sustituye cualquier oferta
         * que todavía quede.
         *
         * Los stickers YA COMPRADOS no están en
         * currentOfferA/B y por tanto no se destruyen.
         */
        ClearRemainingOffers();


        if (stickerPrefabs == null ||
            stickerPrefabs.Length == 0)
        {
            Debug.LogWarning(
                "[REWARD] Sticker reward pool is empty."
            );

            return;
        }


        int indexA =
            UnityEngine.Random.Range(
                0,
                stickerPrefabs.Length
            );


        int indexB =
            GetSecondRewardIndex(
                indexA
            );


        currentOfferA =
            SpawnOffer(
                stickerPrefabs[indexA],
                rewardSlotA
            );


        currentOfferB =
            SpawnOffer(
                stickerPrefabs[indexB],
                rewardSlotB
            );


        Debug.Log(
            $"[REWARD] Generated offers: " +
            $"{GetOfferName(currentOfferA)} / " +
            $"{GetOfferName(currentOfferB)}. " +
            $"Current multiplier: " +
            $"x{CurrentPurchaseMultiplier}."
        );
    }


    // =========================================================
    // SECOND RANDOM STICKER
    // =========================================================

    private int GetSecondRewardIndex(
        int firstIndex)
    {
        /*
         * Durante desarrollo permitimos dos copias
         * si el pool solo contiene un prefab.
         */
        if (stickerPrefabs.Length <= 1)
            return firstIndex;


        int secondIndex =
            firstIndex;


        while (secondIndex == firstIndex)
        {
            secondIndex =
                UnityEngine.Random.Range(
                    0,
                    stickerPrefabs.Length
                );
        }


        return secondIndex;
    }


    // =========================================================
    // SPAWN
    // =========================================================

    private GameObject SpawnOffer(
        GameObject prefab,
        Transform slot)
    {
        if (prefab == null ||
            slot == null)
        {
            return null;
        }


        GameObject instance =
            Instantiate(
                prefab,
                slot.position,
                slot.rotation
            );


        // =====================================================
        // REWARD OFFER COMPONENT
        // =====================================================

        RewardStickerOffer offer =
            instance.GetComponent<RewardStickerOffer>();


        if (offer == null)
        {
            offer =
                instance.AddComponent<RewardStickerOffer>();
        }


        offer.Initialize(
            this,
            instance,
            slot
        );


        return instance;
    }


    // =========================================================
    // REROLL
    // =========================================================

    public bool TryReroll()
    {
        if (!RewardPhaseActive)
            return false;


        // -----------------------------------------------------
        // FREE REROLL
        // -----------------------------------------------------

        if (rerollBloodCost <= 0)
        {
            GenerateOffers();

            Debug.Log(
                "[REWARD] Free reroll. " +
                $"Purchase multiplier remains " +
                $"x{CurrentPurchaseMultiplier}."
            );

            return true;
        }


        // -----------------------------------------------------
        // BLOOD
        // -----------------------------------------------------

        if (BloodManager.Instance == null)
        {
            Debug.LogWarning(
                "[REWARD] BloodManager missing. " +
                "Cannot pay reroll cost."
            );

            return false;
        }


        if (BloodManager.Instance.currentBlood <
            rerollBloodCost)
        {
            Debug.Log(
                $"[REWARD] Cannot reroll. " +
                $"Need {rerollBloodCost} blood, " +
                $"have " +
                $"{BloodManager.Instance.currentBlood}."
            );

            return false;
        }


        bool paid =
            BloodManager.Instance
                .ConsumeBlood(
                    rerollBloodCost
                );


        if (!paid)
            return false;


        GenerateOffers();


        Debug.Log(
            $"[REWARD] Rerolled for " +
            $"{rerollBloodCost} blood. " +
            $"Purchase multiplier remains " +
            $"x{CurrentPurchaseMultiplier}."
        );


        return true;
    }


    // =========================================================
    // SKIP
    // =========================================================

    public void SkipReward()
    {
        if (!RewardPhaseActive)
            return;


        Debug.Log(
            $"[REWARD] Reward phase skipped. " +
            $"Total purchases: " +
            $"{PurchasesThisPhase}."
        );


        CompleteRewardPhase();
    }


    // =========================================================
    // COMPLETE PHASE
    // =========================================================

    private void CompleteRewardPhase()
    {
        /*
         * Destruimos únicamente los stickers que
         * todavía seguían a la venta.
         */
        ClearRemainingOffers();


        RewardPhaseActive = false;


        if (rewardPanel != null)
        {
            rewardPanel.SetActive(false);
        }


        OnRewardPhaseCompleted?
            .Invoke();


        Debug.Log(
            "[REWARD] Reward phase completed."
        );
    }


    // =========================================================
    // CLEAR REMAINING OFFERS
    // =========================================================

    private void ClearRemainingOffers()
    {
        if (currentOfferA != null)
        {
            Destroy(currentOfferA);

            currentOfferA = null;
        }


        if (currentOfferB != null)
        {
            Destroy(currentOfferB);

            currentOfferB = null;
        }
    }


    // =========================================================
    // DEBUG
    // =========================================================

    [ContextMenu("DEBUG - Begin Reward Phase")]
    private void DebugBeginRewardPhase()
    {
        BeginRewardPhase();
    }


    // =========================================================
    // HELPERS
    // =========================================================

    private string GetOfferName(
        GameObject offer)
    {
        if (offer == null)
            return "EMPTY";


        BaseSticker sticker =
            offer.GetComponentInChildren<BaseSticker>(
                true
            );


        return GetStickerName(
            sticker
        );
    }


    private string GetStickerName(
        BaseSticker sticker)
    {
        if (sticker == null)
            return "NULL";


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
