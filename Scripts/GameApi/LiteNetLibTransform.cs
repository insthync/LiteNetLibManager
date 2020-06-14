using LiteNetLib.Utils;
using UnityEngine;
using UnityEngine.AI;

namespace LiteNetLibManager
{
    public class LiteNetLibTransform : LiteNetLibBehaviour
    {
        private struct TransformResult
        {
            public Vector3 position;
            public Quaternion rotation;
            public float timestamp;
        }

        public enum SyncPositionOptions
        {
            Sync,
            NotSync,
        }

        public enum SyncRotationOptions
        {
            Sync,
            NotSync,
            CompressToUShort,
            CompressToByte,
        }

        [Tooltip("Which transform you are going to sync, if it is empty it will use transform which this component attached to")]
        public Transform syncingTransform;
        [Tooltip("If this is TRUE, transform data will be sent from owner client to server to update to another clients")]
        public bool ownerClientCanSendTransform;
        [Tooltip("If this is TRUE, it will not interpolate transform at owner client, but it's still snapping")]
        public bool ownerClientNotInterpolate;
        [Tooltip("This will be used when `ownerClientCanSendTransform` is set to TRUE to send transform update from client to server")]
        [Range(0.01f, 2f)]
        public float ownerClientSendInterval = 0.01f;
        public float snapThreshold = 5.0f;
        public float movementTheshold = 0.075f;
        public float rotateTheshold = 1f;
        [Header("Sync Position Settings")]
        public SyncPositionOptions syncPositionX;
        public SyncPositionOptions syncPositionY;
        public SyncPositionOptions syncPositionZ;
        [Header("Sync Rotation Settings")]
        public SyncRotationOptions syncRotationX;
        public SyncRotationOptions syncRotationY;
        public SyncRotationOptions syncRotationZ;

        public System.Action<Vector3, Quaternion> onTeleport;

        #region Cache components
        public NavMeshAgent CacheNavMeshAgent { get; private set; }
        public Rigidbody CacheRigidbody3D { get; private set; }
        public Rigidbody2D CacheRigidbody2D { get; private set; }
        public CharacterController CacheCharacterController { get; private set; }
        #endregion

        private bool syncingTransformIsIdentity;
        // Interpolation related variables
        private float ownerSendElapsed = 0;
        private float lastClientTimestamp = 0;
        private float lastServerTimestamp = 0;
        private TransformResult currentInterpResult;
        private TransformResult endInterpResult;
        private TransformResult syncResult;
        // Optimize garbage collection
        private float tempDeltaTime;

        private void Awake()
        {
            if (syncingTransform == null)
                syncingTransform = transform;
            // Nav mesh agent is highest priority, then character controller
            // Then Rigidbodies, that both 3d/2d are same priority
            CacheNavMeshAgent = syncingTransform.GetComponent<NavMeshAgent>();
            if (CacheNavMeshAgent == null)
            {
                CacheCharacterController = syncingTransform.GetComponent<CharacterController>();
                if (CacheCharacterController == null)
                {
                    CacheRigidbody3D = syncingTransform.GetComponent<Rigidbody>();
                    CacheRigidbody2D = syncingTransform.GetComponent<Rigidbody2D>();
                }
            }
        }

        private void Start()
        {
            InitInterpResults(syncingTransform.position, syncingTransform.rotation);
        }

        public override void OnSetOwnerClient(bool isOwnerClient)
        {
            InitInterpResults(syncingTransform.position, syncingTransform.rotation);
        }

        public override void InitTransform(Vector3 position, Quaternion rotation)
        {
            if (syncingTransformIsIdentity)
            {
                InitInterpResults(position, rotation);
                Snap(position, rotation);
            }
        }

        private void InitInterpResults(Vector3 position, Quaternion rotation)
        {
            currentInterpResult = new TransformResult();
            currentInterpResult.position = position;
            currentInterpResult.rotation = rotation;
            currentInterpResult.timestamp = Time.unscaledTime;
            syncResult = currentInterpResult;
            endInterpResult = currentInterpResult;
        }

