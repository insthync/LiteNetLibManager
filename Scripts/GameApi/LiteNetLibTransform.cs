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
        [Tooltip("Whats will be synced?")]
        public SyncTransformState syncData = SyncTransformState.PositionX | SyncTransformState.PositionY | SyncTransformState.PositionZ | SyncTransformState.EulerAnglesY;
        [Tooltip("If distance between current frame and previous frame is greater than this value, then it will determine that changes occurs and will sync transform later")]
        [Min(0.01f)]
        public float positionThreshold = 0.01f;
        [Tooltip("If angle between current frame and previous frame is greater than this value, then it will determine that changes occurs and will sync transform later")]
        [Min(0.01f)]
        public float eulerAnglesThreshold = 1f;
        [Tooltip("If distance between current frame and previous frame is greater than this value, then it will determine that changes occurs and will sync transform later")]
        [Min(0.01f)]
        public float scaleThreshold = 0.1f;
        [Tooltip("Keep alive ticks before it is stop syncing (after has no changes)")]
        public int keepAliveTicks = 10;
        [Tooltip("Ticks for interpolation")]
        [Min(1)]
        public uint interpolationTicks = 2;

        private TransformData _prevSyncData;
        private TransformData _interpFromData;
        private TransformData _interpToData;
        private uint _prevInterpFromTick;
        private float _startInterpTime;
        private float _endInterpTime;

        private SortedList<uint, TransformData> _buffers = new SortedList<uint, TransformData>();

        private bool _hasInterpTick = false;
        private uint _interpTick;
        public uint InitialInterpTick { get; private set; }
        public uint RenderTick => _interpTick - interpolationTicks;

        private void Start()
        {
            Manager.LogicUpdater.OnTick += LogicUpdater_OnTick;
            _interpFromData = _interpToData = new TransformData()
            {
                Position = transform.position,
                EulerAngles = transform.eulerAngles,
                Scale = transform.localScale,
            };
        }

        private void OnDestroy()
        {
            Manager.LogicUpdater.OnTick -= LogicUpdater_OnTick;
        }

        public override void OnSetOwnerClient(bool isOwnerClient)
        {
            base.OnSetOwnerClient(isOwnerClient);
            _hasInterpTick = false;
            TransformData transformData = _prevSyncData;
            transformData.Tick = Manager.Tick;
            transformData.SyncData = syncData;
            transformData.Position = transform.position;
            transformData.EulerAngles = transform.eulerAngles;
            transformData.Scale = transform.localScale;
            _prevSyncData = transformData;
        }

        private void LogicUpdater_OnTick(LogicUpdater updater)
        {
            _interpTick++;

            TransformData transformData = _prevSyncData;
            bool changed =
                Vector3.Distance(transform.position, transformData.Position) > positionThreshold ||
                Vector3.Angle(transform.eulerAngles, transformData.EulerAngles) > eulerAnglesThreshold ||
                Vector3.Distance(transform.localScale, transformData.Scale) > scaleThreshold;
            bool keepAlive = updater.LocalTick - transformData.Tick >= keepAliveTicks;

            if (!changed && !keepAlive)
                return;

            if (changed)
            {
                transformData.Tick = updater.LocalTick;
                transformData.SyncData = syncData;
                transformData.Position = transform.position;
                transformData.EulerAngles = transform.eulerAngles;
                transformData.Scale = transform.localScale;
                _prevSyncData = transformData;
            }

            transformData.Tick = updater.LocalTick;
            if (!syncByOwnerClient && IsServer)
            {
                StoreSyncBuffer(transformData);
                RPC(ServerSyncTransform, 0, LiteNetLib.DeliveryMethod.Unreliable, _buffers.Values.ToArray());
            }
            if (syncByOwnerClient && IsOwnerClient)
            {
                StoreSyncBuffer(transformData);
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
                _prevInterpFromTick = 0;
                return;
            }

            float currentTime = Time.time;
            uint renderTick = RenderTick;

            // Find two ticks around renderTick
            uint interpFromTick = 0;
            uint interpToTick = 0;

            for (int i = 0; i < _buffers.Count - 1; i++)
            {
                uint tick1 = _buffers.Keys[i];
                uint tick2 = _buffers.Keys[i + 1];
                TransformData data1 = _buffers[tick1];
                TransformData data2 = _buffers[tick2];

                if (tick1 <= renderTick && renderTick <= tick2)
                {
                    interpFromTick = tick1;
                    interpToTick = tick2;
                    _interpFromData = new TransformData()
                    {
                        Tick = data1.Tick,
                        Position = data1.GetPosition(transform.position),
                        EulerAngles = data1.GetEulerAngles(transform.eulerAngles),
                        Scale = data1.GetScale(transform.localScale),
                    };
                    _interpToData = new TransformData()
                    {
                        Tick = data2.Tick,
                        Position = data2.GetPosition(transform.position),
                        EulerAngles = data2.GetEulerAngles(transform.eulerAngles),
                        Scale = data2.GetScale(transform.localScale),
                    };
                    if (_prevInterpFromTick != interpFromTick)
                    {
                        _startInterpTime = currentTime;
                        _endInterpTime = currentTime + (Manager.LogicUpdater.DeltaTimeF * (tick2 - tick1));
                        _prevInterpFromTick = interpFromTick;
                    }
                    break;
                }
            }

            float t = Mathf.InverseLerp(_startInterpTime, _endInterpTime, currentTime);
            transform.position = Vector3.Lerp(_interpFromData.Position, _interpToData.Position, t);
            Quaternion olderRot = Quaternion.Euler(_interpFromData.EulerAngles);
            Quaternion newerRot = Quaternion.Euler(_interpToData.EulerAngles);
            transform.rotation = Quaternion.Slerp(olderRot, newerRot, t);
            transform.localScale = Vector3.Lerp(_interpFromData.Scale, _interpToData.Scale, t);
        }

        [ServerRpc]
        private void OwnerSyncTransform(TransformData[] data)
        {
            if (!syncByOwnerClient && IsServer)
                return;
            StoreSyncBuffers(data, 30);
            if (!IsOwnerClient && !_hasInterpTick && _buffers.Count > 0)
            {
                _hasInterpTick = true;
                uint interpTick = _buffers.Keys[_buffers.Count - 1];
                if (Player != null)
                    interpTick += LogicUpdater.TimeToTick(Player.Rtt / 2, Manager.LogicUpdater.DeltaTime);
                _interpTick = InitialInterpTick = interpTick;
            }
            // Sync to other clients immediately
            RPC(ServerSyncTransform, 0, LiteNetLib.DeliveryMethod.Unreliable, data);
        }

        [AllRpc]
        private void ServerSyncTransform(TransformData[] data)
        {
            if (syncByOwnerClient && IsOwnerClient)
                return;
            StoreSyncBuffers(data, 30);
            if (!IsServer && !_hasInterpTick && _buffers.Count > 0)
            {
                _hasInterpTick = true;
                uint interpTick = _buffers.Keys[_buffers.Count - 1];
                interpTick += LogicUpdater.TimeToTick(Manager.Rtt / 2, Manager.LogicUpdater.DeltaTime);
                _interpTick = InitialInterpTick = interpTick;
            }
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
