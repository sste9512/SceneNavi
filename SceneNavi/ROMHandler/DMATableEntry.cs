using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SceneNavi.RomHandlers;

namespace SceneNavi.ROMHandler
{
    public class DmaTableEntry
    {
        public enum FileTypes { Undefined, General, Empty, Scene, Room, Overlay, Object, Invalid };

        const int HeaderScanThreshold = 0x200;

        public uint VStart { get; private set; }
        public uint VEnd { get; private set; }
        public uint PStart { get; private set; }
        public uint PEnd { get; private set; }
        public string Name { get; set; }
        public bool IsValid { get; private set; }
        public bool IsCompressed { get; private set; }

        public FileTypes FileType { get; private set; }
        public byte AssumedSegment { get; private set; }

        public DmaTableEntry(BaseRomHandler baseRom, int idx)
        {
            var readOffset = (baseRom.DmaTableAddress + (idx * 0x10));

            VStart = Endian.SwapUInt32(BitConverter.ToUInt32(baseRom.Data, readOffset));
            VEnd = Endian.SwapUInt32(BitConverter.ToUInt32(baseRom.Data, readOffset + 4));
            PStart = Endian.SwapUInt32(BitConverter.ToUInt32(baseRom.Data, readOffset + 8));
            PEnd = Endian.SwapUInt32(BitConverter.ToUInt32(baseRom.Data, readOffset + 12));

            if (PStart == 0xFFFFFFFF || PEnd == 0xFFFFFFFF)
                IsValid = false;
            else
            {
                IsValid = true;
                if (PEnd != 0 && Encoding.ASCII.GetString(baseRom.Data, (int)PStart, 4) == "Yaz0") IsCompressed = true;
                else IsCompressed = false;
            }

            Name = $"File #{idx}";

            FileType = FileTypes.Undefined;
            AssumedSegment = 0x00;
        }

        public void Identify(BaseRomHandler baseRom)
        {
            var fileNameAssumed = FileTypes.General;

            if (baseRom.FileNameTableAddress != -1)
            {
                if (Name.EndsWith("_scene") == true) fileNameAssumed = FileTypes.Scene;
                else if (Name.Contains("_room_") == true) fileNameAssumed = FileTypes.Room;
                else if (Name.StartsWith("ovl_") == true) fileNameAssumed = FileTypes.Overlay;
                else if (Name.StartsWith("object_") == true) fileNameAssumed = FileTypes.Object;
            }

            /* Invalid file? */
            if (!IsValid || VEnd - VStart == 0)
            {
                FileType = FileTypes.Invalid;
                return;
            }

            if (!IsCompressed)
            {
                var data = new byte[VEnd - VStart];
                Buffer.BlockCopy(baseRom.Data, (int)PStart, data, 0, data.Length);

                /* Room file? */
                if (BitConverter.ToUInt32(data, (int)0) == 0x16 || ((BitConverter.ToUInt32(data, (int)0) == 0x18) && data[4] == 0x03 && BitConverter.ToUInt32(data, (int)8) == 0x16))
                {
                    for (var i = 8; i < HeaderScanThreshold; i += 8)
                        if (BitConverter.ToUInt32(data, i) == 0x14 && (fileNameAssumed == FileTypes.General || fileNameAssumed == FileTypes.Room))
                        {
                            AssumedSegment = 0x03;
                            FileType = FileTypes.Room;
                            return;
                        }
                }

                /* Scene file? */
                if ((BitConverter.ToUInt32(data, (int)0) & 0xFFFF00FF) == 0x15 || ((BitConverter.ToUInt32(data, (int)0) == 0x18) && data[4] == 0x02 && (BitConverter.ToUInt32(data, (int)8) & 0xFFFF00FF) == 0x15))
                {
                    for (var i = 8; i < HeaderScanThreshold; i += 8)
                        if (BitConverter.ToUInt32(data, i) == 0x14 && (fileNameAssumed == FileTypes.General || fileNameAssumed == FileTypes.Scene))
                        {
                            AssumedSegment = 0x02;
                            FileType = FileTypes.Scene;
                            return;
                        }
                }

                /* Overlay file? */
                var ovlheader = ((uint)data.Length - Endian.SwapUInt32(BitConverter.ToUInt32(data, (data.Length - 4))));
                if ((ovlheader + 16) < data.Length)
                {
                    var btext = Endian.SwapUInt32(BitConverter.ToUInt32(data, (int)ovlheader));
                    var bdata = Endian.SwapUInt32(BitConverter.ToUInt32(data, (int)ovlheader + 4));
                    var brodata = Endian.SwapUInt32(BitConverter.ToUInt32(data, (int)ovlheader + 8));
                    var bssdata = Endian.SwapUInt32(BitConverter.ToUInt32(data, (int)ovlheader + 12));

                    if ((btext + bdata + brodata == data.Length || btext + bdata + brodata == ovlheader) && (fileNameAssumed == FileTypes.General || fileNameAssumed == FileTypes.Overlay))
                    {
                        FileType = FileTypes.Overlay;
                        return;
                    }
                }

                /* Object file? */
                bool indl, hassync, hasvtx, hasdlend;
                var segmentCount = new int[16];
                indl = hassync = hasvtx = hasdlend = false;
                for (var i = 0; i < data.Length; i += 8)
                {
                    if (BitConverter.ToUInt32(data, i) == 0xE7 && BitConverter.ToUInt32(data, i + 4) == 0x0)
                    {
                        hassync = true;
                        indl = true;
                    }
                    else if (indl && data[i] == 0x01 && data[i + 4] <= 0x0F)
                    {
                        hasvtx = true;
                        segmentCount[data[i + 4]]++;
                    }
                    else if (BitConverter.ToUInt32(data, i) == 0xDF && BitConverter.ToUInt32(data, i + 4) == 0x0)
                    {
                        hasdlend = true;
                        indl = false;
                    }
                }
                if (hassync && hasvtx && hasdlend && (fileNameAssumed == FileTypes.General || fileNameAssumed == FileTypes.Object))
                {
                    AssumedSegment = (byte)segmentCount.ToList().IndexOf(segmentCount.Max());
                    FileType = FileTypes.Object;
                    return;
                }

                /* Empty file? */
                if (data.Length < 0x100)
                {
                    var isempty = data.Count(x => x != 0);
                    if (isempty == 0)
                    {
                        FileType = FileTypes.Empty;
                        return;
                    }
                }
            }

            /* Use assumption */
            FileType = fileNameAssumed;
        }
    }
}
