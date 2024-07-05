using Insthync.AddressableAssetTools;

namespace LiteNetLibManager
{
    public static class AssetReferenceSceneExtensions
    {
        public static bool IsSameSceneName(this AssetReferenceScene scene, string sceneName)
        {
            return scene.IsDataValid() && string.Equals(scene.SceneName, sceneName);
        }

        public static ServerSceneInfo GetServerSceneInfo(this AssetReferenceScene scene)
        {
            return new ServerSceneInfo()
            {
                isAddressable = true,
                addressableKey = scene.RuntimeKey as string,
                sceneName = scene.SceneName,
            };
        }
    }
}
