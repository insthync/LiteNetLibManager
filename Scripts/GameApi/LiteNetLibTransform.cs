using System.Collections;
using System.Collections.Generic;
using LiteNetLib.Utils;
using UnityEngine;
using UnityEngine.AI;

namespace LiteNetLibManager
{
    [DisallowMultipleComponent]
    public class LiteNetLibTransform : LiteNetLibBehaviour
    {
        [System.Serializable]
        private struct TransformResult
        {
            public Vector3 position;
            public Quaternion rotation;
            public float timestamp;
        }
        private class TransformResultNetField : NetFieldStruct<TransformResult> { }

        public enum SyncOptions
        {
            Sync,
            NotSync,
        }

        [Tooltip("If this is TRUE, transform data will be sent from owner client to server to update to another clients")]
        public bool ownerClientCanSendTransform;
        [Tooltip("If this is TRUE, it will not interpolate transform at owner client, but it's still snapping")]
        public bool ownerClientNotInterpolate;
        public float snapThreshold = 5.0f;
        public float movementTheshold = 0.075f;
        [Header("Sync Position Settings")]
        public SyncOptions syncPositionX;
        public SyncOptions syncPositionY;
        public SyncOptions syncPositionZ;
        [Header("Sync Rotation Settings")]
        public SyncOptions syncRotationX;
        public SyncOptions syncRotationY;
        public SyncOptions syncRotationZ;

        #region Cache components
        public Transform CacheTransform { get; private set; }
        public NavMeshAgent CacheNavMeshAgent { get; private set; }
        public Rigidbody CacheRigidbody3D { get; private set; }
        public Rigidbody2D CacheRigidbody2D { get; private set; }
        public CharacterController CacheCharacterController { get; private set; }
        #endregion

        // Interpolation related variables
        private float syncElapsed = 0;
        private float lastClientTimestamp = 0;
        private float lastServerTimestamp = 0;
        private TransformResult currentInterpResult;
        private TransformResult endInterpResult;
        private TransformResult syncResult;

        private void Awake()
        {
            CacheTransform = GetComponent<Transform>();
            // Nav mesh agent is highest priority, then character controller
            // Then Rigidbodies, that both 3d/2d are same priority
            CacheNavMeshAgent = GetComponent<NavMeshAgent>();
            if (CacheNavMeshAgent == null)
            {
                CacheCharacterController = GetComponent<CharacterController>();
                if (CacheCharacterController == null)
                {
                    CacheRigidbody3D = GetComponent<Rigidbody>();
                    CacheRigidbody2D = GetComponent<Rigidbody2D>();
                }
            }
        }

        private void Start()
        {
            currentInterpResult = new TransformResult();
            currentInterpResult.position = CacheTransform.position;
            currentInterpResult.rotation = CacheTransform.rotation;
            currentInterpResult.timestamp = Time.unscaledTime;
            syncResult = currentInterpResult;
            endInterpResult = currentInterpResult;
            if (IsServer)
                Teleport(CacheTransform.position, CacheTransform.rotation);
        }

        public override void OnSetup()
        {
            base.OnSetup();
            RegisterNetFunction("ClientSendResult", new LiteNetLibFunction<TransformResultNetField>(ClientSendResultCallback));
            RegisterNetFunction("Teleport", new LiteNetLibFunction<NetFieldVector3, NetFieldQuaternion>(TeleportCallback));
        }

        private void ClientSendResultCallback(TransformResultNetField resultParam)
        {
            // Don't update transform follow client's request if not set "canClientSendResult" to TRUE or it's the server
            if (!ownerClientCanSendTransform || IsOwnerClient)
                return;
            var result = resultParam.Value;
            // Discard out of order results
            if (result.timestamp <= lastClientTimestamp)
                return;
            lastClientTimestamp = result.timestamp;
            // Adding results to the results list so they can be used in interpolation process
            result.timestamp = Time.unscaledTime;
            endInterpResult = result;
        }

