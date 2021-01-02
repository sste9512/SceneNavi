using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using SceneNavi.RomHandlers;

namespace SceneNavi.HeaderCommands
{
    public class Collision : Generic, IStoreable
    {
        private Vector3d AbsoluteMinimum { get; set; }
        private Vector3d AbsoluteMaximum { get; set; }
        private ushort VertexCount { get; set; }
        private uint VertexArrayOffset { get; set; }
        private ushort PolygonCount { get; set; }
        private uint PolygonArrayOffset { get; set; }
        private uint PolygonTypeOffset { get; set; }
        private uint CameraDataOffset { get; set; }
        private ushort WaterboxCount { get; set; }
        private uint WaterboxOffset { get; set; }

        private List<Vector3d> Vertices { get; set; }
        public List<Polygon> Polygons { get; private set; }
        public List<PolygonType> PolygonTypes { get; private set; }
        public List<Waterbox> Waterboxes { get; private set; }

        public Collision(Generic baseCommand)
            : base(baseCommand)
        {
            var adr = (uint)(GetAddressGeneric() & 0xFFFFFF);

            var segmentData = (byte[])BaseRom.Rom.SegmentMapping[(byte)(GetAddressGeneric() >> 24)];
            if (segmentData == null) return;

            /* Read header */
            AbsoluteMinimum = new Vector3d(
                Endian.SwapInt16(BitConverter.ToInt16(segmentData, (int)adr)),
                Endian.SwapInt16(BitConverter.ToInt16(segmentData, (int)adr + 0x2)),
                Endian.SwapInt16(BitConverter.ToInt16(segmentData, (int)adr + 0x4)));
            AbsoluteMaximum = new Vector3d(
                Endian.SwapInt16(BitConverter.ToInt16(segmentData, (int)adr + 0x6)),
                Endian.SwapInt16(BitConverter.ToInt16(segmentData, (int)adr + 0x8)),
                Endian.SwapInt16(BitConverter.ToInt16(segmentData, (int)adr + 0xA)));

            VertexCount = Endian.SwapUInt16(BitConverter.ToUInt16(segmentData, (int)adr + 0xC));
            VertexArrayOffset = Endian.SwapUInt32(BitConverter.ToUInt32(segmentData, (int)adr + 0x10));
            PolygonCount = Endian.SwapUInt16(BitConverter.ToUInt16(segmentData, (int)adr + 0x14));
            PolygonArrayOffset = Endian.SwapUInt32(BitConverter.ToUInt32(segmentData, (int)adr + 0x18));
            PolygonTypeOffset = Endian.SwapUInt32(BitConverter.ToUInt32(segmentData, (int)adr + 0x1C));
            CameraDataOffset = Endian.SwapUInt32(BitConverter.ToUInt32(segmentData, (int)adr + 0x20));
            WaterboxCount = Endian.SwapUInt16(BitConverter.ToUInt16(segmentData, (int)adr + 0x24));
            WaterboxOffset = Endian.SwapUInt32(BitConverter.ToUInt32(segmentData, (int)adr + 0x28));

            /* Read vertices */
            var vertsegdata = (byte[])BaseRom.Rom.SegmentMapping[(byte)(VertexArrayOffset >> 24)];
            if (vertsegdata != null)
            {
                Vertices = new List<Vector3d>();
                for (var i = 0; i < VertexCount; i++)
                {
                    Vertices.Add(new Vector3d(
                        Endian.SwapInt16(BitConverter.ToInt16(vertsegdata, (int)(VertexArrayOffset & 0xFFFFFF) + (i * 6))),
                        Endian.SwapInt16(BitConverter.ToInt16(vertsegdata, (int)(VertexArrayOffset & 0xFFFFFF) + (i * 6) + 2)),
                        Endian.SwapInt16(BitConverter.ToInt16(vertsegdata, (int)(VertexArrayOffset & 0xFFFFFF) + (i * 6) + 4))));
                }
            }

            /* Read polygons */
            Polygons = new List<Polygon>();
            for (var i = 0; i < PolygonCount; i++)
            {
                Polygons.Add(new Polygon(BaseRom, (uint)(PolygonArrayOffset + (i * 0x10)), i, this));
            }

            /* Read polygon types */
            PolygonTypes = new List<PolygonType>();
            var ptlen = (int)(PolygonArrayOffset - PolygonTypeOffset);                      /* Official maps */
            if (ptlen <= 0) ptlen = (int)(WaterboxOffset - PolygonTypeOffset);              /* SO imports */
            if (ptlen <= 0) ptlen = (int)(this.GetAddressGeneric() - PolygonTypeOffset);    /* HT imports */

            if (ptlen > 0)
            {
                for (uint i = PolygonTypeOffset, j = 0; i < (uint)(PolygonTypeOffset + (ptlen & 0xFFFFFF)); i += 8, j++)
                {
                    PolygonTypes.Add(new PolygonType(BaseRom, i, (int)j));
                }
            }

            /* Read camera data */
            //

            /* Read waterboxes */
            Waterboxes = new List<Waterbox>();
            for (var i = 0; i < WaterboxCount; i++)
            {
                Waterboxes.Add(new Waterbox(BaseRom, (uint)(WaterboxOffset + (i * 0x10)), i, this));
            }
        }

