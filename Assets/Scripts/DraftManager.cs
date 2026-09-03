using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum DraftRerollMode
{
    /*
     * Already claimed stickers stay in Album.
     * Only offers still sitting in DraftSlots are replaced.
     */
    UnclaimedOnly,

    /*
     * Reroll always replaces the complete draft.
     * The moment the player claims the first sticker, reroll is disabled.
     */
    AllOffersUntilFirstClaim
}


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
    // DRAFT REROLL
    // =========================================================

    [Header("Draft Reroll")]

    [Tooltip(
        "Collider of the reroll button used only by the starting draft."
    )]
    public Collider2D rerollButtonCollider;


    [Tooltip(
        "TMP child of the draft reroll button. Displays rerolls remaining."
    )]
    public TMPro.TMP_Text rerollsRemainingText;


    [Tooltip(
        "Number of free rerolls available at the beginning of the draft."
    )]
    [Min(0)]
    public int startingRerolls =
        1;


    [Tooltip(
        "Unclaimed Only: reroll replaces only offers not yet claimed. " +
        "All Offers Until First Claim: reroll replaces the complete draft, " +
        "but becomes unavailable as soon as one sticker is claimed."
    )]
    public DraftRerollMode rerollMode =
        DraftRerollMode.UnclaimedOnly;


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


    /*
     * Parallel slot array for the current draft.
     * This lets reroll replace only UNCLAIMED offers while preserving already
     * claimed stickers exactly like the normal shop reroll preserves purchases.
     */
    private Transform[] currentOfferSlots =
        new Transform[0];


    public int RerollsRemaining
    {
        get;
        private set;
    } =
        0;


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


        // -----------------------------------------------------
        // DRAFT REROLL
        // -----------------------------------------------------

        if (rerollButtonCollider != null &&
            rerollButtonCollider
                .gameObject
                .activeInHierarchy &&
            rerollButtonCollider
                .OverlapPoint(
                    mouseWorld
                ))
        {
            TryReroll();
            return;
        }


        // -----------------------------------------------------
        // CONTINUE / SKIP
        // -----------------------------------------------------

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


        if (rewardManager
                .skipButtonCollider
                .gameObject
                .activeInHierarchy &&
            rewardManager
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


        RerollsRemaining =
            Mathf.Max(
                0,
                startingRerolls
            );


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


        currentOfferSlots =
            new Transform[
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

            currentOfferSlots[i] =
                slots[i];


            if (offer != null)
            {
                ClaimsRequired++;
            }
        }


        UpdateDraftControls();


        Debug.Log(
            $"[DRAFT] Starting draft opened. " +
            $"Offers = {ClaimsRequired}. " +
            $"Rerolls = {RerollsRemaining}."
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


        UpdateDraftControls();


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


        RerollsRemaining =
            0;

        UpdateRerollText();


        SetColliderObjectActive(
            rerollButtonCollider,
            false
        );


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


        /*
         * Dedicated DRAFT reroll remains visible while there are unclaimed
         * offers. Its TMP shows how many free rerolls remain.
         */
        UpdateDraftControls();


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


        UpdateDraftControls();
    }


    private void UpdateDraftControls()
    {
        UpdateSkipVisibility();
        UpdateRerollVisibility();
        UpdateRerollText();
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


    private void UpdateRerollVisibility()
    {
        /*
         * The button stays visible at 0 rerolls so the TMP communicates the
         * exhausted resource clearly.
         *
         * Mode-specific availability:
         *
         * UnclaimedOnly
         * -> available while at least one draft offer is still unclaimed.
         *
         * AllOffersUntilFirstClaim
         * -> available only BEFORE the first sticker is claimed.
         *    Claiming 1 sticker immediately hides/disables reroll.
         */
        bool choicePhaseActive =
            DraftActive &&
            ClaimsCompleted <
                ClaimsRequired;


        bool modeAllowsReroll =
            false;


        switch (rerollMode)
        {
            case DraftRerollMode.AllOffersUntilFirstClaim:

                modeAllowsReroll =
                    ClaimsCompleted ==
                    0;

                break;


            case DraftRerollMode.UnclaimedOnly:
            default:

                modeAllowsReroll =
                    true;

                break;
        }


        bool showReroll =
            choicePhaseActive &&
            modeAllowsReroll;


        SetColliderObjectActive(
            rerollButtonCollider,
            showReroll
        );
    }


    private void UpdateRerollText()
    {
        if (rerollsRemainingText == null)
            return;


        rerollsRemainingText.text =
            Mathf.Max(
                0,
                RerollsRemaining
            )
            .ToString();
    }


    // =========================================================
    // REROLL
    // =========================================================

    public bool TryReroll()
    {
        if (!DraftActive)
            return false;


        if (ClaimsCompleted >=
            ClaimsRequired)
        {
            return false;
        }


        /*
         * In full-draft mode, claiming the first sticker locks the current
         * draft composition permanently. No partial reroll is allowed.
         */
        if (rerollMode ==
                DraftRerollMode.AllOffersUntilFirstClaim &&
            ClaimsCompleted > 0)
        {
            Debug.Log(
                "[DRAFT] Reroll is locked after the first claim in " +
                "All Offers mode."
            );

            UpdateDraftControls();

            return false;
        }


        if (RerollsRemaining <= 0)
        {
            Debug.Log(
                "[DRAFT] No rerolls remaining."
            );

            UpdateDraftControls();

            return false;
        }


        List<GameObject> pool =
            BuildValidPool();


        if (pool.Count <= 0)
        {
            Debug.LogWarning(
                "[DRAFT] Cannot reroll: Draft Pool is empty."
            );

            return false;
        }


        List<int> offerIndicesToReroll =
            new List<int>();


        switch (rerollMode)
        {
            // -------------------------------------------------
            // REROLL THE COMPLETE DRAFT
            // -------------------------------------------------

            case DraftRerollMode.AllOffersUntilFirstClaim:

                /*
                 * The guard above guarantees ClaimsCompleted == 0.
                 * Every currently available offer is replaced together.
                 */
                for (int i = 0;
                     i < currentOffers.Length;
                     i++)
                {
                    if (currentOffers[i] != null)
                    {
                        offerIndicesToReroll.Add(
                            i
                        );
                    }
                }

                break;


            // -------------------------------------------------
            // REROLL ONLY UNCLAIMED OFFERS
            // -------------------------------------------------

            case DraftRerollMode.UnclaimedOnly:
            default:

                /*
                 * Claimed stickers are already detached from currentOffers
                 * (their entry is null), so they remain untouched in Album.
                 */
                for (int i = 0;
                     i < currentOffers.Length;
                     i++)
                {
                    if (currentOffers[i] != null)
                    {
                        offerIndicesToReroll.Add(
                            i
                        );
                    }
                }

                break;
        }


        if (offerIndicesToReroll.Count <= 0)
            return false;


        /*
         * Only spend the reroll once we know there is something to replace.
         */
        RerollsRemaining--;


        foreach (int index in
                 offerIndicesToReroll)
        {
            if (currentOffers[index] != null)
            {
                Destroy(
                    currentOffers[index]
                );

                currentOffers[index] =
                    null;
            }
        }


        /*
         * Unique offers within THIS reroll result.
         * As in the shop, an offer seen on an earlier roll may appear again.
         */
        Shuffle(
            pool
        );


        int spawnCount =
            Mathf.Min(
                offerIndicesToReroll.Count,
                pool.Count
            );


        for (int i = 0;
             i < spawnCount;
             i++)
        {
            int offerIndex =
                offerIndicesToReroll[i];


            Transform slot =
                offerIndex >= 0 &&
                offerIndex <
                    currentOfferSlots.Length
                    ? currentOfferSlots[
                        offerIndex
                    ]
                    : null;


            currentOffers[offerIndex] =
                SpawnDraftOffer(
                    pool[i],
                    slot
                );
        }


        /*
         * Defensive fallback for an unexpectedly smaller runtime pool:
         * reduce the required claim count rather than soft-locking the draft.
         * In normal setup this path is never reached because the initial draft
         * already proved the pool could fill all active slots.
         */
        int missingOffers =
            offerIndicesToReroll.Count -
            spawnCount;


        if (missingOffers > 0)
        {
            ClaimsRequired =
                Mathf.Max(
                    ClaimsCompleted,
                    ClaimsRequired -
                    missingOffers
                );
        }


        UpdateDraftControls();


        Debug.Log(
            $"[DRAFT] Rerolled {spawnCount} offer(s) " +
            $"using mode {rerollMode}. " +
            $"Rerolls remaining = {RerollsRemaining}."
        );


        return true;
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

        currentOfferSlots =
            new Transform[0];
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