using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using LiteNetLib.Utils;
using UnityEngine.Profiling;

namespace LiteNetLibManager
{
    [DisallowMultipleComponent]
    public sealed class LiteNetLibIdentity : MonoBehaviour
    {
        public static uint HighestObjectId { get; private set; }
        [LiteNetLibReadOnly, SerializeField]
        private string assetId;
        [LiteNetLibReadOnly, SerializeField]
        private uint objectId;
        [LiteNetLibReadOnly, SerializeField]
        private long connectionId;
        [LiteNetLibReadOnly, SerializeField]
        private LiteNetLibGameManager manager;
#if UNITY_EDITOR
        [LiteNetLibReadOnly, SerializeField]
        private List<long> subscriberIds = new List<long>();
#endif
        private bool hasSetupBehaviours;
        internal LiteNetLibBehaviour[] Behaviours { get; private set; }
        internal readonly Dictionary<long, LiteNetLibPlayer> Subscribers = new Dictionary<long, LiteNetLibPlayer>();
        public string AssetId { get { return assetId; } }
        public int HashAssetId
        {
            get
            {
                unchecked
                {
                    int hash1 = 5381;
                    int hash2 = hash1;

                    for (int i = 0; i < AssetId.Length && AssetId[i] != '\0'; i += 2)
                    {
                        hash1 = ((hash1 << 5) + hash1) ^ AssetId[i];
                        if (i == AssetId.Length - 1 || AssetId[i + 1] == '\0')
                            break;
                        hash2 = ((hash2 << 5) + hash2) ^ AssetId[i + 1];
                    }

                    return hash1 + (hash2 * 1566083941);
                }
            }
        }
        public uint ObjectId { get { return objectId; } }
        public long ConnectionId { get { return connectionId; } }
        public LiteNetLibGameManager Manager { get { return manager; } }

        public LiteNetLibPlayer Player
        {
            get
            {
                LiteNetLibPlayer foundPlayer;
                if (Manager == null || !Manager.TryGetPlayer(ConnectionId, out foundPlayer))
                    return null;
                return foundPlayer;
            }
        }

        public bool IsServer
        {
            get { return Manager != null && Manager.IsServer; }
        }

        public bool IsClient
        {
            get { return Manager != null && Manager.IsClient; }
        }

        public bool IsOwnerClient { get; private set; }

        private bool ownerValidated;
        private bool destroyed;
        // Optimize garbage collector
        private int loopCounter;

        internal void NetworkUpdate()
        {
            if (!IsServer || Manager == null)
                return;

            Profiler.BeginSample("LiteNetLibIdentity - Network Update");
            for (loopCounter = 0; loopCounter < Behaviours.Length; ++loopCounter)
            {
                Behaviours[loopCounter].NetworkUpdate();
            }
            Profiler.EndSample();
        }

        #region IDs generate in Editor
#if UNITY_EDITOR
        private void OnValidate()
        {
            SetupIDs();
        }

        [ContextMenu("Reorder Scene Object Id")]
        public void ContextMenuReorderSceneObjectId()
        {
            ReorderSceneObjectId();
        }

        private void AssignAssetID(GameObject prefab)
        {
            string path = AssetDatabase.GetAssetPath(prefab);
            assetId = AssetDatabase.AssetPathToGUID(path);
        }

        private bool ThisIsAPrefab()
        {
#if UNITY_2018_3_OR_NEWER
            return PrefabUtility.IsPartOfPrefabAsset(gameObject);
#else
            PrefabType prefabType = PrefabUtility.GetPrefabType(gameObject);
            if (prefabType == PrefabType.Prefab)
                return true;
            return false;
#endif
        }

        private bool ThisIsASceneObjectWithThatReferencesPrefabAsset(out GameObject prefab)
        {
            prefab = null;
#if UNITY_2018_3_OR_NEWER
            if (!PrefabUtility.IsPartOfNonAssetPrefabInstance(gameObject))
                return false;
#else
            PrefabType prefabType = PrefabUtility.GetPrefabType(gameObject);
            if (prefabType == PrefabType.None)
                return false;
#endif
#if UNITY_2018_2_OR_NEWER
            prefab = (GameObject)PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
#else
            prefab = (GameObject)PrefabUtility.GetPrefabParent(gameObject);
#endif
            if (prefab == null)
            {
                Debug.LogError("Failed to find prefab parent for scene object [name:" + gameObject.name + "]");
                return false;
            }
            return true;
        }

