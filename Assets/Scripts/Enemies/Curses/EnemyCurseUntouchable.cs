using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "EnemyCurse_Untouchable",
    menuName = "Hell's Roulette/Enemy Curses/Untouchable"
)]
public class EnemyCurseUntouchable : EnemyCurse
{
    // =========================================================
    // PHASING TRIGGER
    // =========================================================

    public enum PhasingTrigger
    {
        PowerSpin,
        LuckyShot
    }


    [Header("Phasing Trigger")]

    [Tooltip(
        "Spin method that activates Untouchable's single-target phasing. " +
        "Power Spin preserves the original behaviour. Lucky Shot makes the " +
        "enemy phase only during Lucky Shot spins."
    )]
    [SerializeField]
    private PhasingTrigger phasingTrigger =
        PhasingTrigger.PowerSpin;


    public PhasingTrigger Trigger =>
        phasingTrigger;


    // =========================================================
    // VISUAL FEEDBACK
    // =========================================================

    public enum PowerSpinFeedbackMode
    {
        Fade,
        Darken
    }


    [Header("Phasing Feedback")]

    [Tooltip(
        "Visual feedback used while Untouchable's configured trigger is active. " +
        "For Power Spin this begins while the Power Switch is being charged and " +
        "continues through the complete spin. For Lucky Shot it begins when the " +
        "Lucky Shot launches and continues through its complete resolution."
    )]
    [SerializeField]
    private PowerSpinFeedbackMode feedbackMode =
        PowerSpinFeedbackMode.Fade;


    [Tooltip(
        "0 = no visual change. " +
        "1 = maximum effect. " +
        "Fade: 1 makes the enemy fully transparent. " +
        "Darken: 1 makes the enemy fully black."
    )]
    [Range(0f, 1f)]
    [SerializeField]
    private float feedbackStrength =
        0.6f;


    public PowerSpinFeedbackMode FeedbackMode =>
        feedbackMode;


    public float FeedbackStrength =>
        Mathf.Clamp01(
            feedbackStrength
        );


    // =========================================================
    // TARGETING RULE
    // =========================================================

    /// <summary>
    /// Untouchable is a targeting rule rather than damage immunity:
    /// - directional single-target attacks pass to the next valid enemy;
    /// - random single-target attacks (Stone) exclude this enemy from the pool;
    /// - all-enemy / AoE damage still reaches this enemy normally.
    ///
    /// Which spin method enables that rule is authored in the asset.
    /// </summary>
    public override bool BlocksPowerSpinSingleTargeting
    {
        get
        {
            return
                phasingTrigger ==
                PhasingTrigger.PowerSpin;
        }
    }


    public override bool BlocksLuckyShotSingleTargeting
    {
        get
        {
            return
                phasingTrigger ==
                PhasingTrigger.LuckyShot;
        }
    }


    // =========================================================
    // TOOLTIP
    // =========================================================

    public override string GetTooltipDescription(
        BaseEnemy enemy,
        int value)
    {
        string authored =
            base.GetTooltipDescription(
                enemy,
                value
            );


        if (string.IsNullOrWhiteSpace(
                authored))
        {
            return "";
        }


        string triggerText =
            phasingTrigger ==
                PhasingTrigger.LuckyShot
                ? "Lucky Shots"
                : "Power Spins";


        return
            authored.Replace(
                "{trigger}",
                triggerText
            );
    }


    // =========================================================
    // LIFECYCLE
    // =========================================================

    public override void Activate(
        BaseEnemy enemy,
        int value)
    {
        if (enemy == null)
            return;


        /*
         * Legacy component name retained intentionally so existing project
         * references do not need a script/class rename. It now supports both
         * Power Spin and Lucky Shot trigger modes.
         */
        EnemyUntouchablePowerSpinFeedback feedback =
            enemy.GetComponent<EnemyUntouchablePowerSpinFeedback>();


        if (feedback == null)
        {
            feedback =
                enemy.gameObject
                    .AddComponent<EnemyUntouchablePowerSpinFeedback>();
        }


        feedback.Register(
            this
        );
    }


    public override void Deactivate(
        BaseEnemy enemy,
        int value)
    {
        if (enemy == null)
            return;


        EnemyUntouchablePowerSpinFeedback feedback =
            enemy.GetComponent<EnemyUntouchablePowerSpinFeedback>();


        if (feedback == null)
            return;


        feedback.Unregister(
            this
        );
    }
}
