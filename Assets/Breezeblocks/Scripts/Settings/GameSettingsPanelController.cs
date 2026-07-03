using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public sealed class GameSettingsPanelController : MonoBehaviour
{
    private const string VideoTitleKey = "settings.title.video";
    private const string AudioTitleKey = "settings.title.audio";
    private const string ControlsTitleKey = "settings.title.controls";
    private const string GameplayTitleKey = "settings.title.gameplay";
    private const string MonitorFormatKey = "settings.monitorFormat";
    private const string FullscreenRenderModeKey = "settings.renderMode.fullscreen";
    private const string BorderlessRenderModeKey = "settings.renderMode.borderless";
    private const string WindowedRenderModeKey = "settings.renderMode.windowed";
    private const string UnlimitedFrameRateKey = "settings.frameRate.unlimited";
    private const string PortugueseLanguageKey = "settings.language.ptBr";
    private const string EnglishLanguageKey = "settings.language.enUs";

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
    private GameObject gameplayOptionsRoot;

    [SerializeField]
    private RewiredControlMapperSettingsBridge controlsBridge;

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

    [Title("Gameplay")]
    [SerializeField]
    private TMP_Dropdown languageDropdown;

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
    private LocalizedText settingsTitleLocalizedText;
    private Tween fadeTween;
    private Resolution[] availableResolutions = new Resolution[0];
    private bool isOpen;
    private bool isApplyingSavedSettings;

    /// <summary>
    /// Gets whether the settings panel is currently open for player interaction.
    /// </summary>
    public bool IsOpen => isOpen;

    /// <summary>
    /// Caches the same-object CanvasGroup used by the settings panel.
    /// </summary>
    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        settingsTitleLocalizedText = settingsTitleText != null ? settingsTitleText.GetComponent<LocalizedText>() : null;
        GameLocalization.LanguageChanged += RefreshLocalizedText;
        SetupVideoControls();
        SetupGameplayControls();
        SetupAudioControls();
        LoadAndApplySavedSettings();
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
        GameLocalization.LanguageChanged -= RefreshLocalizedText;
        RemoveControlListeners();
    }

    /// <summary>
    /// Opens the settings panel and defaults to the video tab.
    /// </summary>
    public void Open()
    {
        LoadAndApplySavedSettings();
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
    /// Gives settings first chance to consume the PauseMenu input before the pause menu toggles.
    /// </summary>
    public bool HandlePauseMenuInput()
    {
        if (controlsBridge != null && controlsBridge.CancelPendingBinding())
        {
            return true;
        }

        if (!isOpen)
        {
            return false;
        }

        controlsBridge?.CloseMapperIfValid();
        Hide();
        return true;
    }

    /// <summary>
    /// Shows the video options group.
    /// </summary>
    public void ShowVideoSettings()
    {
        SetActiveGroup(videoOptionsRoot, true);
        SetActiveGroup(audioOptionsRoot, false);
        SetActiveGroup(controlsOptionsRoot, false);
        SetActiveGroup(gameplayOptionsRoot, false);
        SetTitle(VideoTitleKey, "Video");
    }

    /// <summary>
    /// Shows the audio options group.
    /// </summary>
    public void ShowAudioSettings()
    {
        SetActiveGroup(videoOptionsRoot, false);
        SetActiveGroup(audioOptionsRoot, true);
        SetActiveGroup(controlsOptionsRoot, false);
        SetActiveGroup(gameplayOptionsRoot, false);
        SetTitle(AudioTitleKey, "Audio");
    }

    /// <summary>
    /// Shows the controls options group and opens the Rewired mapper when assigned.
    /// </summary>
    public void ShowControlsSettings()
    {
        SetActiveGroup(videoOptionsRoot, false);
        SetActiveGroup(audioOptionsRoot, false);
        SetActiveGroup(controlsOptionsRoot, true);
        SetActiveGroup(gameplayOptionsRoot, false);
        SetTitle(ControlsTitleKey, "Controls");
        controlsBridge?.OpenMapper();
    }

    /// <summary>
    /// Shows the gameplay options group.
    /// </summary>
    public void ShowGameplaySettings()
    {
        SetActiveGroup(videoOptionsRoot, false);
        SetActiveGroup(audioOptionsRoot, false);
        SetActiveGroup(controlsOptionsRoot, false);
        SetActiveGroup(gameplayOptionsRoot, true);
        SetTitle(GameplayTitleKey, "Gameplay");
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
        SaveVideoSettingsIfNeeded();
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

        SaveVideoSettingsIfNeeded();
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

        SaveVideoSettingsIfNeeded();
    }

    /// <summary>
    /// Applies the selected VSync setting.
    /// </summary>
    public void ApplyVsync(bool isEnabled)
    {
        QualitySettings.vSyncCount = isEnabled ? 1 : 0;
        SaveVideoSettingsIfNeeded();
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
        SaveVideoSettingsIfNeeded();
    }

    /// <summary>
    /// Applies the selected gameplay language and saves it immediately.
    /// </summary>
    public void ApplyLanguage(int optionIndex)
    {
        GameLocalization.SetLanguage(optionIndex == 1 ? GameLanguage.EnUs : GameLanguage.PtBr);
    }

    /// <summary>
    /// Applies the master volume slider to the AudioMixer.
    /// </summary>
    public void ApplyMasterVolume(float volume)
    {
        SetMixerVolume(masterVolumeParameter, volume);
        SaveAudioSettingsIfNeeded();
    }

    /// <summary>
    /// Applies the music volume slider to the AudioMixer.
    /// </summary>
    public void ApplyMusicVolume(float volume)
    {
        SetMixerVolume(musicVolumeParameter, volume);
        SaveAudioSettingsIfNeeded();
    }

    /// <summary>
    /// Applies the SFX volume slider to the AudioMixer.
    /// </summary>
    public void ApplySfxVolume(float volume)
    {
        SetMixerVolume(sfxVolumeParameter, volume);
        SaveAudioSettingsIfNeeded();
    }

    /// <summary>
    /// Applies the UI SFX volume slider to the AudioMixer.
    /// </summary>
    public void ApplyUiVolume(float volume)
    {
        SetMixerVolume(uiVolumeParameter, volume);
        SaveAudioSettingsIfNeeded();
    }

    /// <summary>
    /// Loads saved settings, applies them, and synchronizes the visible UI fields.
    /// </summary>
    private void LoadAndApplySavedSettings()
    {
        PlayerSettingsSaveData settings = GameSaveSystem.LoadSettings();
        isApplyingSavedSettings = true;
        ApplySavedVideoSettings(settings.Video);
        ApplySavedAudioSettings(settings.Audio);
        isApplyingSavedSettings = false;
    }

    /// <summary>
    /// Applies saved video settings to Unity and to the dropdown or toggle controls.
    /// </summary>
    private void ApplySavedVideoSettings(VideoSettingsSaveData videoSettings)
    {
        if (videoSettings == null || !videoSettings.HasSavedSettings)
        {
            return;
        }

        int renderModeIndex = Mathf.Clamp(videoSettings.RenderModeIndex, 0, 2);
        SetDropdownValue(renderModeDropdown, renderModeIndex);
        ApplyRenderMode(renderModeIndex);

        int resolutionIndex = GetSavedResolutionIndex(videoSettings);
        SetDropdownValue(resolutionDropdown, resolutionIndex);
        ApplyResolution(resolutionIndex);

        int monitorIndex = Mathf.Clamp(videoSettings.MonitorIndex, 0, Mathf.Max(0, Display.displays.Length - 1));
        SetDropdownValue(monitorDropdown, monitorIndex);
        ApplyMonitor(monitorIndex);

        int frameRateIndex = Mathf.Clamp(videoSettings.FrameRateOptionIndex, 0, frameRateOptions.Length - 1);
        SetDropdownValue(frameRateDropdown, frameRateIndex);
        ApplyFrameRate(frameRateIndex);

        if (vsyncToggle != null)
        {
            vsyncToggle.SetIsOnWithoutNotify(videoSettings.VsyncEnabled);
        }

        ApplyVsync(videoSettings.VsyncEnabled);
    }

    /// <summary>
    /// Applies saved audio values to the sliders and AudioMixer.
    /// </summary>
    private void ApplySavedAudioSettings(AudioSettingsSaveData audioSettings)
    {
        if (audioSettings == null)
        {
            return;
        }

        SetSliderValue(masterVolumeSlider, audioSettings.MasterVolume);
        SetSliderValue(musicVolumeSlider, audioSettings.MusicVolume);
        SetSliderValue(sfxVolumeSlider, audioSettings.SfxVolume);
        SetSliderValue(uiVolumeSlider, audioSettings.UiVolume);
        GameSaveSystem.ApplyAudioSettings(
            audioMixer,
            audioSettings,
            masterVolumeParameter,
            musicVolumeParameter,
            sfxVolumeParameter,
            uiVolumeParameter);
    }

    /// <summary>
    /// Saves current video control values when they came from player input.
    /// </summary>
    private void SaveVideoSettingsIfNeeded()
    {
        if (isApplyingSavedSettings)
        {
            return;
        }

        GameSaveSystem.SaveVideoSettings(CaptureVideoSettings());
    }

    /// <summary>
    /// Saves current audio control values when they came from player input.
    /// </summary>
    private void SaveAudioSettingsIfNeeded()
    {
        if (isApplyingSavedSettings)
        {
            return;
        }

        GameSaveSystem.SaveAudioSettings(CaptureAudioSettings());
    }

    /// <summary>
    /// Captures current video UI values into save data.
    /// </summary>
    private VideoSettingsSaveData CaptureVideoSettings()
    {
        VideoSettingsSaveData videoSettings = new VideoSettingsSaveData
        {
            HasSavedSettings = true,
            MonitorIndex = GetDropdownValue(monitorDropdown),
            RenderModeIndex = GetDropdownValue(renderModeDropdown),
            VsyncEnabled = vsyncToggle != null && vsyncToggle.isOn,
            FrameRateOptionIndex = GetDropdownValue(frameRateDropdown)
        };

        int resolutionIndex = GetDropdownValue(resolutionDropdown);

        if (resolutionIndex >= 0 && resolutionIndex < availableResolutions.Length)
        {
            Resolution resolution = availableResolutions[resolutionIndex];
            videoSettings.HasSavedResolution = true;
            videoSettings.ResolutionWidth = resolution.width;
            videoSettings.ResolutionHeight = resolution.height;
        }

        return videoSettings;
    }

    /// <summary>
    /// Captures current audio slider values into save data.
    /// </summary>
    private AudioSettingsSaveData CaptureAudioSettings()
    {
        return new AudioSettingsSaveData
        {
            MasterVolume = GetSliderValue(masterVolumeSlider),
            MusicVolume = GetSliderValue(musicVolumeSlider),
            SfxVolume = GetSliderValue(sfxVolumeSlider),
            UiVolume = GetSliderValue(uiVolumeSlider)
        };
    }

    /// <summary>
    /// Hides the settings panel instantly.
    /// </summary>
    private void HideImmediate()
    {
        EnsureCanvasGroup();
        isOpen = false;
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    /// <summary>
    /// Fades the settings panel in.
    /// </summary>
    private void Show()
    {
        isOpen = true;
        FadePanel(1f, true);
    }

    /// <summary>
    /// Fades the settings panel out.
    /// </summary>
    private void Hide()
    {
        isOpen = false;
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
    /// Builds gameplay dropdown options and synchronizes language selection.
    /// </summary>
    private void SetupGameplayControls()
    {
        SetupLanguageDropdown();
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
            monitorDropdown.options.Add(new TMP_Dropdown.OptionData(GameLocalization.Format(MonitorFormatKey, "Monitor {0}", i + 1)));
        }

        monitorDropdown.interactable = Display.displays.Length > 1;
        monitorDropdown.SetValueWithoutNotify(0);
        monitorDropdown.RefreshShownValue();
        SetDropdownCaptionFallback(monitorDropdown);
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
        renderModeDropdown.options.Add(new TMP_Dropdown.OptionData(GameLocalization.Get(FullscreenRenderModeKey, "Fullscreen")));
        renderModeDropdown.options.Add(new TMP_Dropdown.OptionData(GameLocalization.Get(BorderlessRenderModeKey, "Borderless Window")));
        renderModeDropdown.options.Add(new TMP_Dropdown.OptionData(GameLocalization.Get(WindowedRenderModeKey, "Windowed")));
        renderModeDropdown.SetValueWithoutNotify(GetCurrentRenderModeIndex());
        renderModeDropdown.RefreshShownValue();
        SetDropdownCaptionFallback(renderModeDropdown);
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
        frameRateDropdown.options.Add(new TMP_Dropdown.OptionData(GameLocalization.Get(UnlimitedFrameRateKey, "Unlimited")));
        frameRateDropdown.SetValueWithoutNotify(1);
        frameRateDropdown.RefreshShownValue();
        SetDropdownCaptionFallback(frameRateDropdown);
        Application.targetFrameRate = 60;
        frameRateDropdown.onValueChanged.AddListener(ApplyFrameRate);
    }

    /// <summary>
    /// Builds the language dropdown and selects the saved language.
    /// </summary>
    private void SetupLanguageDropdown()
    {
        if (languageDropdown == null)
        {
            return;
        }

        languageDropdown.ClearOptions();
        languageDropdown.options.Add(new TMP_Dropdown.OptionData(GameLocalization.Get(PortugueseLanguageKey, "PT-BR")));
        languageDropdown.options.Add(new TMP_Dropdown.OptionData(GameLocalization.Get(EnglishLanguageKey, "EN-US")));
        languageDropdown.SetValueWithoutNotify(GetCurrentLanguageIndex());
        languageDropdown.onValueChanged.AddListener(ApplyLanguage);
        languageDropdown.RefreshShownValue();
        SetDropdownCaptionFallback(languageDropdown);
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
    /// Finds the saved resolution in the current hardware list, falling back to the current screen size.
    /// </summary>
    private int GetSavedResolutionIndex(VideoSettingsSaveData videoSettings)
    {
        if (availableResolutions.Length == 0)
        {
            return 0;
        }

        if (videoSettings != null && videoSettings.HasSavedResolution)
        {
            int savedIndex = FindResolutionIndex(videoSettings.ResolutionWidth, videoSettings.ResolutionHeight);

            if (savedIndex >= 0)
            {
                return savedIndex;
            }
        }

        int currentIndex = FindResolutionIndex(Screen.width, Screen.height);
        return currentIndex >= 0 ? currentIndex : 0;
    }

    /// <summary>
    /// Finds a resolution option by width and height.
    /// </summary>
    private int FindResolutionIndex(int width, int height)
    {
        for (int i = 0; i < availableResolutions.Length; i++)
        {
            Resolution resolution = availableResolutions[i];

            if (resolution.width == width && resolution.height == height)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Sets a dropdown value without firing change callbacks.
    /// </summary>
    private static void SetDropdownValue(TMP_Dropdown dropdown, int value)
    {
        if (dropdown == null)
        {
            return;
        }

        int optionCount = dropdown.options.Count;

        if (optionCount <= 0)
        {
            return;
        }

        dropdown.SetValueWithoutNotify(Mathf.Clamp(value, 0, optionCount - 1));
        dropdown.RefreshShownValue();
        SetDropdownCaptionFallback(dropdown);
    }

    /// <summary>
    /// Gets a dropdown value, returning zero when the dropdown is missing.
    /// </summary>
    private static int GetDropdownValue(TMP_Dropdown dropdown)
    {
        return dropdown != null ? dropdown.value : 0;
    }

    /// <summary>
    /// Sets a slider value without firing change callbacks.
    /// </summary>
    private static void SetSliderValue(Slider slider, float value)
    {
        if (slider != null)
        {
            slider.SetValueWithoutNotify(Mathf.Clamp01(value));
        }
    }

    /// <summary>
    /// Gets a slider value, returning full volume when the slider is missing.
    /// </summary>
    private static float GetSliderValue(Slider slider)
    {
        return slider != null ? Mathf.Clamp01(slider.value) : 1f;
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
    private void SetTitle(string titleKey, string fallback)
    {
        if (settingsTitleLocalizedText != null)
        {
            settingsTitleLocalizedText.SetKey(titleKey, fallback);
            return;
        }

        if (settingsTitleText != null)
        {
            settingsTitleText.text = GameLocalization.Get(titleKey, fallback);
        }
    }

    /// <summary>
    /// Refreshes settings labels that depend on the active language.
    /// </summary>
    private void RefreshLocalizedText()
    {
        RefreshMonitorDropdownLabels();
        RefreshRenderModeDropdownLabels();
        RefreshFrameRateDropdownLabels();
        RefreshLanguageDropdownLabels();
        RefreshCurrentTitle();
    }

    /// <summary>
    /// Updates monitor dropdown option labels without changing the selected display.
    /// </summary>
    private void RefreshMonitorDropdownLabels()
    {
        if (monitorDropdown == null)
        {
            return;
        }

        for (int i = 0; i < monitorDropdown.options.Count; i++)
        {
            monitorDropdown.options[i].text = GameLocalization.Format(MonitorFormatKey, "Monitor {0}", i + 1);
        }

        monitorDropdown.RefreshShownValue();
        SetDropdownCaptionFallback(monitorDropdown);
    }

    /// <summary>
    /// Updates render mode dropdown option labels without changing the selected mode.
    /// </summary>
    private void RefreshRenderModeDropdownLabels()
    {
        if (renderModeDropdown == null || renderModeDropdown.options.Count < 3)
        {
            return;
        }

        renderModeDropdown.options[0].text = GameLocalization.Get(FullscreenRenderModeKey, "Fullscreen");
        renderModeDropdown.options[1].text = GameLocalization.Get(BorderlessRenderModeKey, "Borderless Window");
        renderModeDropdown.options[2].text = GameLocalization.Get(WindowedRenderModeKey, "Windowed");
        renderModeDropdown.RefreshShownValue();
        SetDropdownCaptionFallback(renderModeDropdown);
    }

    /// <summary>
    /// Updates frame rate dropdown labels that are language dependent.
    /// </summary>
    private void RefreshFrameRateDropdownLabels()
    {
        if (frameRateDropdown == null || frameRateDropdown.options.Count < 5)
        {
            return;
        }

        frameRateDropdown.options[4].text = GameLocalization.Get(UnlimitedFrameRateKey, "Unlimited");
        frameRateDropdown.RefreshShownValue();
        SetDropdownCaptionFallback(frameRateDropdown);
    }

    /// <summary>
    /// Updates language dropdown labels and selected value after a language change.
    /// </summary>
    private void RefreshLanguageDropdownLabels()
    {
        if (languageDropdown == null || languageDropdown.options.Count < 2)
        {
            return;
        }

        languageDropdown.options[0].text = GameLocalization.Get(PortugueseLanguageKey, "PT-BR");
        languageDropdown.options[1].text = GameLocalization.Get(EnglishLanguageKey, "EN-US");
        languageDropdown.SetValueWithoutNotify(GetCurrentLanguageIndex());
        languageDropdown.RefreshShownValue();
        SetDropdownCaptionFallback(languageDropdown);
    }

    /// <summary>
    /// Updates the settings section title to match the currently visible options group.
    /// </summary>
    private void RefreshCurrentTitle()
    {
        if (videoOptionsRoot != null && videoOptionsRoot.activeSelf)
        {
            SetTitle(VideoTitleKey, "Video");
            return;
        }

        if (audioOptionsRoot != null && audioOptionsRoot.activeSelf)
        {
            SetTitle(AudioTitleKey, "Audio");
            return;
        }

        if (controlsOptionsRoot != null && controlsOptionsRoot.activeSelf)
        {
            SetTitle(ControlsTitleKey, "Controls");
            return;
        }

        if (gameplayOptionsRoot != null && gameplayOptionsRoot.activeSelf)
        {
            SetTitle(GameplayTitleKey, "Gameplay");
        }
    }

    /// <summary>
    /// Gets the dropdown index that matches the current localization language.
    /// </summary>
    private static int GetCurrentLanguageIndex()
    {
        return GameLocalization.CurrentLanguage == GameLanguage.EnUs ? 1 : 0;
    }

    /// <summary>
    /// Keeps dynamic dropdown caption text from being overwritten by a static prefab localization key.
    /// </summary>
    private static void SetDropdownCaptionFallback(TMP_Dropdown dropdown)
    {
        if (dropdown == null || dropdown.captionText == null)
        {
            return;
        }

        LocalizedText localizedText = dropdown.captionText.GetComponent<LocalizedText>();

        if (localizedText != null)
        {
            localizedText.SetKey(string.Empty, dropdown.captionText.text);
        }
    }

    /// <summary>
    /// Removes runtime listeners created for settings UI controls.
    /// </summary>
    private void RemoveControlListeners()
    {
        if (languageDropdown != null)
        {
            languageDropdown.onValueChanged.RemoveListener(ApplyLanguage);
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