        private void TeleportCallback(NetFieldVector3 position, NetFieldQuaternion rotation)
        {
            currentInterpResult = new TransformResult();
            currentInterpResult.position = position;
            currentInterpResult.rotation = rotation;
            currentInterpResult.timestamp = Time.unscaledTime;
            syncResult = currentInterpResult;
            endInterpResult = currentInterpResult;
            Snap(position, rotation);
        }

        private void ClientSendResult(TransformResult result)
        {
            // Don't request to set transform if not set "canClientSendResult" to TRUE
            if (!ownerClientCanSendTransform || !IsOwnerClient || IsServer)
                return;
            CallNetFunction("ClientSendResult", FunctionReceivers.Server, result);
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            if (IsServer || (ownerClientCanSendTransform && IsOwnerClient))
                CallNetFunction("Teleport", FunctionReceivers.All, position, rotation);
        }

        public override bool ShouldSyncBehaviour()
        {
            if (Vector3.Distance(syncResult.position, CacheTransform.position) >= movementTheshold || syncResult.rotation != CacheTransform.rotation)
            {
                syncResult.position = CacheTransform.position;
                syncResult.rotation = CacheTransform.rotation;
                syncResult.timestamp = Time.unscaledTime;
                return true;
            }
            return false;
        }

        public override void OnSerialize(NetDataWriter writer)
        {
            SerializeFloat(writer, CacheTransform.position.x, syncPositionX);
            SerializeFloat(writer, CacheTransform.position.y, syncPositionX);
            SerializeFloat(writer, CacheTransform.position.z, syncPositionX);
            SerializeFloat(writer, CacheTransform.rotation.eulerAngles.x, syncRotationX);
            SerializeFloat(writer, CacheTransform.rotation.eulerAngles.y, syncRotationY);
            SerializeFloat(writer, CacheTransform.rotation.eulerAngles.z, syncRotationZ);
            writer.Put(Time.unscaledTime);
        }

        public override void OnDeserialize(NetDataReader reader)
        {
            // Update transform only non-owner client
            if ((ownerClientCanSendTransform && IsOwnerClient) || IsServer)
                return;
            var result = new TransformResult();
            result.position = new Vector3(
                DeserializeFloat(reader, syncPositionX),
                DeserializeFloat(reader, syncPositionY),
                DeserializeFloat(reader, syncPositionZ));
            result.rotation = Quaternion.Euler(
                DeserializeFloat(reader, syncRotationX),
                DeserializeFloat(reader, syncRotationY),
                DeserializeFloat(reader, syncRotationZ));
            result.timestamp = reader.GetFloat();
            // Discard out of order results
            if (result.timestamp <= lastServerTimestamp)
                return;
            lastServerTimestamp = result.timestamp;
            // Adding results to the results list so they can be used in interpolation process
            result.timestamp = Time.unscaledTime;
            endInterpResult = result;
        }

        private void SerializeFloat(NetDataWriter writer, float data, SyncOptions syncOptions)
        {
            switch (syncOptions)
            {
                case SyncOptions.Sync:
                    writer.Put(data);
                    break;
                default:
                case SyncOptions.NotSync:
                    break;
            }
        }

        private float DeserializeFloat(NetDataReader reader, SyncOptions syncOptions)
        {
            switch (syncOptions)
            {
                case SyncOptions.Sync:
                    return reader.GetFloat();
                default:
                case SyncOptions.NotSync:
                    break;
            }
            return 0f;
        }

        private void Update()
        {
            // Sending client transform result to server
            if (ownerClientCanSendTransform && IsOwnerClient)
            {
                if (syncElapsed >= sendInterval)
                {
                    // Send transform to server only when there are changes on transform
                    if (ShouldSyncBehaviour())
                        ClientSendResult(syncResult);
                    syncElapsed = 0;
                }
                syncElapsed += Time.deltaTime;
            }

            if (CacheRigidbody2D == null && CacheRigidbody3D == null)
                UpdateInterpolate();
        }

        private void FixedUpdate()
        {
            if (CacheRigidbody2D != null || CacheRigidbody3D != null)
                UpdateInterpolate();
        }

