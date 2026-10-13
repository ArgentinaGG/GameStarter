using UnityEditor;
using gg.argentina.gamestarter.Localization;
using UnityPackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace gg.argentina.gamestarter.Editor
{
    public sealed class PackageHubAboutWindow : EditorWindow
    {
        private void OnGUI()
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
    }
}
