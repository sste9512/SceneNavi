using System;
using OpenTK;

namespace SceneNavi.Framework.Client.Dependencies.Interfaces
{
    public interface ICamera: IDisposable
    {
        void Reset();
        void MouseCenter(Vector2d newMouseCoordinate);
        void MouseMove(Vector2d newMouseCoord);
        void KeyUpdate(bool[] keysDown);
        Vector3d GetCurrentPosition();
        Vector3d GetCurrentRotation();
        void TransformPosition(Action<double,double,double> action);
        void TransformRotation(Action<double, double, double> action);
        void RenderPosition();
    }
}