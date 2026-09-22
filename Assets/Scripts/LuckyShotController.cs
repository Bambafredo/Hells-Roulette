using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class LuckyShotController : MonoBehaviour
{
    public static LuckyShotController Instance
    {
        get;
        private set;
    }


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

    private readonly Dictionary<Object, float>
        rewardBonusSources =
            new Dictionary<Object, float>();


    public float ActiveRewardBonusPercent
    {
        get
        {
            float total = 0f;

            foreach (
                KeyValuePair<Object, float> pair
                in rewardBonusSources)
            {
                if (pair.Key == null)
                    continue;

                total +=
                    Mathf.Max(
                        0f,
                        pair.Value
                    );
            }

            return total;
        }
    }


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        Instance = this;

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

        /*
         * Keyboard / Steam Deck shortcut.
         * It calls the exact same TryLuckyShot() path as the world-space button.
         */
        if (InputsManager.Instance != null &&
            InputsManager.Instance.LuckyShotPressed)
        {
            TryLuckyShot();
            return;
        }


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


    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }


    // =========================================================
    // LUCKY SHOT BONUS SOURCES
    // =========================================================

    public void RegisterRewardBonus(
        Object source,
        float percent)
    {
        if (source == null)
            return;

        rewardBonusSources[source] =
            Mathf.Max(
                0f,
                percent
            );
    }


    public void UnregisterRewardBonus(
        Object source)
    {
        if (source == null)
            return;

        rewardBonusSources.Remove(
            source
        );
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

        float finalRewardPercent =
            Mathf.Clamp(
                rewardPercent +
                ActiveRewardBonusPercent,
                0f,
                500f
            );


        bool started =
            roulette.TryStartLuckyShot(
                normalizedPower,
                finalRewardPercent,
                allowManualBrake
            );

        if (started)
        {
            Debug.Log(
                $"[LUCKY SHOT] Rolled {rolledPowerPercent:0.##}% power. " +
                $"Base Reward = {rewardPercent:0.##}%. " +
                $"Sticker Bonus = +{ActiveRewardBonusPercent:0.##}%. " +
                $"Final Reward = {finalRewardPercent:0.##}%."
            );
        }

        return started;
    }
}