        private void SetupIDs()
        {
            string oldAssetId = assetId;
            uint oldObjectId = objectId;
            GameObject prefab;
            if (ThisIsAPrefab())
            {
                // This is a prefab
                AssignAssetID(gameObject);
                objectId = 0;
            }
            else if (ThisIsASceneObjectWithThatReferencesPrefabAsset(out prefab))
            {
                // This is a scene object with prefab link
                AssignAssetID(prefab);
                if (gameObject.scene == SceneManager.GetActiveScene())
                {
                    // Assign object id if it is in scene
                    ValidateObjectId();
                    if (!Application.isPlaying)
                        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                }
                else
                    objectId = 0;
            }
            else
            {
                // This is a pure scene object (Not a prefab)
                assetId = string.Empty;
                if (gameObject.scene == SceneManager.GetActiveScene())
                {
                    // Assign object id if it is in scene
                    ValidateObjectId();
                    if (!Application.isPlaying)
                        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                }
                else
                    objectId = 0;
            }
            // Do not mark dirty while playing
            if (!Application.isPlaying && (oldAssetId != assetId || oldObjectId != objectId))
                EditorUtility.SetDirty(this);
        }
#endif
        #endregion

        internal LiteNetLibSyncField ProcessSyncField(LiteNetLibElementInfo info, NetDataReader reader, bool isInitial)
        {
            if (info.objectId != ObjectId)
                return null;
            if (info.behaviourIndex >= Behaviours.Length)
                return null;
            return Behaviours[info.behaviourIndex].ProcessSyncField(info, reader, isInitial);
        }

        internal LiteNetLibFunction ProcessNetFunction(LiteNetLibElementInfo info, NetDataReader reader, bool hookCallback)
        {
            if (info.objectId != ObjectId)
                return null;
            if (info.behaviourIndex >= Behaviours.Length)
                return null;
            return Behaviours[info.behaviourIndex].ProcessNetFunction(info, reader, hookCallback);
        }

        internal LiteNetLibSyncList ProcessSyncList(LiteNetLibElementInfo info, NetDataReader reader)
        {
            if (info.objectId != ObjectId)
                return null;
            if (info.behaviourIndex >= Behaviours.Length)
                return null;
            return Behaviours[info.behaviourIndex].ProcessSyncList(info, reader);
        }

        internal LiteNetLibBehaviour ProcessSyncBehaviour(byte behaviourIndex, NetDataReader reader)
        {
            if (behaviourIndex >= Behaviours.Length)
                return null;
            LiteNetLibBehaviour behaviour = Behaviours[behaviourIndex];
            behaviour.Deserialize(reader);
            return behaviour;
        }

        internal bool TryGetBehaviour<T>(byte behaviourIndex, out T behaviour)
            where T : LiteNetLibBehaviour
        {
            behaviour = null;
            if (behaviourIndex >= Behaviours.Length)
                return false;
            behaviour = Behaviours[behaviourIndex] as T;
            return behaviour != null;
        }

        internal void WriteInitialSyncFields(NetDataWriter writer)
        {
            for (loopCounter = 0; loopCounter < Behaviours.Length; ++loopCounter)
            {
                Behaviours[loopCounter].WriteInitialSyncFields(writer);
            }
        }

        internal void ReadInitialSyncFields(NetDataReader reader)
        {
            for (loopCounter = 0; loopCounter < Behaviours.Length; ++loopCounter)
            {
                Behaviours[loopCounter].ReadInitialSyncFields(reader);
            }
        }

        internal void SendInitSyncFields()
        {
            for (loopCounter = 0; loopCounter < Behaviours.Length; ++loopCounter)
            {
                Behaviours[loopCounter].SendInitSyncFields();
            }
        }

        internal void SendInitSyncFields(long connectionId)
        {
            for (loopCounter = 0; loopCounter < Behaviours.Length; ++loopCounter)
            {
                Behaviours[loopCounter].SendInitSyncFields(connectionId);
            }
        }

        internal void SendInitSyncLists()
        {
            for (loopCounter = 0; loopCounter < Behaviours.Length; ++loopCounter)
            {
                Behaviours[loopCounter].SendInitSyncLists();
            }
        }

        internal void SendInitSyncLists(long connectionId)
        {
            for (loopCounter = 0; loopCounter < Behaviours.Length; ++loopCounter)
            {
                Behaviours[loopCounter].SendInitSyncLists(connectionId);
            }
        }

