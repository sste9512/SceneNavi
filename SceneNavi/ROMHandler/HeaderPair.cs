using System;
using System.Collections.Generic;

namespace SceneNavi.ROMHandler
{
    public class HeaderPair
    {
        public HeaderLoader SceneHeader { get; private set; }
        private List<HeaderLoader> RoomHeaders { get; set; }
        public string Description { get; set; }

        public HeaderPair(HeaderLoader headerLoader, List<HeaderLoader> headerLoaderList)
        {
            SceneHeader = headerLoader;
            RoomHeaders = headerLoaderList;
            Description = string.Empty;
        }
    }
}