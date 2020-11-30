using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SceneNavi.HeaderCommands
{
    public class Objects : Generic, IStoreable
    {
        public List<Entry> ObjectList { get; set; }

        public Objects(Generic baseCommand)
            : base(baseCommand)
        {
            ObjectList = new List<Entry>();
            for (var i = 0; i < GetCountGeneric(); i++) ObjectList.Add(new Entry(BaseRom, (uint)(GetAddressGeneric() + i * 2)));
        }

        public void Store(byte[] dataBuffer, int baseAddress)
        {
            foreach (var obj in this.ObjectList)
            {
                var objbytes = BitConverter.GetBytes(Endian.SwapUInt16(obj.Number));
                Buffer.BlockCopy(objbytes, 0, dataBuffer, (int)(baseAddress + (obj.Address & 0xFFFFFF)), objbytes.Length);
            }
        }

        public class Entry
        {
            public uint Address { get; set; }
            public ushort Number { get; set; }
            public string Name
            {
                get
                {
                    return (Number < _baseRom.Objects.Count ? _baseRom.Objects[Number].Name : "(invalid?)");
                }

                set
                {
                    if (value == null) return;
                    var objidx = _baseRom.Objects.FindIndex(x => x.Name.ToLowerInvariant() == value.ToLowerInvariant());
                    if (objidx != -1)
                        Number = (ushort)objidx;
                    else
                        System.Media.SystemSounds.Hand.Play();
                }
            }

            ROMHandler.BaseRomHandler _baseRom;

            public Entry() { }

            public Entry(ROMHandler.BaseRomHandler baseRom, uint adr)
            {
                _baseRom = baseRom;
                Address = adr;

                var segdata = (byte[])_baseRom.SegmentMapping[(byte)(adr >> 24)];
                if (segdata == null) return;

                Number = Endian.SwapUInt16(BitConverter.ToUInt16(segdata, (int)(adr & 0xFFFFFF)));
            }
        }
    }
}
