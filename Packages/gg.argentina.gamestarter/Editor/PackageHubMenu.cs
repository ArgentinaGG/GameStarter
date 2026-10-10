using UnityEditor;

namespace gg.argentina.gamestarter.Editor
{
    public static class PackageHubMenu
    {
        [MenuItem("GameStarter/Package Hub")]
        public static void OpenPackageHub()
        {
            PackageHubWindow.Open();
        }
    }
}
