using System;
using System.Collections.Generic;
using System.Windows.Forms;
using NLog;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using SceneNavi.Dependencies.Interfaces;

namespace SceneNavi.Dependencies.Implementations
{

    public interface ICameraSettings
    {
        double Sensitivity { get; set; } // 0.5
        double CameraCoefficient { get; set; } //0.15
        double Modifier { get; set; }
    }

    public class PositionState
    {
        public Vector3d Position;
        public Vector3d Rotation;
        public Vector2d MouseCoordinate;
        public Vector2d MouseCoordOld;
    }


    public interface IButtonContext
    {
        MouseButtons ButtonsDown { get; set; }
        void RegisterControls();
    }

    public class Camera : ICamera, IMemento<PositionState>
    {
        private readonly ICameraSettings _cameraSettings;
        private readonly IButtonContext _buttonContext;
        private readonly ILogger _logger;
        private readonly PositionState _positionState;

        public delegate void OnCameraPositionChanged(Vector3d position);

        public MouseButtons ButtonsDown;

        public IDictionary<string, PositionState> States { get; set; }

        public Camera(ICameraSettings cameraSettings, PositionState positionState, /*IButtonContext buttonContext,*/ ILogger logger)
        {
            _cameraSettings = cameraSettings;
            _positionState = positionState;
            //_buttonContext = buttonContext;
            _logger = logger;

            Reset();
        }


        
        public void Reset()
        {
            _positionState.Position = new Vector3d(0.0, 0.0, -15.0);
            _positionState.Rotation = new Vector3d(0.0, 0.0, 0.0);
        }

        public void MouseCenter(Vector2d newMouseCoordinate)
        {
            _positionState.MouseCoordOld = _positionState.MouseCoordinate;
            _positionState.MouseCoordinate = newMouseCoordinate;
        }

        public void MouseMove(Vector2d newMouseCoord)
        {
            var changed = false;
            double dx = 0.0, dy = 0.0;

            if (newMouseCoord.X != _positionState.MouseCoordinate.X)
            {
                dx = (newMouseCoord.X - _positionState.MouseCoordinate.X) * _cameraSettings.Sensitivity;
                changed = true;
            }

            if (newMouseCoord.Y != _positionState.MouseCoordinate.Y)
            {
                dy = (newMouseCoord.Y - _positionState.MouseCoordinate.Y) * _cameraSettings.Sensitivity;
                changed = true;
            }

            if (changed)
            {
                if (_positionState.MouseCoordinate.X < newMouseCoord.X)
                {
                    _positionState.Rotation.Y += (newMouseCoord.X - _positionState.MouseCoordinate.X) * 0.225;
                    
                    if (_positionState.Rotation.Y > 360.0) _positionState.Rotation.Y = 0.0;
                }
                else
                {
                   
                    _positionState.Rotation.Y -= (_positionState.MouseCoordinate.X - newMouseCoord.X) * 0.225;
                    if (_positionState.Rotation.Y < -360.0) _positionState.Rotation.Y = 0.0;
                }

                if (_positionState.MouseCoordinate.Y < newMouseCoord.Y)
                {
                    if (_positionState.Rotation.X >= 90.0)
                        _positionState.Rotation.X = 90.0;
                    else
                        _positionState.Rotation.X += (dy / _cameraSettings.Sensitivity) * 0.225;
                }
                else
                {
                    if (_positionState.Rotation.X <= -90.0)
                        _positionState.Rotation.X = -90.0;
                    else
                        _positionState.Rotation.X += (dy / _cameraSettings.Sensitivity) * 0.225;
                }

                _positionState.MouseCoordOld = _positionState.MouseCoordinate;
                _positionState.MouseCoordinate = newMouseCoord;
            }
        }

        public void KeyUpdate(bool[] keysDown)
        {
            var rotYRad = (_positionState.Rotation.Y / 180.0 * Math.PI);
            var rotXRad = (_positionState.Rotation.X / 180.0 * Math.PI);

            var modifier = 2.0;
            if (keysDown[(char) Keys.Space]) modifier = 10.0;
            else if (keysDown[(char) Keys.ShiftKey]) modifier = 0.5;

            if (keysDown[(char) Keys.W])
            {
                if (_positionState.Rotation.X >= 90.0 || _positionState.Rotation.X <= -90.0)
                {
                    _positionState.Position.Y += Math.Sin(rotXRad) * _cameraSettings.CameraCoefficient * 2.0 * modifier;
                }
                else
                {
                    _positionState.Position.X -= Math.Sin(rotYRad) * _cameraSettings.CameraCoefficient * 2.0 * modifier;
                    _positionState.Position.Z += Math.Cos(rotYRad) * _cameraSettings.CameraCoefficient * 2.0 * modifier;
                    _positionState.Position.Y += Math.Sin(rotXRad) * _cameraSettings.CameraCoefficient * 2.0 * modifier;
                }
            }

            if (keysDown[(char) Keys.S])
            {
                if (_positionState.Rotation.X >= 90.0 || _positionState.Rotation.X <= -90.0)
                {
                    _positionState.Position.Y -= Math.Sin(rotXRad) * _cameraSettings.CameraCoefficient * 2.0 * modifier;
                }
                else
                {
                    _positionState.Position.X += Math.Sin(rotYRad) * _cameraSettings.CameraCoefficient * 2.0 * modifier;
                    _positionState.Position.Z -= Math.Cos(rotYRad) * _cameraSettings.CameraCoefficient * 2.0 * modifier;
                    _positionState.Position.Y -= Math.Sin(rotXRad) * _cameraSettings.CameraCoefficient * 2.0 * modifier;
                }
            }

            if (keysDown[(char) Keys.A])
            {
                _positionState.Position.X += Math.Cos(rotYRad) * _cameraSettings.CameraCoefficient * 2.0 * modifier;
                _positionState.Position.Z += Math.Sin(rotYRad) * _cameraSettings.CameraCoefficient * 2.0 * modifier;
            }

            if (keysDown[(char) Keys.D])
            {
                _positionState.Position.X -= Math.Cos(rotYRad) * _cameraSettings.CameraCoefficient * 2.0 * modifier;
                _positionState.Position.Z -= Math.Sin(rotYRad) * _cameraSettings.CameraCoefficient * 2.0 * modifier;
            }
        }

        public Vector3d GetCurrentPosition()
        {
            return _positionState.Position;
        }

        public Vector3d GetCurrentRotation()
        {
            return _positionState.Rotation;
        }

        public void TransformPosition(Action<double,double,double> action)
        {
            action.Invoke(_positionState.Position.X, _positionState.Position.Y, _positionState.Position.Z);
        }

        public void TransformRotation(Action<double, double, double> action)
        {
            action.Invoke(_positionState.Rotation.X, _positionState.Rotation.Y, _positionState.Rotation.Z);
        }


        public void RenderPosition()
        {
            GL.Rotate(_positionState.Rotation.X, 1.0, 0.0, 0.0);
            GL.Rotate(_positionState.Rotation.Y, 0.0, 1.0, 0.0);
            GL.Rotate(_positionState.Rotation.Z, 0.0, 0.0, 1.0);
            GL.Translate(_positionState.Position);
        }

        public void Dispose()
        {
        }

        public void AddState(string message, PositionState state)
        {
            _logger.Log(LogLevel.Info, state);
            States.Add(message, state);
        }

        public void Undo()
        {
            throw new NotImplementedException();
        }

        public void Redo()
        {
            throw new NotImplementedException();
        }
    }
}