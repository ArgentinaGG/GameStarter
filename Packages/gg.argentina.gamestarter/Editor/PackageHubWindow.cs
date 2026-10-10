using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using gg.argentina.gamestarter.Localization;
using UnityPackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace gg.argentina.gamestarter.Editor
{
    public sealed class PackageHubWindow : EditorWindow
    {
        private const string LocalizationFolder = "Assets/GameStarter/Localization";
        private const string TranslationsFolder = LocalizationFolder + "/Translations";
        private const string ConfigPath = LocalizationFolder + "/LocalizationConfig.asset";
        private const string EnglishPath = TranslationsFolder + "/en.json";
        private const string SpanishPath = TranslationsFolder + "/es.json";

        public static void Open()
        {
            GetWindow<PackageHubWindow>("GameStarter");
        }

        private void OnGUI()
        {
            DrawPackageInformation();
            EditorGUILayout.Space();
            DrawLocalizationModule();
        }

        private static void DrawPackageInformation()
        {
            EditorGUILayout.LabelField("GameStarter", EditorStyles.largeLabel);
            UnityPackageInfo package = UnityPackageInfo.FindForAssembly(typeof(LocalizationConfig).Assembly);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Package", package?.displayName ?? "GameStarter");
                EditorGUILayout.LabelField("Version", package?.version ?? "Unknown");
                EditorGUILayout.LabelField("Package ID", package?.packageId ?? "Unknown");
                EditorGUILayout.HelpBox(package?.description ?? "Reusable modules and tools for Unity games.", MessageType.None);
            }
        }

        private void DrawLocalizationModule()
        {
            EditorGUILayout.LabelField("Localization", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("The runtime module is included with the package. Install a game-owned configuration and English/Spanish JSON examples to get started.", MessageType.Info);

            LocalizationConfig config = AssetDatabase.LoadAssetAtPath<LocalizationConfig>(ConfigPath);
            if (config != null)
            {
                LocalizationModule module = FindModuleInScene(EditorSceneManager.GetActiveScene());
                if (module == null)
                {
                    EditorGUILayout.HelpBox("Create a root GameObject in the active scene, add LocalizationModule, and assign this config.", MessageType.None);
                    if (GUILayout.Button("Create GameObject with Localization Module"))
                        CreateLocalizationGameObject(config);
                }
                else if (GetAssignedConfig(module) == null)
                {
                    EditorGUILayout.HelpBox("A LocalizationModule already exists in the active scene but has no config assigned.", MessageType.Warning);
                    if (GUILayout.Button("Assign Installed Localization Config"))
                        AssignConfigToModule(module, config);
                }
                else
                {
                    EditorGUILayout.HelpBox("A LocalizationModule already exists in the active scene. No duplicate will be created.", MessageType.None);
                    if (GUILayout.Button("Select Existing Localization Module"))
                    {
                        Selection.activeGameObject = module.gameObject;
                        EditorGUIUtility.PingObject(module);
                    }
                }

                if (GUILayout.Button("Select Localization Config"))
                {
                    Selection.activeObject = config;
                    EditorGUIUtility.PingObject(config);
                }

                return;
            }

            if (GUILayout.Button("Install Localization Files"))
                InstallLocalizationFiles();
        }

        private static void CreateLocalizationGameObject(LocalizationConfig config)
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            LocalizationModule existingModule = FindModuleInScene(scene);
            if (existingModule != null)
            {
                if (GetAssignedConfig(existingModule) == null)
                    AssignConfigToModule(existingModule, config);

                Selection.activeGameObject = existingModule.gameObject;
                EditorGUIUtility.PingObject(existingModule);
                return;
            }

            var gameObject = new GameObject("Localization");
            Undo.RegisterCreatedObjectUndo(gameObject, "Create Localization GameObject");
            LocalizationModule module = Undo.AddComponent<LocalizationModule>(gameObject);
            Undo.RecordObject(module, "Assign Localization Config");
            var serializedModule = new SerializedObject(module);
            serializedModule.FindProperty("configuration").objectReferenceValue = config;
            serializedModule.ApplyModifiedProperties();
            EditorUtility.SetDirty(module);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = gameObject;
            EditorGUIUtility.PingObject(module);
        }

        private static LocalizationConfig GetAssignedConfig(LocalizationModule module)
        {
            return new SerializedObject(module).FindProperty("configuration").objectReferenceValue as LocalizationConfig;
        }

        private static void AssignConfigToModule(LocalizationModule module, LocalizationConfig config)
        {
            Undo.RecordObject(module, "Assign Localization Config");
            var serializedModule = new SerializedObject(module);
            serializedModule.FindProperty("configuration").objectReferenceValue = config;
            serializedModule.ApplyModifiedProperties();
            EditorUtility.SetDirty(module);
            EditorSceneManager.MarkSceneDirty(module.gameObject.scene);
        }

        private static LocalizationModule FindModuleInScene(Scene scene)
        {
            foreach (LocalizationModule module in Resources.FindObjectsOfTypeAll<LocalizationModule>())
            {
                if (module != null && module.gameObject.scene == scene)
                    return module;
            }

            return null;
        }

        private static void InstallLocalizationFiles()
        {
            UnityPackageInfo package = UnityPackageInfo.FindForAssembly(typeof(LocalizationConfig).Assembly);
            if (package == null || string.IsNullOrWhiteSpace(package.resolvedPath))
            {
                EditorUtility.DisplayDialog("GameStarter", "Could not locate the installed GameStarter package.", "OK");
                return;
            }

            string sampleFolder = Path.Combine(package.resolvedPath, "Samples~", "Localization");
            string sourceEnglish = Path.Combine(sampleFolder, "en.json");
            string sourceSpanish = Path.Combine(sampleFolder, "es.json");
            if (!File.Exists(sourceEnglish) || !File.Exists(sourceSpanish))
            {
                EditorUtility.DisplayDialog("GameStarter", "The localization example files are missing from the package.", "OK");
                return;
            }

            string absoluteConfigPath = GetAbsoluteAssetPath(ConfigPath);
            string absoluteEnglishPath = GetAbsoluteAssetPath(EnglishPath);
            string absoluteSpanishPath = GetAbsoluteAssetPath(SpanishPath);
            if (File.Exists(absoluteConfigPath) || File.Exists(absoluteEnglishPath) || File.Exists(absoluteSpanishPath))
            {
                EditorUtility.DisplayDialog("GameStarter", "One or more localization install files already exist. Existing game files were not overwritten.", "OK");
                return;
            }

            var createdFiles = new List<string>();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(absoluteEnglishPath));
                File.Copy(sourceEnglish, absoluteEnglishPath);
                createdFiles.Add(absoluteEnglishPath);
                File.Copy(sourceSpanish, absoluteSpanishPath);
                createdFiles.Add(absoluteSpanishPath);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                TextAsset english = AssetDatabase.LoadAssetAtPath<TextAsset>(EnglishPath);
                TextAsset spanish = AssetDatabase.LoadAssetAtPath<TextAsset>(SpanishPath);
                if (english == null || spanish == null)
                    throw new InvalidOperationException("Unity could not import the example translation files.");

                LocalizationConfig config = ScriptableObject.CreateInstance<LocalizationConfig>();
                var serializedConfig = new SerializedObject(config);
                serializedConfig.FindProperty("defaultLocaleCode").stringValue = "en";
                SerializedProperty languages = serializedConfig.FindProperty("languages");
                languages.arraySize = 2;
                SetLanguage(languages.GetArrayElementAtIndex(0), "en", "English", english);
                SetLanguage(languages.GetArrayElementAtIndex(1), "es", "Español", spanish);
                serializedConfig.ApplyModifiedPropertiesWithoutUndo();
                config.Validate();

                AssetDatabase.CreateAsset(config, ConfigPath);
                AssetDatabase.SaveAssets();
                Selection.activeObject = config;
                EditorGUIUtility.PingObject(config);
            }
            catch (Exception exception)
            {
                if (File.Exists(absoluteConfigPath))
                    AssetDatabase.DeleteAsset(ConfigPath);

                foreach (string createdFile in createdFiles)
                {
                    File.Delete(createdFile);
                    File.Delete(createdFile + ".meta");
                }

                AssetDatabase.Refresh();
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("GameStarter", $"Localization files could not be installed: {exception.Message}", "OK");
            }
        }

        private static void SetLanguage(SerializedProperty language, string localeCode, string displayName, TextAsset translations)
        {
            language.FindPropertyRelative("localeCode").stringValue = localeCode;
            language.FindPropertyRelative("displayName").stringValue = displayName;
            language.FindPropertyRelative("translations").objectReferenceValue = translations;
        }

        private static string GetAbsoluteAssetPath(string assetPath)
        {
            string relativePath = assetPath.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(Application.dataPath, relativePath);
        }
    }
}
