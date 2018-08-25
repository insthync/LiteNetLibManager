using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using LiteNetLib;
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
        private long connectId;
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
        public long ConnectId { get { return connectId; } }
        public LiteNetLibGameManager Manager { get { return manager; } }

        public LiteNetLibPlayer Player
        {
            get
            {
                LiteNetLibPlayer foundPlayer;
                if (Manager == null || !Manager.Players.TryGetValue(ConnectId, out foundPlayer))
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

        public bool IsOwnerClient
        {
            get { return Manager != null && Manager.Client != null && Manager.Client.Peer != null && ConnectId == Manager.Client.Peer.ConnectId; }
        }

        // Optimize garbage collector
        private int loopCounter;

        private void Update()
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
            PrefabType prefabType = PrefabUtility.GetPrefabType(gameObject);
            if (prefabType == PrefabType.Prefab)
                return true;
            return false;
        }

        private bool ThisIsASceneObjectWithPrefabParent(out GameObject prefab)
        {
            prefab = null;
            PrefabType prefabType = PrefabUtility.GetPrefabType(gameObject);
            if (prefabType == PrefabType.None)
                return false;
            prefab = (GameObject)PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
            if (prefab == null)
            {
                Debug.LogError("Failed to find prefab parent for scene object [name:" + gameObject.name + "]");
                return false;
            }
            return true;
        }

        private void SetupIDs()
        {
            GameObject prefab;
            if (ThisIsAPrefab())
            {
                // This is a prefab
                AssignAssetID(gameObject);
                objectId = 0;
            }
            else if (ThisIsASceneObjectWithPrefabParent(out prefab))
            {
                // This is a scene object with prefab link
                AssignAssetID(prefab);
                ValidateObjectId();
                if (!Application.isPlaying)
                    EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }
            else
            {
                // This is a pure scene object (Not a prefab)
                assetId = string.Empty;
                ValidateObjectId();
                if (!Application.isPlaying)
                    EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }
            // Do not mark dirty while playing
            if (!Application.isPlaying)
                EditorUtility.SetDirty(this);
        }
#endif
        #endregion

        internal LiteNetLibSyncField ProcessSyncField(LiteNetLibElementInfo info, NetDataReader reader)
        {
            if (info.objectId != ObjectId)
                return null;
            if (info.behaviourIndex >= Behaviours.Length)
                return null;
            return Behaviours[info.behaviourIndex].ProcessSyncField(info, reader);
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
            var behaviour = Behaviours[behaviourIndex];
            behaviour.Deserialize(reader);
            return behaviour;
        }

        internal void SendInitSyncFields()
        {
            for (loopCounter = 0; loopCounter < Behaviours.Length; ++loopCounter)
            {
                Behaviours[loopCounter].SendInitSyncFields();
            }
        }

        internal void SendInitSyncFields(NetPeer peer)
        {
            for (loopCounter = 0; loopCounter < Behaviours.Length; ++loopCounter)
            {
                Behaviours[loopCounter].SendInitSyncFields(peer);
            }
        }

        internal void SendInitSyncLists()
        {
            for (loopCounter = 0; loopCounter < Behaviours.Length; ++loopCounter)
            {
                Behaviours[loopCounter].SendInitSyncLists();
            }
        }

        internal void SendInitSyncLists(NetPeer peer)
        {
            for (loopCounter = 0; loopCounter < Behaviours.Length; ++loopCounter)
            {
                Behaviours[loopCounter].SendInitSyncLists(peer);
            }
        }

        public bool IsSceneObjectExists(uint objectId)
        {
            if (Manager == null)
                return false;
            return Manager.Assets.ContainsSceneObject(objectId);
        }

        /// <summary>
        /// Initial Identity, will be called when spawned. If object id == 0, it will generate new object id
        /// </summary>
        /// <param name="manager"></param>
        /// <param name="objectId"></param>
        /// <param name="connectId"></param>
        internal void Initial(LiteNetLibGameManager manager, bool isSceneObject, uint objectId = 0, long connectId = 0)
        {
            this.objectId = objectId;
            this.connectId = connectId;
            this.manager = manager;
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

            // If this is not local host client object, hide it
            if (IsServer && IsClient && !IsOwnerClient)
                OnServerSubscribingRemoved();

            RebuildSubscribers(true);
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

        internal void ClearSubscribers()
        {
            // Only server can manage subscribers
            if (!IsServer)
                return;

            var values = Subscribers.Values;
            foreach (var subscriber in values)
            {
                subscriber.RemoveSubscribing(this, false);
            }
            Subscribers.Clear();
#if UNITY_EDITOR
            subscriberIds.Clear();
#endif
        }

        internal void AddSubscriber(LiteNetLibPlayer subscriber)
        {
            // Only server can manage subscribers
            if (!IsServer || subscriber == null)
                return;
            
            if (Subscribers.ContainsKey(subscriber.ConnectId))
            {
                if (Manager.LogDebug)
                    Debug.Log("Subscriber [" + subscriber.ConnectId + "] already added to [" + gameObject + "]");
                return;
            }

            Subscribers[subscriber.ConnectId] = subscriber;
#if UNITY_EDITOR
            if (!subscriberIds.Contains(subscriber.ConnectId))
                subscriberIds.Add(subscriber.ConnectId);
#endif
            subscriber.AddSubscribing(this);
        }

        internal void RemoveSubscriber(LiteNetLibPlayer subscriber, bool removePlayerSubscribing)
        {
            // Only server can manage subscribers
            if (!IsServer)
                return;

            Subscribers.Remove(subscriber.ConnectId);
#if UNITY_EDITOR
            subscriberIds.Remove(subscriber.ConnectId);
#endif
            if (removePlayerSubscribing)
                subscriber.RemoveSubscribing(this, false);
        }

        internal bool ContainsSubscriber(long connectId)
        {
            return Subscribers.ContainsKey(connectId);
        }

        internal bool ShouldAddSubscriber(LiteNetLibPlayer subscriber)
        {
            for (var i = 0; i < Behaviours.Length; ++i)
            {
                if (!Behaviours[i].ShouldAddSubscriber(subscriber))
                    return false;
            }
            return true;
        }

        public bool IsSubscribedOrOwning(long connectId)
        {
            return ContainsSubscriber(connectId) || connectId == ConnectId;
        }

        public void RebuildSubscribers(bool initialize)
        {
            // Only server can manage subscribers
            if (!IsServer)
                return;

            var ownerPlayer = Player;
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
                    var players = Manager.Players.Values;
                    foreach (var player in players)
                    {
                        if (ConnectId == player.ConnectId || !player.IsReady)
                            continue;

                        if (ShouldAddSubscriber(player))
                            AddSubscriber(player);
                    }
                }
                return;
            }

            // Apply changes from rebuild
            foreach (var subscriber in newSubscribers)
            {
                if (subscriber == null)
                    continue;

                if (!subscriber.IsReady)
                {
                    if (Manager.LogWarn)
                        Debug.Log("Subscriber [" + subscriber.ConnectId + "] is not ready");
                    continue;
                }

                if ((ownerPlayer == null || subscriber.ConnectId != ownerPlayer.ConnectId) && (initialize || !oldSubscribers.Contains(subscriber)))
                {
                    subscriber.AddSubscribing(this);
                    if (Manager.LogDebug)
                        Debug.Log("Add subscriber [" + subscriber.ConnectId + "] to [" + gameObject + "]");
                    hasChanges = true;
                }
            }

            // Remove subscribers that is not in new subscribers list
            foreach (var subscriber in oldSubscribers)
            {
                if (!newSubscribers.Contains(subscriber))
                {
                    subscriber.RemoveSubscribing(this, true);
                    if (Manager.LogDebug)
                        Debug.Log("Remove subscriber [" + subscriber.ConnectId + "] from [" + gameObject + "]");
                    hasChanges = true;
                }
            }

            if (!hasChanges)
                return;

            // Rebuild subscribers
            Subscribers.Clear();
            foreach (var subscriber in newSubscribers)
                Subscribers.Add(subscriber.ConnectId, subscriber);

#if UNITY_EDITOR
            subscriberIds.Clear();
            foreach (var subscriber in newSubscribers)
                subscriberIds.Add(subscriber.ConnectId);
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

            Manager.Assets.NetworkDestroy(ObjectId, DestroyObjectReasons.RequestedToDestroy);
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
            NetworkDestroy();
        }

        public void OnNetworkDestroy(DestroyObjectReasons reasons)
        {
            for (loopCounter = 0; loopCounter < Behaviours.Length; ++loopCounter)
            {
                Behaviours[loopCounter].OnNetworkDestroy(reasons);
            }
        }
    }
}