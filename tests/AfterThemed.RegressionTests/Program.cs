using System.Diagnostics;
using System.Text.Json;
using DvauiThemeEditor;

namespace AfterThemed.RegressionTests;

internal static class Program
{
    private static int Main()
    {
        var failures = new List<string>();
        Run("native installer failure is not reported as a final hash failure",
            NativeInstallerFailureIsNotReportedAsFinalHashFailure, failures);
        Run("native installer reports the failing stage",
            NativeInstallerReportsTheFailingStage, failures);
        Run("native installer success is backed up and verified",
            NativeInstallerSuccessIsBackedUpAndVerified, failures);
        Run("post-commit verification failure restores the original",
            PostCommitVerificationFailureRestoresTheOriginal, failures);
        Run("rollback failure retains both failures and the backup path",
            RollbackFailureRetainsBothFailuresAndTheBackupPath, failures);
        Run("native install reports round-trip through JSON",
            NativeInstallReportsRoundTripThroughJson, failures);
        Run("a requested native install report is mandatory",
            RequestedNativeInstallReportIsMandatory, failures);
        Run("an unwritable report path prevents target mutation",
            UnwritableReportPathPreventsTargetMutation, failures);
        Run("an existing report destination prevents target mutation",
            ExistingReportDestinationPreventsTargetMutation, failures);
        Run("panel failure after native success preserves the native result",
            PanelFailureAfterNativeSuccessPreservesNativeResult, failures);
        Run("a post-success target change remains a hash failure",
            PostSuccessTargetChangeRemainsAHashFailure, failures);
        Run("historical snapshot must match the current target version",
            HistoricalSnapshotMustMatchCurrentTargetVersion, failures);
        Run("newest same-version snapshot wins after an Adobe hotfix",
            NewestSameVersionSnapshotWinsAfterAdobeHotfix, failures);
        Run("active snapshot provenance overrides capture recency",
            ActiveSnapshotProvenanceOverridesCaptureRecency, failures);
        Run("restore captures a signed same-version hotfix before selecting an original",
            RestoreCapturesSameVersionHotfixBeforeSelectingOriginal, failures);
        Run("legacy and current DROVER resource names are recognized",
            LegacyAndCurrentDroverResourceNamesAreRecognized, failures);
        Run("installer upgrade guard matches the application mutex",
            InstallerUpgradeGuardMatchesApplicationMutex, failures);

        foreach (var failure in failures) Console.Error.WriteLine($"FAIL: {failure}");
        if (failures.Count != 0) return 1;

        Console.WriteLine("PASS: all AfterThemed regression tests");
        return 0;
    }

