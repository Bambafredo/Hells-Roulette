using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class GameOverManager : MonoBehaviour
{
    public static GameOverManager Instance;

    private enum EndScreenMode
    {
        None,
        GameOver,
        Victory
    }

    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("Shared End Screen UI")]

    [Tooltip(
        "Root GameOver_Panel. The same panel is reused for normal Game Over " +
        "and the Limbo victory screen."
    )]
    [SerializeField]
    private GameObject gameOverPanel;


    [Tooltip(
        "Main TMP title. Shows the authored Game Over title on defeat and the " +
        "authored Victory title after defeating Limbo."
    )]
    [SerializeField]
    private TMP_Text titleText;


    [Tooltip(
        "TMP below the title. Shows the defeat reason on Game Over or the " +
        "authored victory message after defeating Limbo."
    )]
    [SerializeField]
    private TMP_Text defeatReasonText;


    [Tooltip(
        "Existing Retry button root. Active on Game Over, hidden on Victory."
    )]
    [SerializeField]
    private GameObject retryButton;


    [Tooltip(
        "Endless / Continue button root. Hidden on Game Over, active on Victory."
    )]
    [SerializeField]
    private GameObject endlessButton;


    [Tooltip(
        "Optional. RoundManager is found automatically when left empty."
    )]
    [SerializeField]
    private RoundManager roundManager;


    // =========================================================
    // DEFEAT AUTHORING
    // =========================================================

    [Header("Game Over Text")]

    [SerializeField]
    private string gameOverTitle =
        "GAME OVER";


    [SerializeField]
    private Color gameOverTitleColor =
        Color.red;


    [SerializeField]
    private Color gameOverReasonColor =
        Color.white;


    [TextArea(2, 4)]
    [SerializeField]
    private string bloodDepletedText =
        "You ran out of Blood.";


    [TextArea(2, 4)]
    [SerializeField]
    private string debtUnpaidText =
        "You couldn't pay your debt.";


    [TextArea(2, 4)]
    [SerializeField]
    private string limboMoneyDepletedText =
        "Limbo reduced your money to $0.";


    // =========================================================
    // VICTORY AUTHORING
    // =========================================================

    [Header("Victory Text")]

    [SerializeField]
    private string victoryTitle =
        "YOU WIN";


    [SerializeField]
    private Color victoryTitleColor =
        Color.green;


    [TextArea(2, 5)]
    [SerializeField]
    private string victoryMessage =
        "You defeated Limbo.";


    [SerializeField]
    private Color victoryMessageColor =
        Color.white;


    // =========================================================
    // PRESENTATION
    // =========================================================

    [Header("Presentation")]

    [Tooltip(
        "If enabled, the normal spin-token icons referenced by RoundManager " +
        "are hidden while the shared end screen is visible."
    )]
    [SerializeField]
    private bool hideRoundTokenIcons =
        true;


    [Tooltip(
        "Optional extra roots to hide while the shared end screen is visible."
    )]
    [SerializeField]
    private GameObject[] additionalObjectsToHide =
        new GameObject[0];


    [Tooltip(
        "Optional behaviours to disable while the shared end screen is visible. " +
        "Useful for tooltip/presentation managers."
    )]
    [SerializeField]
    private Behaviour[] behavioursToDisableOnGameOver =
        new Behaviour[0];


    // =========================================================
    // RUNTIME STATE
    // =========================================================

    private bool subscribed =
        false;


    private bool presentationPending =
        false;


    private EndScreenMode pendingMode =
        EndScreenMode.None;


    private GameOverReason pendingReason =
        GameOverReason.BloodDepleted;


    private EndScreenMode visibleMode =
        EndScreenMode.None;


    private readonly Dictionary<Behaviour, bool>
        previousBehaviourStates =
            new Dictionary<Behaviour, bool>();


    private readonly Dictionary<GameObject, bool>
        previousObjectStates =
            new Dictionary<GameObject, bool>();


    public bool GameOverVisible =>
        visibleMode ==
        EndScreenMode.GameOver;


    public bool VictoryVisible =>
        visibleMode ==
        EndScreenMode.Victory;


    public bool EndScreenVisible =>
        visibleMode !=
        EndScreenMode.None;


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(this);
            return;
        }


        Instance =
            this;


        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(
                false
            );
        }


        /*
         * Keep button states deterministic even if the scene was authored with
         * both buttons active.
         */
        if (retryButton != null)
        {
            retryButton.SetActive(
                true
            );
        }

        if (endlessButton != null)
        {
            endlessButton.SetActive(
                false
            );
        }
    }


    private void Start()
    {
        ResolveRoundManager();
        Subscribe();


        /*
         * Defensive startup catch in case another component managed to end the
         * run before this component subscribed because of script execution order.
         */
        if (roundManager != null &&
            roundManager.IsGameOver)
        {
            QueueGameOverPresentation(
                roundManager.CurrentGameOverReason
            );
        }
    }


    private void Update()
    {
        if (!subscribed)
        {
            ResolveRoundManager();
            Subscribe();
        }


        TryPresentPendingEndScreen();
    }


    private void OnDestroy()
    {
        Unsubscribe();


        if (Instance == this)
        {
            Instance =
                null;
        }
    }


    // =========================================================
    // ROUND SUBSCRIPTION
    // =========================================================

    private void ResolveRoundManager()
    {
        if (roundManager != null)
            return;


        roundManager =
            RoundManager.Instance != null
                ? RoundManager.Instance
                : FindObjectOfType<RoundManager>();
    }


    private void Subscribe()
    {
        if (subscribed ||
            roundManager == null)
        {
            return;
        }


        roundManager.OnGameOver +=
            HandleGameOver;

        subscribed =
            true;
    }


    private void Unsubscribe()
    {
        if (!subscribed ||
            roundManager == null)
        {
            return;
        }


        roundManager.OnGameOver -=
            HandleGameOver;

        subscribed =
            false;
    }


    // =========================================================
    // GAME OVER
    // =========================================================

    private void HandleGameOver()
    {
        GameOverReason reason =
            roundManager != null
                ? roundManager.CurrentGameOverReason
                : GameOverReason.BloodDepleted;


        QueueGameOverPresentation(
            reason
        );
    }


    private void QueueGameOverPresentation(
        GameOverReason reason)
    {
        /*
         * A terminal Game Over always wins over a queued/non-terminal Victory.
         * This is defensive; under normal gameplay the two should not compete.
         */
        pendingMode =
            EndScreenMode.GameOver;

        pendingReason =
            reason;

        presentationPending =
            true;


        TryPresentPendingEndScreen();
    }


    // =========================================================
    // VICTORY
    // =========================================================

    /// <summary>
    /// Called by LimboBossController when Limbo is defeated.
    ///
    /// Victory is intentionally NON-terminal. We temporarily block new spins,
    /// show the shared end screen after the current roulette resolution is fully
    /// finished, and allow the player to continue through Endless mode.
    /// </summary>
    public void RequestVictory()
    {
        ResolveRoundManager();


        if (roundManager != null &&
            roundManager.IsGameOver)
        {
            return;
        }


        if (EndScreenVisible ||
            (
                presentationPending &&
                pendingMode ==
                    EndScreenMode.Victory
            ))
        {
            return;
        }


        /*
         * Block gameplay immediately, not only when the panel becomes visible.
         *
         * If Limbo dies during the final part of a spin, RoundManager can safely
         * defer Debt / Reward flow until the player chooses Endless.
         */
        roundManager?.SetExternalSpinBlock(
            true
        );


        pendingMode =
            EndScreenMode.Victory;

        presentationPending =
            true;


        TryPresentPendingEndScreen();
    }


    // =========================================================
    // SHARED PRESENTATION
    // =========================================================

    private void TryPresentPendingEndScreen()
    {
        if (!presentationPending ||
            EndScreenVisible)
        {
            return;
        }


        /*
         * Do not cover the screen in the middle of RouletteController's
         * resolution. The final gameplay/log state must finish first.
         */
        if (RouletteController.Instance != null &&
            RouletteController.Instance.SpinInProgress)
        {
            return;
        }


        EndScreenMode modeToPresent =
            pendingMode;

        presentationPending =
            false;

        pendingMode =
            EndScreenMode.None;


        if (modeToPresent ==
            EndScreenMode.Victory)
        {
            PresentVictory();
        }
        else
        {
            PresentGameOver(
                pendingReason
            );
        }
    }


    private void PresentGameOver(
        GameOverReason reason)
    {
        ConfigureGameOverText(
            reason
        );

        ConfigureButtons(
            showRetry: true,
            showEndless: false
        );

        ApplySharedPresentation();


        visibleMode =
            EndScreenMode.GameOver;


        Debug.Log(
            $"[GAME OVER UI] Presented. Reason = {reason}."
        );
    }


    private void PresentVictory()
    {
        ConfigureVictoryText();

        ConfigureButtons(
            showRetry: false,
            showEndless: true
        );

        ApplySharedPresentation();


        visibleMode =
            EndScreenMode.Victory;


        Debug.Log(
            "[VICTORY UI] Limbo defeated. Victory screen presented."
        );
    }


    private void ApplySharedPresentation()
    {
        CaptureAndDisablePresentationBehaviours();
        CaptureAndHideAdditionalObjects();


        if (hideRoundTokenIcons)
        {
            HideRoundTokenIcons();
        }


        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(
                true
            );
        }
        else
        {
            Debug.LogError(
                "[END SCREEN UI] GameOver_Panel reference is missing."
            );
        }
    }


    private void ConfigureGameOverText(
        GameOverReason reason)
    {
        if (titleText != null)
        {
            titleText.text =
                gameOverTitle;

            titleText.color =
                gameOverTitleColor;
        }


        if (defeatReasonText != null)
        {
            defeatReasonText.text =
                GetReasonText(
                    reason
                );

            defeatReasonText.color =
                gameOverReasonColor;
        }
    }


    private void ConfigureVictoryText()
    {
        if (titleText != null)
        {
            titleText.text =
                victoryTitle;

            titleText.color =
                victoryTitleColor;
        }


        if (defeatReasonText != null)
        {
            defeatReasonText.text =
                victoryMessage;

            defeatReasonText.color =
                victoryMessageColor;
        }
    }


    private void ConfigureButtons(
        bool showRetry,
        bool showEndless)
    {
        if (retryButton != null)
        {
            retryButton.SetActive(
                showRetry
            );
        }


        if (endlessButton != null)
        {
            endlessButton.SetActive(
                showEndless
            );
        }
    }


    private string GetReasonText(
        GameOverReason reason)
    {
        switch (reason)
        {
            case GameOverReason.DebtUnpaid:
                return
                    debtUnpaidText;


            case GameOverReason.LimboMoneyDepleted:
                return
                    limboMoneyDepletedText;


            case GameOverReason.BloodDepleted:
            default:
                return
                    bloodDepletedText;
        }
    }


    // =========================================================
    // TEMPORARY PRESENTATION SUPPRESSION
    // =========================================================

    private void HideRoundTokenIcons()
    {
        if (roundManager == null ||
            roundManager.tokenIcons == null)
        {
            return;
        }


        foreach (GameObject icon in
                 roundManager.tokenIcons)
        {
            if (icon == null)
                continue;


            icon.SetActive(
                false
            );
        }
    }


    private void RestoreRoundTokenIcons()
    {
        if (roundManager == null ||
            roundManager.tokenIcons == null)
        {
            return;
        }


        for (int i = 0;
             i < roundManager.tokenIcons.Length;
             i++)
        {
            GameObject icon =
                roundManager.tokenIcons[i];

            if (icon == null)
                continue;


            icon.SetActive(
                i <
                roundManager.TokensRemaining
            );
        }
    }


    private void CaptureAndDisablePresentationBehaviours()
    {
        previousBehaviourStates.Clear();


        if (behavioursToDisableOnGameOver == null)
            return;


        foreach (Behaviour behaviour in
                 behavioursToDisableOnGameOver)
        {
            if (behaviour == null ||
                behaviour == this)
            {
                continue;
            }


            if (!previousBehaviourStates.ContainsKey(
                    behaviour))
            {
                previousBehaviourStates.Add(
                    behaviour,
                    behaviour.enabled
                );
            }


            behaviour.enabled =
                false;
        }
    }


    private void RestorePresentationBehaviours()
    {
        foreach (KeyValuePair<Behaviour, bool> entry in
                 previousBehaviourStates)
        {
            if (entry.Key == null)
                continue;


            entry.Key.enabled =
                entry.Value;
        }


        previousBehaviourStates.Clear();
    }


    private void CaptureAndHideAdditionalObjects()
    {
        previousObjectStates.Clear();


        if (additionalObjectsToHide == null)
            return;


        foreach (GameObject target in
                 additionalObjectsToHide)
        {
            if (target == null)
                continue;


            if (!previousObjectStates.ContainsKey(
                    target))
            {
                previousObjectStates.Add(
                    target,
                    target.activeSelf
                );
            }


            target.SetActive(
                false
            );
        }
    }


    private void RestoreAdditionalObjects()
    {
        foreach (KeyValuePair<GameObject, bool> entry in
                 previousObjectStates)
        {
            if (entry.Key == null)
                continue;


            entry.Key.SetActive(
                entry.Value
            );
        }


        previousObjectStates.Clear();
    }


    // =========================================================
    // BUTTONS
    // =========================================================

    public void RetryRun()
    {
        Time.timeScale =
            1f;


        Scene activeScene =
            SceneManager.GetActiveScene();


        SceneManager.LoadScene(
            activeScene.buildIndex
        );
    }


    /// <summary>
    /// Closes the non-terminal Victory screen and continues the SAME run.
    /// Normal encounter / debt / reward flow resumes from exactly where it was
    /// temporarily blocked when Limbo died.
    /// </summary>
    public void ContinueEndless()
    {
        if (visibleMode !=
            EndScreenMode.Victory)
        {
            return;
        }


        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(
                false
            );
        }


        RestorePresentationBehaviours();
        RestoreAdditionalObjects();


        if (hideRoundTokenIcons)
        {
            RestoreRoundTokenIcons();
        }


        visibleMode =
            EndScreenMode.None;


        /*
         * Release this LAST. RoundManager may immediately resume deferred
         * Debt / Reward flow when this lock is removed.
         */
        roundManager?.SetExternalSpinBlock(
            false
        );


        Debug.Log(
            "[VICTORY UI] Endless selected. Current run continues."
        );
    }


    public void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying =
            false;
#else
        Application.Quit();
#endif
    }
}
