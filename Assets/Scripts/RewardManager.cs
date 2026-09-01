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


    public enum FirstPurchaseDiscountTarget
    {
        CoinOnly,
        BloodOnly,
        Both
    }


    public enum RewardStage
    {
        None,
        CleanRowBonus,
        StandardRewards
    }


    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("Reward Screen")]
    public GameObject rewardPanel;

    public Transform rewardSlotA;
    public Transform rewardSlotB;


    // =========================================================
    // CLEAN ROW BONUS
    // =========================================================

    [Header("Clean Row Bonus")]

    [Tooltip(
        "Master switch for the Clean Row Bonus. " +
        "Enabled by default so it can be disabled instantly for playtesting."
    )]
    public bool enableCleanRowBonus =
        true;


    [Tooltip(
        "Reward_Panel/RewardBonus. This acts as both the visual root and " +
        "the spawn/return slot for the single free sticker."
    )]
    public Transform rewardBonusSlot;


    [Tooltip(
        "Reward_Panel/Background. Shared by BOTH Clean Row Bonus and the normal " +
        "Reward Phase. If left empty, a direct child named 'Background' is used."
    )]
    public GameObject regularRewardBackground;


    // =========================================================
    // ENEMY VIEW
    // =========================================================

    [Header("Enemy View")]

    [Tooltip(
        "Optional camera that renders the enemy corridor. " +
        "It is temporarily disabled during the modal Reward Phase so the " +
        "world-space Reward Panel is never covered by the corridor camera."
    )]
    public Camera enemyCamera;


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
        "Moneda seleccionada por defecto cada vez " +
        "que comienza una nueva Reward Phase."
    )]
    public PurchaseCurrency defaultPurchaseCurrency =
        PurchaseCurrency.Coin;


    // =========================================================
    // CLEAR BONUS OFFER
    // =========================================================

    private void ClearBonusOffer()
    {
        if (currentBonusOffer == null)
            return;


        Destroy(
            currentBonusOffer
        );


        currentBonusOffer =
            null;
    }


    // =========================================================
    // REWARD TEXTS
    // =========================================================

    [Header("Reward Texts")]

    public TMP_Text rewardSlotAPriceText;
    public TMP_Text rewardSlotBPriceText;
    public TMP_Text rerollCostText;


    [Header("Reward Price Colors")]

    public Color bloodPriceColor = Color.red;
    public Color coinPriceColor = Color.yellow;

    [Tooltip(
        "Color del texto cuando un sticker es gratuito."
    )]
    public Color freePriceColor = Color.green;


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
        "Multiplicador adicional aplicado SOLO " +
        "al precio en Blood."
    )]
    [Min(1)]
    public int bloodPriceMultiplier = 1;


    // =========================================================
    // FIRST PURCHASE DISCOUNT
    // =========================================================

    [Header("First Purchase Discount")]

    [Tooltip(
        "Activa/desactiva el descuento especial."
    )]
    public bool enableFirstPurchaseDiscount = false;


    [Tooltip(
        "Moneda o monedas en las que puede utilizarse " +
        "el descuento."
    )]
    public FirstPurchaseDiscountTarget firstPurchaseDiscountTarget =
        FirstPurchaseDiscountTarget.CoinOnly;


    [Tooltip(
        "Porcentaje de descuento. " +
        "100 = FREE."
    )]
    [Range(0, 100)]
    public int firstPurchaseDiscountPercent = 0;


    // =========================================================
    // REROLL
    // =========================================================

    [Header("Reroll")]

    [Tooltip(
        "Coste base de Blood del reroll."
    )]
    [Min(0)]
    public int rerollBloodCost = 1;


    [Tooltip(
        "Si está activo, cada reroll utiliza " +
        "multiplicadores Fibonacci: x3, x5, x8, x13..."
    )]
    public bool enableFibonacciRerollMultiplier = false;


    // =========================================================
    // STATE
    // =========================================================

    public bool RewardPhaseActive
    {
        get;
        private set;
    }


    public RewardStage CurrentRewardStage
    {
        get;
        private set;
    } =
        RewardStage.None;


    public bool CleanRowBonusActive =>
        RewardPhaseActive &&
        CurrentRewardStage ==
            RewardStage.CleanRowBonus;


    public bool StandardRewardStageActive =>
        RewardPhaseActive &&
        CurrentRewardStage ==
            RewardStage.StandardRewards;


    /*
     * Número TOTAL de stickers adquiridos durante
     * esta Reward Phase.
     *
     * Incluye compras FREE.
     */
    public int PurchasesThisPhase
    {
        get;
        private set;
    }


    /*
     * Número de compras que SÍ han avanzado
     * la progresión x1, x2, x3...
     *
     * Una compra FREE causada por descuento 100%
     * NO entra aquí.
     */
    public int MultiplierPurchasesThisPhase
    {
        get;
        private set;
    }


    /*
     * Número de rerolls realizados durante
     * esta Reward Phase.
     */
    public int RerollsThisPhase
    {
        get;
        private set;
    }


    public PurchaseCurrency CurrentPurchaseCurrency
    {
        get;
        private set;
    }


    /*
     * El descuento tiene su propio estado.
     *
     * NO desaparece por comprar con una moneda
     * que no sea elegible para el descuento.
     */
    private bool firstPurchaseDiscountAvailable = false;


    /// <summary>
    /// 0 compras normales → x1
    /// 1 compra normal   → x2
    /// 2 compras normales → x3
    ///
    /// Las compras FREE por 100% discount
    /// no hacen avanzar este contador.
    /// </summary>
    public int CurrentPurchaseMultiplier
    {
        get
        {
            return
                MultiplierPurchasesThisPhase + 1;
        }
    }


    /// <summary>
    /// Coste real del reroll que se realizaría ahora.
    /// </summary>
    public int CurrentRerollCost
    {
        get
        {
            return CalculateCurrentRerollCost();
        }
    }


    private GameObject currentOfferA;
    private GameObject currentOfferB;

    private GameObject currentBonusOffer;

    private Camera cam;

    /*
     * Preserve the camera's previous state instead of blindly enabling it
     * when the Reward Phase closes.
     */
    private bool enemyCameraWasEnabledBeforeReward = false;


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


        CurrentPurchaseCurrency =
            defaultPurchaseCurrency;
    }


    private void Start()
    {
        ResolveCleanRowBonusReferences();


        if (rewardPanel != null)
        {
            rewardPanel.SetActive(false);
        }


        RewardPhaseActive = false;

        CurrentRewardStage =
            RewardStage.None;

        PurchasesThisPhase = 0;

        MultiplierPurchasesThisPhase = 0;

        RerollsThisPhase = 0;


        CurrentPurchaseCurrency =
            defaultPurchaseCurrency;


        ResetFirstPurchaseDiscount();


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
        // STANDARD-REWARD CONTROLS
        // -----------------------------------------------------

        if (StandardRewardStageActive)
        {
            // -------------------------------------------------
            // CHANGE CURRENCY
            // -------------------------------------------------

            if (changeCurrencyButtonCollider != null &&
                changeCurrencyButtonCollider.OverlapPoint(mouseWorld))
            {
                TogglePurchaseCurrency();
                return;
            }


            // -------------------------------------------------
            // REROLL
            // -------------------------------------------------

            if (rerollButtonCollider != null &&
                rerollButtonCollider.OverlapPoint(mouseWorld))
            {
                TryReroll();
                return;
            }
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
        /*
         * Preserve the old public/debug API:
         * BeginRewardPhase() opens the normal shop directly.
         *
         * RoundManager uses BeginRewardSequence(cleanRowCleared) so a Clean
         * Row Bonus can be inserted before the normal shop.
         */
        BeginRewardSequence(
            false
        );
    }


    /// <summary>
    /// Complete post-round reward flow.
    ///
    /// Clean Row:
    ///   Free sticker stage -> normal Reward Phase -> next round.
    ///
    /// No Clean Row:
    ///   Normal Reward Phase -> next round.
    ///
    /// EnemyCamera remains disabled for the COMPLETE reward sequence, exactly
    /// like the pre-existing modal Reward Phase. No overlay camera/layer is
    /// required.
    /// </summary>
    public void BeginRewardSequence(
        bool cleanRowCleared)
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


        ResolveCleanRowBonusReferences();


        // -----------------------------------------------------
        // MODAL ENEMY VIEW
        // -----------------------------------------------------

        /*
         * This is the EXISTING project solution:
         * Reward_Panel is rendered by Main Camera while EnemyCamera is
         * temporarily disabled for the whole modal reward sequence.
         */
        if (enemyCamera != null)
        {
            enemyCameraWasEnabledBeforeReward =
                enemyCamera.enabled;

            enemyCamera.enabled =
                false;
        }


        RewardPhaseActive =
            true;

        CurrentRewardStage =
            RewardStage.None;


        // -----------------------------------------------------
        // RESET NORMAL SHOP STATE ONCE PER REWARD SEQUENCE
        // -----------------------------------------------------

        PurchasesThisPhase =
            0;

        MultiplierPurchasesThisPhase =
            0;

        RerollsThisPhase =
            0;


        CurrentPurchaseCurrency =
            defaultPurchaseCurrency;


        ResetFirstPurchaseDiscount();


        OnPurchaseCountChanged?
            .Invoke(
                PurchasesThisPhase
            );


        rewardPanel.SetActive(
            true
        );


        // -----------------------------------------------------
        // OPTIONAL CLEAN ROW BONUS FIRST
        // -----------------------------------------------------

        if (CanStartCleanRowBonus(
                cleanRowCleared))
        {
            BeginCleanRowBonusStage();
            return;
        }


        BeginStandardRewardStage();
    }


    // =========================================================
    // CLEAN ROW BONUS STAGE
    // =========================================================

    private bool CanStartCleanRowBonus(
        bool cleanRowCleared)
    {
        if (!enableCleanRowBonus ||
            !cleanRowCleared)
        {
            return false;
        }


        if (rewardBonusSlot == null)
        {
            Debug.LogWarning(
                "[CLEAN ROW BONUS] Reward Bonus Slot is missing. " +
                "Opening the normal Reward Phase instead."
            );

            return false;
        }


        if (stickerPrefabs == null ||
            stickerPrefabs.Length <= 0)
        {
            Debug.LogWarning(
                "[CLEAN ROW BONUS] Sticker reward pool is empty. " +
                "Opening the normal Reward Phase instead."
            );

            return false;
        }


        return true;
    }


    private void BeginCleanRowBonusStage()
    {
        ClearRemainingOffers();
        ClearBonusOffer();


        CurrentRewardStage =
            RewardStage.CleanRowBonus;


        ShowCleanRowBonusView();


        int randomIndex =
            UnityEngine.Random.Range(
                0,
                stickerPrefabs.Length
            );


        currentBonusOffer =
            SpawnOffer(
                stickerPrefabs[randomIndex],
                rewardBonusSlot,
                RewardStickerOffer.OfferMode.FreeClaim
            );


        if (currentBonusOffer == null)
        {
            Debug.LogWarning(
                "[CLEAN ROW BONUS] Could not spawn the free sticker. " +
                "Opening the normal Reward Phase instead."
            );


            BeginStandardRewardStage();
            return;
        }


        Debug.Log(
            "[CLEAN ROW BONUS] CurrentRow cleared. " +
            $"Free sticker = {GetOfferName(currentBonusOffer)}."
        );
    }


    /// <summary>
    /// Claims the single free offer.
    ///
    /// RewardStickerOffer has already verified that BaseSticker successfully
    /// ended in Album or Roulette before this method is called.
    ///
    /// This does NOT:
    /// - spend Blood or Coins;
    /// - increment PurchasesThisPhase;
    /// - advance purchase multiplier;
    /// - consume the First Purchase Discount.
    /// </summary>
    public bool TryClaimFreeOffer(
        GameObject offerObject,
        BaseSticker sticker)
    {
        if (!CleanRowBonusActive ||
            offerObject == null ||
            sticker == null ||
            currentBonusOffer !=
                offerObject)
        {
            return false;
        }


        string stickerName =
            GetStickerName(
                sticker
            );


        /*
         * Detach the claimed object from bonus ownership BEFORE switching
         * views. BeginStandardRewardStage() clears any UNCLAIMED bonus offer.
         */
        currentBonusOffer =
            null;


        Debug.Log(
            $"[CLEAN ROW BONUS] Claimed '{stickerName}' for FREE."
        );


        BeginStandardRewardStage();


        return true;
    }


    // =========================================================
    // STANDARD REWARD STAGE
    // =========================================================

    private void BeginStandardRewardStage()
    {
        ClearBonusOffer();


        CurrentRewardStage =
            RewardStage.StandardRewards;


        ShowStandardRewardView();


        GenerateOffers();

        UpdateCurrencyButtonVisuals();
        UpdateRewardTexts();


        Debug.Log(
            "[REWARD] Standard Reward Phase started. " +
            $"Multiplier = x{CurrentPurchaseMultiplier}. " +
            $"Currency = {CurrentPurchaseCurrency}. " +
            $"Discount available = " +
            $"{firstPurchaseDiscountAvailable}."
        );
    }


    // =========================================================
    // REWARD VIEW MODES
    // =========================================================

    private void ResolveCleanRowBonusReferences()
    {
        if (regularRewardBackground == null &&
            rewardPanel != null)
        {
            Transform background =
                rewardPanel.transform
                    .Find(
                        "Background"
                    );


            if (background != null)
            {
                regularRewardBackground =
                    background.gameObject;
            }
        }
    }


    private void ShowCleanRowBonusView()
    {
        /*
         * Desired Clean Row presentation:
         *
         * Shared Reward background             = ON
         * RewardBonus + Bonus_Text (its child) = ON
         * SkipButton                           = ON
         *
         * Everything specific to the normal store = OFF.
         */
        SetActive(
            regularRewardBackground,
            true
        );


        SetTransformActive(
            rewardSlotA,
            false
        );

        SetTransformActive(
            rewardSlotB,
            false
        );


        SetTextActive(
            rewardSlotAPriceText,
            false
        );

        SetTextActive(
            rewardSlotBPriceText,
            false
        );


        SetTransformActive(
            rewardBonusSlot,
            true
        );


        SetColliderObjectActive(
            rerollButtonCollider,
            false
        );

        SetTextActive(
            rerollCostText,
            false
        );


        SetColliderObjectActive(
            changeCurrencyButtonCollider,
            false
        );

        SetTextActive(
            changeCurrencyButtonText,
            false
        );


        SetColliderObjectActive(
            skipButtonCollider,
            true
        );
    }


    private void ShowStandardRewardView()
    {
        SetActive(
            regularRewardBackground,
            true
        );


        SetTransformActive(
            rewardSlotA,
            true
        );

        SetTransformActive(
            rewardSlotB,
            true
        );


        SetTextActive(
            rewardSlotAPriceText,
            true
        );

        SetTextActive(
            rewardSlotBPriceText,
            true
        );


        SetTransformActive(
            rewardBonusSlot,
            false
        );


        SetColliderObjectActive(
            rerollButtonCollider,
            true
        );

        SetTextActive(
            rerollCostText,
            true
        );


        SetColliderObjectActive(
            changeCurrencyButtonCollider,
            true
        );

        SetTextActive(
            changeCurrencyButtonText,
            true
        );


        SetColliderObjectActive(
            skipButtonCollider,
            true
        );
    }


    private void SetActive(
        GameObject target,
        bool active)
    {
        if (target == null)
            return;


        target.SetActive(
            active
        );
    }


    private void SetTransformActive(
        Transform target,
        bool active)
    {
        if (target == null)
            return;


        target.gameObject
            .SetActive(
                active
            );
    }


    private void SetColliderObjectActive(
        Collider2D target,
        bool active)
    {
        if (target == null)
            return;


        target.gameObject
            .SetActive(
                active
            );
    }


    private void SetTextActive(
        TMP_Text target,
        bool active)
    {
        if (target == null)
            return;


        target.gameObject
            .SetActive(
                active
            );
    }


    // =========================================================
    // RESET FIRST PURCHASE DISCOUNT
    // =========================================================

    private void ResetFirstPurchaseDiscount()
    {
        firstPurchaseDiscountAvailable =
            enableFirstPurchaseDiscount &&
            firstPurchaseDiscountPercent > 0;
    }


    // =========================================================
    // CHANGE PURCHASE CURRENCY
    // =========================================================

    private void TogglePurchaseCurrency()
    {
        if (!StandardRewardStageActive)
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
            $"{CurrentPurchaseCurrency}. " +
            $"Multiplier remains " +
            $"x{CurrentPurchaseMultiplier}."
        );
    }


    private void UpdateCurrencyButtonVisuals()
    {
        bool usingBlood =
            CurrentPurchaseCurrency ==
            PurchaseCurrency.Blood;


        if (changeCurrencyButtonText != null)
        {
            changeCurrencyButtonText.text =
                usingBlood
                    ? "Blood"
                    : "Coin";
        }


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


        // -----------------------------------------------------
        // NORMAL GLOBAL PROGRESSION
        // -----------------------------------------------------

        int price =
            baseCost *
            CurrentPurchaseMultiplier;


        // -----------------------------------------------------
        // BLOOD BALANCE MULTIPLIER
        // -----------------------------------------------------

        if (CurrentPurchaseCurrency ==
            PurchaseCurrency.Blood)
        {
            price *=
                Mathf.Max(
                    1,
                    bloodPriceMultiplier
                );
        }


        // -----------------------------------------------------
        // DISCOUNT
        // -----------------------------------------------------

        if (ShouldApplyFirstPurchaseDiscount())
        {
            price =
                ApplyDiscount(
                    price,
                    firstPurchaseDiscountPercent
                );
        }


        return
            Mathf.Max(
                0,
                price
            );
    }


    // =========================================================
    // DISCOUNT ELIGIBILITY
    // =========================================================

    private bool ShouldApplyFirstPurchaseDiscount()
    {
        if (!firstPurchaseDiscountAvailable)
            return false;


        if (!enableFirstPurchaseDiscount)
            return false;


        if (firstPurchaseDiscountPercent <= 0)
            return false;


        switch (firstPurchaseDiscountTarget)
        {
            case FirstPurchaseDiscountTarget.CoinOnly:

                return
                    CurrentPurchaseCurrency ==
                    PurchaseCurrency.Coin;


            case FirstPurchaseDiscountTarget.BloodOnly:

                return
                    CurrentPurchaseCurrency ==
                    PurchaseCurrency.Blood;


            case FirstPurchaseDiscountTarget.Both:

                return true;
        }


        return false;
    }


    // =========================================================
    // DISCOUNT CALCULATION
    // =========================================================

    private int ApplyDiscount(
        int originalPrice,
        int discountPercent)
    {
        originalPrice =
            Mathf.Max(
                0,
                originalPrice
            );


        discountPercent =
            Mathf.Clamp(
                discountPercent,
                0,
                100
            );


        if (discountPercent >= 100)
            return 0;


        if (discountPercent <= 0)
            return originalPrice;


        float remaining =
            (100f - discountPercent) /
            100f;


        /*
         * Redondeamos hacia arriba.
         *
         * 5 con 50% off = 2.5 → 3.
         */
        return
            Mathf.CeilToInt(
                originalPrice *
                remaining
            );
    }


    // =========================================================
    // PURCHASE
    // =========================================================

    public bool TryPurchaseOffer(
        GameObject offerObject,
        BaseSticker sticker)
    {
        if (!StandardRewardStageActive)
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


        /*
         * Guardamos si ESTA compra está utilizando
         * el descuento antes de modificar ningún estado.
         */
        bool discountApplied =
            ShouldApplyFirstPurchaseDiscount();


        /*
         * Solo una compra con descuento 100%
         * se considera la compra FREE especial
         * que no avanza la progresión.
         */
        bool freeDiscountPurchase =
            discountApplied &&
            firstPurchaseDiscountPercent >= 100;


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

        if (isOfferA)
            currentOfferA = null;


        if (isOfferB)
            currentOfferB = null;


        // -----------------------------------------------------
        // CONSUME DISCOUNT
        // -----------------------------------------------------

        /*
         * IMPORTANTÍSIMO:
         *
         * El descuento SOLO se consume si la compra
         * realmente se ha realizado utilizando una
         * moneda elegible.
         *
         * Ejemplo Coin Only:
         *
         * comprar con Blood NO consume el FREE de Coin.
         */
        if (discountApplied)
        {
            firstPurchaseDiscountAvailable =
                false;
        }


        // -----------------------------------------------------
        // TOTAL PURCHASES
        // -----------------------------------------------------

        PurchasesThisPhase++;


        // -----------------------------------------------------
        // PRICE PROGRESSION
        // -----------------------------------------------------

        /*
         * Una compra FREE provocada por el descuento
         * del 100% NO avanza x1 → x2.
         *
         * Todas las demás compras sí.
         */
        if (!freeDiscountPurchase)
        {
            MultiplierPurchasesThisPhase++;
        }


        OnPurchaseCountChanged?
            .Invoke(PurchasesThisPhase);


        UpdateRewardTexts();


        Debug.Log(
            $"[REWARD] Purchased " +
            $"'{GetStickerName(sticker)}' " +
            $"for {GetPriceLogString(price)}. " +
            $"Total purchases = " +
            $"{PurchasesThisPhase}. " +
            $"Progression purchases = " +
            $"{MultiplierPurchasesThisPhase}. " +
            $"Next multiplier = " +
            $"x{CurrentPurchaseMultiplier}. " +
            $"Discount available = " +
            $"{firstPurchaseDiscountAvailable}."
        );


        return true;
    }


    // =========================================================
    // PAY PURCHASE PRICE
    // =========================================================

    private bool TryPayPurchasePrice(
        int price)
    {
        // -----------------------------------------------------
        // FREE
        // -----------------------------------------------------

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
        /*
         * Reroll:
         *
         * - NO toca el multiplicador
         * - NO consume el descuento
         * - NO afecta al número de compras
         *
         * Solo reemplaza las ofertas.
         */
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
            $"Purchase multiplier = " +
            $"x{CurrentPurchaseMultiplier}. " +
            $"Discount available = " +
            $"{firstPurchaseDiscountAvailable}."
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
        Transform slot,
        RewardStickerOffer.OfferMode offerMode =
            RewardStickerOffer.OfferMode.StandardPurchase)
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
            slot,
            offerMode
        );


        return instance;
    }


    // =========================================================
    // REROLL COST
    // =========================================================

    private int CalculateCurrentRerollCost()
    {
        int baseCost =
            Mathf.Max(
                0,
                rerollBloodCost
            );


        if (!enableFibonacciRerollMultiplier)
            return baseCost;


        int multiplier =
            GetFibonacciRerollMultiplier(
                RerollsThisPhase
            );


        long result =
            (long)baseCost *
            multiplier;


        /*
         * Protección absurda por si alguien hace
         * suficientes rerolls para desbordar un int.
         */
        if (result > int.MaxValue)
            return int.MaxValue;


        return (int)result;
    }


    // =========================================================
    // FIBONACCI
    // =========================================================

    /*
     * Index:
     *
     * 0 → 3
     * 1 → 5
     * 2 → 8
     * 3 → 13
     * 4 → 21
     * ...
     */
    private int GetFibonacciRerollMultiplier(
        int index)
    {
        index =
            Mathf.Max(
                0,
                index
            );


        if (index == 0)
            return 3;


        if (index == 1)
            return 5;


        long previous =
            3;

        long current =
            5;


        for (int i = 2;
             i <= index;
             i++)
        {
            long next =
                previous +
                current;


            previous =
                current;


            current =
                next;


            if (current >= int.MaxValue)
                return int.MaxValue;
        }


        return (int)current;
    }


    // =========================================================
    // REROLL
    // =========================================================

    public bool TryReroll()
    {
        if (!StandardRewardStageActive)
            return false;


        int currentCost =
            CurrentRerollCost;


        // -----------------------------------------------------
        // FREE REROLL
        // -----------------------------------------------------

        if (currentCost <= 0)
        {
            /*
             * Aunque sea gratis, cuenta como reroll
             * para avanzar Fibonacci.
             */
            RerollsThisPhase++;


            GenerateOffers();


            Debug.Log(
                "[REWARD] Free reroll. " +
                $"Rerolls this phase = " +
                $"{RerollsThisPhase}. " +
                $"Next reroll cost = " +
                $"{CurrentRerollCost}."
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
            currentCost)
        {
            Debug.Log(
                $"[REWARD] Cannot reroll. " +
                $"Need {currentCost} blood, " +
                $"have " +
                $"{BloodManager.Instance.currentBlood}."
            );

            return false;
        }


        bool paid =
            BloodManager.Instance
                .ConsumeBlood(
                    currentCost
                );


        if (!paid)
            return false;


        // -----------------------------------------------------
        // ADVANCE FIBONACCI
        // -----------------------------------------------------

        RerollsThisPhase++;


        /*
         * GenerateOffers NO toca ningún estado
         * de precios/descuento.
         */
        GenerateOffers();


        Debug.Log(
            $"[REWARD] Rerolled for " +
            $"{currentCost} blood. " +
            $"Rerolls this phase = " +
            $"{RerollsThisPhase}. " +
            $"Next reroll cost = " +
            $"{CurrentRerollCost}. " +
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


        // -----------------------------------------------------
        // CLEAN ROW BONUS
        // -----------------------------------------------------

        if (CleanRowBonusActive)
        {
            Debug.Log(
                "[CLEAN ROW BONUS] Free sticker skipped. " +
                "Opening the normal Reward Phase."
            );


            ClearBonusOffer();

            BeginStandardRewardStage();
            return;
        }


        // -----------------------------------------------------
        // STANDARD REWARD PHASE
        // -----------------------------------------------------

        if (!StandardRewardStageActive)
            return;


        Debug.Log(
            $"[REWARD] Reward phase skipped. " +
            $"Total purchases = " +
            $"{PurchasesThisPhase}. " +
            $"Progression purchases = " +
            $"{MultiplierPurchasesThisPhase}."
        );


        CompleteRewardPhase();
    }


    // =========================================================
    // COMPLETE PHASE
    // =========================================================

    private void CompleteRewardPhase()
    {
        ClearRemainingOffers();
        ClearBonusOffer();


        UpdateRewardTexts();


        RewardPhaseActive = false;

        CurrentRewardStage =
            RewardStage.None;


        if (rewardPanel != null)
        {
            rewardPanel.SetActive(false);
        }


        // -----------------------------------------------------
        // RESTORE ENEMY VIEW
        // -----------------------------------------------------

        if (enemyCamera != null)
        {
            enemyCamera.enabled =
                enemyCameraWasEnabledBeforeReward;
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
        // REROLL COST
        // -----------------------------------------------------

        if (rerollCostText != null)
        {
            /*
             * Solo número.
             *
             * Ejemplo Fibonacci:
             *
             * 3
             * 5
             * 8
             * 13
             */
            rerollCostText.text =
                CurrentRerollCost.ToString();
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
        // FREE
        // -----------------------------------------------------

        if (price <= 0)
        {
            priceText.text =
                "FREE";


            priceText.color =
                freePriceColor;


            return;
        }


        // -----------------------------------------------------
        // BLOOD
        // -----------------------------------------------------

        if (CurrentPurchaseCurrency ==
            PurchaseCurrency.Blood)
        {
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
        if (price <= 0)
            return "FREE";


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


    [ContextMenu("DEBUG - Begin Clean Row Bonus Sequence")]
    private void DebugBeginCleanRowBonusSequence()
    {
        BeginRewardSequence(
            true
        );
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
