using System;
using System.Text;

internal static class StringExtensions
{
    public static int GetTerminatedString(byte[] bytes, int index, out string str)
    {
        var nullidx = Array.FindIndex(bytes, index, (x) => x == 0);

        if (nullidx >= 0) str = Encoding.ASCII.GetString(bytes, index, nullidx - index);
        else
            str = Encoding.ASCII.GetString(bytes, index, bytes.Length - index);

        var nextidx = Array.FindIndex(bytes, nullidx, (x) => x != 0);

        return nextidx;
    }
}