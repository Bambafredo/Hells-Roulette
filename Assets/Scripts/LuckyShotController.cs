using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class LuckyShotController : MonoBehaviour
{
    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("References")]

    [Tooltip("Roulette that will receive the Lucky Shot.")]
    public RouletteController roulette;

    [Tooltip(
        "World-space collider of the Lucky Shot button. " +
        "If left empty, the script uses the Collider2D on this GameObject."
    )]
    public Collider2D buttonCollider;


    // =========================================================
    // LUCKY SHOT SETTINGS
    // =========================================================

    [Header("Lucky Shot")]

    [Tooltip(
        "Minimum random launch power, expressed as a percentage " +
        "of the roulette's Power Spin maximum."
    )]
    [Range(0f, 100f)]
    public float minPowerPercent = 40f;

    [Tooltip(
        "Maximum random launch power, expressed as a percentage " +
        "of the roulette's Power Spin maximum."
    )]
    [Range(0f, 100f)]
    public float maxPowerPercent = 100f;

    [Tooltip(
        "Bonus paid at the end of a valid Lucky Shot. " +
        "Example: 100% = gain the spin's earned money again; " +
        "500% = gain five times the spin's earned money."
    )]
    [Range(0f, 500f)]
    public float rewardPercent = 100f;

    [Tooltip(
        "If enabled, the player can spend Blood to manually brake " +
        "the roulette during a Lucky Shot. Disable this for a fully " +
        "random Lucky Shot result after launch."
    )]
    public bool allowManualBrake = false;


    // =========================================================
    // DEBUG / PUBLIC STATE
    // =========================================================

    public float LastRolledPowerPercent
    {
        get;
        private set;
    } = 0f;


    private Camera cam;


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        if (roulette == null)
        {
            roulette =
                RouletteController.Instance;
        }

        if (roulette == null)
        {
            roulette =
                FindObjectOfType<RouletteController>();
        }

        if (buttonCollider == null)
        {
            buttonCollider =
                GetComponent<Collider2D>();
        }

        cam =
            Camera.main;
    }


    private void Update()
    {
#if UNITY_EDITOR || UNITY_STANDALONE

        if (!Input.GetMouseButtonDown(0))
            return;

        if (buttonCollider == null)
            return;

        if (cam == null)
            cam = Camera.main;

        if (cam == null)
            return;

        Vector2 mouseWorld =
            cam.ScreenToWorldPoint(
                Input.mousePosition
            );

        if (!buttonCollider.OverlapPoint(
                mouseWorld))
        {
            return;
        }

        /*
         * The Lucky Shot button and the roulette both read LMB.
         * Consume roulette pointer handling for this frame even if
         * the Lucky Shot cannot launch, so the same click can never
         * become an accidental manual drag.
         */
        if (roulette != null)
        {
            roulette
                .ConsumePointerInputThisFrame();
        }

        TryLuckyShot();

#endif
    }


    // =========================================================
    // LUCKY SHOT
    // =========================================================

    public bool TryLuckyShot()
    {
        if (roulette == null)
        {
            Debug.LogWarning(
                "[LUCKY SHOT] RouletteController reference missing."
            );

            return false;
        }

        if (!roulette.CanStartNewSpin())
        {
            return false;
        }

        float min =
            Mathf.Clamp(
                minPowerPercent,
                0f,
                100f
            );

        float max =
            Mathf.Clamp(
                maxPowerPercent,
                0f,
                100f
            );

        /*
         * Inspector mistakes should not break the feature.
         * If min/max are reversed, use the same authored values
         * but swap their runtime meaning.
         */
        if (min > max)
        {
            float temp = min;
            min = max;
            max = temp;
        }

        float rolledPowerPercent =
            Random.Range(
                min,
                max
            );

        LastRolledPowerPercent =
            rolledPowerPercent;

        float normalizedPower =
            rolledPowerPercent /
            100f;

        bool started =
            roulette.TryStartLuckyShot(
                normalizedPower,
                rewardPercent,
                allowManualBrake
            );

        if (started)
        {
            Debug.Log(
                $"[LUCKY SHOT] Rolled {rolledPowerPercent:0.##}% power. " +
                $"Reward = {rewardPercent:0.##}%."
            );
        }

        return started;
    }
}
