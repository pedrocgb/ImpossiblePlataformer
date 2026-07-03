using System.Collections;
using System.Collections.Generic;
using Rewired;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

public sealed class RewiredControlMapperSettingsBridge : MonoBehaviour
{
    private const int InvalidId = -1;
    private const int KeyboardControllerId = 0;
    private const int PrimaryBindingIndex = 0;
    private const int SecondaryBindingIndex = 1;

    private enum BindingSlot
    {
        Primary,
        Secondary
    }

    [Title("Player")]
    [SerializeField, MinValue(0)]
    private int playerId;

    [SerializeField]
    private bool fallbackToFirstPlayer = true;

    [Title("Categories")]
    [SerializeField]
    private string actionCategory = "Default";

    [SerializeField]
    private string mapCategory = "Default";

    [SerializeField]
    private string mapLayout = "Default";

    [SerializeField]
    private string uiMapCategory = "UI";

    [Title("Rows")]
    [SerializeField]
    private CustomControlBindingRow rowPrefab;

    [SerializeField]
    private Transform rowsParent;

    [SerializeField]
    private string unboundLabel = "Unbound";

    [SerializeField]
    private string optionalUnboundLabel = string.Empty;

    [Title("Listening")]
    [SerializeField, MinValue(0f)]
    private float assignmentTimeout = 5f;

    [SerializeField, MinValue(0f)]
    private float listenStartDelay = 0.1f;

    [SerializeField]
    private string listeningMessage = "Press a key...";

    [SerializeField]
    private string cancelActionName = "PauseMenu";

    [Title("Listening Panel")]
    [SerializeField]
    private GameObject listeningPanelRoot;

    [SerializeField]
    private TMP_Text listeningPanelMessageText;

    [Title("Validation")]
    [SerializeField]
    private TMP_Text validationText;

    [SerializeField]
    private string validMessage = string.Empty;

    [SerializeField]
    private string invalidMessagePrefix = "Missing bindings: ";

    [SerializeField]
    private Color validColor = Color.white;

    [SerializeField]
    private Color invalidColor = Color.red;

    private readonly InputMapper inputMapper = new InputMapper();
    private readonly List<BindingRow> bindingRows = new List<BindingRow>();
    private readonly List<CustomControlBindingRow> rowViews = new List<CustomControlBindingRow>();
    private readonly List<ActionElementMap> cancelActionMaps = new List<ActionElementMap>();
    private Coroutine listenCoroutine;
    private Player currentPlayer;
    private BindingRow pendingRow;
    private BindingSlot pendingSlot;
    private ActionElementMap pendingOriginalMap;
    private ActionElementMap pendingConflictMap;
    private ControllerMap pendingConflictControllerMap;
    private bool isListeningForBinding;
    private bool pendingMapChanged;

    /// <summary>
    /// Gets whether the custom mapper is waiting for a new keyboard binding.
    /// </summary>
    public bool IsListeningForBinding => isListeningForBinding;

    /// <summary>
    /// Configures the Rewired input mapper used by this custom controls panel.
    /// </summary>
    private void Awake()
    {
        ConfigureInputMapper();
        SetListeningPanel(false, string.Empty);
    }

    /// <summary>
    /// Loads saved keyboard bindings once Rewired has initialized.
    /// </summary>
    private IEnumerator Start()
    {
        while (!ReInput.isReady)
        {
            yield return null;
        }

        RefreshPlayer();
        LoadSavedBindings();
    }

    /// <summary>
    /// Subscribes to input mapper events while the panel is available.
    /// </summary>
    private void OnEnable()
    {
        SubscribeEvents();
    }

    /// <summary>
    /// Stops listening and removes event subscriptions when the panel is disabled.
    /// </summary>
    private void OnDisable()
    {
        StopListening();
        UnsubscribeEvents();
    }

    /// <summary>
    /// Stops listening and removes event subscriptions before Unity destroys this component.
    /// </summary>
    private void OnDestroy()
    {
        StopListening();
        UnsubscribeEvents();
    }

