using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using SceneNavi.Utilities.OpenGLHelpers;

namespace SceneNavi.Models
{
    public class Definition
    {
        public ushort Number { get; set; }
        public DefaultTypes IsDefault { get; set; }
        public DisplayList DisplayModel { get; set; }
        public DisplayList PickModel { get; set; }
        public double FrontOffset { get; set; }
        public List<Item> Items { get; set; }

        public Definition()
        {
            Number = ushort.MaxValue;
            IsDefault = DefaultTypes.None;
            DisplayModel = StockObjects.ColoredCube;
            PickModel = StockObjects.Cube;
            FrontOffset = 0.0;
            Items = null;
        }
    }
}