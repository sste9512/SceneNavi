using System;
using System.ComponentModel;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using SceneNavi.ROMHandler;
using SceneNavi.RomHandlers;
using SceneNavi.Utilities.OpenGLHelpers;

namespace SceneNavi.HeaderCommands
{
    public class Waypoint : IPickableObject
    {
        public uint Address { get; set; }

        [DisplayName("X position")]
        public double X { get; set; }
        [DisplayName("Y position")]
        public double Y { get; set; }
        [DisplayName("Z position")]
        public double Z { get; set; }

        [Browsable(false)]
        public Vector3d Position { get => new Vector3d(X, Y, Z);
            set { X = value.X; Y = value.Y; Z = value.Z; } }

        [Browsable(false)]
        public System.Drawing.Color PickColor => System.Drawing.Color.FromArgb(GetHashCode() & 0xFFFFFF | (0xFF << 24));

        [Browsable(false)]
        public bool IsMoveable => true;

        readonly BaseRomHandler _baseRom;

        public Waypoint() { }

        public Waypoint(BaseRomHandler baseRom, uint adr)
        {
            _baseRom = baseRom;
            Address = adr;

            var segdata = (byte[])_baseRom.SegmentMapping[(byte)(adr >> 24)];
            if (segdata == null) return;

            X = Endian.SwapInt16(BitConverter.ToInt16(segdata, (int)(adr & 0xFFFFFF)));
            Y = Endian.SwapInt16(BitConverter.ToInt16(segdata, (int)(adr & 0xFFFFFF) + 2));
            Z = Endian.SwapInt16(BitConverter.ToInt16(segdata, (int)(adr & 0xFFFFFF) + 4));
        }

        public void Render(PickableObjectRenderType renderType)
        {
            GL.PushAttrib(AttribMask.AllAttribBits);

            GL.PushMatrix();
            GL.Translate(X, Y, Z);
            GL.Scale(12.0, 12.0, 12.0);

            if (renderType != PickableObjectRenderType.Picking)
            {
                GL.Color3(0.25, 0.5, 1.0);
                StockObjects.DownArrow.Render();

                if (renderType == PickableObjectRenderType.Selected)
                {
                    StockObjects.SimpleAxisMarker.Render();
                    GL.Color3(1.0, 0.5, 0.0);
                }
                else
                    GL.Color3(0.0, 0.0, 0.0);

                GL.LineWidth(4.0f);
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                StockObjects.DownArrow.Render();
            }
            else
            {
                GL.Color3(PickColor);
                StockObjects.DownArrow.Render();
            }

            GL.PopMatrix();
            GL.PopAttrib();
        }
    }
}