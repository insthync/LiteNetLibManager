namespace LiteNetLibManager
{
    [System.Serializable]
    public class AssetReferenceDownloadManagerSettings : AssetReferenceScriptableObject<AddressableAssetDownloadManagerSettings>
    {
        public AssetReferenceDownloadManagerSettings(string guid) : base(guid)
        {
        }
    }
}
