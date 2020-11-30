using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SceneNavi.ROMHandler
{
    public class ObjectTableEntry
    {
        public int Offset { get; private set; }
        public bool IsOffsetRelative { get; private set; }

        public uint StartAddress { get; private set; }
        public uint EndAddress { get; private set; }

        public bool IsValid { get; private set; }
        public bool IsEmpty { get; private set; }

        public string Name { get; private set; }
        public DmaTableEntry DMA { get; private set; }

        BaseRomHandler _baseRom;

        public ObjectTableEntry(BaseRomHandler baseRom, int ofs, bool isrel, ushort number = 0)
        {
            _baseRom = baseRom;
            Offset = ofs;
            IsOffsetRelative = isrel;

            StartAddress = Endian.SwapUInt32(BitConverter.ToUInt32(IsOffsetRelative ? baseRom.CodeData : baseRom.Data, ofs));
            EndAddress = Endian.SwapUInt32(BitConverter.ToUInt32(IsOffsetRelative ? baseRom.CodeData : baseRom.Data, ofs + 4));

            IsValid =
                ((StartAddress > baseRom.Code.VStart) &&
                (StartAddress < baseRom.Size) && (EndAddress < baseRom.Size) &&
                ((StartAddress & 0xF) == 0) && ((EndAddress & 0xF) == 0) &&
                (EndAddress > StartAddress));

            IsEmpty = (StartAddress == 0 && EndAddress == 0);

            Name = "N/A";

            if (IsValid == true && IsEmpty == false)
            {
                if ((Name = (_baseRom.XmlObjectNames.Names[number] as string)) == null)
                {
                    DMA = baseRom.Files.Find(x => x.PStart == StartAddress);
                    if (DMA != null)
                        Name = DMA.Name;
                    else
                        Name = $"S{StartAddress:X}_E{EndAddress:X}";
                }
            }
        }
    }
}
