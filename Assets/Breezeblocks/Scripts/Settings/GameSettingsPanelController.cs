using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public sealed class GameSettingsPanelController : MonoBehaviour
{
    private enum RenderModeOption
    {
        Fullscreen,
        BorderlessWindow,
        Windowed
    }

    private readonly int[] frameRateOptions = { 30, 60, 120, 144, -1 };

    [Title("References")]
    [SerializeField]
    private TMP_Text settingsTitleText;

    [SerializeField]
    private GameObject videoOptionsRoot;

    [SerializeField]
    private GameObject audioOptionsRoot;

    [SerializeField]
    private GameObject controlsOptionsRoot;

    [SerializeField]
    private RewiredControlMapperSettingsBridge controlsBridge;

    [Title("Titles")]
    [SerializeField]
    private string videoSettingsTitle = "Video";

    [SerializeField]
    private string audioSettingsTitle = "Audio";

    [SerializeField]
    private string controlsSettingsTitle = "Controls";

    [Title("Video")]
    [SerializeField]
    private TMP_Dropdown resolutionDropdown;

    [SerializeField]
    private TMP_Dropdown monitorDropdown;

    [SerializeField]
    private TMP_Dropdown renderModeDropdown;

    [SerializeField]
    private Toggle vsyncToggle;

    [SerializeField]
    private TMP_Dropdown frameRateDropdown;

    [SerializeField]
    private Camera[] displayCameras = new Camera[0];

    [SerializeField]
    private Canvas[] displayCanvases = new Canvas[0];

    [Title("Audio")]
    [SerializeField]
    private AudioMixer audioMixer;

    [SerializeField]
    private string masterVolumeParameter = "MasterVolume";

    [SerializeField]
    private string musicVolumeParameter = "MusicVolume";

    [SerializeField]
    private string sfxVolumeParameter = "SFXVolume";

    [SerializeField]
    private string uiVolumeParameter = "UIVolume";

    [SerializeField]
    private Slider masterVolumeSlider;

    [SerializeField]
    private Slider musicVolumeSlider;

    [SerializeField]
    private Slider sfxVolumeSlider;

    [SerializeField]
    private Slider uiVolumeSlider;

    [Title("Animation")]
    [SerializeField, MinValue(0f)]
    private float fadeDuration = 0.2f;

    [SerializeField]
    private Ease fadeEase = Ease.OutQuad;

    private CanvasGroup canvasGroup;
    private Tween fadeTween;
    private Resolution[] availableResolutions = new Resolution[0];

    /// <summary>
    /// Caches the same-object CanvasGroup used by the settings panel.
    /// </summary>
    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        SetupVideoControls();
        SetupAudioControls();
        HideImmediate();
    }

    /// <summary>
    /// Stops active tweens before Unity disables this panel.
    /// </summary>
    private void OnDisable()
    {
        KillFadeTween();
    }

    /// <summary>
    /// Stops active tweens before Unity destroys this panel.
    /// </summary>
    private void OnDestroy()
    {
        KillFadeTween();
    }

    /// <summary>
    /// Opens the settings panel and defaults to the video tab.
    /// </summary>
    public void Open()
    {
        Show();
        ShowVideoSettings();
    }

    /// <summary>
    /// Tries to close the settings panel if controls validation allows it.
    /// </summary>
    public void TryClose()
    {
        if (controlsBridge != null && !controlsBridge.CanCloseSettings())
        {
            ShowControlsSettings();
            return;
        }

        Hide();
    }

    /// <summary>
    /// Shows the video options group.
    /// </summary>
    public void ShowVideoSettings()
    {
        SetActiveGroup(videoOptionsRoot, true);
        SetActiveGroup(audioOptionsRoot, false);
        SetActiveGroup(controlsOptionsRoot, false);
        SetTitle(videoSettingsTitle);
    }

    /// <summary>
    /// Shows the audio options group.
    /// </summary>
    public void ShowAudioSettings()
    {
        SetActiveGroup(videoOptionsRoot, false);
        SetActiveGroup(audioOptionsRoot, true);
        SetActiveGroup(controlsOptionsRoot, false);
        SetTitle(audioSettingsTitle);
    }

    /// <summary>
    /// Shows the controls options group and opens the Rewired mapper when assigned.
    /// </summary>
    public void ShowControlsSettings()
    {
        SetActiveGroup(videoOptionsRoot, false);
        SetActiveGroup(audioOptionsRoot, false);
        SetActiveGroup(controlsOptionsRoot, true);
        SetTitle(controlsSettingsTitle);
        controlsBridge?.OpenMapper();
    }

    /// <summary>
    /// Applies the selected resolution to the game window.
    /// </summary>
    public void ApplyResolution(int optionIndex)
    {
        if (optionIndex < 0 || optionIndex >= availableResolutions.Length)
        {
            return;
        }

        Resolution resolution = availableResolutions[optionIndex];
        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreenMode);
    }

    /// <summary>
    /// Applies the selected display index to configured cameras and canvases.
    /// </summary>
    public void ApplyMonitor(int optionIndex)
    {
        if (optionIndex < 0 || optionIndex >= Display.displays.Length)
        {
            return;
        }

        Display.displays[optionIndex].Activate();

        for (int i = 0; i < displayCameras.Length; i++)
        {
            if (displayCameras[i] != null)
            {
                displayCameras[i].targetDisplay = optionIndex;
            }
        }

        for (int i = 0; i < displayCanvases.Length; i++)
        {
            if (displayCanvases[i] != null)
            {
                displayCanvases[i].targetDisplay = optionIndex;
            }
        }
    }

    /// <summary>
    /// Applies the selected fullscreen mode.
    /// </summary>
    public void ApplyRenderMode(int optionIndex)
    {
        RenderModeOption selectedMode = (RenderModeOption)Mathf.Clamp(optionIndex, 0, 2);

        switch (selectedMode)
        {
            case RenderModeOption.Fullscreen:
                Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen;
                break;
            case RenderModeOption.BorderlessWindow:
                Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
                break;
            case RenderModeOption.Windowed:
                Screen.fullScreenMode = FullScreenMode.Windowed;
                break;
        }
    }

    /// <summary>
    /// Applies the selected VSync setting.
    /// </summary>
    public void ApplyVsync(bool isEnabled)
    {
        QualitySettings.vSyncCount = isEnabled ? 1 : 0;
    }

    /// <summary>
    /// Applies the selected target frame rate.
    /// </summary>
    public void ApplyFrameRate(int optionIndex)
    {
        if (optionIndex < 0 || optionIndex >= frameRateOptions.Length)
        {
            return;
        }

        Application.targetFrameRate = frameRateOptions[optionIndex];
    }

    /// <summary>
    /// Applies the master volume slider to the AudioMixer.
    /// </summary>
    public void ApplyMasterVolume(float volume)
    {
        SetMixerVolume(masterVolumeParameter, volume);
    }

    /// <summary>
    /// Applies the music volume slider to the AudioMixer.
    /// </summary>
    public void ApplyMusicVolume(float volume)
    {
        SetMixerVolume(musicVolumeParameter, volume);
    }

    /// <summary>
    /// Applies the SFX volume slider to the AudioMixer.
    /// </summary>
    public void ApplySfxVolume(float volume)
    {
        SetMixerVolume(sfxVolumeParameter, volume);
    }

    /// <summary>
    /// Applies the UI SFX volume slider to the AudioMixer.
    /// </summary>
    public void ApplyUiVolume(float volume)
    {
        SetMixerVolume(uiVolumeParameter, volume);
    }

    /// <summary>
    /// Hides the settings panel instantly.
    /// </summary>
    private void HideImmediate()
    {
        EnsureCanvasGroup();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    /// <summary>
    /// Fades the settings panel in.
    /// </summary>
    private void Show()
    {
        FadePanel(1f, true);
    }

    /// <summary>
    /// Fades the settings panel out.
    /// </summary>
    private void Hide()
    {
        FadePanel(0f, false);
    }

    /// <summary>
    /// Fades the settings canvas group to the requested alpha.
    /// </summary>
    private void FadePanel(float targetAlpha, bool canInteract)
    {
        EnsureCanvasGroup();
        KillFadeTween();
        canvasGroup.interactable = canInteract;
        canvasGroup.blocksRaycasts = canInteract;
        fadeTween = canvasGroup.DOFade(targetAlpha, fadeDuration).SetEase(fadeEase).SetUpdate(true);
    }

    /// <summary>
    /// Builds video dropdown options from the current hardware state.
    /// </summary>
    private void SetupVideoControls()
    {
        SetupResolutionDropdown();
        SetupMonitorDropdown();
        SetupRenderModeDropdown();
        SetupVsyncToggle();
        SetupFrameRateDropdown();
    }

    /// <summary>
    /// Builds the resolution dropdown from available monitor resolutions.
    /// </summary>
    private void SetupResolutionDropdown()
    {
        if (resolutionDropdown == null)
        {
            return;
        }

        availableResolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();
        int selectedIndex = 0;

        for (int i = 0; i < availableResolutions.Length; i++)
        {
            Resolution resolution = availableResolutions[i];
            resolutionDropdown.options.Add(new TMP_Dropdown.OptionData($"{resolution.width} x {resolution.height}"));

            if (resolution.width == Screen.width && resolution.height == Screen.height)
            {
                selectedIndex = i;
            }
        }

        resolutionDropdown.SetValueWithoutNotify(selectedIndex);
        resolutionDropdown.onValueChanged.AddListener(ApplyResolution);
    }

    /// <summary>
    /// Builds the monitor dropdown from Unity display data.
    /// </summary>
    private void SetupMonitorDropdown()
    {
        if (monitorDropdown == null)
        {
            return;
        }

        monitorDropdown.ClearOptions();

        for (int i = 0; i < Display.displays.Length; i++)
        {
            monitorDropdown.options.Add(new TMP_Dropdown.OptionData($"Monitor {i + 1}"));
        }

        monitorDropdown.interactable = Display.displays.Length > 1;
        monitorDropdown.SetValueWithoutNotify(0);
        monitorDropdown.onValueChanged.AddListener(ApplyMonitor);
    }

    /// <summary>
    /// Builds the fullscreen mode dropdown and selects the current mode.
    /// </summary>
    private void SetupRenderModeDropdown()
    {
        if (renderModeDropdown == null)
        {
            return;
        }

        renderModeDropdown.ClearOptions();
        renderModeDropdown.options.Add(new TMP_Dropdown.OptionData("Fullscreen"));
        renderModeDropdown.options.Add(new TMP_Dropdown.OptionData("Borderless Window"));
        renderModeDropdown.options.Add(new TMP_Dropdown.OptionData("Windowed"));
        renderModeDropdown.SetValueWithoutNotify(GetCurrentRenderModeIndex());
        renderModeDropdown.onValueChanged.AddListener(ApplyRenderMode);
    }

    /// <summary>
    /// Initializes the VSync toggle with disabled as the default value.
    /// </summary>
    private void SetupVsyncToggle()
    {
        if (vsyncToggle == null)
        {
            return;
        }

        QualitySettings.vSyncCount = 0;
        vsyncToggle.SetIsOnWithoutNotify(false);
        vsyncToggle.onValueChanged.AddListener(ApplyVsync);
    }

    /// <summary>
    /// Builds the frame rate dropdown and defaults to 60 FPS.
    /// </summary>
    private void SetupFrameRateDropdown()
    {
        if (frameRateDropdown == null)
        {
            return;
        }

        frameRateDropdown.ClearOptions();
        frameRateDropdown.options.Add(new TMP_Dropdown.OptionData("30"));
        frameRateDropdown.options.Add(new TMP_Dropdown.OptionData("60"));
        frameRateDropdown.options.Add(new TMP_Dropdown.OptionData("120"));
        frameRateDropdown.options.Add(new TMP_Dropdown.OptionData("144"));
        frameRateDropdown.options.Add(new TMP_Dropdown.OptionData("Unlimited"));
        frameRateDropdown.SetValueWithoutNotify(1);
        Application.targetFrameRate = 60;
        frameRateDropdown.onValueChanged.AddListener(ApplyFrameRate);
    }

    /// <summary>
    /// Initializes audio sliders and hooks them to mixer parameters.
    /// </summary>
    private void SetupAudioControls()
    {
        SetupAudioSlider(masterVolumeSlider, ApplyMasterVolume);
        SetupAudioSlider(musicVolumeSlider, ApplyMusicVolume);
        SetupAudioSlider(sfxVolumeSlider, ApplySfxVolume);
        SetupAudioSlider(uiVolumeSlider, ApplyUiVolume);
    }

    /// <summary>
    /// Initializes one audio volume slider.
    /// </summary>
    private static void SetupAudioSlider(Slider slider, UnityEngine.Events.UnityAction<float> callback)
    {
        if (slider == null)
        {
            return;
        }

        slider.SetValueWithoutNotify(1f);
        callback.Invoke(1f);
        slider.onValueChanged.AddListener(callback);
    }

    /// <summary>
    /// Applies one normalized volume value to an exposed AudioMixer parameter.
    /// </summary>
    private void SetMixerVolume(string parameterName, float volume)
    {
        if (audioMixer == null || string.IsNullOrWhiteSpace(parameterName))
        {
            return;
        }

        audioMixer.SetFloat(parameterName, AudioMixerVolumeUtility.LinearToDecibels(volume));
    }

    /// <summary>
    /// Gets the dropdown index that matches the current screen mode.
    /// </summary>
    private static int GetCurrentRenderModeIndex()
    {
        if (Screen.fullScreenMode == FullScreenMode.Windowed)
        {
            return 2;
        }

        if (Screen.fullScreenMode == FullScreenMode.FullScreenWindow)
        {
            return 1;
        }

        return 0;
    }

    /// <summary>
    /// Enables one options group while hiding the others.
    /// </summary>
    private static void SetActiveGroup(GameObject group, bool isActive)
    {
        if (group != null)
        {
            group.SetActive(isActive);
        }
    }

    /// <summary>
    /// Updates the current settings section title.
    /// </summary>
    private void SetTitle(string title)
    {
        if (settingsTitleText != null)
        {
            settingsTitleText.text = title;
        }
    }

    /// <summary>
    /// Caches the same-object CanvasGroup if needed.
    /// </summary>
    private void EnsureCanvasGroup()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
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
