using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LnkScrubber;

/// Parsed LinkInfo structure fields.
internal sealed class LinkInfoResult
{
    public string? LocalBasePath { get; set; }
    public string? CommonPathSuffix { get; set; }
    public string? NetName { get; set; }
    public uint? DriveType { get; set; }
    public uint? VolumeSerial { get; set; }
    public string? VolumeLabel { get; set; }
}

/// LNK file parser — reads raw bytes according to the MS-SHLLINK specification.
internal static class LnkParser
{
    /// Parse the LinkInfo structure from the buffer at the given offset.
    public static (int newOffset, LinkInfoResult result) ParseLinkInfo(byte[] buf, int offset)
    {
        var result = new LinkInfoResult();
        int start = offset;

        if (offset + 4 > buf.Length)
            return (offset, result);

        uint linkInfoSize = BinaryHelpers.ReadDword(buf, offset);
        if (linkInfoSize == 0 || offset + (int)linkInfoSize > buf.Length)
            return (offset, result);

        uint headerSize = BinaryHelpers.ReadDword(buf, offset + 4);
        uint flags = BinaryHelpers.ReadDword(buf, offset + 8);
        uint volumeIdOffset = BinaryHelpers.ReadDword(buf, offset + 12);
        uint localBaseOffset = BinaryHelpers.ReadDword(buf, offset + 16);
        uint cnrlOffset = BinaryHelpers.ReadDword(buf, offset + 20);
        uint commonPathOffset = BinaryHelpers.ReadDword(buf, offset + 24);

        uint? localBaseUnicodeOffset = null;
        uint? commonPathUnicodeOffset = null;

        if (headerSize >= 0x24 && offset + 32 < buf.Length)
        {
            localBaseUnicodeOffset = BinaryHelpers.ReadDword(buf, offset + 28);
            commonPathUnicodeOffset = BinaryHelpers.ReadDword(buf, offset + 32);
        }

        // Volume ID
        if ((flags & 0x1) != 0 && volumeIdOffset != 0)
        {
            int vStart = start + (int)volumeIdOffset;
            if (vStart + 16 <= buf.Length)
            {
                uint driveType = BinaryHelpers.ReadDword(buf, vStart + 4);
                uint volumeSerial = BinaryHelpers.ReadDword(buf, vStart + 8);
                uint labelOffset = BinaryHelpers.ReadDword(buf, vStart + 12);
                uint? labelUnicodeOffset = null;
                if (labelOffset >= 0x14 && vStart + 20 <= buf.Length)
                    labelUnicodeOffset = BinaryHelpers.ReadDword(buf, vStart + 16);

                result.DriveType = driveType;
                result.VolumeSerial = volumeSerial;

                string? label = null;
                if (labelUnicodeOffset != null)
                {
                    int labelStart = vStart + (int)labelUnicodeOffset.Value;
                    if (labelStart < buf.Length)
                        label = BinaryHelpers.ReadNullTerminated(buf, labelStart, Encoding.Unicode);
                }
                else if (labelOffset != 0)
                {
                    int labelStart = vStart + (int)labelOffset;
                    if (labelStart < buf.Length)
                        label = BinaryHelpers.ReadNullTerminated(buf, labelStart, BinaryHelpers.GetEncoding(1252));
                }

                result.VolumeLabel = label;
            }
        }

        // LocalBasePath
        if ((flags & 0x1) != 0 && localBaseOffset != 0)
        {
            int localStart = start + (int)localBaseOffset;
            if (localStart < buf.Length)
            {
                Encoding enc;
                int useStart;
                if (localBaseUnicodeOffset != null)
                {
                    enc = Encoding.Unicode;
                    useStart = start + (int)localBaseUnicodeOffset.Value;
                }
                else
                {
                    enc = BinaryHelpers.GetEncoding(1252);
                    useStart = localStart;
                }

                if (useStart < buf.Length)
                    result.LocalBasePath = BinaryHelpers.ReadNullTerminated(buf, useStart, enc);
            }
        }

        // CommonPathSuffix
        if (commonPathOffset != 0)
        {
            int commonStart = start + (int)commonPathOffset;
            if (commonStart < buf.Length)
            {
                Encoding enc;
                int useStart;
                if (commonPathUnicodeOffset != null)
                {
                    enc = Encoding.Unicode;
                    useStart = start + (int)commonPathUnicodeOffset.Value;
                }
                else
                {
                    enc = BinaryHelpers.GetEncoding(1252);
                    useStart = commonStart;
                }

                if (useStart < buf.Length)
                    result.CommonPathSuffix = BinaryHelpers.ReadNullTerminated(buf, useStart, enc);
            }
        }

        // Network share info (CommonNetworkRelativeLink)
        if ((flags & 0x2) != 0 && cnrlOffset != 0)
        {
            int cnrlStart = start + (int)cnrlOffset;
            if (cnrlStart + 20 <= buf.Length)
            {
                uint netNameOffset = BinaryHelpers.ReadDword(buf, cnrlStart + 8);
                uint netNameUnicodeOffset = BinaryHelpers.ReadDword(buf, cnrlStart + 16);

                if (netNameUnicodeOffset != 0)
                {
                    int netStart = cnrlStart + (int)netNameUnicodeOffset;
                    if (netStart < buf.Length)
                        result.NetName = BinaryHelpers.ReadNullTerminated(buf, netStart, Encoding.Unicode);
                }
                else if (netNameOffset != 0)
                {
                    int netStart = cnrlStart + (int)netNameOffset;
                    if (netStart < buf.Length)
                        result.NetName = BinaryHelpers.ReadNullTerminated(buf, netStart, BinaryHelpers.GetEncoding(1252));
                }
            }
        }

        return (start + (int)linkInfoSize, result);
    }

