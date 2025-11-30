using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlagPin : BaseFlagPin
{
    [Header("Rewards")]
    public int moneyReward = 1;

    [Header("Round Logic")]
    public RoundManager roundManager;

    // --------------------------------------------
    // NUEVO: guardar posición y rotación original
    // --------------------------------------------
    [HideInInspector] public Vector3 originalPosition;
    [HideInInspector] public Quaternion originalRotation;
    [HideInInspector] public Transform originalParent;

    protected override void Awake()
    {
        base.Awake();

        if (roundManager == null)
            roundManager = FindObjectOfType<RoundManager>();

        // Guardamos su posición real al empezar
        originalPosition = transform.position;
        originalRotation = transform.rotation;
        originalParent = transform.parent;
    }

    // Este método lo usará WheelGenerator al restaurarlo
    public void RestoreToOriginal()
    {
        transform.SetParent(originalParent, true);
        transform.position = originalPosition;
        transform.rotation = originalRotation;
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