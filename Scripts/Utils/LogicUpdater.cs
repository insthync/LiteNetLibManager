using System.Diagnostics;

namespace LiteNetLibManager
{
    public class LogicUpdater
    {
        private const int MaxTicksPerUpdate = 5;

        public bool IsRunning => _stopwatch != null && _stopwatch.IsRunning;

        /// <summary>
        /// Local tick count
        /// </summary>
        public uint LocalTick { get; private set; } = 0;

        /// <summary>
        /// Tick count
        /// </summary>
        public uint Tick => (uint)(LocalTick + _tickOffsets);

        /// <summary>
        /// Fixed delta time
        /// </summary>
        public double DeltaTime { get; private set; }

        /// <summary>
        /// Fixed delta time (float for less precision)
        /// </summary>
        public float DeltaTimeF { get; private set; }

        public event LogicUpdateDelegate OnTick;

        private long _deltaTimeTicks = 0;
        private long _accumulator = 0;
        private long _lastTime = 0;
        private uint _latestSyncedTick = 0;
        private int _tickOffsets = 0;

        private readonly Stopwatch _stopwatch;
        private readonly double _stopwatchFrequency;

        public LogicUpdater(double deltaTime)
        {
            _stopwatch = new Stopwatch();
            _stopwatchFrequency = 1.0 / Stopwatch.Frequency;
            SetDeltaTime(deltaTime);
        }

        public LogicUpdater() : this(1.0 / 30)
        {

        }

        public void SetDeltaTime(double deltaTime)
        {
            DeltaTime = deltaTime;
            DeltaTimeF = (float)DeltaTime;
            _deltaTimeTicks = (long)(DeltaTime * Stopwatch.Frequency);
        }

        public void Start()
        {
            Reset();
        }

        public void Stop()
        {
            _stopwatch.Stop();
        }

        public void Reset()
        {
            _accumulator = 0;
            _lastTime = 0;
            _stopwatch.Restart();
        }

        protected virtual void InvokeOnTick()
        {
            if (OnTick != null)
                OnTick.Invoke(this);
        }

        /// <summary>
        /// Main update method, updates internal fixed timer and do all other stuff
        /// </summary>
        public void Update()
        {
            long elapsedTime = _stopwatch.ElapsedTicks;
            long ticksDelta = elapsedTime - _lastTime;
            _accumulator += ticksDelta;
            _lastTime = elapsedTime;

            int updates = 0;
            while (_accumulator >= _deltaTimeTicks)
            {
                // Lag
                if (updates >= MaxTicksPerUpdate)
                {
                    _accumulator = 0;
                    return;
                }
                InvokeOnTick();
                LocalTick++;

                _accumulator -= _deltaTimeTicks;
                updates++;
            }
        }

        public uint TimeToTick(long milliseconds)
        {
            return (uint)(milliseconds / 1000 / DeltaTime);
        }

        public uint TimeToTick(float milliseconds)
        {
            return (uint)(milliseconds / 1000f / DeltaTimeF);
        }

        public uint TimeInSecondsToTick(long seconds)
        {
            return (uint)(seconds / DeltaTime);
        }

        public uint TimeInSecondsToTick(float seconds)
        {
            return (uint)(seconds / DeltaTimeF);
        }

        public void OnSyncTick(uint tick, long rtt)
        {
            if (_latestSyncedTick > tick)
                return;
            _latestSyncedTick = tick;
            uint newTick = tick + TimeToTick(rtt / 2);
            _tickOffsets = (int)newTick - (int)LocalTick;
        }
    }
}
