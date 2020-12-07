using System;

namespace SceneNavi.Framework.Client.Dependencies.Interfaces
{
    public interface IFpsMonitor: IDisposable
    {
        float Value { get; }
        TimeSpan Sample { get; set; }
        void Update();
    }
}