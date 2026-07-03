using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public static class GameLocalization
{
    private const string PlayerPrefsLanguageKey = "Breezeblocks.Localization.Language";
    private const string ResourceFolder = "Localization/";
    private const GameLanguage DefaultLanguage = GameLanguage.PtBr;

    private static readonly Dictionary<string, string> LocalizedValues = new Dictionary<string, string>();
    private static bool isInitialized;
    private static GameLanguage currentLanguage;

    /// <summary>
    /// Fires whenever the active language changes and visible localized UI should refresh.
    /// </summary>
    public static event Action LanguageChanged;

    /// <summary>
    /// Gets the active language, loading saved player preference on first access.
    /// </summary>
    public static GameLanguage CurrentLanguage
    {
        get
        {
            EnsureInitialized();
            return currentLanguage;
        }
    }

    /// <summary>
    /// Sets the active language, saves the preference, and refreshes registered localized UI.
    /// </summary>
    public static void SetLanguage(GameLanguage language)
    {
        EnsureInitialized();

        if (currentLanguage == language)
        {
            return;
        }

        currentLanguage = language;
        PlayerPrefs.SetString(PlayerPrefsLanguageKey, GetLanguageCode(language));
        PlayerPrefs.Save();
        LoadLanguage(language);
        LanguageChanged?.Invoke();
    }

    /// <summary>
    /// Gets localized text for a key, falling back to the provided text or the key itself.
    /// </summary>
    public static string Get(string key, string fallback)
    {
        EnsureInitialized();

        if (string.IsNullOrWhiteSpace(key))
        {
            return fallback ?? string.Empty;
        }

        if (LocalizedValues.TryGetValue(key, out string localizedValue) && !string.IsNullOrEmpty(localizedValue))
        {
            return localizedValue;
        }

        return string.IsNullOrEmpty(fallback) ? key : fallback;
    }

    /// <summary>
    /// Gets localized text for a key, falling back to the key when no entry exists.
    /// </summary>
    public static string Get(string key)
    {
        return Get(key, key);
    }

    /// <summary>
    /// Formats a localized template with invariant numeric formatting for stable UI output.
    /// </summary>
    public static string Format(string key, string fallback, params object[] arguments)
    {
        string template = Get(key, fallback);

        try
        {
            return string.Format(CultureInfo.InvariantCulture, template, arguments);
        }
        catch (FormatException)
        {
            return template;
        }
    }

    /// <summary>
    /// Loads saved language preference and its JSON table when localization is first used.
    /// </summary>
    private static void EnsureInitialized()
    {
        if (isInitialized)
        {
            return;
        }

        currentLanguage = ParseLanguage(PlayerPrefs.GetString(PlayerPrefsLanguageKey, GetLanguageCode(DefaultLanguage)));
        LoadLanguage(currentLanguage);
        isInitialized = true;
    }

    /// <summary>
    /// Loads one language JSON file from Resources into the runtime lookup table.
    /// </summary>
    private static void LoadLanguage(GameLanguage language)
    {
        LocalizedValues.Clear();
        TextAsset languageAsset = Resources.Load<TextAsset>(ResourceFolder + GetLanguageCode(language));

        if (languageAsset == null)
        {
            Debug.LogWarning($"Localization file missing for {language}.");
            return;
        }

        LocalizationFile localizationFile = JsonUtility.FromJson<LocalizationFile>(languageAsset.text);

        if (localizationFile?.entries == null)
        {
            return;
        }

        for (int i = 0; i < localizationFile.entries.Length; i++)
        {
            LocalizationEntry entry = localizationFile.entries[i];

            if (entry == null || string.IsNullOrWhiteSpace(entry.key))
            {
                continue;
            }

            LocalizedValues[entry.key] = entry.value ?? string.Empty;
        }
    }

    /// <summary>
    /// Converts saved language code into the supported enum value.
    /// </summary>
    private static GameLanguage ParseLanguage(string languageCode)
    {
        return string.Equals(languageCode, "en-US", StringComparison.OrdinalIgnoreCase)
            ? GameLanguage.EnUs
            : GameLanguage.PtBr;
    }

    /// <summary>
    /// Converts the language enum into the Resources JSON file name.
    /// </summary>
    private static string GetLanguageCode(GameLanguage language)
    {
        return language == GameLanguage.EnUs ? "en-US" : "pt-BR";
    }

    [Serializable]
    private sealed class LocalizationFile
    {
        public LocalizationEntry[] entries;
    }

    [Serializable]
    private sealed class LocalizationEntry
    {
        public string key;
        public string value;
    }
}
