using System.Text.Json;

namespace DvauiThemeEditor;

internal sealed record NativeInstallReport(
    int ExitCode,
    string Stage,
    string Message,
    string? BackupPath = null,
    bool RollbackAttempted = false,
    bool RollbackSucceeded = false,
    int? ErrorHResult = null,
    string? ExpectedSha256 = null,
    string? ActualSha256 = null,
    string? RollbackMessage = null)
{
    internal bool Succeeded => ExitCode == 0;
}

internal static class NativeInstallReportStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    internal static bool CanWrite(string? reportPath)
    {
        if (string.IsNullOrWhiteSpace(reportPath)) return true;

        string? probePath = null;
        try
        {
            var fullPath = Path.GetFullPath(reportPath.Trim());
            // Reports are per-invocation protocol messages and must never reuse an
            // existing file or directory whose replacement semantics may differ.
            if (File.Exists(fullPath) || Directory.Exists(fullPath)) return false;
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            probePath = fullPath + $".{Guid.NewGuid():N}.probe";
            File.WriteAllText(probePath, "AfterThemed native install report probe");
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (probePath is not null && File.Exists(probePath))
            {
                try { File.Delete(probePath); } catch { /* Best-effort probe cleanup. */ }
            }
        }
    }

    internal static bool TryWrite(string? reportPath, NativeInstallReport report)
    {
        if (string.IsNullOrWhiteSpace(reportPath)) return true;

        string? temporaryPath = null;
        try
        {
            var fullPath = Path.GetFullPath(reportPath.Trim());
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            temporaryPath = fullPath + $".{Guid.NewGuid():N}.tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(report, JsonOptions));
            File.Move(temporaryPath, fullPath, true);
            temporaryPath = null;
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (temporaryPath is not null && File.Exists(temporaryPath))
            {
                try { File.Delete(temporaryPath); } catch { /* Best-effort report cleanup. */ }
            }
        }
    }

    internal static NativeInstallReport? TryRead(string reportPath)
    {
        try
        {
            return File.Exists(reportPath)
                ? JsonSerializer.Deserialize<NativeInstallReport>(File.ReadAllText(reportPath))
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}

internal static class NativeDllInstallCommand
{
    internal static int Run(
        string sourcePath,
        string targetPath,
        string backupDirectory,
        string? reportPath,
        bool requireAfterEffectsClosed = true)
    {
        // A requested report is part of the elevated-operation protocol. Refuse to
        // mutate the target when the result channel is not writable.
        if (!NativeInstallReportStore.CanWrite(reportPath)) return 2;

        var report = NativeDllInstaller.Install(sourcePath, targetPath, backupDirectory,
            requireAfterEffectsClosed);
        return NativeInstallReportStore.TryWrite(reportPath, report) ? report.ExitCode : 2;
    }
}

internal static class NativeDllInstaller
{
    internal interface IAtomicCommitter
    {
        void Replace(string stagedPath, string targetPath);
    }

    private sealed class FileAtomicCommitter : IAtomicCommitter
    {
        internal static readonly FileAtomicCommitter Instance = new();

        public void Replace(string stagedPath, string targetPath) => File.Move(stagedPath, targetPath, true);
    }

    internal static NativeInstallReport Install(
        string sourcePath,
        string targetPath,
        string backupDirectory,
        bool requireAfterEffectsClosed = true,
        IAtomicCommitter? committer = null)
    {
        string stage = "preflight";
        string? temporaryPath = null;
        string? backupPath = null;
        string? fullTarget = null;
        string? expectedHash = null;
        string? actualHash = null;
        var replacementCommitted = false;
        var rollbackAttempted = false;
        var rollbackSucceeded = false;
        string? rollbackMessage = null;

        try
        {
            if (requireAfterEffectsClosed && System.Diagnostics.Process.GetProcessesByName("AfterFX").Length > 0)
                return new NativeInstallReport(3, stage, "After Effects is running.");

            stage = "source path validation";
            var fullSource = Path.GetFullPath(sourcePath);
            stage = "target path validation";
            fullTarget = Path.GetFullPath(targetPath);
            if (!File.Exists(fullSource))
                throw new FileNotFoundException("The generated DLL to install was not found.", fullSource);
            if (!File.Exists(fullTarget))
                throw new FileNotFoundException("The selected installed dvaui.dll was not found.", fullTarget);

            stage = "source verification";
            expectedHash = OriginalDllStore.Sha256(fullSource);

            stage = "backup preparation";
            Directory.CreateDirectory(backupDirectory);

            stage = "target backup";
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            backupPath = Path.Combine(backupDirectory, $"dvaui-{stamp}-{Guid.NewGuid():N}.dll");
            File.Copy(fullTarget, backupPath, false);
            actualHash = OriginalDllStore.Sha256(backupPath);
            if (!string.Equals(OriginalDllStore.Sha256(fullTarget), actualHash, StringComparison.Ordinal))
                throw new IOException("The backup did not match the installed DLL.");

            stage = "staged copy";
            Directory.CreateDirectory(Path.GetDirectoryName(fullTarget)!);
            temporaryPath = Path.Combine(Path.GetDirectoryName(fullTarget)!,
                $"dvaui.afterthemed-{Guid.NewGuid():N}.tmp");
            File.Copy(fullSource, temporaryPath, false);
            actualHash = OriginalDllStore.Sha256(temporaryPath);
            if (!string.Equals(expectedHash, actualHash, StringComparison.Ordinal))
                throw new IOException("The staged DLL did not match the generated DLL.");

            stage = "pre-replacement verification";
            var backupHash = OriginalDllStore.Sha256(backupPath);
            actualHash = OriginalDllStore.Sha256(fullTarget);
            if (!string.Equals(backupHash, actualHash, StringComparison.Ordinal))
                throw new IOException("The installed DLL changed after it was backed up; replacement was cancelled.");

            stage = "DLL replacement";
            (committer ?? FileAtomicCommitter.Instance).Replace(temporaryPath, fullTarget);
            temporaryPath = null;
            replacementCommitted = true;

            stage = "final verification";
            actualHash = OriginalDllStore.Sha256(fullTarget);
            if (!string.Equals(expectedHash, actualHash, StringComparison.Ordinal))
                throw new IOException("The installed DLL did not match the generated DLL.");

            return new NativeInstallReport(0, "completed", "The generated DLL was installed and verified.",
                backupPath, ExpectedSha256: expectedHash, ActualSha256: actualHash);
        }
        catch (Exception exception)
        {
            if (replacementCommitted)
            {
                rollbackAttempted = true;
                string? rollbackTemporaryPath = null;
                if (backupPath is null || fullTarget is null || !File.Exists(backupPath))
                {
                    rollbackMessage = "The verified backup was unavailable for rollback.";
                }
                else
                {
                    try
                    {
                        rollbackTemporaryPath = Path.Combine(Path.GetDirectoryName(fullTarget)!,
                            $"dvaui.afterthemed-rollback-{Guid.NewGuid():N}.tmp");
                        File.Copy(backupPath, rollbackTemporaryPath, false);
                        if (!string.Equals(OriginalDllStore.Sha256(backupPath),
                                OriginalDllStore.Sha256(rollbackTemporaryPath), StringComparison.Ordinal))
                            throw new IOException("The staged rollback DLL did not match the verified backup.");
                        File.Move(rollbackTemporaryPath, fullTarget, true);
                        rollbackTemporaryPath = null;
                        rollbackSucceeded = string.Equals(
                            OriginalDllStore.Sha256(backupPath), OriginalDllStore.Sha256(fullTarget),
                            StringComparison.Ordinal);
                        rollbackMessage = rollbackSucceeded
                            ? "The original installed DLL was restored from the verified backup."
                            : "The restored DLL did not match the verified backup.";
                    }
                    catch (Exception rollbackException)
                    {
                        rollbackSucceeded = false;
                        rollbackMessage = rollbackException.Message;
                    }
                    finally
                    {
                        if (rollbackTemporaryPath is not null && File.Exists(rollbackTemporaryPath))
                        {
                            try { File.Delete(rollbackTemporaryPath); } catch { /* Best-effort rollback cleanup. */ }
                        }
                    }
                }
            }

            return new NativeInstallReport(2, stage, exception.Message, backupPath,
                rollbackAttempted, rollbackSucceeded, exception.HResult, expectedHash, actualHash, rollbackMessage);
        }
        finally
        {
            if (temporaryPath is not null && File.Exists(temporaryPath))
            {
                try { File.Delete(temporaryPath); } catch { /* Best-effort staging cleanup. */ }
            }
        }
    }
}

internal static class NativeInstallVerifier
{
    internal static void EnsureNativeInstallSucceeded(
        int processExitCode,
        string inputPath,
        string targetPath,
        string operation,
        NativeInstallReport? report = null,
        string? reportPath = null)
    {
        if (processExitCode == 3 || report?.ExitCode == 3)
            throw new InvalidOperationException("After Effects is running. Close it and try again.");

        if (!string.IsNullOrWhiteSpace(reportPath) && report is null)
            throw new InvalidOperationException(
                $"{operation} did not return a valid diagnostic report from the elevated installer. " +
                $"Expected report: {reportPath}");

        if (report is { Succeeded: false })
            throw new InvalidOperationException(FormatFailure(operation, report, reportPath));

        // A combined native + panel command can exit 2 after the native report has
        // recorded success. Without that positive report, a nonzero exit means the
        // helper failed before a final hash comparison can say anything useful.
        if (processExitCode != 0 && report is null)
            throw new InvalidOperationException(
                $"{operation} failed in the elevated installer (exit code {processExitCode}), " +
                "but no diagnostic report was returned.");

        if (report is { Succeeded: true } &&
            (report.ExpectedSha256 is null || report.ActualSha256 is null ||
             !string.Equals(report.ExpectedSha256, report.ActualSha256, StringComparison.Ordinal)))
            throw new InvalidOperationException(
                $"{operation} returned an incomplete or inconsistent success report from the elevated installer.");

        var expectedHash = report?.ExpectedSha256 ?? OriginalDllStore.Sha256(inputPath);
        var actualHash = OriginalDllStore.Sha256(targetPath.Trim());
        if (!string.Equals(expectedHash, actualHash, StringComparison.Ordinal))
            throw new IOException(
                $"{operation} was verified by the elevated installer, but the installed DLL changed before " +
                "the final SHA-256 check. Close every Adobe application and any tool that may repair or protect " +
                "the After Effects installation, then try again.");
    }

    private static string FormatFailure(string operation, NativeInstallReport report, string? reportPath)
    {
        var rollback = report.RollbackAttempted
            ? report.RollbackSucceeded
                ? $" {report.RollbackMessage ?? "The original installed DLL was restored from the verified backup."}"
                : $" Restoring the original DLL also failed; repair After Effects before launching it. {report.RollbackMessage}"
            : " The installed DLL was not replaced.";
        var diagnostic = string.IsNullOrWhiteSpace(reportPath)
            ? string.Empty
            : $" Diagnostic report: {reportPath}";
        return $"{operation} failed during {report.Stage}: {report.Message}{rollback}{diagnostic}";
    }
}
