using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using System;

public enum GameOverReason
{
    BloodDepleted = 0,
    DebtUnpaid = 1,
    LimboMoneyDepleted = 2
}

public class RoundManager : MonoBehaviour
{
    public static RoundManager Instance;

    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("Refs")]
    public RouletteController controller;
    public FlagPin flagPin;

    [Tooltip(
        "Opcional. Si la escena tiene Reward Screen, " +
        "asigna aquí su RewardManager. " +
        "Si queda vacío se intenta encontrar automáticamente."
    )]
    public RewardManager rewardManager;


    [Header("Enemy Corridor")]

    [Tooltip(
        "Optional. Controls round-based enemy row progression and " +
        "the debt penalty for surviving enemies."
    )]
    public EnemyCorridorController enemyCorridorController;

    // =========================================================
    // VALID SPIN CONDITIONS
    // =========================================================

    [Header("Condiciones de tirada válida")]
    public int minHitsRequired = 2;

    [Tooltip(
        "Duración mínima (en segundos) que debe durar " +
        "el spin para considerarse válido."
    )]
    public float minSpinDuration = 0.25f;

    // =========================================================
    // ROUND / TOKENS
    // =========================================================

    [Header("Round / Tokens")]

    [Tooltip(
        "Número base de tiradas válidas disponibles por ronda."
    )]
    [Min(1)]
    public int tokensPerRound = 3;

    [SerializeField]
    private int currentRound = 0;

    [SerializeField]
    private int tokensRemaining = 0;

    // =========================================================
    // DEBT
    // =========================================================

    [Header("Debt")]

    [Tooltip("Deuda de la primera ronda (R-0).")]
    [Min(0)]
    public int startingDebt = 10;

    [Tooltip(
        "Cantidad que aumenta la deuda después " +
        "de cada ronda superada."
    )]
    [Min(0)]
    public int debtIncreasePerRound = 5;


    [Header("Enemy Debt Penalty")]

    [Tooltip(
        "Porcentaje que se añade a la deuda BASE de la siguiente ronda " +
        "cada vez que termina una ronda con enemigos vivos."
    )]
    [Min(0)]
    public int enemyDebtPenaltyPerFailedRoundPercent = 20;


    [SerializeField]
    private int currentDebt = 0;

    [SerializeField]
    private int currentBaseDebt = 0;

    [SerializeField]
    private int currentEnemyDebtPenaltyAmount = 0;

    [SerializeField]
    private int enemyDebtPenaltyPercent = 0;


    [Header("Active Enemy Curses")]

    [SerializeField]
    private int activeEnemyCurseDebtPercent = 0;

    [SerializeField]
    private int currentEnemyCurseDebtAmount = 0;

    /*
     * Keyed by physical enemy instance so:
     * - multiple Tax Collectors stack additively;
     * - the same enemy can never register twice;
     * - killing/removing one enemy removes only its own contribution.
     */
    private readonly Dictionary<BaseEnemy, int>
        activeEnemyDebtCurses =
            new Dictionary<BaseEnemy, int>();


    [Header("Active Sticker Interest")]

    [SerializeField]
    private int activeStickerInterestPercent = 0;

    [SerializeField]
    private int currentStickerInterestAmount = 0;

    [SerializeField]
    private int currentActiveInterestAmount = 0;

    /*
     * Keyed by physical sticker instance so:
     * - multiple Ladybugs stack additively;
     * - one physical sticker can never register twice;
     * - moving / destroying one Ladybug removes only its own contribution;
     * - registrations survive round transitions while the sticker stays in Album.
     */
    private readonly Dictionary<BaseSticker, int>
        activeStickerInterestSources =
            new Dictionary<BaseSticker, int>();


    [Header("Active Sticker Debt Discount")]

    [SerializeField]
    private int activeStickerDebtDiscountPercent = 0;

    [SerializeField]
    private int currentStickerDebtDiscountAmount = 0;

    /*
     * Keyed by physical sticker instance so several discount stickers stack
     * additively while each physical sticker can register only once.
     * The combined discount is clamped to 100%.
     */
    private readonly Dictionary<BaseSticker, int>
        activeStickerDebtDiscountSources =
            new Dictionary<BaseSticker, int>();

    /*
     * Debt-paid listeners may consume / destroy a physical discount sticker.
     * Unregistering during that callback must not rebuild an already-paid debt.
     */
    private bool debtSettlementInProgress = false;


    [SerializeField]
    private bool debtPending = false;


    // =========================================================
    // CLEAN ROW STREAK
    // =========================================================

    [Header("Clean Row Streak")]

    [SerializeField, Range(0, 4)]
    private int currentCleanRowStreak = 0;

    public int CurrentCleanRowStreak =>
        currentCleanRowStreak;


    public void ResetCleanRowStreak()
    {
        if (currentCleanRowStreak == 0)
            return;

        Debug.Log(
            $"[CLEAN ROW STREAK] Cashed out/reset from " +
            $"{currentCleanRowStreak} to 0."
        );

        currentCleanRowStreak = 0;
    }


    private void AdvanceCleanRowStreak()
    {
        int previous =
            currentCleanRowStreak;

        currentCleanRowStreak =
            Mathf.Clamp(
                currentCleanRowStreak + 1,
                0,
                4
            );

        Debug.Log(
            $"[CLEAN ROW STREAK] Clean Row: " +
            $"{previous} -> {currentCleanRowStreak}."
        );
    }


    private void BreakCleanRowStreak()
    {
        if (currentCleanRowStreak <= 0)
            return;

        Debug.Log(
            $"[CLEAN ROW STREAK] Row not cleared. " +
            $"Streak {currentCleanRowStreak} -> 0."
        );

        currentCleanRowStreak = 0;
    }


    // =========================================================
    // ENEMY ROUND OUTCOME
    // =========================================================

    private bool ResolveEnemyEncounterOutcome()
    {
        /*
         * Scenes without the enemy corridor keep the old debt behaviour and
         * cannot qualify for Clean Row Bonus.
         */
        if (enemyCorridorController == null)
            return false;


        bool encounterCleared =
            enemyCorridorController
                .IsCurrentEncounterCleared();


        // -----------------------------------------------------
        // SUCCESS: RESET THE SNOWBALL
        // -----------------------------------------------------

        if (encounterCleared)
        {
            if (enemyDebtPenaltyPercent != 0)
            {
                Debug.Log(
                    $"[ENEMY DEBT] Encounter cleared. " +
                    $"Penalty reset from {enemyDebtPenaltyPercent}% to 0%."
                );
            }
            else
            {
                Debug.Log(
                    "[ENEMY DEBT] Encounter cleared. " +
                    "Penalty remains at 0%."
                );
            }


            enemyDebtPenaltyPercent =
                0;
        }

        // -----------------------------------------------------
        // FAILURE: DEVIL INTEREST
        // -----------------------------------------------------

        else
        {
            int livingEnemies =
                enemyCorridorController
                    .GetLivingEnemyCountInCurrentRow();


            enemyDebtPenaltyPercent =
                Mathf.Max(
                    0,
                    enemyDebtPenaltyPercent +
                    enemyDebtPenaltyPerFailedRoundPercent
                );


            Debug.Log(
                $"[ENEMY DEBT] {livingEnemies} enemy/enemies survived. " +
                $"Next-round debt penalty is now " +
                $"{enemyDebtPenaltyPercent}%."
            );
        }


        if (encounterCleared)
        {
            AdvanceCleanRowStreak();
        }
        else
        {
            BreakCleanRowStreak();
        }


        UpdateEnemyDebtPenaltyUI();


        OnEnemyDebtPenaltyChanged?
            .Invoke(
                enemyDebtPenaltyPercent
            );


        return
            encounterCleared;
    }


    // =========================================================
    // REWARD PHASE
    // =========================================================

    [Header("Reward Phase")]

    [SerializeField]
    private bool waitingForRewardCompletion = false;

    // =========================================================
    // UI
    // =========================================================

    [Header("UI")]

    [Tooltip("Texto de ronda. Ejemplo: R-0")]
    public TMP_Text roundText;

    [Tooltip("Texto de deuda. Ejemplo: DEBT: $10")]
    public TMP_Text debtText;

    [Tooltip(
        "Muestra únicamente el porcentaje actual del extra de deuda. " +
        "Ejemplo: 40%"
    )]
    public TMP_Text enemyDebtPenaltyText;

    [Tooltip(
        "Opcional. Texto numérico de fichas, por ejemplo 2 / 3."
    )]
    public TMP_Text tokensText;

    [Tooltip(
        "GameObjects de las fichas, en orden. " +
        "Pueden ser UI Images con el sprite que quieras."
    )]
    public GameObject[] tokenIcons;

    // =========================================================
    // CURRENT SPIN STATE
    // =========================================================

    private int hitsThisSpin = 0;
    private bool spinActive = false;


    /*
     * Generic lock for modal/run-setup flows that are neither a spin nor the
     * normal end-of-round Reward Phase.
     *
     * A starting setup flow can use this without RoundManager depending on a
     * concrete manager type.
     */
    private bool externalSpinBlockActive =
        false;


    public bool ExternalSpinBlockActive =>
        externalSpinBlockActive;


    /*
     * If an external modal opens DURING spin resolution (for example from an
     * enemy action on the final token), debt / Reward Phase must wait until
     * that modal has been completed.
     *
     * This is generic flow infrastructure: RoundManager does not know what
     * opened the modal.
     */
    private bool externalFlowDeferredDebtResolution =
        false;


    private bool lastSpinWasValid = false;

    private float spinStartTime = 0f;

    /*
     * Indica que la tirada ya ha sido validada,
     * pero todavía falta que RouletteController termine
     * de resolver stickers + enemigos.
     *
     * La deuda JAMÁS se paga antes de ese momento.
     */
    private bool waitingForSpinResolution = false;


    // =========================================================
    // GAME OVER STATE
    // =========================================================

    /*
     * Run-terminal state.
     *
     * The first accepted reason wins and is never replaced later in the same
     * run. This matters when several consequences happen inside one resolution
     * (for example Blood reaches 0 and another effect also empties money).
     */
    [SerializeField]
    private bool gameOverActive =
        false;


    [SerializeField]
    private GameOverReason currentGameOverReason =
        GameOverReason.BloodDepleted;


    public bool IsGameOver =>
        gameOverActive;


    public GameOverReason CurrentGameOverReason =>
        currentGameOverReason;

    // =========================================================
    // EVENTS
    // =========================================================

    public event Action<int> OnRoundStarted;
    public event Action<int> OnTokensChanged;
    public event Action<int> OnDebtChanged;
    public event Action<int> OnDebtPaid;
    public event Action<int> OnEnemyDebtPenaltyChanged;
    public event Action<bool> OnSpinValidated;

    /*
     * Fired after stickers + enemy actions have completely resolved, but
     * BEFORE end-of-round Debt / Reward flow is allowed to begin.
     *
     * Generic modal systems can use this to collect requests produced during
     * gameplay resolution and open one modal atomically at the correct time.
     */
    public event Action OnGameplaySpinResolutionCompleted;

    public event Action OnGameOver;

    // =========================================================
    // PUBLIC READ-ONLY STATE
    // =========================================================

    public int CurrentRound => currentRound;

    public int TokensRemaining =>
        tokensRemaining;

    public int CurrentDebt =>
        currentDebt;

    public int CurrentBaseDebt =>
        currentBaseDebt;

    public int CurrentEnemyDebtPenaltyAmount =>
        currentEnemyDebtPenaltyAmount;

    public int EnemyDebtPenaltyPercent =>
        enemyDebtPenaltyPercent;

    public int ActiveEnemyCurseDebtPercent =>
        activeEnemyCurseDebtPercent;

    public int CurrentEnemyCurseDebtAmount =>
        currentEnemyCurseDebtAmount;

    public int ActiveStickerInterestPercent =>
        activeStickerInterestPercent;

    public int CurrentStickerInterestAmount =>
        currentStickerInterestAmount;

    public int CurrentActiveInterestAmount =>
        currentActiveInterestAmount;

    /*
     * This is the number shown by the existing Extra Debt TMP.
     *
     * Ascension penalty + active enemy Debt curses + Album sticker interest
     * stack additively into one readable percentage.
     */
    public int CurrentTotalDebtExtraPercent =>
        enemyDebtPenaltyPercent +
        activeEnemyCurseDebtPercent +
        activeStickerInterestPercent;

    public bool DebtPending =>
        debtPending;

    public bool WasLastSpinValid =>
        lastSpinWasValid;

    public bool WaitingForRewardCompletion =>
        waitingForRewardCompletion;

    /*
     * Una nueva tirada solo puede comenzar si:
     *
     * - quedan fichas
     * - no hay deuda pendiente
     * - no estamos esperando terminar Rewards
     * - Reward Screen no está activa
     *
     * La última comprobación también hace que el
     * ContextMenu DEBUG de RewardManager bloquee la ruleta.
     */
    public bool CanStartSpin
    {
        get
        {
            if (gameOverActive)
                return false;

            if (tokensRemaining <= 0)
                return false;

            if (debtPending)
                return false;

            if (waitingForRewardCompletion)
                return false;

            if (externalSpinBlockActive)
                return false;

            if (enemyCorridorController != null &&
                enemyCorridorController.IsAdvancing)
            {
                return false;
            }

            if (RewardManager.Instance != null &&
                RewardManager.Instance.RewardPhaseActive)
            {
                return false;
            }

            return true;
        }
    }

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

        Instance = this;
    }

    private void Start()
    {
        // -----------------------------------------------------
        // REFERENCES
        // -----------------------------------------------------

        if (controller == null)
        {
            controller =
                FindObjectOfType<RouletteController>();
        }

        if (flagPin == null)
        {
            flagPin =
                FindObjectOfType<FlagPin>();
        }

        if (rewardManager == null)
        {
            RewardManager[] managers =
                FindObjectsOfType<RewardManager>(true);

            if (managers.Length > 0)
                rewardManager = managers[0];
        }

        if (enemyCorridorController == null)
        {
            enemyCorridorController =
                FindObjectOfType<EnemyCorridorController>();
        }

        // -----------------------------------------------------
        // REWARD EVENT
        // -----------------------------------------------------

        if (rewardManager != null)
        {
            rewardManager.OnRewardPhaseCompleted +=
                HandleRewardPhaseCompleted;
        }

        // -----------------------------------------------------
        // INITIAL RUN STATE
        // -----------------------------------------------------

        currentRound = 0;

        tokensRemaining =
            tokensPerRound;

        enemyDebtPenaltyPercent =
            0;

        SetDebtForRound(
            currentRound
        );

        debtPending = false;

        waitingForRewardCompletion = false;

        externalSpinBlockActive =
            false;

        externalFlowDeferredDebtResolution =
            false;

        spinActive = false;

        lastSpinWasValid = false;

        waitingForSpinResolution = false;

        gameOverActive =
            false;

        currentGameOverReason =
            GameOverReason.BloodDepleted;

        UpdateAllUI();

        Debug.Log(
            $"[ROUND] Run started. " +
            $"Round = {currentRound}, " +
            $"Tokens = {tokensRemaining}, " +
            $"Debt = ${currentDebt}"
        );
    }

    private void OnDestroy()
    {
        if (rewardManager != null)
        {
            rewardManager.OnRewardPhaseCompleted -=
                HandleRewardPhaseCompleted;
        }

        if (Instance == this)
            Instance = null;
    }

    // =========================================================
    // EXTERNAL FLOW SPIN BLOCK
    // =========================================================

    /// <summary>
    /// Generic gameplay-flow lock used by modal setup screens.
    ///
    /// RoundManager intentionally does not know who owns the modal.
    /// </summary>
    public void SetExternalSpinBlock(
        bool blocked)
    {
        bool wasBlocked =
            externalSpinBlockActive;


        externalSpinBlockActive =
            blocked;


        /*
         * Game Over is terminal. A modal that happens to close afterwards must
         * never restart deferred Debt / Reward flow.
         */
        if (gameOverActive)
        {
            externalFlowDeferredDebtResolution =
                false;

            return;
        }


        /*
         * When a modal that interrupted final-spin resolution closes, resume
         * the exact debt/reward flow that was deferred.
         *
         * Draft-at-run-start also uses this API, but has no pending debt, so
         * this branch naturally does nothing there.
         */
        if (wasBlocked &&
            !blocked &&
            externalFlowDeferredDebtResolution)
        {
            externalFlowDeferredDebtResolution =
                false;


            if (debtPending &&
                !waitingForSpinResolution)
            {
                Debug.Log(
                    "[ROUND] External modal completed. " +
                    "Resuming deferred debt resolution."
                );


                ResolveDebt();
            }
        }
    }


    // =========================================================
    // SPIN HOOKS
    // =========================================================

    public void NotifySpinStart()
    {
        if (!CanStartSpin)
        {
            Debug.LogWarning(
                "[ROUND] Spin rejected: " +
                "round state does not currently allow a spin."
            );

            return;
        }

        StartNewSpin();
    }

    public void NotifySpinEnd()
    {
        if (!spinActive)
            return;

        EndSpin();
    }

    /*
     * RouletteController llama aquí DESPUÉS de:
     *
     * 1. Resolver stickers
     * 2. Resolver enemigos
     *
     * Solo entonces puede entrar la deuda.
     */
    public void NotifySpinResolved()
    {
        if (!waitingForSpinResolution)
            return;

        waitingForSpinResolution =
            false;


        /*
         * A terminal condition may have been reached during sticker/enemy
         * resolution (most commonly Blood <= 0).
         *
         * Do not let Infestation, gameplay free-sticker rewards, Debt or normal
         * Rewards begin after the run has already ended. RouletteController will
         * still finish its own end-of-spin summary and publish the completed log
         * block after this method returns.
         */
        if (gameOverActive)
        {
            debtPending =
                false;

            return;
        }


        /*
         * IMPORTANT ORDER:
         *
         * Enemy actions have all already executed at this point.
         * Give generic post-gameplay modal systems one atomic opportunity to
         * react BEFORE Debt / Clean Row Bonus / Reward Phase can start.
         *
         * For example, several enemies can independently request a modal
         * consequence during OnSpinEnd; the receiving system can aggregate
         * them here and then set ExternalSpinBlock in time to defer Rewards.
         */
        OnGameplaySpinResolutionCompleted?
            .Invoke();


        /*
         * Some terminal conditions are intentionally decided inside the
         * post-gameplay hook itself. Limbo's $0 defeat is one such case because
         * his Divide Money / Collect effects resolve there, after all spin money.
         */
        if (gameOverActive)
        {
            debtPending =
                false;

            return;
        }


        /*
         * Un sticker/enemigo podría haber añadido fichas
         * durante la resolución.
         */
        debtPending =
            tokensRemaining <= 0;

        if (debtPending)
        {
            /*
             * An enemy action may have opened a modal during the enemy
             * resolution pass. Do not let Debt / Reward Phase replace that
             * modal before the player has completed it.
             */
            if (externalSpinBlockActive)
            {
                externalFlowDeferredDebtResolution =
                    true;


                Debug.Log(
                    "[ROUND] Debt resolution deferred until the active " +
                    "external modal is completed."
                );


                return;
            }


            ResolveDebt();
        }
    }

    // =========================================================
    // START SPIN
    // =========================================================

    private void StartNewSpin()
    {
        spinActive = true;

        hitsThisSpin = 0;

        lastSpinWasValid = false;

        spinStartTime =
            Time.time;

        waitingForSpinResolution =
            false;

        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance
                .BeginSpin();
        }

        Debug.Log(
            $"[ROUND] Spin started. " +
            $"Tokens remaining = {tokensRemaining}"
        );
    }

    // =========================================================
    // END SPIN / VALIDATION
    // =========================================================

    private void EndSpin()
    {
        spinActive = false;

        lastSpinWasValid =
            ComputeIsSpinValid();

        OnSpinValidated?
            .Invoke(lastSpinWasValid);

        // -----------------------------------------------------
        // VALID
        // -----------------------------------------------------

        if (lastSpinWasValid)
        {
            if (CurrencyManager.Instance != null)
            {
                int committed =
                    CurrencyManager.Instance
                        .CommitPending();

                Debug.Log(
                    $"[MONEY] Spin committed ${committed}."
                );
            }

            SpendToken();

            waitingForSpinResolution =
                true;
        }

        // -----------------------------------------------------
        // INVALID
        // -----------------------------------------------------

        else
        {
            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance
                    .ClearPending();
            }

            waitingForSpinResolution =
                false;
        }

        float duration =
            Time.time -
            spinStartTime;

        bool flagPlaced =
            flagPin == null ||
            flagPin.isPlaced;

        Debug.Log(
            $"[ROUND] Spin ended. " +
            $"Valid = {lastSpinWasValid}, " +
            $"hits = {hitsThisSpin}, " +
            $"flagPlaced = {flagPlaced}, " +
            $"duration = {duration:F2}s, " +
            $"tokens = {tokensRemaining}"
        );
    }

    // =========================================================
    // VALIDATION
    // =========================================================

    private bool ComputeIsSpinValid()
    {
        bool pinPlaced =
            flagPin == null ||
            flagPin.isPlaced;

        bool enoughHits =
            hitsThisSpin >=
            minHitsRequired;

        float duration =
            Time.time -
            spinStartTime;

        bool enoughDuration =
            duration >=
            minSpinDuration;

        return
            pinPlaced &&
            enoughHits &&
            enoughDuration;
    }

    // =========================================================
    // PIN HITS
    // =========================================================

    public void RegisterPinHit(
        FlagPin p)
    {
        if (!spinActive)
            return;

        hitsThisSpin++;
    }

    // =========================================================
    // TOKENS
    // =========================================================

    private void SpendToken()
    {
        if (tokensRemaining <= 0)
            return;

        tokensRemaining--;

        debtPending =
            tokensRemaining <= 0;

        UpdateTokensUI();

        OnTokensChanged?
            .Invoke(tokensRemaining);

        Debug.Log(
            $"[TOKENS] Token spent. " +
            $"{tokensRemaining} remaining."
        );

        if (debtPending)
        {
            Debug.Log(
                "[TOKENS] Last token spent. " +
                "Debt will be checked AFTER spin resolution."
            );
        }
    }

    public void ModifyTokens(
        int amount)
    {
        tokensRemaining =
            Mathf.Max(
                0,
                tokensRemaining + amount
            );

        debtPending =
            tokensRemaining <= 0;

        UpdateTokensUI();

        OnTokensChanged?
            .Invoke(tokensRemaining);

        Debug.Log(
            $"[TOKENS] Modified by {amount}. " +
            $"Current = {tokensRemaining}"
        );
    }

    public void AddTokens(
        int amount)
    {
        if (amount <= 0)
            return;

        ModifyTokens(amount);
    }

    // =========================================================
    // DEBT
    // =========================================================

    private void ResolveDebt()
    {
        if (!debtPending)
            return;

        Debug.Log(
            $"[DEBT] Debt due: ${currentDebt}"
        );

        if (CurrencyManager.Instance == null)
        {
            Debug.LogError(
                "[DEBT] CurrencyManager missing."
            );

            return;
        }

        int playerMoney =
            CurrencyManager.Instance.dollars;

        Debug.Log(
            $"[DEBT] Player money: ${playerMoney}"
        );

        // -----------------------------------------------------
        // CAN PAY
        // -----------------------------------------------------

        if (CurrencyManager.Instance
            .Spend(currentDebt))
        {
            int paidAmount =
                currentDebt;

            Debug.Log(
                $"[DEBT] Paid ${paidAmount} successfully."
            );

            debtSettlementInProgress =
                true;

            try
            {
                OnDebtPaid?
                    .Invoke(paidAmount);
            }
            finally
            {
                debtSettlementInProgress =
                    false;
            }


            /*
             * La ronda ya está cerrada económicamente.
             *
             * Antes de Rewards registramos si quedaron enemigos vivos.
             * Ese resultado NO cambia la deuda que acabamos de pagar:
             * modifica únicamente la deuda BASE de la SIGUIENTE ronda.
             */
            bool cleanRowCleared =
                ResolveEnemyEncounterOutcome();


            /*
             * La deuda ya está PAGADA.
             *
             * Ahora entramos en Reward Phase antes
             * de avanzar la ronda.
             */
            debtPending = false;

            BeginRewardPhaseOrNextRound(
                cleanRowCleared
            );

            return;
        }

        // -----------------------------------------------------
        // CANNOT PAY
        // -----------------------------------------------------

        Debug.Log(
            $"[GAME OVER] Could not pay Roulette Tax. " +
            $"Needed ${currentDebt}, had ${playerMoney}."
        );

        RequestGameOver(
            GameOverReason.DebtUnpaid
        );
    }

    // =========================================================
    // REWARD PHASE
    // =========================================================

    private void BeginRewardPhaseOrNextRound(
        bool cleanRowCleared)
    {
        /*
         * Si esta escena NO utiliza RewardManager
         * (por ejemplo una escena mobile antigua),
         * conservamos el comportamiento anterior.
         */
        if (rewardManager == null)
        {
            Debug.Log(
                "[REWARD] No RewardManager in scene. " +
                "Starting next round directly."
            );

            StartNextRound();
            return;
        }

        /*
         * Bloqueamos explícitamente el cambio de ronda
         * hasta que RewardManager nos avise de que
         * el jugador ha hecho Skip.
         */
        waitingForRewardCompletion =
            true;

        rewardManager.BeginRewardSequence(
            cleanRowCleared
        );

        /*
         * Protección por si RewardManager no ha podido
         * abrirse (por ejemplo RewardPanel sin asignar).
         *
         * No queremos dejar la run bloqueada para siempre.
         */
        if (!rewardManager.RewardPhaseActive)
        {
            Debug.LogWarning(
                "[REWARD] Reward phase could not start. " +
                "Starting next round as fallback."
            );

            waitingForRewardCompletion =
                false;

            StartNextRound();
            return;
        }

        Debug.Log(
            "[ROUND] Waiting for Reward Phase completion."
        );
    }

    private void HandleRewardPhaseCompleted()
    {
        if (!waitingForRewardCompletion)
            return;

        waitingForRewardCompletion =
            false;

        Debug.Log(
            "[ROUND] Reward Phase completed. " +
            "Starting next round."
        );

        StartNextRound();
    }

    // =========================================================
    // ENEMY DEBT CURSES
    // =========================================================

    public void RegisterEnemyDebtCurse(
        BaseEnemy source,
        int percent)
    {
        if (source == null ||
            percent <= 0)
        {
            return;
        }


        activeEnemyDebtCurses[source] =
            percent;


        RefreshEnemyCurseDebtContribution();


        Debug.Log(
            $"[DEBT CURSE] {source.EnemyName} adds " +
            $"+{percent}% Debt. " +
            $"Active curse extra = " +
            $"{activeEnemyCurseDebtPercent}%."
        );
    }


    public void UnregisterEnemyDebtCurse(
        BaseEnemy source)
    {
        if (source == null)
            return;


        if (!activeEnemyDebtCurses.Remove(
            source))
        {
            return;
        }


        RefreshEnemyCurseDebtContribution();


        Debug.Log(
            $"[DEBT CURSE] {source.EnemyName}'s Debt curse removed. " +
            $"Active curse extra = " +
            $"{activeEnemyCurseDebtPercent}%."
        );
    }


    private void RefreshEnemyCurseDebtContribution()
    {
        int previousActiveInterestAmount =
            currentActiveInterestAmount;


        activeEnemyCurseDebtPercent =
            CalculateActiveEnemyCurseDebtPercent();


        currentEnemyCurseDebtAmount =
            CalculatePercentAmount(
                currentBaseDebt,
                activeEnemyCurseDebtPercent
            );


        /*
         * Enemy Debt curses and Album sticker Interest are the SAME Interest
         * pool for debt calculation. Sum percentages FIRST, then round ONCE.
         *
         * Example: Tax Collector +5% + Ladybug +5% = 10% Interest, not two
         * separately-rounded 5% amounts.
         */
        currentActiveInterestAmount =
            CalculatePercentAmount(
                currentBaseDebt,
                activeEnemyCurseDebtPercent +
                activeStickerInterestPercent
            );


        /*
         * Apply only the DELTA.
         *
         * This preserves any other direct debt modifications that may have
         * happened during the round instead of rebuilding currentDebt from
         * scratch.
         */
        int amountDelta =
            currentActiveInterestAmount -
            previousActiveInterestAmount;


        int grossDebt =
            Mathf.Max(
                0,
                currentDebt +
                currentStickerDebtDiscountAmount +
                amountDelta
            );


        ApplyStickerDebtDiscountToGrossDebt(
            grossDebt
        );


        UpdateDebtUI();

        UpdateEnemyDebtPenaltyUI();


        OnDebtChanged?
            .Invoke(
                currentDebt
            );
    }


    private int CalculateActiveEnemyCurseDebtPercent()
    {
        int total =
            0;


        foreach (
            KeyValuePair<BaseEnemy, int> entry
            in activeEnemyDebtCurses)
        {
            if (entry.Key == null ||
                entry.Value <= 0)
            {
                continue;
            }


            total +=
                entry.Value;
        }


        return
            Mathf.Max(
                0,
                total
            );
    }


    private void ClearEnemyDebtCursesForRoundTransition()
    {
        activeEnemyDebtCurses.Clear();

        activeEnemyCurseDebtPercent =
            0;

        currentEnemyCurseDebtAmount =
            0;

        currentActiveInterestAmount =
            CalculatePercentAmount(
                currentBaseDebt,
                activeStickerInterestPercent
            );
    }


    // =========================================================
    // STICKER INTEREST
    // =========================================================

    /// <summary>
    /// Registers one physical Album sticker as an Interest source for the
    /// current / upcoming round. Re-registering the same sticker updates its
    /// percentage instead of stacking it twice.
    /// </summary>
    public void RegisterStickerInterest(
        BaseSticker source,
        int percent)
    {
        if (source == null ||
            percent <= 0)
        {
            return;
        }


        if (activeStickerInterestSources.TryGetValue(
                source,
                out int existingPercent) &&
            existingPercent == percent)
        {
            return;
        }


        activeStickerInterestSources[source] =
            percent;


        RefreshStickerInterestContribution(
            ShouldApplyStickerInterestToCurrentDebt()
        );


        Debug.Log(
            $"[STICKER INTEREST] {GetStickerDisplayName(source)} adds " +
            $"+{percent}% Interest. " +
            $"Active sticker Interest = {activeStickerInterestPercent}%."
        );
    }


    /// <summary>
    /// Removes one physical sticker's Interest contribution.
    /// </summary>
    public void UnregisterStickerInterest(
        BaseSticker source)
    {
        if (source == null)
            return;


        if (!activeStickerInterestSources.Remove(
                source))
        {
            return;
        }


        RefreshStickerInterestContribution(
            ShouldApplyStickerInterestToCurrentDebt()
        );


        Debug.Log(
            $"[STICKER INTEREST] {GetStickerDisplayName(source)} removed. " +
            $"Active sticker Interest = {activeStickerInterestPercent}%."
        );
    }


    private void RefreshStickerInterestContribution(
        bool applyDeltaToCurrentDebt)
    {
        int previousActiveInterestAmount =
            currentActiveInterestAmount;


        activeStickerInterestPercent =
            CalculateActiveStickerInterestPercent();


        currentStickerInterestAmount =
            CalculatePercentAmount(
                currentBaseDebt,
                activeStickerInterestPercent
            );


        /*
         * Ladybug and Tax Collector feed the SAME Interest pool.
         * Percentages are combined before converting them to dollars so the
         * final result follows one rounding pass.
         */
        currentActiveInterestAmount =
            CalculatePercentAmount(
                currentBaseDebt,
                activeEnemyCurseDebtPercent +
                activeStickerInterestPercent
            );


        /*
         * During normal gameplay / starting Draft, changing Album composition
         * changes this round's debt immediately.
         *
         * During end-of-round Rewards the previous debt has already been paid.
         * We still update the registrations so SetDebtForRound() can apply the
         * correct Interest to the NEXT round, but we deliberately do not mutate
         * the already-settled debt total.
         */
        if (applyDeltaToCurrentDebt)
        {
            int amountDelta =
                currentActiveInterestAmount -
                previousActiveInterestAmount;


            int grossDebt =
                Mathf.Max(
                    0,
                    currentDebt +
                    currentStickerDebtDiscountAmount +
                    amountDelta
                );


            ApplyStickerDebtDiscountToGrossDebt(
                grossDebt
            );


            UpdateDebtUI();

            UpdateEnemyDebtPenaltyUI();


            OnDebtChanged?
                .Invoke(
                    currentDebt
                );
        }
    }


    private int CalculateActiveStickerInterestPercent()
    {
        int total =
            0;


        foreach (
            KeyValuePair<BaseSticker, int> entry
            in activeStickerInterestSources)
        {
            if (entry.Key == null ||
                entry.Value <= 0)
            {
                continue;
            }


            total +=
                entry.Value;
        }


        return
            Mathf.Max(
                0,
                total
            );
    }


    private bool ShouldApplyStickerInterestToCurrentDebt()
    {
        /*
         * Once Rewards are active, this round's Debt has already been paid.
         * Album edits there configure the next round instead.
         */
        if (waitingForRewardCompletion)
            return false;


        if (RewardManager.Instance != null &&
            RewardManager.Instance.RewardPhaseActive)
        {
            return false;
        }


        return true;
    }


    // =========================================================
    // STICKER DEBT DISCOUNT
    // =========================================================

    /// <summary>
    /// Registers one physical Album sticker as a percentage discount on Debt.
    /// Multiple physical stickers stack additively up to 100%.
    /// </summary>
    public void RegisterStickerDebtDiscount(
        BaseSticker source,
        int percent)
    {
        if (source == null ||
            percent <= 0)
        {
            return;
        }


        int clampedPercent =
            Mathf.Clamp(
                percent,
                0,
                100
            );


        if (activeStickerDebtDiscountSources.TryGetValue(
                source,
                out int existingPercent) &&
            existingPercent == clampedPercent)
        {
            return;
        }


        activeStickerDebtDiscountSources[source] =
            clampedPercent;


        RefreshStickerDebtDiscountContribution(
            ShouldApplyStickerDebtDiscountToCurrentDebt()
        );


        Debug.Log(
            $"[STICKER DEBT DISCOUNT] {GetStickerDisplayName(source)} adds " +
            $"-{clampedPercent}% Debt. Active discount = " +
            $"{activeStickerDebtDiscountPercent}%."
        );
    }


    /// <summary>
    /// Removes one physical sticker's Debt discount contribution.
    /// </summary>
    public void UnregisterStickerDebtDiscount(
        BaseSticker source)
    {
        if (source == null)
            return;


        if (!activeStickerDebtDiscountSources.Remove(
                source))
        {
            return;
        }


        RefreshStickerDebtDiscountContribution(
            ShouldApplyStickerDebtDiscountToCurrentDebt()
        );


        Debug.Log(
            $"[STICKER DEBT DISCOUNT] {GetStickerDisplayName(source)} removed. " +
            $"Active discount = {activeStickerDebtDiscountPercent}%."
        );
    }


    private void RefreshStickerDebtDiscountContribution(
        bool applyToCurrentDebt)
    {
        activeStickerDebtDiscountPercent =
            CalculateActiveStickerDebtDiscountPercent();


        if (!applyToCurrentDebt)
        {
            currentStickerDebtDiscountAmount =
                0;

            return;
        }


        int grossDebt =
            Mathf.Max(
                0,
                currentDebt +
                currentStickerDebtDiscountAmount
            );


        ApplyStickerDebtDiscountToGrossDebt(
            grossDebt
        );


        UpdateDebtUI();
        UpdateEnemyDebtPenaltyUI();


        OnDebtChanged?
            .Invoke(
                currentDebt
            );
    }


    private int CalculateActiveStickerDebtDiscountPercent()
    {
        int total =
            0;


        foreach (
            KeyValuePair<BaseSticker, int> entry
            in activeStickerDebtDiscountSources)
        {
            if (entry.Key == null ||
                entry.Value <= 0)
            {
                continue;
            }


            total +=
                entry.Value;
        }


        return
            Mathf.Clamp(
                total,
                0,
                100
            );
    }


    private void ApplyStickerDebtDiscountToGrossDebt(
        int grossDebt)
    {
        grossDebt =
            Mathf.Max(
                0,
                grossDebt
            );


        activeStickerDebtDiscountPercent =
            CalculateActiveStickerDebtDiscountPercent();


        currentStickerDebtDiscountAmount =
            Mathf.Min(
                grossDebt,
                CalculatePercentAmount(
                    grossDebt,
                    activeStickerDebtDiscountPercent
                )
            );


        currentDebt =
            Mathf.Max(
                0,
                grossDebt -
                currentStickerDebtDiscountAmount
            );
    }


    private bool ShouldApplyStickerDebtDiscountToCurrentDebt()
    {
        if (debtSettlementInProgress)
            return false;


        if (waitingForRewardCompletion)
            return false;


        if (RewardManager.Instance != null &&
            RewardManager.Instance.RewardPhaseActive)
        {
            return false;
        }


        return true;
    }


    private string GetStickerDisplayName(
        BaseSticker sticker)
    {
        if (sticker != null &&
            sticker.effect != null &&
            !string.IsNullOrWhiteSpace(
                sticker.effect.stickerName
            ))
        {
            return
                sticker.effect.stickerName;
        }


        return
            sticker != null
                ? sticker.name
                : "Sticker";
    }


    // =========================================================
    // MODIFY DEBT
    // =========================================================

    public void ModifyDebt(
        int amount)
    {
        int grossDebt =
            Mathf.Max(
                0,
                currentDebt +
                currentStickerDebtDiscountAmount +
                amount
            );


        ApplyStickerDebtDiscountToGrossDebt(
            grossDebt
        );

        UpdateDebtUI();

        OnDebtChanged?
            .Invoke(currentDebt);

        Debug.Log(
            $"[DEBT] Modified by {amount}. " +
            $"Current debt = ${currentDebt}"
        );
    }

    // =========================================================
    // NEXT ROUND
    // =========================================================

    private void StartNextRound()
    {
        /*
         * SegmentBlock is now duration-based, not round-based.
         *
         * Blocks therefore survive a round transition if they still have
         * remaining valid spins. Their countdown advances only when a future
         * valid spin is actually resolved.
         */


        /*
         * The previous CurrentRow no longer belongs to the new round.
         *
         * Clear its curse registrations immediately. The enemies that move
         * into CurrentRow will register their own curses when the corridor
         * finishes advancing and SetCombatActive(true) is called.
         */
        ClearEnemyDebtCursesForRoundTransition();


        /*
         * Enemy progression is tied to ROUND progression, not kills.
         *
         * Even if CurrentRow still contains living enemies, it is removed
         * now. NextRow physically advances and becomes the new combat row.
         */
        if (enemyCorridorController != null)
        {
            enemyCorridorController
                .AdvanceEncounterForRoundTransition();
        }


        currentRound++;

        tokensRemaining =
            tokensPerRound;

        SetDebtForRound(
            currentRound
        );

        debtPending =
            false;

        waitingForRewardCompletion =
            false;

        waitingForSpinResolution =
            false;

        externalFlowDeferredDebtResolution =
            false;

        lastSpinWasValid =
            false;

        UpdateAllUI();

        OnRoundStarted?
            .Invoke(currentRound);

        OnTokensChanged?
            .Invoke(tokensRemaining);

        OnDebtChanged?
            .Invoke(currentDebt);

        Debug.Log(
            $"[ROUND] Starting R-{currentRound}. " +
            $"Tokens = {tokensRemaining}, " +
            $"Base debt = ${currentBaseDebt}, " +
            $"Ascension extra = {enemyDebtPenaltyPercent}% " +
            $"(+${currentEnemyDebtPenaltyAmount}), " +
            $"Enemy curse Interest = {activeEnemyCurseDebtPercent}%, " +
            $"Sticker Interest = {activeStickerInterestPercent}%, " +
            $"Combined active Interest = " +
            $"{activeEnemyCurseDebtPercent + activeStickerInterestPercent}% " +
            $"(+${currentActiveInterestAmount}), " +
            $"Total debt = ${currentDebt}"
        );
    }


    private int CalculateBaseDebtForRound(
        int round)
    {
        return
            startingDebt +
            (round *
             debtIncreasePerRound);
    }


    private int CalculateEnemyDebtPenaltyAmount(
        int baseDebt)
    {
        return
            CalculatePercentAmount(
                baseDebt,
                enemyDebtPenaltyPercent
            );
    }


    private int CalculatePercentAmount(
        int baseDebt,
        int percent)
    {
        if (baseDebt <= 0 ||
            percent <= 0)
        {
            return 0;
        }


        /*
         * All extra-debt percentage sources use the same rounding rule.
         */
        return
            Mathf.RoundToInt(
                baseDebt *
                (
                    percent /
                    100f
                )
            );
    }


    private void SetDebtForRound(
        int round)
    {
        currentBaseDebt =
            CalculateBaseDebtForRound(
                round
            );


        currentEnemyDebtPenaltyAmount =
            CalculateEnemyDebtPenaltyAmount(
                currentBaseDebt
            );


        /*
         * Usually zero at this exact moment because the corridor is still
         * advancing. If a curse is already registered (e.g. initial round),
         * it is included here too.
         */
        activeEnemyCurseDebtPercent =
            CalculateActiveEnemyCurseDebtPercent();


        currentEnemyCurseDebtAmount =
            CalculatePercentAmount(
                currentBaseDebt,
                activeEnemyCurseDebtPercent
            );


        /*
         * Album-based sticker Interest survives round transitions while the
         * physical sticker remains registered in the Album. The percentage is
         * NOT cumulative across rounds: every round recalculates the amount
         * from that round's fresh base Debt.
         */
        activeStickerInterestPercent =
            CalculateActiveStickerInterestPercent();


        currentStickerInterestAmount =
            CalculatePercentAmount(
                currentBaseDebt,
                activeStickerInterestPercent
            );


        /*
         * Tax Collector + Ladybug are one Interest pool.
         * Add percentages first, then round once.
         */
        currentActiveInterestAmount =
            CalculatePercentAmount(
                currentBaseDebt,
                activeEnemyCurseDebtPercent +
                activeStickerInterestPercent
            );


        int grossDebt =
            currentBaseDebt +
            currentEnemyDebtPenaltyAmount +
            currentActiveInterestAmount;


        ApplyStickerDebtDiscountToGrossDebt(
            grossDebt
        );
    }


    // =========================================================
    // GAME OVER
    // =========================================================

    /// <summary>
    /// Ends the current run with one explicit reason.
    ///
    /// The first accepted reason wins. Requesting Game Over again later in the
    /// same resolution cannot overwrite the reason already shown to the player.
    ///
    /// This method deliberately does NOT reload the scene. GameOverManager owns
    /// presentation and Retry / Exit; RoundManager only owns terminal run state.
    /// </summary>
    public bool RequestGameOver(
        GameOverReason reason)
    {
        if (gameOverActive)
        {
            return false;
        }


        gameOverActive =
            true;

        currentGameOverReason =
            reason;


        /*
         * No unresolved economy should survive a terminal run.
         *
         * We do NOT interrupt RouletteController's current resolution here:
         * it is still allowed to finish Blood/money totals and commit the final
         * Game Log block before GameOverManager displays the panel.
         */
        debtPending =
            false;

        externalFlowDeferredDebtResolution =
            false;


        /*
         * Do NOT clear CurrencyManager here. A Blood death can be requested
         * before RouletteController writes the final spin totals; clearing the
         * manager would erase that spin's earnings/loss tracking and corrupt the
         * death log. Retry reloads the whole scene anyway.
         */


        Debug.Log(
            $"[GAME OVER] Run ended. Reason = {currentGameOverReason}."
        );


        OnGameOver?
            .Invoke();


        return true;
    }

    // =========================================================
    // UI
    // =========================================================

    private void UpdateAllUI()
    {
        UpdateRoundUI();
        UpdateDebtUI();
        UpdateEnemyDebtPenaltyUI();
        UpdateTokensUI();
    }

    private void UpdateRoundUI()
    {
        if (roundText != null)
        {
            roundText.text =
                $"R-{currentRound}";
        }
    }

    private void UpdateDebtUI()
    {
        if (debtText != null)
        {
            debtText.text =
                $"${currentDebt}";
        }
    }


    private void UpdateEnemyDebtPenaltyUI()
    {
        if (enemyDebtPenaltyText != null)
        {
            enemyDebtPenaltyText.text =
                $"{CurrentTotalDebtExtraPercent}%";
        }
    }


    private void UpdateTokensUI()
    {
        if (tokensText != null)
        {
            tokensText.text =
                $"{tokensRemaining} / " +
                $"{tokensPerRound}";
        }

        if (tokenIcons == null)
            return;

        for (int i = 0;
             i < tokenIcons.Length;
             i++)
        {
            if (tokenIcons[i] == null)
                continue;

            bool shouldBeVisible =
                i < tokensRemaining;

            tokenIcons[i]
                .SetActive(
                    shouldBeVisible
                );
        }
    }
}