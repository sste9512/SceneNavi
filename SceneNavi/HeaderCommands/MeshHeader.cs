using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using SceneNavi.Forms;
using SceneNavi.Utilities.OpenGLHelpers;

namespace SceneNavi.HeaderCommands
{
    public class MeshHeader : Generic, IPickableObject
    {
        public byte Type { get; private set; }
        public byte Count { get; private set; }
        public uint DLTablePointer { get; private set; }
        public uint OtherDataPointer { get; private set; }

        public List<uint> DLAddresses { get; private set; }
        public List<DisplayListEx> DLs { get; private set; }
        public List<SimpleF3DEX2.SimpleTriangle> TriangleList { get; private set; }

        public bool CachedWithTextures { get; private set; }
        public CombinerTypes CachedWithCombinerType { get; private set; }

        public int PickGLID { get; private set; }
        public List<Vector3d> MaxClipBounds { get; private set; }
        public List<Vector3d> MinClipBounds { get; private set; }

        [Browsable(false)]
        public System.Drawing.Color PickColor => System.Drawing.Color.FromArgb(this.GetHashCode() & 0xFFFFFF | (0xFF << 24));

        [Browsable(false)]
        public bool IsMoveable => false;

        [Browsable(false)]
        public OpenTK.Vector3d Position { get => Vector3d.Zero;
            set { return; } }

        public MeshHeader(Generic baseCommand)
            : base(baseCommand)
        {
            var seg = (byte)(GetAddressGeneric() >> 24);
            var adr = (uint)(GetAddressGeneric() & 0xFFFFFF);

            Type = ((byte[])BaseRom.Rom.SegmentMapping[seg])[adr];
            Count = ((byte[])BaseRom.Rom.SegmentMapping[seg])[adr + 1];
            DLTablePointer = Endian.SwapUInt32(BitConverter.ToUInt32(((byte[])BaseRom.Rom.SegmentMapping[seg]), (int)(adr + 4)));
            OtherDataPointer = Endian.SwapUInt32(BitConverter.ToUInt32(((byte[])BaseRom.Rom.SegmentMapping[seg]), (int)(adr + 8)));

            DLAddresses = new List<uint>();

            MaxClipBounds = new List<Vector3d>();
            MinClipBounds = new List<Vector3d>();

            var opaqueDLs = new List<uint>();
            var transparentDLs = new List<uint>();

            switch (Type)
            {
                case 0x00:
                    {
                        for (var i = 0; i < Count; i++)
                        {
                            opaqueDLs.Add(Endian.SwapUInt32(BitConverter.ToUInt32(((byte[])BaseRom.Rom.SegmentMapping[(byte)(DLTablePointer >> 24)]), (int)((DLTablePointer & 0xFFFFFF) + (i * 8)))));
                            transparentDLs.Add(Endian.SwapUInt32(BitConverter.ToUInt32(((byte[])BaseRom.Rom.SegmentMapping[(byte)(DLTablePointer >> 24)]), (int)((DLTablePointer & 0xFFFFFF) + (i * 8) + 4))));
                        }
                        break;
                    }
                case 0x01:
                    {
                        for (var i = 0; i < Count; i++)
                            opaqueDLs.Add(Endian.SwapUInt32(BitConverter.ToUInt32(((byte[])BaseRom.Rom.SegmentMapping[(byte)(DLTablePointer >> 24)]), (int)((DLTablePointer & 0xFFFFFF) + (i * 4)))));
                        break;
                    }
                case 0x02:
                    {
                        for (var i = 0; i < Count; i++)
                        {
                            var s1 = Endian.SwapInt16(BitConverter.ToInt16(((byte[])BaseRom.Rom.SegmentMapping[(byte)(DLTablePointer >> 24)]), (int)((DLTablePointer & 0xFFFFFF) + (i * 16))));
                            var s2 = Endian.SwapInt16(BitConverter.ToInt16(((byte[])BaseRom.Rom.SegmentMapping[(byte)(DLTablePointer >> 24)]), (int)((DLTablePointer & 0xFFFFFF) + (i * 16) + 2)));
                            var s3 = Endian.SwapInt16(BitConverter.ToInt16(((byte[])BaseRom.Rom.SegmentMapping[(byte)(DLTablePointer >> 24)]), (int)((DLTablePointer & 0xFFFFFF) + (i * 16) + 4)));
                            var s4 = Endian.SwapInt16(BitConverter.ToInt16(((byte[])BaseRom.Rom.SegmentMapping[(byte)(DLTablePointer >> 24)]), (int)((DLTablePointer & 0xFFFFFF) + (i * 16) + 6)));

                            MaxClipBounds.Add(new Vector3d(s1, 0.0, s2));
                            MinClipBounds.Add(new Vector3d(s3, 0.0, s4));

                            opaqueDLs.Add(Endian.SwapUInt32(BitConverter.ToUInt32(((byte[])BaseRom.Rom.SegmentMapping[(byte)(DLTablePointer >> 24)]), (int)((DLTablePointer & 0xFFFFFF) + (i * 16) + 8))));
                            transparentDLs.Add(Endian.SwapUInt32(BitConverter.ToUInt32(((byte[])BaseRom.Rom.SegmentMapping[(byte)(DLTablePointer >> 24)]), (int)((DLTablePointer & 0xFFFFFF) + (i * 16) + 12))));
                        }
                        break;
                    }

                default: throw new Exception(string.Format("Invalid mesh type 0x{0:X2}", Type));
            }

            DLAddresses.AddRange(opaqueDLs);
            DLAddresses.AddRange(transparentDLs);
            DLAddresses.RemoveAll(x => x == 0);
        }

