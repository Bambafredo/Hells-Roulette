using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance;

    [Header("Money")]
    public int dollars = 0; 
    public int pendingDollars = 0;

    [Header("UI")]
    public TMP_Text dollarsText;       // $ real
    public TMP_Text pendingText;       // +$

    private RouletteController roulette;
    private RoundManager round;

    private bool wasSpinning = false;

    // ---------------------------------------------------------
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        roulette = FindObjectOfType<RouletteController>();
        round = FindObjectOfType<RoundManager>();

        if (pendingText != null)
            pendingText.gameObject.SetActive(false);

        UpdateDollarsUI();
        UpdatePendingUI();
    }

    // ---------------------------------------------------------
    void Update()
    {
        if (roulette == null || round == null)
            return;

        bool spinningNow = roulette.SpinInProgress;

        // Detecta INICIO del spin
        if (spinningNow && !wasSpinning)
        {
            OnSpinStarted();
        }

        // Detecta FIN del spin
        if (!spinningNow && wasSpinning)
        {
            OnSpinEnded(round.WasLastSpinValid);
        }

        wasSpinning = spinningNow;

        // Si estamos girando, actualizar dinero pendiente en tiempo real
        if (spinningNow)
            UpdatePendingUI();
    }

    // ---------------------------------------------------------
    // SPIN EVENTS
    // ---------------------------------------------------------
    private void OnSpinStarted()
    {
        pendingDollars = 0;

        if (pendingText != null)
            pendingText.gameObject.SetActive(true);

        UpdatePendingUI();
    }

    private void OnSpinEnded(bool valid)
    {
        if (valid)
            AddDollar(pendingDollars);

        pendingDollars = 0;

        if (pendingText != null)
            pendingText.gameObject.SetActive(false);

        UpdatePendingUI();
        UpdateDollarsUI();
    }

    // ---------------------------------------------------------
    public void AddDollar(int amount)
    {
        dollars += amount;
        UpdateDollarsUI();
    }

    public void AddPending(int amount)
    {
        pendingDollars += amount;
        UpdatePendingUI();
    }

    // ---------------------------------------------------------
    // UI
    private void UpdateDollarsUI()
    {
        if (dollarsText != null)
            dollarsText.text = "$" + dollars.ToString();
    }

    private void UpdatePendingUI()
    {
        if (pendingText != null)
            pendingText.text = "+" + pendingDollars.ToString();
    }
}
