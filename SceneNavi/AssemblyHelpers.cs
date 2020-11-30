using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SceneNavi
{
    class AssemblyHelpers
    {
        public static DateTime RetrieveLinkerTimestamp()
        {
            var filePath = System.Reflection.Assembly.GetCallingAssembly().Location;
            const int cPeHeaderOffset = 60;
            const int cLinkerTimestampOffset = 8;
            var b = new byte[2048];
            System.IO.Stream s = null;

            try
            {
                s = new System.IO.FileStream(filePath, System.IO.FileMode.Open, System.IO.FileAccess.Read);
                s.Read(b, 0, 2048);
            }
            finally
            {
                s?.Close();
            }

            var i = System.BitConverter.ToInt32(b, cPeHeaderOffset);
            var secondsSince1970 = System.BitConverter.ToInt32(b, i + cLinkerTimestampOffset);
            var dateTime = new DateTime(1970, 1, 1, 0, 0, 0);
            dateTime = dateTime.AddSeconds(secondsSince1970);
            dateTime = dateTime.AddHours(TimeZone.CurrentTimeZone.GetUtcOffset(dateTime).Hours);
            return dateTime;
        }
    }
}