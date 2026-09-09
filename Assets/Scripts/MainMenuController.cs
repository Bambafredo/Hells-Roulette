using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Minimal main-menu controller for Hell's Roulette.
///
/// Responsibilities:
/// - Start a fresh run by loading the gameplay scene.
/// - Quit the application.
/// - Optionally select the New Run button on scene start so controller /
///   keyboard navigation has a sensible default focus.
///
/// This script intentionally owns no persistent game state.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Scenes")]
    [SerializeField]
    private string gameplaySceneName = "Scene_Main";

    [Header("Default Selection")]
    [Tooltip(
        "Optional. Assign NewRun_Button here so the menu already has a selected " +
        "button when using keyboard / controller navigation."
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

    public void StartNewRun()
    {
        if (string.IsNullOrWhiteSpace(gameplaySceneName))
        {
            Debug.LogError(
                "[MAIN MENU] Gameplay scene name is empty."
            );

            return;
        }

        SceneManager.LoadScene(
            gameplaySceneName
        );
    }

    public void ExitGame()
    {
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
