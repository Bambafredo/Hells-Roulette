using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlagPin : BaseFlagPin
{
    [Header("Rewards")]
    public int moneyReward = 1;

    [Header("Round Logic")]
    public RoundManager roundManager;

    protected override void Awake()
    {
        base.Awake();

        if (roundManager == null)
            roundManager = FindObjectOfType<RoundManager>();
    }

    public override void RegisterHit()
    {
        base.RegisterHit();

        if (controller == null || CurrencyManager.Instance == null)
            return;

        CurrencyManager.Instance.AddPending(moneyReward);

        Debug.Log($"💰 Pending +{moneyReward} (pending total: {CurrencyManager.Instance.pendingDollars})");
    }
}