using System.Text;
using Rewired;
using Rewired.UI.ControlMapper;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

public sealed class RewiredControlMapperSettingsBridge : MonoBehaviour
{
    [Title("Control Mapper")]
    [SerializeField]
    private ControlMapper controlMapper;

    [SerializeField, MinValue(0)]
    private int playerId;

    [SerializeField]
    private string requiredActionCategory = "Default";

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

    /// <summary>
    /// Opens the Rewired Control Mapper when assigned.
    /// </summary>
    public void OpenMapper()
    {
        if (controlMapper != null && !controlMapper.isOpen)
        {
            controlMapper.Open();
        }

        RefreshValidation();
    }

    /// <summary>
    /// Closes the Rewired Control Mapper only when all required actions have bindings.
    /// </summary>
    public bool CloseMapperIfValid()
    {
        bool canClose = HasAllRequiredBindings();
        RefreshValidation();

        if (canClose && controlMapper != null && controlMapper.isOpen)
        {
            controlMapper.Close(true);
        }

        return canClose;
    }

    /// <summary>
    /// Gets whether settings may close without leaving required actions unbound.
    /// </summary>
    public bool CanCloseSettings()
    {
        bool canClose = HasAllRequiredBindings();
        RefreshValidation();
        return canClose;
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
    /// Checks whether every required Rewired action has at least one binding.
    /// </summary>
    private bool HasAllRequiredBindings()
    {
        return string.IsNullOrEmpty(GetMissingBindingsText());
    }

    /// <summary>
    /// Builds a comma-separated list of actions that have no bindings.
    /// </summary>
    private string GetMissingBindingsText()
    {
        if (!ReInput.isReady)
        {
            return string.Empty;
        }

        Player player = ReInput.players.GetPlayer(playerId);

        if (player == null)
        {
            return string.Empty;
        }

        var mapCategory = ReInput.mapping.GetMapCategory(requiredActionCategory);
        int mapCategoryId = mapCategory != null ? mapCategory.id : 0;
        StringBuilder missingBuilder = new StringBuilder();

        foreach (InputAction action in ReInput.mapping.ActionsInCategory(requiredActionCategory))
        {
            if (HasAnyBinding(player, action.id, mapCategoryId))
            {
                continue;
            }

            if (missingBuilder.Length > 0)
            {
                missingBuilder.Append(", ");
            }

            missingBuilder.Append(GetActionDisplayName(action));
        }

        return missingBuilder.ToString();
    }

    /// <summary>
    /// Checks whether the player has any keyboard, mouse, or joystick binding for an action.
    /// </summary>
    private static bool HasAnyBinding(Player player, int actionId, int mapCategoryId)
    {
        if (HasBindingInMap(player.controllers.maps.GetFirstMapInCategory(ControllerType.Keyboard, 0, mapCategoryId), actionId))
        {
            return true;
        }

        if (HasBindingInMap(player.controllers.maps.GetFirstMapInCategory(ControllerType.Mouse, 0, mapCategoryId), actionId))
        {
            return true;
        }

        for (int i = 0; i < player.controllers.joystickCount; i++)
        {
            Joystick joystick = player.controllers.Joysticks[i];

            if (HasBindingInMap(player.controllers.maps.GetFirstMapInCategory(ControllerType.Joystick, joystick.id, mapCategoryId), actionId))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks whether one controller map has any element mapped to an action.
    /// </summary>
    private static bool HasBindingInMap(ControllerMap controllerMap, int actionId)
    {
        if (controllerMap == null)
        {
            return false;
        }

        foreach (ActionElementMap elementMap in controllerMap.ElementMapsWithAction(actionId))
        {
            if (elementMap != null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets a readable action label for validation text.
    /// </summary>
    private static string GetActionDisplayName(InputAction action)
    {
        if (!string.IsNullOrWhiteSpace(action.descriptiveName))
        {
            return action.descriptiveName;
        }

        return action.name;
    }
}
