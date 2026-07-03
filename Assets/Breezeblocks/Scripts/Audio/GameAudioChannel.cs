using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Audio;

[DefaultExecutionOrder(-10000)]
[RequireComponent(typeof(AudioSource))]
public sealed class GameAudioChannel : MonoBehaviour
{
    [Title("Mixer")]
    [SerializeField]
    private AudioMixerGroup outputMixerGroup;

    [SerializeField]
    private string volumeParameter;

    private AudioSource audioSource;

    /// <summary>
    /// Caches the same-object AudioSource and applies saved mixer volume before audio playback starts.
    /// </summary>
    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        ApplySettings();
    }

    /// <summary>
    /// Reapplies saved mixer volume when this channel is enabled after scene load.
    /// </summary>
    private void OnEnable()
    {
        ApplySettings();
    }

    /// <summary>
    /// Reapplies saved mixer volumes after Unity finishes initializing audio objects.
    /// </summary>
    private void Start()
    {
        ApplySavedMixerVolumes();
    }

    /// <summary>
    /// Keeps mixer routing and saved mixer volume current when inspector values change during play mode.
    /// </summary>
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            ApplySettings();
        }
    }

    /// <summary>
    /// Plays a one-shot clip through this channel.
    /// </summary>
    public void PlayOneShot(AudioClip clip)
    {
        EnsureAudioSource();

        if (clip == null)
        {
            return;
        }

        audioSource.PlayOneShot(clip);
    }

    /// <summary>
    /// Starts looping music or ambience through this channel.
    /// </summary>
    public void PlayLoop(AudioClip clip)
    {
        EnsureAudioSource();

        if (clip == null)
        {
            return;
        }

        audioSource.clip = clip;
        audioSource.loop = true;
        audioSource.Play();
    }

    /// <summary>
    /// Starts playing a non-looping clip through this channel.
    /// </summary>
    public void Play(AudioClip clip)
    {
        EnsureAudioSource();

        if (clip == null)
        {
            return;
        }

        audioSource.clip = clip;
        audioSource.loop = false;
        audioSource.Play();
    }

    /// <summary>
    /// Stops this channel's current looping clip.
    /// </summary>
    public void Stop()
    {
        EnsureAudioSource();
        audioSource.Stop();
        audioSource.clip = null;
    }

    /// <summary>
    /// Gets whether this channel is currently playing audio.
    /// </summary>
    public bool IsPlaying()
    {
        EnsureAudioSource();
        return audioSource.isPlaying;
    }

    /// <summary>
    /// Gets the current clip length on this channel.
    /// </summary>
    public float GetCurrentClipLength()
    {
        EnsureAudioSource();
        return audioSource.clip != null ? audioSource.clip.length : 0f;
    }

    /// <summary>
    /// Applies mixer routing without changing any runtime volume.
    /// </summary>
    public void ApplySettings()
    {
        EnsureAudioSource();
        audioSource.outputAudioMixerGroup = outputMixerGroup;
        ApplySavedMixerVolumes();
    }

    /// <summary>
    /// Caches the same-object AudioSource when another method runs before Awake.
    /// </summary>
    private void EnsureAudioSource()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    /// <summary>
    /// Applies saved volumes to every known exposed AudioMixer parameter on this channel's mixer.
    /// </summary>
    private void ApplySavedMixerVolumes()
    {
        if (outputMixerGroup == null || outputMixerGroup.audioMixer == null)
        {
            return;
        }

        GameSaveSystem.ApplyAudioSettings(
            outputMixerGroup.audioMixer,
            "MasterVolume",
            "MusicVolume",
            "SFXVolume",
            "UIVolume");

        if (!string.IsNullOrWhiteSpace(volumeParameter))
        {
            GameSaveSystem.ApplySavedAudioMixerParameter(outputMixerGroup.audioMixer, volumeParameter);
        }
    }
}
