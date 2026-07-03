using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Audio;

[DefaultExecutionOrder(-10000)]
public sealed class SavedAudioMixerSettingsApplier : MonoBehaviour
{
    [Title("Audio Mixer")]
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

    /// <summary>
    /// Applies saved mixer volumes before gameplay audio starts using scene audio references.
    /// </summary>
    private void Awake()
    {
        ApplySavedAudioSettings();
    }

    /// <summary>
    /// Applies saved mixer volumes through the configured exposed parameters.
    /// </summary>
    [Button]
    public void ApplySavedAudioSettings()
    {
        GameSaveSystem.ApplyAudioSettings(
            audioMixer,
            masterVolumeParameter,
            musicVolumeParameter,
            sfxVolumeParameter,
            uiVolumeParameter);
    }
}
