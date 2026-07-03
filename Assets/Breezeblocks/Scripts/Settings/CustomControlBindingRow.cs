using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed class CustomControlBindingRow : MonoBehaviour
{
    [Title("Labels")]
    [SerializeField]
    private TMP_Text actionLabel;

    [SerializeField]
    private TMP_Text primaryBindingLabel;

    [SerializeField]
    private TMP_Text secondaryBindingLabel;

    [Title("Buttons")]
    [SerializeField]
    private Button primaryBindingButton;

    [SerializeField]
    private Button secondaryBindingButton;

    /// <summary>
    /// Initializes the row with the action name, visible bindings, and rebinding callbacks.
    /// </summary>
    public void Initialize(
        string actionName,
        string primaryBinding,
        string secondaryBinding,
        UnityAction primaryCallback,
        UnityAction secondaryCallback)
    {
        SetText(actionLabel, actionName);
        Refresh(primaryBinding, secondaryBinding);
        ConfigureButton(primaryBindingButton, primaryCallback);
        ConfigureButton(secondaryBindingButton, secondaryCallback);
    }

    /// <summary>
    /// Updates the row labels after keyboard mappings change.
    /// </summary>
    public void Refresh(string primaryBinding, string secondaryBinding)
    {
        SetText(primaryBindingLabel, primaryBinding);
        SetText(secondaryBindingLabel, secondaryBinding);
    }

    /// <summary>
    /// Assigns text when a label reference exists.
    /// </summary>
    private static void SetText(TMP_Text label, string value)
    {
        if (label != null)
        {
            label.text = value;
        }
    }

    /// <summary>
    /// Replaces a button callback for one binding column.
    /// </summary>
    private static void ConfigureButton(Button button, UnityAction callback)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();

        if (callback != null)
        {
            button.onClick.AddListener(callback);
        }

        button.interactable = true;
    }
}
