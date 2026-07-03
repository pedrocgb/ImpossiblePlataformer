using System.Collections.Generic;
using System.IO;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class LevelSelectPanelController : MonoBehaviour
{
    [Title("References")]
    [SerializeField, Required]
    private Transform levelButtonParent;

    [SerializeField, Required]
    private Button levelButtonPrefab;

    [SerializeField]
    private Button playLevelButton;

    [SerializeField]
    private TMP_Text selectedLevelText;

    [SerializeField]
    private TMP_Text selectedLevelDeathsText;

    [Title("Labels")]
    [SerializeField]
    private string defaultSelectionText = "Select a level";

    [SerializeField]
    private string deathCountFormat = "Deaths: {0}";

    [SerializeField]
    private string lockedSuffix = " (Locked)";

    private readonly List<Button> spawnedButtons = new List<Button>();
    private int selectedBuildIndex = -1;

    /// <summary>
    /// Hooks the play button and starts with no selected level.
    /// </summary>
    private void Awake()
    {
        if (playLevelButton != null)
        {
            playLevelButton.onClick.AddListener(PlaySelectedLevel);
            playLevelButton.interactable = false;
        }
    }

    /// <summary>
    /// Refreshes level buttons whenever the panel becomes visible.
    /// </summary>
    private void OnEnable()
    {
        RefreshLevelButtons();
    }

    /// <summary>
    /// Removes runtime listeners created by this controller.
    /// </summary>
    private void OnDestroy()
    {
        if (playLevelButton != null)
        {
            playLevelButton.onClick.RemoveListener(PlaySelectedLevel);
        }

        ClearLevelButtons();
    }

    /// <summary>
    /// Rebuilds the level button list from enabled Build Settings scenes.
    /// </summary>
    [Button]
    public void RefreshLevelButtons()
    {
        ClearLevelButtons();
        selectedBuildIndex = -1;

        for (int buildIndex = 0; buildIndex < SceneManager.sceneCountInBuildSettings; buildIndex++)
        {
            CreateLevelButton(buildIndex);
        }

        RefreshSelection();
    }

    /// <summary>
    /// Selects an unlocked level and refreshes the details panel.
    /// </summary>
    public void SelectLevel(int buildIndex)
    {
        if (!GameSaveSystem.IsLevelUnlocked(buildIndex))
        {
            return;
        }

        selectedBuildIndex = buildIndex;
        RefreshSelection();
    }

    /// <summary>
    /// Loads the selected level when the selection is valid and unlocked.
    /// </summary>
    public void PlaySelectedLevel()
    {
        if (selectedBuildIndex < 0 || !GameSaveSystem.IsLevelUnlocked(selectedBuildIndex))
        {
            return;
        }

        LoadScene(selectedBuildIndex);
    }

    /// <summary>
    /// Creates one runtime button for a level build index.
    /// </summary>
    private void CreateLevelButton(int buildIndex)
    {
        if (levelButtonParent == null || levelButtonPrefab == null)
        {
            return;
        }

        Button button = Instantiate(levelButtonPrefab, levelButtonParent);
        bool isUnlocked = GameSaveSystem.IsLevelUnlocked(buildIndex);
        int capturedBuildIndex = buildIndex;
        button.interactable = isUnlocked;
        button.onClick.AddListener(() => SelectLevel(capturedBuildIndex));
        SetButtonLabel(button, GetLevelButtonLabel(buildIndex, isUnlocked));
        spawnedButtons.Add(button);
    }

    /// <summary>
    /// Removes spawned level buttons and their listeners.
    /// </summary>
    private void ClearLevelButtons()
    {
        for (int i = 0; i < spawnedButtons.Count; i++)
        {
            if (spawnedButtons[i] == null)
            {
                continue;
            }

            spawnedButtons[i].onClick.RemoveAllListeners();
            Destroy(spawnedButtons[i].gameObject);
        }

        spawnedButtons.Clear();
    }

    /// <summary>
    /// Refreshes selected level text, death count, and play button state.
    /// </summary>
    private void RefreshSelection()
    {
        bool hasValidSelection = selectedBuildIndex >= 0 && GameSaveSystem.IsLevelUnlocked(selectedBuildIndex);

        if (playLevelButton != null)
        {
            playLevelButton.interactable = hasValidSelection;
        }

        if (selectedLevelText != null)
        {
            selectedLevelText.text = hasValidSelection ? GetLevelName(selectedBuildIndex) : defaultSelectionText;
        }

        if (selectedLevelDeathsText != null)
        {
            int deathCount = hasValidSelection ? DeathSaveSystem.LoadLevelDeaths(selectedBuildIndex) : 0;
            selectedLevelDeathsText.text = string.Format(deathCountFormat, deathCount);
        }
    }

    /// <summary>
    /// Assigns the display label to the first TMP text found on a level button.
    /// </summary>
    private static void SetButtonLabel(Button button, string label)
    {
        if (button == null)
        {
            return;
        }

        TMP_Text buttonText = button.GetComponentInChildren<TMP_Text>(true);

        if (buttonText != null)
        {
            buttonText.text = label;
        }
    }

    /// <summary>
    /// Builds the level button label and marks locked entries.
    /// </summary>
    private string GetLevelButtonLabel(int buildIndex, bool isUnlocked)
    {
        string levelName = GetLevelName(buildIndex);
        return isUnlocked ? levelName : $"{levelName}{lockedSuffix}";
    }

    /// <summary>
    /// Gets a friendly level name from the build scene path.
    /// </summary>
    private static string GetLevelName(int buildIndex)
    {
        string scenePath = SceneUtility.GetScenePathByBuildIndex(buildIndex);

        if (string.IsNullOrWhiteSpace(scenePath))
        {
            return $"Level {buildIndex + 1}";
        }

        string fileName = Path.GetFileNameWithoutExtension(scenePath);
        return string.IsNullOrWhiteSpace(fileName) ? $"Level {buildIndex + 1}" : fileName;
    }

    /// <summary>
    /// Loads a level through the fade manager when one is available.
    /// </summary>
    private static void LoadScene(int buildIndex)
    {
        if (SceneFadeUiManager.Current != null)
        {
            SceneFadeUiManager.Current.LoadScene(buildIndex);
            return;
        }

        SceneManager.LoadScene(buildIndex);
    }
}
