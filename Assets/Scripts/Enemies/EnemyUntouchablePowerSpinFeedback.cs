using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime-only presentation controller for Untouchable.
///
/// Added automatically by EnemyCurseUntouchable when the Curse becomes active.
/// It does not own targeting logic; it only provides the temporary visual
/// feedback while Untouchable's configured trigger spin is active.
/// </summary>
[DisallowMultipleComponent]
public class EnemyUntouchablePowerSpinFeedback : MonoBehaviour
{
    private readonly Dictionary<EnemyCurseUntouchable, int>
        activeSources =
            new Dictionary<EnemyCurseUntouchable, int>();


    private BaseEnemy enemy;

    private SpriteRenderer trackedRenderer;

    private Color originalColor =
        Color.white;

    private bool feedbackApplied =
        false;


    /*
     * BaseEnemy's hit flash temporarily changes sprite.color.
     *
     * If the phasing window ends while that coroutine is still alive, its delayed
     * restore could otherwise put the faded/darkened color back AFTER we have
     * restored the normal enemy color.
     *
     * Keep a very short restore guard after the feedback window closes.
     */
    private float restoreGuardUntil =
        -1f;


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        enemy =
            GetComponent<BaseEnemy>();
    }


    private void Update()
    {
        if (activeSources.Count <= 0)
            return;


        if (enemy == null)
        {
            enemy =
                GetComponent<BaseEnemy>();
        }


        SpriteRenderer renderer =
            enemy != null
                ? enemy.sprite
                : null;


        if (renderer == null)
            return;


        HandleRendererChange(
            renderer
        );


        EnemyCurseUntouchable activeSource =
            GetActivePresentationSource();


        bool shouldShowFeedback =
            enemy.CombatActive &&
            !enemy.IsDead &&
            activeSource != null;


        if (shouldShowFeedback)
        {
            restoreGuardUntil =
                -1f;


            if (!feedbackApplied)
            {
                ApplyFeedback(
                    renderer,
                    activeSource
                );
            }


            return;
        }


        if (feedbackApplied)
        {
            RestoreFeedback();

            restoreGuardUntil =
                Time.unscaledTime +
                0.25f;

            return;
        }


        /*
         * See restoreGuardUntil comment above.
         */
        if (Time.unscaledTime <
                restoreGuardUntil &&
            trackedRenderer != null &&
            trackedRenderer.color != originalColor)
        {
            trackedRenderer.color =
                originalColor;
        }
    }


    private void OnDisable()
    {
        RestoreFeedback();
    }


    private void OnDestroy()
    {
        RestoreFeedback();
    }


    // =========================================================
    // REGISTRATION
    // =========================================================

    public void Register(
        EnemyCurseUntouchable source)
    {
        if (source == null)
            return;


        int count =
            0;


        activeSources.TryGetValue(
            source,
            out count
        );


        activeSources[source] =
            count + 1;


        enabled =
            true;
    }


    public void Unregister(
        EnemyCurseUntouchable source)
    {
        if (source == null)
            return;


        int count;


        if (!activeSources.TryGetValue(
                source,
                out count))
        {
            return;
        }


        count--;


        if (count <= 0)
        {
            activeSources.Remove(
                source
            );
        }
        else
        {
            activeSources[source] =
                count;
        }


        if (activeSources.Count > 0)
            return;


        RestoreFeedback();

        restoreGuardUntil =
            -1f;
    }


    // =========================================================
    // FEEDBACK WINDOW
    // =========================================================

    private bool IsTriggerWindowActive(
        EnemyCurseUntouchable source)
    {
        if (source == null)
            return false;


        /*
         * Power Spin feedback begins immediately when the player successfully
         * starts charging the Power Switch, before the physical spin launches.
         */
        if (source.Trigger ==
                EnemyCurseUntouchable.PhasingTrigger.PowerSpin &&
            PowerSpinController.Instance != null &&
            PowerSpinController.Instance.IsCharging)
        {
            return true;
        }


        RouletteController roulette =
            RouletteController.Instance;


        if (roulette == null ||
            !roulette.SpinInProgress)
        {
            return false;
        }


        /*
         * SpinInProgress stays true through the entire sticker/enemy resolution
         * pass, so feedback remains active until the triggering spin is
         * genuinely finished.
         */
        switch (source.Trigger)
        {
            case EnemyCurseUntouchable.PhasingTrigger.LuckyShot:
                return
                    roulette.CurrentSpinMethod ==
                    RouletteController.SpinMethod.LuckyShot;


            case EnemyCurseUntouchable.PhasingTrigger.PowerSpin:
            default:
                return
                    roulette.CurrentSpinMethod ==
                    RouletteController.SpinMethod.Power;
        }
    }


    // =========================================================
    // PRESENTATION
    // =========================================================

    private void HandleRendererChange(
        SpriteRenderer renderer)
    {
        if (trackedRenderer ==
            renderer)
        {
            return;
        }


        RestoreFeedback();


        trackedRenderer =
            renderer;

        feedbackApplied =
            false;

        restoreGuardUntil =
            -1f;
    }


    private void ApplyFeedback(
        SpriteRenderer renderer,
        EnemyCurseUntouchable source)
    {
        if (renderer == null ||
            source == null)
        {
            return;
        }


        trackedRenderer =
            renderer;

        originalColor =
            renderer.color;


        float strength =
            source.FeedbackStrength;


        Color feedbackColor =
            originalColor;


        switch (source.FeedbackMode)
        {
            case EnemyCurseUntouchable.PowerSpinFeedbackMode.Darken:

                feedbackColor.r =
                    Mathf.Lerp(
                        originalColor.r,
                        0f,
                        strength
                    );

                feedbackColor.g =
                    Mathf.Lerp(
                        originalColor.g,
                        0f,
                        strength
                    );

                feedbackColor.b =
                    Mathf.Lerp(
                        originalColor.b,
                        0f,
                        strength
                    );

                break;


            case EnemyCurseUntouchable.PowerSpinFeedbackMode.Fade:
            default:

                feedbackColor.a =
                    Mathf.Lerp(
                        originalColor.a,
                        0f,
                        strength
                    );

                break;
        }


        renderer.color =
            feedbackColor;


        feedbackApplied =
            true;
    }


    private void RestoreFeedback()
    {
        if (!feedbackApplied)
            return;


        if (trackedRenderer != null)
        {
            trackedRenderer.color =
                originalColor;
        }


        feedbackApplied =
            false;
    }


    private EnemyCurseUntouchable GetActivePresentationSource()
    {
        foreach (
            KeyValuePair<EnemyCurseUntouchable, int> entry
            in activeSources)
        {
            if (entry.Key == null ||
                entry.Value <= 0)
            {
                continue;
            }


            if (IsTriggerWindowActive(
                    entry.Key))
            {
                return
                    entry.Key;
            }
        }


        return null;
    }

}
