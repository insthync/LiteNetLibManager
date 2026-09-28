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
        public delegate bool ValidateInterpolationDelegate(TransformData interpFromData, TransformData interpToData, TransformData currentData, float interpTime);
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
            internal bool OwnsExtraBuffer;

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
                OwnsExtraBuffer = false;
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
                if (Extra != null && Extra.Length > byte.MaxValue)
                    throw new System.ArgumentOutOfRangeException(nameof(Extra), "Transform extra data cannot exceed 255 bytes.");

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
                if (Extra != null && Extra.Length > 0)
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

        public class SyncTransforms : SortedList<uint, TransformData>, INetSerializable
        {
            public void Serialize(NetDataWriter writer)
            {
                writer.Put(Count);
                foreach (var entry in this)
                {
                    entry.Value.Serialize(writer);
                }
            }

            public void Deserialize(NetDataReader reader)
            {
                Clear();
                int count = reader.GetInt();
                for (int i = 0; i < count; ++i)
                {
                    TransformData entry = reader.Get<TransformData>();
                    Add(entry.Tick, entry);
                }
            }
        }

        public class SyncTransformsField : LiteNetLibSyncField<SyncTransforms>
        {
            private readonly SyncTransforms _ignoredValue = new SyncTransforms();

            public SyncTransformsField()
            {
                _value = new SyncTransforms();
            }

            internal override void SerializeValue(NetDataWriter writer)
            {
                _value.Serialize(writer);
            }

            internal override void DeserializeValue(NetDataReader reader)
            {
                _value.Deserialize(reader);
            }

            internal override void DeserializeIgnoredValue(NetDataReader reader)
            {
                _ignoredValue.Deserialize(reader);
            }

            protected override bool IsValueChanged(SyncTransforms oldValue, SyncTransforms newValue)
            {
                return true;
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
        [Tooltip("Ticks for interpolation")]
        [Min(1)]
        public uint interpolationTicks = 2;

        public event WriteSyncBufferDelegate onWriteSyncBuffer;
        public event ReadInterpBufferDelegate onReadInterpBuffer;
        public event ValidateInterpolationDelegate onValidateInterpolation;
        public event InterpolateDelegate onInterpolate;

        private TransformData _prevSyncData;
        private TransformData _interpFromData;
        private TransformData _interpToData;
        private uint _prevInterpFromTick;
        private float _startInterpTime;
        private float _endInterpTime;

        private readonly SyncTransforms _clientSyncBuffers = new SyncTransforms();
        private readonly object[] _ownerSyncRpcParameters = new object[1];
        private readonly List<byte[]> _freeExtraBuffers = new List<byte[]>(4);
        private readonly SyncTransformsField _syncBuffers = new SyncTransformsField()
        {
            syncMode = LiteNetLibSyncFieldMode.ServerToClients,
        };
        private SortedList<uint, TransformData> _interpBuffers = new SortedList<uint, TransformData>();

        private LogicUpdater _logicUpdater = null;
        private LiteNetLibRPC _ownerSyncRpc;
        private uint _interpTick;
        public uint InitialInterpTick { get; private set; }
        public uint RenderTick => _interpTick - interpolationTicks;

        private void Awake()
        {
            _syncBuffers.onChange += OnSyncBuffersChanged;
        }

        private void OnDestroy()
        {
            _syncBuffers.onChange -= OnSyncBuffersChanged;
        }

        public override void OnIdentityInitialize()
        {
            _ownerSyncRpc = GetServerRpc(nameof(OwnerSyncTransform));
            _ownerSyncRpcParameters[0] = _clientSyncBuffers;
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
            ReleaseExtraBuffers(_clientSyncBuffers);
            ReleaseExtraBuffers(_syncBuffers.Value);
            _clientSyncBuffers.Clear();
            _syncBuffers.Value.Clear();
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
                HasRotationChanged(transform.eulerAngles, transformData.EulerAngles) ||
                Vector3.Distance(transform.localScale, transformData.Scale) > scaleThreshold;

            if (!changed)
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
                StoreSyncBuffer(_syncBuffers.Value, transformData);
                _syncBuffers.MarkAsChanged();
            }
            else if (syncByOwnerClient && IsOwnedByServer)
            {
                StoreSyncBuffer(_syncBuffers.Value, transformData);
                _syncBuffers.MarkAsChanged();
            }
            else if (syncByOwnerClient && IsOwnerClient)
            {
                StoreSyncBuffer(_clientSyncBuffers, transformData);
                _ownerSyncRpc?.Call(0, LiteNetLib.DeliveryMethod.Unreliable,
                    RPCReceivers.Server, _ownerSyncRpcParameters);
            }
        }

        private bool HasRotationChanged(Vector3 currentEulerAngles, Vector3 previousEulerAngles)
        {
            return (syncData & SyncTransformState.EulerAnglesX) != 0 &&
                    Mathf.Abs(Mathf.DeltaAngle(previousEulerAngles.x, currentEulerAngles.x)) > eulerAnglesThreshold ||
                (syncData & SyncTransformState.EulerAnglesY) != 0 &&
                    Mathf.Abs(Mathf.DeltaAngle(previousEulerAngles.y, currentEulerAngles.y)) > eulerAnglesThreshold ||
                (syncData & SyncTransformState.EulerAnglesZ) != 0 &&
                    Mathf.Abs(Mathf.DeltaAngle(previousEulerAngles.z, currentEulerAngles.z)) > eulerAnglesThreshold;
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
            if (_interpBuffers.Count == 0)
            {
                _prevInterpFromTick = 0;
                return;
            }

            if (_interpTick < interpolationTicks)
                return;

            float currentTime = Time.time;
            uint renderTick = RenderTick;

            TransformData latestData = _interpBuffers.Values[_interpBuffers.Count - 1];
            if (renderTick >= latestData.Tick)
            {
                _prevInterpFromTick = 0;
                TransformData currentData = new TransformData()
                {
                    Tick = latestData.Tick,
                    Position = latestData.GetPosition(transform.position),
                    EulerAngles = latestData.GetEulerAngles(transform.eulerAngles),
                    Scale = latestData.GetScale(transform.localScale),
                };
                ApplyInterpolatedTransform(currentData, currentData, currentData, 1f);
                return;
            }

            if (_interpBuffers.Count < 2)
                return;

            // Find two ticks around renderTick
            bool foundInterval = false;

            for (int i = _interpBuffers.Count - 1; i >= 1; --i)
            {
                uint tick1 = _interpBuffers.Keys[i - 1];
                uint tick2 = _interpBuffers.Keys[i];
                TransformData data1 = _interpBuffers[tick1];
                TransformData data2 = _interpBuffers[tick2];

                if (tick1 <= renderTick && renderTick <= tick2)
                {
                    foundInterval = true;
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
                    if (_prevInterpFromTick != tick1)
                    {
                        _startInterpTime = currentTime;
                        _endInterpTime = currentTime + (_logicUpdater.DeltaTimeF * (tick2 - tick1));
                        _prevInterpFromTick = tick1;
                    }
                    break;
                }
            }

            if (!foundInterval)
                return;

            float t = Mathf.InverseLerp(_startInterpTime, _endInterpTime, currentTime);
            Quaternion fromRot = Quaternion.Euler(_interpFromData.EulerAngles);
            Quaternion toRot = Quaternion.Euler(_interpToData.EulerAngles);
            Quaternion currentRot = Quaternion.Slerp(fromRot, toRot, t);
            TransformData currentInterp = new TransformData()
            {
                Position = Vector3.Lerp(_interpFromData.Position, _interpToData.Position, t),
                EulerAngles = currentRot.eulerAngles,
                Scale = Vector3.Lerp(_interpFromData.Scale, _interpToData.Scale, t),
            };
            ApplyInterpolatedTransform(_interpFromData, _interpToData, currentInterp, t);
        }

        private void ApplyInterpolatedTransform(TransformData fromData, TransformData toData,
            TransformData currentData, float interpolationTime)
        {
            if (onValidateInterpolation != null && !onValidateInterpolation.Invoke(fromData, toData, currentData, interpolationTime))
            {
                // Not pass the validation
                return;
            }
            transform.position = currentData.Position;
            transform.eulerAngles = currentData.EulerAngles;
            transform.localScale = currentData.Scale;
            onInterpolate?.Invoke(fromData, toData, interpolationTime);
        }

        [ServerRpc]
        private void OwnerSyncTransform(SyncTransforms data)
        {
            if (!syncByOwnerClient && IsServer)
                return;
            StoreInterpolateBuffers(data, 30);
            if (!IsOwnerClient && _interpBuffers.Count > 0)
            {
                uint interpTick = _interpBuffers.Keys[_interpBuffers.Count - 1];
                if (Player != null)
                    interpTick += LogicUpdater.TimeToTick(Player.Rtt / 2, _logicUpdater.DeltaTime);
                if (_interpTick > interpTick && _interpTick - interpTick > 2)
                    _interpTick = InitialInterpTick = interpTick;
                if (interpTick > _interpTick && interpTick - _interpTick > 2)
                    _interpTick = InitialInterpTick = interpTick;
            }
            // Sync to other clients immediately
            foreach (var entry in data)
            {
                StoreSyncBuffer(_syncBuffers.Value, entry.Value);
            }
            _syncBuffers.MarkAsChanged();
        }

        private void OnSyncBuffersChanged(bool initial, SyncTransforms oldValue, SyncTransforms newValue)
        {
            if (IsServer)
                return;
            if (syncByOwnerClient && IsOwnerClient)
                return;
            StoreInterpolateBuffers(newValue, 30);
            if (_interpBuffers.Count > 0)
            {
                uint interpTick = _interpBuffers.Keys[_interpBuffers.Count - 1];
                interpTick += LogicUpdater.TimeToTick(Manager.Rtt / 2, _logicUpdater.DeltaTime);
                if (_interpTick > interpTick && _interpTick - interpTick > 2)
                    _interpTick = InitialInterpTick = interpTick;
                if (interpTick > _interpTick && interpTick - _interpTick > 2)
                    _interpTick = InitialInterpTick = interpTick;
            }
        }

        private void StoreInterpolateBuffers(SyncTransforms data, int maxBuffers = 3)
        {
            foreach (var entry in data)
            {
                if (_interpBuffers.ContainsKey(entry.Key))
                    continue;
                TransformData buffered = entry.Value;
                if (buffered.Extra != null)
                {
                    s_ExtraReader.SetSource(buffered.Extra);
                    onReadInterpBuffer?.Invoke(s_ExtraReader, entry.Key);
                }
                // Extra is consumed by the callback and may belong to a reused outgoing buffer.
                buffered.Extra = null;
                buffered.OwnsExtraBuffer = false;
                _interpBuffers.Add(entry.Key, buffered);
            }
            // Prune old ticks (keep last N)
            while (_interpBuffers.Count > maxBuffers)
            {
                _interpBuffers.RemoveAt(0);
            }
        }

        private void StoreSyncBuffer(SortedList<uint, TransformData> buffers, TransformData entry, int maxBuffers = 3)
        {
            if (!buffers.ContainsKey(entry.Tick))
            {
                s_ExtraWriter.Reset();
                onWriteSyncBuffer?.Invoke(s_ExtraWriter, entry.Tick);
                if (s_ExtraWriter.Length > 0)
                {
                    if (s_ExtraWriter.Length > byte.MaxValue)
                        throw new System.ArgumentOutOfRangeException(nameof(entry.Extra), "Transform extra data cannot exceed 255 bytes.");
                    byte[] extra = RentExtraBuffer(s_ExtraWriter.Length);
                    System.Buffer.BlockCopy(s_ExtraWriter.Data, 0, extra, 0, extra.Length);
                    entry.Extra = extra;
                    entry.OwnsExtraBuffer = true;
                }
                else if (entry.OwnsExtraBuffer && entry.Extra != null)
                {
                    // A host can relay its own transform; the two histories need separate buffers.
                    byte[] extra = RentExtraBuffer(entry.Extra.Length);
                    System.Buffer.BlockCopy(entry.Extra, 0, extra, 0, extra.Length);
                    entry.Extra = extra;
                }
                buffers.Add(entry.Tick, entry);
            }
            // Prune old ticks (keep last N)
            while (buffers.Count > maxBuffers)
            {
                ReleaseExtraBuffer(buffers.Values[0]);
                buffers.RemoveAt(0);
            }
        }

        private byte[] RentExtraBuffer(int length)
        {
            for (int i = _freeExtraBuffers.Count - 1; i >= 0; --i)
            {
                if (_freeExtraBuffers[i].Length != length)
                    continue;
                byte[] result = _freeExtraBuffers[i];
                _freeExtraBuffers.RemoveAt(i);
                return result;
            }
            return new byte[length];
        }

        private void ReleaseExtraBuffers(SortedList<uint, TransformData> buffers)
        {
            foreach (var entry in buffers)
                ReleaseExtraBuffer(entry.Value);
        }

        private void ReleaseExtraBuffer(TransformData data)
        {
            if (data.OwnsExtraBuffer && data.Extra != null && _freeExtraBuffers.Count < 4)
                _freeExtraBuffers.Add(data.Extra);
        }
    }
}
