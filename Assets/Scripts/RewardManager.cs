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

    [Tooltip("Punto donde aparecerá la primera oferta.")]
    public Transform rewardSlotA;

    [Tooltip("Punto donde aparecerá la segunda oferta.")]
    public Transform rewardSlotB;


    [Header("Buttons")]
    public Collider2D rerollButtonCollider;
    public Collider2D skipButtonCollider;


    // =========================================================
    // REWARD POOL
    // =========================================================

    [Header("Reward Pool")]

    [Tooltip(
        "Prefabs de stickers que pueden aparecer como recompensa."
    )]
    public GameObject[] stickerPrefabs;


    // =========================================================
    // REROLL
    // =========================================================

    [Header("Reroll")]

    [Tooltip("Coste en sangre de hacer reroll.")]
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

    private GameObject currentOfferA;
    private GameObject currentOfferB;

    private Camera cam;


    // =========================================================
    // EVENTS
    // =========================================================

    /*
     * Más adelante RoundManager podrá utilizar este evento
     * para comenzar la siguiente ronda.
     *
     * Se dispara tanto al comprar un sticker como al hacer Skip.
     */
    public event Action OnRewardPhaseCompleted;


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
        /*
         * RewardScreen debe comenzar cerrado.
         *
         * Aunque accidentalmente lo dejemos activo en Editor,
         * el manager corrige el estado al empezar.
         */
        if (rewardPanel != null)
        {
            rewardPanel.SetActive(false);
        }

        RewardPhaseActive = false;
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

        rewardPanel.SetActive(true);

        GenerateOffers();

        Debug.Log(
            "[REWARD] Reward phase started."
        );
    }


    // =========================================================
    // GENERATE OFFERS
    // =========================================================

    private void GenerateOffers()
    {
        ClearOffers();


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
            GetSecondRewardIndex(indexA);


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
            $"{GetOfferName(currentOfferB)}"
        );
    }


    // =========================================================
    // SECOND RANDOM STICKER
    // =========================================================

    private int GetSecondRewardIndex(
        int firstIndex)
    {
        /*
         * Si solamente tenemos un sticker configurado
         * permitimos repetirlo para no romper el sistema
         * durante desarrollo.
         */
        if (stickerPrefabs.Length <= 1)
            return firstIndex;


        int secondIndex =
            firstIndex;


        /*
         * Evitamos que las dos ofertas sean
         * exactamente el mismo prefab.
         */
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
    // SPAWN OFFER
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


        /*
         * IMPORTANTE:
         *
         * Lo instanciamos en la posición del slot,
         * pero NO como hijo del slot.
         *
         * Los stickers ya tienen su propia jerarquía/root
         * y BaseSticker se ocupa después de parentarlos
         * a Roulette o Album.
         */
        GameObject instance =
            Instantiate(
                prefab,
                slot.position,
                slot.rotation
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
                "[REWARD] Free reroll."
            );

            return true;
        }


        // -----------------------------------------------------
        // BLOOD CHECK
        // -----------------------------------------------------

        if (BloodManager.Instance == null)
        {
            Debug.LogWarning(
                "[REWARD] BloodManager missing. " +
                "Cannot pay reroll cost."
            );

            return false;
        }


        /*
         * BloodManager.ConsumeBlood() actualmente permite
         * consumir más sangre de la disponible y simplemente
         * clampa a 0.
         *
         * Para una compra/reroll queremos exigir que
         * realmente puedas pagar el coste completo.
         */
        if (BloodManager.Instance.currentBlood <
            rerollBloodCost)
        {
            Debug.Log(
                $"[REWARD] Cannot reroll. " +
                $"Need {rerollBloodCost} blood, " +
                $"have {BloodManager.Instance.currentBlood}."
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
            $"[REWARD] Rerolled offers for " +
            $"{rerollBloodCost} blood."
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
            "[REWARD] Reward skipped."
        );


        CompleteRewardPhase();
    }


    // =========================================================
    // PURCHASE COMPLETED
    // =========================================================

    /*
     * RewardStickerOffer llamará a este método
     * cuando uno de los dos stickers se haya comprado
     * satisfactoriamente.
     */
    public void NotifyStickerPurchased(
        GameObject purchasedSticker)
    {
        if (!RewardPhaseActive)
            return;


        /*
         * El comprado ya pertenece al jugador.
         * NO debemos destruirlo.
         *
         * Destruimos solamente la otra oferta.
         */

        if (currentOfferA != null &&
            currentOfferA != purchasedSticker)
        {
            Destroy(currentOfferA);
        }


        if (currentOfferB != null &&
            currentOfferB != purchasedSticker)
        {
            Destroy(currentOfferB);
        }


        currentOfferA = null;
        currentOfferB = null;


        Debug.Log(
            $"[REWARD] Sticker purchased: " +
            $"{GetOfferName(purchasedSticker)}"
        );


        CompleteRewardPhase(
            clearOffers: false
        );
    }


    // =========================================================
    // COMPLETE PHASE
    // =========================================================

    private void CompleteRewardPhase(
        bool clearOffers = true)
    {
        if (clearOffers)
        {
            ClearOffers();
        }


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
    // CLEAR OFFERS
    // =========================================================

    private void ClearOffers()
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

    /*
     * Esto nos permite probar RewardScreen AHORA,
     * antes de tocar RoundManager.
     *
     * Selecciona RewardManager en Inspector,
     * menú de los tres puntos del componente:
     *
     * DEBUG - Begin Reward Phase
     */
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
            return "NULL";


        BaseSticker sticker =
            offer.GetComponentInChildren<BaseSticker>(
                true
            );


        if (sticker != null &&
            sticker.effect != null &&
            !string.IsNullOrEmpty(
                sticker.effect.stickerName
            ))
        {
            return sticker.effect.stickerName;
        }


        return offer.name;
    }
}
