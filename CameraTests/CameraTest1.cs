using System;
using System.ComponentModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;
using Moq;
using SceneNavi.Framework.Client.Dependencies.Implementations;

namespace CameraTests
{
    [TestClass]
    public class CameraTest1
    {
        [TestMethod]
        public void TestMethod1()
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
           // Assert.IsTrue(camera.GetCurrentPosition().X == 1);
        }

       
    }
}