using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
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


    // =========================================================
    // DAMAGE RESULT
    // =========================================================

    /// <summary>
    /// Result returned by TakeDamage().
    ///
    /// requestedDamage = raw incoming damage.
    /// preventedDamage = damage absorbed by registered protection.
    /// bloodLost = actual Blood removed from the player.
    /// </summary>
    public struct DamageResult
    {
        public int requestedDamage;
        public int preventedDamage;
        public int bloodLost;

        public DamageResult(
            int requestedDamage,
            int preventedDamage,
            int bloodLost)
        {
            this.requestedDamage =
                requestedDamage;

            this.preventedDamage =
                preventedDamage;

            this.bloodLost =
                bloodLost;
        }
    }


    /// <summary>
    /// Event sent back to one registered blocker whenever it actually
    /// prevents damage.
    /// </summary>
    public struct DamageBlockEvent
    {
        public int preventedDamage;
        public int remainingCapacity;
        public bool firstPreventionForBlocker;

        public DamageBlockEvent(
            int preventedDamage,
            int remainingCapacity,
            bool firstPreventionForBlocker)
        {
            this.preventedDamage =
                preventedDamage;

            this.remainingCapacity =
                remainingCapacity;

            this.firstPreventionForBlocker =
                firstPreventionForBlocker;
        }
    }


    // =========================================================
    // SPIN DAMAGE PROTECTION
    // =========================================================

    private class DamageBlockerRegistration
    {
        public UnityEngine.Object source;
        public int remainingCapacity;
        public bool hasPreventedDamage;
        public Action<DamageBlockEvent> onDamagePrevented;
    }


    private readonly List<DamageBlockerRegistration>
        spinDamageBlockers =
            new List<DamageBlockerRegistration>();


    private bool spinDamageProtectionWindowActive =
        false;


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
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


    // =========================================================
    // BLOOD COST / DIRECT CONSUMPTION
    // =========================================================

    /// <summary>
    /// Direct Blood spending / consumption.
    ///
    /// IMPORTANT:
    /// This intentionally bypasses Shield and every other damage blocker.
    /// Use this for costs such as:
    /// - manual braking
    /// - Power Spin costs
    /// - Reward purchases / rerolls
    ///
    /// Actual DAMAGE should call TakeDamage() instead.
    /// </summary>
    public bool ConsumeBlood(int amount)
    {
        if (amount <= 0)
            return false;

        if (currentBlood <= 0)
            return false;

        currentBlood =
            Mathf.Max(
                0,
                currentBlood - amount
            );

        UpdateUI();

        if (currentBlood <= 0)
            OnDeath();

        return true;
    }


    // =========================================================
    // DAMAGE
    // =========================================================

    /// <summary>
    /// Applies actual player damage through the shared mitigation pipeline.
    ///
    /// Costs must NOT use this method.
    ///
    /// Shield registrations are cumulative ONLY inside the current valid
    /// spin resolution. A 10-point Shield can absorb 4 from one attack and
    /// another 6 from a later attack during that same spin.
    ///
    /// The remaining capacity is discarded when the spin resolution ends and
    /// never carries into the next spin.
    /// </summary>
    public DamageResult TakeDamage(
        int amount)
    {
        int requestedDamage =
            Mathf.Max(
                0,
                amount
            );


        if (requestedDamage <= 0 ||
            currentBlood <= 0)
        {
            return
                new DamageResult(
                    requestedDamage,
                    0,
                    0
                );
        }


        /*
         * Damage beyond the player's remaining Blood cannot meaningfully be
         * "prevented". Capping here keeps Shield use consumption truthful:
         * a Shield only spends a use when it saves Blood that could actually
         * have been lost.
         */
        int remainingDamage =
            Mathf.Min(
                requestedDamage,
                currentBlood
            );


        int preventedTotal =
            0;


        if (spinDamageProtectionWindowActive &&
            spinDamageBlockers.Count > 0)
        {
            foreach (DamageBlockerRegistration blocker in
                     spinDamageBlockers)
            {
                if (remainingDamage <= 0)
                    break;


                if (blocker == null ||
                    blocker.source == null ||
                    blocker.remainingCapacity <= 0)
                {
                    continue;
                }


                int prevented =
                    Mathf.Min(
                        remainingDamage,
                        blocker.remainingCapacity
                    );


                if (prevented <= 0)
                    continue;


                remainingDamage -=
                    prevented;

                blocker.remainingCapacity -=
                    prevented;

                preventedTotal +=
                    prevented;


                bool firstPrevention =
                    !blocker.hasPreventedDamage;

                blocker.hasPreventedDamage =
                    true;


                blocker.onDamagePrevented?
                    .Invoke(
                        new DamageBlockEvent(
                            prevented,
                            blocker.remainingCapacity,
                            firstPrevention
                        )
                    );
            }
        }


        int bloodBefore =
            currentBlood;


        if (remainingDamage > 0)
        {
            ConsumeBlood(
                remainingDamage
            );
        }


        int actualBloodLost =
            Mathf.Max(
                0,
                bloodBefore - currentBlood
            );


        return
            new DamageResult(
                requestedDamage,
                preventedTotal,
                actualBloodLost
            );
    }


    // =========================================================
    // DAMAGE BLOCK REGISTRATION
    // =========================================================

    /// <summary>
    /// Starts the temporary protection window for one valid spin resolution.
    /// Any stale registrations are discarded defensively.
    /// </summary>
    public void BeginSpinDamageProtectionWindow()
    {
        spinDamageBlockers.Clear();

        spinDamageProtectionWindowActive =
            true;
    }


    /// <summary>
    /// Registers one independent damage-capacity pool for the current valid
    /// spin. Duplicate registrations from the same physical source are
    /// ignored.
    /// </summary>
    public bool RegisterSpinDamageBlocker(
        UnityEngine.Object source,
        int capacity,
        Action<DamageBlockEvent> onDamagePrevented)
    {
        if (!spinDamageProtectionWindowActive ||
            source == null ||
            capacity <= 0)
        {
            return false;
        }


        foreach (DamageBlockerRegistration existing in
                 spinDamageBlockers)
        {
            if (existing != null &&
                existing.source == source)
            {
                return false;
            }
        }


        spinDamageBlockers.Add(
            new DamageBlockerRegistration
            {
                source = source,
                remainingCapacity = capacity,
                hasPreventedDamage = false,
                onDamagePrevented = onDamagePrevented
            }
        );


        return true;
    }


    /// <summary>
    /// Ends the current valid-spin protection window.
    /// Shield protection never leaks into menus, rewards or the next spin.
    /// </summary>
    public void EndSpinDamageProtectionWindow()
    {
        spinDamageBlockers.Clear();

        spinDamageProtectionWindowActive =
            false;
    }


    // =========================================================
    // HEAL
    // =========================================================

    public void HealBlood(int amount)
    {
        currentBlood =
            Mathf.Min(
                maxBlood,
                currentBlood + amount
            );

        UpdateUI();
    }


    // =========================================================
    // UI
    // =========================================================

    private void UpdateUI()
    {
        if (bloodSlider != null)
        {
            bloodSlider.maxValue = maxBlood;
            bloodSlider.value = currentBlood;
        }

        if (fill != null)
        {
            float t =
                (float)currentBlood /
                maxBlood;

            fill.color =
                Color.Lerp(
                    emptyColor,
                    fullColor,
                    t
                );
        }

        if (valueText != null)
        {
            valueText.text =
                $"{currentBlood} / {maxBlood}";
        }
    }


    private void OnDeath()
    {
        Debug.Log(
            "💀 Te has desangrado…"
        );

        // Aquí podrías lanzar Game Over, reinicio, etc.
    }
}
