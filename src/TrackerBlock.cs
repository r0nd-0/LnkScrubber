using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LnkScrubber;

/// Parsed tracker data from a TrackerDataBaseBlock.
internal sealed class TrackerInfo
{
    public string? MachineID { get; set; }
    public string? MachineMACAddress { get; set; }
    public string? MACVendor { get; set; }
    public string? TrackerCreatedOn { get; set; }
    public string? VolumeDroid { get; set; }
    public string? VolumeDroidBirth { get; set; }
    public string? FileDroid { get; set; }
    public string? FileDroidBirth { get; set; }
}

/// TrackerDataBaseBlock parsing, building, and UUID v1 helpers.
internal static class TrackerBlock
{
    private static readonly Random Rng = new Random();

    private static Dictionary<string, string>? _macVendors;

    /// Load OUI vendor prefixes from the embedded MACs.txt resource.
    /// Keys are 6-char uppercase hex (3-byte OUI), values are vendor names.
    public static Dictionary<string, string> LoadMacVendors(string? resourcePath = null)
    {
        if (_macVendors != null)
            return _macVendors;

        _macVendors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Read from embedded resource
        using (var stream = typeof(TrackerBlock).Assembly.GetManifestResourceStream("MACs.txt"))
        {
            if (stream == null)
                return _macVendors;

            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var parts = line.ToUpperInvariant().Split('\t');
                    if (parts.Length < 2)
                        continue;

                    string key = parts[0].Replace("-", "").Replace(":", "").Trim();
                    string val = parts[1].Trim();

                    if (!string.IsNullOrEmpty(key) && !_macVendors.ContainsKey(key))
                        _macVendors[key] = val;
                }
            }
        }

        return _macVendors;
    }

    /// Parse a TrackerDataBaseBlock payload into a TrackerInfo.
    /// The block includes the 4-byte size + 4-byte signature header.
    public static TrackerInfo ParseTrackerBlock(byte[] block, string dateTimeFormat)
    {
        var info = new TrackerInfo();

        if (block.Length < 0x60)
            return info;

        // MachineID is at offset 0x10..0x20 (16 bytes, null-terminated ASCII)
        var machineRaw = new byte[16];
        Array.Copy(block, 0x10, machineRaw, 0, 16);
        int nullIdx = Array.IndexOf(machineRaw, (byte)0);
        info.MachineID = nullIdx >= 0
            ? Encoding.ASCII.GetString(machineRaw, 0, nullIdx)
            : Encoding.ASCII.GetString(machineRaw);

        if (string.IsNullOrEmpty(info.MachineID))
            info.MachineID = null;

        // Four GUIDs at 0x20, 0x30, 0x40, 0x50 (16 bytes each, bytes_le format)
        Guid?[] guids = new Guid?[4];
        for (int i = 0; i < 4; i++)
        {
            int guidOffset = 0x20 + i * 16;
            if (guidOffset + 16 <= block.Length)
            {
                try
                {
                    var guidBytes = new byte[16];
                    Array.Copy(block, guidOffset, guidBytes, 0, 16);
                    guids[i] = new Guid(guidBytes);
                }
                catch
                {
                    guids[i] = null;
                }
            }
        }

        // Labels in order: VolumeDroid, FileDroid, VolumeDroidBirth, FileDroidBirth
        string?[] labels = new string?[] { "VolumeDroid", "FileDroid", "VolumeDroidBirth", "FileDroidBirth" };
        for (int i = 0; i < 4; i++)
        {
            string? value = guids[i]?.ToString();
            switch (labels[i])
            {
                case "VolumeDroid": info.VolumeDroid = value; break;
                case "FileDroid": info.FileDroid = value; break;
                case "VolumeDroidBirth": info.VolumeDroidBirth = value; break;
                case "FileDroidBirth": info.FileDroidBirth = value; break;
            }
        }

        // Find the first UUID v1 to extract MAC and creation timestamp
        Guid? firstV1 = guids.FirstOrDefault(g => g != null && GetUuidVersion(g.Value) == 1);
        if (firstV1 != null)
        {
            string? mac = MacFromUuid(firstV1.Value);
            if (mac != null)
            {
                info.MachineMACAddress = mac;
                string prefix = mac.Replace(":", "").Substring(0, 6).ToUpperInvariant();
                var vendors = LoadMacVendors();
                info.MACVendor = vendors.TryGetValue(prefix, out var vendor) ? vendor : "(Unknown vendor)";
            }

            var dt = GuidTimestampToDateTime(firstV1.Value);
            if (dt != null)
            {
                info.TrackerCreatedOn = dt.Value.ToString(dateTimeFormat);
            }
        }

        return info;
    }

    /// Get the version number from a GUID's bytes.
    private static int GetUuidVersion(Guid g)
    {
        var bytes = g.ToByteArray();
        return (bytes[7] >> 4) & 0x0F;
    }

    /// Extract the 100-nanosecond timestamp from a UUID v1 GUID.
    private static long GetUuidTimestamp(Guid g)
    {
        var bytes = g.ToByteArray();
        uint timeLow = BitConverter.ToUInt32(bytes, 0);
        ushort timeMid = BitConverter.ToUInt16(bytes, 4);
        ushort timeHiAndVersion = BitConverter.ToUInt16(bytes, 6);

        long timeHi = timeHiAndVersion & 0x0FFF;
        return ((long)timeHi << 48) | ((long)timeMid << 32) | timeLow;
    }

    /// Extract the MAC address from a UUID v1 GUID.
    public static string? MacFromUuid(Guid g)
    {
        if (GetUuidVersion(g) != 1)
            return null;

        var bytes = g.ToByteArray();
        return string.Join(":", bytes.Skip(10).Take(6).Select(b => b.ToString("x2")));
    }

    /// Convert a UUID v1 timestamp to a DateTime in UTC.
    public static DateTime? GuidTimestampToDateTime(Guid g)
    {
        if (GetUuidVersion(g) != 1)
            return null;

        try
        {
            long ns100 = GetUuidTimestamp(g);
            double posixSeconds = (ns100 - 0x01B21DD213814000L) / 1e7;
            return DateTimeOffset.FromUnixTimeMilliseconds((long)(posixSeconds * 1000)).UtcDateTime;
        }
        catch
        {
            return null;
        }
    }

    /// Build a UUID v1 GUID from a node (MAC as long), 100ns timestamp, and optional clock sequence.
    public static Guid Uuid1FromNs100(long node, long ns100, int? clockSeq = null)
    {
        int seq = clockSeq ?? Rng.Next(0, 0x4000);

        uint timeLow = (uint)(ns100 & 0xFFFFFFFF);
        ushort timeMid = (ushort)((ns100 >> 32) & 0xFFFF);
        ushort timeHiAndVersion = (ushort)(((ns100 >> 48) & 0x0FFF) | (1 << 12));

        byte clockSeqHiVariant = (byte)(((seq >> 8) & 0x3F) | 0x80);
        byte clockSeqLow = (byte)(seq & 0xFF);

        byte[] nodeBytes = new byte[6];
        for (int i = 5; i >= 0; i--)
        {
            nodeBytes[i] = (byte)(node & 0xFF);
            node >>= 8;
        }

        return new Guid(
            timeLow,
            timeMid,
            timeHiAndVersion,
            clockSeqHiVariant,
            clockSeqLow,
            nodeBytes[0],
            nodeBytes[1],
            nodeBytes[2],
            nodeBytes[3],
            nodeBytes[4],
            nodeBytes[5]
        );
    }

    /// Build a TrackerDataBaseBlock from components.
    public static byte[] BuildTrackerBlock(byte[]? template, string machineId, string mac, DateTime created)
    {
        uint size, sig, version, reserved;

        if (template != null && template.Length >= 16)
        {
            size = BinaryHelpers.ReadDword(template, 0);
            sig = BinaryHelpers.ReadDword(template, 4);
            version = BinaryHelpers.ReadDword(template, 8);
            reserved = BinaryHelpers.ReadDword(template, 12);
        }
        else
        {
            size = 0x60;
            sig = 0xA0000003;
            version = 0x58;
            reserved = 0;
        }

        byte[] machineBytes = new byte[16];
        byte[] encoded = Encoding.ASCII.GetBytes(machineId);
        int copyLen = Math.Min(encoded.Length, 16);
        Array.Copy(encoded, machineBytes, copyLen);

        long macInt = MacToLong(mac);

        if (created.Kind != DateTimeKind.Utc)
            created = created.ToUniversalTime();

        long posixTicks = new DateTimeOffset(created).ToUnixTimeMilliseconds();
        long ns100 = (long)(posixTicks / 1000.0 * 1e7) + 0x01B21DD213814000L;

        var guid1 = Uuid1FromNs100(macInt, ns100);
        var guid2 = Uuid1FromNs100(macInt, ns100 + 1);
        var guid3 = Uuid1FromNs100(macInt, ns100 + 2);
        var guid4 = Uuid1FromNs100(macInt, ns100 + 3);

        using (var ms = new MemoryStream())
        using (var bw = new BinaryWriter(ms))
        {
            bw.Write(size);
            bw.Write(sig);
            bw.Write(version);
            bw.Write(reserved);
            bw.Write(machineBytes);
            bw.Write(guid1.ToByteArray());
            bw.Write(guid2.ToByteArray());
            bw.Write(guid3.ToByteArray());
            bw.Write(guid4.ToByteArray());

            return ms.ToArray();
        }
    }

    /// Generate a random locally-administered unicast MAC address.
    public static string RandomLocalMac()
    {
        byte first = (byte)((Rng.Next(0, 256) | 0x02) & 0xFE);
        byte[] rest = new byte[5];
        Rng.NextBytes(rest);

        var parts = new byte[6];
        parts[0] = first;
        Array.Copy(rest, 0, parts, 1, 5);

        return string.Join(":", parts.Select(b => b.ToString("x2")));
    }

    /// Parse a colon-separated MAC address string to a long integer.
    public static long MacToLong(string mac)
    {
        string[] parts = mac.Replace("-", ":").Split(':');
        if (parts.Length != 6)
            throw new ArgumentException("MAC must have 6 bytes");

        long result = 0;
        foreach (var part in parts)
        {
            result = (result << 8) | Convert.ToByte(part.PadLeft(2, '0'), 16);
        }

        return result;
    }
}
