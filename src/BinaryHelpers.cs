using System;
using System.Collections.Generic;
using System.Text;

namespace LnkScrubber;

/// Low-level binary reading helpers for parsing LNK structures.
internal static class BinaryHelpers
{
    public static uint ReadDword(byte[] buf, int offset)
    {
        return BitConverter.ToUInt32(buf, offset);
    }

    public static ulong ReadQword(byte[] buf, int offset)
    {
        return BitConverter.ToUInt64(buf, offset);
    }

    /// Read a null-terminated string from a byte buffer.
    /// For UTF-16LE, scans for a double-null terminator.
    /// For single-byte encodings, scans for a single null byte.
    public static string ReadNullTerminated(byte[] buf, int offset, Encoding encoding)
    {
        if (encoding == Encoding.Unicode) // UTF-16LE
        {
            int end = offset;
            while (end + 1 < buf.Length)
            {
                if (buf[end] == 0 && buf[end + 1] == 0)
                    break;
                end += 2;
            }

            int length = end - offset;
            if (length <= 0)
                return string.Empty;

            return encoding.GetString(buf, offset, length).TrimEnd('\0');
        }
        else
        {
            int end = Array.IndexOf(buf, (byte)0, offset);
            if (end == -1)
                end = buf.Length;

            int length = end - offset;
            if (length <= 0)
                return string.Empty;

            return encoding.GetString(buf, offset, length).TrimEnd('\0');
        }
    }

    /// Decode a bitmask into comma-separated flag names.
    public static string DecodeFlags(uint value, Dictionary<uint, string> mapping)
    {
        var names = new List<string>();
        foreach (var kvp in mapping)
        {
            if ((value & kvp.Key) != 0)
                names.Add(kvp.Value);
        }

        return names.Count > 0 ? string.Join(", ", names) : string.Empty;
    }

    /// Convert a Windows FILETIME (100ns intervals since 1601-01-01) to a formatted UTC string.
    /// Returns null for zero or invalid values.
    public static string? FileTimeToString(ulong value, string format)
    {
        if (value == 0)
            return null;

        try
        {
            var dt = DateTime.FromFileTimeUtc((long)value);
            return dt.ToString(format);
        }
        catch
        {
            return null;
        }
    }

    /// Get the encoding for a given Windows code page number.
    public static Encoding GetEncoding(int codepage)
    {
        try
        {
            return Encoding.GetEncoding(codepage);
        }
        catch
        {
            return Encoding.GetEncoding(1252);
        }
    }
}