        public void Store(byte[] dataBuffer, int baseAddress)
        {
            foreach (var poly in this.Polygons)
            {
                /* Polygon type */
                var bytes = BitConverter.GetBytes(Endian.SwapUInt16(poly.PolygonType));
                Buffer.BlockCopy(bytes, 0, dataBuffer, (int)(baseAddress + (poly.Address & 0xFFFFFF)), bytes.Length);

                /* Normal stuff etc. */
                bytes = BitConverter.GetBytes(Endian.SwapInt16(poly.NormalXDirection));
                Buffer.BlockCopy(bytes, 0, dataBuffer, (int)(baseAddress + (poly.Address & 0xFFFFFF) + 0x8), bytes.Length);
                bytes = BitConverter.GetBytes(Endian.SwapInt16(poly.NormalYDirection));
                Buffer.BlockCopy(bytes, 0, dataBuffer, (int)(baseAddress + (poly.Address & 0xFFFFFF) + 0xA), bytes.Length);
                bytes = BitConverter.GetBytes(Endian.SwapInt16(poly.NormalZDirection));
                Buffer.BlockCopy(bytes, 0, dataBuffer, (int)(baseAddress + (poly.Address & 0xFFFFFF) + 0xC), bytes.Length);
                bytes = BitConverter.GetBytes(Endian.SwapInt16(poly.CollisionPlaneDistFromOrigin));
                Buffer.BlockCopy(bytes, 0, dataBuffer, (int)(baseAddress + (poly.Address & 0xFFFFFF) + 0xE), bytes.Length);

                /* TODO vertex IDs???  even allow editing of those?? */
            }

            foreach (var ptype in this.PolygonTypes)
            {
                /* Just get & save raw data; should be enough */
                var bytes = BitConverter.GetBytes(Endian.SwapUInt64(ptype.Raw));
                Buffer.BlockCopy(bytes, 0, dataBuffer, (int)(baseAddress + (ptype.Address & 0xFFFFFF)), bytes.Length);
            }

            foreach (var wb in this.Waterboxes)
            {
                /* RenderPosition */
                var bytes = BitConverter.GetBytes(Endian.SwapInt16(Convert.ToInt16(wb.Position.X)));
                Buffer.BlockCopy(bytes, 0, dataBuffer, (int)(baseAddress + (wb.Address & 0xFFFFFF) + 0x0), bytes.Length);
                bytes = BitConverter.GetBytes(Endian.SwapInt16(Convert.ToInt16(wb.Position.Y)));
                Buffer.BlockCopy(bytes, 0, dataBuffer, (int)(baseAddress + (wb.Address & 0xFFFFFF) + 0x2), bytes.Length);
                bytes = BitConverter.GetBytes(Endian.SwapInt16(Convert.ToInt16(wb.Position.Z)));
                Buffer.BlockCopy(bytes, 0, dataBuffer, (int)(baseAddress + (wb.Address & 0xFFFFFF) + 0x4), bytes.Length);

                /* Size */
                bytes = BitConverter.GetBytes(Endian.SwapInt16(Convert.ToInt16(wb.SizeXZ.X)));
                Buffer.BlockCopy(bytes, 0, dataBuffer, (int)(baseAddress + (wb.Address & 0xFFFFFF) + 0x6), bytes.Length);
                bytes = BitConverter.GetBytes(Endian.SwapInt16(Convert.ToInt16(wb.SizeXZ.Y)));
                Buffer.BlockCopy(bytes, 0, dataBuffer, (int)(baseAddress + (wb.Address & 0xFFFFFF) + 0x8), bytes.Length);

                /* Property thingy (room number, whatever else) */
                bytes = BitConverter.GetBytes(Endian.SwapUInt32(wb.RoomPropRaw));
                Buffer.BlockCopy(bytes, 0, dataBuffer, (int)(baseAddress + (wb.Address & 0xFFFFFF) + 0xC), bytes.Length);
            }
        }