    /// Parse the StringData section following LinkInfo.
    public static (int newOffset, Dictionary<string, string> result) ParseStringData(
        byte[] buf, int offset, uint linkFlags, int codepage)
    {
        bool isUnicode = (linkFlags & 0x80) != 0;
        Encoding encoding = isUnicode ? Encoding.Unicode : BinaryHelpers.GetEncoding(codepage);

        var result = new Dictionary<string, string>();
        var stringFlags = new (string key, uint mask)[]
        {
            ("name", 0x00000004),
            ("relative_path", 0x00000008),
            ("working_dir", 0x00000010),
            ("arguments", 0x00000020),
            ("icon_location", 0x00000040),
        };

        foreach (var (key, mask) in stringFlags)
        {
            if ((linkFlags & mask) == 0)
                continue;

            if (offset + 2 > buf.Length)
                break;

            ushort count = BitConverter.ToUInt16(buf, offset);
            offset += 2;

            int byteLen = count * (isUnicode ? 2 : 1);
            if (offset + byteLen > buf.Length)
                break;

            string value = encoding.GetString(buf, offset, byteLen).TrimEnd('\0');
            offset += byteLen;
            result[key] = value;
        }

        return (offset, result);
    }

    /// Parse the ExtraData region into a list of (signature, payload) tuples.
    public static List<(uint signature, byte[] payload)> ParseExtraDataBlocks(byte[] data, int startOffset)
    {
        var blocks = new List<(uint, byte[])>();
        int cursor = startOffset;

        while (cursor + 8 <= data.Length)
        {
            uint size = BinaryHelpers.ReadDword(data, cursor);
            if (size < 4 || cursor + (int)size > data.Length)
                break;

            uint sig = BinaryHelpers.ReadDword(data, cursor + 4);
            byte[] payload = new byte[size];
            Array.Copy(data, cursor, payload, 0, (int)size);
            blocks.Add((sig, payload));

            cursor += (int)size;
            if (size == 0)
                break;
        }

        return blocks;
    }

    /// Compute the start offset of the ExtraData region.
    public static int? LocateExtraDataOffset(byte[] data, int codepage)
    {
        if (data.Length < 0x4C)
            return null;

        int cursor = 0x4C;
        uint linkFlags = BinaryHelpers.ReadDword(data, 0x14);

        if ((linkFlags & 0x1) != 0)
        {
            if (cursor + 2 > data.Length)
                return null;

            ushort idSize = BitConverter.ToUInt16(data, cursor);
            cursor += 2 + idSize;
        }

        if ((linkFlags & 0x2) != 0 && (linkFlags & (1 << 8)) == 0)
        {
            (cursor, _) = ParseLinkInfo(data, cursor);
        }

        (cursor, _) = ParseStringData(data, cursor, linkFlags, codepage);

        return cursor;
    }

    private static string? TryGet(Dictionary<string, string> dict, string key)
    {
        string? val;
        return dict.TryGetValue(key, out val) ? val : null;
    }

