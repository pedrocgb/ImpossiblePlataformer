using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

public sealed class GameMusicPlayer : MonoBehaviour
{
    private enum PlaylistMode
    {
        InOrder,
        Random
    }

    [Title("Persistence")]
    [SerializeField]
    private bool dontDestroyOnLoad = true;

    [Title("Channel")]
    [SerializeField, Required]
    private GameAudioChannel musicChannel;

    [Title("Playlist")]
    [SerializeField]
    private AudioClip[] musicClips = new AudioClip[0];

    [SerializeField]
    private PlaylistMode playlistMode = PlaylistMode.InOrder;

    [SerializeField]
    private bool playOnStart = true;

    [Title("Fade")]
    [SerializeField, MinValue(0f)]
    private float fadeInDuration = 1f;

    [SerializeField, MinValue(0f)]
    private float fadeOutDuration = 1f;

    [SerializeField, Range(0f, 1f)]
    private float playbackVolume = 1f;

    [FoldoutGroup("Runtime Data")]
    [ShowInInspector, ReadOnly]
    private int currentMusicIndex = -1;

    private Coroutine playlistRoutine;
    private bool isStopping;

    /// <summary>
    /// Preserves this music player across scenes when configured.
    /// </summary>
    private void Awake()
    {
        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    /// <summary>
    /// Starts the playlist when configured to play automatically.
    /// </summary>
    private void Start()
    {
        if (playOnStart)
        {
            PlayPlaylist();
        }
    }

    /// <summary>
    /// Stops the playlist coroutine when this player is disabled.
    /// </summary>
    private void OnDisable()
    {
        StopPlaylistRoutine();
    }

    /// <summary>
    /// Starts or restarts the configured music playlist.
    /// </summary>
    [Button]
    public void PlayPlaylist()
    {
        if (musicChannel == null || musicClips == null || musicClips.Length == 0)
        {
            return;
        }

        isStopping = false;
        StopPlaylistRoutine();
        playlistRoutine = StartCoroutine(RunPlaylist());
    }

    /// <summary>
    /// Fades out and stops the music playlist.
    /// </summary>
    [Button]
    public void StopPlaylist()
    {
        if (playlistRoutine == null)
        {
            return;
        }

        isStopping = true;
    }

    /// <summary>
    /// Runs the playlist forever using either ordered or random track selection.
    /// </summary>
    private IEnumerator RunPlaylist()
    {
        while (!isStopping)
        {
            AudioClip nextClip = GetNextClip();

            if (nextClip == null)
            {
                yield break;
            }

            musicChannel.SetSourceVolume(0f);
            musicChannel.Play(nextClip);
            yield return FadeSourceVolume(0f, playbackVolume, fadeInDuration);
            yield return WaitForTrackBody(nextClip);
            yield return FadeSourceVolume(playbackVolume, 0f, fadeOutDuration);
            musicChannel.Stop();
        }

        yield return FadeSourceVolume(playbackVolume, 0f, fadeOutDuration);
        musicChannel.Stop();
        playlistRoutine = null;
    }

    /// <summary>
    /// Waits until the current track is ready to transition out.
    /// </summary>
    private IEnumerator WaitForTrackBody(AudioClip clip)
    {
        float waitTime = Mathf.Max(0f, clip.length - fadeInDuration - fadeOutDuration);
        float elapsed = 0f;

        while (elapsed < waitTime && !isStopping)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    /// <summary>
    /// Fades the music channel source volume over unscaled time.
    /// </summary>
    private IEnumerator FadeSourceVolume(float fromVolume, float toVolume, float duration)
    {
        if (musicChannel == null)
        {
            yield break;
        }

        if (duration <= 0f)
        {
            musicChannel.SetSourceVolume(toVolume);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float percent = Mathf.Clamp01(elapsed / duration);
            musicChannel.SetSourceVolume(Mathf.Lerp(fromVolume, toVolume, percent));
            yield return null;
        }

        musicChannel.SetSourceVolume(toVolume);
    }

    /// <summary>
    /// Selects the next clip from the playlist mode.
    /// </summary>
    private AudioClip GetNextClip()
    {
        if (musicClips == null || musicClips.Length == 0)
        {
            return null;
        }

        currentMusicIndex = playlistMode == PlaylistMode.Random ? GetRandomMusicIndex() : GetNextOrderedMusicIndex();
        return musicClips[currentMusicIndex];
    }

    /// <summary>
    /// Gets the next playlist index in order and wraps around at the end.
    /// </summary>
    private int GetNextOrderedMusicIndex()
    {
        return (currentMusicIndex + 1) % musicClips.Length;
    }

    /// <summary>
    /// Gets a random playlist index while avoiding immediate repeats when possible.
    /// </summary>
    private int GetRandomMusicIndex()
    {
        if (musicClips.Length == 1)
        {
            return 0;
        }

        int nextIndex = Random.Range(0, musicClips.Length);

        while (nextIndex == currentMusicIndex)
        {
            nextIndex = Random.Range(0, musicClips.Length);
        }

        return nextIndex;
    }

    /// <summary>
    /// Stops the active playlist coroutine when it exists.
    /// </summary>
    private void StopPlaylistRoutine()
    {
        if (playlistRoutine == null)
        {
            return;
        }

        StopCoroutine(playlistRoutine);
        playlistRoutine = null;
    }
}
