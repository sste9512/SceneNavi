using System;

namespace SceneNavi.Utilities.OpenGLHelpers
{
    public interface IFpsMonitor: IDisposable
    {
        float Value { get; }
        TimeSpan Sample { get; set; }
        void Update();
    }
}