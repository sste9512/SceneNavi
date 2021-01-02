using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using SceneNavi.HeaderCommands;
using SceneNavi.ROMHandler.Interfaces;
using SceneNavi.RomHandlers;

namespace SceneNavi.ROMHandler
{
    class SceneTableEntryMajora : ISceneTableEntry
    {
        [ReadOnly(true)]
        public ushort Number { get; set; }
        public ushort GetNumber() { return Number; }
        public void SetNumber(ushort number) { Number = number; }

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

        [DisplayName("Unknown 1")]
        public byte Unknown1 { get; set; }
        [DisplayName("Unknown 2")]
        public byte Unknown2 { get; set; }
        [DisplayName("Unknown 3")]
        public byte Unknown3 { get; set; }
        [DisplayName("Unknown 4")]
        public byte Unknown4 { get; set; }
        [DisplayName("Padding?")]
        public uint PresumedPadding { get; set; }

        public bool IsValid()
        {
            return ((sceneStartAddress < _baseRom.Size) && (sceneEndAddress < _baseRom.Size) && ((sceneStartAddress & 0xF) == 0) && ((sceneEndAddress & 0xF) == 0) &&
                (sceneEndAddress > sceneStartAddress) && (PresumedPadding == 0));
        }

        public bool IsAllZero()
        {
            return (sceneStartAddress == 0) && (sceneEndAddress == 0) &&
                (Unknown1 == 0) && (Unknown2 == 0) && (Unknown3 == 0) && (Unknown4 == 0) &&
                (PresumedPadding == 0);
        }


        [Browsable(false)]
        byte[] data;
        public byte[] GetData() { return data; }

        [Browsable(false)]
        List<HeaderLoader> _sceneHeaders;
        public List<HeaderLoader> GetSceneHeaders() { return _sceneHeaders; }

        [Browsable(false)] readonly bool inROM;
        public bool IsInROM() { return inROM; }

        [Browsable(false)]
        bool isNameExternal;
        public bool IsNameExternal() { return isNameExternal; }

        [Browsable(false)]
        HeaderLoader _currentSceneHeader;
        public HeaderLoader GetCurrentSceneHeader() { return _currentSceneHeader; }
        public void SetCurrentSceneHeader(HeaderLoader header) { _currentSceneHeader = header; }

        public Actors GetActiveTransitionData()
        {
            return _currentSceneHeader?.Commands.FirstOrDefault(x => x.Command == CommandTypeIDs.Transitions) as Actors;
        }

        public Actors GetActiveSpawnPointData()
        {
            return _currentSceneHeader?.Commands.FirstOrDefault(x => x.Command == CommandTypeIDs.Spawns) as Actors;
        }

        public SpecialObjects GetActiveSpecialObjs()
        {
            return _currentSceneHeader?.Commands.FirstOrDefault(x => x.Command == CommandTypeIDs.SpecialObjects) as SpecialObjects;
        }

        public Waypoints GetActiveWaypoints()
        {
            return _currentSceneHeader?.Commands.FirstOrDefault(x => x.Command == CommandTypeIDs.Waypoints) as Waypoints;
        }

        public Collision GetActiveCollision()
        {
            return _currentSceneHeader?.Commands.FirstOrDefault(x => x.Command == CommandTypeIDs.Collision) as Collision;
        }

        public SettingsSoundScene GetActiveSettingsSoundScene()
        {
            return _currentSceneHeader?.Commands.FirstOrDefault(x => x.Command == CommandTypeIDs.SettingsSoundScene) as SettingsSoundScene;
        }

        public EnvironmentSettings GetActiveEnvSettings()
        {
            return _currentSceneHeader?.Commands.FirstOrDefault(x => x.Command == CommandTypeIDs.EnvironmentSettings) as EnvironmentSettings;
        }


    

        BaseRomHandler _baseRom;

        public SceneTableEntryMajora(BaseRomHandler baseRom, string fn)
        {
            _baseRom = baseRom;
            inROM = false;

            Offset = -1;
            IsOffsetRelative = false;

            sceneStartAddress = sceneEndAddress = 0;

            Unknown1 = Unknown2 = Unknown3 = Unknown4 = 0;

            var fs = new System.IO.FileStream(fn, System.IO.FileMode.Open);
            data = new byte[fs.Length];
            fs.Read(data, 0, (int)fs.Length);
            fs.Close();

            Name = System.IO.Path.GetFileNameWithoutExtension(fn);
        }

        public SceneTableEntryMajora(BaseRomHandler baseRom, int offSet, bool isRelativeOffset)
        {
            _baseRom = baseRom;
            inROM = true;

            Offset = offSet;
            IsOffsetRelative = isRelativeOffset;

            sceneStartAddress = Endian.SwapUInt32(BitConverter.ToUInt32(IsOffsetRelative ? baseRom.CodeData : baseRom.Data, offSet));
            sceneEndAddress = Endian.SwapUInt32(BitConverter.ToUInt32(IsOffsetRelative ? baseRom.CodeData : baseRom.Data, offSet + 4));

            Unknown1 = (IsOffsetRelative ? baseRom.CodeData : baseRom.Data)[offSet + 8];
            Unknown2 = (IsOffsetRelative ? baseRom.CodeData : baseRom.Data)[offSet + 9];
            Unknown3 = (IsOffsetRelative ? baseRom.CodeData : baseRom.Data)[offSet + 10];
            Unknown4 = (IsOffsetRelative ? baseRom.CodeData : baseRom.Data)[offSet + 11];
            PresumedPadding = Endian.SwapUInt32(BitConverter.ToUInt32(IsOffsetRelative ? baseRom.CodeData : baseRom.Data, offSet + 12));

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
                        Name = $"S{sceneStartAddress:X}_E{sceneEndAddress:X}";
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

            (IsOffsetRelative ? _baseRom.CodeData : _baseRom.Data)[Offset + 8] = Unknown1;
            (IsOffsetRelative ? _baseRom.CodeData : _baseRom.Data)[Offset + 9] = Unknown2;
            (IsOffsetRelative ? _baseRom.CodeData : _baseRom.Data)[Offset + 10] = Unknown3;
            (IsOffsetRelative ? _baseRom.CodeData : _baseRom.Data)[Offset + 11] = Unknown4;

            tmpbuf = BitConverter.GetBytes(Endian.SwapUInt32(PresumedPadding));
            Buffer.BlockCopy(tmpbuf, 0, (IsOffsetRelative ? _baseRom.CodeData : _baseRom.Data), Offset + 12, tmpbuf.Length);
        }

        public void ReadScene(Rooms forcerooms = null)
        {
            //Program.Status.Message = string.Format("Reading scene '{0}'...", Name);

            _baseRom.SegmentMapping.Remove((byte)0x02);
            _baseRom.SegmentMapping.Add((byte)0x02, data);

            _sceneHeaders = new List<HeaderLoader>();

            HeaderLoader newheader = null;
            Rooms rooms = null;
            Collision coll = null;

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

                rooms = newheader.Commands.FirstOrDefault(x => x.Command == CommandTypeIDs.Rooms) as Rooms;
                coll = newheader.Commands.FirstOrDefault(x => x.Command == CommandTypeIDs.Collision) as Collision;
                _sceneHeaders.Add(newheader);

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

                            _sceneHeaders.Add(newheader);
                        }
                        aofs += 4;
                    }
                }

                _currentSceneHeader = _sceneHeaders[0];
            }
        }
    }
}