    /// <summary>
    /// Opens the project-owned control mapper by building and refreshing its keyboard rows.
    /// </summary>
    public void OpenMapper()
    {
        RefreshPlayer();
        LoadSavedBindings();
        BuildRows();
        RefreshRows();
        RefreshValidation();
    }

    /// <summary>
    /// Cancels any pending keyboard assignment and restores the previous binding state.
    /// </summary>
    public bool CancelPendingBinding()
    {
        if (!isListeningForBinding)
        {
            return false;
        }

        RestorePendingBinding();
        StopListening();
        RefreshRows();
        RefreshValidation();
        return true;
    }

    /// <summary>
    /// Keeps the legacy settings close hook while validating the custom mapper rows.
    /// </summary>
    public bool CloseMapperIfValid()
    {
        bool canClose = HasAllRequiredBindings();
        RefreshValidation();
        SaveBindings();
        return canClose;
    }

    /// <summary>
    /// Gets whether settings may close without leaving required controls unbound.
    /// </summary>
    public bool CanCloseSettings()
    {
        bool canClose = HasAllRequiredBindings();
        RefreshValidation();
        return canClose;
    }

    /// <summary>
    /// Reloads default Rewired keyboard maps for the configured player and redraws all custom rows.
    /// </summary>
    public void RestoreDefaults()
    {
        RefreshPlayer();

        if (currentPlayer == null)
        {
            return;
        }

        currentPlayer.controllers.maps.LoadDefaultMaps(ControllerType.Keyboard);
        SaveBindings();
        RefreshRows();
        RefreshValidation();
    }

    /// <summary>
    /// Updates validation text with any missing required action bindings.
    /// </summary>
    public void RefreshValidation()
    {
        if (validationText == null)
        {
            return;
        }

        string missingActions = GetMissingBindingsText();
        bool isValid = string.IsNullOrEmpty(missingActions);
        validationText.text = isValid ? validMessage : invalidMessagePrefix + missingActions;
        validationText.color = isValid ? validColor : invalidColor;
    }

    /// <summary>
    /// Applies stable input mapper options for rebinding keyboard controls.
    /// </summary>
    private void ConfigureInputMapper()
    {
        inputMapper.options.timeout = assignmentTimeout;
    }

    /// <summary>
    /// Subscribes to mapper events without duplicating handlers.
    /// </summary>
    private void SubscribeEvents()
    {
        UnsubscribeEvents();
        inputMapper.InputMappedEvent += OnInputMapped;
        inputMapper.StoppedEvent += OnInputMappingStopped;
        inputMapper.ConflictFoundEvent += OnConflictFound;
    }

    /// <summary>
    /// Removes mapper event handlers.
    /// </summary>
    private void UnsubscribeEvents()
    {
        inputMapper.RemoveAllEventListeners();
    }

    /// <summary>
    /// Resolves the configured player, optionally falling back to the first Rewired game player.
    /// </summary>
    private void RefreshPlayer()
    {
        currentPlayer = null;

        if (!ReInput.isReady)
        {
            return;
        }

        currentPlayer = ReInput.players.GetPlayer(playerId);

        if (currentPlayer != null || !fallbackToFirstPlayer)
        {
            return;
        }

        IList<Player> players = ReInput.players.Players;

        if (players.Count > 0)
        {
            currentPlayer = players[0];
        }
    }

    /// <summary>
    /// Rebuilds the runtime rows from the configured Rewired action category.
    /// </summary>
    private void BuildRows()
    {
        ClearRows();

        if (!ReInput.isReady || rowPrefab == null || rowsParent == null)
        {
            return;
        }

        foreach (InputAction action in ReInput.mapping.ActionsInCategory(actionCategory))
        {
            if (action.type == InputActionType.Button)
            {
                CreateRow(action, AxisRange.Positive, GetActionDisplayName(action));
                continue;
            }

            if (action.type == InputActionType.Axis)
            {
                CreateRow(action, AxisRange.Positive, GetAxisDisplayName(action, true));
                CreateRow(action, AxisRange.Negative, GetAxisDisplayName(action, false));
            }
        }
    }

