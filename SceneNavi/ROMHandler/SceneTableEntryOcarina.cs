﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using SceneNavi.ROMHandler.Interfaces;

namespace SceneNavi.ROMHandler
{
    public class SceneTableEntryOcarina : ISceneTableEntry
    {
        #region Pseudo-code for validity - ONLY FOR OOT
        /*
         * xxxxxxxx yyyyyyyy aaaaaaaa bbbbbbbb cc dd ee ff
         * 
         * MUST
         * || (yyyyyyyy > xxxxxxxx)
         * 
         * MUST THIS
         * || (aaaaaaaa AND bbbbbbbb != 0)
         * || --> ((bbbbbbbb > aaaaaaaa) AND (bbbbbbbb == aaaaaaaa + 0x2880 || bbbbbbbb == aaaaaaaa + 0x1B00))
         * OR THIS
         * || (aaaaaaaa AND bbbbbbbb == 0)
         * 
         * MUST
         * || ((cc & 0xF0) == 0)
         * 
         * MUST
         * || ((ee & 0xF0) == 0)
         * 
         * MUST
         * || (ff == 0)
         */
        #endregion

        [ReadOnly(true)]
        public ushort Number { get; set; }
        public ushort GetNumber() { return Number; }
        public void SetNumber(ushort number) { this.Number = number; }

        [Browsable(false)]
        string dmaFilename;
        public string GetDMAFilename() { return dmaFilename; }

        [ReadOnly(true)]
        public string Name { get; private set; }
        public string GetName() { return Name; }

        [Browsable(false)]
        public int Offset { get; private set; }
        [Browsable(false)]
        public bool IsOffsetRelative { get; private set; }

        [Browsable(false)]
        uint sceneStartAddress, sceneEndAddress;
        public uint GetSceneStartAddress() { return sceneStartAddress; }
        public uint GetSceneEndAddress() { return sceneEndAddress; }

        [DisplayName("Title start"), Description("Start address of area title card")]
        public uint LabelStartAddress { get; set; }
        [DisplayName("Title end"), Description("End address of area title card")]
        public uint LabelEndAddress { get; set; }

        [DisplayName("Unknown 1"), Description("Unknown; either 0x01 or 0x02 for some dungeons, otherwise 0x00")]
        public byte Unknown1 { get; set; }
        [DisplayName("Configuration #"), Description("Specifies ex. camera effects, dynamic textures, etc.")]
        public byte ConfigurationNo { get; set; }
        [DisplayName("Unknown 3"), Description("Unknown; unique value between 0x02 and 0x0A for some dungeons")]
        public byte Unknown3 { get; set; }
        [DisplayName("Unknown 4"), Description("Unknown; always 0x00, unused or padding?")]
        public byte Unknown4 { get; set; }

        public bool IsValid()
        {
            return
                (sceneStartAddress < _baseRom.Size) && (sceneEndAddress < _baseRom.Size) && (LabelStartAddress < _baseRom.Size) && (LabelEndAddress < _baseRom.Size) &&
                ((sceneStartAddress & 0xF) == 0) && ((sceneEndAddress & 0xF) == 0) && ((LabelStartAddress & 0xF) == 0) && ((LabelEndAddress & 0xF) == 0) &&
                (sceneEndAddress > sceneStartAddress) &&
                (((LabelStartAddress != 0) && (LabelEndAddress != 0) && (LabelEndAddress > LabelStartAddress) &&
                (LabelEndAddress == LabelStartAddress + 0x2880 || LabelEndAddress == LabelStartAddress + 0x1B00)) || (LabelStartAddress == 0 && LabelEndAddress == 0));
        }

        public bool IsAllZero()
        {
            return (sceneStartAddress == 0) && (sceneEndAddress == 0) && (LabelStartAddress == 0) && (LabelEndAddress == 0);
        }

        [Browsable(false)]
        byte[] data;
        public byte[] GetData() { return data; }

        [Browsable(false)]
        List<HeaderLoader> sceneHeaders;
        public List<HeaderLoader> GetSceneHeaders() { return sceneHeaders; }

        [Browsable(false)]
        bool inROM;
        public bool IsInROM() { return inROM; }

        [Browsable(false)]
        bool isNameExternal;
        public bool IsNameExternal() { return isNameExternal; }

        [Browsable(false)]
        HeaderLoader currentSceneHeader;
        public HeaderLoader GetCurrentSceneHeader() { return currentSceneHeader; }
        public void SetCurrentSceneHeader(HeaderLoader header) { currentSceneHeader = header; }

        public HeaderCommands.Actors GetActiveTransitionData()
        {
            return (currentSceneHeader == null ? null : currentSceneHeader.Commands.FirstOrDefault(x => x.Command == CommandTypeIDs.Transitions) as HeaderCommands.Actors);
        }

