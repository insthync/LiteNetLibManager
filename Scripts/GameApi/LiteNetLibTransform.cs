﻿using LiteNetLib.Utils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace LiteNetLibManager
{
    public class LiteNetLibTransform : LiteNetLibBehaviour
    {
        [System.Flags]
        public enum SyncTransformState : uint
        {
            None = 0,
            PositionX = 1 << 0,
            PositionY = 1 << 1,
            PositionZ = 1 << 2,
            EulerAnglesX = 1 << 3,
            EulerAnglesY = 1 << 4,
            EulerAnglesZ = 1 << 5,
            ScaleX = 1 << 6,
            ScaleY = 1 << 7,
            ScaleZ = 1 << 8,
        }

        [System.Serializable]
        public struct TransformData : INetSerializable
        {
            public float StoreTime;
            public uint Tick;
            public SyncTransformState SyncData;
            public Vector3 Position;
            public Vector3 EulerAngles;
            public Vector3 Scale;

            public void Deserialize(NetDataReader reader)
            {
                Tick = reader.GetPackedUInt();
                SyncData = (SyncTransformState)reader.GetPackedUInt();

                Position = new Vector3(
                    !SyncData.HasFlag(SyncTransformState.PositionX) ? 0f : reader.GetFloat(),
                    !SyncData.HasFlag(SyncTransformState.PositionY) ? 0f : reader.GetFloat(),
                    !SyncData.HasFlag(SyncTransformState.PositionZ) ? 0f : reader.GetFloat());

                EulerAngles = new Vector3(
                    !SyncData.HasFlag(SyncTransformState.EulerAnglesX) ? 0f : reader.GetFloat(),
                    !SyncData.HasFlag(SyncTransformState.EulerAnglesY) ? 0f : reader.GetFloat(),
                    !SyncData.HasFlag(SyncTransformState.EulerAnglesZ) ? 0f : reader.GetFloat());

                Scale = new Vector3(
                    !SyncData.HasFlag(SyncTransformState.ScaleX) ? 0f : reader.GetFloat(),
                    !SyncData.HasFlag(SyncTransformState.ScaleY) ? 0f : reader.GetFloat(),
                    !SyncData.HasFlag(SyncTransformState.ScaleZ) ? 0f : reader.GetFloat());
            }

            public void Serialize(NetDataWriter writer)
            {
                writer.PutPackedUInt(Tick);
                writer.PutPackedUInt((uint)SyncData);

                if (SyncData.HasFlag(SyncTransformState.PositionX))
                    writer.Put(Position.x);
                if (SyncData.HasFlag(SyncTransformState.PositionY))
                    writer.Put(Position.y);
                if (SyncData.HasFlag(SyncTransformState.PositionZ))
                    writer.Put(Position.z);

                if (SyncData.HasFlag(SyncTransformState.EulerAnglesX))
                    writer.Put(EulerAngles.x);
                if (SyncData.HasFlag(SyncTransformState.EulerAnglesY))
                    writer.Put(EulerAngles.y);
                if (SyncData.HasFlag(SyncTransformState.EulerAnglesZ))
                    writer.Put(EulerAngles.z);

                if (SyncData.HasFlag(SyncTransformState.ScaleX))
                    writer.Put(Scale.x);
                if (SyncData.HasFlag(SyncTransformState.ScaleY))
                    writer.Put(Scale.y);
                if (SyncData.HasFlag(SyncTransformState.ScaleZ))
                    writer.Put(Scale.z);
            }

            public Vector3 GetPosition(Vector3 defaultPosition)
            {
                return new Vector3(
                    !SyncData.HasFlag(SyncTransformState.PositionX) ? defaultPosition.x : Position.x,
                    !SyncData.HasFlag(SyncTransformState.PositionY) ? defaultPosition.y : Position.y,
                    !SyncData.HasFlag(SyncTransformState.PositionZ) ? defaultPosition.z : Position.z);
            }

            public Vector3 GetEulerAngles(Vector3 defaultEulerAngles)
            {
                return new Vector3(
                    !SyncData.HasFlag(SyncTransformState.EulerAnglesX) ? defaultEulerAngles.x : EulerAngles.x,
                    !SyncData.HasFlag(SyncTransformState.EulerAnglesY) ? defaultEulerAngles.y : EulerAngles.y,
                    !SyncData.HasFlag(SyncTransformState.EulerAnglesZ) ? defaultEulerAngles.z : EulerAngles.z);
            }

            public Vector3 GetScale(Vector3 defaultScale)
            {
                return new Vector3(
                    !SyncData.HasFlag(SyncTransformState.ScaleX) ? defaultScale.x : Scale.x,
                    !SyncData.HasFlag(SyncTransformState.ScaleY) ? defaultScale.y : Scale.y,
                    !SyncData.HasFlag(SyncTransformState.ScaleZ) ? defaultScale.z : Scale.z);
            }
        }

        [Header("Sync Settings")]
        [Tooltip("If this is TRUE, transform data will be sent from owner client to server to update to another clients")]
        [FormerlySerializedAs("ownerClientCanSendTransform")]
        public bool syncByOwnerClient;
        public SyncTransformState syncData = SyncTransformState.PositionX | SyncTransformState.PositionY | SyncTransformState.PositionZ | SyncTransformState.EulerAnglesY;
        public float positionThreshold = 0.01f;
        public float eulerAnglesThreshold = 1f;
        public float scaleThreshold = 0.1f;
        public int keepAliveTicks = 10;
        public uint interpolationTicks = 2;

        private Vector3 _lastChangePosition;
        private Vector3 _lastChangeEulerAngles;
        private Vector3 _lastChangeScale;
        private uint _lastChangeTick;
        private uint _prevOlderTick;
        private float _startInterpTime;
        private float _endInterpTime;
        private Vector3 _olderPosition;
        private Vector3 _newerPosition;
        private Vector3 _olderEulerAngles;
        private Vector3 _newerEulerAngles;
        private Vector3 _olderScale;
        private Vector3 _newerScale;

        private SortedList<uint, TransformData> _buffers = new SortedList<uint, TransformData>();

        public uint RenderTick => Manager.Tick - interpolationTicks;

        private void Start()
        {
            Manager.LogicUpdater.OnTick += LogicUpdater_OnTick;
            _olderPosition = transform.position;
            _newerPosition = transform.position;
            _olderEulerAngles = transform.eulerAngles;
            _newerEulerAngles = transform.eulerAngles;
            _olderScale = transform.localScale;
            _newerScale = transform.localScale;
        }

        private void OnDestroy()
        {
            Manager.LogicUpdater.OnTick -= LogicUpdater_OnTick;
        }

        private void LogicUpdater_OnTick(LogicUpdater updater)
        {
            bool changed =
                Vector3.Distance(transform.position, _lastChangePosition) > positionThreshold ||
                Vector3.Angle(transform.eulerAngles, _lastChangeEulerAngles) > eulerAnglesThreshold ||
                Vector3.Distance(transform.localScale, _lastChangeScale) > scaleThreshold;
            bool keepAlive = updater.Tick - _lastChangeTick >= keepAliveTicks;

            if (!changed && !keepAlive)
                return;

            if (changed)
                _lastChangeTick = updater.Tick;
            _lastChangePosition = transform.position;
            _lastChangeEulerAngles = transform.eulerAngles;
            _lastChangeScale = transform.localScale;

            if (!syncByOwnerClient && IsServer)
            {
                StoreSyncBuffer(new TransformData()
                {
                    Tick = updater.Tick,
                    SyncData = syncData,
                    Position = transform.position,
                    EulerAngles = transform.eulerAngles,
                    Scale = transform.localScale,
                });
                RPC(ServerSyncTransform, 0, LiteNetLib.DeliveryMethod.Unreliable, _buffers.Values.ToArray());
            }
            if (syncByOwnerClient && IsOwnerClient)
            {
                StoreSyncBuffer(new TransformData()
                {
                    Tick = updater.Tick,
                    SyncData = syncData,
                    Position = transform.position,
                    EulerAngles = transform.eulerAngles,
                    Scale = transform.localScale,
                });
                RPC(OwnerSyncTransform, 0, LiteNetLib.DeliveryMethod.Unreliable, _buffers.Values.ToArray());
            }
        }

        private void Update()
        {
            if (!syncByOwnerClient && !IsServer)
            {
                InterpolateTransform();
            }
            if (syncByOwnerClient && !IsOwnerClient)
            {
                InterpolateTransform();
            }
        }

        private void InterpolateTransform()
        {
            if (_buffers.Count < 2)
            {
                _prevOlderTick = 0;
                return;
            }

            uint renderTick = RenderTick;

            // Find two ticks around renderTick
            uint olderTick = 0;
            uint newerTick = 0;

            for (int i = 0; i < _buffers.Count - 1; i++)
            {
                uint tick1 = _buffers.Keys[i];
                uint tick2 = _buffers.Keys[i + 1];
                TransformData data1 = _buffers[tick1];
                TransformData data2 = _buffers[tick2];

                if (tick1 <= renderTick && renderTick <= tick2)
                {
                    olderTick = tick1;
                    newerTick = tick2;
                    _olderPosition = data1.GetPosition(transform.position);
                    _newerPosition = data2.GetPosition(transform.position);
                    _olderEulerAngles = data1.GetEulerAngles(transform.eulerAngles);
                    _newerEulerAngles = data2.GetEulerAngles(transform.eulerAngles);
                    _olderScale = data1.GetScale(transform.localScale);
                    _newerScale = data2.GetScale(transform.localScale);
                    if (_prevOlderTick != olderTick)
                    {
                        _startInterpTime = Time.time;
                        _endInterpTime = Time.time + (Manager.LogicUpdater.DeltaTimeF * (tick2 - tick1));
                        _prevOlderTick = olderTick;
                    }
                    break;
                }
            }

            float t = Mathf.InverseLerp(_startInterpTime, _endInterpTime, Time.time);
            transform.position = Vector3.Lerp(_olderPosition, _newerPosition, t);
            Quaternion olderRot = Quaternion.Euler(_olderEulerAngles);
            Quaternion newerRot = Quaternion.Euler(_newerEulerAngles);
            transform.rotation = Quaternion.Slerp(olderRot, newerRot, t);
            transform.localScale = Vector3.Lerp(_olderScale, _newerScale, t);
        }

        [ServerRpc]
        private void OwnerSyncTransform(TransformData[] data)
        {
            if (!syncByOwnerClient && IsServer)
                return;
            StoreSyncBuffers(data, 30);
            // Sync to other clients immediately
            RPC(ServerSyncTransform, 0, LiteNetLib.DeliveryMethod.Unreliable, data);
        }

        [AllRpc]
        private void ServerSyncTransform(TransformData[] data)
        {
            if (syncByOwnerClient && IsOwnerClient)
                return;
            StoreSyncBuffers(data, 30);
        }

        private void StoreSyncBuffers(TransformData[] data, int maxBuffers = 3)
        {
            foreach (var entry in data)
            {
                if (_buffers.ContainsKey(entry.Tick))
                    continue;
                _buffers.Add(entry.Tick, entry);
            }
            // Prune old ticks (keep last N)
            while (_buffers.Count > maxBuffers)
            {
                _buffers.RemoveAt(0);
            }
        }

        private void StoreSyncBuffer(TransformData entry, int maxBuffers = 3)
        {
            if (!_buffers.ContainsKey(entry.Tick))
            {
                _buffers.Add(entry.Tick, entry);
            }
            // Prune old ticks (keep last N)
            while (_buffers.Count > maxBuffers)
            {
                _buffers.RemoveAt(0);
            }
        }
    }
}
