using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace gg.argentina.gamestarter.Localization
{
    [DisallowMultipleComponent]
    public sealed class LocalizationModule : MonoBehaviour
    {
        [SerializeField] private LocalizationConfig configuration;

        private readonly Dictionary<string, Dictionary<string, string>> translations = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, string> selectedTranslations;

        public static LocalizationModule Instance { get; private set; }
        public event Action<string> LocaleChanged;

        public string SelectedLocaleCode { get; private set; }
        public IReadOnlyList<LocalizationLanguage> Languages
        {
            get
            {
                EnsureInitialized();
                return configuration.Languages;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            try
            {
                Initialize();
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is JsonException || exception is FormatException)
            {
                Debug.LogError($"Localization initialization failed: {exception.Message}", this);
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void SetLocale(string localeCode)
        {
            EnsureInitialized();

            if (string.IsNullOrWhiteSpace(localeCode))
                throw new ArgumentException("A locale code is required.", nameof(localeCode));

            foreach (LocalizationLanguage language in configuration.Languages)
            {
                if (!string.Equals(language.LocaleCode, localeCode, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.Equals(SelectedLocaleCode, language.LocaleCode, StringComparison.OrdinalIgnoreCase))
                    return;

                SelectedLocaleCode = language.LocaleCode;
                selectedTranslations = translations[language.LocaleCode];
                LocaleChanged?.Invoke(SelectedLocaleCode);
                return;
            }

            throw new ArgumentException($"Locale '{localeCode}' is not configured.", nameof(localeCode));
        }

        public string GetText(string key)
        {
            EnsureInitialized();

            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("A localization key is required.", nameof(key));

            if (selectedTranslations.TryGetValue(key, out string value))
                return value;

            if (translations.TryGetValue(configuration.DefaultLocaleCode, out Dictionary<string, string> defaultTranslations) &&
                defaultTranslations.TryGetValue(key, out value))
            {
                return value;
            }

            return key;
        }

        private void Initialize()
        {
            if (configuration == null)
                throw new InvalidOperationException("Assign a LocalizationConfig to the LocalizationModule.");

            if (transform.parent != null)
                throw new InvalidOperationException("LocalizationModule must be on a root GameObject to persist between scenes.");

            configuration.Validate();

            foreach (LocalizationLanguage language in configuration.Languages)
                translations.Add(language.LocaleCode, ParseTranslations(language));

            SelectedLocaleCode = configuration.DefaultLocaleCode;
            selectedTranslations = translations[SelectedLocaleCode];
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private static Dictionary<string, string> ParseTranslations(LocalizationLanguage language)
        {
            JObject document = JObject.Parse(language.Translations.text);
            var entries = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (JProperty property in document.Properties())
            {
                if (string.IsNullOrWhiteSpace(property.Name) || property.Value.Type != JTokenType.String)
                    throw new FormatException($"Translation entry '{property.Name}' in '{language.Translations.name}' must have a non-empty key and a string value.");

                entries.Add(property.Name, property.Value.Value<string>());
            }

            return entries;
        }

        private void EnsureInitialized()
        {
            if (configuration == null || selectedTranslations == null)
                throw new InvalidOperationException("LocalizationModule has not been initialized.");
        }
    }
}
