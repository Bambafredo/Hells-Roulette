using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InfestationManager : MonoBehaviour
{
    public static InfestationManager Instance;


    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("Shared Reward Screen")]

    [Tooltip(
        "Existing RewardManager. Infestation reuses only the shared modal " +
        "Reward Panel, Background, Skip button and Enemy Camera references."
    )]
    public RewardManager rewardManager;


    [Header("Infestation Panel")]

    [Tooltip(
        "Reward_Panel/Infestation_Panel. This panel is inactive by default."
    )]
    public Transform infestationPanel;


    [Tooltip(
        "Assign the three Infestation sticker slots in order."
    )]
    public Transform[] infestationSlots =
        new Transform[3];


    [Tooltip(
        "Prefab of the Leech sticker that this infestation grants."
    )]
    public GameObject leechStickerPrefab;


    [Tooltip(
        "If enabled, unused slot GameObjects are hidden when the action grants " +
        "fewer than three Leeches."
    )]
    public bool hideUnusedSlots =
        true;


    // =========================================================
    // RUNTIME STATE
    // =========================================================

    public bool InfestationActive
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
     * Enemy actions do NOT open the modal immediately.
     *
     * Every Gain Leech request produced during the same spin is accumulated
     * here. RoundManager then gives us one callback after ALL enemy actions
     * have resolved but before Debt / Rewards begin.
     *
     * Example:
     * Enemy A -> Gain 1 Leech
     * Enemy B -> Gain 1 Leech
     *
     * pendingLeechesThisResolution = 2
     * -> one Infestation panel with 2 Leeches.
     */
    private int pendingLeechesThisResolution =
        0;


    /*
     * The panel physically supports up to three slots.
     * If several enemy actions total more than three Leeches in one spin,
     * overflow is split into the minimum number of batches:
     *
     * 5 Leeches -> 3, then 2.
     */
    private readonly Queue<int>
        pendingInfestationBatches =
            new Queue<int>();


    private RoundManager subscribedRoundManager;


    private bool enemyCameraWasEnabled =
        false;


    private Camera inputCamera;


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(this);
            return;
        }


        Instance =
            this;


        ResolveReferences();


        inputCamera =
            Camera.main;
    }


    private void Start()
    {
        EnsureRoundManagerSubscription();


        if (infestationPanel != null)
        {
            infestationPanel
                .gameObject
                .SetActive(
                    false
                );
        }
    }


    private void Update()
    {
        EnsureRoundManagerSubscription();


#if UNITY_EDITOR || UNITY_STANDALONE

        if (!InfestationActive)
            return;


        if (ClaimsCompleted <
            ClaimsRequired)
        {
            return;
        }


        if (!Input.GetMouseButtonDown(0))
            return;


        ResolveReferences();


        if (rewardManager == null ||
            rewardManager.skipButtonCollider == null)
        {
            return;
        }


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
                .gameObject
                .activeInHierarchy &&
            rewardManager
                .skipButtonCollider
                .OverlapPoint(
                    mouseWorld
                ))
        {
            ContinueAfterClaimedBatch();
        }

