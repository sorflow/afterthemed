using System.Buffers.Binary;
using System.Text;

namespace DvauiThemeEditor;

internal sealed record PeSection(string Name, int Offset, int Size, uint Rva, uint VirtualSize, uint Characteristics);
internal sealed record PeResource(string Type, string Name, string Language, int Offset, int Size);

internal sealed class DvauiPeImage
{
    private readonly byte[] data;
    private readonly int optionalHeader;
    private readonly int resourceBase;

    internal DvauiPeImage(byte[] data)
    {
        this.data = data;
        if (data.Length < 0x100 || data[0] != 'M' || data[1] != 'Z')
            throw new InvalidDataException("The selected file is not a Windows DLL/PE file.");

        var pe = ReadInt32(0x3C);
        Require(pe, 24);
        if (ReadUInt32(pe) != 0x00004550)
            throw new InvalidDataException("The selected file has an invalid Windows PE header.");

        var sectionCount = ReadUInt16(pe + 6);
        var optionalSize = ReadUInt16(pe + 20);
        optionalHeader = pe + 24;
        Require(optionalHeader, optionalSize);
        var optionalMagic = ReadUInt16(optionalHeader);
        Is64Bit = optionalMagic switch
        {
            0x20B => true,
            0x10B => false,
            _ => throw new InvalidDataException("The selected DLL uses an unsupported PE optional header.")
        };
        ImageBase = Is64Bit ? ReadUInt64(optionalHeader + 24) : ReadUInt32(optionalHeader + 28);
        var sectionTable = optionalHeader + optionalSize;
        var sections = new List<PeSection>(sectionCount);
        for (var i = 0; i < sectionCount; i++)
        {
            var offset = sectionTable + i * 40;
            Require(offset, 40);
            var nameLength = Array.IndexOf(data, (byte)0, offset, 8);
            if (nameLength < 0) nameLength = offset + 8;
            var name = Encoding.ASCII.GetString(data, offset, nameLength - offset);
            var virtualSize = ReadUInt32(offset + 8);
            var rva = ReadUInt32(offset + 12);
            var rawSize = checked((int)ReadUInt32(offset + 16));
            var rawOffset = checked((int)ReadUInt32(offset + 20));
            var characteristics = ReadUInt32(offset + 36);
            if (rawSize > 0) Require(rawOffset, rawSize);
            sections.Add(new PeSection(name, rawOffset, rawSize, rva, virtualSize, characteristics));
        }
        Sections = sections;

        var resourceRva = ReadDataDirectory(2).Rva;
        resourceBase = resourceRva == 0 ? -1 : RvaToOffset(resourceRva);
    }

    internal IReadOnlyList<PeSection> Sections { get; }
    internal bool Is64Bit { get; }
    internal ulong ImageBase { get; }

    internal bool TryVaToRva(ulong virtualAddress, out uint rva)
    {
        rva = 0;
        if (virtualAddress < ImageBase || virtualAddress - ImageBase > uint.MaxValue) return false;
        var candidate = (uint)(virtualAddress - ImageBase);
        if (!Sections.Any(section => ContainsRva(section, candidate))) return false;
        rva = candidate;
        return true;
    }

    internal bool IsExecutableRva(uint rva) =>
        Sections.Any(section => ContainsRva(section, rva) &&
                                (section.Characteristics & 0x20000000) != 0);

    internal IReadOnlyList<PeResource> Resources()
    {
        if (resourceBase < 0) return Array.Empty<PeResource>();
        var resources = new List<PeResource>();
        WalkResourceDirectory(0, [], resources, 0);
        return resources;
    }

    internal bool TryFindExport(Func<string, bool> predicate, out uint functionRva)
    {
        functionRva = 0;
        var export = ReadDataDirectory(0);
        if (export.Rva == 0 || export.Size < 40) return false;
        var directory = RvaToOffset(export.Rva);
        Require(directory, 40);
        var functionCount = ReadUInt32(directory + 20);
        var nameCount = ReadUInt32(directory + 24);
        var functions = RvaToOffset(ReadUInt32(directory + 28));
        var names = RvaToOffset(ReadUInt32(directory + 32));
        var ordinals = RvaToOffset(ReadUInt32(directory + 36));
        if (functionCount > 200_000 || nameCount > 200_000) return false;

        for (uint i = 0; i < nameCount; i++)
        {
            var nameRva = ReadUInt32(names + checked((int)i * 4));
            var name = ReadAsciiZ(RvaToOffset(nameRva), 1024);
            if (!predicate(name)) continue;
            var ordinal = ReadUInt16(ordinals + checked((int)i * 2));
            if (ordinal >= functionCount) return false;
            functionRva = ReadUInt32(functions + ordinal * 4);
            return true;
        }
        return false;
    }

