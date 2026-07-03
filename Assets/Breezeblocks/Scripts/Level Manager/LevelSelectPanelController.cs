using System.Collections.Generic;
using System.IO;
using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class LevelSelectPanelController : MonoBehaviour
{
    private const string DefaultSelectionKey = "level.select.default";
    private const string DeathCountFormatKey = "level.select.deathsFormat";
    private const string DifficultyFormatKey = "level.select.difficultyFormat";
    private const string LevelButtonFormatKey = "level.select.buttonFormat";
    private const string SelectedTitleFormatKey = "level.select.selectedTitleFormat";

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

    [SerializeField]
    private TMP_Text selectedLevelDifficultyText;

    [Title("Level Data")]
    [SerializeField]
    private LevelDefinition[] levelDefinitions = new LevelDefinition[0];

    [SerializeField, MinValue(0)]
    private int levelBuildIndexOffset = 2;

    [Title("Panel Animation")]
    [SerializeField]
    private bool refreshOnEnable = true;

    [SerializeField]
    private RectTransform levelTitleTransform;

    [SerializeField]
    private RectTransform levelsFrameTransform;

    [SerializeField]
    private Vector2 titleEntranceOffset = new Vector2(0f, 420f);

    [SerializeField]
    private Vector2 frameEntranceOffset = new Vector2(0f, -520f);

    [SerializeField, MinValue(0f)]
    private float panelMoveDuration = 0.45f;

    [SerializeField]
    private Ease panelMoveEase = Ease.OutBack;

    [Title("Button Spawn Animation")]
    [SerializeField, MinValue(0f)]
    private float levelButtonSpawnDelay = 0.045f;

    [SerializeField, MinValue(0f)]
    private float levelButtonPopDuration = 0.18f;

    [SerializeField, Range(0.01f, 1f)]
    private float levelButtonHiddenScale = 0.15f;

    [SerializeField]
    private Ease levelButtonPopEase = Ease.OutBack;

    private readonly List<Button> spawnedButtons = new List<Button>();
    private readonly List<Vector3> spawnedButtonScales = new List<Vector3>();
    private readonly List<int> spawnedBuildIndexes = new List<int>();
    private readonly List<int> spawnedLevelListIndexes = new List<int>();
    private int selectedBuildIndex = -1;
    private int selectedLevelListIndex = -1;
    private LevelDefinition selectedLevelDefinition;
    private Vector2 titleOriginalPosition;
    private Vector2 frameOriginalPosition;
    private Sequence panelSequence;
    private Sequence buttonSpawnSequence;
    private bool suppressNextEnableRefresh;

    /// <summary>
    /// Hooks the play button, caches authored panel positions, and starts with no selected level.
    /// </summary>
    private void Awake()
    {
        CacheOriginalPositions();
        GameLocalization.LanguageChanged += RefreshLocalizedContent;

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
        if (suppressNextEnableRefresh)
        {
            suppressNextEnableRefresh = false;
            return;
        }

        if (refreshOnEnable)
        {
            RefreshLevelButtons();
        }
    }

    /// <summary>
    /// Stops active panel tweens when Unity disables this controller.
    /// </summary>
    private void OnDisable()
    {
        KillPanelSequence();
        KillButtonSpawnSequence();
    }

    /// <summary>
    /// Removes runtime listeners created by this controller.
    /// </summary>
    private void OnDestroy()
    {
        KillPanelSequence();
        KillButtonSpawnSequence();
        GameLocalization.LanguageChanged -= RefreshLocalizedContent;

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
        ResetSelectionState();

        if (HasConfiguredLevels())
        {
            CreateConfiguredLevelButtons();
        }
        else
        {
            for (int buildIndex = 0; buildIndex < SceneManager.sceneCountInBuildSettings; buildIndex++)
            {
                CreateLevelButton(buildIndex);
            }
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
        selectedLevelListIndex = FindLevelDefinitionIndex(buildIndex);
        selectedLevelDefinition = GetLevelDefinition(selectedLevelListIndex);
        RefreshSelection();
    }

    /// <summary>
    /// Selects an unlocked ScriptableObject-backed level by its list position.
    /// </summary>
    public void SelectConfiguredLevel(int listIndex)
    {
        LevelDefinition levelDefinition = GetLevelDefinition(listIndex);

        if (levelDefinition == null)
        {
            return;
        }

        int buildIndex = GetBuildIndex(listIndex);

        if (!IsConfiguredLevelUnlocked(listIndex))
        {
            return;
        }

        selectedBuildIndex = buildIndex;
        selectedLevelListIndex = listIndex;
        selectedLevelDefinition = levelDefinition;
        RefreshSelection();
    }

    /// <summary>
    /// Loads the selected level when the selection is valid and unlocked.
    /// </summary>
    public void PlaySelectedLevel()
    {
        if (selectedBuildIndex < 0 || !IsSelectedLevelUnlocked())
        {
            return;
        }

        LoadScene(selectedBuildIndex);
    }

    /// <summary>
    /// Plays the levels panel entrance and then pops level buttons in rapidly one by one.
    /// </summary>
    public void ShowAnimated()
    {
        KillPanelSequence();
        KillButtonSpawnSequence();
        ClearLevelButtons();
        ResetSelectionState();
        RefreshSelection();
        SetPanelToEntrancePositions();

        panelSequence = DOTween.Sequence().SetUpdate(true);
        AppendPanelEntrance(levelTitleTransform, titleOriginalPosition);
        AppendPanelEntrance(levelsFrameTransform, frameOriginalPosition);
        panelSequence.OnComplete(SpawnAndAnimateLevelButtons);
    }

    /// <summary>
    /// Prepares this panel for an animated show before the parent panel GameObject becomes active.
    /// </summary>
    public void PrepareForAnimatedShow()
    {
        suppressNextEnableRefresh = true;
        KillPanelSequence();
        KillButtonSpawnSequence();
        ClearLevelButtons();
        ResetSelectionState();
        RefreshSelection();
        SetPanelToEntrancePositions();
    }

    /// <summary>
    /// Plays the levels panel exit animation and then invokes the completion callback.
    /// </summary>
    public void HideAnimated(System.Action onComplete)
    {
        KillPanelSequence();
        KillButtonSpawnSequence();
        SetSpawnedButtonsInteractable(false);

        panelSequence = DOTween.Sequence().SetUpdate(true);
        AppendPanelExit(levelTitleTransform, titleOriginalPosition + titleEntranceOffset);
        AppendPanelExit(levelsFrameTransform, frameOriginalPosition + frameEntranceOffset);
        panelSequence.OnComplete(() =>
        {
            ClearLevelButtons();
            onComplete?.Invoke();
        });
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
        SetButtonLabel(button, GetLevelButtonLabel(buildIndex));
        TrackSpawnedButton(button, buildIndex, -1);
    }

    /// <summary>
    /// Creates runtime buttons from configured ScriptableObject level definitions.
    /// </summary>
    private void CreateConfiguredLevelButtons()
    {
        for (int i = 0; i < levelDefinitions.Length; i++)
        {
            LevelDefinition levelDefinition = levelDefinitions[i];

            if (levelDefinition == null)
            {
                continue;
            }

            CreateLevelButton(levelDefinition, i);
        }
    }

    /// <summary>
    /// Creates one runtime button for a ScriptableObject-backed level at its list position.
    /// </summary>
    private void CreateLevelButton(LevelDefinition levelDefinition, int listIndex)
    {
        if (levelButtonParent == null || levelButtonPrefab == null || levelDefinition == null)
        {
            return;
        }

        int buildIndex = GetBuildIndex(listIndex);
        Button button = Instantiate(levelButtonPrefab, levelButtonParent);
        bool isUnlocked = IsConfiguredLevelUnlocked(listIndex);
        int capturedListIndex = listIndex;
        button.interactable = isUnlocked;
        button.onClick.AddListener(() => SelectConfiguredLevel(capturedListIndex));
        SetButtonLabel(button, GetConfiguredLevelButtonLabel(listIndex));
        TrackSpawnedButton(button, buildIndex, listIndex);
    }

    /// <summary>
    /// Removes spawned level buttons and their listeners.
    /// </summary>
    private void ClearLevelButtons()
    {
        KillButtonSpawnSequence();

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
        spawnedButtonScales.Clear();
        spawnedBuildIndexes.Clear();
        spawnedLevelListIndexes.Clear();
    }

    /// <summary>
    /// Resets the currently selected level without rebuilding buttons.
    /// </summary>
    private void ResetSelectionState()
    {
        selectedBuildIndex = -1;
        selectedLevelListIndex = -1;
        selectedLevelDefinition = null;
    }

    /// <summary>
    /// Refreshes selected level text, death count, and play button state.
    /// </summary>
    private void RefreshSelection()
    {
        bool hasValidSelection = selectedBuildIndex >= 0 && IsSelectedLevelUnlocked();

        if (playLevelButton != null)
        {
            playLevelButton.interactable = hasValidSelection;
        }

        if (selectedLevelText != null)
        {
            selectedLevelText.text = hasValidSelection ? GetSelectedLevelName() : GameLocalization.Get(DefaultSelectionKey, "Select a level");
        }

        if (selectedLevelDeathsText != null)
        {
            int deathCount = hasValidSelection ? DeathSaveSystem.LoadLevelDeaths(selectedBuildIndex) : 0;
            selectedLevelDeathsText.text = GameLocalization.Format(DeathCountFormatKey, "Deaths: {0}", deathCount);
        }

        if (selectedLevelDifficultyText != null)
        {
            string difficultyLabel = hasValidSelection ? GetSelectedDifficultyLabel() : string.Empty;
            selectedLevelDifficultyText.text = GameLocalization.Format(DifficultyFormatKey, "Difficulty: {0}", difficultyLabel);
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
    /// Builds the level button label without changing text for locked entries.
    /// </summary>
    private string GetLevelButtonLabel(int buildIndex)
    {
        return GetLevelName(buildIndex);
    }

    /// <summary>
    /// Builds the ScriptableObject-backed level button label without changing text for locked entries.
    /// </summary>
    private string GetConfiguredLevelButtonLabel(int listIndex)
    {
        return GameLocalization.Format(LevelButtonFormatKey, "Level {0}", GetDisplayLevelNumber(listIndex));
    }

    /// <summary>
    /// Gets the selected level display name from configured data when available.
    /// </summary>
    private string GetSelectedLevelName()
    {
        if (selectedLevelDefinition != null)
        {
            return GameLocalization.Format(SelectedTitleFormatKey, "Level {0}", GetDisplayLevelNumber(selectedLevelListIndex));
        }

        return GetLevelName(selectedBuildIndex);
    }

    /// <summary>
    /// Gets the selected level difficulty label from configured data when available.
    /// </summary>
    private string GetSelectedDifficultyLabel()
    {
        if (selectedLevelDefinition != null)
        {
            return selectedLevelDefinition.GetDifficultyLabel();
        }

        LevelDefinition levelDefinition = GetLevelDefinition(selectedLevelListIndex);
        return levelDefinition != null ? levelDefinition.GetDifficultyLabel() : string.Empty;
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

    /// <summary>
    /// Caches authored positions for animated level panel containers.
    /// </summary>
    private void CacheOriginalPositions()
    {
        if (levelTitleTransform != null)
        {
            titleOriginalPosition = levelTitleTransform.anchoredPosition;
        }

        if (levelsFrameTransform != null)
        {
            frameOriginalPosition = levelsFrameTransform.anchoredPosition;
        }
    }

    /// <summary>
    /// Places the panel containers offscreen before the entrance animation starts.
    /// </summary>
    private void SetPanelToEntrancePositions()
    {
        SetAnchoredPosition(levelTitleTransform, titleOriginalPosition + titleEntranceOffset);
        SetAnchoredPosition(levelsFrameTransform, frameOriginalPosition + frameEntranceOffset);
    }

    /// <summary>
    /// Adds one panel container entrance tween to the active sequence.
    /// </summary>
    private void AppendPanelEntrance(RectTransform target, Vector2 originalPosition)
    {
        if (panelSequence == null || target == null)
        {
            return;
        }

        panelSequence.Join(target.DOAnchorPos(originalPosition, panelMoveDuration).SetEase(panelMoveEase));
    }

    /// <summary>
    /// Adds one panel container exit tween to the active sequence.
    /// </summary>
    private void AppendPanelExit(RectTransform target, Vector2 exitPosition)
    {
        if (panelSequence == null || target == null)
        {
            return;
        }

        panelSequence.Join(target.DOAnchorPos(exitPosition, panelMoveDuration).SetEase(Ease.InBack));
    }

    /// <summary>
    /// Plays pop animations for spawned level buttons and enables each button after its own tween completes.
    /// </summary>
    private void PlayButtonSpawnAnimation()
    {
        KillButtonSpawnSequence();
        buttonSpawnSequence = DOTween.Sequence().SetUpdate(true);

        for (int i = 0; i < spawnedButtons.Count; i++)
        {
            Button button = spawnedButtons[i];

            if (button == null)
            {
                continue;
            }

            int capturedIndex = i;
            Transform buttonTransform = button.transform;
            Vector3 targetScale = spawnedButtonScales[i];
            button.interactable = false;
            buttonTransform.localScale = targetScale * levelButtonHiddenScale;
            float startTime = levelButtonSpawnDelay * i;

            Tween popTween = buttonTransform
                .DOScale(targetScale, levelButtonPopDuration)
                .SetEase(levelButtonPopEase)
                .OnComplete(() => SetSpawnedButtonInteractable(capturedIndex, true));

            buttonSpawnSequence.Insert(startTime, popTween);
        }
    }

    /// <summary>
    /// Rebuilds level buttons after the panel entrance animation has finished, then plays their pop animation.
    /// </summary>
    private void SpawnAndAnimateLevelButtons()
    {
        RefreshLevelButtons();
        SetSpawnedButtonsInteractable(false);
        PlayButtonSpawnAnimation();
    }

    /// <summary>
    /// Tracks one spawned button and its authored scale for pop animation restore.
    /// </summary>
    private void TrackSpawnedButton(Button button, int buildIndex, int listIndex)
    {
        if (button == null)
        {
            return;
        }

        spawnedButtons.Add(button);
        spawnedButtonScales.Add(button.transform.localScale);
        spawnedBuildIndexes.Add(buildIndex);
        spawnedLevelListIndexes.Add(listIndex);
    }

    /// <summary>
    /// Enables or disables every spawned level button.
    /// </summary>
    private void SetSpawnedButtonsInteractable(bool isInteractable)
    {
        for (int i = 0; i < spawnedButtons.Count; i++)
        {
            SetSpawnedButtonInteractable(i, isInteractable);
        }
    }

    /// <summary>
    /// Enables one spawned level button when it is valid and unlocked.
    /// </summary>
    private void SetSpawnedButtonInteractable(int index, bool isInteractable)
    {
        if (index < 0 || index >= spawnedButtons.Count || spawnedButtons[index] == null)
        {
            return;
        }

        if (!isInteractable)
        {
            spawnedButtons[index].interactable = false;
            return;
        }

        int buildIndex = GetBuildIndexForSpawnedButton(index);
        int listIndex = GetLevelListIndexForSpawnedButton(index);
        spawnedButtons[index].interactable = listIndex >= 0
            ? IsConfiguredLevelUnlocked(listIndex)
            : GameSaveSystem.IsLevelUnlocked(buildIndex);
    }

    /// <summary>
    /// Resolves the build index associated with one spawned button.
    /// </summary>
    private int GetBuildIndexForSpawnedButton(int index)
    {
        return index >= 0 && index < spawnedBuildIndexes.Count ? spawnedBuildIndexes[index] : -1;
    }

    /// <summary>
    /// Resolves the configured level list index associated with one spawned button.
    /// </summary>
    private int GetLevelListIndexForSpawnedButton(int index)
    {
        return index >= 0 && index < spawnedLevelListIndexes.Count ? spawnedLevelListIndexes[index] : -1;
    }

    /// <summary>
    /// Checks whether a configured level is unlocked, always allowing the first listed level.
    /// </summary>
    private bool IsConfiguredLevelUnlocked(int listIndex)
    {
        return listIndex == 0 || GameSaveSystem.IsLevelUnlocked(GetBuildIndex(listIndex));
    }

    /// <summary>
    /// Checks whether the current selection is playable.
    /// </summary>
    private bool IsSelectedLevelUnlocked()
    {
        if (selectedLevelListIndex >= 0)
        {
            return IsConfiguredLevelUnlocked(selectedLevelListIndex);
        }

        return GameSaveSystem.IsLevelUnlocked(selectedBuildIndex);
    }

    /// <summary>
    /// Checks whether the inspector contains any configured level definitions.
    /// </summary>
    private bool HasConfiguredLevels()
    {
        if (levelDefinitions == null)
        {
            return false;
        }

        for (int i = 0; i < levelDefinitions.Length; i++)
        {
            if (levelDefinitions[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Resolves the scene build index from a ScriptableObject list position plus the configured offset.
    /// </summary>
    private int GetBuildIndex(int listIndex)
    {
        return listIndex < 0 ? -1 : listIndex + levelBuildIndexOffset;
    }

    /// <summary>
    /// Finds the configured level list position that maps to the selected build index.
    /// </summary>
    private int FindLevelDefinitionIndex(int buildIndex)
    {
        if (!HasConfiguredLevels())
        {
            return -1;
        }

        for (int i = 0; i < levelDefinitions.Length; i++)
        {
            LevelDefinition levelDefinition = levelDefinitions[i];

            if (levelDefinition != null && GetBuildIndex(i) == buildIndex)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Gets configured level data by list position when one exists.
    /// </summary>
    private LevelDefinition GetLevelDefinition(int listIndex)
    {
        if (levelDefinitions == null || listIndex < 0 || listIndex >= levelDefinitions.Length)
        {
            return null;
        }

        return levelDefinitions[listIndex];
    }

    /// <summary>
    /// Converts a zero-based list position into the one-based level number shown to players.
    /// </summary>
    private static int GetDisplayLevelNumber(int listIndex)
    {
        return listIndex + 1;
    }

    /// <summary>
    /// Refreshes text that depends on the active language without rebuilding panel animation state.
    /// </summary>
    private void RefreshLocalizedContent()
    {
        RefreshLevelButtonLabels();
        RefreshSelection();
    }

    /// <summary>
    /// Updates spawned level button labels after the active language changes.
    /// </summary>
    private void RefreshLevelButtonLabels()
    {
        for (int i = 0; i < spawnedButtons.Count; i++)
        {
            if (spawnedButtons[i] == null)
            {
                continue;
            }

            int buildIndex = GetBuildIndexForSpawnedButton(i);
            int listIndex = GetLevelListIndexForSpawnedButton(i);
            string label = listIndex >= 0 ? GetConfiguredLevelButtonLabel(listIndex) : GetLevelButtonLabel(buildIndex);
            SetButtonLabel(spawnedButtons[i], label);
        }
    }

    /// <summary>
    /// Assigns a RectTransform anchored position when the reference exists.
    /// </summary>
    private static void SetAnchoredPosition(RectTransform target, Vector2 position)
    {
        if (target != null)
        {
            target.anchoredPosition = position;
        }
    }

    /// <summary>
    /// Stops the active panel movement sequence when it exists.
    /// </summary>
    private void KillPanelSequence()
    {
        if (panelSequence == null || !panelSequence.IsActive())
        {
            return;
        }

        panelSequence.Kill();
        panelSequence = null;
    }

    /// <summary>
    /// Stops the active button pop sequence when it exists.
    /// </summary>
    private void KillButtonSpawnSequence()
    {
        if (buttonSpawnSequence == null || !buttonSpawnSequence.IsActive())
        {
            return;
        }

        buttonSpawnSequence.Kill();
        buttonSpawnSequence = null;
    }
}
