using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance;

    [Header("UI")]
    public TextMeshProUGUI dollarText;

    [Header("Values")]
    public int dollars = 0;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void AddDollar(int amount = 1)
    {
        dollars += amount;
        UpdateUI();
    }

    public void UpdateUI()
    {
        if (dollarText != null)
            dollarText.text = $"${dollars}";
    }
}
