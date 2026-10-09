using System.Diagnostics;

namespace DvauiThemeEditor;

/// <summary>The AEP Downgrader tab: a queue of projects converted on a background thread.</summary>
public partial class Form1
{
    private sealed class AepItem
    {
        public required string Id;
        public required string Path;
        public long Size;
        public AepVersion? Version;
        public string Status = "ready"; // ready · current · converting · done · error
        public string Detail = string.Empty;
        public string Output = string.Empty;
        public IReadOnlyList<string> Changes = [];
        // Dry-run changes for PreviewTarget, so the inspector shows real edits before anything is written.
        public IReadOnlyList<string>? Preview;
        public int PreviewTarget;
    }

    private readonly List<AepItem> aepItems = [];
    private int aepTarget = 24;
    private bool aepBusy;
    private const int NewestKnownAep = 25;

    private void AddAepFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            var full = Path.GetFullPath(path);
            if (aepItems.Any(item => string.Equals(item.Path, full, StringComparison.OrdinalIgnoreCase))) continue;
            var item = new AepItem { Id = Guid.NewGuid().ToString("N"), Path = full };
            try
            {
                if (!Path.GetExtension(full).Equals(".aep", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Only After Effects projects (.aep) can be downgraded.");
                item.Size = new FileInfo(full).Length;
                item.Version = AepDowngrader.ReadVersion(full);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                item.Status = "error";
                item.Detail = ex.Message;
            }
            aepItems.Add(item);
        }
        RefreshAepStatuses();
        PreviewAepItems();
    }

    /// <summary>Runs the conversion in memory for each ready project whose preview is missing or for another target.</summary>
    private async void PreviewAepItems()
    {
        foreach (var item in aepItems.Where(item => item.Status == "ready" && (item.Preview is null || item.PreviewTarget != aepTarget)).ToList())
        {
            var target = aepTarget;
            IReadOnlyList<string> changes;
            try { changes = (await Task.Run(() => AepDowngrader.Preview(item.Path, target))).Changes; }
            catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or ArgumentException)
            {
                changes = [$"Could not inspect: {ex.Message}"];
            }
            // The target may have changed while this ran; a newer pass computes that target's preview.
            if (target != aepTarget) continue;
            item.Preview = changes;
            item.PreviewTarget = target;
            if (IsDisposed || Disposing) return;
            PublishWebState();
        }
    }

    private void RefreshAepStatuses()
    {
        foreach (var item in aepItems.Where(item => item.Status is "ready" or "current" && item.Version is not null))
        {
            var major = item.Version!.Major;
            item.Status = major <= aepTarget ? "current" : "ready";
            item.Detail = major <= aepTarget
                ? $"Already opens in After Effects {aepTarget}.x."
                : major > NewestKnownAep
                    ? $"Made with After Effects {major}. Changes newer than {NewestKnownAep}.x are not known yet and are kept as they are."
                    : string.Empty;
        }
    }

    private async void ConvertAepFiles()
    {
        if (aepBusy) return;
        aepBusy = true;
        var target = aepTarget;
        try
        {
            foreach (var item in aepItems.Where(item => item.Status == "ready").ToList())
            {
                item.Status = "converting";
                PublishWebState();
                try
                {
                    var result = await Task.Run(() => AepDowngrader.Downgrade(item.Path, AepDowngrader.OutputPathFor(item.Path, target), target));
                    item.Status = "done";
                    item.Output = result.OutputPath;
                    item.Changes = result.Changes;
                    item.Detail = $"Saved for After Effects {target}.x";
                    Log($"Downgraded {Path.GetFileName(item.Path)} to After Effects {target}.x · {result.OutputPath}");
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or ArgumentException)
                {
                    item.Status = "error";
                    item.Detail = ex.Message;
                    Log($"Could not downgrade {Path.GetFileName(item.Path)} · {ex.Message}");
                }
            }
        }
        finally
        {
            aepBusy = false;
            if (!IsDisposed && !Disposing) PublishWebState();
        }
    }

    private void HandleAepMessage(string type, string value, IReadOnlyList<string> files)
    {
        switch (type)
        {
            case "aepAdd": AddAepFiles(files); break;
            case "aepTarget":
                if (!aepBusy && int.TryParse(value, out var target) && AepDowngrader.TargetHeads.ContainsKey(target))
                {
                    aepTarget = target;
                    RefreshAepStatuses();
                    PreviewAepItems();
                }
                break;
            case "aepRemove": if (!aepBusy) aepItems.RemoveAll(item => item.Id == value); break;
            case "aepClear": if (!aepBusy) aepItems.Clear(); break;
            case "aepConvert": ConvertAepFiles(); break;
            case "aepReveal":
                if (aepItems.Find(item => item.Id == value) is { Output.Length: > 0 } done && File.Exists(done.Output))
                    Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{done.Output}\"") { UseShellExecute = true });
                break;
        }
    }

    private object AepState() => new
    {
        target = aepTarget,
        targets = AepDowngrader.TargetHeads.Keys.OrderDescending().ToArray(),
        busy = aepBusy,
        items = aepItems.Select(item => new
        {
            id = item.Id,
            name = Path.GetFileName(item.Path),
            folder = Path.GetDirectoryName(item.Path) ?? string.Empty,
            size = item.Size,
            version = item.Version is { } v ? $"{v.Major}.{v.Minor}" : string.Empty,
            status = item.Status,
            detail = item.Detail,
            output = item.Output.Length > 0 ? Path.GetFileName(item.Output) : string.Empty,
            changes = item.Changes,
            path = item.Path,
            plannedOutput = item.Output.Length > 0 ? item.Output
                : item.Status == "ready" ? AepDowngrader.OutputPathFor(item.Path, aepTarget) : string.Empty,
            preview = item.Preview is not null && item.PreviewTarget == aepTarget ? item.Preview : null
        }).ToArray()
    };
}
