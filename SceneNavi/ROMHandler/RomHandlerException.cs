using System;

namespace SceneNavi.ROMHandler
{
    public class RomHandlerException : Exception
    {
        public RomHandlerException(string errorMessage) : base(errorMessage) { }
        public RomHandlerException(string errorMessage, Exception innerEx) : base(errorMessage, innerEx) { }
    };
}