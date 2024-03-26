using UnityEngine.AddressableAssets;

namespace LiteNetLibManager
{
    public static class AssetReferenceUtils
    {
        public static bool IsDataValid(this AssetReference asset)
        {
            return asset != null && asset.RuntimeKeyIsValid();
        }
    }
}