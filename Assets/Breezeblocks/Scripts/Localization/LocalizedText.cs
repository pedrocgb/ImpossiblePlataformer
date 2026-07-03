using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public sealed class LocalizedText : MonoBehaviour
{
    [Title("Localization")]
    [SerializeField]
    private string localizationKey;

    private TMP_Text targetText;
    private string runtimeFallbackText;

    /// <summary>
    /// Caches the same-object TMP text component.
    /// </summary>
    private void Awake()
    {
        targetText = GetComponent<TMP_Text>();
    }

    /// <summary>
    /// Refreshes text and subscribes to language changes while visible.
    /// </summary>
    private void OnEnable()
    {
        GameLocalization.LanguageChanged += Refresh;
        Refresh();
    }

    /// <summary>
    /// Stops listening for language changes when this text is disabled.
    /// </summary>
    private void OnDisable()
    {
        GameLocalization.LanguageChanged -= Refresh;
    }

    /// <summary>
    /// Assigns a localization key at runtime and refreshes the visible label.
    /// </summary>
    public void SetKey(string key, string fallback)
    {
        localizationKey = key;
        runtimeFallbackText = fallback;
        Refresh();
    }

    /// <summary>
    /// Applies the current localized value to the TMP text component.
    /// </summary>
    [Button]
    public void Refresh()
    {
        EnsureTargetText();

        if (targetText != null)
        {
            targetText.text = GameLocalization.Get(localizationKey, runtimeFallbackText);
        }
    }

    /// <summary>
    /// Caches the TMP text component when called before Awake.
    /// </summary>
    private void EnsureTargetText()
    {
        if (targetText == null)
        {
            targetText = GetComponent<TMP_Text>();
        }
    }
}
