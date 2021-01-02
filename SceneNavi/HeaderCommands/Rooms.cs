using System;
using System.Collections.Generic;
using System.Text;
using SceneNavi.ROMHandler;
using SceneNavi.ROMHandler.Interfaces;
using SceneNavi.RomHandlers;

namespace SceneNavi.HeaderCommands
{
    public class Rooms : Generic
    {
        public List<RoomInfoClass> RoomInformation { get; private set; }

        public Rooms(BaseRomHandler baseRom, IHeaderParent parent, string fileName)
            : base(baseRom, parent, CommandTypeIDs.Rooms)
        {
            RoomInformation = new List<RoomInfoClass>
            {
                new RoomInfoClass(baseRom, parent, fileName)
            };
        }

        public Rooms(Generic baseCommand)
            : base(baseCommand)
        {
            RoomInformation = new List<RoomInfoClass>();

            var seg = (byte) (GetAddressGeneric() >> 24);

            for (var i = 0; i < GetCountGeneric(); i++)
            {
                var roomAddress = new RoomInfoClass(BaseRom, baseCommand.Parent, i,
                    Endian.SwapUInt32(BitConverter.ToUInt32(((byte[]) BaseRom.Rom.SegmentMapping[seg]),
                        (int) ((GetAddressGeneric() & 0xFFFFFF) + i * 8))),
                    Endian.SwapUInt32(BitConverter.ToUInt32(((byte[]) BaseRom.Rom.SegmentMapping[seg]),
                        (int) ((GetAddressGeneric() & 0xFFFFFF) + i * 8) + 4)));
                RoomInformation.Add(roomAddress);
            }
        }
    }
}