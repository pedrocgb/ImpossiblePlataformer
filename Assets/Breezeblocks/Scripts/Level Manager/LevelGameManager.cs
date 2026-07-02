using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Sirenix.OdinInspector;

public sealed class LevelGameManager : MonoBehaviour
{
    private struct LevelProgress
    {
        public int DeathCount;
        public float ElapsedSeconds;
    }

    private static readonly Dictionary<string, LevelProgress> SavedProgressByLevel = new Dictionary<string, LevelProgress>();

    public static LevelGameManager Current { get; private set; }

    /// <summary>
    /// Gets whether win zones are allowed to complete the level.
    /// </summary>
    public bool CanCompleteLevel => !levelEnded && !resetQueued;

    [Title("Level")]
    [SerializeField]
    private string levelTitle = "Level 1";

    [SerializeField]
    [MinValue(0f)]
    private float resetDelay = 0.5f;

    [SerializeField]
    private bool pauseGameOnWin = true;

    [Title("Panels")]
    [SerializeField]
    private LevelResultPanel winPanel;

    [Title("Optional Live HUD")]
    [SerializeField]
    private TMP_Text timerText;

    [SerializeField]
    private TMP_Text deathsText;

    [ShowInInspector]
    [ReadOnly]
    private int deathCount;

    [ShowInInspector]
    [ReadOnly]
    private float elapsedSeconds;

    private bool levelEnded;
    private bool resetQueued;
    private string progressKey;
    private ILevelResettable[] resettableObjects = new ILevelResettable[0];
    private Coroutine resetRoutine;

    /// <summary>
    /// Registers the active manager instance and prepares the level UI state.
    /// </summary>
    private void Awake()
    {
        if (Current != null && Current != this)
        {
            Debug.LogWarning("Multiple LevelGameManager instances found. The newest instance will be used.", this);
        }

        Current = this;
        ResumeGameTime();
        progressKey = GetProgressKey();
        LoadProgress();
        HideWinPanel();
        RefreshHud();
    }

    /// <summary>
    /// Finds resettable level objects after all scene objects have run Awake.
    /// </summary>
    private void Start()
    {
        CacheResettableObjects();
    }

    /// <summary>
    /// Clears the active manager reference when this manager is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        if (resetRoutine != null)
        {
            StopCoroutine(resetRoutine);
        }

        ResumeGameTime();

