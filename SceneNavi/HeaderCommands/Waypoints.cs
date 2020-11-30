using System;
using System.Collections.Generic;


namespace SceneNavi.HeaderCommands
{
    public class Waypoints : Generic, IStoreable
    {
        public List<PathHeader> Paths { get; set; }

        public Waypoints(Generic baseCommand)
            : base(baseCommand)
        {
            Paths = new List<PathHeader>();

            var i = 0;
            while (true)
            {
                var nph = new PathHeader(BaseRom, (uint)(GetAddressGeneric() + i * 8), i);
                if (nph.Points == null) break;
                Paths.Add(nph);
                i++;
            }
        }

        public void Store(byte[] dataBuffer, int baseAddress)
        {
            foreach (var path in Paths)
            {
                foreach (var wp in path.Points)
                {
                    var bytes = BitConverter.GetBytes(Endian.SwapInt16((short)wp.X));
                    Buffer.BlockCopy(bytes, 0, dataBuffer, (int)(baseAddress + (wp.Address & 0xFFFFFF)), bytes.Length);
                    bytes = BitConverter.GetBytes(Endian.SwapInt16((short)wp.Y));
                    Buffer.BlockCopy(bytes, 0, dataBuffer, (int)(baseAddress + (wp.Address & 0xFFFFFF) + 2), bytes.Length);
                    bytes = BitConverter.GetBytes(Endian.SwapInt16((short)wp.Z));
                    Buffer.BlockCopy(bytes, 0, dataBuffer, (int)(baseAddress + (wp.Address & 0xFFFFFF) + 4), bytes.Length);
                }
            }
        }
    }
}