    /// <summary>
    /// Creates one runtime row and wires it to primary and secondary keyboard rebinding callbacks.
    /// </summary>
    private void CreateRow(InputAction action, AxisRange actionRange, string displayName)
    {
        CustomControlBindingRow rowView = Instantiate(rowPrefab, rowsParent);
        BindingRow row = new BindingRow(action.id, actionRange, displayName, rowView);
        bindingRows.Add(row);
        rowViews.Add(rowView);
        InitializeRow(row);
    }

    /// <summary>
    /// Initializes one row view with its labels and keyboard callbacks.
    /// </summary>
    private void InitializeRow(BindingRow row)
    {
        row.View.Initialize(
            row.DisplayName,
            GetBindingLabel(row, BindingSlot.Primary),
            GetBindingLabel(row, BindingSlot.Secondary),
            () => BeginListening(row, BindingSlot.Primary),
            () => BeginListening(row, BindingSlot.Secondary));
    }

    /// <summary>
    /// Refreshes every row view with the current Rewired keyboard bindings.
    /// </summary>
    private void RefreshRows()
    {
        RefreshPlayer();

        for (int i = 0; i < bindingRows.Count; i++)
        {
            BindingRow row = bindingRows[i];
            row.View.Refresh(
                GetBindingLabel(row, BindingSlot.Primary),
                GetBindingLabel(row, BindingSlot.Secondary));
        }
    }

    /// <summary>
    /// Removes runtime row instances created by this mapper.
    /// </summary>
    private void ClearRows()
    {
        StopListening();
        bindingRows.Clear();

        for (int i = 0; i < rowViews.Count; i++)
        {
            if (rowViews[i] != null)
            {
                Destroy(rowViews[i].gameObject);
            }
        }

        rowViews.Clear();
    }

    /// <summary>
    /// Starts rebinding one row for the requested keyboard slot.
    /// </summary>
    private void BeginListening(BindingRow row, BindingSlot slot)
    {
        StopListening();
        pendingRow = row;
        pendingSlot = slot;
        pendingOriginalMap = CopyActionElementMap(FindActionElementMap(row, slot));
        CacheCancelActionMaps();
        isListeningForBinding = true;
        SetListeningPanel(true, listeningMessage);
        listenCoroutine = StartCoroutine(StartListeningDelayed(row, slot));
    }

    /// <summary>
    /// Starts Rewired input listening after a short realtime delay to avoid capturing the submit click.
    /// </summary>
    private IEnumerator StartListeningDelayed(BindingRow row, BindingSlot slot)
    {
        if (listenStartDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(listenStartDelay);
        }

        RefreshPlayer();
        ControllerMap controllerMap = GetKeyboardMap();

        if (currentPlayer == null || controllerMap == null)
        {
            StopListening();
            RefreshValidation();
            yield break;
        }

        ActionElementMap replaceMap = FindActionElementMap(row, slot);
        inputMapper.Start(
            new InputMapper.Context
            {
                actionId = row.ActionId,
                controllerMap = controllerMap,
                actionRange = row.ActionRange,
                actionElementMapToReplace = replaceMap
            });

        SetUiMapsEnabled(false);
        listenCoroutine = null;
    }

    /// <summary>
    /// Stops any active listening coroutine or Rewired input mapper session.
    /// </summary>
    private void StopListening()
    {
        if (listenCoroutine != null)
        {
            StopCoroutine(listenCoroutine);
            listenCoroutine = null;
        }

        inputMapper.Stop();
        SetUiMapsEnabled(true);
        SetListeningPanel(false, string.Empty);
        isListeningForBinding = false;
        ClearPendingState();
    }