        public override void OnSetup()
        {
            base.OnSetup();
            RegisterNetFunction<Vector3, Quaternion>(NetFunction_Teleport);
            if (syncingTransform == null)
                syncingTransform = transform;
            syncingTransformIsIdentity = syncingTransform == Identity.transform;
        }

        private void NetFunction_Teleport(Vector3 position, Quaternion rotation)
        {
            InitInterpResults(position, rotation);
            Snap(position, rotation);
            if (onTeleport != null)
                onTeleport.Invoke(position, rotation);
        }

        internal void HandleClientSendTransform(NetDataReader reader)
        {
            // Don't update transform follow client's request if not set "canClientSendResult" to TRUE or it's the server
            if (!ownerClientCanSendTransform || IsOwnerClient)
                return;
            TransformResult result = DeserializeResult(reader);
            // Discard out of order results
            if (result.timestamp <= lastClientTimestamp)
                return;
            lastClientTimestamp = result.timestamp;
            // Adding results to the results list so they can be used in interpolation process
            result.timestamp = Time.unscaledTime;
            endInterpResult = result;
        }

        private void ClientSendTransform(TransformResult transformResult)
        {
            // Don't request to set transform if not set "canClientSendResult" to TRUE
            if (!ownerClientCanSendTransform || !IsOwnerClient)
                return;
            Manager.ClientSendPacket(sendOptions, LiteNetLibGameManager.GameMsgTypes.ClientSendTransform, (writer) => ClientSendTransformWriter(writer, transformResult));
        }

        private void ClientSendTransformWriter(NetDataWriter writer, TransformResult transformResult)
        {
            writer.PutPackedUInt(ObjectId);
            writer.Put(BehaviourIndex);
            SerializePositionAxis(writer, transformResult.position.x, syncPositionX);
            SerializePositionAxis(writer, transformResult.position.y, syncPositionY);
            SerializePositionAxis(writer, transformResult.position.z, syncPositionZ);
            SerializeRotationAxis(writer, transformResult.rotation.eulerAngles.x, syncRotationX);
            SerializeRotationAxis(writer, transformResult.rotation.eulerAngles.y, syncRotationY);
            SerializeRotationAxis(writer, transformResult.rotation.eulerAngles.z, syncRotationZ);
            writer.Put(Time.unscaledTime);
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            if (IsServer || (ownerClientCanSendTransform && IsOwnerClient))
            {
                InitInterpResults(position, rotation);
                Snap(position, rotation);
                CallNetFunction(NetFunction_Teleport, FunctionReceivers.All, position, rotation);
            }
        }

        public override bool CanSyncBehaviour()
        {
            return true;
        }

        public override bool ShouldSyncBehaviour()
        {
            if (Vector3.Distance(syncResult.position, syncingTransform.position) >= movementTheshold || Quaternion.Angle(syncResult.rotation, syncingTransform.rotation) >= rotateTheshold)
            {
                syncResult.position = syncingTransform.position;
                syncResult.rotation = syncingTransform.rotation;
                syncResult.timestamp = Time.unscaledTime;
                return true;
            }
            return false;
        }

        public override void OnSerialize(NetDataWriter writer)
        {
            SerializePositionAxis(writer, syncingTransform.position.x, syncPositionX);
            SerializePositionAxis(writer, syncingTransform.position.y, syncPositionY);
            SerializePositionAxis(writer, syncingTransform.position.z, syncPositionZ);
            SerializeRotationAxis(writer, syncingTransform.rotation.eulerAngles.x, syncRotationX);
            SerializeRotationAxis(writer, syncingTransform.rotation.eulerAngles.y, syncRotationY);
            SerializeRotationAxis(writer, syncingTransform.rotation.eulerAngles.z, syncRotationZ);
            writer.Put(Time.unscaledTime);
        }

