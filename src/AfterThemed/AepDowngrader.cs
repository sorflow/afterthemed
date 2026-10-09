using System.Buffers.Binary;
using System.Text;

namespace DvauiThemeEditor;

internal sealed record AepVersion(int Major, int Minor, int Patch, int Build)
{
    public override string ToString() => $"{Major}.{Minor}.{Patch} (build {Build})";
}

internal sealed record AepDowngradeResult(AepVersion Source, AepVersion Target, string OutputPath, IReadOnlyList<string> Changes);

/// <summary>
/// Rewrites an After Effects project (RIFX "Egg!") so an older release opens it, matching what
/// After Effects' own "Save as 23.x/24.x" writes: the version header is replaced and properties that
/// the older release does not know are removed. Everything else is copied byte for byte.
/// </summary>
internal static class AepDowngrader
{
    /// <summary>
    /// Head chunk bytes 0-7 (format generation + release word) and byte 19, exactly as After Effects writes them
    /// for each "Save as" target. Releases without their own entry use the newest older format (19-21 use 18).
    /// </summary>
    internal static readonly IReadOnlyDictionary<int, (byte[] Head, byte Tail)> TargetHeads = new Dictionary<int, (byte[], byte)>
    {
        [24] = ([0x00, 0x5F, 0x00, 0x06, 0x0F, 0x03, 0x06, 0x41], 0x3A),
        [23] = ([0x00, 0x5E, 0x00, 0x09, 0x0B, 0x3B, 0x06, 0x37], 0x3A),
        [22] = ([0x00, 0x5D, 0x00, 0x2B, 0x0B, 0x33, 0x06, 0x3B], 0x48),
        [18] = ([0x00, 0x5D, 0x00, 0x1D, 0x0B, 0x12, 0x06, 0x26], 0x48),
    };

    /// <summary>The format used for <paramref name="major"/>: its own, or the newest older one (the 18.x fallback).</summary>
    internal static int FormatFor(int major)
    {
        var known = TargetHeads.Keys.Where(format => format <= major).ToList();
        if (known.Count == 0)
            throw new ArgumentOutOfRangeException(nameof(major), $"After Effects {major}.x is older than the oldest supported format (18.x).");
        return known.Max();
    }

    /// <summary>Properties that only exist from a release on; they are removed for any older target.</summary>
    private static readonly (string MatchName, int AddedIn, string Description)[] NewProperties =
    [
        ("ADBE Shadow Color", 24, "Material Options › Shadow Color"),
        ("ADBE Light Transmission", 23, "Material Options › Light Transmission"),
    ];

    // nhed/nnhd tail After Effects writes when saving down to 23.x/24.x; 22.x and older zero the first
    // eight bytes and keep a four-byte project identifier.
    private static readonly byte[] DowngradedHeaderTail = [0xF7, 0x2B, 0xC9, 0xD5, 0x00, 0x00, 0x0A, 0xA5, 0x00, 0x00, 0x00, 0x00];

    private sealed class Chunk
    {
        public required string Id;
        public string? ListType;
        public byte[] Data = [];
        public List<Chunk> Children = [];
        public bool IsList => ListType is not null;
    }

