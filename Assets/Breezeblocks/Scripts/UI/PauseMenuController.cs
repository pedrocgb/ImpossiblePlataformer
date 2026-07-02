using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(CanvasGroup))]
public sealed class PauseMenuController : MonoBehaviour
{
    [Title("References")]
    [SerializeField]
    private GameSettingsPanelController settingsPanel;

    [Title("Main Menu")]
    [SerializeField]
    private string mainMenuSceneName = "MainMenu";

    [Title("Audio Toggle")]
    [SerializeField]
    private AudioMixer masterAudioMixer;

    [SerializeField]
    private string masterVolumeParameter = "MasterVolume";

    [SerializeField, Range(0f, 1f)]
    private float unmutedMasterVolume = 1f;

    [Title("Animation")]
    [SerializeField, MinValue(0f)]
    private float fadeDuration = 0.2f;

    [SerializeField]
    private Ease fadeEase = Ease.OutQuad;

    private CanvasGroup canvasGroup;
    private Tween fadeTween;
    private float previousTimeScale = 1f;
    private bool isPaused;
    private bool isMuted;

    /// <summary>
    /// Caches the same-object CanvasGroup and hides the pause menu.
    /// </summary>
    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        HideImmediate();
    }

    /// <summary>
    /// Stops active tweens before Unity disables this menu.
    /// </summary>
    private void OnDisable()
    {
        KillFadeTween();
    }

    /// <summary>
    /// Stops active tweens before Unity destroys this menu.
    /// </summary>
    private void OnDestroy()
    {
        KillFadeTween();
    }

    /// <summary>
    /// Pauses gameplay and shows the pause menu.
    /// </summary>
    public void OpenPauseMenu()
    {
        if (isPaused)
        {
            return;
        }

        isPaused = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        FadeMenu(1f, true);
    }

    /// <summary>
    /// Resumes gameplay and hides the pause menu.
    /// </summary>
    public void ContinueGameplay()
    {
        if (!isPaused)
        {
            return;
        }

        isPaused = false;
        Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
        FadeMenu(0f, false);
    }

    /// <summary>
    /// Opens the assigned settings panel while the game remains paused.
    /// </summary>
    public void OpenSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.Open();
        }
    }

    /// <summary>
    /// Loads the configured main menu scene after restoring time scale.
    /// </summary>
    public void QuitToMainMenu()
    {
        Time.timeScale = 1f;

        if (SceneFadeUiManager.Current != null)
        {
            SceneFadeUiManager.Current.LoadScene(mainMenuSceneName);
            return;
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }

    /// <summary>
    /// Toggles master audio mute through the configured AudioMixer parameter.
    /// </summary>
    public void ToggleMasterAudio()
    {
        isMuted = !isMuted;
        SetMasterMute(isMuted);
    }

    /// <summary>
    /// Hides the pause menu instantly without changing time scale.
    /// </summary>
    private void HideImmediate()
    {
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    /// <summary>
    /// Fades the pause menu to the requested alpha and interaction state.
    /// </summary>
    private void FadeMenu(float targetAlpha, bool canInteract)
    {
        KillFadeTween();
        canvasGroup.interactable = canInteract;
        canvasGroup.blocksRaycasts = canInteract;
        fadeTween = canvasGroup.DOFade(targetAlpha, fadeDuration).SetEase(fadeEase).SetUpdate(true);
    }

    /// <summary>
    /// Applies the mute state to the master AudioMixer parameter.
    /// </summary>
    private void SetMasterMute(bool shouldMute)
    {
        if (masterAudioMixer == null || string.IsNullOrWhiteSpace(masterVolumeParameter))
        {
            return;
        }

        float volume = shouldMute ? 0f : unmutedMasterVolume;
        masterAudioMixer.SetFloat(masterVolumeParameter, AudioMixerVolumeUtility.LinearToDecibels(volume));
    }

    /// <summary>
    /// Stops the active fade tween when one exists.
    /// </summary>
    private void KillFadeTween()
    {
        if (fadeTween == null || !fadeTween.IsActive())
        {
            return;
        }

        fadeTween.Kill();
        fadeTween = null;
    }
}
