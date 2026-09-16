using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Main-menu controller for Hell's Roulette.
///
/// Responsibilities:
/// - Start a fresh Limbo run.
/// - Start a fresh Lust run.
/// - Quit the application.
/// - Optionally select a default button for keyboard/controller navigation.
///
/// This script intentionally owns no persistent game state.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    // =========================================================
    // SCENES
    // =========================================================

    [Header("Scenes")]

    [Tooltip("Gameplay scene used for a fresh Limbo run.")]
    [SerializeField]
    private string limboSceneName = "Scene_Main";

    [Tooltip("Gameplay scene used for a fresh Lust run.")]
    [SerializeField]
    private string lustSceneName = "Scene_Lust";


    // =========================================================
    // DEFAULT SELECTION
    // =========================================================

    [Header("Default Selection")]

    [Tooltip(
        "Optional. Assign the button that should already be selected when " +
        "using keyboard / controller navigation."
    )]
    [SerializeField]
    private Button firstSelectedButton;


    private void Start()
    {
        if (EventSystem.current == null ||
            firstSelectedButton == null)
        {
            return;
        }


        EventSystem.current.SetSelectedGameObject(
            firstSelectedButton.gameObject
        );
    }


    // =========================================================
    // RUN SELECTION
    // =========================================================

    /// <summary>
    /// Backwards-compatible entry point for the existing New Run / Limbo button.
    /// </summary>
    public void StartNewRun()
    {
        StartLimboRun();
    }


    public void StartLimboRun()
    {
        LoadRunScene(
            limboSceneName,
            "Limbo"
        );
    }


    public void StartLustRun()
    {
        LoadRunScene(
            lustSceneName,
            "Lust"
        );
    }


    private void LoadRunScene(
        string sceneName,
        string runLabel)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError(
                $"[MAIN MENU] {runLabel} scene name is empty."
            );

            return;
        }


        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError(
                $"[MAIN MENU] Cannot load {runLabel} scene '{sceneName}'. " +
                "Check the scene name and make sure it is included in Build Settings."
            );

            return;
        }


        SceneManager.LoadScene(
            sceneName
        );
    }


    // =========================================================
    // EXIT
    // =========================================================

    public void ExitGame()
    {
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
