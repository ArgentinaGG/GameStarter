using System;
using System.Collections.Generic;
using UnityEngine;

namespace gg.argentina.gamestarter.Localization
{
    public sealed class LocalizationConfig : ScriptableObject
    {
        [SerializeField] private string defaultLocaleCode = "en";
        [SerializeField] private List<LocalizationLanguage> languages = new();

        public string DefaultLocaleCode => defaultLocaleCode;
        public IReadOnlyList<LocalizationLanguage> Languages => languages;

        public void Validate()
        {
            if (languages == null || languages.Count == 0)
                throw new InvalidOperationException("Localization configuration must contain at least one language.");

            var localeCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (LocalizationLanguage language in languages)
            {
                if (language == null || string.IsNullOrWhiteSpace(language.LocaleCode))
                    throw new InvalidOperationException("Each language must have a locale code.");

                if (language.LocaleCode != language.LocaleCode.Trim() || language.LocaleCode.Contains("_") || language.LocaleCode.Contains(" "))
                    throw new InvalidOperationException($"Use a hyphen-separated locale code without whitespace: '{language.LocaleCode}'.");

                if (!localeCodes.Add(language.LocaleCode))
                    throw new InvalidOperationException($"Duplicate locale code: '{language.LocaleCode}'.");

                if (string.IsNullOrWhiteSpace(language.DisplayName))
                    throw new InvalidOperationException($"Language '{language.LocaleCode}' must have a display name.");

                if (language.Translations == null)
                    throw new InvalidOperationException($"Language '{language.LocaleCode}' must have a JSON translation file assigned.");
            }

            if (string.IsNullOrWhiteSpace(defaultLocaleCode) || !localeCodes.Contains(defaultLocaleCode))
                throw new InvalidOperationException("The default locale must match a configured language.");
        }
    }

    [Serializable]
    public sealed class LocalizationLanguage
    {
        [SerializeField] private string localeCode;
        [SerializeField] private string displayName;
        [SerializeField] private TextAsset translations;

        public string LocaleCode => localeCode;
        public string DisplayName => displayName;
        public TextAsset Translations => translations;
    }
}
