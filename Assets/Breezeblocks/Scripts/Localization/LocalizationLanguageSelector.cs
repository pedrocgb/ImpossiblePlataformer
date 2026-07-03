using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Dropdown))]
public sealed class LocalizationLanguageSelector : MonoBehaviour
{
    private const string PortugueseLabel = "PT-BR";
    private const string EnglishLabel = "EN-US";

    private TMP_Dropdown dropdown;

    /// <summary>
    /// Caches the same-object dropdown and builds its language options.
    /// </summary>
    private void Awake()
    {
        dropdown = GetComponent<TMP_Dropdown>();
        ConfigureDropdown();
    }

    /// <summary>
    /// Hooks dropdown changes when this selector is active.
    /// </summary>
    private void OnEnable()
    {
        EnsureDropdown();
        dropdown.onValueChanged.AddListener(SetLanguageByIndex);
        SyncDropdownToLanguage();
    }

    /// <summary>
    /// Removes dropdown listeners when this selector is disabled.
    /// </summary>
    private void OnDisable()
    {
        if (dropdown != null)
        {
            dropdown.onValueChanged.RemoveListener(SetLanguageByIndex);
        }
    }

    /// <summary>
    /// Selects Portuguese Brazil from UI events.
    /// </summary>
    public void UsePortugueseBrazil()
    {
        GameLocalization.SetLanguage(GameLanguage.PtBr);
        SyncDropdownToLanguage();
    }

    /// <summary>
    /// Selects English United States from UI events.
    /// </summary>
    public void UseEnglishUnitedStates()
    {
        GameLocalization.SetLanguage(GameLanguage.EnUs);
        SyncDropdownToLanguage();
    }

    /// <summary>
    /// Selects language by dropdown option index.
    /// </summary>
    public void SetLanguageByIndex(int optionIndex)
    {
        GameLocalization.SetLanguage(optionIndex == 1 ? GameLanguage.EnUs : GameLanguage.PtBr);
    }

    /// <summary>
    /// Rebuilds dropdown options for supported languages.
    /// </summary>
    private void ConfigureDropdown()
    {
        EnsureDropdown();

        if (dropdown == null)
        {
            return;
        }

        dropdown.ClearOptions();
        dropdown.options.Add(new TMP_Dropdown.OptionData(PortugueseLabel));
        dropdown.options.Add(new TMP_Dropdown.OptionData(EnglishLabel));
        SyncDropdownToLanguage();
    }

    /// <summary>
    /// Updates selected dropdown value to match the saved localization language.
    /// </summary>
    private void SyncDropdownToLanguage()
    {
        EnsureDropdown();

        if (dropdown == null)
        {
            return;
        }

        int optionIndex = GameLocalization.CurrentLanguage == GameLanguage.EnUs ? 1 : 0;
        dropdown.SetValueWithoutNotify(optionIndex);
        dropdown.RefreshShownValue();
    }

    /// <summary>
    /// Caches the dropdown if another method runs before Awake.
    /// </summary>
    private void EnsureDropdown()
    {
        if (dropdown == null)
        {
            dropdown = GetComponent<TMP_Dropdown>();
        }
    }
}