#endif
    }


    private void OnDestroy()
    {
        UnsubscribeFromRoundManager();


        if (Instance == this)
        {
            Instance =
                null;
        }
    }


    // =========================================================
    // PUBLIC REQUEST API
    // =========================================================

    /// <summary>
    /// Records a forced Leech consequence from one enemy action.
    ///
    /// All requests created by the same spin are aggregated before the modal
    /// opens. EnemyAction assets never control UI directly.
    /// </summary>
    public bool RequestInfestation(
        int leechCount)
    {
        ResolveReferences();
        EnsureRoundManagerSubscription();


        int safeCount =
            Mathf.Clamp(
                leechCount,
                1,
                3
            );


        if (!ValidateSetup(
                safeCount))
        {
            return false;
        }


        /*
         * IMPORTANT:
         * Do not open Reward_Panel from inside an individual enemy callback.
         *
         * BaseEnemy instances execute sequentially through the same OnSpinEnd
         * event. Opening immediately means the first enemy creates a modal
         * before the remaining enemies have had their turn.
         *
         * We only record the consequence here.
         */
        pendingLeechesThisResolution +=
            safeCount;


        Debug.Log(
            $"[INFESTATION] Queued {safeCount} Leech(es) for this " +
            $"gameplay resolution. Pending total = " +
            $"{pendingLeechesThisResolution}."
        );


        /*
         * Normal gameplay always has RoundManager and will flush through
         * OnGameplaySpinResolutionCompleted.
         *
         * This fallback keeps direct/debug execution usable in stripped scenes.
         */
        if (subscribedRoundManager == null)
        {
            FlushPendingInfestationRequests();
        }


        return true;
    }


    // =========================================================
    // POST-GAMEPLAY AGGREGATION
    // =========================================================

    private void HandleGameplaySpinResolutionCompleted()
    {
        FlushPendingInfestationRequests();
    }


    private void FlushPendingInfestationRequests()
    {
        if (pendingLeechesThisResolution <= 0)
            return;


        int totalLeeches =
            pendingLeechesThisResolution;


        pendingLeechesThisResolution =
            0;


        int batchCapacity =
            GetBatchCapacity();


        if (batchCapacity <= 0)
        {
            Debug.LogError(
                "[INFESTATION] No valid Infestation slots are available."
            );

            return;
        }


        /*
         * Combine every request from this spin first, THEN split only if the
         * physical three-slot panel cannot display the whole result.
         *
         * 1 + 1 -> one batch of 2
         * 1 + 2 -> one batch of 3
         * 2 + 2 -> batches 3 + 1
         */
        QueueBatches(
            totalLeeches,
            batchCapacity
        );


        if (!InfestationActive)
        {
            BeginNextQueuedBatch();
        }


        Debug.Log(
            $"[INFESTATION] Aggregated {totalLeeches} Leech(es) from the " +
            "completed enemy phase."
        );
    }


    private void QueueBatches(
        int totalLeeches,
        int batchCapacity)
    {
        int remaining =
            Mathf.Max(
                0,
                totalLeeches
            );


        while (remaining > 0)
        {
            int batch =
                Mathf.Min(
                    batchCapacity,
                    remaining
                );


            pendingInfestationBatches
                .Enqueue(
                    batch
                );


            remaining -=
                batch;
        }
    }


    private void BeginNextQueuedBatch()
    {
        if (InfestationActive ||
            pendingInfestationBatches.Count <= 0)
        {
            return;
        }


        int nextCount =
            pendingInfestationBatches
                .Dequeue();


        BeginInfestationBatch(
            nextCount
        );
    }


    // =========================================================
    // BEGIN BATCH
    // =========================================================

    private void BeginInfestationBatch(
        int leechCount)
    {
        ClearCurrentOffers();


        ClaimsRequired =
            0;

        ClaimsCompleted =
            0;


        if (!InfestationActive)
        {
            InfestationActive =
                true;


            // -------------------------------------------------
            // GENERIC GAMEPLAY LOCK
            // -------------------------------------------------

            RoundManager.Instance?
                .SetExternalSpinBlock(
                    true
                );


            // -------------------------------------------------
            // MODAL ENEMY VIEW
            // -------------------------------------------------

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
        }


        ShowInfestationOnlyView(
            leechCount
        );


        currentOffers =
            new GameObject[
                leechCount
            ];


        for (int i = 0;
             i < leechCount;
             i++)
        {
            Transform slot =
                GetSlot(
                    i
                );


            GameObject offer =
                SpawnInfestationOffer(
                    leechStickerPrefab,
                    slot
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
            $"[INFESTATION] Opened with {ClaimsRequired} Leech(es)."
        );
    }


    // =========================================================
    // CLAIM
    // =========================================================

    public bool TryClaimOffer(
        GameObject offerObject,
        BaseSticker sticker)
    {
        if (!InfestationActive ||
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
         * InfestationStickerOffer already verified that this Leech ended in
         * a valid player-owned location (Album or Roulette). Detach it before
         * cleanup so it remains player property.
         */
        currentOffers[index] =
            null;


        ClaimsCompleted =
            Mathf.Min(
                ClaimsRequired,
                ClaimsCompleted + 1
            );


        Debug.Log(
            $"[INFESTATION] Claimed '{GetStickerName(sticker)}'. " +
            $"{ClaimsCompleted}/{ClaimsRequired}."
        );


        UpdateSkipVisibility();


        return true;
    }


    // =========================================================
    // CONTINUE / COMPLETE
    // =========================================================

    private void ContinueAfterClaimedBatch()
    {
        if (!InfestationActive ||
            ClaimsCompleted <
                ClaimsRequired)
        {
            return;
        }


        ClearCurrentOffers();


        if (pendingInfestationBatches.Count > 0)
        {
            int nextCount =
                pendingInfestationBatches
                    .Dequeue();


            BeginInfestationBatch(
                nextCount
            );


            return;
        }


        /*
         * Defensive: if a request somehow arrived while the modal was already
         * open, fold it in before releasing gameplay.
         */
        if (pendingLeechesThisResolution > 0)
        {
            int pending =
                pendingLeechesThisResolution;


            pendingLeechesThisResolution =
                0;


            QueueBatches(
                pending,
                GetBatchCapacity()
            );


            if (pendingInfestationBatches.Count > 0)
            {
                int nextCount =
                    pendingInfestationBatches
                        .Dequeue();


                BeginInfestationBatch(
                    nextCount
                );


                return;
            }
        }


        CompleteInfestation();
    }


    private void CompleteInfestation()
    {
        ClearCurrentOffers();


        InfestationActive =
            false;

        ClaimsRequired =
            0;

        ClaimsCompleted =
            0;


        SetTransformActive(
            infestationPanel,
            false
        );


        SetColliderObjectActive(
            rewardManager != null
                ? rewardManager
                    .skipButtonCollider
                : null,
            false
        );


        if (rewardManager != null &&
            rewardManager.rewardPanel != null)
        {
            rewardManager
                .rewardPanel
                .SetActive(
                    false
                );
        }


        /*
         * Restore the corridor BEFORE releasing the RoundManager lock.
         *
         * If this was the final spin, SetExternalSpinBlock(false) can
         * synchronously resume Debt -> Reward Phase. RewardManager is then free
         * to disable EnemyCamera and reopen Reward_Panel for its own modal.
         */
        if (rewardManager != null &&
            rewardManager.enemyCamera != null)
        {
            rewardManager
                .enemyCamera
                .enabled =
                    enemyCameraWasEnabled;
        }


        RoundManager.Instance?
            .SetExternalSpinBlock(
                false
            );


        Debug.Log(
            "[INFESTATION] Completed."
        );
    }


    // =========================================================
    // VIEW
    // =========================================================

    private void ShowInfestationOnlyView(
        int leechCount)
    {
        // Shared Reward background stays visible.
        SetGameObjectActive(
            rewardManager
                .regularRewardBackground,
            true
        );


        // Clean Row Bonus hidden.
        SetTransformActive(
            rewardManager
                .rewardBonusSlot,
            false
        );


        // Normal shop hidden.
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


        // Infestation itself.
        SetTransformActive(
            infestationPanel,
            true
        );


        UpdateSlotVisibility(
            leechCount
        );


        // Cannot continue before ALL forced Leeches are claimed somewhere.
        SetColliderObjectActive(
            rewardManager
                .skipButtonCollider,
            false
        );
    }


    private void UpdateSlotVisibility(
        int leechCount)
    {
        if (infestationSlots == null)
            return;


        for (int i = 0;
             i < infestationSlots.Length;
             i++)
        {
            Transform slot =
                infestationSlots[i];


            if (slot == null)
                continue;


            if (!hideUnusedSlots)
            {
                slot.gameObject
                    .SetActive(
                        true
                    );

                continue;
            }


            slot.gameObject
                .SetActive(
                    i < leechCount
                );
        }
    }


    private void UpdateSkipVisibility()
    {
        if (rewardManager == null)
            return;


        bool show =
            InfestationActive &&
            ClaimsCompleted >=
                ClaimsRequired;


        SetColliderObjectActive(
            rewardManager
                .skipButtonCollider,
            show
        );
    }


    // =========================================================
    // SPAWN
    // =========================================================

    private GameObject SpawnInfestationOffer(
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


        InfestationStickerOffer offer =
            instance
                .GetComponent<InfestationStickerOffer>();


        if (offer == null)
        {
            offer =
                instance
                    .AddComponent<InfestationStickerOffer>();
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
    // ROUND RESOLUTION SUBSCRIPTION
    // =========================================================

    private void EnsureRoundManagerSubscription()
    {
        RoundManager target =
            RoundManager.Instance;


        if (target == null)
        {
            target =
                FindObjectOfType<RoundManager>();
        }


        if (target ==
            subscribedRoundManager)
        {
            return;
        }


        UnsubscribeFromRoundManager();


        subscribedRoundManager =
            target;


        if (subscribedRoundManager != null)
        {
            subscribedRoundManager
                .OnGameplaySpinResolutionCompleted +=
                    HandleGameplaySpinResolutionCompleted;
        }
    }


    private void UnsubscribeFromRoundManager()
    {
        if (subscribedRoundManager == null)
            return;


        subscribedRoundManager
            .OnGameplaySpinResolutionCompleted -=
                HandleGameplaySpinResolutionCompleted;


        subscribedRoundManager =
            null;
    }


    // =========================================================
    // SETUP / VALIDATION
    // =========================================================

    private int GetBatchCapacity()
    {
        if (infestationSlots == null)
            return 0;


        int validSlots =
            0;


        for (int i = 0;
             i < infestationSlots.Length;
             i++)
        {
            if (infestationSlots[i] != null)
            {
                validSlots++;
            }
        }


        return
            Mathf.Clamp(
                validSlots,
                0,
                3
            );
    }


    private void ResolveReferences()
    {
        if (rewardManager == null)
        {
            rewardManager =
                FindObjectOfType<RewardManager>(
                    true
                );
        }


        if (inputCamera == null)
        {
            inputCamera =
                Camera.main;
        }
    }


    private bool ValidateSetup(
        int requestedCount)
    {
        if (rewardManager == null ||
            rewardManager.rewardPanel == null)
        {
            Debug.LogError(
                "[INFESTATION] RewardManager / Reward Panel reference missing."
            );

            return false;
        }


        if (infestationPanel == null)
        {
            Debug.LogError(
                "[INFESTATION] Infestation Panel reference missing."
            );

            return false;
        }


        if (leechStickerPrefab == null)
        {
            Debug.LogError(
                "[INFESTATION] Leech Sticker Prefab reference missing."
            );

            return false;
        }


        for (int i = 0;
             i < requestedCount;
             i++)
        {
            if (GetSlot(i) == null)
            {
                Debug.LogError(
                    $"[INFESTATION] Missing slot for Leech #{i + 1}."
                );

                return false;
            }
        }


        return true;
    }


    private Transform GetSlot(
        int index)
    {
        if (infestationSlots == null ||
            index < 0 ||
            index >= infestationSlots.Length)
        {
            return null;
        }


        return
            infestationSlots[index];
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