    /// <summary>
    /// Handles a Rewired conflict by storing the first conflicting binding for a swap and allowing replacement.
    /// </summary>
    private void OnConflictFound(InputMapper.ConflictFoundEventData data)
    {
        CaptureFirstConflict(data.assignment);
        data.responseCallback(InputMapper.ConflictResponse.Replace);
    }

    /// <summary>
    /// Handles a successful Rewired input mapping by canceling PauseMenu presses or saving a clean swap.
    /// </summary>
    private void OnInputMapped(InputMapper.InputMappedEventData data)
    {
        pendingMapChanged = true;

        if (IsCancelActionElement(data.actionElementMap))
        {
            RestorePendingBinding();
            FinishListeningWithoutSave();
            return;
        }

        ApplyPendingSwap();
        SaveBindings();
        RefreshRows();
        RefreshValidation();
        FinishListeningWithoutClearingRows();
    }

    /// <summary>
    /// Restores UI map input and validation text after listening ends without a mapping.
    /// </summary>
    private void OnInputMappingStopped(InputMapper.StoppedEventData data)
    {
        if (isListeningForBinding)
        {
            FinishListeningWithoutClearingRows();
            RefreshValidation();
        }
    }

    /// <summary>
    /// Saves user bindings through Rewired's configured user data store when one exists.
    /// </summary>
    private void SaveBindings()
    {
        if (!ReInput.isReady)
        {
            return;
        }

        ControllerMap keyboardMap = GetKeyboardMap();

        if (keyboardMap == null)
        {
            return;
        }

        GameSaveSystem.SaveControlSettings(new ControlSettingsSaveData
        {
            PlayerId = currentPlayer != null ? currentPlayer.id : playerId,
            MapCategory = mapCategory,
            MapLayout = mapLayout,
            KeyboardMapXml = keyboardMap.ToXmlString()
        });
    }

    /// <summary>
    /// Loads saved keyboard bindings into the active Rewired player when compatible data exists.
    /// </summary>
    private void LoadSavedBindings()
    {
        if (!ReInput.isReady || currentPlayer == null)
        {
            return;
        }

        ControlSettingsSaveData controlSettings = GameSaveSystem.LoadControlSettings();

        if (controlSettings == null
            || string.IsNullOrWhiteSpace(controlSettings.KeyboardMapXml)
            || controlSettings.MapCategory != mapCategory
            || controlSettings.MapLayout != mapLayout)
        {
            return;
        }

        ControllerMap keyboardMap = ControllerMap.CreateFromXml(ControllerType.Keyboard, controlSettings.KeyboardMapXml);

        if (keyboardMap != null)
        {
            currentPlayer.controllers.maps.AddMap(ReInput.controllers.Keyboard, keyboardMap);
        }
    }

    /// <summary>
    /// Enables or disables UI maps while assignment polling is active.
    /// </summary>
    private void SetUiMapsEnabled(bool isEnabled)
    {
        if (currentPlayer == null || string.IsNullOrWhiteSpace(uiMapCategory))
        {
            return;
        }

        currentPlayer.controllers.maps.SetMapsEnabled(isEnabled, uiMapCategory);
    }

    /// <summary>
    /// Gets the configured keyboard map for the active player.
    /// </summary>
    private ControllerMap GetKeyboardMap()
    {
        if (currentPlayer == null)
        {
            return null;
        }

        return currentPlayer.controllers.maps.GetMap(ControllerType.Keyboard, KeyboardControllerId, mapCategory, mapLayout);
    }

    /// <summary>
    /// Gets the display label for one keyboard binding slot.
    /// </summary>
    private string GetBindingLabel(BindingRow row, BindingSlot slot)
    {
        ActionElementMap elementMap = FindActionElementMap(row, slot);

        if (elementMap != null)
        {
            return elementMap.elementIdentifierName;
        }

        return slot == BindingSlot.Primary ? unboundLabel : optionalUnboundLabel;
    }

