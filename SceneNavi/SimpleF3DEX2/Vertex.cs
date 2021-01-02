using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using SceneNavi.RomHandlers;
using SceneNavi.Utilities.OpenGLHelpers;

namespace SceneNavi.SimpleF3DEX2
{
    internal class Vertex : HeaderCommands.IPickableObject, HeaderCommands.IStoreable
    {
        BaseRomHandler _baseRom;

        [Browsable(false)]
        public System.Drawing.Color PickColor => System.Drawing.Color.FromArgb(this.GetHashCode() & 0xFFFFFF | (0xFF << 24));

        [Browsable(false)]
        public bool IsMoveable => false;

        public Vector3d Position { get; set; }
        public Vector2d TexCoord { get; set; }
        public byte[] Colors { get; }
        public sbyte[] Normals { get; }

        public uint Address { get; set; }

        public Vertex(BaseRomHandler baseRom, byte[] raw, uint adr, Matrix4d mtx)
        {
            _baseRom = baseRom;

            Address = adr;
            adr &= 0xFFFFFF;

            Position = new Vector3d(
                Endian.SwapInt16(BitConverter.ToInt16(raw, (int)adr)),
                Endian.SwapInt16(BitConverter.ToInt16(raw, (int)(adr + 2))),
                Endian.SwapInt16(BitConverter.ToInt16(raw, (int)(adr + 4))));

            Position = Vector3d.Transform(Position, mtx);

            TexCoord = new Vector2d(
                Endian.SwapInt16(BitConverter.ToInt16(raw, (int)(adr + 8))) * General.Fixed2Float[5],
                Endian.SwapInt16(BitConverter.ToInt16(raw, (int)(adr + 10))) * General.Fixed2Float[5]);

            TexCoord.Normalize();

            Colors = new[] { raw[adr + 12], raw[adr + 13], raw[adr + 14], raw[adr + 15] };
            Normals = new[] { (sbyte)raw[adr + 12], (sbyte)raw[adr + 13], (sbyte)raw[adr + 14] };
        }

        public void Store(byte[] dataBuffer, int baseAddress)
        {
            // KLUDGE! Write to ROM HERE, write to local room data for rendering in MainForm

            // (Colors only!)
            dataBuffer[(int)(baseAddress + (Address & 0xFFFFFF)) + 12] = Colors[0];
            dataBuffer[(int)(baseAddress + (Address & 0xFFFFFF)) + 13] = Colors[1];
            dataBuffer[(int)(baseAddress + (Address & 0xFFFFFF)) + 14] = Colors[2];
            dataBuffer[(int)(baseAddress + (Address & 0xFFFFFF)) + 15] = Colors[3];
        }

        public void Render(HeaderCommands.PickableObjectRenderType renderType)
        {
            if (renderType != HeaderCommands.PickableObjectRenderType.Picking) return;
            GL.PushAttrib(AttribMask.AllAttribBits);
            GL.Disable(EnableCap.Texture2D);
            GL.Disable(EnableCap.Lighting);
            if (Initialization.SupportsFunction("glGenProgramsARB")) GL.Disable((EnableCap)All.FragmentProgram);
            GL.Disable(EnableCap.CullFace);

            GL.DepthRange(0.0, 0.999);
            GL.PointSize(50.0f);
            GL.Color3(PickColor);
            GL.Begin(PrimitiveType.Points);
            GL.Vertex3(Position);
            GL.End();

            GL.PopAttrib();
        }
    }
}