        public void CreateDisplayLists(bool texenabled, CombinerTypes combinertype)
        {
            //Program.Status.Message = string.Format("Rendering room '{0}'...", (this.Parent as RoomInfoClass).Description);

            CachedWithTextures = texenabled;
            CachedWithCombinerType = combinertype;

            /* Execute DLs once before creating GL lists, to cache textures & fragment programs beforehand */
            foreach (var dl in DLAddresses) this.BaseRom.Rom.Renderer.Render(dl);

            /* Copy most recently rendered triangles - this mesh header's DL's - to triangle list */
            TriangleList = new List<SimpleF3DEX2.SimpleTriangle>();
            foreach (var st in this.BaseRom.Rom.Renderer.LastTriList) TriangleList.Add(st);

            /* Now execute DLs again, with stuff already cached, which speeds everything up! */
            DLs = new List<DisplayListEx>();
            foreach (var dl in DLAddresses)
            {
                var newdlex = new DisplayListEx(ListMode.Compile);
                this.BaseRom.Rom.Renderer.Render(dl, gldl: newdlex);
                newdlex.End();
                DLs.Add(newdlex);
            }

            /* Clear the renderer's triangle list */
            this.BaseRom.Rom.Renderer.LastTriList.Clear();

            /* Finally, from the triangle list compiled before, create a simple display list for picking purposes */
            PickGLID = GL.GenLists(1);
            GL.NewList(PickGLID, ListMode.Compile);
            GL.Disable(EnableCap.Lighting);
            GL.Disable(EnableCap.Texture2D);
            if (Initialization.SupportsFunction("glGenProgramsARB")) GL.Disable((EnableCap)All.FragmentProgram);
            GL.Begin(PrimitiveType.Triangles);
            foreach (var st in TriangleList)
            {
                GL.Vertex3(st.Vertices[0]);
                GL.Vertex3(st.Vertices[1]);
                GL.Vertex3(st.Vertices[2]);
            }
            GL.End();
            GL.EndList();
        }

        public void DestroyDisplayLists()
        {
            if (DLs == null) return;

            foreach (var gldl in DLs) gldl.Dispose();
            DLs = null;
        }

        public void Render(PickableObjectRenderType renderType)
        {
            GL.PushAttrib(AttribMask.AllAttribBits);
            if (renderType == PickableObjectRenderType.Picking)
            {
                GL.Color3(PickColor);
                GL.CallList(PickGLID);
            }
            GL.PopAttrib();
        }
    }
}
