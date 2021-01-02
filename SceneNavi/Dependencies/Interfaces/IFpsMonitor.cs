using System;

namespace SceneNavi.Dependencies.Interfaces
{
    public interface IFpsMonitor: IDisposable
    {
        float Value { get; }
        TimeSpan Sample { get; set; }
        void Update();
    }
}