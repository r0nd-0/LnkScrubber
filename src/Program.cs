using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LnkScrubber;

internal static class Program
{
    private static void PrintUsage()
    {
        Console.WriteLine(@"LnkScrubber - Parse and sanitize Windows .lnk shortcut files

Usage:
  LnkScrubber -f <file> [options]

Required:
  -f, --file <path>        Path to the .lnk file to parse

Sanitization options (mutually exclusive):
  --strip-tracker          Remove the TrackerDataBaseBlock entirely
  --anonymize              Replace tracker with randomized MachineID/MAC/droids
  --manualedit             Interactively edit tracker fields

Other:
  --out-lnk <path>         Output path for the modified LNK (default: <stem>_sanitized.lnk)
  --dt <format>            DateTime format string (default: yyyy-MM-dd HH:mm:ss)
  -h, --help               Show this help message");
    }

    private static int Main(string[] args)
    {
        // Parse arguments
        string? filePath = null;
        string dateTimeFormat = "yyyy-MM-dd HH:mm:ss";
        int codepage = 1252;
        string? outLnk = null;
        bool stripTracker = false;
        bool anonymize = false;
        bool manualEdit = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-f":
                case "--file":
                    filePath = GetNextArg(args, ref i, args[i]);
                    break;
                case "--dt":
                    dateTimeFormat = GetNextArg(args, ref i, args[i]);
                    break;
                case "--out-lnk":
                    outLnk = GetNextArg(args, ref i, args[i]);
                    break;
                case "--strip-tracker":
                    stripTracker = true;
                    break;
                case "--anonymize":
                    anonymize = true;
                    break;
                case "--manualedit":
                    manualEdit = true;
                    break;
                case "-h":
                case "--help":
                    PrintUsage();
                    return 0;
                default:
                    Console.Error.WriteLine($"Error: Unknown argument: {args[i]}");
                    PrintUsage();
                    return 1;
            }
        }

        if (string.IsNullOrEmpty(filePath))
        {
            Console.Error.WriteLine("Error: -f/--file is required.");
            PrintUsage();
            return 1;
        }

        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"Error: File not found: {filePath}");
            return 1;
        }

        // Validate mutually exclusive sanitization flags
        int sanitizeCount = (stripTracker ? 1 : 0) + (anonymize ? 1 : 0) + (manualEdit ? 1 : 0);
        if (sanitizeCount > 1)
        {
            Console.Error.WriteLine("Error: --strip-tracker, --anonymize, and --manualedit are mutually exclusive.");
            return 1;
        }

        string targetPath = filePath;

        // Sanitize if requested
        if (stripTracker || anonymize || manualEdit)
        {
            string mode = stripTracker ? "strip" : anonymize ? "anonymize" : "manual";

            string newPath;
            if (!string.IsNullOrEmpty(outLnk))
            {
                newPath = outLnk;
            }
            else
            {
                string dir = Path.GetDirectoryName(filePath) ?? ".";
                string stem = Path.GetFileNameWithoutExtension(filePath);
                string ext = Path.GetExtension(filePath);
                newPath = Path.Combine(dir, $"{stem}_sanitized{ext}");
            }

            byte[] originalData = File.ReadAllBytes(filePath);
            byte[] newData = LnkSanitizer.SanitizeLnk(originalData, dateTimeFormat, codepage, mode);

            // Ensure output directory exists
            string? outputDir = Path.GetDirectoryName(newPath);
            if (!string.IsNullOrEmpty(outputDir))
                Directory.CreateDirectory(outputDir);

            File.WriteAllBytes(newPath, newData);
            targetPath = newPath;

            Console.Error.WriteLine($"Sanitized LNK written to: {newPath}");
        }

        // Parse the (possibly modified) LNK
        Dictionary<string, object?> record;
        try
        {
            record = LnkParser.ParseLnk(targetPath, dateTimeFormat, codepage);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error parsing LNK: {ex.Message}");
            return 1;
        }

        // Print JSON to stdout
        string json = SerializeJson(record);
        Console.WriteLine(json);

        return 0;
    }

    private static string GetNextArg(string[] args, ref int index, string flag)
    {
        index++;
        if (index >= args.Length)
        {
            Console.Error.WriteLine($"Error: {flag} requires a value.");
            Environment.Exit(1);
        }
        return args[index];
    }

    private static string SerializeJson(Dictionary<string, object?> record)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        bool first = true;
        foreach (string key in LnkConstants.FieldOrder)
        {
            if (!first)
                sb.AppendLine(",");
            first = false;

            object? val;
            record.TryGetValue(key, out val);

            sb.Append($"    \"{EscapeJson(key)}\": ");
            if (val == null)
                sb.Append("null");
            else if (val is int || val is uint || val is long || val is ulong)
                sb.Append(val);
            else
                sb.Append($"\"{EscapeJson(val.ToString() ?? "")}\"");
        }
        sb.AppendLine();
        sb.Append("}");
        return sb.ToString();
    }

    private static string EscapeJson(string s)
    {
        return s
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}
