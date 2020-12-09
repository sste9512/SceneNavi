

using System;
using Moq;
using NUnit.Framework;
using SceneNavi.Framework.Client.Dependencies.Implementations;

namespace CameraTests
{
    [TestFixture]
    public class CameraTest1
    {
        [Test]
        public void EnsurePositionAccessible()
        {
            var mock = new Mock<ICameraSettings>();
            var positionMock = new PositionState();
            var buttonMock = new Mock<IButtonContext>();

            var camera = new Camera(mock.Object, positionMock, buttonMock.Object);

            camera.TransformPosition((x, y, z) => { 
                x += 1;
                y += 1;
                z += 1;
            });


            Assert.IsFalse(camera.GetCurrentPosition() == null);
          
        }


        [Test]
        public void EnsurePositionMovement()
        {
            var mock = new Mock<ICameraSettings>();
            var positionMock = new PositionState();
            var buttonMock = new Mock<IButtonContext>();

            var camera = new Camera(mock.Object, positionMock, buttonMock.Object);

            camera.TransformPosition((x, y, z) => {
                x += 1.0;
                y += 1.0;
                z += 1.0;
            }); 

            Console.WriteLine(camera.GetCurrentPosition().X);
            Assert.IsTrue(camera.GetCurrentPosition().X == 1.0);
        }


        [Test]
        public void EnsureRotationMovement()
        {
            var mock = new Mock<ICameraSettings>();
            var positionMock = new PositionState();
            var buttonMock = new Mock<IButtonContext>();

            var camera = new Camera(mock.Object, positionMock, buttonMock.Object);

            camera.TransformRotation((x, y, z) => {
                x += 1;
                y += 1;
                z += 1;
            }); 


            Assert.IsTrue(camera.GetCurrentRotation().X == 1.0);
        }



    }
}