    /// <summary>
    /// Finds the primary or secondary action element map matching the row's action and axis range.
    /// </summary>
    private ActionElementMap FindActionElementMap(BindingRow row, BindingSlot slot)
    {
        return FindActionElementMap(GetKeyboardMap(), row, GetBindingIndex(slot));
    }

    /// <summary>
    /// Finds the indexed action element map matching the row's action and axis range.
    /// </summary>
    private static ActionElementMap FindActionElementMap(ControllerMap controllerMap, BindingRow row, int bindingIndex)
    {
        if (controllerMap == null)
        {
            return null;
        }

        int currentIndex = 0;

        foreach (ActionElementMap elementMap in controllerMap.ElementMapsWithAction(row.ActionId))
        {
            if (elementMap == null || !elementMap.ShowInField(row.ActionRange))
            {
                continue;
            }

            if (currentIndex == bindingIndex)
            {
                return elementMap;
            }

            currentIndex++;
        }

        return null;
    }

    /// <summary>
    /// Gets the zero-based binding index represented by a UI slot.
    /// </summary>
    private static int GetBindingIndex(BindingSlot slot)
    {
        return slot == BindingSlot.Primary ? PrimaryBindingIndex : SecondaryBindingIndex;
    }

    /// <summary>
    /// Checks whether every generated binding row has at least one keyboard binding.
    /// </summary>
    private bool HasAllRequiredBindings()
    {
        return string.IsNullOrEmpty(GetMissingBindingsText());
    }

    /// <summary>
    /// Builds a comma-separated list of row labels that have no keyboard bindings.
    /// </summary>
    private string GetMissingBindingsText()
    {
        if (bindingRows.Count == 0)
        {
            RefreshPlayer();
            BuildRows();
        }

        string missingActions = string.Empty;

        for (int i = 0; i < bindingRows.Count; i++)
        {
            if (HasAnyBinding(bindingRows[i]))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(missingActions))
            {
                missingActions += ", ";
            }

            missingActions += bindingRows[i].DisplayName;
        }

