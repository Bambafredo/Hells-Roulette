using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(100)]
public class DraftManager : MonoBehaviour
{
    // =========================================================
    // CONFIGURATION
    // =========================================================

    [Header("Draft")]

    [Tooltip(
        "Master switch for the starting sticker draft."
    )]
    public bool enableStartingDraft =
        true;


    [Tooltip(
        "Existing RewardManager. DraftManager reuses only its shared modal " +
        "Reward Panel references; draft rules/pool/state remain here."
    )]
    public RewardManager rewardManager;


    [Tooltip(
        "Reward_Panel/DraftPanel."
    )]
    public Transform draftPanel;


    [Tooltip(
        "Assign DraftSlot_1, DraftSlot_2 and DraftSlot_3."
    )]
    public Transform[] draftSlots =
        new Transform[3];


    [Header("Draft Pool")]

    [Tooltip(
        "Separate sticker prefab pool used ONLY by the starting draft. " +
        "Selections are unique within one draft."
    )]
    public GameObject[] draftStickerPrefabs;


    // =========================================================
    // RUNTIME STATE
    // =========================================================

    public bool DraftActive
    {
        get;
        private set;
    } =
        false;


    public int ClaimsRequired
    {
        get;
        private set;
    } =
        0;


    public int ClaimsCompleted
    {
        get;
        private set;
    } =
        0;


    private GameObject[] currentOffers =
        new GameObject[0];


    private bool enemyCameraWasEnabled =
        false;


    private Camera inputCamera;


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        if (rewardManager == null)
        {
            rewardManager =
                FindObjectOfType<RewardManager>(
                    true
                );
        }


