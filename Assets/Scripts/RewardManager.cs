using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using TMPro;

public class RewardManager : MonoBehaviour
{
    public static RewardManager Instance;


    // =========================================================
    // PURCHASE CURRENCY
    // =========================================================

    public enum PurchaseCurrency
    {
        Blood,
        Coin
    }


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
    // CHANGE CURRENCY BUTTON
    // =========================================================

    [Header("Change Currency Button")]

    public Collider2D changeCurrencyButtonCollider;

    public SpriteRenderer changeCurrencyButtonRenderer;

    public TMP_Text changeCurrencyButtonText;


    [Header("Currency Button Colors")]

    [Tooltip("Color del botón cuando estamos pagando con Blood.")]
    public Color bloodButtonColor = Color.red;

    [Tooltip("Color del botón cuando estamos pagando con Coin.")]
    public Color coinButtonColor = Color.yellow;


    // =========================================================
    // DEFAULT CURRENCY
    // =========================================================

    [Header("Default Currency")]

    [Tooltip(
        "Moneda que estará seleccionada por defecto " +
        "cada vez que se abra una nueva Reward Screen."
    )]
    public PurchaseCurrency defaultPurchaseCurrency =
        PurchaseCurrency.Coin;


    // =========================================================
    // REWARD TEXTS
    // =========================================================

    [Header("Reward Texts")]

    public TMP_Text rewardSlotAPriceText;

    public TMP_Text rewardSlotBPriceText;

    public TMP_Text rerollCostText;


    [Header("Reward Price Colors")]

    [Tooltip("Color de los precios cuando se paga con Blood.")]
    public Color bloodPriceColor = Color.red;

    [Tooltip("Color de los precios cuando se paga con Coin.")]
    public Color coinPriceColor = Color.yellow;


    // =========================================================
    // REWARD POOL
    // =========================================================

    [Header("Reward Pool")]
    public GameObject[] stickerPrefabs;


    // =========================================================
    // PURCHASE BALANCE
    // =========================================================

    [Header("Purchase Balance")]

    [Tooltip(
        "Multiplicador adicional aplicado SOLO cuando " +
        "los stickers se compran con Blood. " +
        "1 = mismo precio que Coin, 2 = x2, 3 = x3, etc."
    )]
    [Min(1)]
    public int bloodPriceMultiplier = 1;


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


    public PurchaseCurrency CurrentPurchaseCurrency
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


