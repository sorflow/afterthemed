using System.Drawing.Text;
using System.Text;

namespace DvauiThemeEditor;

internal static class InstalledFontCatalog
{
    // Every supported DVAUI layout contains at least one 16-byte, NUL-terminated
    // font slot. Limiting names to 15 ASCII bytes guarantees that a generic
    // family name can be written to every slot without truncation.
    internal const int MaximumPatchNameBytes = 15;

    internal static IReadOnlyList<string> FindCompatibleFamilies()
    {
        using var installed = new InstalledFontCollection();
        return installed.Families
            .Select(family => family.Name.Trim())
            .Where(IsCompatiblePatchName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    internal static bool IsCompatiblePatchName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var trimmed = name.Trim();
        return trimmed.All(character => character is >= ' ' and <= '~') &&
               Encoding.ASCII.GetByteCount(trimmed) <= MaximumPatchNameBytes;
    }

    internal static void ValidatePatchName(string name)
    {
        if (IsCompatiblePatchName(name)) return;
        throw new InvalidDataException(
            $"The DVAUI font name must be ASCII and no longer than {MaximumPatchNameBytes} bytes. " +
            "A longer name would overwrite the next fixed-width DLL field.");
    }
}
