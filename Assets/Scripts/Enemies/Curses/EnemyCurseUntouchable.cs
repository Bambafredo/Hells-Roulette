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
    // POWER SPIN FEEDBACK
    // =========================================================

    public enum PowerSpinFeedbackMode
    {
        Fade,
        Darken
    }


    [Header("Power Spin Feedback")]

    [Tooltip(
        "Visual feedback used while the Power Switch is being charged and " +
        "throughout the complete Power Spin."
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
    /// While this Curse is active, EnemyPanelManager skips its owner when a
    /// Power Spin resolves a single-target attack.
    ///
    /// This is a targeting rule rather than damage immunity:
    /// - left/right single-target attacks pass to the next valid enemy;
    /// - random single-target attacks (Stone) exclude this enemy from the pool;
    /// - all-enemy / AoE damage still reaches this enemy normally.
    /// </summary>
    public override bool BlocksPowerSpinSingleTargeting
    {
        get { return true; }
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
