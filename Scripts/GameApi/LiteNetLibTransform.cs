using LiteNetLib.Utils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace LiteNetLibManager
{
    public class LiteNetLibTransform : LiteNetLibBehaviour
    {
        private static readonly NetDataWriter s_ExtraWriter = new NetDataWriter();
        private static readonly NetDataReader s_ExtraReader = new NetDataReader();
        public delegate void WriteSyncBufferDelegate(NetDataWriter writer, uint tick);
        public delegate void ReadInterpBufferDelegate(NetDataReader reader, uint tick);
        public delegate void InterpolateDelegate(TransformData interpFromData, TransformData interpToData, float interpTime);

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
            public byte[] Extra;

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

                Extra = null;
                byte extraLength = reader.GetByte();
                if (extraLength > 0)
                {
                    Extra = new byte[extraLength];
                    for (byte i = 0; i < extraLength; ++i)
                    {
                        Extra[i] = reader.GetByte();
                    }
                }
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

                byte extraLength = 0;
                if (Extra != null)
                {
                    extraLength = (byte)Extra.Length;
                    writer.Put(extraLength);
                    for (byte i = 0; i < extraLength; ++i)
                    {
                        writer.Put(Extra[i]);
                    }
                }
                else
                {
                    writer.Put(extraLength);
                }
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

        public event WriteSyncBufferDelegate onWriteSyncBuffer;
        public event ReadInterpBufferDelegate onReadInterpBuffer;
        public event InterpolateDelegate onInterpolate;

        private TransformData _prevSyncData;
        private TransformData _interpFromData;
        private TransformData _interpToData;
        private uint _prevInterpFromTick;
        private float _startInterpTime;
        private float _endInterpTime;

        private SortedList<uint, TransformData> _syncBuffers = new SortedList<uint, TransformData>();
        private SortedList<uint, TransformData> _interpBuffers = new SortedList<uint, TransformData>();

        private LogicUpdater _logicUpdater = null;
        private uint _interpTick;
        public uint InitialInterpTick { get; private set; }
        public uint RenderTick => _interpTick - interpolationTicks;

        public override void OnIdentityInitialize()
        {
            if (_logicUpdater == null)
            {
                _logicUpdater = Manager.LogicUpdater;
                _logicUpdater.OnTick += LogicUpdater_OnTick;
            }
            _interpFromData = _interpToData = new TransformData()
            {
                Position = transform.position,
                EulerAngles = transform.eulerAngles,
                Scale = transform.localScale,
            };
            ResetBuffersAndStates();
        }

        public override void OnIdentityDestroy()
        {
            if (_logicUpdater != null)
                _logicUpdater.OnTick -= LogicUpdater_OnTick;
        }

        public override void OnSetOwnerClient(bool isOwnerClient)
        {
            ResetBuffersAndStates();
        }

        private void ResetBuffersAndStates()
        {
            _syncBuffers.Clear();
            _interpBuffers.Clear();
            _interpTick = InitialInterpTick = 0;
            _prevSyncData = new TransformData()
            {
                Tick = Manager.LocalTick,
                SyncData = syncData,
                Position = transform.position,
                EulerAngles = transform.eulerAngles,
                Scale = transform.localScale,
            };
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause)
                return;
            ResetBuffersAndStates();
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
                RPC(ServerSyncTransform, 0, LiteNetLib.DeliveryMethod.Unreliable, _syncBuffers.Values.ToArray());
            }
            else if (syncByOwnerClient && IsOwnedByServer)
            {
                StoreSyncBuffer(transformData);
                RPC(ServerSyncTransform, 0, LiteNetLib.DeliveryMethod.Unreliable, _syncBuffers.Values.ToArray());
            }
            else if (syncByOwnerClient && IsOwnerClient)
            {
                StoreSyncBuffer(transformData);
                RPC(OwnerSyncTransform, 0, LiteNetLib.DeliveryMethod.Unreliable, _syncBuffers.Values.ToArray());
            }
        }

        private void Update()
        {
            if (!syncByOwnerClient && !IsServer)
            {
                InterpolateTransform();
            }
            if (syncByOwnerClient && !IsOwnedByServer && !IsOwnerClient)
            {
                InterpolateTransform();
            }
        }

        private void InterpolateTransform()
        {
            if (_interpBuffers.Count < 2)
            {
                _prevInterpFromTick = 0;
                return;
            }

            float currentTime = Time.time;
            uint renderTick = RenderTick;

            // Find two ticks around renderTick
            uint interpFromTick = 0;
            uint interpToTick = 0;

            for (int i = _interpBuffers.Count - 1; i >= 1; --i)
            {
                uint tick1 = _interpBuffers.Keys[i - 1];
                uint tick2 = _interpBuffers.Keys[i];
                TransformData data1 = _interpBuffers[tick1];
                TransformData data2 = _interpBuffers[tick2];

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
                        _endInterpTime = currentTime + (_logicUpdater.DeltaTimeF * (tick2 - tick1));
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
            onInterpolate?.Invoke(_interpFromData, _interpToData, t);
        }

        [ServerRpc]
        private void OwnerSyncTransform(TransformData[] data)
        {
            if (!syncByOwnerClient && IsServer)
                return;
            StoreInterpolateBuffers(data, 30);
            if (!IsOwnerClient && _interpBuffers.Count > 0)
            {
                uint interpTick = _interpBuffers.Keys[_interpBuffers.Count - 1];
                if (Player != null)
                    interpTick += LogicUpdater.TimeToTick(Player.Rtt / 2, _logicUpdater.DeltaTime);
                if (_interpTick > interpTick && _interpTick - interpTick > 1)
                    _interpTick = InitialInterpTick = interpTick;
                if (interpTick > _interpTick && interpTick - _interpTick > 1)
                    _interpTick = InitialInterpTick = interpTick;
            }
            // Sync to other clients immediately
            RPC(ServerSyncTransform, 0, LiteNetLib.DeliveryMethod.Unreliable, data);
        }

        [AllRpc]
        private void ServerSyncTransform(TransformData[] data)
        {
            if (IsServer)
                return;
            if (syncByOwnerClient && IsOwnerClient)
                return;
            StoreInterpolateBuffers(data, 30);
            if (_interpBuffers.Count > 0)
            {
                uint interpTick = _interpBuffers.Keys[_interpBuffers.Count - 1];
                interpTick += LogicUpdater.TimeToTick(Manager.Rtt / 2, _logicUpdater.DeltaTime);
                if (_interpTick > interpTick && _interpTick - interpTick > 1)
                    _interpTick = InitialInterpTick = interpTick;
                if (interpTick > _interpTick && interpTick - _interpTick > 1)
                    _interpTick = InitialInterpTick = interpTick;
            }
        }

        private void StoreInterpolateBuffers(TransformData[] data, int maxBuffers = 3)
        {
            foreach (var entry in data)
            {
                if (_interpBuffers.ContainsKey(entry.Tick))
                    continue;
                if (entry.Extra != null)
                {
                    s_ExtraReader.SetSource(entry.Extra);
                    onReadInterpBuffer?.Invoke(s_ExtraReader, entry.Tick);
                }
                _interpBuffers.Add(entry.Tick, entry);
            }
            // Prune old ticks (keep last N)
            while (_interpBuffers.Count > maxBuffers)
            {
                _interpBuffers.RemoveAt(0);
            }
        }

        private void StoreSyncBuffer(TransformData entry, int maxBuffers = 3)
        {
            if (!_syncBuffers.ContainsKey(entry.Tick))
            {
                s_ExtraWriter.Reset();
                onWriteSyncBuffer?.Invoke(s_ExtraWriter, entry.Tick);
                if (s_ExtraWriter.Length > 0)
                    entry.Extra = s_ExtraWriter.CopyData();
                _syncBuffers.Add(entry.Tick, entry);
            }
            // Prune old ticks (keep last N)
            while (_syncBuffers.Count > maxBuffers)
            {
                _syncBuffers.RemoveAt(0);
            }
        }
    }
}
