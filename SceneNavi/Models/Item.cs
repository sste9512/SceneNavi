using System;
using System.Collections.Generic;

namespace SceneNavi.Models
{
    public class Item
    {
        public int Index { get; set; }
        public Type ValueType { get; set; }
        public DisplayStyles DisplayStyle { get; set; }
        public Usages Usage { get; set; }
        public string Description { get; set; }
        public UInt64 Mask { get; set; }
        public Type ControlType { get; set; }
        public List<Option> Options { get; set; }

        public Item()
        {
            Index = 0;
            ValueType = null;
            DisplayStyle = DisplayStyles.Decimal;
            Usage = Usages.Generic;
            Description = String.Empty;
            Mask = UInt64.MaxValue;
            ControlType = null;
            Options = new List<Option>();
        }
    }
}