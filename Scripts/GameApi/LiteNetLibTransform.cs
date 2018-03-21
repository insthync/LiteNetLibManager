using System.Collections;
using System.Collections.Generic;
using LiteNetLib.Utils;
using UnityEngine;
using UnityEngine.AI;

namespace LiteNetLibHighLevel
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

        public bool canClientSendResult;
        public float snapThreshold = 5.0f;
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

        // This list stores results of transform. Needed for non-owner client interpolation
        private readonly List<TransformResult> interpResults = new List<TransformResult>();
        // Interpolation related variables
        private bool isInterpolating = false;
        private float syncElapsed = 0;
        private float interpStep = 0;
        private float lastClientTimestamp = 0;
        private float lastServerTimestamp = 0;
        private TransformResult startInterpResult;
        private TransformResult endInterpResult;
        private TransformResult lastResult;
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

            RegisterNetFunction("ClientSendResult", new LiteNetLibFunction<TransformResultNetField>(ClientSendResultCallback));
        }

        private void Start()
        {
            lastResult = new TransformResult();
            lastResult.position = CacheTransform.position;
            lastResult.rotation = CacheTransform.rotation;
            lastResult.timestamp = Time.realtimeSinceStartup;
            syncResult = lastResult;
            interpResults.Add(lastResult);
        }

        private void ClientSendResult(TransformResult result)
        {
            // Don't request to set transform if not set "canClientSendResult" to TRUE
            if (!canClientSendResult || !IsLocalClient || IsServer)
                return;
            CallNetFunction("ClientSendResult", FunctionReceivers.Server, result);
        }

        private void ClientSendResultCallback(TransformResultNetField resultParam)
        {
            // Don't update transform follow client's request if not set "canClientSendResult" to TRUE or it's the server
            if (!canClientSendResult || IsLocalClient)
                return;
            var result = resultParam.Value;
            // Discard out of order results
            if (result.timestamp <= lastClientTimestamp)
                return;
            lastClientTimestamp = result.timestamp;
            // Adding results to the results list so they can be used in interpolation process
            result.timestamp = Time.realtimeSinceStartup;
            interpResults.Add(result);
        }

        public override bool ShouldSyncBehaviour()
        {
            if (syncResult.position != CacheTransform.position || syncResult.rotation != CacheTransform.rotation)
            {
                syncResult.position = CacheTransform.position;
                syncResult.rotation = CacheTransform.rotation;
                syncResult.timestamp = Time.realtimeSinceStartup;
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
            writer.Put(Time.realtimeSinceStartup);
        }

        public override void OnDeserialize(NetDataReader reader)
        {
            // Update transform only non-owner client
            if ((canClientSendResult && IsLocalClient) || IsServer)
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
            result.timestamp = Time.realtimeSinceStartup;
            interpResults.Add(result);
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
            if (canClientSendResult && IsLocalClient)
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

            if (CacheRigidbody3D == null && CacheRigidbody2D == null)
                UpdateSync(false);
        }

        private void FixedUpdate()
        {
            if (CacheRigidbody3D != null || CacheRigidbody2D != null)
                UpdateSync(false);
        }

        private void UpdateSync(bool isFixedUpdate)
        {
            // Sending transform to all clients
            if (IsServer)
            {
                // Interpolate transform that receives from clients
                if (canClientSendResult && !IsLocalClient)
                    Interpolate(isFixedUpdate);
            }
            // Interpolating results for non-owner client objects on clients
            else if (!canClientSendResult || !IsLocalClient)
                Interpolate(isFixedUpdate);
        }

        private void Interpolate(bool isFixedUpdate)
        {
            // There should be at least two records in the results list to interpolate between them
            // And continue interpolating when there is one record left
            if (interpResults.Count == 0 && isInterpolating)
            {
                isInterpolating = false;
                if (CacheRigidbody3D != null)
                    CacheRigidbody3D.velocity = Vector3.zero;
                if (CacheRigidbody2D != null)
                    CacheRigidbody2D.velocity = Vector2.zero;
            }

            if (interpResults.Count > 0)
                isInterpolating = true;

            if (isInterpolating)
            {
                var lastInterpResult = interpResults[interpResults.Count - 1];
                if (!ShouldSnap(lastInterpResult.position))
                {
                    if (interpStep == 0)
                    {
                        startInterpResult = lastResult;
                        endInterpResult = interpResults[0];
                    }

                    float step = 1f / sendInterval;
                    lastResult.position = Vector3.Lerp(startInterpResult.position, endInterpResult.position, interpStep);
                    lastResult.rotation = Quaternion.Slerp(startInterpResult.rotation, endInterpResult.rotation, interpStep);

                    if (CacheNavMeshAgent != null)
                        InterpolateNavMeshAgent();
                    else if (CacheCharacterController != null)
                        InterpolateCharacterController();
                    else if (CacheRigidbody3D != null)
                        InterpolateRigibody3D();
                    else if (CacheRigidbody2D != null)
                        InterpolateRigibody2D();
                    else
                        InterpolateTransform();

                    interpStep += step * (isFixedUpdate ? Time.fixedDeltaTime : Time.deltaTime);
                    if (interpStep >= 1)
                    {
                        interpStep = 0f;
                        interpResults.RemoveAt(0);
                    }
                }
                else
                {
                    lastResult.position = lastInterpResult.position;
                    lastResult.rotation = lastInterpResult.rotation;
                    Snap();
                    interpResults.Clear();
                    interpStep = 0;
                }
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

        private void Snap()
        {
            if (CacheRigidbody3D != null)
            {
                CacheRigidbody3D.position = lastResult.position;
                CacheRigidbody3D.rotation = lastResult.rotation;
            }
            else if (CacheRigidbody2D != null)
            {
                CacheRigidbody2D.position = lastResult.position;
                CacheRigidbody2D.rotation = lastResult.rotation.eulerAngles.z;
            }
            else if (CacheNavMeshAgent != null)
            {
                CacheNavMeshAgent.Warp(lastResult.position);
                CacheTransform.rotation = lastResult.rotation;
            }
            else
            {
                CacheTransform.position = lastResult.position;
                CacheTransform.rotation = lastResult.rotation;
            }
        }

        private void InterpolateRigibody3D()
        {
            CacheRigidbody3D.velocity = (lastResult.position - CacheRigidbody3D.position) / sendInterval;
            CacheRigidbody3D.MoveRotation(lastResult.rotation);
        }

        private void InterpolateRigibody2D()
        {
            CacheRigidbody2D.velocity = ((Vector2)lastResult.position - CacheRigidbody2D.position) / sendInterval;
            CacheRigidbody2D.MoveRotation(lastResult.rotation.eulerAngles.z);
        }

        private void InterpolateCharacterController()
        {
            CacheCharacterController.Move(lastResult.position - CacheTransform.position);
            CacheTransform.rotation = lastResult.rotation;
        }

        private void InterpolateNavMeshAgent()
        {
            CacheNavMeshAgent.Move(lastResult.position - CacheTransform.position);
            CacheTransform.rotation = lastResult.rotation;
        }

        private void InterpolateTransform()
        {
            CacheTransform.position = lastResult.position;
            CacheTransform.rotation = lastResult.rotation;
        }
    }
}
