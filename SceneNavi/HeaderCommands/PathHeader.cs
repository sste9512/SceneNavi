using System;
using System.Collections.Generic;
using SceneNavi.ROMHandler;

namespace SceneNavi.HeaderCommands
{
    public class PathHeader
    {
        readonly int _pathNumber;
        private uint Address { get; set; }
        private uint WayPointCount { get; set; }
        private uint WayPointAddress { get; set; }
        public List<Waypoint> Points { get; private set; }
        public string Description => _baseRom == null ? "(None)" : $"Path #{(_pathNumber + 1)}: {WayPointCount} waypoints";

        readonly BaseRomHandler _baseRom;

        public PathHeader() { }

        public PathHeader(BaseRomHandler baseRom, uint address, int number)
        {
            Address = address;
            _baseRom = baseRom;
            _pathNumber = number;

            var segmentedData = (byte[])_baseRom.SegmentMapping[(byte)(address >> 24)];
            if (segmentedData == null) return;

            WayPointCount = BitConverter.ToUInt32(segmentedData, (int)(address & 0xFFFFFF));
            WayPointAddress = Endian.SwapUInt32(BitConverter.ToUInt32(segmentedData, (int)(address & 0xFFFFFF) + 4));

            var psegdata = (byte[])_baseRom.SegmentMapping[(byte)(WayPointAddress >> 24)];
            if (WayPointCount == 0 || WayPointCount > 0xFF || psegdata == null || (WayPointAddress & 0xFFFFFF) >= psegdata.Length) return;

            Points = new List<Waypoint>();
            for (var i = 0; i < WayPointCount; i++)
            {
                Points.Add(new Waypoint(_baseRom, (uint)(WayPointAddress + (i * 6))));
            }
        }
    }
}