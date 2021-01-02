using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SceneNavi.ROMHandler;
using SceneNavi.ROMHandler.Interfaces;
using SceneNavi.RomHandlers;

namespace SceneNavi.HeaderCommands
{
    public class Generic
    {
        public CommandTypeIDs Command { get; private set; }
        protected int Offset { get; private set; }
        protected ulong Data { get; private set; }
        public IHeaderParent Parent { get; private set; }

        public string Description => (string) HeaderLoader.CommandHumanNames[Command];

        public string ByteString => $"0x{(Data >> 32):X8} 0x{(Data & 0xFFFFFFFF):X8}";

        protected BaseRomHandler BaseRom { get; private set; }
        private bool InRom { get; set; }

        protected Generic(BaseRomHandler baseRom, IHeaderParent parent, CommandTypeIDs commandTypeIds)
        {
            BaseRom = baseRom;
            InRom = false;
            Command = commandTypeIds;
            Offset = -1;
            Data = ulong.MaxValue;
            Parent = parent;
        }

        protected Generic(Generic baseCommand)
        {
            BaseRom = baseCommand.BaseRom;
            InRom = baseCommand.InRom;
            Command = baseCommand.Command;
            Offset = baseCommand.Offset;
            Data = baseCommand.Data;
            Parent = baseCommand.Parent;
        }

        public Generic(BaseRomHandler baseRom, IHeaderParent parent, byte seg, ref int ofs)
        {
            BaseRom = baseRom;
            Command = (CommandTypeIDs) ((byte[]) baseRom.Rom.SegmentMapping[seg])[ofs];
            Offset = ofs;
            Data = Endian.SwapUInt64(BitConverter.ToUInt64(((byte[]) baseRom.Rom.SegmentMapping[seg]), ofs));
            Parent = parent;
            ofs += 8;

            if ((parent as RoomInfoClass)?.Parent is SceneTableEntryOcarina)
            {
                var ste = ((parent as RoomInfoClass).Parent as ISceneTableEntry);
                InRom = ste.IsInROM();
            }
            else if (parent is ISceneTableEntry)
            {
                InRom = (parent as ISceneTableEntry).IsInROM();
            }
        }

        protected int GetCountGeneric()
        {
            return (int) ((Data >> 48) & 0xFF);
        }

        protected int GetAddressGeneric()
        {
            return (int) (Data & 0xFFFFFFFF);
        }
    }
}