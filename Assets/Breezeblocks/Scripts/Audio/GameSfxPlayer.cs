using Sirenix.OdinInspector;
using UnityEngine;

public sealed class GameSfxPlayer : MonoBehaviour
{
    public static GameSfxPlayer Current { get; private set; }

    [Title("Persistence")]
    [SerializeField]
    private bool dontDestroyOnLoad = true;

    [Title("Channels")]
    [SerializeField]
    private GameAudioChannel sfxChannel;

    [SerializeField]
    private GameAudioChannel uiChannel;

    [Title("Clips")]
    [SerializeField]
    private AudioClip[] playerDeathClips = new AudioClip[0];

    [SerializeField]
    private AudioClip playerJumpClip;

    [SerializeField]
    private AudioClip winZoneClip;

    [SerializeField]
    private AudioClip uiButtonClickClip;

    /// <summary>
    /// Registers the active SFX player and applies channel settings.
    /// </summary>
    private void Awake()
    {
        if (Current != null && Current != this)
        {
            Destroy(gameObject);
            return;
        }

        Current = this;
        ApplyChannelSettings();

        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    /// <summary>
    /// Clears the active SFX player reference when this object is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

    /// <summary>
    /// Plays the configured player death SFX.
    /// </summary>
    public static void PlayPlayerDeath()
    {
        if (Current == null)
        {
            return;
        }

        Current.PlaySfx(Current.GetRandomDeathClip());
    }

    /// <summary>
    /// Plays the configured player jump SFX.
    /// </summary>
    public static void PlayPlayerJump()
    {
        Current?.PlaySfx(Current.playerJumpClip);
    }

    /// <summary>
    /// Plays the configured objective reached SFX.
    /// </summary>
    public static void PlayWinZone()
    {
        Current?.PlaySfx(Current.winZoneClip);
    }

    /// <summary>
    /// Plays the configured UI button click SFX.
    /// </summary>
    public static void PlayUiButtonClick()
    {
        Current?.PlayUi(Current.uiButtonClickClip);
    }

    /// <summary>
    /// Applies all channel mixer routing settings.
    /// </summary>
    private void ApplyChannelSettings()
    {
        sfxChannel?.ApplySettings();
        uiChannel?.ApplySettings();
    }

    /// <summary>
    /// Plays a clip through the configured SFX channel.
    /// </summary>
    private void PlaySfx(AudioClip clip)
    {
        PlayOneShot(sfxChannel, clip);
    }

    /// <summary>
    /// Plays a clip through the configured UI channel.
    /// </summary>
    private void PlayUi(AudioClip clip)
    {
        PlayOneShot(uiChannel, clip);
    }

    /// <summary>
    /// Plays a one-shot clip through the requested channel.
    /// </summary>
    private static void PlayOneShot(GameAudioChannel channel, AudioClip clip)
    {
        if (channel == null || clip == null)
        {
            return;
        }

        channel.PlayOneShot(clip);
    }

    /// <summary>
    /// Picks a random death clip from the configured death clip list.
    /// </summary>
    private AudioClip GetRandomDeathClip()
    {
        if (playerDeathClips == null || playerDeathClips.Length == 0)
        {
            return null;
        }

        return playerDeathClips[Random.Range(0, playerDeathClips.Length)];
    }
}
