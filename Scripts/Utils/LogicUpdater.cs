using System.Diagnostics;

namespace LiteNetLibManager
{
    public class LogicUpdater
    {
        private const int MaxTicksPerUpdate = 5;

        /// <summary>
        /// Tick count
        /// </summary>
        public uint Tick { get; private set; } = 0;

        /// <summary>
        /// Fixed delta time
        /// </summary>
        public double DeltaTime { get; private set; }

        /// <summary>
        /// Fixed delta time (float for less precision)
        /// </summary>
        public float DeltaTimeF { get; private set; }
        public double VisualDeltaTime { get; private set; }
        public LogicUpdateDelegate OnLogicUpdate;

        private long _deltaTimeTicks;
        private long _accumulator;
        private long _lastTime;

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
            VisualDeltaTime = 0.0;
            _accumulator = 0;
            _lastTime = 0;
            _stopwatch.Restart();
        }

        protected virtual void LogicUpdate()
        {
            if (OnLogicUpdate != null)
                OnLogicUpdate.Invoke(this);
        }

        /// <summary>
        /// Main update method, updates internal fixed timer and do all other stuff
        /// </summary>
        public virtual void Update()
        {
            long elapsedTime = _stopwatch.ElapsedTicks;
            long ticksDelta = elapsedTime - _lastTime;
            VisualDeltaTime = ticksDelta * _stopwatchFrequency;
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
                LogicUpdate();
                Tick++;

                _accumulator -= _deltaTimeTicks;
                updates++;
            }
        }

        public uint TimeToTick(long milliseconds)
        {
            return (uint)((milliseconds / 1000) / DeltaTime);
        }

        public void OnSyncTick(uint tick, long rtt)
        {
            uint newTick = tick + TimeToTick(rtt / 2);
            Tick = newTick;
        }
    }
}
