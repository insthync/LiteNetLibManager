namespace LiteNetLibManager
{
    [System.Serializable]
    public class AssetReferenceLiteNetLibIdentity : ComponentReference<LiteNetLibIdentity>
    {
        public AssetReferenceLiteNetLibIdentity(string guid) : base(guid)
        {
        }
    }

    public static class AssetReferenceLiteNetLibIdentityExtensions
    {
        public static bool IsDataValid(this AssetReferenceLiteNetLibIdentity asset)
        {
            return asset != null && asset.RuntimeKeyIsValid();
        }

        public static int GetHashedId(this AssetReferenceLiteNetLibIdentity asset)
        {
            if (!asset.IsDataValid())
                return 0;
            // Generate hashed ID by runtime key
            string id = asset.RuntimeKey.ToString();
            unchecked
            {
                int hash1 = 5381;
                int hash2 = hash1;

                for (int i = 0; i < id.Length && id[i] != '\0'; i += 2)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ id[i];
                    if (i == id.Length - 1 || id[i + 1] == '\0')
                        break;
                    hash2 = ((hash2 << 5) + hash2) ^ id[i + 1];
                }
                return hash1 + (hash2 * 1566083941);
            }
        }
    }
}