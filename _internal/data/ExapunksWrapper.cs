using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

internal sealed class PatchEntry
{
    public string Label;
    public byte[] OldBytes;
    public byte[] NewBytes;
    public int DesiredHits;
    public bool Bootstrap;
    public string Group;
    public int GroupPresenceRequired;
}

internal static class Program
{
    private const uint ProcessQueryInformation = 0x0400;
    private const uint ProcessVmRead = 0x0010;
    private const uint ProcessVmWrite = 0x0020;
    private const uint ProcessVmOperation = 0x0008;
    private const uint MemCommit = 0x1000;
    private const uint MemPrivate = 0x20000;
    private const uint MemMapped = 0x40000;
    private const uint MemImage = 0x1000000;
    private const uint PageNoAccess = 0x01;
    private const uint PageReadOnly = 0x02;
    private const uint PageGuard = 0x100;
    private const uint PageReadWrite = 0x04;
    private const uint PageWriteCopy = 0x08;
    private const uint PageExecuteRead = 0x20;
    private const uint PageExecuteReadWrite = 0x40;
    private const uint PageExecuteWriteCopy = 0x80;
    private const uint ProtectAccessMask = 0xFF;
    private const int ScanMinBytes = 4 * 1024;
    private const int ScanMaxBytes = 16 * 1024 * 1024;
    private const int BootstrapRegionMaxBytes = 16 * 1024 * 1024;
    private const int BootstrapChunkBytes = 1024 * 1024;
    private const int FullChunkBytes = 1024 * 1024;
    private const int FullDeepScanInterval = 12;