        public class PolygonType
        {
            public class GroundType
            {
                public byte Value { get; private set; }
                public string Description { get; private set; }
                public System.Drawing.Color RenderColor { get; private set; }

                public GroundType(byte val, string desc, int col)
                {
                    Value = val;
                    Description = desc;
                    RenderColor = System.Drawing.Color.FromArgb(col);
                }
            }

            public static readonly List<GroundType> GroundTypes = new List<GroundType>()
            {
                new GroundType(0, "Dirt", Color4.RosyBrown.ToArgb()),
                new GroundType(1, "Sand", Color4.SandyBrown.ToArgb()),
                new GroundType(2, "Stone", Color4.DarkSlateGray.ToArgb()),
                new GroundType(3, "Wet stone", Color4.SlateBlue.ToArgb()),
                new GroundType(4, "Shallow water", Color4.Blue.ToArgb()),
                new GroundType(5, "Not-as-shallow water", Color4.Blue.ToArgb()),
                new GroundType(6, "Underbrush/grass", Color4.ForestGreen.ToArgb()),
                new GroundType(7, "Lava/goo", Color4.DarkRed.ToArgb()),
                new GroundType(8, "Earth/dirt", Color4.ForestGreen.ToArgb()),
                new GroundType(9, "Wooden plank", Color4.SandyBrown.ToArgb()),
                new GroundType(0xA, "Packed earth/wood", Color4.SandyBrown.ToArgb()),
                new GroundType(0xB, "Earth/dirt",  Color4.Purple.ToArgb()), //fixme?
                new GroundType(0xC, "Ceramic/ice", Color4.SlateBlue.ToArgb()),
                new GroundType(0xD, "Loose earth/carpet", Color4.LightGoldenrodYellow.ToArgb()),
                new GroundType(0xE, "Earth/dirt",  Color4.Purple.ToArgb()), //fixme?
                new GroundType(0xF, "Earth/dirt",  Color4.Purple.ToArgb()), //fixme?
            };

            //TODO
            [Flags]
            public enum Climbing
            {
                NoClimbing = 0x00,
                Ladder = 0x04,
                WholeSurface = 0x08
            }

            public int Number { get; private set; }
            public uint Address { get; private set; }
            public ulong Raw { get; set; }

            public ulong ExitNumber
            {
                get => (ulong)((Raw & 0x00000F0000000000) >> 40);
                set => Raw = ((Raw & 0xFFFFF0FFFFFFFFFF) | ((ulong)(value & 0xF) << 40));
            }

            public ulong ClimbingCrawlingFlags
            {
                get => (ulong)((Raw & 0x00F0000000000000) >> 52);
                set => Raw = ((Raw & 0xFF0FFFFFFFFFFFFF) | ((ulong)(value & 0xF) << 52));
            }

            public ulong DamageSurfaceFlags
            {
                get => (ulong)((Raw & 0x000FF00000000000) >> 44);
                set => Raw = ((Raw & 0xFFF00FFFFFFFFFFF) | ((ulong)(value & 0xFF) << 44));
            }

            public bool IsHookshotable
            {
                get => ((Raw & 0x0000000000020000) != 0);
                set { if (value) { Raw |= 0x20000; } else { Raw &= ~((ulong)0x20000); } }
            }

            public uint EchoRange
            {
                get => (uint)((Raw & 0x000000000000F000) >> 12);
                set => Raw = ((Raw & 0xFFFFFFFFFFFF0FFF) | ((ulong)(value & 0xF) << 12));
            }

            public uint EnvNumber
            {
                get => (uint)((Raw & 0x0000000000000F00) >> 8);
                set => Raw = ((Raw & 0xFFFFFFFFFFFFF0FF) | ((ulong)(value & 0xF) << 8));
            }

            public bool IsSteep
            {
                get => ((Raw & 0x0000000000000030) == 0x10);
                set { if (value) { Raw |= 0x10; } else { Raw &= ~((ulong)0x10); } }
            }

            public uint TerrainType
            {
                get => (uint)((Raw & 0x00000000000000F0) >> 4);
                set => Raw = ((Raw & 0xFFFFFFFFFFFFFF0F) | ((ulong)(value & 0xF) << 4));
            }

            public uint GroundTypeID
            {
                get => (uint)(Raw & 0x000000000000000F);
                set => Raw = ((Raw & 0xFFFFFFFFFFFFFFF0) | (ulong)((value & 0xF)));
            }

