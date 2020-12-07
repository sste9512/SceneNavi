using System;

namespace SceneNavi.Models
{
    public class Option
    {
        public UInt64 Value { get; set; }
        public string Description { get; set; }

        public Option()
        {
            Value = 0;
            Description = string.Empty;
        }
    }
}