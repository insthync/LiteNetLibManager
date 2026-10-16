using System;
using Cysharp.Threading.Tasks;

namespace LiteNetLibManager
{
    /// <summary>Optional server dependencies held until the prefab catalog is cleared.</summary>
    public interface ILiteNetLibPrefabAssetsPreloader
    {
        UniTask<IDisposable> PreloadPrefabAssets();
    }
}