    /// <summary>Reads only the top-level chunks up to the version header, so large projects are inspected instantly.</summary>
    internal static AepVersion ReadVersion(string path)
    {
        using var stream = File.OpenRead(path);
        var header = new byte[12];
        if (stream.Read(header, 0, 12) < 12 || Encoding.Latin1.GetString(header, 0, 4) != "RIFX" || Encoding.Latin1.GetString(header, 8, 4) != "Egg!")
            throw new InvalidDataException("This is not an After Effects project (.aep).");
        for (var i = 0; i < 64 && stream.Position + 8 <= stream.Length; i++)
        {
            stream.ReadExactly(header, 0, 8);
            var size = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(4));
            if (Encoding.Latin1.GetString(header, 0, 4) == "head")
            {
                var data = new byte[Math.Min(size, 64u)];
                stream.ReadExactly(data);
                return ReadVersion(data);
            }
            stream.Seek(size + (size & 1), SeekOrigin.Current);
        }
        throw new InvalidDataException("This project has no version header.");
    }

    /// <summary>"Name (AE 24.x).aep" beside the original, numbered instead of ever replacing a file.</summary>
    internal static string OutputPathFor(string input, int targetMajor)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(input))!;
        var name = Path.GetFileNameWithoutExtension(input);
        var candidate = Path.Combine(folder, $"{name} (AE {targetMajor}.x).aep");
        for (var n = 2; File.Exists(candidate); n++)
            candidate = Path.Combine(folder, $"{name} (AE {targetMajor}.x) {n}.aep");
        return candidate;
    }

    internal static AepDowngradeResult Downgrade(string input, string output, int targetMajor)
    {
        if (string.Equals(Path.GetFullPath(input), Path.GetFullPath(output), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Choose a different file name; the original project is never overwritten.");
        var (bytes, root, riffEnd, source, target, changes) = Convert(input, targetMajor);
        var rewritten = Serialize(root);
        var temporary = output + ".partial";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
            {
                stream.Write(rewritten);
                stream.Write(bytes, riffEnd, bytes.Length - riffEnd); // XMP metadata after the RIFX form.
            }
            File.Move(temporary, output);
        }
        catch
        {
            File.Delete(temporary);
            throw;
        }
        return new AepDowngradeResult(source, target, output, changes);
    }

    /// <summary>The changes a downgrade would make, computed by the same conversion without writing anything.</summary>
    internal static AepDowngradeResult Preview(string input, int targetMajor)
    {
        var (_, _, _, source, target, changes) = Convert(input, targetMajor);
        return new AepDowngradeResult(source, target, OutputPathFor(input, targetMajor), changes);
    }

    private static (byte[] Bytes, Chunk Root, int RiffEnd, AepVersion Source, AepVersion Target, List<string> Changes)
        Convert(string input, int targetMajor)
    {
        var format = FormatFor(targetMajor);
        var (targetHead, headTail) = TargetHeads[format];
        if (new FileInfo(input).Length > int.MaxValue - 64)
            throw new InvalidDataException("Projects larger than 2 GB are not supported.");
        var bytes = File.ReadAllBytes(input);
        var (root, riffEnd) = Parse(bytes);
        var head = Find(root, "head") ?? throw new InvalidDataException("This project has no version header.");
        var source = ReadVersion(head.Data);
        var target = ReadVersion(targetHead.Concat(head.Data.Skip(8)).ToArray());
        if (source.Major <= targetMajor)
            throw new InvalidOperationException($"This project is already from After Effects {source.Major}.x, so it opens in {targetMajor}.x without converting.");
        if (source.Major <= format)
            throw new InvalidOperationException($"After Effects {targetMajor}.x uses the {format}.x format, which this {source.Major}.x project already has.");

        var changes = new List<string> { $"Version header {source.Major}.{source.Minor} → {target.Major}.{target.Minor}" };
        if (format != targetMajor) changes.Add($"Saved in the {format}.x format, which After Effects {targetMajor}.x opens");
        Array.Copy(targetHead, head.Data, 8);
        if (head.Data.Length >= 20) head.Data[19] = headTail;
        var projectId = format >= 23 ? null : ProjectId(root, bytes);
        foreach (var (id, offset) in new[] { ("nhed", 0x14), ("nnhd", 0x1C) })
            if (Find(root, id) is { } chunk && chunk.Data.Length >= offset + 12)
            {
                if (projectId is null) Array.Copy(DowngradedHeaderTail, 0, chunk.Data, offset, 12);
                else
                {
                    Array.Clear(chunk.Data, offset, 8);
                    Array.Copy(projectId, 0, chunk.Data, offset + 8, 4);
                }
            }

        foreach (var (matchName, addedIn, description) in NewProperties)
        {
            if (format >= addedIn) continue;
            var removed = RemoveProperty(root, matchName);
            if (removed > 0) changes.Add($"Removed {description} ({removed}×, added in {addedIn}.x)");
        }
        if (format < 23)
        {
            // 23.x added four bytes to every layer record and a flag byte to spatial property records.
            var layers = 0;
            Walk(root, chunk =>
            {
                if (chunk.Id == "ldta" && chunk.Data.Length == 164) { chunk.Data = chunk.Data[..160]; layers++; }
                if (chunk.Id == "tdb4" && chunk.Data.Length == 124) chunk.Data[119] = 0;
            });
            if (layers > 0) changes.Add($"Converted {layers} layer record{(layers == 1 ? "" : "s")} to the {format}.x layout");
        }
        if (format < 19 && RemoveChunks(root, "ppSn") > 0)
            changes.Add("Removed a preview setting added after 18.x");
        return (bytes, root, riffEnd, source, target, changes);
    }

    internal static AepVersion ReadVersion(byte[] head)
    {
        if (head.Length < 8) throw new InvalidDataException("The version header is too short.");
        var word = BinaryPrimitives.ReadUInt32BigEndian(head.AsSpan(4));
        int Bits(int start, int length) => (int)((word >> (32 - start - length)) & ((1u << length) - 1));
        return new AepVersion((Bits(1, 5) << 3) | Bits(10, 3), Bits(13, 4), Bits(17, 4), Bits(23, 9));
    }

    private static (Chunk Root, int RiffEnd) Parse(byte[] bytes)
    {
        if (bytes.Length < 12 || Encoding.Latin1.GetString(bytes, 0, 4) != "RIFX" || Encoding.Latin1.GetString(bytes, 8, 4) != "Egg!")
            throw new InvalidDataException("This is not an After Effects project (.aep).");
        var size = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(4));
        if (size < 4 || 8L + size > bytes.Length) throw new InvalidDataException("The project file is truncated.");
        var root = new Chunk { Id = "RIFX", ListType = "Egg!" };
        ParseChildren(bytes, 12, 8 + (int)size, root.Children);
        return (root, 8 + (int)size);
    }

    private static void ParseChildren(byte[] bytes, int offset, int end, List<Chunk> into)
    {
        while (offset < end)
        {
            if (end - offset < 8) throw new InvalidDataException("The project contains a damaged chunk.");
            var id = Encoding.Latin1.GetString(bytes, offset, 4);
            var size = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset + 4));
            var data = offset + 8;
            if (size > end - data) throw new InvalidDataException($"The project's '{id}' chunk is damaged.");
            var chunk = new Chunk { Id = id };
            // btdk lists hold an opaque payload rather than chunks.
            if (id == "LIST" && size >= 4 && Encoding.Latin1.GetString(bytes, data, 4) != "btdk")
            {
                chunk.ListType = Encoding.Latin1.GetString(bytes, data, 4);
                ParseChildren(bytes, data + 4, data + (int)size, chunk.Children);
            }
            else chunk.Data = bytes.AsSpan(data, (int)size).ToArray();
            into.Add(chunk);
            offset = data + (int)size + (int)(size & 1);
        }
    }

    private static byte[] Serialize(Chunk root)
    {
        using var stream = new MemoryStream();
        Write(stream, root);
        return stream.ToArray();
    }

    private static void Write(Stream stream, Chunk chunk)
    {
        Span<byte> header = stackalloc byte[8];
        Encoding.Latin1.GetBytes(chunk.Id, header);
        var start = stream.Position;
        stream.Write(header);
        if (chunk.IsList)
        {
            stream.Write(Encoding.Latin1.GetBytes(chunk.ListType!));
            foreach (var child in chunk.Children) Write(stream, child);
        }
        else stream.Write(chunk.Data);
        var size = (uint)(stream.Position - start - 8);
        var end = stream.Position;
        stream.Position = start + 4;
        BinaryPrimitives.WriteUInt32BigEndian(header[4..], size);
        stream.Write(header[4..]);
        stream.Position = end;
        if ((size & 1) == 1 && chunk.Id != "RIFX") stream.WriteByte(0);
    }

    private static Chunk? Find(Chunk parent, string id)
    {
        foreach (var child in parent.Children)
            if (child.Id == id) return child;
        return null;
    }

    /// <summary>Removes each "tdmn" naming <paramref name="matchName"/> together with the property list that follows it.</summary>
    private static int RemoveProperty(Chunk parent, string matchName)
    {
        var removed = 0;
        for (var i = 0; i < parent.Children.Count; i++)
        {
            var child = parent.Children[i];
            if (child.Id == "tdmn" && MatchName(child.Data) == matchName &&
                i + 1 < parent.Children.Count && parent.Children[i + 1].IsList)
            {
                parent.Children.RemoveRange(i, 2);
                removed++;
                i--;
            }
            else if (child.IsList) removed += RemoveProperty(child, matchName);
        }
        return removed;
    }

    private static void Walk(Chunk parent, Action<Chunk> visit)
    {
        foreach (var child in parent.Children)
        {
            visit(child);
            if (child.IsList) Walk(child, visit);
        }
    }

    private static int RemoveChunks(Chunk parent, string id)
    {
        var removed = parent.Children.RemoveAll(child => child.Id == id);
        foreach (var child in parent.Children.Where(child => child.IsList)) removed += RemoveChunks(child, id);
        return removed;
    }

    /// <summary>22.x and older keep a four-byte identifier at the end of nhed: reuse a non-zero one, else derive it from the project.</summary>
    private static byte[] ProjectId(Chunk root, byte[] bytes)
    {
        if (Find(root, "nhed") is { Data.Length: >= 0x20 } nhed && nhed.Data.AsSpan(0x1C, 4).IndexOfAnyExcept((byte)0) >= 0)
            return nhed.Data[0x1C..0x20];
        return System.Security.Cryptography.SHA256.HashData(bytes)[..4];
    }

    private static string MatchName(byte[] data)
    {
        var length = Array.IndexOf(data, (byte)0);
        return Encoding.Latin1.GetString(data, 0, length < 0 ? data.Length : length);
    }
}