        private void UpdateInterpolate()
        {
            // Sending transform to all clients
            if (IsServer)
            {
                // Interpolate transform that receives from clients
                if (ownerClientCanSendTransform && !IsOwnerClient)
                    Interpolate();
            }
            // Interpolating results for non-owner client objects on clients
            else if (!ownerClientCanSendTransform || !IsOwnerClient)
                Interpolate();
        }

        private float GetPositionInterpStep()
        {
            return 1f / sendInterval;
        }

        private float GetRotationInterpStep()
        {
            return 1f / sendInterval;
        }

        private void Interpolate()
        {
            if (ShouldSnap(endInterpResult.position))
            {
                currentInterpResult.position = endInterpResult.position;
                currentInterpResult.rotation = endInterpResult.rotation;
                Snap(endInterpResult.position, endInterpResult.rotation);
            }
            else if (!IsOwnerClient || !ownerClientNotInterpolate)
            {
                currentInterpResult.position = Vector3.Lerp(currentInterpResult.position, endInterpResult.position, GetPositionInterpStep() * Time.deltaTime);
                currentInterpResult.rotation = Quaternion.Slerp(currentInterpResult.rotation, endInterpResult.rotation, GetRotationInterpStep() * Time.deltaTime);
                Interpolate(currentInterpResult.position, currentInterpResult.rotation);
            }
        }

        private bool ShouldSnap(Vector3 targetPosition)
        {
            var dist = 0f;
            if (CacheRigidbody2D != null)
                dist = (CacheRigidbody2D.position - new Vector2(targetPosition.x, targetPosition.y)).magnitude;
            else
                dist = (CacheTransform.position - targetPosition).magnitude;
            return dist > snapThreshold;
        }

        private void Snap(Vector3 position, Quaternion rotation)
        {
            if (CacheNavMeshAgent != null)
            {
                CacheNavMeshAgent.Warp(position);
                CacheTransform.rotation = rotation;
            }
            else if (CacheRigidbody3D != null)
            {
                CacheRigidbody3D.position = position;
                CacheRigidbody3D.rotation = rotation;
            }
            else if (CacheRigidbody2D != null)
            {
                CacheRigidbody2D.position = position;
                CacheRigidbody2D.rotation = rotation.eulerAngles.z;
            }
            else
            {
                CacheTransform.position = position;
                CacheTransform.rotation = rotation;
            }
        }

        private void Interpolate(Vector3 position, Quaternion rotation)
        {
            if (CacheNavMeshAgent != null)
            {
                CacheNavMeshAgent.Move(position - CacheTransform.position);
                CacheTransform.rotation = rotation;
            }
            else if (CacheCharacterController != null)
            {
                CacheCharacterController.Move(position - CacheTransform.position);
                CacheTransform.rotation = rotation;
            }
            else if (CacheRigidbody3D != null)
            {
                CacheRigidbody3D.MoveRotation(rotation);
                var velocity = (position - CacheRigidbody3D.position) * GetPositionInterpStep();
                if (Vector3.Distance(position, CacheRigidbody3D.position) >= movementTheshold)
                {
                    if (!CacheRigidbody3D.isKinematic)
                        CacheRigidbody3D.velocity = velocity;
                    else
                        CacheRigidbody3D.MovePosition(position);
                }
                else
                {
                    CacheRigidbody3D.velocity = Vector3.zero;
                    CacheRigidbody3D.MovePosition(position);
                }
            }
            else if (CacheRigidbody2D != null)
            {
                CacheRigidbody2D.MoveRotation(rotation.eulerAngles.z);
                var velocity = ((Vector2)position - CacheRigidbody2D.position) * GetPositionInterpStep();
                if (Vector2.Distance(position, CacheRigidbody2D.position) >= movementTheshold)
                {
                    if (!CacheRigidbody2D.isKinematic)
                        CacheRigidbody2D.velocity = velocity;
                    else
                        CacheRigidbody2D.MovePosition(position);
                }
                else
                {
                    CacheRigidbody2D.velocity = Vector2.zero;
                    CacheRigidbody2D.MovePosition(position);
                }
            }
            else
            {
                CacheTransform.position = position;
                CacheTransform.rotation = rotation;
            }
        }
    }
}
