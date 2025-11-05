using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BrakeBarUI : MonoBehaviour
{
    [Header("References")]
    public Slider slider;
    public Image fill;
    public TMP_Text valueText; // 🩸 Texto para mostrar la sangre restante

    [Header("Colors")]
    public Color fullColor = Color.green;
    public Color emptyColor = Color.red;

    void Update()
    {
        if (slider == null) return;

        // 🌈 Color dinámico
        if (fill != null)
        {
            float t = slider.value / slider.maxValue;
            fill.color = Color.Lerp(emptyColor, fullColor, t);
        }

        // 🔢 Actualizar texto numérico
        if (valueText != null)
        {
            valueText.text = $"{slider.value:F1} / {slider.maxValue:F1}";
        }
    }
}