        inputCamera =
            Camera.main;
    }


    private void Start()
    {
        /*
         * DraftManager has DefaultExecutionOrder(100), while RewardManager
         * uses Unity's normal default order (0).
         *
         * That means RewardManager.Start() performs its initial UI reset first,
         * then DraftManager.Start() opens the draft in the SAME frame, before
         * Unity renders the first gameplay frame.
         *
         * We deliberately do NOT wait a frame here: doing so caused the enemy
         * screen to flash briefly before the draft appeared.
         *
         * IMPORTANT:
         * This component must live on an ACTIVE GameObject.
         * DraftPanel itself starts inactive, so do not put DraftManager on the
         * inactive DraftPanel if you want the draft to open automatically.
         */
        if (enableStartingDraft)
        {
            BeginStartingDraft();
        }
    }


    private void Update()
    {
#if UNITY_EDITOR || UNITY_STANDALONE

        if (!DraftActive)
            return;


        if (ClaimsCompleted <
            ClaimsRequired)
        {
            return;
        }


        if (rewardManager == null ||
            rewardManager.skipButtonCollider == null)
        {
            return;
        }


        if (!Input.GetMouseButtonDown(0))
            return;


        if (inputCamera == null)
        {
            inputCamera =
                Camera.main;
        }


        if (inputCamera == null)
            return;


        Vector2 mouseWorld =
            inputCamera.ScreenToWorldPoint(
                Input.mousePosition
            );


        if (rewardManager
                .skipButtonCollider
                .OverlapPoint(
                    mouseWorld
                ))
        {
            CompleteStartingDraft();
        }

#endif
    }


    private void OnDisable()
    {
        /*
         * Defensive cleanup only. Normally the manager itself stays active
         * throughout the run while DraftPanel is just a referenced visual root.
         */
        if (!DraftActive)
            return;


        RoundManager.Instance?
            .SetExternalSpinBlock(
                false
            );
    }


    // =========================================================
    // BEGIN DRAFT
    // =========================================================

    [ContextMenu("DEBUG - Begin Starting Draft")]
    public void BeginStartingDraft()
    {
        if (DraftActive)
            return;


        if (!enableStartingDraft)
            return;


        if (!ValidateSetup())
            return;


        ClearCurrentOffers();


        DraftActive =
            true;


        ClaimsCompleted =
            0;


        // -----------------------------------------------------
        // GENERIC GAMEPLAY LOCK
        // -----------------------------------------------------

        RoundManager.Instance?
            .SetExternalSpinBlock(
                true
            );


        // -----------------------------------------------------
        // MODAL VIEW
        // -----------------------------------------------------

        if (rewardManager.enemyCamera != null)
        {
            enemyCameraWasEnabled =
                rewardManager
                    .enemyCamera
                    .enabled;

            rewardManager
                .enemyCamera
                .enabled =
                    false;
        }


        rewardManager
            .rewardPanel
            .SetActive(
                true
            );


        ShowDraftOnlyView();


        // -----------------------------------------------------
        // RANDOM UNIQUE OFFERS
        // -----------------------------------------------------

        List<GameObject> pool =
            BuildValidPool();


        Shuffle(
            pool
        );


        List<Transform> slots =
            BuildValidSlots();


        int offerCount =
            Mathf.Min(
                slots.Count,
                pool.Count
            );


        currentOffers =
            new GameObject[
                offerCount
            ];


        ClaimsRequired =
            0;


        for (int i = 0;
             i < offerCount;
             i++)
        {
            GameObject offer =
                SpawnDraftOffer(
                    pool[i],
                    slots[i]
                );


            currentOffers[i] =
                offer;


            if (offer != null)
            {
                ClaimsRequired++;
            }
        }


        UpdateSkipVisibility();


        Debug.Log(
            $"[DRAFT] Starting draft opened. " +
            $"Offers = {ClaimsRequired}."
        );
    }


    private bool ValidateSetup()
    {
        if (rewardManager == null)
        {
            Debug.LogError(
                "[DRAFT] RewardManager reference missing."
            );

            return false;
        }


        if (rewardManager.rewardPanel == null)
        {
            Debug.LogError(
                "[DRAFT] RewardManager has no Reward Panel assigned."
            );

            return false;
        }


        if (draftPanel == null)
        {
            Debug.LogError(
                "[DRAFT] Draft Panel reference missing."
            );

            return false;
        }


        if (BuildValidSlots().Count <= 0)
        {
            Debug.LogError(
                "[DRAFT] No valid Draft Slots assigned."
            );

            return false;
        }


        if (BuildValidPool().Count <= 0)
        {
            Debug.LogError(
                "[DRAFT] Draft Pool is empty."
            );

            return false;
        }


        return true;
    }


    // =========================================================
    // CLAIM
    // =========================================================

    public bool TryClaimOffer(
        GameObject offerObject,
        BaseSticker sticker)
    {
        if (!DraftActive ||
            offerObject == null ||
            sticker == null)
        {
            return false;
        }


        int index =
            FindOfferIndex(
                offerObject
            );


        if (index < 0)
            return false;


        /*
         * DraftStickerOffer already verified that this physical sticker ended
         * in Album. Removing the reference prevents draft cleanup from
         * destroying the player's claimed sticker.
         */
        currentOffers[index] =
            null;


        ClaimsCompleted =
            Mathf.Min(
                ClaimsRequired,
                ClaimsCompleted + 1
            );


        Debug.Log(
            $"[DRAFT] Claimed '{GetStickerName(sticker)}'. " +
            $"{ClaimsCompleted}/{ClaimsRequired}."
        );


        UpdateSkipVisibility();


        return true;
    }


    // =========================================================
    // COMPLETE
    // =========================================================

    private void CompleteStartingDraft()
    {
        if (!DraftActive)
            return;


        if (ClaimsCompleted <
            ClaimsRequired)
        {
            return;
        }


        ClearCurrentOffers();


        DraftActive =
            false;


        SetTransformActive(
            draftPanel,
            false
        );


        if (rewardManager.rewardPanel != null)
        {
            rewardManager
                .rewardPanel
                .SetActive(
                    false
                );
        }


        if (rewardManager.enemyCamera != null)
        {
            rewardManager
                .enemyCamera
                .enabled =
                    enemyCameraWasEnabled;
        }


        SetColliderObjectActive(
            rewardManager.skipButtonCollider,
            false
        );


        RoundManager.Instance?
            .SetExternalSpinBlock(
                false
            );


        Debug.Log(
            "[DRAFT] Starting draft completed. Gameplay unlocked."
        );
    }


    // =========================================================
    // DRAFT VIEW
    // =========================================================

    private void ShowDraftOnlyView()
    {
        /*
         * Reward background is shared, just like Clean Row Bonus.
         */
        SetGameObjectActive(
            rewardManager
                .regularRewardBackground,
            true
        );


        SetTransformActive(
            draftPanel,
            true
        );


        // Clean Row Bonus hidden.
        SetTransformActive(
            rewardManager
                .rewardBonusSlot,
            false
        );


        // Standard shop hidden.
        SetTransformActive(
            rewardManager
                .rewardSlotA,
            false
        );

        SetTransformActive(
            rewardManager
                .rewardSlotB,
            false
        );


        SetTextActive(
            rewardManager
                .rewardSlotAPriceText,
            false
        );

        SetTextActive(
            rewardManager
                .rewardSlotBPriceText,
            false
        );


        SetColliderObjectActive(
            rewardManager
                .rerollButtonCollider,
            false
        );

        SetTextActive(
            rewardManager
                .rerollCostText,
            false
        );


        SetColliderObjectActive(
            rewardManager
                .changeCurrencyButtonCollider,
            false
        );

        SetTextActive(
            rewardManager
                .changeCurrencyButtonText,
            false
        );


        UpdateSkipVisibility();
    }


    private void UpdateSkipVisibility()
    {
        if (rewardManager == null)
            return;


        bool showSkip =
            DraftActive &&
            ClaimsCompleted >=
                ClaimsRequired;


        SetColliderObjectActive(
            rewardManager
                .skipButtonCollider,
            showSkip
        );
    }


    // =========================================================
    // SPAWN
    // =========================================================

    private GameObject SpawnDraftOffer(
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


        DraftStickerOffer offer =
            instance
                .GetComponent<DraftStickerOffer>();


        if (offer == null)
        {
            offer =
                instance
                    .AddComponent<DraftStickerOffer>();
        }


        offer.Initialize(
            this,
            instance,
            slot
        );


        return
            instance;
    }


    // =========================================================
    // POOL / SLOTS
    // =========================================================

    private List<GameObject> BuildValidPool()
    {
        List<GameObject> valid =
            new List<GameObject>();


        if (draftStickerPrefabs == null)
            return valid;


        foreach (GameObject prefab in
                 draftStickerPrefabs)
        {
            if (prefab != null)
            {
                valid.Add(
                    prefab
                );
            }
        }


        return
            valid;
    }


    private List<Transform> BuildValidSlots()
    {
        List<Transform> valid =
            new List<Transform>();


        if (draftSlots == null)
            return valid;


        foreach (Transform slot in
                 draftSlots)
        {
            if (slot != null)
            {
                valid.Add(
                    slot
                );
            }
        }


        return
            valid;
    }


    private void Shuffle(
        List<GameObject> list)
    {
        if (list == null)
            return;


        for (int i = list.Count - 1;
             i > 0;
             i--)
        {
            int j =
                Random.Range(
                    0,
                    i + 1
                );


            GameObject temp =
                list[i];

            list[i] =
                list[j];

            list[j] =
                temp;
        }
    }


    // =========================================================
    // CLEANUP / HELPERS
    // =========================================================

    private void ClearCurrentOffers()
    {
        if (currentOffers == null)
            return;


        foreach (GameObject offer in
                 currentOffers)
        {
            if (offer != null)
            {
                Destroy(
                    offer
                );
            }
        }


        currentOffers =
            new GameObject[0];
    }


    private int FindOfferIndex(
        GameObject offerObject)
    {
        if (currentOffers == null)
            return -1;


        for (int i = 0;
             i < currentOffers.Length;
             i++)
        {
            if (currentOffers[i] ==
                offerObject)
            {
                return i;
            }
        }


        return -1;
    }


    private string GetStickerName(
        BaseSticker sticker)
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


    private void SetGameObjectActive(
        GameObject target,
        bool active)
    {
        if (target != null)
        {
            target.SetActive(
                active
            );
        }
    }


    private void SetTransformActive(
        Transform target,
        bool active)
    {
        if (target != null)
        {
            target
                .gameObject
                .SetActive(
                    active
                );
        }
    }


    private void SetColliderObjectActive(
        Collider2D target,
        bool active)
    {
        if (target != null)
        {
            target
                .gameObject
                .SetActive(
                    active
                );
        }
    }


    private void SetTextActive(
        TMPro.TMP_Text target,
        bool active)
    {
        if (target != null)
        {
            target
                .gameObject
                .SetActive(
                    active
                );
        }
    }
}
