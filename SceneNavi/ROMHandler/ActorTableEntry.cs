using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SceneNavi.RomHandlers;

namespace SceneNavi.ROMHandler
{
    public class ActorTableEntryBase
    {
        private BaseRomHandler _baseRom;

        public ActorTableEntryBase(BaseRomHandler baseRom, int offset, bool isRelativeOffset)
        {
            _baseRom = baseRom;
            Offset = offset;
            IsOffsetRelative = isRelativeOffset;

            StartAddress = Endian.SwapUInt32(BitConverter.ToUInt32(IsOffsetRelative ? baseRom.Rom.CodeData : baseRom.Rom.Data, offset));
            EndAddress = Endian.SwapUInt32(BitConverter.ToUInt32(IsOffsetRelative ? baseRom.Rom.CodeData : baseRom.Rom.Data, offset + 4));
            RamStartAddress = Endian.SwapUInt32(BitConverter.ToUInt32(IsOffsetRelative ? baseRom.Rom.CodeData : baseRom.Rom.Data, offset + 8));
            RamEndAddress = Endian.SwapUInt32(BitConverter.ToUInt32(IsOffsetRelative ? baseRom.Rom.CodeData : baseRom.Rom.Data, offset + 12));
            Unknown1 = Endian.SwapUInt32(BitConverter.ToUInt32(IsOffsetRelative ? baseRom.Rom.CodeData : baseRom.Rom.Data, offset + 16));
            ActorInfoRamAddress = Endian.SwapUInt32(BitConverter.ToUInt32(IsOffsetRelative ? baseRom.Rom.CodeData : baseRom.Rom.Data, offset + 20));
            ActorNameRamAddress = Endian.SwapUInt32(BitConverter.ToUInt32(IsOffsetRelative ? baseRom.Rom.CodeData : baseRom.Rom.Data, offset + 24));
            Unknown2 = Endian.SwapUInt32(BitConverter.ToUInt32(IsOffsetRelative ? baseRom.Rom.CodeData : baseRom.Rom.Data, offset + 28));

            IsValid =
                (ActorInfoRamAddress >> 24 == 0x80) &&
                (((ActorNameRamAddress >> 24 == 0x80) && (ActorNameRamAddress - RomConstants.CodeRamAddress) < baseRom.Rom.CodeData.Length) || (ActorNameRamAddress == 0));

            IsIncomplete = (StartAddress == 0 && EndAddress == 0 && RamStartAddress == 0 && RamEndAddress == 0);

            IsComplete =
                (StartAddress > baseRom.Rom.Code.VStart && StartAddress < baseRom.Rom.Size) &&
                (EndAddress > StartAddress && EndAddress > baseRom.Rom.Code.VStart && EndAddress < baseRom.Rom.Size) &&
                (RamStartAddress >> 24 == 0x80) &&
                (RamEndAddress > RamStartAddress && RamEndAddress >> 24 == 0x80) &&
                (ActorInfoRamAddress >> 24 == 0x80) &&
                (((ActorNameRamAddress >> 24 == 0x80) && (ActorNameRamAddress - RomConstants.CodeRamAddress) < baseRom.Rom.CodeData.Length) || (ActorNameRamAddress == 0));

            IsEmpty = (StartAddress == 0 && EndAddress == 0 && RamStartAddress == 0 && RamEndAddress == 0 && Unknown1 == 0 && ActorInfoRamAddress == 0 && ActorNameRamAddress == 0 && Unknown2 == 0);

            Name = Filename = "N/A";

            if (IsValid == true && IsEmpty == false)
            {
                if (ActorNameRamAddress != 0)
                {
                    var tmp = string.Empty;
                    StringExtensions.GetTerminatedString(baseRom.Rom.CodeData, (int)(ActorNameRamAddress - RomConstants.CodeRamAddress), out tmp);
                    Name = tmp;
                }
                else
                    Name = $"RAM Start 0x{RamStartAddress:X}";

                if (RamStartAddress != 0 && RamEndAddress != 0)
                {
                    var dma = baseRom.Rom.Files.Find(x => x.PStart == StartAddress);
                    if (dma != null)
                    {
                        Filename = dma.Name;

                        var tmp = new byte[dma.VEnd - dma.VStart];
                        Array.Copy(baseRom.Rom.Data, dma.PStart, tmp, 0, dma.VEnd - dma.VStart);

                        var infoAddress = (ActorInfoRamAddress - RamStartAddress);
                        if (infoAddress >= tmp.Length) return;

                        ActorNumber = Endian.SwapUInt16(BitConverter.ToUInt16(tmp, (int)infoAddress));
                        ActorType = tmp[infoAddress + 2];
                        ObjectNumber = Endian.SwapUInt16(BitConverter.ToUInt16(tmp, (int)infoAddress + 8));
                    }
                    else
                        Filename = Name;
                }
            }
        }

        public int Offset { get; private set; }
        public bool IsOffsetRelative { get; private set; }
        public uint StartAddress { get; private set; }
        public uint EndAddress { get; private set; }
        public uint RamStartAddress { get; private set; }
        public uint RamEndAddress { get; private set; }
        public uint Unknown1 { get; private set; }
        public uint ActorInfoRamAddress { get; private set; }
        public uint ActorNameRamAddress { get; private set; }
        public uint Unknown2 { get; private set; }
        public bool IsValid { get; private set; }
        public bool IsIncomplete { get; private set; }
        public bool IsComplete { get; private set; }
        public bool IsEmpty { get; private set; }
        private ushort ActorNumber { get; set; }
        private byte ActorType { get; set; }
        private ushort ObjectNumber { get; set; }
        public string Name { get; private set; }
        public string Filename { get; private set; }
    }

    public class ActorTableEntry : ActorTableEntryBase
    {
        public ActorTableEntry(BaseRomHandler baseRom, int offset, bool isRelativeOffset) : base(baseRom, offset, isRelativeOffset)
        {
        }
    }
}