            public System.Drawing.Color RenderColor
            {
                get
                {
                    var rgb = Color4.White.ToArgb();

                    switch (GroundTypeID)
                    {
                        /* Dirt */
                        case 0: rgb = Color4.RosyBrown.ToArgb(); break;
                        /* Sand / wood / earth */
                        case 1:
                        case 9:
                        case 0xA: rgb = Color4.SandyBrown.ToArgb(); break;
                        /* Stone */
                        case 2: rgb = Color4.DarkSlateGray.ToArgb(); break;
                        /* Wet stone */
                        case 3: rgb = Color4.SlateBlue.ToArgb(); break;
                        /* Water */
                        case 4:
                        case 5: rgb = Color4.Blue.ToArgb(); break;
                        /* Grass / other ground */
                        case 6:
                        case 8: rgb = Color4.ForestGreen.ToArgb(); break;
                        /* Lava / goo */
                        case 7: rgb = Color4.DarkRed.ToArgb(); break;
                        /* Ice / ceramic */
                        case 0xC: rgb = Color4.SlateBlue.ToArgb(); break;
                        /* Loose earth / carpet */
                        case 0xD: rgb = Color4.LightGoldenrodYellow.ToArgb(); break;

                        /* ??? unknown */
                        case 0xB:
                        case 0xE:
                        case 0xF: rgb = Color4.Purple.ToArgb(); break;
                    }

                    return System.Drawing.Color.FromArgb((rgb & 0xFFFFFF) | (128 << 24));
                }
            }

            public string Description
            {
                get
                {
                    if (_baseRom == null)
                        return "(None)";
                    else
                    {
                        var sb = new StringBuilder();
                        sb.AppendFormat("#{0}: {1}", Number, GroundTypes.FirstOrDefault(x => x.Value == GroundTypeID)?.Description);
                        if (ExitNumber != 0) sb.AppendFormat(", triggers exit #{0}", ExitNumber);
                        //more?
                        return sb.ToString();
                    }
                }
            }

            public bool IsDummy => (_baseRom == null);

            BaseRomHandler _baseRom;

            public PolygonType()
            {
                Number = -1;
            }

            public PolygonType(BaseRomHandler baseRom, uint adr, int number)
            {
                _baseRom = baseRom;
                Address = adr;
                Number = number;

                var segmentData = (byte[])_baseRom.Rom.SegmentMapping[(byte)(adr >> 24)];
                if (segmentData == null) return;

                Raw = Endian.SwapUInt64(BitConverter.ToUInt64(segmentData, (int)(adr & 0xFFFFFF)));
            }
        }

        public class Polygon : IPickableObject
        {
            public int Number { get; private set; }
            public uint Address { get; private set; }

            public ushort PolygonType { get; set; }
            public ushort[] VertexIDs { get; set; }
            public Vector3d[] Vertices { get; set; }
            public short NormalXDirection { get; set; }
            public short NormalYDirection { get; set; }
            public short NormalZDirection { get; set; }
            public short CollisionPlaneDistFromOrigin { get; set; }

            [Browsable(false)]
            public Vector3d Position { get => Vector3d.Zero;
                set { } }

            [Browsable(false)]
            public System.Drawing.Color PickColor => System.Drawing.Color.FromArgb(this.GetHashCode() & 0xFFFFFF | (0xFF << 24));

            [Browsable(false)]
            public bool IsMoveable => false;

            public string Description => _baseRom == null ? "(None)" : string.Format("#{0}: Vertices {1} / {2} / {3}, type #{4}", Number, VertexIDs[0], VertexIDs[1], VertexIDs[2], PolygonType);

            public bool IsDummy => (_baseRom == null);

            readonly BaseRomHandler _baseRom;
            readonly Collision _parentCollisionHeader;

            public Polygon() { }

            public Polygon(BaseRomHandler baseRom, uint adr, int number, Collision colheader)
            {
                _baseRom = baseRom;
                Address = adr;
                Number = number;
                _parentCollisionHeader = colheader;

                var segdata = (byte[])_baseRom.Rom.SegmentMapping[(byte)(adr >> 24)];
                if (segdata == null) return;

                PolygonType = Endian.SwapUInt16(BitConverter.ToUInt16(segdata, (int)(adr & 0xFFFFFF)));
                NormalXDirection = Endian.SwapInt16(BitConverter.ToInt16(segdata, (int)(adr & 0xFFFFFF) + 0x8));
                NormalYDirection = Endian.SwapInt16(BitConverter.ToInt16(segdata, (int)(adr & 0xFFFFFF) + 0xA));
                NormalZDirection = Endian.SwapInt16(BitConverter.ToInt16(segdata, (int)(adr & 0xFFFFFF) + 0xC));
                CollisionPlaneDistFromOrigin = Endian.SwapInt16(BitConverter.ToInt16(segdata, (int)(adr & 0xFFFFFF) + 0xE));

                /* Read vertex IDs & fetch vertices */
                VertexIDs = new ushort[3];
                Vertices = new Vector3d[3];
                for (var i = 0; i < 3; i++)
                {
                    var vidx = (ushort)(Endian.SwapUInt16(BitConverter.ToUInt16(segdata, (int)(adr & 0xFFFFFF) + 0x2 + (i * 2))) & 0xFFF);
                    VertexIDs[i] = vidx;
                    Vertices[i] = _parentCollisionHeader.Vertices[vidx];
                }
            }

