using System.Collections;
using System.Collections.Generic;
using LiteNetLib.Utils;
using UnityEngine;

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

        public bool canClientSendResult;
        public float snapThreshold = 5.0f;
        [Header("Sync Position Settings")]
        public bool notSyncPositionX;
        public bool notSyncPositionY;
        public bool notSyncPositionZ;
        [Header("Sync Rotation Settings")]
        public bool notSyncRotationX;
        public bool notSyncRotationY;
        public bool notSyncRotationZ;

        #region Cache components
        public Transform CacheTransform { get; private set; }
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
            CacheRigidbody3D = GetComponent<Rigidbody>();
            CacheRigidbody2D = GetComponent<Rigidbody2D>();
            CacheCharacterController = GetComponent<CharacterController>();

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
            if (!notSyncPositionX)
                writer.Put(CacheTransform.position.x);
            if (!notSyncPositionY)
                writer.Put(CacheTransform.position.y);
            if (!notSyncPositionZ)
                writer.Put(CacheTransform.position.z);
            if (!notSyncRotationX)
                writer.Put(CacheTransform.rotation.eulerAngles.x);
            if (!notSyncRotationY)
                writer.Put(CacheTransform.rotation.eulerAngles.y);
            if (!notSyncRotationZ)
                writer.Put(CacheTransform.rotation.eulerAngles.z);
            writer.Put(Time.realtimeSinceStartup);
        }

        public override void OnDeserialize(NetDataReader reader)
        {
            // Update transform only non-owner client
            if (IsLocalClient || IsServer)
                return;
            var result = new TransformResult();
            result.position = new Vector3(
                !notSyncPositionX ? reader.GetFloat() : 0f,
                !notSyncPositionY ? reader.GetFloat() : 0f,
                !notSyncPositionZ ? reader.GetFloat() : 0f);
            result.rotation = Quaternion.Euler(
                !notSyncRotationX ? reader.GetFloat() : 0f,
                !notSyncRotationY ? reader.GetFloat() : 0f,
                !notSyncRotationZ ? reader.GetFloat() : 0f);
            result.timestamp = reader.GetFloat();
            // Discard out of order results
            if (result.timestamp <= lastServerTimestamp)
                return;
            lastServerTimestamp = result.timestamp;
            // Adding results to the results list so they can be used in interpolation process
            result.timestamp = Time.realtimeSinceStartup;
            interpResults.Add(result);
        }

        private void FixedUpdate()
        {
            // Sending transform to all clients
            if (IsServer)
            {
                // Interpolate transform that receives from clients
                if (canClientSendResult && !IsLocalClient)
                    Interpolate();
            }
            // Sending client transform result to server
            else if (canClientSendResult && IsLocalClient)
            {
                if (syncElapsed >= sendInterval)
                {
                    // Send transform to server only when there are changes on transform
                    if (ShouldSyncBehaviour())
                        ClientSendResult(syncResult);
                    syncElapsed = 0;
                }
                syncElapsed += Time.fixedDeltaTime;
            }
            // Interpolating results for non-owner client objects on clients
            else if (!canClientSendResult || !IsLocalClient)
                Interpolate();
        }

        private void Interpolate()
        {
            // There should be at least two records in the results list to interpolate between them
            // And continue interpolating when there is one record left
            if (interpResults.Count == 0)
                isInterpolating = false;

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

                    if (CacheRigidbody3D != null)
                        InterpolateRigibody3D();
                    else if (CacheRigidbody2D != null)
                        InterpolateRigibody2D();
                    else if (CacheCharacterController != null)
                        InterpolateCharacterController();
                    else
                        InterpolateTransform();

                    interpStep += step * Time.fixedDeltaTime;
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
            if (CacheRigidbody3D != null)
                dist = (CacheRigidbody3D.position - targetPosition).magnitude;
            else if (CacheRigidbody2D != null)
                dist = (CacheRigidbody2D.position - new Vector2(targetPosition.x, targetPosition.y)).magnitude;
            else if (CacheCharacterController != null)
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
            else if (CacheCharacterController != null)
            {
                CacheTransform.position = lastResult.position;
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
            CacheRigidbody3D.MovePosition(lastResult.position);
            CacheRigidbody3D.MoveRotation(lastResult.rotation);
        }

        private void InterpolateRigibody2D()
        {
            CacheRigidbody2D.MovePosition(lastResult.position);
            CacheRigidbody2D.MoveRotation(lastResult.rotation.eulerAngles.z);
        }

        private void InterpolateCharacterController()
        {
            CacheCharacterController.Move(lastResult.position - CacheTransform.position);
            CacheTransform.rotation = lastResult.rotation;
        }

        private void InterpolateTransform()
        {
            CacheTransform.position = lastResult.position;
            CacheTransform.rotation = lastResult.rotation;
        }
    }
}
