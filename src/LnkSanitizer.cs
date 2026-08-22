using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LnkScrubber;

/// <summary>
/// LNK TrackerDataBaseBlock sanitization — strip, anonymize, or manually edit tracker metadata.
/// </summary>
internal static class LnkSanitizer
{
    private static readonly Random Rng = new Random();
    private const string AlphaNumChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    /// <summary>
    /// Sanitize an LNK file's TrackerDataBaseBlock.
    /// </summary>
    public static byte[] SanitizeLnk(byte[] data, string dateTimeFormat, int codepage, string mode)
    {
        int? extraOffset = LnkParser.LocateExtraDataOffset(data, codepage);
        if (extraOffset == null)
            return data;

        var blocks = LnkParser.ParseExtraDataBlocks(data, extraOffset.Value);
        var newBlocks = new List<byte[]>();
        byte[]? trackerTemplate = null;
        TrackerInfo? existingTrackerInfo = null;

        foreach (var (sig, blk) in blocks)
        {
            if (sig == 0xA0000003)
            {
                trackerTemplate = blk;
                existingTrackerInfo = TrackerBlock.ParseTrackerBlock(blk, dateTimeFormat);
                continue;
            }

            newBlocks.Add(blk);
        }

        if (mode == "anonymize" || mode == "manual")
        {
            string machineId;
            string mac;
            DateTime created;

            if (mode == "anonymize")
            {
                var suffix = new string(Enumerable.Range(0, 7)
                    .Select(_ => AlphaNumChars[Rng.Next(AlphaNumChars.Length)])
                    .ToArray());
                machineId = $"DESKTOP-{suffix}";

                mac = TrackerBlock.RandomLocalMac();

                var now = DateTime.UtcNow;
                int randomDays = Rng.Next(-365, 1);
                int randomSeconds = Rng.Next(0, 86400);
                created = now.AddDays(randomDays).AddSeconds(randomSeconds);
            }
            else
            {
                string currentMachine = existingTrackerInfo?.MachineID ?? "";
                string exampleMachine = string.IsNullOrEmpty(currentMachine) ? "DESKTOP-ABC1234" : currentMachine;
                Console.Write($"MachineID [{exampleMachine}]: ");
                string? machineInput = Console.ReadLine()?.Trim();
                machineId = string.IsNullOrEmpty(machineInput)
                    ? (string.IsNullOrEmpty(currentMachine) ? exampleMachine : currentMachine)
                    : machineInput;

                string currentMac = existingTrackerInfo?.MachineMACAddress ?? TrackerBlock.RandomLocalMac();
                Console.Write($"MAC [{currentMac}]: ");
                string? macInput = Console.ReadLine()?.Trim();
                mac = string.IsNullOrEmpty(macInput) ? currentMac : macInput;

                string? currentCreated = existingTrackerInfo?.TrackerCreatedOn;
                string defaultDisplay = currentCreated ?? "now";
                Console.Write($"Creation datetime (UTC, e.g. 2024-01-01 12:00:00) [{defaultDisplay}]: ");
                string? createdInput = Console.ReadLine()?.Trim();
                if (!string.IsNullOrEmpty(createdInput))
                {
                    DateTime parsed;
                    if (DateTime.TryParse(createdInput, out parsed))
                    {
                        created = parsed.Kind == DateTimeKind.Utc
                            ? parsed
                            : DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
                    }
                    else
                    {
                        created = DateTime.UtcNow;
                    }
                }
                else
                {
                    created = DateTime.UtcNow;
                }
            }

            byte[] newTracker = TrackerBlock.BuildTrackerBlock(trackerTemplate, machineId, mac, created);
            newBlocks.Add(newTracker);
        }

        // Reconstruct the extra data region with a terminal block (4 zero bytes)
        using (var ms = new MemoryStream())
        {
            foreach (var block in newBlocks)
            {
                ms.Write(block, 0, block.Length);
            }

            ms.Write(new byte[4], 0, 4);

            byte[] newExtra = ms.ToArray();

            byte[] result = new byte[extraOffset.Value + newExtra.Length];
            Array.Copy(data, 0, result, 0, extraOffset.Value);
            Array.Copy(newExtra, 0, result, extraOffset.Value, newExtra.Length);

            return result;
        }
    }
}