            public void Render(PickableObjectRenderType renderType)
            {
                if (renderType == PickableObjectRenderType.Picking)
                {
                    GL.Color3(PickColor);
                    GL.Begin(PrimitiveType.Triangles);
                    foreach (var v in Vertices) GL.Vertex3(v);
                    GL.End();
                }
                else
                {
                    if (renderType != PickableObjectRenderType.NoColor)
                        GL.Color4(_parentCollisionHeader.PolygonTypes[PolygonType].RenderColor);

                    foreach (var v in Vertices) GL.Vertex3(v);
                }
            }
        }

        public class Waterbox : IPickableObject
        {
            public int Number { get; private set; }
            public uint Address { get; private set; }

            public Vector3d Position { get; set; }
            public Vector2d SizeXZ { get; set; }

            public uint RoomPropRaw { get; private set; }

            public ushort RoomNumber
            {
                get => (ushort)(RoomPropRaw >> 13);
                set => RoomPropRaw = (uint)(Properties | (value << 13));
            }

            public ushort Properties
            {
                get => (ushort)(RoomPropRaw & 0x1FFF);
                set => RoomPropRaw = (uint)((RoomNumber << 13) | value);
            }

            [Browsable(false)]
            public System.Drawing.Color PickColor => System.Drawing.Color.FromArgb(this.GetHashCode() & 0xFFFFFF | (0xFF << 24));

            [Browsable(false)]
            public bool IsMoveable => true;

            public string Description => _baseRom == null ? "(None)" : $"Waterbox #{(Number + 1)}: X: {Position.X}, Y: {Position.Y}, Z: {Position.Z}";

            public bool IsDummy => (_baseRom == null);

            readonly BaseRomHandler _baseRom;
            private Collision _parentCollisionHeader;

            public Waterbox() { }

            public Waterbox(BaseRomHandler baseRom, uint adr, int number, Collision colheader)
            {
                _baseRom = baseRom;
                Address = adr;
                Number = number;
                _parentCollisionHeader = colheader;

                var segmentData = (byte[])_baseRom.Rom.SegmentMapping[(byte)(adr >> 24)];
                if (segmentData == null) return;

                Position = new Vector3d(
                    (double)Endian.SwapInt16(BitConverter.ToInt16(segmentData, (int)(adr & 0xFFFFFF))),
                    (double)Endian.SwapInt16(BitConverter.ToInt16(segmentData, (int)((adr & 0xFFFFFF) + 2))),
                    (double)Endian.SwapInt16(BitConverter.ToInt16(segmentData, (int)((adr & 0xFFFFFF) + 4))));

                SizeXZ = new Vector2d(
                    (double)Endian.SwapInt16(BitConverter.ToInt16(segmentData, (int)((adr & 0xFFFFFF) + 6))),
                    (double)Endian.SwapInt16(BitConverter.ToInt16(segmentData, (int)((adr & 0xFFFFFF) + 8))));

                RoomPropRaw = (Endian.SwapUInt32(BitConverter.ToUInt32(segmentData, (int)((adr & 0xFFFFFF) + 12))) & 0xFFFFFF);
            }

            public void Render(PickableObjectRenderType renderType)
            {
                if (renderType == PickableObjectRenderType.Picking)
                {
                    GL.Color3(PickColor);
                    GL.Begin(PrimitiveType.Quads);
                    RenderVertices();
                    GL.End();
                }
                else
                    RenderVertices();
            }

            private void RenderVertices()
            {
                GL.Vertex3(Position.X, Position.Y, Position.Z);
                GL.Vertex3(Position.X, Position.Y, Position.Z + SizeXZ.Y);
                GL.Vertex3(Position.X + SizeXZ.X, Position.Y, Position.Z + SizeXZ.Y);
                GL.Vertex3(Position.X + SizeXZ.X, Position.Y, Position.Z);
            }
        }
    }
}