    /// Parse a complete .lnk file and return a dictionary of fields matching the LECmd field order.
    public static Dictionary<string, object?> ParseLnk(string path, string dateTimeFormat, int codepage)
    {
        byte[] data = File.ReadAllBytes(path);
        var fileInfo = new FileInfo(path);

        string sourceCreated = fileInfo.CreationTimeUtc.ToString(dateTimeFormat);
        string sourceModified = fileInfo.LastWriteTimeUtc.ToString(dateTimeFormat);
        string sourceAccessed = fileInfo.LastAccessTimeUtc.ToString(dateTimeFormat);

        uint linkFlags = BinaryHelpers.ReadDword(data, 0x14);
        uint fileAttributes = BinaryHelpers.ReadDword(data, 0x18);

        ulong creationTime = BinaryHelpers.ReadQword(data, 0x1C);
        ulong accessTime = BinaryHelpers.ReadQword(data, 0x24);
        ulong writeTime = BinaryHelpers.ReadQword(data, 0x2C);

        string? targetCreated = BinaryHelpers.FileTimeToString(creationTime, dateTimeFormat);
        string? targetModified = BinaryHelpers.FileTimeToString(writeTime, dateTimeFormat);
        string? targetAccessed = BinaryHelpers.FileTimeToString(accessTime, dateTimeFormat);

        uint fileSize = BinaryHelpers.ReadDword(data, 0x34);
        int iconIndex = BitConverter.ToInt32(data, 0x38);
        uint showCommand = BinaryHelpers.ReadDword(data, 0x3C);

        string? showWindowDesc;
        if (!LnkConstants.ShowWindowNames.TryGetValue(showCommand, out showWindowDesc))
            showWindowDesc = showCommand.ToString();

        int cursor = 0x4C;

        if ((linkFlags & 0x1) != 0)
        {
            if (cursor + 2 <= data.Length)
            {
                ushort idSize = BitConverter.ToUInt16(data, cursor);
                cursor += 2 + idSize;
            }
        }

        var linkInfo = new LinkInfoResult();
        if ((linkFlags & 0x2) != 0 && (linkFlags & (1 << 8)) == 0)
        {
            (cursor, linkInfo) = ParseLinkInfo(data, cursor);
        }

        Dictionary<string, string> stringData;
        (cursor, stringData) = ParseStringData(data, cursor, linkFlags, codepage);

        var trackerInfo = new TrackerInfo();
        var extraNames = new List<string>();
        int? extraOffset = LocateExtraDataOffset(data, codepage);

        if (extraOffset != null)
        {
            var blocks = ParseExtraDataBlocks(data, extraOffset.Value);
            foreach (var (sig, blk) in blocks)
            {
                string name;
                if (!LnkConstants.ExtraDataSignatures.TryGetValue(sig, out name!))
                    name = $"Unparsed_0x{sig:X}";
                extraNames.Add(name);

                if (sig == 0xA0000003)
                {
                    trackerInfo = TrackerBlock.ParseTrackerBlock(blk, dateTimeFormat);
                }
            }
        }

        string? volumeSerialStr = linkInfo.VolumeSerial != null
            ? $"0x{linkInfo.VolumeSerial:X8}"
            : null;

        string? driveTypeName = null;
        if (linkInfo.DriveType != null)
        {
            if (!LnkConstants.DriveTypeNames.TryGetValue(linkInfo.DriveType.Value, out driveTypeName))
                driveTypeName = linkInfo.DriveType.Value.ToString();
        }

        return new Dictionary<string, object?>
        {
            { "SourceFile", path },
            { "SourceCreated", sourceCreated },
            { "SourceModified", sourceModified },
            { "SourceAccessed", sourceAccessed },
            { "TargetCreated", targetCreated },
            { "TargetModified", targetModified },
            { "TargetAccessed", targetAccessed },
            { "FileSize", fileSize },
            { "IconIndex", iconIndex },
            { "IconLocation", TryGet(stringData, "icon_location") },
            { "ShowWindow", showWindowDesc },
            { "Name", TryGet(stringData, "name") },
            { "RelativePath", TryGet(stringData, "relative_path") },
            { "WorkingDirectory", TryGet(stringData, "working_dir") },
            { "FileAttributes", BinaryHelpers.DecodeFlags(fileAttributes, LnkConstants.FileAttributeNames) },
            { "HeaderFlags", BinaryHelpers.DecodeFlags(linkFlags, LnkConstants.LinkFlagNames) },
            { "DriveType", driveTypeName },
            { "VolumeSerialNumber", volumeSerialStr },
            { "VolumeLabel", linkInfo.VolumeLabel },
            { "LocalPath", linkInfo.LocalBasePath },
            { "NetworkPath", linkInfo.NetName },
            { "CommonPath", linkInfo.CommonPathSuffix },
            { "Arguments", TryGet(stringData, "arguments") },
            { "TargetIDAbsolutePath", null },
            { "TargetMFTEntryNumber", null },
            { "TargetMFTSequenceNumber", null },
            { "MachineID", trackerInfo.MachineID },
            { "MachineMACAddress", trackerInfo.MachineMACAddress },
            { "MACVendor", trackerInfo.MACVendor },
            { "TrackerCreatedOn", trackerInfo.TrackerCreatedOn },
            { "VolumeDroid", trackerInfo.VolumeDroid },
            { "VolumeDroidBirth", trackerInfo.VolumeDroidBirth },
            { "FileDroid", trackerInfo.FileDroid },
            { "FileDroidBirth", trackerInfo.FileDroidBirth },
            { "ExtraBlocksPresent", extraNames.Count > 0 ? string.Join(", ", extraNames) : null },
        };
    }
}
