using LiteNetLib;
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

        public enum InterpolateMode
        {
            EstimatedSpeed,
            FixedSpeed,
        }

        public enum ExtrapolateMode
        {
            None,
            EstimatedSpeed,
            FixedSpeed,
        }

        [Tooltip("Which transform you are going to sync, if it is empty it will use transform which this component attached to")]
        public Transform syncingTransform;
        [Tooltip("If this is TRUE, transform data will be sent from owner client to server to update to another clients")]
        public bool ownerClientCanSendTransform;
        [Tooltip("This will be used when `ownerClientCanSendTransform` is set to TRUE to send transform update from client to server")]
        [Range(0.01f, 2f)]
        public float ownerClientSendInterval = 0.1f;
        public float snapThreshold = 5.0f;
        public InterpolateMode interpolateMode = InterpolateMode.EstimatedSpeed;
        public float fixedInterpolateSpeed;
        public ExtrapolateMode extrapolateMode = ExtrapolateMode.None;
        public float fixedExtrapolateSpeed;
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
        private float ownerSendElapsed = 0f;
        private float lastReceivedTime = 0f;
        private float lastReceivedTimestamp = 0f;
        private TransformResult currentInterpResult;
        private TransformResult previousEndInterpResult;
        private TransformResult endInterpResult;

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
            sendOptions = DeliveryMethod.Unreliable;
        }

        private void OnValidate()
        {
            // Force set to `Unreliable`
            sendOptions = DeliveryMethod.Unreliable;
        }

        private void OnEnable()
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
            currentInterpResult.timestamp = GetTimeStamp();
            previousEndInterpResult = currentInterpResult;
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

        /// <summary>
        /// This function will be called at server after receive transform from clients 
        /// to interpolate and sync position to other clients
        /// </summary>
        /// <param name="reader"></param>
        internal void HandleClientSendTransform(NetDataReader reader)
        {
            // Don't update transform follow client's request 
            // if not set "ownerClientCanSendTransform" to `TRUE`
            // or it's this is owned by host
            if (!ownerClientCanSendTransform || IsOwnerClient)
                return;
            TransformResult result = DeserializeResult(reader);
            // Discard out of order results
            if (result.timestamp <= lastReceivedTimestamp)
                return;
            lastReceivedTime = Time.fixedTime;
            lastReceivedTimestamp = result.timestamp;
            result.timestamp = GetTimeStamp();
            previousEndInterpResult = endInterpResult;
            endInterpResult = result;
        }

        /// <summary>
        /// This function will be called at client to send transform
        /// </summary>
        /// <param name="transformResult"></param>
        private void ClientSendTransform(TransformResult transformResult)
        {
            // Don't request to set transform if not set "canClientSendResult" to TRUE
            if (!ownerClientCanSendTransform || !IsOwnerClient)
                return;
            Manager.ClientSendPacket(0, DeliveryMethod.Unreliable, GameMsgTypes.ClientSendTransform, (writer) => ClientSendTransformWriter(writer, transformResult));
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
            writer.Put(GetTimeStamp());
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            if (IsServer || (ownerClientCanSendTransform && IsOwnerClient))
            {
                InitInterpResults(position, rotation);
                Snap(position, rotation);
                // This function can be called before networked object spawned
                // to set initial object transform, so it won't be able to call net function yet.
                // So have to avoid net function calling by check is behaviours already setup or not.
                if (Identity.IsSetupBehaviours)
                    CallNetFunction(NetFunction_Teleport, FunctionReceivers.All, position, rotation);
            }
        }

        public override bool CanSyncBehaviour()
        {
            return true;
        }

        public override bool ShouldSyncBehaviour()
        {
            return true;
        }

        public override void OnSerialize(NetDataWriter writer)
        {
            SerializePositionAxis(writer, syncingTransform.position.x, syncPositionX);
            SerializePositionAxis(writer, syncingTransform.position.y, syncPositionY);
            SerializePositionAxis(writer, syncingTransform.position.z, syncPositionZ);
            SerializeRotationAxis(writer, syncingTransform.rotation.eulerAngles.x, syncRotationX);
            SerializeRotationAxis(writer, syncingTransform.rotation.eulerAngles.y, syncRotationY);
            SerializeRotationAxis(writer, syncingTransform.rotation.eulerAngles.z, syncRotationZ);
            writer.Put(GetTimeStamp());
        }

        public override void OnDeserialize(NetDataReader reader)
        {
            // Update transform only non-owner client
            if ((ownerClientCanSendTransform && IsOwnerClient) || IsServer)
                return;
            TransformResult result = DeserializeResult(reader);
            // Discard out of order results
            if (result.timestamp <= lastReceivedTimestamp)
                return;
            lastReceivedTime = Time.fixedTime;
            lastReceivedTimestamp = result.timestamp;
            result.timestamp = GetTimeStamp();
            previousEndInterpResult = endInterpResult;
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
            if (!IsServer && !IsClient)
                return;
            // Sending transform to all clients
            if (IsServer)
            {
                // Interpolate transform that receives from clients
                // So if this is no owning client (ConnectionId < 0), it won't interpolate
                if (ownerClientCanSendTransform && !IsOwnerClient && ConnectionId >= 0)
                    UpdateTransform(Time.deltaTime);
            }
            // Interpolating results for non-owner client objects on clients
            else if (!ownerClientCanSendTransform || !IsOwnerClient)
            {
                UpdateTransform(Time.deltaTime);
            }
        }

        private void FixedUpdate()
        {
            if (!IsServer && !IsClient)
                return;
            // Sending client transform result to server
            // Don't send to server if it's server which already update transform result
            // So only owner client can send transform result to server
            if (ownerClientCanSendTransform && IsOwnerClient && !IsServer)
            {
                ownerSendElapsed += Time.fixedDeltaTime;
                if (ownerSendElapsed >= ownerClientSendInterval)
                {
                    // Send transform to server only when there are changes on transform
                    ClientSendTransform(new TransformResult()
                    {
                        position = syncingTransform.position,
                        rotation = syncingTransform.rotation,
                        timestamp = GetTimeStamp(),
                    });
                    ownerSendElapsed = 0;
                }
            }
        }

        private void LateUpdate()
        {
            if (!IsServer)
                return;
            Manager.RegisterSyncBehaviourUpdating(this);
        }

        private void OnDestroy()
        {
            Manager.UnregisterSyncBehaviourUpdating(this);
        }

        private void UpdateTransform(float deltaTime)
        {
            if (ShouldSnap(endInterpResult.position))
            {
                currentInterpResult.position = endInterpResult.position;
                currentInterpResult.rotation = endInterpResult.rotation;
                Snap(endInterpResult.position, endInterpResult.rotation);
            }
            else
            {
                if (IsOwnerClient && ownerClientCanSendTransform)
                {
                    // If owner client can send transform, it won't interpolating transform at owner client
                    return;
                }
                // Calculate move speed by distance and time
                float dist = Vector3.Distance(endInterpResult.position, previousEndInterpResult.position);
                float timeDiff = endInterpResult.timestamp - previousEndInterpResult.timestamp;
                float moveSpeed = 0f;
                if (timeDiff > 0f)
                    moveSpeed = dist / timeDiff;
                // Find extrapolated move by move direction, time passed (which calculated by current time and last received time) 
                // and calculate move speed or fixed extrapolate speed
                Vector3 extrapolatedMove = Vector3.zero;
                if (extrapolateMode != ExtrapolateMode.None)
                {
                    float timePassed = Time.fixedTime - lastReceivedTime;
                    Vector3 moveDirection = (endInterpResult.position - previousEndInterpResult.position).normalized;
                    if (moveDirection.sqrMagnitude > 0f && timePassed > 0f)
                    {
                        switch (extrapolateMode)
                        {
                            case ExtrapolateMode.EstimatedSpeed:
                                extrapolatedMove = moveDirection * moveSpeed * timePassed;
                                break;
                            case ExtrapolateMode.FixedSpeed:
                                extrapolatedMove = moveDirection * fixedExtrapolateSpeed * timePassed;
                                break;
                        }
                    }
                }
                // Set interpolate move speed to fixed value if interpolate mode is fixed speed
                if (interpolateMode == InterpolateMode.FixedSpeed)
                    moveSpeed = fixedInterpolateSpeed;
                // Move it
                currentInterpResult.position = Vector3.MoveTowards(currentInterpResult.position, endInterpResult.position + extrapolatedMove, moveSpeed * deltaTime);
                currentInterpResult.rotation = Quaternion.Slerp(currentInterpResult.rotation, endInterpResult.rotation, (1f / timeDiff) * deltaTime);
                Interpolate(currentInterpResult.position, currentInterpResult.rotation);
            }
        }

        private bool ShouldSnap(Vector3 targetPosition)
        {
            float dist;
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
                syncingTransform.position = position;
                syncingTransform.rotation = rotation;
            }
            else if (CacheRigidbody3D != null && !CacheRigidbody3D.isKinematic)
            {
                if (Vector3.Distance(position, CacheRigidbody3D.position) > 0f)
                    CacheRigidbody3D.MovePosition(position);
                else
                {
#if UNITY_6000_0_OR_NEWER
                    CacheRigidbody3D.linearVelocity = Vector3.zero;
#else
                    CacheRigidbody3D.velocity = Vector3.zero;
#endif
                    CacheRigidbody3D.MovePosition(position);
                }
                syncingTransform.rotation = rotation;
            }
            else if (CacheRigidbody2D != null && !CacheRigidbody2D.isKinematic)
            {
                if (Vector2.Distance(position, CacheRigidbody2D.position) > 0f)
                    CacheRigidbody2D.MovePosition(position);
                else
                {
#if UNITY_6000_0_OR_NEWER
                    CacheRigidbody2D.linearVelocity = Vector2.zero;
#else
                    CacheRigidbody2D.velocity = Vector2.zero;
#endif
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

        private float GetTimeStamp()
        {
            return Time.fixedTime;
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
