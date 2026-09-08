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

    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("Game Over UI")]

    [Tooltip(
        "Root GameOver_Panel. Keep GameOverManager itself on an ACTIVE " +
        "GameObject such as GameManagers or Canvas; do not put this component " +
        "on the panel that starts inactive."
    )]
    [SerializeField]
    private GameObject gameOverPanel;


    [Tooltip("TMP below the Game Over title that explains why the run ended.")]
    [SerializeField]
    private TMP_Text defeatReasonText;


    [Tooltip(
        "Optional. RoundManager is found automatically when left empty."
    )]
    [SerializeField]
    private RoundManager roundManager;

    // =========================================================
    // AUTHORED REASON TEXT
    // =========================================================

    [Header("Defeat Reason Text")]

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
    // PRESENTATION
    // =========================================================

    [Header("Presentation")]

    [Tooltip(
        "If enabled, the normal spin-token icons referenced by RoundManager " +
        "are hidden when Game Over is shown."
    )]
    [SerializeField]
    private bool hideRoundTokenIcons =
        true;


    [Tooltip(
        "Optional extra roots to hide when Game Over appears. Useful for shared " +
        "modal panels such as Reward_Panel if a future Blood cost can kill the " +
        "player while that modal is open."
    )]
    [SerializeField]
    private GameObject[] additionalObjectsToHide =
        new GameObject[0];


    [Tooltip(
        "Optional behaviours to disable when Game Over appears. " +
        "Use this for systems that keep recreating presentation from Update, " +
        "such as tooltip managers. Disable the COMPONENTS, not their shared " +
        "GameObject, if several managers live on the same GameManagers root."
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


    private GameOverReason pendingReason =
        GameOverReason.BloodDepleted;


    public bool GameOverVisible
    {
        get;
        private set;
    } = false;

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
            QueuePresentation(
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


        TryPresentPendingGameOver();
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
    // GAME OVER PRESENTATION
    // =========================================================

    private void HandleGameOver()
    {
        GameOverReason reason =
            roundManager != null
                ? roundManager.CurrentGameOverReason
                : GameOverReason.BloodDepleted;


        QueuePresentation(
            reason
        );
    }


    private void QueuePresentation(
        GameOverReason reason)
    {
        pendingReason =
            reason;

        presentationPending =
            true;


        TryPresentPendingGameOver();
    }


    private void TryPresentPendingGameOver()
    {
        if (!presentationPending ||
            GameOverVisible)
        {
            return;
        }


        /*
         * A death can happen in the middle of RouletteController's gameplay
         * resolution. Do not cover the screen yet: RouletteController still has
         * to finish Blood/money totals and CommitSpinBlock() so the final log is
         * complete and readable on the Game Over screen.
         */
        if (RouletteController.Instance != null &&
            RouletteController.Instance.SpinInProgress)
        {
            return;
        }


        PresentGameOver(
            pendingReason
        );
    }


    private void PresentGameOver(
        GameOverReason reason)
    {
        presentationPending =
            false;


        if (defeatReasonText != null)
        {
            defeatReasonText.text =
                GetReasonText(
                    reason
                );
        }


        if (hideRoundTokenIcons)
        {
            HideRoundTokenIcons();
        }


        DisablePresentationBehaviours();


        HideAdditionalObjects();


        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(
                true
            );
        }
        else
        {
            Debug.LogError(
                "[GAME OVER UI] GameOver_Panel reference is missing."
            );
        }


        GameOverVisible =
            true;


        Debug.Log(
            $"[GAME OVER UI] Presented. Reason = {reason}."
        );
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


    private void DisablePresentationBehaviours()
    {
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


            behaviour.enabled =
                false;
        }
    }


    private void HideAdditionalObjects()
    {
        if (additionalObjectsToHide == null)
            return;


        foreach (GameObject target in
                 additionalObjectsToHide)
        {
            if (target == null)
                continue;


            target.SetActive(
                false
            );
        }
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
