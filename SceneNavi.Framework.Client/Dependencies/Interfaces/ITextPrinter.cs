using System;
using System.Drawing;
using OpenTK;

namespace SceneNavi.Framework.Client.Dependencies.Interfaces
{
    public interface ITextPrinter : IDisposable
    {
        void Print(string text, Vector2d position);
        void Print(string text, Vector2d position, Color backColor);
        void Begin(GLControl glControl);
        void Flush();
    }
}