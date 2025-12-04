using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class RoundManager : MonoBehaviour
{
    public static RoundManager Instance;

    [Header("Refs")]
    public RouletteController controller;
    public FlagPin flagPin;

    [Header("Condiciones de ronda válida")]
    public int minHitsRequired = 2;

    [Tooltip("Duración mínima (en segundos) que debe durar el spin para considerarse válido.")]
    public float minSpinDuration = 0.25f;

    [Header("UI")]
    public TMP_Text roundText;

    private int hitsThisSpin = 0;
    private bool spinActive = false;

    private int currentRound = 0;
    private bool lastSpinWasValid = false;

    private float spinStartTime = 0f;   // ⬅️ nuevo: para medir duración de la tirada

    // ---------------------------------------------------------
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        UpdateRoundUI();
    }

    // Recibidos desde RouletteController
    public void NotifySpinStart()
    {
        StartNewSpin();
    }

    public void NotifySpinEnd()
    {
        EndSpin();
    }

    // ---------------------------------------------------------
    private void StartNewSpin()
    {
        spinActive = true;
        hitsThisSpin = 0;
        lastSpinWasValid = false;
        spinStartTime = Time.time;   // ⬅️ empezamos a contar
    }

    private void EndSpin()
    {
        spinActive = false;

        lastSpinWasValid = ComputeIsSpinValid();

        // 🟢 Tirada válida
        if (lastSpinWasValid)
        {
            currentRound++;
            UpdateRoundUI();

            // Aplicar dinero pendiente
            if (CurrencyManager.Instance != null)
                CurrencyManager.Instance.AddDollar(CurrencyManager.Instance.pendingDollars);
        }

        // Resetear siempre el dinero pendiente
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.pendingDollars = 0;

        Debug.Log($"[ROUND] Spin ended. Valid = {lastSpinWasValid}, hits = {hitsThisSpin}, " +
                  $"isPlaced = {flagPin.isPlaced}, duration = {Time.time - spinStartTime:F2}s");
    }

    // ---------------------------------------------------------
    private bool ComputeIsSpinValid()
    {
        bool pinPlaced = (flagPin == null) || flagPin.isPlaced;
        bool enoughHits = hitsThisSpin >= minHitsRequired;

        float duration = Time.time - spinStartTime;
        bool enoughDuration = duration >= minSpinDuration;

        // 🔴 AHORA: hace falta pin colocado, hits suficientes *y* duración mínima
        return pinPlaced && enoughHits && enoughDuration;
    }

    // Sumamos hits desde FlagPin
    public void RegisterPinHit(FlagPin p)
    {
        if (!spinActive) return;
        hitsThisSpin++;
    }

    public bool WasLastSpinValid => lastSpinWasValid;

    private void UpdateRoundUI()
    {
        if (roundText != null)
            roundText.text = $"R-{currentRound}";
    }
}