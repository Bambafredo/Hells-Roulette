using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlagPin : BaseFlagPin
{
    [Header("Rewards")]
    [Tooltip("Cantidad de dinero que otorga este pin al ser golpeado durante una tirada activa.")]
    public int moneyReward = 1;

    public override void RegisterHit()
    {
        base.RegisterHit();

        // 💵 Solo sumar dinero si la ruleta está girando
        if (controller != null && controller.SpinInProgress && CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.AddDollar(moneyReward);
            Debug.Log($"💰 +{moneyReward}$ (Total: {CurrencyManager.Instance.dollars})");
        }
    }
}