    private static void NativeInstallerFailureIsNotReportedAsFinalHashFailure()
    {
        var root = Path.Combine(Path.GetTempPath(), $"afterthemed-regression-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var input = Path.Combine(root, "generated.dll");
            var target = Path.Combine(root, "dvaui.dll");
            File.WriteAllText(input, "generated");
            File.WriteAllText(target, "original");
            var report = new NativeInstallReport(2, "prepare-backup", "The backup path is not a directory.");

            var exception = Capture(() =>
                NativeInstallVerifier.EnsureNativeInstallSucceeded(2, input, target, "Installation", report));

            Require(exception is not null, "expected verification to reject the failed install");
            Require(!exception!.Message.Contains("final SHA-256 verification", StringComparison.Ordinal),
                $"misleading dialog survived: {exception.Message}");
            Require(exception.Message.Contains("backup", StringComparison.OrdinalIgnoreCase),
                $"failure stage was lost: {exception.Message}");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static void NativeInstallerReportsTheFailingStage()
    {
        var root = NewTempDirectory("installer-stage");
        try
        {
            var input = Path.Combine(root, "generated.dll");
            var target = Path.Combine(root, "dvaui.dll");
            var invalidBackupDirectory = Path.Combine(root, "Backups");
            File.WriteAllText(input, "generated");
            File.WriteAllText(target, "original");
            File.WriteAllText(invalidBackupDirectory, "not a directory");

            var report = NativeDllInstaller.Install(input, target, invalidBackupDirectory,
                requireAfterEffectsClosed: false);

            Require(report.ExitCode == 2, $"expected exit 2, got {report.ExitCode}");
            Require(report.Stage == "backup preparation", $"unexpected failure stage: {report.Stage}");
            Require(File.ReadAllText(target) == "original", "failed install changed the target");
            Require(!report.RollbackAttempted, "rollback ran before replacement was attempted");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static void NativeInstallerSuccessIsBackedUpAndVerified()
    {
        var root = NewTempDirectory("installer-success");
        try
        {
            var input = Path.Combine(root, "generated.dll");
            var target = Path.Combine(root, "dvaui.dll");
            var backups = Path.Combine(root, "Backups");
            File.WriteAllText(input, "generated");
            File.WriteAllText(target, "original");

            var report = NativeDllInstaller.Install(input, target, backups,
                requireAfterEffectsClosed: false);

            Require(report.Succeeded, $"install failed during {report.Stage}: {report.Message}");
            Require(OriginalDllStore.Sha256(input) == OriginalDllStore.Sha256(target),
                "installed target does not match the generated source");
            var backup = Directory.EnumerateFiles(backups, "dvaui-*.dll").Single();
            Require(File.ReadAllText(backup) == "original", "backup does not contain the original target");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static void PostCommitVerificationFailureRestoresTheOriginal()
    {
        var root = NewTempDirectory("installer-rollback");
        try
        {
            var input = Path.Combine(root, "generated.dll");
            var target = Path.Combine(root, "dvaui.dll");
            var backups = Path.Combine(root, "Backups");
            File.WriteAllText(input, "generated");
            File.WriteAllText(target, "original");

            var report = NativeDllInstaller.Install(input, target, backups,
                requireAfterEffectsClosed: false, new CorruptingCommitter());

            Require(report.ExitCode == 2, $"expected exit 2, got {report.ExitCode}");
            Require(report.Stage == "final verification", $"unexpected failure stage: {report.Stage}");
            Require(report.RollbackAttempted, "rollback was not attempted after a committed replacement");
            Require(report.RollbackSucceeded, $"rollback failed: {report.RollbackMessage}");
            Require(File.ReadAllText(target) == "original", "rollback did not restore the original target");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static void RollbackFailureRetainsBothFailuresAndTheBackupPath()
    {
        var root = NewTempDirectory("installer-rollback-failure");
        try
        {
            var input = Path.Combine(root, "generated.dll");
            var target = Path.Combine(root, "dvaui.dll");
            var backups = Path.Combine(root, "Backups");
            File.WriteAllText(input, "generated");
            File.WriteAllText(target, "original");

            var report = NativeDllInstaller.Install(input, target, backups,
                requireAfterEffectsClosed: false, new CorruptingAndDeletingBackupCommitter(backups));

            Require(report.ExitCode == 2, $"expected exit 2, got {report.ExitCode}");
            Require(report.Stage == "final verification", $"original failure was lost: {report.Stage}");
            Require(report.RollbackAttempted, "rollback attempt was not recorded");
            Require(!report.RollbackSucceeded, "rollback unexpectedly succeeded after its backup was removed");
            Require(report.RollbackMessage?.Contains("unavailable", StringComparison.OrdinalIgnoreCase) == true,
                $"rollback failure was lost: {report.RollbackMessage}");
            Require(!string.IsNullOrWhiteSpace(report.BackupPath), "verified backup path was not retained");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static void NativeInstallReportsRoundTripThroughJson()
    {
        var root = NewTempDirectory("report-json");
        try
        {
            var reportPath = Path.Combine(root, "native-install.json");
            var expected = new NativeInstallReport(2, "DLL replacement", "Access denied.",
                Path.Combine(root, "backup.dll"), true, false, unchecked((int)0x80070005), "EXPECTED", "ACTUAL");

            NativeInstallReportStore.TryWrite(reportPath, expected);
            var actual = NativeInstallReportStore.TryRead(reportPath);

            Require(actual == expected, "serialized native install report did not round-trip exactly");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static void RequestedNativeInstallReportIsMandatory()
    {
        var root = NewTempDirectory("missing-report");
        try
        {
            var input = Path.Combine(root, "generated.dll");
            var target = Path.Combine(root, "dvaui.dll");
            var reportPath = Path.Combine(root, "expected-report.json");
            File.WriteAllText(input, "generated");
            File.Copy(input, target);

            var exception = Capture(() => NativeInstallVerifier.EnsureNativeInstallSucceeded(
                0, input, target, "Installation", report: null, reportPath));

            Require(exception is InvalidOperationException,
                $"expected protocol failure, got {exception?.GetType().Name ?? "none"}");
            Require(exception!.Message.Contains("diagnostic report", StringComparison.OrdinalIgnoreCase),
                $"missing report was not identified: {exception.Message}");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static void UnwritableReportPathPreventsTargetMutation()
    {
        var root = NewTempDirectory("unwritable-report");
        try
        {
            var input = Path.Combine(root, "generated.dll");
            var target = Path.Combine(root, "dvaui.dll");
            var backups = Path.Combine(root, "Backups");
            var reportParent = Path.Combine(root, "report-parent-is-a-file");
            var reportPath = Path.Combine(reportParent, "native-install.json");
            File.WriteAllText(input, "generated");
            File.WriteAllText(target, "original");
            File.WriteAllText(reportParent, "not a directory");

            var exitCode = NativeDllInstallCommand.Run(input, target, backups, reportPath,
                requireAfterEffectsClosed: false);

            Require(exitCode == 2, $"expected exit 2, got {exitCode}");
            Require(File.ReadAllText(target) == "original", "target changed without a writable result channel");
            Require(!Directory.Exists(backups), "installer ran far enough to create a backup");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static void ExistingReportDestinationPreventsTargetMutation()
    {
        var root = NewTempDirectory("existing-report");
        try
        {
            var input = Path.Combine(root, "generated.dll");
            var target = Path.Combine(root, "dvaui.dll");
            var backups = Path.Combine(root, "Backups");
            var reportPath = Path.Combine(root, "native-install.json");
            File.WriteAllText(input, "generated");
            File.WriteAllText(target, "original");
            File.WriteAllText(reportPath, "must not be replaced");

            var exitCode = NativeDllInstallCommand.Run(input, target, backups, reportPath,
                requireAfterEffectsClosed: false);

            Require(exitCode == 2, $"expected exit 2, got {exitCode}");
            Require(File.ReadAllText(target) == "original", "target changed with an existing report destination");
            Require(File.ReadAllText(reportPath) == "must not be replaced", "existing report was overwritten");
            Require(!Directory.Exists(backups), "installer ran far enough to create a backup");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static void PanelFailureAfterNativeSuccessPreservesNativeResult()
    {
        var root = NewTempDirectory("panel-exit");
        try
        {
            var input = Path.Combine(root, "generated.dll");
            var target = Path.Combine(root, "dvaui.dll");
            File.WriteAllText(input, "generated");
            File.Copy(input, target);
            var hash = OriginalDllStore.Sha256(input);
            var report = new NativeInstallReport(0, "completed", "Installed and verified.",
                ExpectedSha256: hash, ActualSha256: hash);

            NativeInstallVerifier.EnsureNativeInstallSucceeded(2, input, target, "Installation", report);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static void PostSuccessTargetChangeRemainsAHashFailure()
    {
        var root = NewTempDirectory("post-success-change");
        try
        {
            var input = Path.Combine(root, "generated.dll");
            var target = Path.Combine(root, "dvaui.dll");
            File.WriteAllText(input, "generated");
            File.WriteAllText(target, "restored externally");
            var hash = OriginalDllStore.Sha256(input);
            var report = new NativeInstallReport(0, "completed", "Installed and verified.",
                ExpectedSha256: hash, ActualSha256: hash);

            var exception = Capture(() =>
                NativeInstallVerifier.EnsureNativeInstallSucceeded(0, input, target, "Installation", report));

            Require(exception is IOException, $"expected IOException, got {exception?.GetType().Name ?? "none"}");
            Require(exception!.Message.Contains("changed before", StringComparison.OrdinalIgnoreCase),
                $"post-success explanation was lost: {exception.Message}");
            Require(exception.Message.Contains("SHA-256", StringComparison.Ordinal),
                $"hash verification was not identified: {exception.Message}");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static void HistoricalSnapshotMustMatchCurrentTargetVersion()
    {
        var root = Path.Combine(Path.GetTempPath(), $"afterthemed-snapshot-regression-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var target = Path.Combine(root, "dvaui.dll");
            File.Copy(typeof(OriginalDllStore).Assembly.Location, target);
            var currentVersion = FileVersionInfo.GetVersionInfo(target).FileVersion ?? "unknown-version";

            var originals = Path.Combine(root, "Originals");
            var staleDirectory = Path.Combine(originals, "stale-snapshot");
            Directory.CreateDirectory(staleDirectory);
            var staleOriginal = Path.Combine(staleDirectory, "dvaui.dll.adobe-original");
            File.Copy(typeof(Program).Assembly.Location, staleOriginal);
            var staleVersion = FileVersionInfo.GetVersionInfo(staleOriginal).FileVersion ?? "unknown-version";
            File.WriteAllText(Path.Combine(staleDirectory, "snapshot.json"), JsonSerializer.Serialize(new
            {
                TargetPath = target,
                Sha256 = OriginalDllStore.Sha256(staleOriginal),
                FileVersion = staleVersion
            }));

            var selected = OriginalDllStore.ExistingFor(target, originals, requireAdobeSignature: false);
            Require(selected is null,
                $"reused snapshot version {staleVersion} for current target version {currentVersion}");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static void NewestSameVersionSnapshotWinsAfterAdobeHotfix()
    {
        var root = NewTempDirectory("same-version-snapshots");
        try
        {
            var target = Path.Combine(root, "dvaui.dll");
            File.Copy(typeof(OriginalDllStore).Assembly.Location, target);
            var originals = Path.Combine(root, "Originals");

            var oldSnapshot = CreateHistoricalSnapshot(originals, "old-snapshot", target,
                DateTimeOffset.Parse("2026-01-01T00:00:00Z"), mutateLastByte: true);
            var newSnapshot = CreateHistoricalSnapshot(originals, "new-snapshot", target,
                DateTimeOffset.Parse("2026-06-01T00:00:00Z"), mutateLastByte: false);

            var selected = OriginalDllStore.ExistingFor(target, originals, requireAdobeSignature: false);

            Require(!string.Equals(oldSnapshot, newSnapshot, StringComparison.OrdinalIgnoreCase),
                "test snapshots unexpectedly share a path");
            Require(string.Equals(selected, newSnapshot, StringComparison.OrdinalIgnoreCase),
                $"selected stale same-version snapshot: {selected ?? "none"}");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static void ActiveSnapshotProvenanceOverridesCaptureRecency()
    {
        var root = NewTempDirectory("active-snapshot");
        try
        {
            var target = Path.Combine(root, "dvaui.dll");
            File.Copy(typeof(OriginalDllStore).Assembly.Location, target);
            var originals = Path.Combine(root, "Originals");

            var olderActiveSnapshot = CreateHistoricalSnapshot(originals, "older-active", target,
                DateTimeOffset.Parse("2026-01-01T00:00:00Z"), mutateLastByte: true);
            _ = CreateHistoricalSnapshot(originals, "newer-inactive", target,
                DateTimeOffset.Parse("2026-06-01T00:00:00Z"), mutateLastByte: false);
            OriginalDllStore.MarkActiveSnapshot(target, originals, olderActiveSnapshot);

            var selected = OriginalDllStore.ExistingFor(target, originals, requireAdobeSignature: false);

            Require(string.Equals(selected, olderActiveSnapshot, StringComparison.OrdinalIgnoreCase),
                $"active snapshot provenance was ignored: {selected ?? "none"}");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static void RestoreCapturesSameVersionHotfixBeforeSelectingOriginal()
    {
        var root = NewTempDirectory("restore-hotfix");
        try
        {
            var target = Path.Combine(root, "dvaui.dll");
            var originals = Path.Combine(root, "Originals");
            var restoreOutput = Path.Combine(root, "restore", "dvaui.dll");
            File.Copy(typeof(OriginalDllStore).Assembly.Location, target);
            OriginalDllStore.AdobeSignature TrustTestFixture(string _) =>
                new("CN=Adobe Test Fixture", "TEST-THUMBPRINT");

            _ = OriginalDllStore.CaptureIfMissing(target, originals, out var initialCaptured, TrustTestFixture);
            Require(initialCaptured, "initial test original was not captured");

            using (var stream = new FileStream(target, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                stream.Position = stream.Length - 1;
                var value = stream.ReadByte();
                stream.Position = stream.Length - 1;
                stream.WriteByte((byte)(value ^ 0x01));
            }
            var hotfixHash = OriginalDllStore.Sha256(target);

            OriginalDllStore.CreateRestoreDll(target, originals, restoreOutput, TrustTestFixture);

            Require(OriginalDllStore.Sha256(restoreOutput) == hotfixHash,
                "restore selected the stale original instead of the newly installed hotfix");
            Require(Directory.EnumerateFiles(originals, "snapshot.json", SearchOption.AllDirectories).Count() == 2,
                "same-version hotfix was not preserved as a distinct snapshot");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string CreateHistoricalSnapshot(string originals, string name, string target,
        DateTimeOffset capturedAtUtc, bool mutateLastByte)
    {
        var directory = Path.Combine(originals, name);
        Directory.CreateDirectory(directory);
        var snapshot = Path.Combine(directory, "dvaui.dll.adobe-original");
        File.Copy(target, snapshot);
        if (mutateLastByte)
        {
            using var stream = new FileStream(snapshot, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            stream.Position = stream.Length - 1;
            var value = stream.ReadByte();
            stream.Position = stream.Length - 1;
            stream.WriteByte((byte)(value ^ 0x01));
        }
        var version = FileVersionInfo.GetVersionInfo(snapshot);
        File.WriteAllText(Path.Combine(directory, "snapshot.json"), JsonSerializer.Serialize(new
        {
            TargetPath = target,
            CapturedAtUtc = capturedAtUtc,
            Sha256 = OriginalDllStore.Sha256(snapshot),
            version.FileVersion
        }));
        return snapshot;
    }

    private static void LegacyAndCurrentDroverResourceNamesAreRecognized()
    {
        Require(ThemePatcher.IsSpectrumJsonResourceName("DROVER-VARS"),
            "Premiere Pro 2020's DROVER-VARS resource was not recognized");
        Require(ThemePatcher.IsSpectrumJsonResourceName("DROVER-DNA-VARS"),
            "DVA 2026's DROVER-DNA-VARS resource was not recognized");
        Require(ThemePatcher.IsSpectrumJsonResourceName("DNA-VARS-LINKED"),
            "linked Spectrum variables were not recognized");
        Require(!ThemePatcher.IsSpectrumJsonResourceName("DNA-API"),
            "a non-theme JSON resource was accepted");
    }

    private static void InstallerUpgradeGuardMatchesApplicationMutex()
    {
        var installerScript = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "AfterThemed.iss"));
        Require(installerScript.Contains($"AppMutex={ApplicationLifetime.UpgradeMutexName}",
                StringComparison.Ordinal),
            "the installer and application use different upgrade mutex names");
        Require(installerScript.Contains("ComparePackedVersion(InstalledPackedVersion, SetupPackedVersion)",
                StringComparison.Ordinal),
            "the installer does not compare semantic versions before uninstalling");
        Require(installerScript.Contains("QuietUninstallString", StringComparison.Ordinal),
            "the installer does not prefer the registered quiet uninstall command");
        Require(installerScript.Contains("RegKeyExists(InstalledRoot, AfterThemedUninstallKey)",
                StringComparison.Ordinal),
            "the installer does not verify that the previous registration was removed");
    }

    private static Exception? Capture(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static string NewTempDirectory(string name)
    {
        var root = Path.Combine(Path.GetTempPath(), $"afterthemed-{name}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Run(string name, Action test, ICollection<string> failures)
    {
        try
        {
            test();
            Console.WriteLine($"PASS: {name}");
        }
        catch (Exception exception)
        {
            failures.Add($"{name}: {exception.Message}");
        }
    }

    private sealed class CorruptingCommitter : NativeDllInstaller.IAtomicCommitter
    {
        public void Replace(string stagedPath, string targetPath)
        {
            File.Move(stagedPath, targetPath, true);
            File.WriteAllText(targetPath, "corrupted after commit");
        }
    }

    private sealed class CorruptingAndDeletingBackupCommitter(string backupDirectory)
        : NativeDllInstaller.IAtomicCommitter
    {
        public void Replace(string stagedPath, string targetPath)
        {
            File.Move(stagedPath, targetPath, true);
            File.WriteAllText(targetPath, "corrupted after commit");
            File.Delete(Directory.EnumerateFiles(backupDirectory, "dvaui-*.dll").Single());
        }
    }
}
