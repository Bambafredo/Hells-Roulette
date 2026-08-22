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

    // =========================================================
    // VALID SPIN CONDITIONS
    // =========================================================

    [Header("Condiciones de tirada válida")]
    public int minHitsRequired = 2;

    [Tooltip("Duración mínima (en segundos) que debe durar el spin para considerarse válido.")]
    public float minSpinDuration = 0.25f;

    // =========================================================
    // ROUND / TOKENS
    // =========================================================

    [Header("Round / Tokens")]

    [Tooltip("Número base de tiradas válidas disponibles por ronda.")]
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

    [Tooltip("Cantidad que aumenta la deuda después de cada ronda superada.")]
    [Min(0)]
    public int debtIncreasePerRound = 5;

    [SerializeField]
    private int currentDebt = 0;

    [SerializeField]
    private bool debtPending = false;

    // =========================================================
    // UI
    // =========================================================

    [Header("UI")]

    [Tooltip("Texto de ronda. Ejemplo: R-0")]
    public TMP_Text roundText;

    [Tooltip("Texto de deuda. Ejemplo: DEBT: $10")]
    public TMP_Text debtText;

    [Tooltip("Opcional. Texto numérico de fichas, por ejemplo 2 / 3.")]
    public TMP_Text tokensText;

    [Tooltip("GameObjects de las fichas, en orden. Pueden ser UI Images con el sprite que quieras.")]
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
    //
    // De momento nadie necesita escucharlos.
    // Los dejamos preparados porque serán muy útiles
    // para stickers condicionales.
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
    public int TokensRemaining => tokensRemaining;
    public int CurrentDebt => currentDebt;

    public bool DebtPending => debtPending;

    public bool WasLastSpinValid => lastSpinWasValid;

    /*
     * RouletteController utilizará esto en el siguiente paso
     * para impedir físicamente nuevas tiradas cuando no queden fichas.
     */
    public bool CanStartSpin =>
        tokensRemaining > 0 &&
        !debtPending;

    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        // -----------------------------------------------------
        // Resolver referencias si no están asignadas
        // -----------------------------------------------------

        if (controller == null)
            controller = FindObjectOfType<RouletteController>();

        if (flagPin == null)
            flagPin = FindObjectOfType<FlagPin>();

        // -----------------------------------------------------
        // ESTADO INICIAL DE LA RUN
        // -----------------------------------------------------

        currentRound = 0;

        tokensRemaining =
            tokensPerRound;

        currentDebt =
            CalculateDebtForRound(currentRound);

        debtPending = false;

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

    // =========================================================
    // SPIN HOOKS
    // =========================================================

    /// <summary>
    /// Llamado por RouletteController cuando comienza
    /// una tirada real.
    /// </summary>
    public void NotifySpinStart()
    {
        /*
         * Esto será además bloqueado ANTES de lanzar
         * desde RouletteController en el siguiente paso.
         *
         * Dejamos esta protección aquí igualmente.
         */
        if (!CanStartSpin)
        {
            Debug.LogWarning(
                "[ROUND] Spin rejected: no tokens available " +
                "or debt resolution is pending."
            );

            return;
        }

        StartNewSpin();
    }

    /// <summary>
    /// Llamado cuando la ruleta se ha detenido.
    ///
    /// AQUÍ:
    /// - validamos la tirada
    /// - confirmamos o descartamos dinero pendiente
    /// - gastamos ficha
    ///
    /// NO pagamos todavía la deuda.
    /// </summary>
    public void NotifySpinEnd()
    {
        if (!spinActive)
            return;

        EndSpin();
    }

    /// <summary>
    /// NUEVO.
    ///
    /// RouletteController llamará a esto DESPUÉS de:
    ///
    /// 1. Resolver stickers
    /// 2. Resolver enemigos
    ///
    /// Solo entonces puede entrar la deuda.
    /// </summary>
    public void NotifySpinResolved()
    {
        if (!waitingForSpinResolution)
            return;

        waitingForSpinResolution = false;

        /*
         * Es posible que algún sticker de la tirada
         * haya añadido fichas.
         *
         * Por eso volvemos a mirar tokensRemaining
         * en lugar de asumir que seguimos a cero.
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

        /*
         * Si todavía quedase este flag de una tirada anterior
         * durante desarrollo, la nueva tirada pasa a ser
         * la resolución relevante.
         *
         * En el flujo final RouletteController llamará a
         * NotifySpinResolved() en el mismo frame de resolución.
         */
        waitingForSpinResolution = false;

        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.BeginSpin();
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
        // VALID SPIN
        // -----------------------------------------------------

        if (lastSpinWasValid)
        {
            /*
             * Primero confirmamos el dinero generado
             * durante el giro físico de la ruleta.
             */
            if (CurrencyManager.Instance != null)
            {
                int committed =
                    CurrencyManager.Instance
                        .CommitPending();

                Debug.Log(
                    $"[MONEY] Spin committed ${committed}."
                );
            }

            /*
             * SOLO una tirada válida consume ficha.
             */
            SpendToken();

            /*
             * Todavía NO resolvemos deuda.
             *
             * Faltan:
             *
             * - efectos del segmento ganador
             * - enemigos
             */
            waitingForSpinResolution = true;
        }

        // -----------------------------------------------------
        // INVALID SPIN
        // -----------------------------------------------------

        else
        {
            /*
             * Una tirada inválida:
             *
             * - NO consume ficha
             * - NO conserva pending money
             */
            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance
                    .ClearPending();
            }

            waitingForSpinResolution = false;
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

    public void RegisterPinHit(FlagPin p)
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

        /*
         * Si llegamos a cero marcamos que existe
         * una potencial deuda pendiente.
         *
         * Pero NO se cobra hasta NotifySpinResolved().
         */
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

    /// <summary>
    /// API preparada para stickers/enemigos.
    ///
    /// Ej:
    /// ModifyTokens(+1) -> gana una tirada
    /// ModifyTokens(-1) -> pierde una tirada
    /// </summary>
    public void ModifyTokens(int amount)
    {
        tokensRemaining =
            Mathf.Max(
                0,
                tokensRemaining + amount
            );

        /*
         * Muy importante:
         *
         * Si durante la ÚLTIMA tirada un sticker
         * concede +1 token, la deuda deja de estar
         * pendiente y la ronda puede continuar.
         */
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

    public void AddTokens(int amount)
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
        /*
         * Este método SOLO debe alcanzarse después
         * de NotifySpinResolved().
         */

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
             * De momento todavía no existe Reward Screen.
             *
             * Por eso pasamos directamente a la siguiente ronda.
             *
             * MÁS ADELANTE:
             *
             * Debt paid
             *      ↓
             * Reward Screen
             *      ↓
             * StartNextRound()
             */
            StartNextRound();

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

    /// <summary>
    /// Permite a stickers/enemigos modificar
    /// la deuda actual.
    ///
    /// Ej:
    /// ModifyDebt(-5)
    /// ModifyDebt(+10)
    /// </summary>
    public void ModifyDebt(int amount)
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

        debtPending = false;

        waitingForSpinResolution = false;

        lastSpinWasValid = false;

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
            (round * debtIncreasePerRound);
    }

    // =========================================================
    // GAME OVER
    // =========================================================

    private void GameOver()
    {
        OnGameOver?.Invoke();

        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance
                .ClearPending();
        }

        Debug.Log(
            "[GAME OVER] Resetting run..."
        );

        /*
         * Recargar la escena es muchísimo más seguro
         * durante esta fase que intentar resetear manualmente:
         *
         * - dinero
         * - sangre
         * - enemigos
         * - stickers
         * - rueda
         * - bolsa
         * - ronda
         * - fichas
         *
         * Todo vuelve exactamente al estado del Editor.
         */
        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex
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
        // -----------------------------------------------------
        // OPTIONAL TEXT
        // -----------------------------------------------------

        if (tokensText != null)
        {
            tokensText.text =
                $"{tokensRemaining} / {tokensPerRound}";
        }

        // -----------------------------------------------------
        // TOKEN ICONS
        // -----------------------------------------------------

        if (tokenIcons == null)
            return;

        for (int i = 0;
             i < tokenIcons.Length;
             i++)
        {
            if (tokenIcons[i] == null)
                continue;

            /*
             * Ejemplo con 3 tokens:
             *
             * 3 restantes: ● ● ●
             * 2 restantes: ● ●
             * 1 restante : ●
             * 0 restantes:
             */
            bool shouldBeVisible =
                i < tokensRemaining;

            tokenIcons[i]
                .SetActive(
                    shouldBeVisible
                );
        }
    }
}