        return missingActions;
    }

    /// <summary>
    /// Checks whether a binding row has any keyboard binding.
    /// </summary>
    private bool HasAnyBinding(BindingRow row)
    {
        return FindActionElementMap(row, BindingSlot.Primary) != null || FindActionElementMap(row, BindingSlot.Secondary) != null;
    }

    /// <summary>
    /// Stores the current PauseMenu keyboard bindings so pressing PauseMenu cancels rebinding instead of assigning.
    /// </summary>
    private void CacheCancelActionMaps()
    {
        cancelActionMaps.Clear();

        if (!ReInput.isReady || string.IsNullOrWhiteSpace(cancelActionName))
        {
            return;
        }

        InputAction cancelAction = ReInput.mapping.GetAction(cancelActionName);
        ControllerMap keyboardMap = GetKeyboardMap();

        if (cancelAction == null || keyboardMap == null)
        {
            return;
        }

        foreach (ActionElementMap elementMap in keyboardMap.ElementMapsWithAction(cancelAction.id))
        {
            ActionElementMap copy = CopyActionElementMap(elementMap);

            if (copy != null)
            {
                cancelActionMaps.Add(copy);
            }
        }
    }

    /// <summary>
    /// Gets whether the newly assigned element matches a cached PauseMenu cancel binding.
    /// </summary>
    private bool IsCancelActionElement(ActionElementMap elementMap)
    {
        if (elementMap == null)
        {
            return false;
        }

        for (int i = 0; i < cancelActionMaps.Count; i++)
        {
            if (HasSameKeyboardElement(elementMap, cancelActionMaps[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Captures the first assignment that will be replaced so the old key can be swapped into it.
    /// </summary>
    private void CaptureFirstConflict(ElementAssignmentInfo assignment)
    {
        pendingConflictMap = null;
        pendingConflictControllerMap = null;

        if (currentPlayer == null)
        {
            return;
        }

        ControllerMap keyboardMap = GetKeyboardMap();

        if (keyboardMap == null)
        {
            return;
        }

        ActionElementMap replaceMap = FindActionElementMap(pendingRow, pendingSlot);

        foreach (ActionElementMap elementMap in keyboardMap.ElementMaps)
        {
            if (elementMap == null || elementMap == replaceMap)
            {
                continue;
            }

            if (elementMap.elementType == assignment.elementType
                && elementMap.elementIdentifierId == assignment.elementIdentifier.id
                && elementMap.keyCode == assignment.keyCode
                && elementMap.modifierKeyFlags == assignment.modifierKeyFlags)
            {
                pendingConflictMap = CopyActionElementMap(elementMap);
                pendingConflictControllerMap = keyboardMap;
                return;
            }
        }
    }

    /// <summary>
    /// Applies the previous key to the replaced conflict so two actions never keep the same key.
    /// </summary>
    private void ApplyPendingSwap()
    {
        if (pendingOriginalMap == null || pendingConflictMap == null || pendingConflictControllerMap == null)
        {
            return;
        }

        pendingConflictControllerMap.ReplaceOrCreateElementMap(CreateSwapAssignment(pendingConflictMap, pendingOriginalMap));
    }

    /// <summary>
    /// Restores the selected binding slot and PauseMenu bindings after a cancel.
    /// </summary>
    private void RestorePendingBinding()
    {
        if (!pendingMapChanged || pendingRow == null)
        {
            return;
        }

        DeleteBindingSlot(pendingRow, pendingSlot);

        if (pendingOriginalMap != null)
        {
            ControllerMap keyboardMap = GetKeyboardMap();

            if (keyboardMap != null)
            {
                keyboardMap.ReplaceOrCreateElementMap(CreateAssignmentFromMap(pendingOriginalMap));
            }
        }

        RestoreCancelActionMaps();
    }

    /// <summary>
    /// Restores the cached PauseMenu bindings if a canceled assignment removed them.
    /// </summary>
    private void RestoreCancelActionMaps()
    {
        ControllerMap keyboardMap = GetKeyboardMap();

        if (keyboardMap == null)
        {
            return;
        }

        for (int i = 0; i < cancelActionMaps.Count; i++)
        {
            if (FindMatchingKeyboardElementMap(keyboardMap, cancelActionMaps[i]) == null)
            {
                keyboardMap.ReplaceOrCreateElementMap(CreateAssignmentFromMap(cancelActionMaps[i]));
            }
        }
    }

    /// <summary>
    /// Deletes the current action element map shown in one UI binding slot.
    /// </summary>
    private void DeleteBindingSlot(BindingRow row, BindingSlot slot)
    {
        ControllerMap keyboardMap = GetKeyboardMap();
        ActionElementMap elementMap = FindActionElementMap(row, slot);

        if (keyboardMap != null && elementMap != null)
        {
            keyboardMap.DeleteElementMap(elementMap.id);
        }
    }

    /// <summary>
    /// Finds an element map that uses the same keyboard element as the template.
    /// </summary>
    private static ActionElementMap FindMatchingKeyboardElementMap(ControllerMap keyboardMap, ActionElementMap template)
    {
        if (keyboardMap == null || template == null)
        {
            return null;
        }

        foreach (ActionElementMap elementMap in keyboardMap.ElementMaps)
        {
            if (HasSameKeyboardElement(elementMap, template))
            {
                return elementMap;
            }
        }

        return null;
    }

    /// <summary>
    /// Creates a copy of one Rewired action element map when a source exists.
    /// </summary>
    private static ActionElementMap CopyActionElementMap(ActionElementMap source)
    {
        return source != null ? new ActionElementMap(source) : null;
    }

    /// <summary>
    /// Creates an assignment using the same action and keyboard element as the source map.
    /// </summary>
    private static ElementAssignment CreateAssignmentFromMap(ActionElementMap source)
    {
        return ElementAssignment.CompleteAssignment(
            ControllerType.Keyboard,
            source.elementType,
            source.elementIdentifierId,
            source.axisRange,
            source.keyCode,
            source.modifierKeyFlags,
            source.actionId,
            source.axisContribution,
            source.invert);
    }

    /// <summary>
    /// Creates a swap assignment that keeps the conflict action but uses the replaced key.
    /// </summary>
    private static ElementAssignment CreateSwapAssignment(ActionElementMap conflict, ActionElementMap replacement)
    {
        return ElementAssignment.CompleteAssignment(
            ControllerType.Keyboard,
            replacement.elementType,
            replacement.elementIdentifierId,
            replacement.axisRange,
            replacement.keyCode,
            replacement.modifierKeyFlags,
            conflict.actionId,
            conflict.axisContribution,
            conflict.invert);
    }

    /// <summary>
    /// Checks whether two Rewired keyboard maps refer to the same physical key and modifiers.
    /// </summary>
    private static bool HasSameKeyboardElement(ActionElementMap left, ActionElementMap right)
    {
        return left != null
            && right != null
            && left.elementType == right.elementType
            && left.elementIdentifierId == right.elementIdentifierId
            && left.keyCode == right.keyCode
            && left.modifierKeyFlags == right.modifierKeyFlags;
    }

    /// <summary>
    /// Finishes an input mapping cancel without saving the canceled change.
    /// </summary>
    private void FinishListeningWithoutSave()
    {
        inputMapper.Stop();
        RefreshRows();
        RefreshValidation();
        FinishListeningWithoutClearingRows();
    }

    /// <summary>
    /// Clears listening state while keeping generated binding rows.
    /// </summary>
    private void FinishListeningWithoutClearingRows()
    {
        if (listenCoroutine != null)
        {
            StopCoroutine(listenCoroutine);
            listenCoroutine = null;
        }

        SetUiMapsEnabled(true);
        SetListeningPanel(false, string.Empty);
        isListeningForBinding = false;
        ClearPendingState();
    }

    /// <summary>
    /// Clears cached data for the current binding operation.
    /// </summary>
    private void ClearPendingState()
    {
        pendingRow = null;
        pendingOriginalMap = null;
        pendingConflictMap = null;
        pendingConflictControllerMap = null;
        pendingMapChanged = false;
        cancelActionMaps.Clear();
    }

    /// <summary>
    /// Shows or hides the listening message panel.
    /// </summary>
    private void SetListeningPanel(bool isVisible, string message)
    {
        if (listeningPanelRoot != null)
        {
            listeningPanelRoot.SetActive(isVisible);
        }

        if (listeningPanelMessageText != null)
        {
            listeningPanelMessageText.text = message;
        }
    }

    /// <summary>
    /// Gets a readable action label for button actions.
    /// </summary>
    private static string GetActionDisplayName(InputAction action)
    {
        if (!string.IsNullOrWhiteSpace(action.descriptiveName))
        {
            return action.descriptiveName;
        }

        return action.name;
    }

    /// <summary>
    /// Gets a readable label for one positive or negative axis row.
    /// </summary>
    private static string GetAxisDisplayName(InputAction action, bool isPositive)
    {
        string axisName = isPositive ? action.positiveDescriptiveName : action.negativeDescriptiveName;

        if (!string.IsNullOrWhiteSpace(axisName))
        {
            return axisName;
        }

        return GetActionDisplayName(action) + (isPositive ? " +" : " -");
    }

    private sealed class BindingRow
    {
        public readonly int ActionId;
        public readonly AxisRange ActionRange;
        public readonly string DisplayName;
        public readonly CustomControlBindingRow View;

        /// <summary>
        /// Stores immutable data for one generated binding row.
        /// </summary>
        public BindingRow(int actionId, AxisRange actionRange, string displayName, CustomControlBindingRow view)
        {
            ActionId = actionId;
            ActionRange = actionRange;
            DisplayName = displayName;
            View = view;
        }
    }
}
