using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

namespace SceneNavi.Framework.Client.Dependencies.Providers
{
    public class ReactiveStopwatch : IDisposable
    {

        private Subject<float> _emitter;
        private float _value;
        public TimeSpan Sample { get; set; }
        private readonly TimeSpan _sample;
        private readonly Stopwatch _stopwatch;

        public ReactiveStopwatch()
        {
            _stopwatch = new Stopwatch();
            _emitter = new Subject<float>();
            _sample = TimeSpan.FromSeconds(1);
            _value = 0;
            //_frames = 0;
        }

        public IDisposable Subscribe(Action<float> func)
        {
            return _emitter.Distinct()
                .Subscribe(func);
        }
        
        public void Start()
        {
            _stopwatch.Start();
            
            while (_emitter != null)
            {
                _emitter.OnNext(_stopwatch.Elapsed.Milliseconds);
            }
        }

        private void UpdateLoopHandler()
        {
            
        }

        public void Stop()
        {
            _stopwatch.Stop();
        }

        public void Reset()
        {
            _stopwatch.Reset();
        }

        public void Restart()
        {
            _stopwatch.Restart();
        }


        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
