using System;
using System.ComponentModel;

namespace SceneNavi.ROMHandler
{
    [TypeConverter(typeof(StageKeyConverter))]
    public class StageKey
    {
        public uint SceneAddress { get; set; }
        public int HeaderNumber { get; set; }
        public string Format => "0x" + SceneAddress.ToString("X8") + ", " + HeaderNumber;

        public StageKey(string format)
        {
            var parts = format.Split(',');
            if (parts.Length != 2)
            {
                throw new Exception("Invalid format");
            }

            SceneAddress = uint.Parse(parts[0].Substring(2), System.Globalization.NumberStyles.HexNumber);
            HeaderNumber = int.Parse(parts[1]);
        }

        public StageKey(uint sceneAddress, int headerNumber)
        {
            SceneAddress = sceneAddress;
            HeaderNumber = headerNumber;
        }
    }
}