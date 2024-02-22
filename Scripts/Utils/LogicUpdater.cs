using System.Diagnostics;

namespace LiteNetLibManager
{
    public class LogicUpdater
    {
        private const int MaxTicksPerUpdate = 5;

        /// <summary>
        /// Fixed delta time
        /// </summary>
        public double DeltaTime { get; private set; }

        /// <summary>
        /// Fixed delta time (float for less precision)
        /// </summary>
        public float DeltaTimeF { get; private set; }
        public double VisualDeltaTime { get; private set; }

        private long _deltaTimeTicks;
        private LogicUpdateDelegate _action;
        private long _accumulator;
        private long _lastTime;

        private readonly Stopwatch _stopwatch;
        private readonly double _stopwatchFrequency;

        public LogicUpdater(double deltaTime, LogicUpdateDelegate action)
        {
            _stopwatch = new Stopwatch();
            _stopwatchFrequency = 1.0 / Stopwatch.Frequency;
            SetDeltaTime(deltaTime);
            SetTickAction(action);
        }

        public LogicUpdater(LogicUpdateDelegate action) : this(1.0 / 60, action)
        {

        }

        public LogicUpdater(double deltaTime) : this(deltaTime, null)
        {

        }

        public LogicUpdater() : this(1.0 / 60, null)
        {

        }

        public void SetDeltaTime(double deltaTime)
        {
            DeltaTime = deltaTime;
            DeltaTimeF = (float)DeltaTime;
            _deltaTimeTicks = (long)(DeltaTime * Stopwatch.Frequency);
        }

        public void SetTickAction(LogicUpdateDelegate action)
        {
            _action = action;
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

        protected virtual void OnAction()
        {
            if (_action != null)
                _action.Invoke(this);
        }

        /// <summary>
        /// Main update method, updates internal fixed timer and do all other stuff
        /// </summary>
        public virtual void Update()
        {
            long elapsedTicks = _stopwatch.ElapsedTicks;
            long ticksDelta = elapsedTicks - _lastTime;
            VisualDeltaTime = ticksDelta * _stopwatchFrequency;
            _accumulator += ticksDelta;
            _lastTime = elapsedTicks;

            int updates = 0;
            while (_accumulator >= _deltaTimeTicks)
            {
                // Lag
                if (updates >= MaxTicksPerUpdate)
                {
                    _accumulator = 0;
                    return;
                }
                OnAction();

                _accumulator -= _deltaTimeTicks;
                updates++;
            }
        }
    }
}
