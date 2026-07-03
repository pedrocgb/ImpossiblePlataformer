using System;
using System.IO;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public static class GameSaveSystem
{
    private const string SaveFileName = "player-save.json";
    private const int CurrentVersion = 1;

    private static GameSaveData cachedSaveData;

    /// <summary>
    /// Applies settings that do not need scene references before the first scene starts.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplySavedSettingsBeforeSceneLoad()
    {
        ApplyVideoSettings(LoadSettings().Video);
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    /// <summary>
    /// Applies saved mixer values once the first scene has loaded but before normal Start methods run.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplySavedAudioAfterSceneLoad()
    {
        ApplyAudioSettingsToLoadedMixers();
    }

    /// <summary>
    /// Applies saved mixer values whenever a later scene loads.
    /// </summary>
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyAudioSettingsToLoadedMixers();
    }

    /// <summary>
    /// Loads the whole save file, creating defaults when no valid file exists.
    /// </summary>
    public static GameSaveData Load()
    {
        if (cachedSaveData != null)
        {
            return cachedSaveData;
        }

        cachedSaveData = ReadSaveFile();
        EnsureSaveDefaults(cachedSaveData);
        return cachedSaveData;
    }

    /// <summary>
    /// Writes the current cached save data to disk.
    /// </summary>
    public static void Save()
    {
        Save(Load());
    }

    /// <summary>
    /// Writes the supplied save data to disk and caches it for later reads.
    /// </summary>
    public static void Save(GameSaveData saveData)
    {
        if (saveData == null)
        {
            return;
        }

        EnsureSaveDefaults(saveData);
        cachedSaveData = saveData;
        string json = JsonUtility.ToJson(cachedSaveData, true);
        string savePath = GetSavePath();
        string saveDirectory = Path.GetDirectoryName(savePath);

        if (!string.IsNullOrEmpty(saveDirectory))
        {
            Directory.CreateDirectory(saveDirectory);
        }

        string tempPath = savePath + ".tmp";
        File.WriteAllText(tempPath, json);

        if (File.Exists(savePath))
        {
            File.Delete(savePath);
        }

        File.Move(tempPath, savePath);
    }

    /// <summary>
    /// Loads the saved all-time death count.
    /// </summary>
    public static int LoadTotalDeaths()
    {
        return Mathf.Max(0, Load().TotalDeaths);
    }

    /// <summary>
    /// Saves the all-time death count into the project save file.
    /// </summary>
    public static void SaveTotalDeaths(int totalDeaths)
    {
        GameSaveData saveData = Load();
        saveData.TotalDeaths = Mathf.Max(0, totalDeaths);
        Save(saveData);
    }

    /// <summary>
    /// Clears the saved all-time death count while keeping settings intact.
    /// </summary>
    public static void ClearTotalDeaths()
    {
        SaveTotalDeaths(0);
    }

    /// <summary>
    /// Loads saved level progression after ensuring build-settings levels exist.
    /// </summary>
    public static LevelSaveData[] LoadLevelStates()
    {
        GameSaveData saveData = Load();
        EnsureLevelDefaults(saveData);
        return saveData.Levels;
    }

    /// <summary>
    /// Checks whether a level build index is unlocked for play.
    /// </summary>
    public static bool IsLevelUnlocked(int buildIndex)
    {
        LevelSaveData levelState = GetLevelState(buildIndex);
        return levelState != null && levelState.IsUnlocked;
    }

    /// <summary>
    /// Loads the saved all-time death count for one level.
    /// </summary>
    public static int LoadLevelDeaths(int buildIndex)
    {
        LevelSaveData levelState = GetLevelState(buildIndex);
        return levelState == null ? 0 : Mathf.Max(0, levelState.TotalDeaths);
    }

    /// <summary>
    /// Adds one death to a level's all-time death count and saves it.
    /// </summary>
    public static int AddLevelDeath(int buildIndex)
    {
        GameSaveData saveData = Load();
        LevelSaveData levelState = GetOrCreateLevelState(saveData, buildIndex);

        if (levelState == null)
        {
            return 0;
        }

        levelState.TotalDeaths = Mathf.Max(0, levelState.TotalDeaths) + 1;
        Save(saveData);
        return levelState.TotalDeaths;
    }

    /// <summary>
    /// Unlocks a level build index and saves progression.
    /// </summary>
    public static void UnlockLevel(int buildIndex)
    {
        GameSaveData saveData = Load();
        LevelSaveData levelState = GetOrCreateLevelState(saveData, buildIndex);

        if (levelState == null || levelState.IsUnlocked)
        {
            return;
        }

        levelState.IsUnlocked = true;
        Save(saveData);
    }

    /// <summary>
    /// Unlocks the next build-settings level after a completed level when one exists.
    /// </summary>
    public static void UnlockNextLevel(int completedBuildIndex)
    {
        int nextBuildIndex = completedBuildIndex + 1;

        if (nextBuildIndex >= SceneManager.sceneCountInBuildSettings)
        {
            return;
        }

        UnlockLevel(nextBuildIndex);
    }

    /// <summary>
    /// Loads all saved player settings.
    /// </summary>
    public static PlayerSettingsSaveData LoadSettings()
    {
        GameSaveData saveData = Load();
        EnsureSettingsDefaults(saveData.Settings);
        return saveData.Settings;
    }

    /// <summary>
    /// Saves video settings and applies them to global Unity state.
    /// </summary>
    public static void SaveVideoSettings(VideoSettingsSaveData videoSettings)
    {
        GameSaveData saveData = Load();
        saveData.Settings.Video = videoSettings ?? new VideoSettingsSaveData();
        Save(saveData);
        ApplyVideoSettings(saveData.Settings.Video);
    }

    /// <summary>
    /// Saves audio settings into the project save file.
    /// </summary>
    public static void SaveAudioSettings(AudioSettingsSaveData audioSettings)
    {
        GameSaveData saveData = Load();
        saveData.Settings.Audio = audioSettings ?? new AudioSettingsSaveData();
        Save(saveData);
    }

    /// <summary>
    /// Applies saved audio values to exposed AudioMixer parameters.
    /// </summary>
    public static void ApplyAudioSettings(
        AudioMixer audioMixer,
        string masterVolumeParameter,
        string musicVolumeParameter,
        string sfxVolumeParameter,
        string uiVolumeParameter)
    {
        ApplyAudioSettings(
            audioMixer,
            LoadSettings().Audio,
            masterVolumeParameter,
            musicVolumeParameter,
            sfxVolumeParameter,
            uiVolumeParameter);
    }

    /// <summary>
    /// Applies supplied audio values to exposed AudioMixer parameters.
    /// </summary>
    public static void ApplyAudioSettings(
        AudioMixer audioMixer,
        AudioSettingsSaveData audioSettings,
        string masterVolumeParameter,
        string musicVolumeParameter,
        string sfxVolumeParameter,
        string uiVolumeParameter)
    {
        if (audioMixer == null || audioSettings == null)
        {
            return;
        }

        ApplyMixerVolume(audioMixer, masterVolumeParameter, audioSettings.MasterVolume);
        ApplyMixerVolume(audioMixer, musicVolumeParameter, audioSettings.MusicVolume);
        ApplyMixerVolume(audioMixer, sfxVolumeParameter, audioSettings.SfxVolume);
        ApplyMixerVolume(audioMixer, uiVolumeParameter, audioSettings.UiVolume);
    }

    /// <summary>
    /// Applies saved audio settings to every loaded AudioMixer asset.
    /// </summary>
    public static void ApplyAudioSettingsToLoadedMixers()
    {
        AudioSettingsSaveData audioSettings = LoadSettings().Audio;
        AudioMixer[] audioMixers = Resources.FindObjectsOfTypeAll<AudioMixer>();

        for (int i = 0; i < audioMixers.Length; i++)
        {
            ApplyAudioSettings(
                audioMixers[i],
                audioSettings,
                "MasterVolume",
                "MusicVolume",
                "SFXVolume",
                "UIVolume");
        }
    }

    /// <summary>
    /// Applies the saved value for one exposed AudioMixer parameter when it is part of player settings.
    /// </summary>
    public static bool ApplySavedAudioMixerParameter(AudioMixer audioMixer, string parameterName)
    {
        if (audioMixer == null || string.IsNullOrWhiteSpace(parameterName))
        {
            return false;
        }

        AudioSettingsSaveData audioSettings = LoadSettings().Audio;

        if (TryGetSavedVolume(parameterName, audioSettings, out float volume))
        {
            ApplyMixerVolume(audioMixer, parameterName, volume);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Saves custom keyboard bindings into the project save file.
    /// </summary>
    public static void SaveControlSettings(ControlSettingsSaveData controlSettings)
    {
        GameSaveData saveData = Load();
        saveData.Settings.Controls = controlSettings ?? new ControlSettingsSaveData();
        Save(saveData);
    }

    /// <summary>
    /// Loads the saved custom keyboard binding data.
    /// </summary>
    public static ControlSettingsSaveData LoadControlSettings()
    {
        PlayerSettingsSaveData settings = LoadSettings();
        EnsureControlDefaults(settings.Controls);
        return settings.Controls;
    }

    /// <summary>
    /// Applies saved video settings that are independent from scene objects.
    /// </summary>
    public static void ApplyVideoSettings(VideoSettingsSaveData videoSettings)
    {
        if (videoSettings == null || !videoSettings.HasSavedSettings)
        {
            return;
        }

        QualitySettings.vSyncCount = videoSettings.VsyncEnabled ? 1 : 0;
        Application.targetFrameRate = GetFrameRateFromOption(videoSettings.FrameRateOptionIndex);

        if (videoSettings.HasSavedResolution && videoSettings.ResolutionWidth > 0 && videoSettings.ResolutionHeight > 0)
        {
            Screen.SetResolution(
                videoSettings.ResolutionWidth,
                videoSettings.ResolutionHeight,
                GetFullScreenMode(videoSettings.RenderModeIndex));
            return;
        }

        Screen.fullScreenMode = GetFullScreenMode(videoSettings.RenderModeIndex);
    }

    /// <summary>
    /// Gets the project save path in Unity's persistent data folder.
    /// </summary>
    public static string GetSavePath()
    {
        return Path.Combine(Application.persistentDataPath, SaveFileName);
    }

    /// <summary>
    /// Reads and parses the save file from disk.
    /// </summary>
    private static GameSaveData ReadSaveFile()
    {
        string savePath = GetSavePath();

        if (!File.Exists(savePath))
        {
            return new GameSaveData();
        }

        try
        {
            string json = File.ReadAllText(savePath);
            GameSaveData saveData = JsonUtility.FromJson<GameSaveData>(json);
            return saveData ?? new GameSaveData();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Could not load save file at {savePath}: {exception.Message}");
            return new GameSaveData();
        }
    }

    /// <summary>
    /// Applies one normalized volume value to an exposed AudioMixer parameter.
    /// </summary>
    private static void ApplyMixerVolume(AudioMixer audioMixer, string parameterName, float volume)
    {
        if (audioMixer == null || string.IsNullOrWhiteSpace(parameterName))
        {
            return;
        }

        audioMixer.SetFloat(parameterName, AudioMixerVolumeUtility.LinearToDecibels(volume));
    }

    /// <summary>
    /// Gets the saved volume for a known exposed AudioMixer parameter.
    /// </summary>
    private static bool TryGetSavedVolume(string parameterName, AudioSettingsSaveData audioSettings, out float volume)
    {
        volume = 1f;

        if (audioSettings == null)
        {
            return false;
        }

        switch (parameterName)
        {
            case "MasterVolume":
                volume = audioSettings.MasterVolume;
                return true;
            case "MusicVolume":
                volume = audioSettings.MusicVolume;
                return true;
            case "SFXVolume":
                volume = audioSettings.SfxVolume;
                return true;
            case "UIVolume":
                volume = audioSettings.UiVolume;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Ensures the root save object has every nested data object.
    /// </summary>
    private static void EnsureSaveDefaults(GameSaveData saveData)
    {
        saveData.Version = CurrentVersion;
        saveData.TotalDeaths = Mathf.Max(0, saveData.TotalDeaths);
        EnsureLevelDefaults(saveData);
        saveData.Settings ??= new PlayerSettingsSaveData();
        EnsureSettingsDefaults(saveData.Settings);
    }

    /// <summary>
    /// Ensures every build-settings level has a saved state and level one starts unlocked.
    /// </summary>
    private static void EnsureLevelDefaults(GameSaveData saveData)
    {
        saveData.Levels ??= new LevelSaveData[0];
        int levelCount = SceneManager.sceneCountInBuildSettings;

        for (int buildIndex = 0; buildIndex < levelCount; buildIndex++)
        {
            LevelSaveData levelState = GetOrCreateLevelState(saveData, buildIndex);

            if (levelState == null)
            {
                continue;
            }

            levelState.ScenePath = GetScenePath(buildIndex);
            levelState.TotalDeaths = Mathf.Max(0, levelState.TotalDeaths);

            if (buildIndex == 0)
            {
                levelState.IsUnlocked = true;
            }
        }
    }

    /// <summary>
    /// Gets a saved level state from the active save data.
    /// </summary>
    private static LevelSaveData GetLevelState(int buildIndex)
    {
        GameSaveData saveData = Load();
        int levelIndex = FindLevelStateIndex(saveData.Levels, buildIndex);
        return levelIndex < 0 ? null : saveData.Levels[levelIndex];
    }

    /// <summary>
    /// Gets an existing level state or adds a default one for a valid build index.
    /// </summary>
    private static LevelSaveData GetOrCreateLevelState(GameSaveData saveData, int buildIndex)
    {
        if (saveData == null || buildIndex < 0 || buildIndex >= SceneManager.sceneCountInBuildSettings)
        {
            return null;
        }

        int levelIndex = FindLevelStateIndex(saveData.Levels, buildIndex);

        if (levelIndex >= 0)
        {
            return saveData.Levels[levelIndex];
        }

        LevelSaveData levelState = CreateDefaultLevelState(buildIndex);
        int oldLength = saveData.Levels.Length;
        Array.Resize(ref saveData.Levels, oldLength + 1);
        saveData.Levels[oldLength] = levelState;
        return levelState;
    }

    /// <summary>
    /// Finds a level state array index by build index.
    /// </summary>
    private static int FindLevelStateIndex(LevelSaveData[] levels, int buildIndex)
    {
        if (levels == null)
        {
            return -1;
        }

        for (int i = 0; i < levels.Length; i++)
        {
            if (levels[i] != null && levels[i].BuildIndex == buildIndex)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Creates a saved level state using the project's default lock rules.
    /// </summary>
    private static LevelSaveData CreateDefaultLevelState(int buildIndex)
    {
        return new LevelSaveData
        {
            BuildIndex = buildIndex,
            ScenePath = GetScenePath(buildIndex),
            IsUnlocked = buildIndex == 0,
            TotalDeaths = 0
        };
    }

    /// <summary>
    /// Gets a scene path safely from Build Settings.
    /// </summary>
    private static string GetScenePath(int buildIndex)
    {
        return SceneUtility.GetScenePathByBuildIndex(buildIndex) ?? string.Empty;
    }

    /// <summary>
    /// Ensures saved settings have every nested settings object.
    /// </summary>
    private static void EnsureSettingsDefaults(PlayerSettingsSaveData settings)
    {
        settings.Video ??= new VideoSettingsSaveData();
        settings.Audio ??= new AudioSettingsSaveData();
        settings.Controls ??= new ControlSettingsSaveData();
        EnsureAudioDefaults(settings.Audio);
        EnsureControlDefaults(settings.Controls);
    }

    /// <summary>
    /// Clamps saved audio values to the slider range.
    /// </summary>
    private static void EnsureAudioDefaults(AudioSettingsSaveData audioSettings)
    {
        audioSettings.MasterVolume = Mathf.Clamp01(audioSettings.MasterVolume);
        audioSettings.MusicVolume = Mathf.Clamp01(audioSettings.MusicVolume);
        audioSettings.SfxVolume = Mathf.Clamp01(audioSettings.SfxVolume);
        audioSettings.UiVolume = Mathf.Clamp01(audioSettings.UiVolume);
    }

    /// <summary>
    /// Ensures saved control metadata has stable values.
    /// </summary>
    private static void EnsureControlDefaults(ControlSettingsSaveData controlSettings)
    {
        controlSettings.MapCategory ??= "Default";
        controlSettings.MapLayout ??= "Default";
        controlSettings.KeyboardMapXml ??= string.Empty;
    }

    /// <summary>
    /// Converts a saved frame rate dropdown index to a target frame rate.
    /// </summary>
    private static int GetFrameRateFromOption(int frameRateOptionIndex)
    {
        switch (Mathf.Clamp(frameRateOptionIndex, 0, 4))
        {
            case 0:
                return 30;
            case 1:
                return 60;
            case 2:
                return 120;
            case 3:
                return 144;
            default:
                return -1;
        }
    }

    /// <summary>
    /// Converts a saved render mode dropdown index to Unity's fullscreen mode.
    /// </summary>
    private static FullScreenMode GetFullScreenMode(int renderModeIndex)
    {
        switch (Mathf.Clamp(renderModeIndex, 0, 2))
        {
            case 1:
                return FullScreenMode.FullScreenWindow;
            case 2:
                return FullScreenMode.Windowed;
            default:
                return FullScreenMode.ExclusiveFullScreen;
        }
    }
}
