using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using SceneNavi.Models;
using SceneNavi.ROMHandler;
using SceneNavi.RomHandlers;
using SceneNavi.Utilities.OpenGLHelpers;

namespace SceneNavi.HeaderCommands
{
    public class Actors : Generic, IStoreable
    {
        public List<Entry> ActorList { get; set; }

        public Actors(Generic baseCommand)
            : base(baseCommand)
        {
            ActorList = new List<Entry>();
            for (var i = 0; i < GetCountGeneric(); i++)
                ActorList.Add(new Entry(BaseRom, (uint) (GetAddressGeneric() + i * 16), (i + 1),
                    (Command == CommandTypeIDs.Spawns), (Command == CommandTypeIDs.Transitions)));
        }

        public void Store(byte[] dataBuffer, int baseAddress)
        {
            foreach (var actorEntry in ActorList)
            {
                Buffer.BlockCopy(actorEntry.RawData, 0, dataBuffer,
                    (int) (baseAddress + (actorEntry.Address & 0xFFFFFF)), actorEntry.RawData.Length);
            }
        }

        public class Entry : IPickableObject
        {
            public uint Address { get; set; }
            public byte[] RawData { get; set; }

            public Definition Definition { get; private set; }
            public string InternalName { get; private set; }

            public bool IsSpawnPoint { get; private set; }
            public bool IsTransitionActor { get; private set; }

            private int _numberInList;

            private ushort GetActorNumber
            {
                get
                {
                    if (Definition != null)
                    {
                        var num = XmlActorDefinitionReader.GetValueFromActor(
                            Definition.Items.Find(x => x.Usage == Usages.ActorNumber), this);
                        if (num != null) return Convert.ToUInt16(num);
                    }

                    return ushort.MaxValue;
                }
            }

            public string Name
            {
                get
                {
                    if (_baseRom == null) return "(None)";

                    var name = (string) _baseRom.Rom.XmlActorNames.Names[GetActorNumber];
                    return name ?? "Unknown actor";
                }
            }

            public string Description => _baseRom == null ? "(None)" : $"Actor #{_numberInList}; {Name}";

            public Vector3d Position
            {
                get
                {
                    if (Definition == null) return Vector3d.Zero;
                    var p = new Vector3d();
                    var px = XmlActorDefinitionReader.GetValueFromActor(
                        Definition.Items.Find(x => x.Usage == Usages.PositionX), this);
                    var py = XmlActorDefinitionReader.GetValueFromActor(
                        Definition.Items.Find(x => x.Usage == Usages.PositionY), this);
                    var pz = XmlActorDefinitionReader.GetValueFromActor(
                        Definition.Items.Find(x => x.Usage == Usages.PositionZ), this);
                    if (px != null) p.X = Convert.ToDouble(px);
                    if (py != null) p.Y = Convert.ToDouble(py);
                    if (pz != null) p.Z = Convert.ToDouble(pz);
                    return p;
                }

                set
                {
                    if (Definition == null) return;
                    XmlActorDefinitionReader.SetValueInActor(Definition.Items.Find(x => x.Usage == Usages.PositionX),
                        this, Convert.ToInt16(value.X));
                    XmlActorDefinitionReader.SetValueInActor(Definition.Items.Find(x => x.Usage == Usages.PositionY),
                        this, Convert.ToInt16(value.Y));
                    XmlActorDefinitionReader.SetValueInActor(Definition.Items.Find(x => x.Usage == Usages.PositionZ),
                        this, Convert.ToInt16(value.Z));
                }
            }

            public Vector3d Rotation
            {
                get
                {
                    if (Definition == null) return Vector3d.Zero;

                    var r = new Vector3d();
                    var rx = XmlActorDefinitionReader.GetValueFromActor(
                        Definition.Items.Find(x => x.Usage == Usages.RotationX), this);
                    var ry = XmlActorDefinitionReader.GetValueFromActor(
                        Definition.Items.Find(x => x.Usage == Usages.RotationY), this);
                    var rz = XmlActorDefinitionReader.GetValueFromActor(
                        Definition.Items.Find(x => x.Usage == Usages.RotationZ), this);
                    if (rx != null) r.X = Convert.ToDouble(rx);
                    if (ry != null) r.Y = Convert.ToDouble(ry);
                    if (rz != null) r.Z = Convert.ToDouble(rz);
                    return r;
                }

                set
                {
                    if (Definition == null) return;
                    XmlActorDefinitionReader.SetValueInActor(Definition.Items.Find(x => x.Usage == Usages.RotationX),
                        this, (short) value.X);
                    XmlActorDefinitionReader.SetValueInActor(Definition.Items.Find(x => x.Usage == Usages.RotationY),
                        this, (short) value.Y);
                    XmlActorDefinitionReader.SetValueInActor(Definition.Items.Find(x => x.Usage == Usages.RotationZ),
                        this, (short) value.Z);
                }
            }

