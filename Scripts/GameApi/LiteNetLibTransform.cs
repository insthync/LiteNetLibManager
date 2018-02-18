using System.Collections;
using System.Collections.Generic;
using LiteNetLib.Utils;
using UnityEngine;

namespace LiteNetLibHighLevel
{
    [DisallowMultipleComponent]
    public class LiteNetLibTransform : LiteNetLibBehaviour
    {
        private struct TransformResult
        {
            public Vector3 velocity;
            public Vector3 position;
            public Quaternion rotation;
            public float timestamp;
        }

        private class TransformResultNetField : LiteNetLibNetField<TransformResult>
        {
            public override void Deserialize(NetDataReader reader)
            {
                var result = new TransformResult();
                result.velocity = new Vector3((float)reader.GetShort() * 0.01f, (float)reader.GetShort() * 0.01f, (float)reader.GetShort() * 0.01f);
                result.position = new Vector3((float)reader.GetShort() * 0.01f, (float)reader.GetShort() * 0.01f, (float)reader.GetShort() * 0.01f);
                result.rotation = new Quaternion((float)reader.GetShort() * 0.01f, (float)reader.GetShort() * 0.01f, (float)reader.GetShort() * 0.01f, (float)reader.GetShort() * 0.01f);
                result.timestamp = (float)reader.GetShort() * 0.01f;
                Value = result;
            }

            public override bool IsValueChanged(TransformResult newValue)
            {
                return !newValue.Equals(Value);
            }

            public override void Serialize(NetDataWriter writer)
            {
                writer.Put((short)(Value.velocity.x * 100));
                writer.Put((short)(Value.velocity.y * 100));
                writer.Put((short)(Value.velocity.z * 100));
                writer.Put((short)(Value.position.x * 100));
                writer.Put((short)(Value.position.y * 100));
                writer.Put((short)(Value.position.z * 100));
                writer.Put((short)(Value.rotation.x * 100));
                writer.Put((short)(Value.rotation.y * 100));
                writer.Put((short)(Value.rotation.z * 100));
                writer.Put((short)(Value.rotation.w * 100));
                writer.Put((short)(Value.timestamp * 100));
            }
        }

        [Range(0, 1)]
        public float sendInterval = 0.1f;
        public bool canClientSendResult;
        public float snapThreshold = 5.0f;
        public Transform TempTransform { get; private set; }
        public Rigidbody TempRigidbody3D { get; private set; }
        public Rigidbody2D TempRigidbody2D { get; private set; }
        public CharacterController TempCharacterController { get; private set; }
        // This list stores results of transform. Needed for non-owner client interpolation
        private readonly List<TransformResult> interpolatingResults = new List<TransformResult>();
        // Interpolation related variables
        private bool isInterpolating = false;
        private float syncElapsed = 0;
        private float interpElapsed = 0;
        private float lastClientTimestamp = 0;
        private float lastServerTimestamp = 0;
        private TransformResult startInterpolateTransformResult;
        private TransformResult lastTransformResult;

        private void Awake()
        {
            TempTransform = GetComponent<Transform>();
            TempRigidbody3D = GetComponent<Rigidbody>();
            TempRigidbody2D = GetComponent<Rigidbody2D>();
            TempCharacterController = GetComponent<CharacterController>();

            RegisterNetFunction("ClientSendResult", new LiteNetLibFunction<TransformResultNetField>(ClientSendResultCallback));
            RegisterNetFunction("ServerSendResult", new LiteNetLibFunction<TransformResultNetField>(ServerSendResultCallback));

            startInterpolateTransformResult = new TransformResult();
            startInterpolateTransformResult.position = TempTransform.position;
            startInterpolateTransformResult.rotation = TempTransform.rotation;
            startInterpolateTransformResult.velocity = Vector3.zero;
            startInterpolateTransformResult.timestamp = 0;
            lastTransformResult = startInterpolateTransformResult;
        }

        private void ClientSendResult(TransformResult result)
        {
            // Don't request to set transform if not set "canClientSendResult" to TRUE
            if (!canClientSendResult || !IsLocalClient || IsServer)
                return;
            CallNetFunction("ClientSendResult", FunctionReceivers.Server, result);
        }

        private void ServerSendResult(TransformResult result)
        {
            CallNetFunction("ServerSendResult", FunctionReceivers.All, result);
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
            result.timestamp = Time.time;
            interpolatingResults.Add(result);
        }

        private void ServerSendResultCallback(TransformResultNetField resultParam)
        {
            // Update transform only non-owner client
            if (IsLocalClient || IsServer)
                return;
            var result = resultParam.Value;
            // Discard out of order results
            if (result.timestamp <= lastServerTimestamp)
                return;
            lastServerTimestamp = result.timestamp;
            // Adding results to the results list so they can be used in interpolation process
            result.timestamp = Time.time;
            interpolatingResults.Add(result);
        }

        private void FixedUpdate()
        {
            // Sending transform to all clients
            if (IsServer)
            {
                if (syncElapsed >= sendInterval)
                {
                    lastTransformResult.timestamp = Time.time;
                    lastTransformResult.position = TempTransform.position;
                    lastTransformResult.rotation = TempTransform.rotation;
                    lastTransformResult.velocity = Vector3.zero;
                    ServerSendResult(lastTransformResult);
                    syncElapsed = 0;
                }
                syncElapsed += Time.fixedDeltaTime;
                // Interpolate transform that receives from clients
                if (canClientSendResult && !IsLocalClient)
                {
                    InterpolateTransform();
                }
            }
            // Sending client transform result to server
            else if (canClientSendResult && IsLocalClient)
            {
                if (syncElapsed >= sendInterval)
                {
                    lastTransformResult.timestamp = Time.time;
                    // Send transform to server only when there are changes on transform
                    if (lastTransformResult.position != TempTransform.position || lastTransformResult.rotation != TempTransform.rotation)
                    {
                        lastTransformResult.position = TempTransform.position;
                        lastTransformResult.rotation = TempTransform.rotation;
                        lastTransformResult.velocity = Vector3.zero;
                        ClientSendResult(lastTransformResult);
                    }
                    syncElapsed = 0;
                }
                syncElapsed += Time.fixedDeltaTime;
            }
            // Interpolating results for non-owner client objects on clients
            else if (!canClientSendResult || !IsLocalClient)
            {
                InterpolateTransform();
            }
        }

        private void InterpolateTransform()
        {
            // There should be at least two records in the results list to interpolate between them
            // And continue interpolating when there is one record left
            if (interpolatingResults.Count == 0)
                isInterpolating = false;

            if (interpolatingResults.Count >= 2)
                isInterpolating = true;

            if (isInterpolating)
            {
                if (interpElapsed == 0)
                    startInterpolateTransformResult = lastTransformResult;
                float step = 1f / sendInterval;
                lastTransformResult.position = Vector3.Lerp(startInterpolateTransformResult.position, interpolatingResults[0].position, interpElapsed);
                lastTransformResult.rotation = Quaternion.Slerp(startInterpolateTransformResult.rotation, interpolatingResults[0].rotation, interpElapsed);
                interpElapsed += step * Time.fixedDeltaTime;
                if (interpElapsed >= 1)
                {
                    interpElapsed = 0;
                    interpolatingResults.RemoveAt(0);
                }
            }
            TempTransform.position = lastTransformResult.position;
            TempTransform.rotation = lastTransformResult.rotation;
        }
    }
}
