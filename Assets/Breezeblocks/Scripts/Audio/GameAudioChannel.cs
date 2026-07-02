using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Audio;

[RequireComponent(typeof(AudioSource))]
public sealed class GameAudioChannel : MonoBehaviour
{
    [Title("Mixer")]
    [SerializeField]
    private AudioMixerGroup outputMixerGroup;

    [SerializeField]
    private bool useAudioMixerVolumeParameter;

    [SerializeField, ShowIf(nameof(useAudioMixerVolumeParameter))]
    private AudioMixer audioMixer;

    [SerializeField, ShowIf(nameof(useAudioMixerVolumeParameter))]
    private string volumeParameter;

    [Title("Volume")]
    [SerializeField, Range(0f, 1f)]
    private float volume = 1f;

    private AudioSource audioSource;
    private float sourceVolumeMultiplier = 1f;

    /// <summary>
    /// Caches the same-object AudioSource and applies mixer routing.
    /// </summary>
    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        ApplySettings();
    }

    /// <summary>
    /// Keeps mixer volume current when inspector values change during play mode.
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
    public void PlayOneShot(AudioClip clip, float clipVolume = 1f)
    {
        EnsureAudioSource();

        if (clip == null)
        {
            return;
        }

        audioSource.PlayOneShot(clip, clipVolume);
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
    /// Sets the AudioSource volume used for fades without changing mixer volume.
    /// </summary>
    public void SetSourceVolume(float sourceVolume)
    {
        EnsureAudioSource();
        sourceVolumeMultiplier = Mathf.Clamp01(sourceVolume);
        ApplySourceVolume();
    }

    /// <summary>
    /// Applies the channel volume to either the AudioMixer parameter or AudioSource volume.
    /// </summary>
    public void ApplySettings()
    {
        EnsureAudioSource();
        audioSource.outputAudioMixerGroup = outputMixerGroup;

        if (useAudioMixerVolumeParameter && audioMixer != null && !string.IsNullOrWhiteSpace(volumeParameter))
        {
            audioMixer.SetFloat(volumeParameter, AudioMixerVolumeUtility.LinearToDecibels(volume));
            ApplySourceVolume();
            return;
        }

        ApplySourceVolume();
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
    /// Applies the AudioSource volume while respecting mixer-driven channel volume.
    /// </summary>
    private void ApplySourceVolume()
    {
        if (useAudioMixerVolumeParameter && audioMixer != null && !string.IsNullOrWhiteSpace(volumeParameter))
        {
            audioSource.volume = sourceVolumeMultiplier;
            return;
        }

        audioSource.volume = volume * sourceVolumeMultiplier;
    }

}
