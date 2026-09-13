using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;

namespace ETCS.Pos.Bridge.Services;

internal static class NfcUidEncoder
{
    public static string ToHex(byte[] uid)
    {
        if (uid.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(uid.Length * 2);
        foreach (var value in uid)
        {
            builder.Append(value.ToString("X2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    public static byte[] Reverse(byte[] uid)
    {
        var reversed = (byte[])uid.Clone();
        Array.Reverse(reversed);
        return reversed;
    }

    public static string ToDecimal(byte[] uid)
    {
        if (uid.Length == 0)
        {
            return string.Empty;
        }

        var value = uid.Aggregate(BigInteger.Zero, (current, b) => (current << 8) + b);
        return value.ToString(CultureInfo.InvariantCulture);
    }
}
