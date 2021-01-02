using System;
using System.Collections.Generic;
using System.Linq;
using SceneNavi.ROMHandler;
using SceneNavi.ROMHandler.Interfaces;
using SceneNavi.RomHandlers;

namespace SceneNavi.HeaderCommands
{
    public partial class RoomInfoClass : IHeaderParent
    {
        BaseRomHandler _baseRom;
        public IHeaderParent Parent { get; private set; }
        public HeaderLoader CurrentRoomHeader { get; set; }
        public MeshHeader ActiveMeshHeader => CurrentRoomHeader?.Commands.FirstOrDefault(x => x.Command == CommandTypeIDs.MeshHeader) as MeshHeader;
        public Actors ActiveRoomActorData => CurrentRoomHeader?.Commands.FirstOrDefault(x => x.Command == CommandTypeIDs.Actors) as Actors;
        public Objects ActiveObjects => CurrentRoomHeader?.Commands.FirstOrDefault(x => x.Command == CommandTypeIDs.Objects) as Objects;

      

        public RoomInfoClass(BaseRomHandler baseRom, IHeaderParent parent, int num, uint start = 0, uint end = 0)
        {
            _baseRom = baseRom;
            Start = start;
            End = end;
            Parent = parent;
            Number = (ulong) num;

            if (Start == 0 || End == 0 || Start >= baseRom.Rom.Data.Length || End >= baseRom.Rom.Data.Length) return;
           
            
            var dma = _baseRom.Rom.Files.Find(x => x.PStart == Start);
            if (dma != null) DmaFilename = dma.Name;

            Data = new byte[End - Start];
            Array.Copy(_baseRom.Rom.Data, Start, Data, 0, End - Start);

            if ((Description = (_baseRom.Rom.XmlRoomNames.Names[Start] as string)) == null)
            {
                var parentTableEntry = (parent as ISceneTableEntry);
                if (parentTableEntry != null && parentTableEntry.IsNameExternal())
                {
                    Description = $"Room {(Number + 1)}";
                }
                else
                {
                    Description = dma != null ? DmaFilename : $"S{Start:X}-E{End:X}";
                }
            }

            Load();
        }

        public RoomInfoClass(BaseRomHandler baseRom, IHeaderParent parent, string fileName)
        {
            _baseRom = baseRom;
            Parent = parent;

            using (var fileStream = new System.IO.FileStream(fileName, System.IO.FileMode.Open))
            {
                Data = new byte[fileStream.Length];
                fileStream.Read(Data, 0, (int) fileStream.Length);
            }

            Description = System.IO.Path.GetFileNameWithoutExtension(fileName);

            Load();
        }

        private void Load()
        {
            _baseRom.Rom.SegmentMapping.Remove((byte) 0x03);
            _baseRom.Rom.SegmentMapping.Add((byte) 0x03, Data);

            Headers = new List<HeaderLoader>();

            if (Data[0] == (byte) CommandTypeIDs.SettingsSoundRoom || Data[0] == (byte) CommandTypeIDs.RoomBehavior ||
                BitConverter.ToUInt32(Data, 0) == (byte) CommandTypeIDs.SubHeaders)
            {
                Headers.Add(new HeaderLoader(_baseRom, this, 0x03, 0, 0));

                if (BitConverter.ToUInt32(((byte[]) _baseRom.Rom.SegmentMapping[(byte) 0x03]), 0) == 0x18)
                {
                    var hnum = 1;
                    var aofs = Endian.SwapUInt32(BitConverter.ToUInt32(((byte[]) _baseRom.Rom.SegmentMapping[(byte) 0x03]),
                        4));
                    while (true)
                    {
                        var rofs = Endian.SwapUInt32(BitConverter.ToUInt32(
                            ((byte[]) _baseRom.Rom.SegmentMapping[(byte) 0x03]), (int) (aofs & 0x00FFFFFF)));
                        if (rofs != 0)
                        {
                            if ((rofs & 0x00FFFFFF) > ((byte[]) _baseRom.Rom.SegmentMapping[(byte) 0x03]).Length ||
                                (rofs >> 24) != 0x03) break;
                            Headers.Add(new HeaderLoader(_baseRom, this, 0x03, (int) (rofs & 0x00FFFFFF), hnum++));
                        }

                        aofs += 4;
                    }
                }

                CurrentRoomHeader = Headers[0];
            }
        }
    }

    public partial class RoomInfoClass
    {
        public uint Start { get; set; }
        public uint End { get; set; }
        public List<HeaderLoader> Headers { get; set; } 
        public string DmaFilename { get; private set; }
        public string Description { get; set; }
        public byte[] Data { get; private set; }
        public ulong Number { get; private set; }
    }
}