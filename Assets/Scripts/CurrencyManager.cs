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
    public TMP_Text dollarsText;   // Dinero real: $X
    public TMP_Text pendingText;   // Dinero de la tirada: +X

    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        UpdateDollarsUI();
        UpdatePendingUI();

        SetPendingVisible(false);
    }

    // =========================================================
    // REAL MONEY
    // =========================================================

    /// <summary>
    /// Añade dinero directamente al total real del jugador.
    ///
    /// Utilizar para:
    /// - recompensas ya resueltas
    /// - stickers que otorguen dinero fuera de una tirada
    /// - debug
    ///
    /// El dinero generado DURANTE una tirada debería entrar
    /// primero mediante AddPending().
    /// </summary>
    public void AddDollar(int amount)
    {
        dollars += amount;

        /*
         * Por seguridad no permitimos dinero negativo
         * mediante AddDollar.
         *
         * Para gastar dinero debe utilizarse Spend().
         */
        if (dollars < 0)
            dollars = 0;

        UpdateDollarsUI();
    }

    /// <summary>
    /// Devuelve true si el jugador tiene dinero suficiente.
    /// </summary>
    public bool CanAfford(int amount)
    {
        if (amount <= 0)
            return true;

        return dollars >= amount;
    }

    /// <summary>
    /// Intenta gastar una cantidad de dinero.
    ///
    /// Devuelve:
    /// true  -> se ha podido pagar
    /// false -> no había dinero suficiente
    ///
    /// Esto será lo que utilizará la deuda.
    /// </summary>
    public bool Spend(int amount)
    {
        if (amount <= 0)
            return true;

        if (!CanAfford(amount))
            return false;

        dollars -= amount;

        UpdateDollarsUI();

        return true;
    }

    // =========================================================
    // PENDING MONEY
    // =========================================================

    /// <summary>
    /// Debe llamarse cuando comienza una nueva tirada real.
    ///
    /// Elimina cualquier dinero pendiente residual
    /// y prepara la UI.
    /// </summary>
    public void BeginSpin()
    {
        pendingDollars = 0;

        UpdatePendingUI();
        SetPendingVisible(true);
    }

    /// <summary>
    /// Añade dinero pendiente generado durante la tirada.
    ///
    /// Todavía NO forma parte del dinero real del jugador.
    /// </summary>
    public void AddPending(int amount)
    {
        pendingDollars += amount;

        /*
         * Permitimos cantidades negativas en pending porque
         * en el futuro puede haber stickers/enemigos que
         * modifiquen la recompensa de una tirada.
         *
         * Pero nunca dejamos el resultado por debajo de cero
         * de momento.
         */
        if (pendingDollars < 0)
            pendingDollars = 0;

        SetPendingVisible(true);
        UpdatePendingUI();
    }

    /// <summary>
    /// Confirma el dinero pendiente de una tirada válida.
    ///
    /// Después de esto:
    /// pending = 0
    /// dollars += pending anterior
    /// </summary>
    public int CommitPending()
    {
        int amountCommitted = pendingDollars;

        if (amountCommitted > 0)
            dollars += amountCommitted;

        pendingDollars = 0;

        UpdateDollarsUI();
        UpdatePendingUI();
        SetPendingVisible(false);

        return amountCommitted;
    }

    /// <summary>
    /// Descarta el dinero pendiente.
    ///
    /// Se utilizará cuando una tirada no sea válida.
    /// </summary>
    public void ClearPending()
    {
        pendingDollars = 0;

        UpdatePendingUI();
        SetPendingVisible(false);
    }

    // =========================================================
    // RESET
    // =========================================================

    /// <summary>
    /// Reset explícito de economía.
    ///
    /// Probablemente el Game Over recargará la escena,
    /// pero dejamos esta función disponible para debug
    /// y futuros resets parciales.
    /// </summary>
    public void ResetCurrency()
    {
        dollars = 0;
        pendingDollars = 0;

        UpdateDollarsUI();
        UpdatePendingUI();
        SetPendingVisible(false);
    }

    // =========================================================
    // UI
    // =========================================================

    private void UpdateDollarsUI()
    {
        if (dollarsText != null)
            dollarsText.text = "$" + dollars;
    }

    private void UpdatePendingUI()
    {
        if (pendingText != null)
            pendingText.text = "+" + pendingDollars;
    }

    private void SetPendingVisible(bool visible)
    {
        if (pendingText != null)
            pendingText.gameObject.SetActive(visible);
    }
}
