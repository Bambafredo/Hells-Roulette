using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BrakeBarUI : MonoBehaviour
{
    public Slider slider;
    public Image fill;
    public Color fullColor = Color.green;
    public Color emptyColor = Color.red;

    void Update()
    {
        if (slider != null && fill != null)
        {
            float t = slider.value / slider.maxValue;
            fill.color = Color.Lerp(emptyColor, fullColor, t);
        }
    }
}
