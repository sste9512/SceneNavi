using System;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using NLog;
using SceneNavi.Framework.Client.Dependencies.Interfaces;

namespace SceneNavi.Framework.Client.Dependencies.Implementations
{
    // Adapted from http://www.daniweb.com/software-development/csharp/code/408351/xna-framework-get-frames-per-second#post1743620
    /// <summary>
    /// Stopwatch-based FPS counter
    /// </summary>
    public class FpsMonitor : IFpsMonitor
    {
        private Subject<float> _emitter = new Subject<float>();

        private readonly ILogger _logger;
        public float Value { get; private set; }
        public TimeSpan Sample { get; set; }

        readonly Stopwatch _stopwatch;

        int _frames;

        public FpsMonitor(ILogger logger)
        {
            _logger = logger;
            Sample = TimeSpan.FromSeconds(1);
            Value = 0;
            _frames = 0;
            _stopwatch = Stopwatch.StartNew();
        }

        public FpsMonitor()
        {
            Sample = TimeSpan.FromSeconds(1);
            Value = 0;
            _frames = 0;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Update()
        {
            _frames++;

            if (_stopwatch.Elapsed <= Sample) return;

            Value = (float) (_frames / _stopwatch.Elapsed.TotalSeconds);
           
            _emitter.OnNext(Value);

            _stopwatch.Reset();
            _stopwatch.Start();
            _frames = 0;
        }

        public IDisposable Subscribe(Action<float> func)
        {
            return _emitter.Subscribe(func);
        }

        public void Dispose()
        {
            _emitter.Dispose();
        }
    }
}