using System;

namespace SceneNavi.ROMHandler
{
    public class ByteOrderException : RomHandlerException
    {
        public ByteOrderException(string errorMessage) : base(errorMessage)
        {
        }

        public ByteOrderException(string errorMessage, Exception innerEx) : base(errorMessage, innerEx)
        {
        }
    };
}