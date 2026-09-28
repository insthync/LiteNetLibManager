using LiteNetLib.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace LiteNetLibManager
{
    public partial class LiteNetLibGameManager
    {
        // ClientReady is sent after both peers have registered their online-scene assets.
        // This header also makes peers using the older ClientReady format fail explicitly.
        private const int SyncSchemaHeader = 0x4C4E5331; // LNS1
        private const ulong SyncSchemaHashOffset = 14695981039346656037UL;
        private const ulong SyncSchemaHashPrime = 1099511628211UL;

        protected void WriteClientReadyWithSyncSchema(NetDataWriter writer)
        {
            WriteSyncSchemaHeader(writer);
            SerializeClientReadyData(writer);
        }

        private void WriteSyncSchemaHeader(NetDataWriter writer)
        {
            ulong fingerprint = CalculateSyncSchemaFingerprint();
            writer.Put(SyncSchemaHeader);
            writer.Put(unchecked((int)fingerprint));
            writer.Put(unchecked((int)(fingerprint >> 32)));
        }

        protected bool ValidateSyncSchemaHeader(NetDataReader reader, string peer)
        {
            if (reader == null || reader.AvailableBytes < 12 || reader.GetInt() != SyncSchemaHeader)
            {
                if (LogError)
                    Logging.LogError(LogTag, $"State sync layout check failed for {peer}: missing or incompatible ClientReady schema header.");
                return false;
            }

            ulong remoteFingerprint = (uint)reader.GetInt() | ((ulong)(uint)reader.GetInt() << 32);
            ulong localFingerprint = CalculateSyncSchemaFingerprint();
            if (remoteFingerprint != localFingerprint)
            {
                if (LogError)
                    Logging.LogError(LogTag, $"State sync layout mismatch for {peer}: local {localFingerprint:X16}, remote {remoteFingerprint:X16}. Both builds must register matching network prefabs, scene objects, behaviours, and sync fields.");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Hash the currently registered network layouts. Override this when spawning identities
        /// from a custom registry so those layouts participate in the ClientReady check.
        /// </summary>
        protected virtual ulong CalculateSyncSchemaFingerprint()
        {
            if (Assets == null)
                throw new InvalidOperationException("LiteNetLibAssets is required to calculate the state sync layout.");

            ulong hash = SyncSchemaHashOffset;
            var fieldCache = new Dictionary<Type, List<FieldInfo>>();
            HashRegisteredLayouts(ref hash, Assets.GuidToPrefabs, 1, fieldCache);
            HashRegisteredLayouts(ref hash, Assets.SceneObjects, 2, fieldCache);
            return hash;
        }

        private static void HashRegisteredLayouts(ref ulong hash, Dictionary<int, LiteNetLibIdentity> identities,
            byte category, Dictionary<Type, List<FieldInfo>> fieldCache)
        {
            HashByte(ref hash, category);
            HashInt(ref hash, identities.Count);
            var keys = new List<int>(identities.Keys);
            keys.Sort();
            foreach (int key in keys)
            {
                LiteNetLibIdentity identity = identities[key];
                HashInt(ref hash, key);
                if (identity == null)
                {
                    HashByte(ref hash, 0);
                    continue;
                }
                HashByte(ref hash, 1);
                HashString(ref hash, category == 1 ? identity.AssetId : identity.SceneObjectId);

                // Include inactive children so a difference cannot be hidden by prefab state.
                LiteNetLibBehaviour[] behaviours = identity.GetComponentsInChildren<LiteNetLibBehaviour>(true);
                HashInt(ref hash, behaviours.Length);
                foreach (LiteNetLibBehaviour behaviour in behaviours)
                {
                    Type behaviourType = behaviour.GetType();
                    HashString(ref hash, behaviourType.FullName);
                    LiteNetLibBehaviour.GetCachedElements(behaviourType, fieldCache, out List<FieldInfo> fields);
                    var orderedFields = new List<FieldInfo>(fields);
                    orderedFields.Sort((left, right) => StringComparer.Ordinal.Compare(left.Name, right.Name));
                    HashInt(ref hash, orderedFields.Count);
                    foreach (FieldInfo field in orderedFields)
                    {
                        HashString(ref hash, field.Name);
                        HashString(ref hash, field.FieldType.FullName);
                        LiteNetLibSyncElement element = (LiteNetLibSyncElement)field.GetValue(behaviour);
                        HashByte(ref hash, element == null ? (byte)0 : (byte)1);
                        if (element != null)
                            HashString(ref hash, element.GetType().FullName);
                    }
                }
            }
        }

        private static void HashString(ref ulong hash, string value)
        {
            if (value == null)
            {
                HashInt(ref hash, -1);
                return;
            }
            HashInt(ref hash, value.Length);
            foreach (char character in value)
            {
                HashByte(ref hash, (byte)character);
                HashByte(ref hash, (byte)(character >> 8));
            }
        }

        private static void HashInt(ref ulong hash, int value)
        {
            for (int i = 0; i < 4; ++i)
                HashByte(ref hash, (byte)(value >> (i * 8)));
        }

        private static void HashByte(ref ulong hash, byte value)
        {
            unchecked
            {
                hash = (hash ^ value) * SyncSchemaHashPrime;
            }
        }
    }
}
