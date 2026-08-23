using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using System;

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

    [SerializeField]
    private int currentDebt = 0;

    [SerializeField]
    private bool debtPending = false;

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
    // EVENTS
    // =========================================================

    public event Action<int> OnRoundStarted;
    public event Action<int> OnTokensChanged;
    public event Action<int> OnDebtChanged;
    public event Action<int> OnDebtPaid;
    public event Action<bool> OnSpinValidated;
    public event Action OnGameOver;

    // =========================================================
    // PUBLIC READ-ONLY STATE
    // =========================================================

    public int CurrentRound => currentRound;

    public int TokensRemaining =>
        tokensRemaining;

    public int CurrentDebt =>
        currentDebt;

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
            if (tokensRemaining <= 0)
                return false;

            if (debtPending)
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

        currentDebt =
            CalculateDebtForRound(
                currentRound
            );

        debtPending = false;

        waitingForRewardCompletion = false;

        spinActive = false;

        lastSpinWasValid = false;

        waitingForSpinResolution = false;

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
         * Un sticker/enemigo podría haber añadido fichas
         * durante la resolución.
         */
        debtPending =
            tokensRemaining <= 0;

        if (debtPending)
        {
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

            OnDebtPaid?
                .Invoke(paidAmount);

            /*
             * La deuda ya está PAGADA.
             *
             * Ahora entramos en Reward Phase antes
             * de avanzar la ronda.
             */
            debtPending = false;

            BeginRewardPhaseOrNextRound();

            return;
        }

        // -----------------------------------------------------
        // CANNOT PAY
        // -----------------------------------------------------

        Debug.Log(
            $"[GAME OVER] Could not pay Roulette Tax. " +
            $"Needed ${currentDebt}, had ${playerMoney}."
        );

        GameOver();
    }

    // =========================================================
    // REWARD PHASE
    // =========================================================

    private void BeginRewardPhaseOrNextRound()
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

        rewardManager.BeginRewardPhase();

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
    // MODIFY DEBT
    // =========================================================

    public void ModifyDebt(
        int amount)
    {
        currentDebt =
            Mathf.Max(
                0,
                currentDebt + amount
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
        currentRound++;

        tokensRemaining =
            tokensPerRound;

        currentDebt =
            CalculateDebtForRound(
                currentRound
            );

        debtPending =
            false;

        waitingForRewardCompletion =
            false;

        waitingForSpinResolution =
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
            $"Debt = ${currentDebt}"
        );
    }

    private int CalculateDebtForRound(
        int round)
    {
        return
            startingDebt +
            (round *
             debtIncreasePerRound);
    }

    // =========================================================
    // GAME OVER
    // =========================================================

    private void GameOver()
    {
        OnGameOver?
            .Invoke();

        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance
                .ClearPending();
        }

        Debug.Log(
            "[GAME OVER] Resetting run..."
        );

        SceneManager.LoadScene(
            SceneManager
                .GetActiveScene()
                .buildIndex
        );
    }

    // =========================================================
    // UI
    // =========================================================

    private void UpdateAllUI()
    {
        UpdateRoundUI();
        UpdateDebtUI();
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