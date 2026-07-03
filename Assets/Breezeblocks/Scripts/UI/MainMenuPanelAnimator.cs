using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MainMenuPanelAnimator : MonoBehaviour
{
    [Title("Panels")]
    [SerializeField, Required]
    private GameObject mainMenuPanel;

    [SerializeField, Required]
    private GameObject levelsPanel;

    [SerializeField]
    private GameSettingsPanelController settingsPanel;

    [SerializeField]
    private LevelSelectPanelController levelSelectPanel;

    [Title("Main Menu Items")]
    [SerializeField, Required]
    private RectTransform titleTransform;

    [SerializeField, Required]
    private Button selectLevelButton;

    [SerializeField, Required]
    private Button settingsButton;

    [SerializeField, Required]
    private Button quitButton;

    [Title("Title Animation Options")]
    [SerializeField]
    private Vector2 titleExitOffset = new Vector2(0f, 420f);

    [SerializeField, MinValue(0f)]
    private float titleMoveDuration = 0.45f;

    [SerializeField]
    private Ease titleMoveEase = Ease.InBack;

    [Title("Button Animation Options")]
    [SerializeField]
    private Vector2 buttonExitOffset = new Vector2(0f, -520f);

    [SerializeField, MinValue(0f)]
    private float buttonMoveDuration = 0.35f;

    [SerializeField, MinValue(0f)]
    private float buttonStaggerDelay = 0.08f;

    [SerializeField]
    private Ease buttonMoveEase = Ease.InBack;

    [Title("Scene")]
    [SerializeField]
    private string quitFallbackSceneName = "MainMenu";

    private RectTransform selectLevelButtonTransform;
    private RectTransform settingsButtonTransform;
    private RectTransform quitButtonTransform;
    private Vector2 titleOriginalPosition;
    private Vector2 selectLevelOriginalPosition;
    private Vector2 settingsOriginalPosition;
    private Vector2 quitOriginalPosition;
    private Sequence menuSequence;

    /// <summary>
    /// Caches button transforms and their authored menu positions.
    /// </summary>
    private void Awake()
    {
        CacheButtonTransforms();
        CacheOriginalPositions();
        SetMainMenuInteraction(true);

        if (levelsPanel != null)
        {
            levelsPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Stops active menu tweens before Unity disables this animator.
    /// </summary>
    private void OnDisable()
    {
        KillMenuSequence();
    }

    /// <summary>
    /// Stops active menu tweens before Unity destroys this animator.
    /// </summary>
    private void OnDestroy()
    {
        KillMenuSequence();
    }

    /// <summary>
    /// Animates the main menu away before showing the levels panel.
    /// </summary>
    public void ShowLevelsPanel()
    {
        if (mainMenuPanel == null || levelsPanel == null)
        {
            return;
        }

        KillMenuSequence();
        SetMainMenuInteraction(false);

        menuSequence = DOTween.Sequence().SetUpdate(true);
        menuSequence.Join(titleTransform.DOAnchorPos(titleOriginalPosition + titleExitOffset, titleMoveDuration).SetEase(titleMoveEase));
        InsertButtonExit(quitButtonTransform, quitOriginalPosition, 0);
        InsertButtonExit(settingsButtonTransform, settingsOriginalPosition, 1);
        InsertButtonExit(selectLevelButtonTransform, selectLevelOriginalPosition, 2);
        menuSequence.OnComplete(ShowLevelsPanelAfterMainMenuExit);
    }

    /// <summary>
    /// Hides the levels panel and animates main menu elements back from their offscreen positions.
    /// </summary>
    public void ReturnToMainMenu()
    {
        if (mainMenuPanel == null)
        {
            return;
        }

        KillMenuSequence();
        SetMainMenuInteraction(false);

        if (levelSelectPanel != null)
        {
            levelSelectPanel.HideAnimated(PlayMainMenuReturnAnimation);
            return;
        }

        if (levelsPanel != null)
        {
            levelsPanel.SetActive(false);
        }

        PlayMainMenuReturnAnimation();
    }

    /// <summary>
    /// Opens the configured settings panel from the main menu.
    /// </summary>
    public void OpenSettingsPanel()
    {
        if (settingsPanel != null)
        {
            settingsPanel.Open();
        }
    }

    /// <summary>
    /// Quits the application, using a scene reload fallback while running inside the editor.
    /// </summary>
    public void QuitGame()
    {
#if UNITY_EDITOR
        if (!string.IsNullOrWhiteSpace(quitFallbackSceneName))
        {
            SceneManager.LoadScene(quitFallbackSceneName);
        }
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// Caches RectTransforms from assigned buttons so movement tweens do not call GetComponent repeatedly.
    /// </summary>
    private void CacheButtonTransforms()
    {
        selectLevelButtonTransform = GetButtonRectTransform(selectLevelButton);
        settingsButtonTransform = GetButtonRectTransform(settingsButton);
        quitButtonTransform = GetButtonRectTransform(quitButton);
    }

    /// <summary>
    /// Stores authored anchored positions for all animated menu items.
    /// </summary>
    private void CacheOriginalPositions()
    {
        if (titleTransform != null)
        {
            titleOriginalPosition = titleTransform.anchoredPosition;
        }

        selectLevelOriginalPosition = GetAnchoredPosition(selectLevelButtonTransform);
        settingsOriginalPosition = GetAnchoredPosition(settingsButtonTransform);
        quitOriginalPosition = GetAnchoredPosition(quitButtonTransform);
    }

    /// <summary>
    /// Inserts one button exit tween in bottom-to-top order.
    /// </summary>
    private void InsertButtonExit(RectTransform buttonTransform, Vector2 originalPosition, int order)
    {
        if (menuSequence == null || buttonTransform == null)
        {
            return;
        }

        float startTime = buttonStaggerDelay * order;
        menuSequence.Insert(startTime, buttonTransform.DOAnchorPos(originalPosition + buttonExitOffset, buttonMoveDuration).SetEase(buttonMoveEase));
    }

    /// <summary>
    /// Activates the levels panel after the main menu has finished leaving the screen.
    /// </summary>
    private void ShowLevelsPanelAfterMainMenuExit()
    {
        mainMenuPanel.SetActive(false);

        if (levelSelectPanel != null)
        {
            levelSelectPanel.PrepareForAnimatedShow();
        }

        levelsPanel.SetActive(true);

        if (levelSelectPanel != null)
        {
            levelSelectPanel.ShowAnimated();
        }
    }

    /// <summary>
    /// Starts the reverse main menu entrance animation from the saved offscreen positions.
    /// </summary>
    private void PlayMainMenuReturnAnimation()
    {
        if (mainMenuPanel == null)
        {
            return;
        }

        if (levelsPanel != null)
        {
            levelsPanel.SetActive(false);
        }

        mainMenuPanel.SetActive(true);
        SetMenuItemsToExitPositions();
        KillMenuSequence();

        menuSequence = DOTween.Sequence().SetUpdate(true);
        menuSequence.Join(titleTransform.DOAnchorPos(titleOriginalPosition, titleMoveDuration).SetEase(Ease.OutBack));
        InsertButtonReturn(selectLevelButtonTransform, selectLevelOriginalPosition, 0);
        InsertButtonReturn(settingsButtonTransform, settingsOriginalPosition, 1);
        InsertButtonReturn(quitButtonTransform, quitOriginalPosition, 2);
        menuSequence.OnComplete(() => SetMainMenuInteraction(true));
    }

    /// <summary>
    /// Places menu items at their configured offscreen positions before reverse animation starts.
    /// </summary>
    private void SetMenuItemsToExitPositions()
    {
        if (titleTransform != null)
        {
            titleTransform.anchoredPosition = titleOriginalPosition + titleExitOffset;
        }

        SetAnchoredPosition(selectLevelButtonTransform, selectLevelOriginalPosition + buttonExitOffset);
        SetAnchoredPosition(settingsButtonTransform, settingsOriginalPosition + buttonExitOffset);
        SetAnchoredPosition(quitButtonTransform, quitOriginalPosition + buttonExitOffset);
    }

    /// <summary>
    /// Inserts one button return tween in top-to-bottom order.
    /// </summary>
    private void InsertButtonReturn(RectTransform buttonTransform, Vector2 originalPosition, int order)
    {
        if (menuSequence == null || buttonTransform == null)
        {
            return;
        }

        float startTime = buttonStaggerDelay * order;
        menuSequence.Insert(startTime, buttonTransform.DOAnchorPos(originalPosition, buttonMoveDuration).SetEase(Ease.OutBack));
    }

    /// <summary>
    /// Enables or disables main menu buttons while menu transitions are running.
    /// </summary>
    private void SetMainMenuInteraction(bool isInteractable)
    {
        SetButtonInteractable(selectLevelButton, isInteractable);
        SetButtonInteractable(settingsButton, isInteractable);
        SetButtonInteractable(quitButton, isInteractable);
    }

    /// <summary>
    /// Gets a button RectTransform when the button reference exists.
    /// </summary>
    private static RectTransform GetButtonRectTransform(Button button)
    {
        return button != null ? button.transform as RectTransform : null;
    }

    /// <summary>
    /// Gets a RectTransform anchored position, returning zero for missing references.
    /// </summary>
    private static Vector2 GetAnchoredPosition(RectTransform target)
    {
        return target != null ? target.anchoredPosition : Vector2.zero;
    }

    /// <summary>
    /// Assigns a RectTransform anchored position when the reference exists.
    /// </summary>
    private static void SetAnchoredPosition(RectTransform target, Vector2 position)
    {
        if (target != null)
        {
            target.anchoredPosition = position;
        }
    }

    /// <summary>
    /// Sets a button interactable state when the reference exists.
    /// </summary>
    private static void SetButtonInteractable(Button button, bool isInteractable)
    {
        if (button != null)
        {
            button.interactable = isInteractable;
        }
    }

    /// <summary>
    /// Kills the active menu animation sequence when it exists.
    /// </summary>
    private void KillMenuSequence()
    {
        if (menuSequence == null || !menuSequence.IsActive())
        {
            return;
        }

        menuSequence.Kill();
        menuSequence = null;
    }
}
