using System.Collections.Generic;

namespace LnkScrubber;

/// <summary>
/// Constants from the MS-SHLLINK specification.
/// </summary>
internal static class LnkConstants
{
    public static readonly Dictionary<uint, string> LinkFlagNames = new Dictionary<uint, string>
    {
        { 0x00000001, "HasLinkTargetIDList" },
        { 0x00000002, "HasLinkInfo" },
        { 0x00000004, "HasName" },
        { 0x00000008, "HasRelativePath" },
        { 0x00000010, "HasWorkingDir" },
        { 0x00000020, "HasArguments" },
        { 0x00000040, "HasIconLocation" },
        { 0x00000080, "IsUnicode" },
        { 0x00000100, "ForceNoLinkInfo" },
        { 0x00000200, "HasExpString" },
        { 0x00000400, "RunInSeparateProcess" },
        { 0x00002000, "HasDarwinID" },
        { 0x00004000, "RunAsUser" },
        { 0x00008000, "HasExpIcon" },
        { 0x00010000, "NoPidlAlias" },
        { 0x00020000, "RunWithShimLayer" },
        { 0x00040000, "ForceNoLinkTrack" },
        { 0x00080000, "EnableTargetMetadata" },
        { 0x00100000, "DisableLinkPathTracking" },
        { 0x00200000, "DisableKnownFolderTracking" },
        { 0x00400000, "DisableKnownFolderAlias" },
        { 0x00800000, "AllowLinkToLink" },
        { 0x01000000, "UnaliasOnSave" },
        { 0x02000000, "PreferEnvironmentPath" },
        { 0x04000000, "KeepLocalIDListForUNCTarget" },
    };

    public static readonly Dictionary<uint, string> FileAttributeNames = new Dictionary<uint, string>
    {
        { 0x00000001, "ReadOnly" },
        { 0x00000002, "Hidden" },
        { 0x00000004, "System" },
        { 0x00000010, "Directory" },
        { 0x00000020, "Archive" },
        { 0x00000040, "Device" },
        { 0x00000080, "Normal" },
        { 0x00000100, "Temporary" },
        { 0x00000200, "SparseFile" },
        { 0x00000400, "ReparsePoint" },
        { 0x00000800, "Compressed" },
        { 0x00001000, "Offline" },
        { 0x00002000, "NotContentIndexed" },
        { 0x00004000, "Encrypted" },
        { 0x00008000, "IntegrityStream" },
        { 0x00010000, "Virtual" },
        { 0x00020000, "NoScrubData" },
    };

    public static readonly Dictionary<uint, string> DriveTypeNames = new Dictionary<uint, string>
    {
        { 0, "Unknown" },
        { 1, "No root directory" },
        { 2, "Removable" },
        { 3, "Fixed" },
        { 4, "Remote" },
        { 5, "CD-ROM" },
        { 6, "RAM disk" },
    };

    public static readonly Dictionary<uint, string> ExtraDataSignatures = new Dictionary<uint, string>
    {
        { 0xA0000003, "TrackerDataBaseBlock" },
        { 0xA0000001, "EnvironmentVariableDataBlock" },
        { 0xA0000002, "ConsoleDataBlock" },
        { 0xA0000004, "ConsoleFEDataBlock" },
        { 0xA0000005, "SpecialFolderDataBlock" },
        { 0xA0000006, "DarwinDataBlock" },
        { 0xA0000007, "IconEnvironmentDataBlock" },
        { 0xA0000008, "ShimDataBlock" },
        { 0xA0000009, "PropertyStoreDataBlock" },
        { 0xA000000B, "KnownFolderDataBlock" },
        { 0xA000000C, "VistaAndAboveIdListDataBlock" },
    };

    public static readonly Dictionary<uint, string> ShowWindowNames = new Dictionary<uint, string>
    {
        { 1, "SwShownormal" },
        { 3, "SwShowmaximized" },
        { 7, "SwShowminnoactive (Display the window as minimized without activating it.)" },
    };

    public static readonly string[] FieldOrder = new string[]
    {
        "SourceFile",
        "SourceCreated",
        "SourceModified",
        "SourceAccessed",
        "TargetCreated",
        "TargetModified",
        "TargetAccessed",
        "FileSize",
        "IconIndex",
        "IconLocation",
        "ShowWindow",
        "Name",
        "RelativePath",
        "WorkingDirectory",
        "FileAttributes",
        "HeaderFlags",
        "DriveType",
        "VolumeSerialNumber",
        "VolumeLabel",
        "LocalPath",
        "NetworkPath",
        "CommonPath",
        "Arguments",
        "TargetIDAbsolutePath",
        "TargetMFTEntryNumber",
        "TargetMFTSequenceNumber",
        "MachineID",
        "MachineMACAddress",
        "MACVendor",
        "TrackerCreatedOn",
        "VolumeDroid",
        "VolumeDroidBirth",
        "FileDroid",
        "FileDroidBirth",
        "ExtraBlocksPresent",
    };
}
