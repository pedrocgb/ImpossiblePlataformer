using System;

/// <summary>
/// Stores all persistent player data written to the project save file.
/// </summary>
[Serializable]
public sealed class GameSaveData
{
    public int Version = 1;
    public int TotalDeaths;
    public LevelSaveData[] Levels = new LevelSaveData[0];
    public PlayerSettingsSaveData Settings = new PlayerSettingsSaveData();
}

/// <summary>
/// Stores progression and total death data for one build-settings level.
/// </summary>
[Serializable]
public sealed class LevelSaveData
{
    public int BuildIndex;
    public string ScenePath = string.Empty;
    public bool IsUnlocked;
    public int TotalDeaths;
}

/// <summary>
/// Stores every player-facing setting that must survive game restarts.
/// </summary>
[Serializable]
public sealed class PlayerSettingsSaveData
{
    public VideoSettingsSaveData Video = new VideoSettingsSaveData();
    public AudioSettingsSaveData Audio = new AudioSettingsSaveData();
    public ControlSettingsSaveData Controls = new ControlSettingsSaveData();
}

/// <summary>
/// Stores display and performance settings selected by the player.
/// </summary>
[Serializable]
public sealed class VideoSettingsSaveData
{
    public bool HasSavedSettings;
    public bool HasSavedResolution;
    public int ResolutionWidth;
    public int ResolutionHeight;
    public int MonitorIndex;
    public int RenderModeIndex;
    public bool VsyncEnabled;
    public int FrameRateOptionIndex = 1;
}

/// <summary>
/// Stores normalized mixer volumes selected by the player.
/// </summary>
[Serializable]
public sealed class AudioSettingsSaveData
{
    public float MasterVolume = 1f;
    public float MusicVolume = 1f;
    public float SfxVolume = 1f;
    public float UiVolume = 1f;
}

/// <summary>
/// Stores keyboard bindings from the custom Rewired mapper.
/// </summary>
[Serializable]
public sealed class ControlSettingsSaveData
{
    public int PlayerId;
    public string MapCategory = "Default";
    public string MapLayout = "Default";
    public string KeyboardMapXml = string.Empty;
}