        public override void OnDeserialize(NetDataReader reader)
        {
            // Update transform only non-owner client
            if ((ownerClientCanSendTransform && IsOwnerClient) || IsServer)
                return;
            TransformResult result = DeserializeResult(reader);
            // Discard out of order results
            if (result.timestamp <= lastServerTimestamp)
                return;
            lastServerTimestamp = result.timestamp;
            // Adding results to the results list so they can be used in interpolation process
            result.timestamp = Time.unscaledTime;
            endInterpResult = result;
        }

        private TransformResult DeserializeResult(NetDataReader reader)
        {
            TransformResult result = new TransformResult();
            result.position = new Vector3(
                DeserializePositionAxis(reader, syncPositionX, syncingTransform.position.x),
                DeserializePositionAxis(reader, syncPositionY, syncingTransform.position.y),
                DeserializePositionAxis(reader, syncPositionZ, syncingTransform.position.z));
            result.rotation = Quaternion.Euler(
                DeserializeRotationAxis(reader, syncRotationX, syncingTransform.rotation.eulerAngles.x),
                DeserializeRotationAxis(reader, syncRotationY, syncingTransform.rotation.eulerAngles.y),
                DeserializeRotationAxis(reader, syncRotationZ, syncingTransform.rotation.eulerAngles.z));
            result.timestamp = reader.GetFloat();
            return result;
        }

        private void SerializePositionAxis(NetDataWriter writer, float data, SyncPositionOptions syncOptions)
        {
            switch (syncOptions)
            {
                case SyncPositionOptions.Sync:
                    writer.Put(data);
                    break;
                default:
                case SyncPositionOptions.NotSync:
                    break;
            }
        }

        private void SerializeRotationAxis(NetDataWriter writer, float data, SyncRotationOptions syncOptions)
        {
            data = To360Angle(data);
            switch (syncOptions)
            {
                case SyncRotationOptions.Sync:
                    writer.Put(data);
                    break;
                case SyncRotationOptions.CompressToUShort:
                    writer.Put((ushort)(data * 100));
                    break;
                case SyncRotationOptions.CompressToByte:
                    writer.Put((byte)(data / 360f * 100));
                    break;
                default:
                case SyncRotationOptions.NotSync:
                    break;
            }
        }

        private float DeserializePositionAxis(NetDataReader reader, SyncPositionOptions syncOptions, float defaultValue)
        {
            switch (syncOptions)
            {
                case SyncPositionOptions.Sync:
                    return reader.GetFloat();
                default:
                case SyncPositionOptions.NotSync:
                    break;
            }
            return defaultValue;
        }

        private float DeserializeRotationAxis(NetDataReader reader, SyncRotationOptions syncOptions, float defaultValue)
        {
            switch (syncOptions)
            {
                case SyncRotationOptions.Sync:
                    return reader.GetFloat();
                case SyncRotationOptions.CompressToUShort:
                    return (float)reader.GetUShort() * 0.01f;
                case SyncRotationOptions.CompressToByte:
                    return (float)reader.GetByte() * 0.01f * 360f;
                default:
                case SyncRotationOptions.NotSync:
                    break;
            }
            return defaultValue;
        }

        private void Update()
        {
            tempDeltaTime = Time.deltaTime;
            // Sending client transform result to server
            // Don't send to server if it's server which already update transform result
            // So only owner client can send transform result to server
            if (ownerClientCanSendTransform && IsOwnerClient && !IsServer)
            {
                if (ownerSendElapsed >= ownerClientSendInterval)
                {
                    // Send transform to server only when there are changes on transform
                    if (ShouldSyncBehaviour())
                    {
                        ClientSendTransform(syncResult);
                        ownerSendElapsed = 0;
                    }
                }
                ownerSendElapsed += tempDeltaTime;
            }

            UpdateInterpolate(tempDeltaTime);
        }