        public bool IsSceneObjectExists(uint objectId)
        {
            if (Manager != null)
            {
                // If this is spawned while gameplay, find it by manager assets
                return Manager.Assets.ContainsSceneObject(objectId);
            }
            // If this is now spawned while gameplay, find objects in scene
            LiteNetLibIdentity[] netObjects = FindObjectsOfType<LiteNetLibIdentity>();
            foreach (LiteNetLibIdentity netObject in netObjects)
            {
                if (netObject.objectId == objectId && netObject != this)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Initial Identity, will be called when spawned. If object id == 0, it will generate new object id
        /// </summary>
        /// <param name="manager"></param>
        /// <param name="objectId"></param>
        /// <param name="connectionId"></param>
        internal void Initial(LiteNetLibGameManager manager, bool isSceneObject, uint objectId = 0, long connectionId = -1)
        {
            this.objectId = objectId;
            this.connectionId = connectionId;
            this.manager = manager;
            destroyed = false;
            if (objectId > HighestObjectId)
                HighestObjectId = objectId;
            if (!isSceneObject)
                ValidateObjectId();

            if (!hasSetupBehaviours)
            {
                // Setup behaviours index, we will use this as reference for network functions
                Behaviours = GetComponents<LiteNetLibBehaviour>();
                for (loopCounter = 0; loopCounter < Behaviours.Length; ++loopCounter)
                {
                    Behaviours[loopCounter].Setup(Convert.ToByte(loopCounter));
                }
                hasSetupBehaviours = true;
            }

            // If this is host, hide it then will showing when rebuild subscribers
            if (IsServer && IsClient)
                OnServerSubscribingRemoved();

            RebuildSubscribers(true);
        }

        internal void SetOwnerClient(bool isOwnerClient)
        {
            // Validate owner at client only 1 time each identity
            if (ownerValidated)
                return;

            ownerValidated = true;

            IsOwnerClient = isOwnerClient;

            Behaviours = GetComponents<LiteNetLibBehaviour>();
            for (loopCounter = 0; loopCounter < Behaviours.Length; ++loopCounter)
            {
                Behaviours[loopCounter].OnSetOwnerClient();
            }
        }

        internal void ValidateObjectId()
        {
            if (objectId == 0 || IsSceneObjectExists(objectId))
                objectId = GetNewObjectId();
        }

        internal static void ResetObjectId()
        {
            HighestObjectId = 0;
        }

        internal static uint GetNewObjectId()
        {
            LiteNetLibIdentity[] netObjects = FindObjectsOfType<LiteNetLibIdentity>();
            if (HighestObjectId == 0)
            {
                uint result = HighestObjectId;
                foreach (LiteNetLibIdentity netObject in netObjects)
                {
                    if (netObject.objectId > result)
                        result = netObject.objectId;
                }
                HighestObjectId = result;
            }
            ++HighestObjectId;
            return HighestObjectId;
        }

        private static void ReorderSceneObjectId()
        {
            ResetObjectId();
            LiteNetLibIdentity[] netObjects = FindObjectsOfType<LiteNetLibIdentity>();
            foreach (LiteNetLibIdentity netObject in netObjects)
            {
                netObject.objectId = ++HighestObjectId;
#if UNITY_EDITOR
                // Do not mark dirty while playing
                if (!Application.isPlaying)
                    EditorUtility.SetDirty(netObject);
#endif
            }
#if UNITY_EDITOR
            // Do not mark dirty while playing
            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
#endif
        }

        public void ClearSubscribers()
        {
            // Only server can manage subscribers
            if (!IsServer)
                return;
            
            foreach (LiteNetLibPlayer subscriber in Subscribers.Values)
            {
                subscriber.RemoveSubscribing(this, false);
            }
            Subscribers.Clear();
#if UNITY_EDITOR
            subscriberIds.Clear();
#endif
        }

        public void AddSubscriber(LiteNetLibPlayer subscriber)
        {
            // Only server can manage subscribers
            if (!IsServer || subscriber == null)
                return;

            if (Subscribers.ContainsKey(subscriber.ConnectionId))
            {
                if (Manager.LogDebug)
                    Debug.Log("Subscriber [" + subscriber.ConnectionId + "] already added to [" + gameObject + "]");
                return;
            }

            Subscribers[subscriber.ConnectionId] = subscriber;
#if UNITY_EDITOR
            if (!subscriberIds.Contains(subscriber.ConnectionId))
                subscriberIds.Add(subscriber.ConnectionId);
#endif
            subscriber.AddSubscribing(this);
        }

        public void RemoveSubscriber(LiteNetLibPlayer subscriber, bool removePlayerSubscribing)
        {
            // Only server can manage subscribers
            if (!IsServer)
                return;

            Subscribers.Remove(subscriber.ConnectionId);
#if UNITY_EDITOR
            subscriberIds.Remove(subscriber.ConnectionId);
#endif
            if (removePlayerSubscribing)
                subscriber.RemoveSubscribing(this, false);
        }

        public bool ContainsSubscriber(long connectionId)
        {
            return Subscribers.ContainsKey(connectionId);
        }

        public bool ShouldAddSubscriber(LiteNetLibPlayer subscriber)
        {
            for (int i = 0; i < Behaviours.Length; ++i)
            {
                if (!Behaviours[i].ShouldAddSubscriber(subscriber))
                    return false;
            }
            return true;
        }

        public bool IsSubscribedOrOwning(long connectionId)
        {
            return ContainsSubscriber(connectionId) || connectionId == ConnectionId;
        }

        public void RebuildSubscribers(bool initialize)
        {
            // Only server can manage subscribers
            if (!IsServer)
                return;

            LiteNetLibPlayer ownerPlayer = Player;
            if (initialize)
                AddSubscriber(ownerPlayer);

            bool hasChanges = false;
            bool shouldRebuild = false;
            HashSet<LiteNetLibPlayer> newSubscribers = new HashSet<LiteNetLibPlayer>();
            HashSet<LiteNetLibPlayer> oldSubscribers = new HashSet<LiteNetLibPlayer>(Subscribers.Values);

            for (loopCounter = 0; loopCounter < Behaviours.Length; ++loopCounter)
            {
                shouldRebuild |= Behaviours[loopCounter].OnRebuildSubscribers(newSubscribers, initialize);
            }

            // If shouldRebuild is FALSE, it's means it does not have to rebuild subscribers
            if (!shouldRebuild)
            {
                // None of the behaviours rebuilt our subscribers, use built-in rebuild method
                if (initialize)
                {
                    foreach (LiteNetLibPlayer player in Manager.GetPlayers())
                    {
                        if (ConnectionId == player.ConnectionId || !player.IsReady)
                            continue;

                        if (ShouldAddSubscriber(player))
                            AddSubscriber(player);
                    }
                }
                return;
            }

            // Apply changes from rebuild
            foreach (LiteNetLibPlayer subscriber in newSubscribers)
            {
                if (subscriber == null)
                    continue;

                if (!subscriber.IsReady)
                {
                    if (Manager.LogWarn)
                        Debug.Log("Subscriber [" + subscriber.ConnectionId + "] is not ready");
                    continue;
                }

                if ((ownerPlayer == null || subscriber.ConnectionId != ownerPlayer.ConnectionId) && (initialize || !oldSubscribers.Contains(subscriber)))
                {
                    subscriber.AddSubscribing(this);
                    if (Manager.LogDebug)
                        Debug.Log("Add subscriber [" + subscriber.ConnectionId + "] to [" + gameObject + "]");
                    hasChanges = true;
                }
            }

            // Remove subscribers that is not in new subscribers list
            foreach (LiteNetLibPlayer subscriber in oldSubscribers)
            {
                if (!newSubscribers.Contains(subscriber))
                {
                    subscriber.RemoveSubscribing(this, true);
                    if (Manager.LogDebug)
                        Debug.Log("Remove subscriber [" + subscriber.ConnectionId + "] from [" + gameObject + "]");
                    hasChanges = true;
                }
            }

            if (!hasChanges)
                return;

            // Rebuild subscribers
            Subscribers.Clear();
            foreach (LiteNetLibPlayer subscriber in newSubscribers)
                Subscribers.Add(subscriber.ConnectionId, subscriber);

#if UNITY_EDITOR
            subscriberIds.Clear();
            foreach (LiteNetLibPlayer subscriber in newSubscribers)
                subscriberIds.Add(subscriber.ConnectionId);
#endif
        }

        public void OnServerSubscribingAdded()
        {
            for (loopCounter = 0; loopCounter < Behaviours.Length; ++loopCounter)
            {
                Behaviours[loopCounter].OnServerSubscribingAdded();
            }
        }

        public void OnServerSubscribingRemoved()
        {
            for (loopCounter = 0; loopCounter < Behaviours.Length; ++loopCounter)
            {
                Behaviours[loopCounter].OnServerSubscribingRemoved();
            }
        }

        public void NetworkDestroy()
        {
            if (!IsServer)
                return;

            StartCoroutine(NetworkDestroyRoutine(0f));
        }

        public void NetworkDestroy(float delay)
        {
            if (!IsServer)
                return;

            StartCoroutine(NetworkDestroyRoutine(delay));
        }

        IEnumerator NetworkDestroyRoutine(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (!destroyed)
            {
                Manager.Assets.NetworkDestroy(ObjectId, LiteNetLibGameManager.DestroyObjectReasons.RequestedToDestroy);
                destroyed = true;
            }
        }

        public void OnNetworkDestroy(byte reasons)
        {
            for (loopCounter = 0; loopCounter < Behaviours.Length; ++loopCounter)
            {
                Behaviours[loopCounter].OnNetworkDestroy(reasons);
            }
        }
    }
}