        public HeaderCommands.Actors GetActiveSpawnPointData()
        {
            return (currentSceneHeader == null ? null : currentSceneHeader.Commands.FirstOrDefault(x => x.Command == CommandTypeIDs.Spawns) as HeaderCommands.Actors);
        }

        public HeaderCommands.SpecialObjects GetActiveSpecialObjs()
        {
            return (currentSceneHeader == null ? null : currentSceneHeader.Commands.FirstOrDefault(x => x.Command == CommandTypeIDs.SpecialObjects) as HeaderCommands.SpecialObjects);
        }

        public HeaderCommands.Waypoints GetActiveWaypoints()
        {
            return (currentSceneHeader == null ? null : currentSceneHeader.Commands.FirstOrDefault(x => x.Command == CommandTypeIDs.Waypoints) as HeaderCommands.Waypoints);
        }

        public HeaderCommands.Collision GetActiveCollision()
        {
            return (currentSceneHeader == null ? null : currentSceneHeader.Commands.FirstOrDefault(x => x.Command == CommandTypeIDs.Collision) as HeaderCommands.Collision);
        }

        public HeaderCommands.SettingsSoundScene GetActiveSettingsSoundScene()
        {
            return (currentSceneHeader == null ? null : currentSceneHeader.Commands.FirstOrDefault(x => x.Command == CommandTypeIDs.SettingsSoundScene) as HeaderCommands.SettingsSoundScene);
        }

        public HeaderCommands.EnvironmentSettings GetActiveEnvSettings()
        {
            return (currentSceneHeader == null ? null : currentSceneHeader.Commands.FirstOrDefault(x => x.Command == CommandTypeIDs.EnvironmentSettings) as HeaderCommands.EnvironmentSettings);
        }

        BaseRomHandler _baseRom;

        public SceneTableEntryOcarina(BaseRomHandler baseRom, string fn)
        {
            _baseRom = baseRom;
            inROM = false;

            Offset = -1;
            IsOffsetRelative = false;

            sceneStartAddress = sceneEndAddress = 0;

            LabelStartAddress = LabelEndAddress = 0;
            Unknown1 = ConfigurationNo = Unknown3 = Unknown4 = 0;

            var fs = new System.IO.FileStream(fn, System.IO.FileMode.Open);
            data = new byte[fs.Length];
            fs.Read(data, 0, (int)fs.Length);
            fs.Close();

            Name = System.IO.Path.GetFileNameWithoutExtension(fn);
        }

        public SceneTableEntryOcarina(BaseRomHandler baseRom, int ofs, bool isrel)
        {
            _baseRom = baseRom;
            inROM = true;

            Offset = ofs;
            IsOffsetRelative = isrel;

            sceneStartAddress = Endian.SwapUInt32(BitConverter.ToUInt32(IsOffsetRelative ? baseRom.CodeData : baseRom.Data, ofs));
            sceneEndAddress = Endian.SwapUInt32(BitConverter.ToUInt32(IsOffsetRelative ? baseRom.CodeData : baseRom.Data, ofs + 4));

            LabelStartAddress = Endian.SwapUInt32(BitConverter.ToUInt32(IsOffsetRelative ? baseRom.CodeData : baseRom.Data, ofs + 8));
            LabelEndAddress = Endian.SwapUInt32(BitConverter.ToUInt32(IsOffsetRelative ? baseRom.CodeData : baseRom.Data, ofs + 12));
            Unknown1 = (IsOffsetRelative ? baseRom.CodeData : baseRom.Data)[ofs + 16];
            ConfigurationNo = (IsOffsetRelative ? baseRom.CodeData : baseRom.Data)[ofs + 17];
            Unknown3 = (IsOffsetRelative ? baseRom.CodeData : baseRom.Data)[ofs + 18];
            Unknown4 = (IsOffsetRelative ? baseRom.CodeData : baseRom.Data)[ofs + 19];

            if (IsValid() && !IsAllZero())
            {
                var dma = baseRom.Files.Find(x => x.PStart == sceneStartAddress);
                if (dma != null) dmaFilename = dma.Name;

                if ((Name = (_baseRom.XmlSceneNames.Names[sceneStartAddress] as string)) == null)
                {
                    isNameExternal = false;

                    if (dma != null)
                        Name = dmaFilename;
                    else
                        Name = $"S{sceneStartAddress:X}_L{LabelStartAddress:X}";
                }
                else
                    isNameExternal = true;

                data = new byte[sceneEndAddress - sceneStartAddress];
                Array.Copy(_baseRom.Data, sceneStartAddress, data, 0, sceneEndAddress - sceneStartAddress);
            }
        }