            [Browsable(false)] public Color PickColor => Color.FromArgb(GetHashCode() & 0xFFFFFF | (0xFF << 24));

            [Browsable(false)] public bool IsMoveable => true;

            BaseRomHandler _baseRom;

            public Entry()
            {
            }

            public Entry(BaseRomHandler baseRom, uint address, int no, bool isSpawn, bool isTrans)
            {
                _baseRom = baseRom;
                Address = address;
                _numberInList = no;
                IsSpawnPoint = isSpawn;
                IsTransitionActor = isTrans;

                /* Load raw data */
                RawData = new byte[16];
                var segdata = (byte[]) _baseRom.Rom.SegmentMapping[(byte) (address >> 24)];
                if (segdata == null) return;
                Buffer.BlockCopy(segdata, (int) (address & 0xFFFFFF), RawData, 0, RawData.Length);

                /* Find definition, internal name */
                RefreshVariables();
            }

            public void RefreshVariables()
            {
                var numofs = (IsTransitionActor ? 4 : 0);
                var actnum = Endian.SwapUInt16(BitConverter.ToUInt16(RawData, numofs));

                Definition = _baseRom.Rom.XmlActorDefReader.Definitions.Find(x => x.Number == actnum);
                if (Definition == null)
                {
                    var flagFind = DefaultTypes.RoomActor;
                    if (IsTransitionActor) flagFind = DefaultTypes.TransitionActor;
                    else if (IsSpawnPoint) flagFind = DefaultTypes.SpawnPoint;
                    Definition =
                        _baseRom.Rom.XmlActorDefReader.Definitions.FirstOrDefault(x => x.IsDefault.HasFlag(flagFind));
                }

                InternalName = actnum < _baseRom.Rom.Actors.Count ? _baseRom.Rom.Actors[actnum].Name : string.Empty;
            }

            public void Render(PickableObjectRenderType renderType)
            {
                GL.PushAttrib(AttribMask.AllAttribBits);

                GL.PushMatrix();
                GL.Translate(Position);
                GL.Rotate(Rotation.Z / 182.0444444, 0.0, 0.0, 1.0);
                GL.Rotate(Rotation.Y / 182.0444444, 0.0, 1.0, 0.0);
                GL.Rotate(Rotation.X / 182.0444444, 1.0, 0.0, 0.0);

                GL.Scale(12.0, 12.0, 12.0);

                /* Determine render mode */
                if (renderType == PickableObjectRenderType.Picking)
                {
                    /* Picking, so set color to PickColor and render the PickModel */
                    GL.Color3(PickColor);
                    Definition.PickModel.Render();
                }
                else
                {
                    /* Not picking, so first render the DisplayModel */
                    Definition.DisplayModel.Render();

                    GL.LineWidth(4.0f);

                    /* Now determine outline color */
                    if (renderType == PickableObjectRenderType.Normal)
                    {
                        /* Outline depends on actor type */
                        if (IsSpawnPoint) GL.Color4(Color4.Green);
                        else if (IsTransitionActor) GL.Color4(Color4.Purple);
                        else GL.Color4(Color4.Black);
                    }
                    else
                    {
                        /* Orange outline */
                        GL.Color3(1.0, 0.5, 0.0);
                    }

                    /* Set line mode, then render PickModel (as that's more likely to not have colors baked in) */
                    GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                    Definition.PickModel.Render();

                    /* When rendering selected actor, make sure to render axis marker too */
                    if (renderType == PickableObjectRenderType.Selected)
                    {
                        /* And if a FrontOffset is given in definition, rotate before rendering the marker */
                        if (Definition.FrontOffset != 0.0) GL.Rotate(Definition.FrontOffset, 0.0, 1.0, 0.0);

                        StockObjects.AxisMarker.Render();
                    }
                }

                GL.PopMatrix();
                GL.PopAttrib();
            }
        }
    }
}