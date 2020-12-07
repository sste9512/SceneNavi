using System;
using System.Diagnostics;
using NLog;
using SceneNavi.Utilities.OpenGLHelpers;

namespace SceneNavi.Dependencies.Implementations
{
    // Adapted from http://www.daniweb.com/software-development/csharp/code/408351/xna-framework-get-frames-per-second#post1743620
    /// <summary>
    /// Stopwatch-based FPS counter
    /// </summary>
    public class FpsMonitor : IFpsMonitor
    {
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

        public void Update()
        {
            _frames++;

            if (_stopwatch.Elapsed <= Sample) return;
            
            Value = (float)(_frames / _stopwatch.Elapsed.TotalSeconds);

            _stopwatch.Reset();
            _stopwatch.Start();
            _frames = 0;
        }

        public void Dispose()
        {
        }
    }
}
