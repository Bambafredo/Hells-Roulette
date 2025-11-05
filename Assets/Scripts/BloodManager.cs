using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class BloodManager : MonoBehaviour
{
    public static BloodManager Instance;

    [Header("Stats")]
    public int maxBlood = 10;
    public int currentBlood;

    [Header("UI")]
    public Slider bloodSlider;
    public Image fill;
    public TMP_Text valueText;
    public Color fullColor = Color.red;
    public Color emptyColor = new Color(0.2f, 0f, 0f);

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
        currentBlood = maxBlood;
        UpdateUI();
    }

    public bool ConsumeBlood(int amount)
    {
        if (currentBlood <= 0) return false;

        currentBlood = Mathf.Max(0, currentBlood - amount);
        UpdateUI();

        if (currentBlood <= 0)
            OnDeath();

        return true;
    }

    public void HealBlood(int amount)
    {
        currentBlood = Mathf.Min(maxBlood, currentBlood + amount);
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (bloodSlider != null)
        {
            bloodSlider.maxValue = maxBlood;
            bloodSlider.value = currentBlood;
        }

        if (fill != null)
        {
            float t = (float)currentBlood / maxBlood;
            fill.color = Color.Lerp(emptyColor, fullColor, t);
        }

        if (valueText != null)
        {
            valueText.text = $"{currentBlood} / {maxBlood}";
        }
    }

    private void OnDeath()
    {
        Debug.Log("💀 Te has desangrado…");
        // Aquí podrías lanzar Game Over, reinicio, etc.
    }
}