    internal int RvaToOffset(uint rva)
    {
        foreach (var section in Sections)
        {
            var span = Math.Max(section.VirtualSize, checked((uint)section.Size));
            if (rva < section.Rva || rva >= section.Rva + span) continue;
            var offset = checked(section.Offset + (int)(rva - section.Rva));
            Require(offset, 1);
            return offset;
        }
        if (rva < data.Length) return checked((int)rva);
        throw new InvalidDataException($"The PE file contains an invalid RVA: 0x{rva:X}.");
    }

    private (uint Rva, uint Size) ReadDataDirectory(int index)
    {
        var magic = ReadUInt16(optionalHeader);
        var directory = optionalHeader + (magic == 0x20B ? 112 : magic == 0x10B ? 96 :
            throw new InvalidDataException("The selected DLL uses an unsupported PE optional header."));
        Require(directory + index * 8, 8);
        return (ReadUInt32(directory + index * 8), ReadUInt32(directory + index * 8 + 4));
    }

    private void WalkResourceDirectory(int relativeOffset, List<string> path, List<PeResource> result, int depth)
    {
        if (depth > 4) throw new InvalidDataException("The PE resource tree is unexpectedly deep.");
        var directory = checked(resourceBase + relativeOffset);
        Require(directory, 16);
        var entries = ReadUInt16(directory + 12) + ReadUInt16(directory + 14);
        if (entries > 20_000) throw new InvalidDataException("The PE resource tree is invalid.");
        Require(directory + 16, entries * 8);

        for (var i = 0; i < entries; i++)
        {
            var entry = directory + 16 + i * 8;
            var name = ReadResourceName(ReadUInt32(entry));
            var child = ReadUInt32(entry + 4);
            var next = new List<string>(path) { name };
            if ((child & 0x80000000) != 0)
            {
                WalkResourceDirectory(checked((int)(child & 0x7FFFFFFF)), next, result, depth + 1);
                continue;
            }

            var dataEntry = checked(resourceBase + (int)child);
            Require(dataEntry, 16);
            var rva = ReadUInt32(dataEntry);
            var size = checked((int)ReadUInt32(dataEntry + 4));
            var offset = RvaToOffset(rva);
            Require(offset, size);
            result.Add(new PeResource(
                next.ElementAtOrDefault(0) ?? string.Empty,
                next.ElementAtOrDefault(1) ?? string.Empty,
                next.ElementAtOrDefault(2) ?? string.Empty,
                offset,
                size));
        }
    }

    private string ReadResourceName(uint value)
    {
        if ((value & 0x80000000) == 0) return value.ToString();
        var offset = checked(resourceBase + (int)(value & 0x7FFFFFFF));
        var length = ReadUInt16(offset);
        Require(offset + 2, length * 2);
        return Encoding.Unicode.GetString(data, offset + 2, length * 2);
    }

    private string ReadAsciiZ(int offset, int maximum)
    {
        Require(offset, 1);
        var end = offset;
        var limit = Math.Min(data.Length, offset + maximum);
        while (end < limit && data[end] != 0) end++;
        return Encoding.ASCII.GetString(data, offset, end - offset);
    }

    private ushort ReadUInt16(int offset)
    {
        Require(offset, 2);
        return BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, 2));
    }

    private uint ReadUInt32(int offset)
    {
        Require(offset, 4);
        return BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4));
    }

    private ulong ReadUInt64(int offset)
    {
        Require(offset, 8);
        return BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(offset, 8));
    }

    private static bool ContainsRva(PeSection section, uint rva)
    {
        var span = Math.Max(section.VirtualSize, checked((uint)section.Size));
        return rva >= section.Rva && (ulong)rva < (ulong)section.Rva + span;
    }

    private int ReadInt32(int offset)
    {
        Require(offset, 4);
        return BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, 4));
    }

    private void Require(int offset, int size)
    {
        if (offset < 0 || size < 0 || offset > data.Length - size)
            throw new InvalidDataException("The selected DLL contains an invalid or truncated PE structure.");
    }
}