    private static readonly HashSet<string> BootstrapLabels = new HashSet<string>(StringComparer.Ordinal)
    {
        "LOADING",
        "Loading",
        "AXIOM PERSONAL ORGANIZER",
        "MEETING WITH NIVAS",
        "MEETING WITH GHAST",
        "WORKHOUSE",
        "INSTALLED PROGRAM",
        "LAUNCH WORKHOUSE",
        "Perform simple tasks to earn money at home.",
        "Find a source for bootleg medication.",
        "SAT 10/04/97 5:21 PM",
        "SAT 10/04/97 5:35 PM",
        "TRASH WORLD NEWS",
        "TUTORIAL 1",
        "Learn to explore networks and leave no trace.",
        "CONNECT TO NETWORK",
        "Solve this puzzle to view histograms.",
        "PLAY CUTSCENE",
        "Reconnect with an old friend.",
        "605 Eddy St. #801, San Francisco, CA",
        "OPTIONS",
        "CONTROLS",
        "EXIT GAME",
        "BACK",
        "DISPLAY",
        "SOUND",
        "INTERFACE",
        "DISPLAY MODE",
        "FULLSCREEN",
        "WINDOWED",
        "WINDOW SIZE",
        "DISPLAY QUALITY",
        "HIGH (4K)",
        "LOW (2K)",
        "OTHER CONTROLS",
        "RETURN",
        "START",
        "RESET / PAUSE / STEP / RUN / FAST",
        "RUN TO INSTRUCTION (ANY EXA / THIS EXA)",
        "SHOW GOAL",
        "PREVIOUS / NEXT EXA WINDOW",
        "CREATE NEW EXA",
        "CUT / COPY / PASTE",
        "UNDO / REDO",
        "HOSTNAME",
        "PROFANITY",
        "SHOW",
        "HIDE",
        "MOUSE CURSOR",
        "HARDWARE",
        "SOFTWARE",
        "CODE FONT SIZE",
        "NORMAL",
        "LARGER",
        "HACK*MATCH CRT EFFECT",
        "NO DISTORTION",
        "REPLACE BACKUP BATTERY",
        "REFERENCE MATERIALS",
        "ISSUE #1",
        "ISSUE #2",
        "EPILOGUE",
        "DIGITAL VERSION (PDF)",
        "PRINTABLE VERSION (PDF)",
        "DIGITAL VERSION",
        "PRINTABLE VERSION",
        "LETTER-SIZE PAPER",
        "A4-SIZE PAPER",
        "Task #",
        "Receipt",
        "Description",
        "Price",
        "Account Balance",
        "Transcribe the items from this receipt",
        "CYCLES",
        "SIZE",
        "ACTIVITY",
        "INTRO",
        "OUTRO",
        "Emulated",
        "Internal",
        "Emotional",
        "Reasoning"
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORY_BASIC_INFORMATION64
    {
        public ulong BaseAddress;
        public ulong AllocationBase;
        public uint AllocationProtect;
        public uint Alignment1;
        public ulong RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
        public uint Alignment2;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(
        IntPtr processHandle,
        IntPtr baseAddress,
        [Out] byte[] buffer,
        int size,
        out IntPtr bytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteProcessMemory(
        IntPtr processHandle,
        IntPtr baseAddress,
        byte[] buffer,
        int size,
        out IntPtr bytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr VirtualQueryEx(
        IntPtr processHandle,
        IntPtr address,
        out MEMORY_BASIC_INFORMATION64 buffer,
        UIntPtr length);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualProtectEx(
        IntPtr processHandle,
        IntPtr address,
        UIntPtr size,
        uint newProtect,
        out uint oldProtect);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    private static int Main()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string originalExe = Path.Combine(baseDir, "EXAPUNKS.original.exe");
        string patchFile = Path.Combine(baseDir, "EXAPUNKS.runtime.tsv");
        string logFile = Path.Combine(baseDir, "EXAPUNKS.runtime.log");

        try
        {
            Log(logFile, "Wrapper start.");

            if (!File.Exists(originalExe))
            {
                Log(logFile, "Missing original exe: " + originalExe);
                return 1;
            }

            List<PatchEntry> patches = LoadPatches(patchFile);
            Dictionary<string, int> cumulativeCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            Process process = Process.Start(new ProcessStartInfo
            {
                FileName = originalExe,
                WorkingDirectory = baseDir,
                UseShellExecute = false,
            });

            if (process == null)
            {
                Log(logFile, "Failed to start original exe.");
                return 1;
            }

            DateTime start = DateTime.UtcNow;
            int totalPatched = 0;
            int bootstrapRoundIndex = 0;
            DateTime bootstrapDeadline = start.AddSeconds(10);

            while (!process.HasExited &&
                   DateTime.UtcNow < bootstrapDeadline)
            {
                Dictionary<string, int> bootstrapDetails;
                int patchedNow = PatchProcess(
                    process.Id,
                    patches,
                    cumulativeCounts,
                    true,
                    false,
                    out bootstrapDetails);
                totalPatched += patchedNow;
                bootstrapRoundIndex++;
                Log(logFile, "Bootstrap patched: " + patchedNow + ", round: " + bootstrapRoundIndex);
                foreach (KeyValuePair<string, int> pair in bootstrapDetails)
                {
                    Log(logFile, "  " + pair.Key + " => " + pair.Value);
                }

                Thread.Sleep(patchedNow > 0 ? 50 : 100);
            }

            int patchRoundIndex = 0;

            while (!process.HasExited)
            {
                bool deepScan = ((patchRoundIndex + 1) % FullDeepScanInterval) == 0;
                Dictionary<string, int> roundDetails;
                int patchedNow = PatchProcess(
                    process.Id,
                    patches,
                    cumulativeCounts,
                    false,
                    deepScan,
                    out roundDetails);
                totalPatched += patchedNow;
                patchRoundIndex++;
                Log(
                    logFile,
                    "Patched: " + patchedNow +
                    ", round: " + patchRoundIndex +
                    ", mode: " + (deepScan ? "deep" : "fast"));
                foreach (KeyValuePair<string, int> pair in roundDetails)
                {
                    Log(logFile, "  " + pair.Key + " => " + pair.Value);
                }

                Thread.Sleep(patchedNow > 0 ? 120 : 220);
            }

            Log(logFile, "Wrapper exit. Total patched: " + totalPatched);
            return process.ExitCode;
        }
        catch (Exception ex)
        {
            Log(logFile, "Wrapper error: " + ex);
            return 1;
        }
    }

    private static List<PatchEntry> LoadPatches(string patchFile)
    {
        List<PatchEntry> patches = new List<PatchEntry>();
        if (!File.Exists(patchFile))
        {
            return patches;
        }

        foreach (string rawLine in File.ReadAllLines(patchFile, Encoding.UTF8))
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            int tabIndex = rawLine.IndexOf('\t');
            if (tabIndex <= 0)
            {
                continue;
            }

            string oldText = rawLine.Substring(0, tabIndex);
            string newText = rawLine.Substring(tabIndex + 1);
            if (newText.Length > oldText.Length)
            {
                continue;
            }

            if (newText.Length < oldText.Length)
            {
                newText = newText + new string(' ', oldText.Length - newText.Length);
            }

            if (string.Equals(oldText, newText, StringComparison.Ordinal))
            {
                continue;
            }

            patches.Add(new PatchEntry
            {
                Label = oldText,
                OldBytes = Encoding.Unicode.GetBytes(oldText),
                NewBytes = Encoding.Unicode.GetBytes(newText),
                DesiredHits = GetDesiredHits(oldText),
                Bootstrap = BootstrapLabels.Contains(oldText),
                Group = GetPatchGroup(oldText),
                GroupPresenceRequired = GetGroupPresenceRequired(oldText),
            });
        }

        patches.Sort((left, right) => right.OldBytes.Length.CompareTo(left.OldBytes.Length));
        return patches;
    }

    private static int PatchProcess(
        int processId,
        List<PatchEntry> patches,
        Dictionary<string, int> cumulativeCounts,
        bool bootstrapOnly,
        bool deepScan,
        out Dictionary<string, int> roundDetails)
    {
        roundDetails = new Dictionary<string, int>(StringComparer.Ordinal);
        IntPtr handle = OpenProcess(
            ProcessQueryInformation | ProcessVmRead | ProcessVmWrite | ProcessVmOperation,
            false,
            processId);

        if (handle == IntPtr.Zero)
        {
            return 0;
        }

        try
        {
            int patched = 0;
            List<PatchEntry> activePatches = GetActivePatches(patches, cumulativeCounts, bootstrapOnly);
            if (activePatches.Count == 0)
            {
                return 0;
            }

            int maxPatternBytes = 0;
            foreach (PatchEntry patch in activePatches)
            {
                if (patch.OldBytes.Length > maxPatternBytes)
                {
                    maxPatternBytes = patch.OldBytes.Length;
                }
            }

            int chunkBytes = bootstrapOnly ? BootstrapChunkBytes : FullChunkBytes;
            List<MEMORY_BASIC_INFORMATION64> regions = GetRegions(handle, bootstrapOnly, deepScan);
            regions.Sort(CompareRegions);

            foreach (MEMORY_BASIC_INFORMATION64 mbi in regions)
            {
                ulong baseAddress = mbi.BaseAddress;
                ulong regionSize = mbi.RegionSize;

                for (ulong chunkOffset = 0; chunkOffset < regionSize; chunkOffset += (ulong)chunkBytes)
                {
                    ulong remaining = regionSize - chunkOffset;
                    int size = (int)Math.Min(
                        remaining,
                        (ulong)(chunkBytes + maxPatternBytes));
                    byte[] data = ReadRegion(handle, baseAddress + chunkOffset, size);
                    if (data == null || data.Length == 0)
                    {
                        continue;
                    }

                    patched += PatchChunk(
                        handle,
                        baseAddress + chunkOffset,
                        mbi.Protect,
                        data,
                        activePatches,
                        cumulativeCounts,
                        roundDetails,
                        bootstrapOnly);

                    if (bootstrapOnly && BootstrapSatisfied(patches, cumulativeCounts))
                    {
                        return patched;
                    }
                }
            }

            return patched;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    private static bool ShouldInspectRegion(MEMORY_BASIC_INFORMATION64 mbi, bool bootstrapOnly)
    {
        uint baseProtect = mbi.Protect & ProtectAccessMask;

        if (mbi.State != MemCommit)
        {
            return false;
        }

        if ((mbi.Protect & PageGuard) != 0 || baseProtect == PageNoAccess)
        {
            return false;
        }

        if (mbi.Type != MemPrivate &&
            mbi.Type != MemMapped &&
            mbi.Type != MemImage)
        {
            return false;
        }

        if (baseProtect != PageReadOnly &&
            baseProtect != PageReadWrite &&
            baseProtect != PageWriteCopy &&
            baseProtect != PageExecuteRead &&
            baseProtect != PageExecuteReadWrite &&
            baseProtect != PageExecuteWriteCopy)
        {
            return false;
        }

        ulong maxRegionBytes = bootstrapOnly
            ? (ulong)BootstrapRegionMaxBytes
            : (ulong)ScanMaxBytes;
        if (mbi.RegionSize < (ulong)ScanMinBytes || mbi.RegionSize > maxRegionBytes)
        {
            return false;
        }

        return true;
    }

    private static List<MEMORY_BASIC_INFORMATION64> GetRegions(
        IntPtr handle,
        bool bootstrapOnly,
        bool deepScan)
    {
        List<MEMORY_BASIC_INFORMATION64> regions = new List<MEMORY_BASIC_INFORMATION64>();
        MEMORY_BASIC_INFORMATION64 mbi;
        ulong address = 0;
        while (VirtualQueryEx(
            handle,
            new IntPtr(unchecked((long)address)),
            out mbi,
            (UIntPtr)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION64))) != IntPtr.Zero)
        {
            if (ShouldInspectRegion(mbi, bootstrapOnly))
            {
                regions.Add(mbi);
            }

            ulong nextAddress = mbi.BaseAddress + mbi.RegionSize;
            if (nextAddress <= address)
            {
                break;
            }

            address = nextAddress;
        }

        List<MEMORY_BASIC_INFORMATION64> preferred = regions
            .Where(IsPreferredStringRegion)
            .ToList();

        if (bootstrapOnly)
        {
            if (preferred.Count > 0)
            {
                return preferred;
            }
        }
        else if (!deepScan && preferred.Count > 0)
        {
            return preferred;
        }

        List<MEMORY_BASIC_INFORMATION64> primary = regions
            .Where(IsPrimaryStringRegion)
            .ToList();
        if (primary.Count > 0)
        {
            if (!deepScan)
            {
                return primary;
            }

            List<MEMORY_BASIC_INFORMATION64> secondary = regions
                .Where(region => !IsPrimaryStringRegion(region))
                .ToList();
            primary.AddRange(secondary);
            return primary;
        }

        return regions;
    }

    private static int CompareRegions(MEMORY_BASIC_INFORMATION64 left, MEMORY_BASIC_INFORMATION64 right)
    {
        bool leftPreferred = IsPreferredStringRegion(left);
        bool rightPreferred = IsPreferredStringRegion(right);
        if (leftPreferred != rightPreferred)
        {
            return leftPreferred ? -1 : 1;
        }

        bool leftPrimary = IsPrimaryStringRegion(left);
        bool rightPrimary = IsPrimaryStringRegion(right);
        if (leftPrimary != rightPrimary)
        {
            return leftPrimary ? -1 : 1;
        }

        if (left.RegionSize < right.RegionSize)
        {
            return -1;
        }

        if (left.RegionSize > right.RegionSize)
        {
            return 1;
        }

        if (left.BaseAddress < right.BaseAddress)
        {
            return -1;
        }

        if (left.BaseAddress > right.BaseAddress)
        {
            return 1;
        }

        return 0;
    }

    private static bool IsPreferredStringRegion(MEMORY_BASIC_INFORMATION64 mbi)
    {
        uint baseProtect = mbi.Protect & ProtectAccessMask;
        return mbi.Type == MemPrivate &&
               baseProtect == PageReadWrite &&
               mbi.RegionSize >= (8UL * 1024UL * 1024UL) &&
               mbi.RegionSize <= (12UL * 1024UL * 1024UL);
    }

    private static bool IsPrimaryStringRegion(MEMORY_BASIC_INFORMATION64 mbi)
    {
        uint baseProtect = mbi.Protect & ProtectAccessMask;
        return mbi.Type == MemPrivate &&
               (baseProtect == PageReadWrite ||
                baseProtect == PageWriteCopy ||
                baseProtect == PageExecuteReadWrite ||
                baseProtect == PageExecuteWriteCopy) &&
               mbi.RegionSize >= (64UL * 1024UL) &&
               mbi.RegionSize <= (16UL * 1024UL * 1024UL);
    }

    private static int PatchChunk(
        IntPtr handle,
        ulong chunkBaseAddress,
        uint protect,
        byte[] data,
        List<PatchEntry> activePatches,
        Dictionary<string, int> cumulativeCounts,
        Dictionary<string, int> roundDetails,
        bool bootstrapOnly)
    {
        int patched = 0;
        Dictionary<string, int> groupsPresent = GetGroupsPresent(data, activePatches);

        foreach (PatchEntry patch in activePatches)
        {
            if (SkipPatchForRound(patch, cumulativeCounts, bootstrapOnly))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(patch.Group))
            {
                int presentCount;
                groupsPresent.TryGetValue(patch.Group, out presentCount);
                if (presentCount < patch.GroupPresenceRequired)
                {
                    continue;
                }
            }

            int searchOffset = 0;
            while (true)
            {
                if (SkipPatchForRound(patch, cumulativeCounts, bootstrapOnly))
                {
                    break;
                }

                int index = IndexOf(data, patch.OldBytes, searchOffset);
                if (index < 0)
                {
                    break;
                }

                ulong patchAddress = chunkBaseAddress + (ulong)index;
                if (WriteBytes(handle, patchAddress, patch.NewBytes, protect))
                {
                    patched++;
                    int count;
                    roundDetails.TryGetValue(patch.Label, out count);
                    roundDetails[patch.Label] = count + 1;

                    int cumulative;
                    cumulativeCounts.TryGetValue(patch.Label, out cumulative);
                    cumulativeCounts[patch.Label] = cumulative + 1;

                    Buffer.BlockCopy(patch.NewBytes, 0, data, index, patch.NewBytes.Length);
                }

                searchOffset = index + 2;
            }
        }

        return patched;
    }

    private static Dictionary<string, int> GetGroupsPresent(byte[] data, List<PatchEntry> patches)
    {
        Dictionary<string, int> groupsPresent = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (PatchEntry patch in patches)
        {
            if (string.IsNullOrEmpty(patch.Group))
            {
                continue;
            }

            if (IndexOf(data, patch.OldBytes, 0) < 0)
            {
                continue;
            }

            int count;
            groupsPresent.TryGetValue(patch.Group, out count);
            groupsPresent[patch.Group] = count + 1;
        }
        return groupsPresent;
    }

    private static List<PatchEntry> GetActivePatches(
        List<PatchEntry> patches,
        Dictionary<string, int> cumulativeCounts,
        bool bootstrapOnly)
    {
        List<PatchEntry> active = new List<PatchEntry>();
        foreach (PatchEntry patch in patches)
        {
            if (bootstrapOnly)
            {
                if (patch.Bootstrap && !SkipPatchForRound(patch, cumulativeCounts, bootstrapOnly))
                {
                    active.Add(patch);
                }
                continue;
            }

            active.Add(patch);
        }
        return active;
    }

    private static bool SkipPatchForRound(
        PatchEntry patch,
        Dictionary<string, int> cumulativeCounts,
        bool bootstrapOnly)
    {
        if (!bootstrapOnly)
        {
            return false;
        }

        if (!patch.Bootstrap)
        {
            return true;
        }

        return ReachedDesiredHits(patch, cumulativeCounts);
    }

    private static bool ReachedDesiredHits(PatchEntry patch, Dictionary<string, int> cumulativeCounts)
    {
        int current;
        cumulativeCounts.TryGetValue(patch.Label, out current);
        return current >= patch.DesiredHits;
    }

    private static bool BootstrapSatisfied(List<PatchEntry> patches, Dictionary<string, int> cumulativeCounts)
    {
        foreach (PatchEntry patch in patches)
        {
            if (!patch.Bootstrap)
            {
                continue;
            }

            if (!ReachedDesiredHits(patch, cumulativeCounts))
            {
                return false;
            }
        }

        return true;
    }

    private static int GetDesiredHits(string label)
    {
        return 1;
    }

    private static string GetPatchGroup(string label)
    {
        switch (label)
        {
            case "Emulated":
            case "Internal":
            case "Emotional":
            case "Reasoning":
                return "cutscene-modes";
            case "CYCLES":
            case "SIZE":
            case "ACTIVITY":
                return "organizer-graph";
            case "INTRO":
            case "OUTRO":
                return "cutscene-buttons";
            default:
                return null;
        }
    }

    private static int GetGroupPresenceRequired(string label)
    {
        switch (label)
        {
            case "Emulated":
            case "Internal":
            case "Emotional":
            case "Reasoning":
                return 3;
            case "CYCLES":
            case "SIZE":
            case "ACTIVITY":
                return 2;
            case "INTRO":
            case "OUTRO":
                return 2;
            default:
                return 0;
        }
    }

    private static bool WriteBytes(IntPtr handle, ulong address, byte[] bytes, uint protect)
    {
        uint baseProtect = protect & ProtectAccessMask;
        bool needsProtectChange = !IsWritableProtect(baseProtect);
        uint originalProtect = 0;

        if (needsProtectChange)
        {
            uint writableProtect = HasExecute(baseProtect) ? PageExecuteReadWrite : PageReadWrite;
            if (!VirtualProtectEx(
                handle,
                new IntPtr(unchecked((long)address)),
                new UIntPtr((uint)bytes.Length),
                writableProtect,
                out originalProtect))
            {
                return false;
            }
        }

        IntPtr written;
        bool result = WriteProcessMemory(
            handle,
            new IntPtr(unchecked((long)address)),
            bytes,
            bytes.Length,
            out written) && written.ToInt32() == bytes.Length;

        if (needsProtectChange)
        {
            uint ignored;
            VirtualProtectEx(
                handle,
                new IntPtr(unchecked((long)address)),
                new UIntPtr((uint)bytes.Length),
                originalProtect,
                out ignored);
        }

        return result;
    }

    private static byte[] ReadRegion(IntPtr handle, ulong baseAddress, int size)
    {
        if (size <= 0)
        {
            return null;
        }

        byte[] buffer = new byte[size];
        IntPtr bytesRead;
        if (!ReadProcessMemory(handle, new IntPtr(unchecked((long)baseAddress)), buffer, size, out bytesRead))
        {
            return null;
        }

        int actual = bytesRead.ToInt32();
        if (actual <= 0)
        {
            return null;
        }

        if (actual == buffer.Length)
        {
            return buffer;
        }

        byte[] trimmed = new byte[actual];
        Buffer.BlockCopy(buffer, 0, trimmed, 0, actual);
        return trimmed;
    }

    private static int IndexOf(byte[] haystack, byte[] needle, int start)
    {
        if (needle.Length == 0 || haystack.Length < needle.Length)
        {
            return -1;
        }

        for (int i = start; i <= haystack.Length - needle.Length; i++)
        {
            if (haystack[i] != needle[0])
            {
                continue;
            }

            bool match = true;
            for (int j = 1; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return i;
            }
        }

        return -1;
    }

    private static void Log(string logPath, string message)
    {
        try
        {
            string line = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + message + Environment.NewLine;
            File.AppendAllText(logPath, line, Encoding.UTF8);
        }
        catch
        {
        }
    }

    private static bool IsWritableProtect(uint protect)
    {
        return protect == PageReadWrite ||
               protect == PageWriteCopy ||
               protect == PageExecuteReadWrite ||
               protect == PageExecuteWriteCopy;
    }

    private static bool HasExecute(uint protect)
    {
        return protect == PageExecuteRead ||
               protect == PageExecuteReadWrite ||
               protect == PageExecuteWriteCopy;
    }
}