        /*
         * Inicializamos inmediatamente con el valor
         * configurado desde Inspector.
         */
        CurrentPurchaseCurrency =
            defaultPurchaseCurrency;
    }


    private void Start()
    {
        if (rewardPanel != null)
        {
            rewardPanel.SetActive(false);
        }


        RewardPhaseActive = false;

        PurchasesThisPhase = 0;


        CurrentPurchaseCurrency =
            defaultPurchaseCurrency;


        UpdateCurrencyButtonVisuals();
        UpdateRewardTexts();
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
        // CHANGE CURRENCY
        // -----------------------------------------------------

        if (changeCurrencyButtonCollider != null &&
            changeCurrencyButtonCollider.OverlapPoint(mouseWorld))
        {
            TogglePurchaseCurrency();
            return;
        }


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
         * Cada nueva Reward Phase:
         *
         * - multiplicador vuelve a x1
         * - moneda vuelve a la configurada como Default
         */
        PurchasesThisPhase = 0;

        CurrentPurchaseCurrency =
            defaultPurchaseCurrency;


        OnPurchaseCountChanged?
            .Invoke(PurchasesThisPhase);


        rewardPanel.SetActive(true);


        GenerateOffers();

        UpdateCurrencyButtonVisuals();
        UpdateRewardTexts();


        Debug.Log(
            "[REWARD] Reward phase started. " +
            $"Next purchase multiplier: x1. " +
            $"Currency: {CurrentPurchaseCurrency}."
        );
    }


    // =========================================================
    // CHANGE PURCHASE CURRENCY
    // =========================================================

    private void TogglePurchaseCurrency()
    {
        if (!RewardPhaseActive)
            return;


        if (CurrentPurchaseCurrency ==
            PurchaseCurrency.Blood)
        {
            CurrentPurchaseCurrency =
                PurchaseCurrency.Coin;
        }

        else
        {
            CurrentPurchaseCurrency =
                PurchaseCurrency.Blood;
        }


        UpdateCurrencyButtonVisuals();
        UpdateRewardTexts();


        Debug.Log(
            $"[REWARD] Purchase currency changed to " +
            $"{CurrentPurchaseCurrency}."
        );
    }


    private void UpdateCurrencyButtonVisuals()
    {
        bool usingBlood =
            CurrentPurchaseCurrency ==
            PurchaseCurrency.Blood;


        // -----------------------------------------------------
        // BUTTON TEXT
        // -----------------------------------------------------

        if (changeCurrencyButtonText != null)
        {
            changeCurrencyButtonText.text =
                usingBlood
                    ? "Blood"
                    : "Coin";
        }


        // -----------------------------------------------------
        // BUTTON COLOR
        // -----------------------------------------------------

        if (changeCurrencyButtonRenderer != null)
        {
            changeCurrencyButtonRenderer.color =
                usingBlood
                    ? bloodButtonColor
                    : coinButtonColor;
        }
    }


    // =========================================================
    // PRICE
    // =========================================================

    /// <summary>
    /// Coin:
    /// Base Cost × Purchase Multiplier
    ///
    /// Blood:
    /// Base Cost × Purchase Multiplier × Blood Price Multiplier
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


        int price =
            baseCost *
            CurrentPurchaseMultiplier;


        if (CurrentPurchaseCurrency ==
            PurchaseCurrency.Blood)
        {
            price *=
                Mathf.Max(
                    1,
                    bloodPriceMultiplier
                );
        }


        return price;
    }


    // =========================================================
    // PURCHASE
    // =========================================================

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


        // -----------------------------------------------------
        // VALID OFFER
        // -----------------------------------------------------

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
        // PAY
        // -----------------------------------------------------

        bool paid =
            TryPayPurchasePrice(
                price
            );


        if (!paid)
        {
            LogFailedPurchase(
                sticker,
                price
            );

            return false;
        }


        // -----------------------------------------------------
        // REMOVE FROM STORE
        // -----------------------------------------------------

        /*
         * El sticker comprado NO se destruye.
         * Ya pertenece al jugador.
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


        /*
         * La oferta restante pasa inmediatamente
         * al nuevo multiplicador.
         */
        UpdateRewardTexts();


        Debug.Log(
            $"[REWARD] Purchased " +
            $"'{GetStickerName(sticker)}' " +
            $"for {GetPriceLogString(price)}. " +
            $"Purchases this phase: " +
            $"{PurchasesThisPhase}. " +
            $"Next multiplier: " +
            $"x{CurrentPurchaseMultiplier}."
        );


        return true;
    }


    // =========================================================
    // PAY PURCHASE PRICE
    // =========================================================

    private bool TryPayPurchasePrice(
        int price)
    {
        if (price <= 0)
            return true;


        // -----------------------------------------------------
        // BLOOD
        // -----------------------------------------------------

        if (CurrentPurchaseCurrency ==
            PurchaseCurrency.Blood)
        {
            if (BloodManager.Instance == null)
            {
                Debug.LogWarning(
                    "[REWARD] BloodManager missing."
                );

                return false;
            }


            if (BloodManager.Instance.currentBlood <
                price)
            {
                return false;
            }


            return
                BloodManager.Instance
                    .ConsumeBlood(
                        price
                    );
        }


        // -----------------------------------------------------
        // COIN
        // -----------------------------------------------------

        if (CurrencyManager.Instance == null)
        {
            Debug.LogWarning(
                "[REWARD] CurrencyManager missing."
            );

            return false;
        }


        if (!CurrencyManager.Instance
            .CanAfford(price))
        {
            return false;
        }


        return
            CurrencyManager.Instance
                .Spend(price);
    }


    // =========================================================
    // FAILED PURCHASE LOG
    // =========================================================

    private void LogFailedPurchase(
        BaseSticker sticker,
        int price)
    {
        if (CurrentPurchaseCurrency ==
            PurchaseCurrency.Blood)
        {
            int available =
                BloodManager.Instance != null
                    ? BloodManager.Instance.currentBlood
                    : 0;


            Debug.Log(
                $"[REWARD] Cannot buy " +
                $"'{GetStickerName(sticker)}'. " +
                $"Need {price} Blood, " +
                $"have {available}."
            );
        }

        else
        {
            int available =
                CurrencyManager.Instance != null
                    ? CurrencyManager.Instance.dollars
                    : 0;


            Debug.Log(
                $"[REWARD] Cannot buy " +
                $"'{GetStickerName(sticker)}'. " +
                $"Need ${price}, " +
                $"have ${available}."
            );
        }
    }


    // =========================================================
    // GENERATE OFFERS
    // =========================================================

    private void GenerateOffers()
    {
        ClearRemainingOffers();


        UpdateRewardTexts();


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


        UpdateRewardTexts();


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

        /*
         * Reroll siempre cuesta Blood.
         *
         * No está afectado ni por Change Currency
         * ni por Blood Price Multiplier.
         */
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
        ClearRemainingOffers();


        UpdateRewardTexts();


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
    // REWARD TEXTS
    // =========================================================

    private void UpdateRewardTexts()
    {
        UpdateOfferPriceText(
            rewardSlotAPriceText,
            currentOfferA
        );


        UpdateOfferPriceText(
            rewardSlotBPriceText,
            currentOfferB
        );


        // -----------------------------------------------------
        // REROLL
        // -----------------------------------------------------

        if (rerollCostText != null)
        {
            /*
             * Solo mostramos el número.
             *
             * 5 → "5"
             * 1 → "1"
             * 0 → "0"
             */
            rerollCostText.text =
                rerollBloodCost.ToString();
        }
    }


    private void UpdateOfferPriceText(
        TMP_Text priceText,
        GameObject offer)
    {
        if (priceText == null)
            return;


        BaseSticker sticker =
            GetOfferSticker(
                offer
            );


        if (sticker == null)
        {
            priceText.text = "";
            return;
        }


        int price =
            GetCurrentPurchasePrice(
                sticker
            );


        // -----------------------------------------------------
        // BLOOD
        // -----------------------------------------------------

        if (CurrentPurchaseCurrency ==
            PurchaseCurrency.Blood)
        {
            /*
             * Blood:
             * número sin símbolo.
             */
            priceText.text =
                price.ToString();


            priceText.color =
                bloodPriceColor;
        }


        // -----------------------------------------------------
        // COIN
        // -----------------------------------------------------

        else
        {
            priceText.text =
                $"${price}";


            priceText.color =
                coinPriceColor;
        }
    }


    // =========================================================
    // OFFER STICKER
    // =========================================================

    private BaseSticker GetOfferSticker(
        GameObject offer)
    {
        if (offer == null)
            return null;


        return
            offer.GetComponentInChildren<BaseSticker>(
                true
            );
    }


    // =========================================================
    // PRICE LOG STRING
    // =========================================================

    private string GetPriceLogString(
        int price)
    {
        if (CurrentPurchaseCurrency ==
            PurchaseCurrency.Blood)
        {
            return
                $"{price} Blood";
        }


        return
            $"${price}";
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