        private void UpdateInterpolate(float deltaTime)
        {
            // Sending transform to all clients
            if (IsServer)
            {
                // Interpolate transform that receives from clients
                if (ownerClientCanSendTransform && !IsOwnerClient)
                    Interpolate(deltaTime);
            }
            // Interpolating results for non-owner client objects on clients
            else if (!ownerClientCanSendTransform || !IsOwnerClient)
                Interpolate(deltaTime);
        }

        private void Interpolate(float deltaTime)
        {
            if (ShouldSnap(endInterpResult.position))
            {
                currentInterpResult.position = endInterpResult.position;
                currentInterpResult.rotation = endInterpResult.rotation;
                Snap(endInterpResult.position, endInterpResult.rotation);
            }
            else if (!IsOwnerClient || !ownerClientNotInterpolate)
            {
                currentInterpResult.position = Vector3.Lerp(currentInterpResult.position, endInterpResult.position, SendRate * deltaTime);
                currentInterpResult.rotation = Quaternion.Slerp(currentInterpResult.rotation, endInterpResult.rotation, SendRate * deltaTime);
                Interpolate(currentInterpResult.position, currentInterpResult.rotation);
            }
        }

        private bool ShouldSnap(Vector3 targetPosition)
        {
            float dist = 0f;
            if (CacheRigidbody2D != null)
                dist = (CacheRigidbody2D.position - new Vector2(targetPosition.x, targetPosition.y)).sqrMagnitude;
            else
                dist = (syncingTransform.position - targetPosition).sqrMagnitude;
            return dist > snapThreshold * snapThreshold;
        }

        private void Snap(Vector3 position, Quaternion rotation)
        {
            if (CacheNavMeshAgent != null)
            {
                CacheNavMeshAgent.Warp(position);
                syncingTransform.rotation = rotation;
            }
            else if (CacheRigidbody3D != null && !CacheRigidbody3D.isKinematic)
            {
                syncingTransform.position = position;
                syncingTransform.rotation = rotation;
                CacheRigidbody3D.position = position;
                CacheRigidbody3D.rotation = rotation;
            }
            else if (CacheRigidbody2D != null && !CacheRigidbody2D.isKinematic)
            {
                syncingTransform.position = position;
                syncingTransform.rotation = rotation;
                CacheRigidbody2D.position = position;
                CacheRigidbody2D.rotation = rotation.eulerAngles.z;
            }
            else
            {
                syncingTransform.position = position;
                syncingTransform.rotation = rotation;
            }
        }

        private void Interpolate(Vector3 position, Quaternion rotation)
        {
            if (CacheNavMeshAgent != null)
            {
                CacheNavMeshAgent.Move(position - syncingTransform.position);
                syncingTransform.rotation = rotation;
            }
            else if (CacheCharacterController != null)
            {
                CacheCharacterController.Move(position - syncingTransform.position);
                syncingTransform.rotation = rotation;
            }
            else if (CacheRigidbody3D != null && !CacheRigidbody3D.isKinematic)
            {
                if (Vector3.Distance(position, CacheRigidbody3D.position) >= movementTheshold)
                    CacheRigidbody3D.MovePosition(position);
                else
                {
                    CacheRigidbody3D.velocity = Vector3.zero;
                    CacheRigidbody3D.MovePosition(position);
                }
                syncingTransform.rotation = rotation;
            }
            else if (CacheRigidbody2D != null && !CacheRigidbody2D.isKinematic)
            {
                if (Vector2.Distance(position, CacheRigidbody2D.position) >= movementTheshold)
                    CacheRigidbody2D.MovePosition(position);
                else
                {
                    CacheRigidbody2D.velocity = Vector2.zero;
                    CacheRigidbody2D.MovePosition(position);
                }
                syncingTransform.rotation = rotation;
            }
            else
            {
                syncingTransform.position = position;
                syncingTransform.rotation = rotation;
            }
        }

        #region Math Utilities
        public static float To360Angle(float angle)
        {
            float result = angle - Mathf.CeilToInt(angle / 360f) * 360f;
            if (result < 0)
            {
                result += 360f;
            }
            return result;
        }
        #endregion
    }
}