        if (Current == this)
        {
            Current = null;
        }
    }

    /// <summary>
    /// Advances the level timer while the level is still active.
    /// </summary>
    private void Update()
    {
        if (levelEnded)
        {
            return;
        }

        elapsedSeconds += Time.deltaTime;
        RefreshHud();
    }

    /// <summary>
    /// Adds one death to the current level and schedules an in-place level reset.
    /// </summary>
    public void RegisterDeath(GameObject player)
    {
        if (levelEnded || resetQueued)
        {
            return;
        }

        deathCount++;
        RegisterTotalDeath();
        GameSfxPlayer.PlayPlayerDeath();
        resetQueued = true;
        LockDeadPlayer(player);
        PlayDeathAnimation(player);
        SaveProgress();
        RefreshHud();
        resetRoutine = StartCoroutine(ResetLevelAfterDelay());
    }

    /// <summary>
    /// Completes the current level and opens the win panel with final stats.
    /// </summary>
    public void RegisterWin()
    {
        if (levelEnded || resetQueued)
        {
            return;
        }

        levelEnded = true;
        ClearProgress();
        RefreshHud();
        GameSfxPlayer.PlayWinZone();
        PauseGameTime();

        if (winPanel != null)
        {
            winPanel.ShowWin(levelTitle, FormatTime(elapsedSeconds), deathCount, GetTotalDeaths());
        }
    }

    /// <summary>
    /// Reloads the active scene so the player can try the level again.
    /// </summary>
    public void RetryLevel()
    {
        SaveProgress();
        ResumeGameTime();
        Scene activeScene = SceneManager.GetActiveScene();
        LoadScene(activeScene.buildIndex);
    }

    /// <summary>
    /// Loads the next scene in build settings when one exists.
    /// </summary>
    public void LoadNextLevel()
    {
        ClearProgress();
        ResumeGameTime();
        int nextIndex = SceneManager.GetActiveScene().buildIndex + 1;

        if (nextIndex < SceneManager.sceneCountInBuildSettings)
        {
            LoadScene(nextIndex);
            return;
        }

        Debug.LogWarning("No next level exists in Build Settings.", this);
    }

    /// <summary>
    /// Quits the game, or exits Play Mode while running in the Unity Editor.
    /// </summary>
    public void QuitGame()
    {
        SaveProgress();
        ResumeGameTime();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// Hides the win panel at level start.
    /// </summary>
    private void HideWinPanel()
    {
        if (winPanel != null)
        {
            winPanel.HideImmediate();
        }
    }

    /// <summary>
    /// Updates optional live HUD text with the latest timer and death values.
    /// </summary>
    private void RefreshHud()
    {
        if (timerText != null)
        {
            timerText.text = FormatTime(elapsedSeconds);
        }

        if (deathsText != null)
        {
            deathsText.text = deathCount.ToString();
        }
    }

    /// <summary>
    /// Converts seconds into a minute and second display string.
    /// </summary>
    private static string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int minutes = totalSeconds / 60;
        int remainingSeconds = totalSeconds % 60;
        return $"{minutes:00}:{remainingSeconds:00}";
    }

    /// <summary>
    /// Loads saved attempt progress for the active level.
    /// </summary>
    private void LoadProgress()
    {
        if (!SavedProgressByLevel.TryGetValue(progressKey, out LevelProgress progress))
        {
            return;
        }

        deathCount = progress.DeathCount;
        elapsedSeconds = progress.ElapsedSeconds;
    }

    /// <summary>
    /// Saves current attempt progress for retry reloads.
    /// </summary>
    private void SaveProgress()
    {
        if (string.IsNullOrEmpty(progressKey))
        {
            return;
        }

        SavedProgressByLevel[progressKey] = new LevelProgress
        {
            DeathCount = deathCount,
            ElapsedSeconds = elapsedSeconds
        };
    }

    /// <summary>
    /// Clears saved attempt progress after the level is completed or left.
    /// </summary>
    private void ClearProgress()
    {
        if (!string.IsNullOrEmpty(progressKey))
        {
            SavedProgressByLevel.Remove(progressKey);
        }
    }

    /// <summary>
    /// Builds a stable key for the active scene progress.
    /// </summary>
    private static string GetProgressKey()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        return string.IsNullOrEmpty(activeScene.path) ? activeScene.name : activeScene.path;
    }

    /// <summary>
    /// Waits for the configured delay before restoring level objects.
    /// </summary>
    private IEnumerator ResetLevelAfterDelay()
    {
        yield return new WaitForSeconds(resetDelay);
        ResetLevelObjects();
        resetQueued = false;
        resetRoutine = null;
    }

    /// <summary>
    /// Restores every cached resettable object while keeping timer and death count intact.
    /// </summary>
    private void ResetLevelObjects()
    {
        if (resettableObjects.Length == 0)
        {
            CacheResettableObjects();
        }

        for (int i = 0; i < resettableObjects.Length; i++)
        {
            resettableObjects[i]?.ResetLevelState();
        }

        RefreshHud();
    }

    /// <summary>
    /// Caches all scene objects that know how to restore their starting state.
    /// </summary>
    private void CacheResettableObjects()
    {
        List<ILevelResettable> resettableList = new List<ILevelResettable>();
        MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is ILevelResettable resettable)
            {
                resettableList.Add(resettable);
            }
        }

        resettableObjects = resettableList.ToArray();
    }

    /// <summary>
    /// Prevents the dead player from accepting input before the level reset happens.
    /// </summary>
    private static void LockDeadPlayer(GameObject player)
    {
        if (player == null)
        {
            return;
        }

        PlayerMovement playerMovement = player.GetComponent<PlayerMovement>();

        if (playerMovement != null)
        {
            playerMovement.SetControlEnabled(false);
        }
    }

    /// <summary>
    /// Starts the optional player death visual effect on the dead player.
    /// </summary>
    private static void PlayDeathAnimation(GameObject player)
    {
        if (player == null)
        {
            return;
        }

        PlayerDeathAnimation deathAnimation = player.GetComponent<PlayerDeathAnimation>();

        if (deathAnimation != null)
        {
            deathAnimation.PlayDeath();
        }
    }

    /// <summary>
    /// Pauses scaled gameplay time after the level is won when configured.
    /// </summary>
    private void PauseGameTime()
    {
        if (pauseGameOnWin)
        {
            Time.timeScale = 0f;
        }
    }

    /// <summary>
    /// Restores scaled gameplay time before scene changes or fresh level startup.
    /// </summary>
    private static void ResumeGameTime()
    {
        Time.timeScale = 1f;
    }

    /// <summary>
    /// Loads a scene through the persistent fade manager when one is available.
    /// </summary>
    private static void LoadScene(int sceneBuildIndex)
    {
        if (SceneFadeUiManager.Current != null)
        {
            SceneFadeUiManager.Current.LoadScene(sceneBuildIndex);
            return;
        }

        SceneManager.LoadScene(sceneBuildIndex);
    }

    /// <summary>
    /// Adds one death to the persistent all-time death counter.
    /// </summary>
    private static int RegisterTotalDeath()
    {
        if (PersistentDeathCounter.Current != null)
        {
            return PersistentDeathCounter.Current.RegisterDeath();
        }

        int totalDeaths = DeathSaveSystem.LoadTotalDeaths() + 1;
        DeathSaveSystem.SaveTotalDeaths(totalDeaths);
        return totalDeaths;
    }

    /// <summary>
    /// Reads the persistent all-time death count for final level results.
    /// </summary>
    private static int GetTotalDeaths()
    {
        if (PersistentDeathCounter.Current != null)
        {
            return PersistentDeathCounter.Current.TotalDeaths;
        }

        return DeathSaveSystem.LoadTotalDeaths();
    }
}