        public void SaveTableEntry()
        {
            if (!inROM) throw new Exception("Trying to save scene table entry for external scene file");

            byte[] tmpbuf = null;

            tmpbuf = BitConverter.GetBytes(Endian.SwapUInt32(sceneStartAddress));
            Buffer.BlockCopy(tmpbuf, 0, (IsOffsetRelative ? _baseRom.CodeData : _baseRom.Data), Offset, tmpbuf.Length);

            tmpbuf = BitConverter.GetBytes(Endian.SwapUInt32(sceneEndAddress));
            Buffer.BlockCopy(tmpbuf, 0, (IsOffsetRelative ? _baseRom.CodeData : _baseRom.Data), Offset + 4, tmpbuf.Length);

            tmpbuf = BitConverter.GetBytes(Endian.SwapUInt32(LabelStartAddress));
            Buffer.BlockCopy(tmpbuf, 0, (IsOffsetRelative ? _baseRom.CodeData : _baseRom.Data), Offset + 8, tmpbuf.Length);

            tmpbuf = BitConverter.GetBytes(Endian.SwapUInt32(LabelEndAddress));
            Buffer.BlockCopy(tmpbuf, 0, (IsOffsetRelative ? _baseRom.CodeData : _baseRom.Data), Offset + 12, tmpbuf.Length);

            (IsOffsetRelative ? _baseRom.CodeData : _baseRom.Data)[Offset + 16] = Unknown1;
            (IsOffsetRelative ? _baseRom.CodeData : _baseRom.Data)[Offset + 17] = ConfigurationNo;
            (IsOffsetRelative ? _baseRom.CodeData : _baseRom.Data)[Offset + 18] = Unknown3;
            (IsOffsetRelative ? _baseRom.CodeData : _baseRom.Data)[Offset + 19] = Unknown4;
        }

        public void ReadScene(HeaderCommands.Rooms forcerooms = null)
        {
            Program.Status.Message = $"Reading scene '{this.Name}'...";

            _baseRom.SegmentMapping.Remove((byte)0x02);
            _baseRom.SegmentMapping.Add((byte)0x02, data);

            sceneHeaders = new List<HeaderLoader>();

            HeaderLoader newheader = null;
            HeaderCommands.Rooms rooms = null;
            HeaderCommands.Collision coll = null;

            if (data[0] == (byte)CommandTypeIDs.SettingsSoundScene || data[0] == (byte)CommandTypeIDs.Rooms ||
                BitConverter.ToUInt32(data, 0) == (byte)CommandTypeIDs.SubHeaders)
            {
                /* Get rooms & collision command from first header */
                newheader = new HeaderLoader(_baseRom, this, (byte)0x02, 0, 0);

                /* If external rooms should be forced, overwrite command in header */
                if (forcerooms != null)
                {
                    var roomidx = newheader.Commands.FindIndex(x => x.Command == CommandTypeIDs.Rooms);
                    if (roomidx != -1) newheader.Commands[roomidx] = forcerooms;
                }

                rooms = newheader.Commands.FirstOrDefault(x => x.Command == CommandTypeIDs.Rooms) as HeaderCommands.Rooms;
                coll = newheader.Commands.FirstOrDefault(x => x.Command == CommandTypeIDs.Collision) as HeaderCommands.Collision;
                sceneHeaders.Add(newheader);

                if (BitConverter.ToUInt32(((byte[])_baseRom.SegmentMapping[(byte)0x02]), 0) == 0x18)
                {
                    var hnum = 1;
                    var aofs = Endian.SwapUInt32(BitConverter.ToUInt32(((byte[])_baseRom.SegmentMapping[(byte)0x02]), 4));
                    while (true)
                    {
                        var rofs = Endian.SwapUInt32(BitConverter.ToUInt32(((byte[])_baseRom.SegmentMapping[(byte)0x02]), (int)(aofs & 0x00FFFFFF)));
                        if (rofs != 0)
                        {
                            if ((rofs & 0x00FFFFFF) > ((byte[])_baseRom.SegmentMapping[(byte)0x02]).Length || (rofs >> 24) != 0x02) break;
                            newheader = new HeaderLoader(_baseRom, this, (byte)0x02, (int)(rofs & 0x00FFFFFF), hnum++);

                            /* Get room command index... */
                            var roomidx = newheader.Commands.FindIndex(x => x.Command == CommandTypeIDs.Rooms);

                            /* If external rooms should be forced, overwrite command in header */
                            if (roomidx != -1 && forcerooms != null) newheader.Commands[roomidx] = forcerooms;

                            /* If rooms were found in first header, force using these! */
                            if (roomidx != -1 && rooms != null) newheader.Commands[roomidx] = rooms;

                            /* If collision was found in header, force */
                            var collidx = newheader.Commands.FindIndex(x => x.Command == CommandTypeIDs.Collision);
                            if (collidx != -1 && coll != null) newheader.Commands[collidx] = coll;

                            sceneHeaders.Add(newheader);
                        }
                        aofs += 4;
                    }
                }

                currentSceneHeader = sceneHeaders[0];
            }
        }
    }
}
