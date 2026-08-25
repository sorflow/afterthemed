using System.Buffers.Binary;
using System.Drawing;
using System.Diagnostics;
using System.Text;
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
        Run("rollback rejects a backup changed after verification",
            RollbackRejectsBackupChangedAfterVerification, failures);
        Run("theme file-set rolls back its first file when the second fails",
            ThemeFileSetRollsBackFirstFileWhenSecondFails, failures);
        Run("theme file-set verification preserves a later panel exit",
            ThemeFileSetVerificationPreservesLaterPanelExit, failures);
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
        Run("hybrid Spectrum JSON and native theme engines patch together",
            HybridSpectrumAndNativeThemeEnginesPatchTogether, failures);
        Run("legacy native color loads accept DVA register and AVX encodings",
            LegacyNativeColorLoadsAcceptDvaEncodings, failures);
        Run("After Effects 2020 companion XML maps native semantic colors",
            Ae2020CompanionXmlMapsNativeSemanticColors, failures);
        Run("foreground roles contrast with the surface they sit on",
            ForegroundRolesContrastWithTheSurfaceTheySitOn, failures);
        Run("companion selection follows resources, not the reported version",
            CompanionSelectionFollowsResourcesNotVersion, failures);
        Run("a themed companion is never preserved as an Adobe original",
            ThemedCompanionIsNeverPreservedAsAnOriginal, failures);
        Run("installer upgrade guard matches the application mutex",
            InstallerUpgradeGuardMatchesApplicationMutex, failures);
        Run("an imported dark palette keeps its own dark surfaces",
            ImportedDarkPaletteKeepsItsDarkSurfaces, failures);
        Run("an imported light palette keeps its own light surfaces",
            ImportedLightPaletteKeepsItsLightSurfaces, failures);

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

    private static void RollbackRejectsBackupChangedAfterVerification()
    {
        var root = NewTempDirectory("installer-tampered-backup");
        try
        {
            var input = Path.Combine(root, "generated.dll");
            var target = Path.Combine(root, "dvaui.dll");
            var backups = Path.Combine(root, "Backups");
            File.WriteAllText(input, "generated");
            File.WriteAllText(target, "original");

            var report = NativeDllInstaller.Install(input, target, backups,
                requireAfterEffectsClosed: false, new CorruptingAndTamperingBackupCommitter(backups));

            Require(report.ExitCode == 2, $"expected exit 2, got {report.ExitCode}");
            Require(report.Stage == "final verification", $"original failure was lost: {report.Stage}");
            Require(report.RollbackAttempted, "rollback attempt was not recorded");
            Require(!report.RollbackSucceeded, "tampered backup was incorrectly accepted for rollback");
            Require(report.RollbackMessage?.Contains("did not match the verified backup",
                    StringComparison.OrdinalIgnoreCase) == true,
                $"backup tampering was not reported: {report.RollbackMessage}");
            Require(File.ReadAllText(target) == "corrupted after commit",
                "tampered backup was copied over the installed target");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static void ThemeFileSetRollsBackFirstFileWhenSecondFails()
    {
        var root = NewTempDirectory("file-set-rollback");
        try
        {
            var backups = Path.Combine(root, "Backups");
            var firstInput = Path.Combine(root, "AfterFXLib.generated.dll");
            var firstTarget = Path.Combine(root, "AfterFXLib.dll");
            var secondInput = Path.Combine(root, "dvaui.generated.dll");
            var secondTarget = Path.Combine(root, "dvaui.dll");
            File.WriteAllText(firstInput, "themed companion");
            File.WriteAllText(firstTarget, "original companion");
            File.WriteAllText(secondInput, "themed native");
            File.WriteAllText(secondTarget, "original native");
            var manifest = new ThemeFileSetManifest(backups,
            [
                new ThemeFileInstall(firstInput, firstTarget),
                new ThemeFileInstall(secondInput, secondTarget)
            ]);
            var call = 0;

            var report = ThemeFileSetInstaller.Install(manifest, requireAfterEffectsClosed: false, file =>
            {
                call++;
                return call == 1
                    ? NativeDllInstaller.Install(file.InputPath, file.TargetPath, backups,
                        requireAfterEffectsClosed: false)
                    : new NativeInstallReport(2, "simulated second install", "simulated failure");
            });

            Require(!report.Succeeded, "the failed second file was reported as a successful file set");
            Require(File.ReadAllText(firstTarget) == "original companion",
                "the first file was not rolled back after the second failed");
            Require(File.ReadAllText(secondTarget) == "original native",
                "the failed second install changed its target");
            Require(report.Files[0].Rollback?.Succeeded == true,
                $"the first-file rollback was not recorded: {report.Files[0].Rollback?.Message}");
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

    private static void ThemeFileSetVerificationPreservesLaterPanelExit()
    {
        var root = NewTempDirectory("file-set-panel-exit");
        try
        {
            var generated = Path.Combine(root, "dvaui.generated.dll");
            var installed = Path.Combine(root, "dvaui.dll");
            File.WriteAllText(generated, "themed native");
            File.Copy(generated, installed);
            var file = new ThemeFileInstall(generated, installed);
            var manifest = new ThemeFileSetManifest(Path.Combine(root, "Backups"), [file]);
            var nativeReport = new NativeInstallReport(0, "completed", "Installed and verified.",
                ExpectedSha256: OriginalDllStore.Sha256(generated),
                ActualSha256: OriginalDllStore.Sha256(installed));
            var report = new ThemeFileSetReport(0, "completed", "Installed and verified 1 theme file.",
                [new ThemeFileInstallResult(file, nativeReport)]);

            ThemeFileSetVerifier.EnsureSucceeded(8, manifest, report,
                Path.Combine(root, "theme-file-set-result.json"), "Installation");
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

    private static void LegacyNativeColorLoadsAcceptDvaEncodings()
    {
        Require(ThemePatcher.RipRelativeColorLoadLength([0x0F, 0x10, 0x35, 0, 0, 0, 0]) == 7,
            "DVA 14.6's non-xmm0 SSE color load was not recognized");
        Require(ThemePatcher.RipRelativeColorLoadLength([0x0F, 0x28, 0x05, 0, 0, 0, 0]) == 7,
            "the legacy movaps color load was not recognized");
        Require(ThemePatcher.RipRelativeColorLoadLength([0x0F, 0x6F, 0x3D, 0, 0, 0, 0]) == 0,
            "an unprefixed MMX load was incorrectly accepted as a 16-byte color reference");
        Require(ThemePatcher.RipRelativeColorLoadLength([0xC5, 0xFA, 0x6F, 0x0D, 0, 0, 0, 0]) == 8,
            "current DVA's AVX color load was not recognized");
        Require(ThemePatcher.RipRelativeColorLoadLength([0xC5, 0xFE, 0x6F, 0x0D, 0, 0, 0, 0]) == 0,
            "a 256-bit AVX load was incorrectly accepted as a 16-byte color reference");
        Require(ThemePatcher.RipRelativeColorLoadLength([0x0F, 0x10, 0xC0, 0, 0, 0, 0]) == 0,
            "a register-only SSE instruction was incorrectly accepted as a color reference");
    }

    private static void Ae2020CompanionXmlMapsNativeSemanticColors()
    {
        const string xml = """
            <?xml version="1.0"?><ThemeColors>
              <!-- formatting space must be reclaimable inside fixed-size PE resources -->
              <KeyFrame name="&amp;kColor_ApplicationBackground;" v="0.10" />
              <KeyFrame name="&amp;kColor_ContentBackground;" v="0.20" />
              <KeyFrame name="&amp;kColor_Focus;" h="200" s="0.75" v="0.80" />
              <KeyFrame name="&amp;kColor_StaticTextNormal;" v="0.60" />
              <KeyFrame name="&amp;kColor_TextEditBackgroundFocused;" v="0.60" />
              <KeyFrame name="&amp;kColor_TextEditTextFocused;" v="0.60" />
              <KeyFrame name="&amp;kColor_ButtonSelectedInnerFillStartGradient;" v="0.60" />
              <KeyFrame name="&amp;kColor_ButtonSelectedText;" v="0.60" />
              <KeyFrame name="&amp;kAEColor_LabelColor_Red;" h="0" s="1" v="1" />
            </ThemeColors>
            """;

        var rewritten = LegacyAeThemePatcher.RewriteThemeXml(
            xml, ThemeSettings.HatsuneMikuAccessible, out var changed);

        Require(changed == 8, $"expected eight native UI colors, got {changed}");
        Require(rewritten.Contains(
                "name=\"&amp;kColor_ApplicationBackground;\" h=\"195\" s=\"0.205128\" v=\"0.152941\"",
                StringComparison.Ordinal),
            "the application background was not mapped to the requested background role");
        Require(rewritten.Contains(
                "name=\"&amp;kColor_ContentBackground;\" h=\"189.230769\" s=\"0.265306\" v=\"0.192157\"",
                StringComparison.Ordinal),
            "the content background was not mapped to the requested panel role");
        Require(rewritten.Contains(
                "name=\"&amp;kColor_Focus;\" h=\"177.5\" s=\"0.349515\" v=\"0.807843\"",
                StringComparison.Ordinal),
            "the focus color was not mapped to the requested primary role");
        Require(rewritten.Contains(
                "name=\"&amp;kColor_StaticTextNormal;\" h=\"208.421053\" s=\"0.090909\" v=\"0.819608\"",
                StringComparison.Ordinal),
            "the static text color was not mapped to the requested text role");
        Require(rewritten.Contains(
                "name=\"&amp;kColor_TextEditBackgroundFocused;\" h=\"177.5\" s=\"0.349515\" v=\"0.807843\"",
                StringComparison.Ordinal),
            "the focused text-edit background was not mapped to the requested primary role");
        Require(rewritten.Contains(
                "name=\"&amp;kColor_TextEditTextFocused;\" h=\"195\" s=\"0.205128\" v=\"0.152941\"",
                StringComparison.Ordinal),
            "focused text was flattened into its primary background instead of a contrasting foreground");
        Require(rewritten.Contains(
                "name=\"&amp;kColor_ButtonSelectedInnerFillStartGradient;\" h=\"177.5\" s=\"0.349515\" v=\"0.807843\"",
                StringComparison.Ordinal),
            "the selected-button fill was not mapped to the requested primary role");
        Require(rewritten.Contains(
                "name=\"&amp;kColor_ButtonSelectedText;\" h=\"195\" s=\"0.205128\" v=\"0.152941\"",
                StringComparison.Ordinal),
            "selected-button text was flattened into its primary fill instead of a contrasting foreground");
        Require(rewritten.Contains(
                "name=\"&amp;kAEColor_LabelColor_Red;\" h=\"0\" s=\"1\" v=\"1\"",
                StringComparison.Ordinal),
            "document label colors were incorrectly rewritten as interface colors");
        Require(!rewritten.Contains("<!--", StringComparison.Ordinal),
            "fixed-size resource compaction did not remove formatting comments");
    }

    private static void HybridSpectrumAndNativeThemeEnginesPatchTogether()
    {
        foreach (var (name, useAvx) in new[] { ("SSE", false), ("AVX", true) })
        {
            var fixture = CreateHybridDvauiFixture(useAvx);
            var output = ThemePatcher.GenerateForTesting(fixture.Data, 14, $"14.6-test-{name}",
                ThemeSettings.MaterialLavenderRich);

            Require(fixture.Data.Length == output.Length, $"{name}: PE size changed");
            Require(Math.Abs(BitConverter.ToSingle(output, fixture.NativeColorOffset) -
                             ThemeSettings.MaterialLavenderRich.Background.R / 255f) < .000001f,
                $"{name}: native red channel was not patched");
            Require(Math.Abs(BitConverter.ToSingle(output, fixture.NativeColorOffset + 4) -
                             ThemeSettings.MaterialLavenderRich.Background.G / 255f) < .000001f,
                $"{name}: native green channel was not patched");
            Require(Math.Abs(BitConverter.ToSingle(output, fixture.NativeColorOffset + 8) -
                             ThemeSettings.MaterialLavenderRich.Background.B / 255f) < .000001f,
                $"{name}: native blue channel was not patched");
            Require(Math.Abs(BitConverter.ToSingle(fixture.Data, fixture.NativeColorOffset) - 38f / 255f) <
                    .000001f,
                $"{name}: the source fixture was mutated");

            var json = Encoding.UTF8.GetString(output, fixture.JsonOffset, fixture.JsonSize);
            var mapped = $"rgb({ThemeSettings.MaterialLavenderRich.Background.R}, " +
                         $"{ThemeSettings.MaterialLavenderRich.Background.G}, " +
                         $"{ThemeSettings.MaterialLavenderRich.Background.B})";
            Require(json.Split(mapped, StringSplitOptions.None).Length - 1 == 8,
                $"{name}: Spectrum JSON colors were not patched alongside the native color");
            Require(!json.Contains("rgb(38, 38, 38)", StringComparison.Ordinal),
                $"{name}: original Spectrum JSON colors remain");
        }
    }

    private static HybridDvauiFixture CreateHybridDvauiFixture(bool useAvx)
    {
        const int peOffset = 0x80;
        const int optionalHeader = peOffset + 24;
        const int sectionTable = optionalHeader + 0xF0;
        const int functionOffset = 0x500;
        const uint functionRva = 0x1100;
        const int resourceBase = 0x800;
        const int jsonOffset = 0xA00;
        const int jsonSize = 0x300;
        const int nativeColorOffset = 0xE00;
        const uint nativeColorRva = 0x4000;

        var data = new byte[0x1000];
        data[0] = (byte)'M';
        data[1] = (byte)'Z';
        WriteInt32(data, 0x3C, peOffset);
        WriteUInt32(data, peOffset, 0x00004550);
        WriteUInt16(data, peOffset + 4, 0x8664);
        WriteUInt16(data, peOffset + 6, 4);
        WriteUInt16(data, peOffset + 20, 0xF0);
        WriteUInt16(data, optionalHeader, 0x20B);
        WriteUInt64(data, optionalHeader + 24, 0x0000000180000000);
        WriteUInt32(data, optionalHeader + 108, 16);
        WriteUInt32(data, optionalHeader + 112, 0x2000);
        WriteUInt32(data, optionalHeader + 116, 0x100);
        WriteUInt32(data, optionalHeader + 128, 0x3000);
        WriteUInt32(data, optionalHeader + 132, 0x600);

        WriteSection(data, sectionTable, 0, ".text", 0x1000, 0x400, 0x200, 0x60000020);
        WriteSection(data, sectionTable, 1, ".rdata", 0x2000, 0x600, 0x200, 0x40000040);
        WriteSection(data, sectionTable, 2, ".rsrc", 0x3000, 0x800, 0x600, 0x40000040);
        WriteSection(data, sectionTable, 3, ".data", 0x4000, 0xE00, 0x200, 0xC0000040);

        WriteUInt32(data, 0x600 + 16, 1);
        WriteUInt32(data, 0x600 + 20, 1);
        WriteUInt32(data, 0x600 + 24, 1);
        WriteUInt32(data, 0x600 + 28, 0x2040);
        WriteUInt32(data, 0x600 + 32, 0x2048);
        WriteUInt32(data, 0x600 + 36, 0x2050);
        WriteUInt32(data, 0x640, functionRva);
        WriteUInt32(data, 0x648, 0x2060);
        WriteUInt16(data, 0x650, 0);
        Encoding.ASCII.GetBytes("?InitializeColors@Theme@ui@dvaui@@QEAAXXZ\0").CopyTo(data, 0x660);

        var instructionLength = useAvx ? 8 : 7;
        if (useAvx)
            new byte[] { 0xC5, 0xFA, 0x6F, 0x0D }.CopyTo(data, functionOffset);
        else
            new byte[] { 0x0F, 0x10, 0x35 }.CopyTo(data, functionOffset);
        WriteInt32(data, functionOffset + instructionLength - 4,
            checked((int)(nativeColorRva - (functionRva + instructionLength))));
        data[functionOffset + instructionLength] = 0xC3;

        WriteUInt16(data, resourceBase + 12, 1);
        WriteUInt32(data, resourceBase + 16, 0x80000100);
        WriteUInt32(data, resourceBase + 20, 0x80000020);
        WriteUInt16(data, resourceBase + 0x20 + 12, 1);
        WriteUInt32(data, resourceBase + 0x30, 0x80000110);
        WriteUInt32(data, resourceBase + 0x34, 0x80000040);
        WriteUInt16(data, resourceBase + 0x40 + 14, 1);
        WriteUInt32(data, resourceBase + 0x50, 1033);
        WriteUInt32(data, resourceBase + 0x54, 0x60);
        WriteUInt32(data, resourceBase + 0x60, 0x3200);
        WriteUInt32(data, resourceBase + 0x64, jsonSize);
        WriteResourceString(data, resourceBase + 0x100, "JSON");
        WriteResourceString(data, resourceBase + 0x110, "DNA-VARS");

        data.AsSpan(jsonOffset, jsonSize).Fill((byte)' ');
        var properties = Enumerable.Range(0, 8)
            .Select(index => $"\"spectrum-test-color-{index}\":\"rgb(38, 38, 38)\"");
        Encoding.UTF8.GetBytes("{" + string.Join(',', properties) + "}").CopyTo(data, jsonOffset);
        WriteSingle(data, nativeColorOffset, 38f / 255f);
        WriteSingle(data, nativeColorOffset + 4, 38f / 255f);
        WriteSingle(data, nativeColorOffset + 8, 38f / 255f);
        WriteSingle(data, nativeColorOffset + 12, 1f);
        return new HybridDvauiFixture(data, nativeColorOffset, jsonOffset, jsonSize);
    }

    /// <summary>
    /// Builds a minimal AfterFXLib.dll-shaped PE whose XML resources carry the named native color
    /// themes, optionally padded the way theming leaves them.
    /// </summary>
    private static void ForegroundRolesContrastWithTheSurfaceTheySitOn()
    {
        // A light raised surface with light UI text: the foreground has to follow the control's
        // own face, not the theme's text role, and not the shadow drawn behind the control.
        var settings = ThemeSettings.HatsuneMikuAccessible with
        {
            Background = ColorTranslator.FromHtml("#5A0A14"),
            Panel = ColorTranslator.FromHtml("#7A0F1E"),
            Raised = ColorTranslator.FromHtml("#FFE000"),
            Primary = ColorTranslator.FromHtml("#FFE000"),
            Text = ColorTranslator.FromHtml("#FFFFFF")
        };

        const string xml = """
            <?xml version="1.0"?><ThemeColors>
              <KeyFrame name="&amp;kColor_ButtonNormalDownInnerFillStartGradient;" v="0.20" />
              <KeyFrame name="&amp;kColor_ButtonNormalDownTopShadowFill;" v="0.20" />
              <KeyFrame name="&amp;kColor_ButtonNormalDownTextColor;" v="0.60" />
              <KeyFrame name="&amp;kColor_ApplicationBackground;" v="0.10" />
              <KeyFrame name="&amp;kColor_ContentBackground;" v="0.20" />
              <KeyFrame name="&amp;kColor_Focus;" v="0.80" />
              <KeyFrame name="&amp;kColor_StaticTextNormal;" v="0.60" />
              <KeyFrame name="&amp;kColor_TextEditBackgroundFocused;" v="0.60" />
              <KeyFrame name="&amp;kColor_TextEditTextFocused;" v="0.60" />
              <KeyFrame name="&amp;kColor_ButtonSelectedText;" v="0.60" />
            </ThemeColors>
            """;

        var rewritten = LegacyAeThemePatcher.RewriteThemeXml(xml, settings, out _);

        // #5A0A14 is the dark background role, the readable choice against a #FFE000 face.
        Require(rewritten.Contains(
                "name=\"&amp;kColor_ButtonNormalDownTextColor;\" h=\"352.5\" s=\"0.888889\" v=\"0.352941\"",
                StringComparison.Ordinal),
            "button text was not made readable against the button's own light face");

        // Unpaired body text still follows the panel it sits on.
        Require(rewritten.Contains(
                "name=\"&amp;kColor_StaticTextNormal;\" h=\"0\" s=\"0\" v=\"1\"",
                StringComparison.Ordinal),
            "body text on a dark panel stopped using the light text role");
    }

    private static void ImportedDarkPaletteKeepsItsDarkSurfaces()
    {
        // Nord. Its surfaces are dark but faintly blue, which an HSV saturation reading
        // scores as .28 and rejects as an accent. Every surface was then discarded and
        // the theme came back rebuilt from its text colors: a light background, a purple
        // body text, and the darkest surface handed back as the primary accent.
        var nord = new[]
        {
            "#2E3440", "#3B4252", "#434C5E", "#4C566A", "#D8DEE9", "#E5E9F0", "#ECEFF4",
            "#8FBCBB", "#88C0D0", "#81A1C1", "#5E81AC", "#BF616A", "#D08770", "#EBCB8B",
            "#A3BE8C", "#B48EAD"
        }.Select(ColorTranslator.FromHtml).ToArray();

        var suggested = ThemeImporter.Suggest("nord", nord);

        Require(suggested.Background == ColorTranslator.FromHtml("#2E3440"),
            $"the darkest Nord surface was not used as the background; got {suggested.Background}");
        Require(suggested.Panel == ColorTranslator.FromHtml("#3B4252"),
            $"the Nord panel surface was not the next shade up; got {suggested.Panel}");
        Require(suggested.Text == ColorTranslator.FromHtml("#ECEFF4"),
            $"Nord's body text was not its lightest neutral; got {suggested.Text}");
        Require(suggested.Primary != suggested.Background && suggested.Secondary != suggested.Background,
            "a background surface was handed back as an accent");
        Require(suggested.Danger == ColorTranslator.FromHtml("#BF616A"),
            $"Nord's red was not chosen for the danger role; got {suggested.Danger}");
    }

    private static void ImportedLightPaletteKeepsItsLightSurfaces()
    {
        var solarized = new[]
        {
            "#002B36", "#073642", "#586E75", "#657B83", "#839496", "#93A1A1", "#EEE8D5", "#FDF6E3",
            "#B58900", "#CB4B16", "#DC322F", "#D33682", "#6C71C4", "#268BD2", "#2AA198", "#859900"
        }.Select(ColorTranslator.FromHtml).ToArray();

        var suggested = ThemeImporter.Suggest("solarized-light", solarized);

        Require(suggested.Background == ColorTranslator.FromHtml("#FDF6E3"),
            $"the lightest Solarized surface was not used as the background; got {suggested.Background}");
        Require(suggested.Panel == ColorTranslator.FromHtml("#EEE8D5"),
            $"the Solarized panel surface was not the next shade down; got {suggested.Panel}");
        Require(suggested.Text.GetBrightness() < suggested.Background.GetBrightness(),
            "body text on a light palette was not darker than its background");
        Require(suggested.Danger == ColorTranslator.FromHtml("#DC322F"),
            $"Solarized's red was not chosen for the danger role; got {suggested.Danger}");
    }

    private static readonly string[] CompanionResourceNames =
        ["AECOLORTHEMES", "DVACOLORTHEMESV2", "DVACOLORTHEMESV4", "DVACOLORTHEMESV5"];

    private static void CompanionSelectionFollowsResourcesNotVersion()
    {
        var root = Path.Combine(Path.GetTempPath(), $"afterthemed-regression-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            // After Effects stamps dvaui.dll with the application version in some releases and the
            // DVA version in others, so selection has to follow the resources the companion carries.
            var complete = Path.Combine(root, "AfterFXLib.dll");
            File.WriteAllBytes(complete, CreateCompanionFixture(CompanionResourceNames, themed: false));
            Require(LegacyAeThemePatcher.HasNativeThemeResources(complete),
                "a companion carrying every native color theme was not recognized");

            var partial = Path.Combine(root, "Partial.dll");
            File.WriteAllBytes(partial, CreateCompanionFixture(
                ["AECOLORTHEMES", "DVACOLORTHEMESV2"], themed: false));
            Require(!LegacyAeThemePatcher.HasNativeThemeResources(partial),
                "a companion missing native color themes was treated as themeable");

            var unrelated = Path.Combine(root, "Modern.dll");
            File.WriteAllBytes(unrelated, CreateCompanionFixture(["SPECTRUM"], themed: false));
            Require(!LegacyAeThemePatcher.HasNativeThemeResources(unrelated),
                "a modern companion without legacy color themes was treated as themeable");

            Require(!LegacyAeThemePatcher.HasNativeThemeResources(Path.Combine(root, "Absent.dll")),
                "a missing companion was treated as themeable");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static void ThemedCompanionIsNeverPreservedAsAnOriginal()
    {
        var root = Path.Combine(Path.GetTempPath(), $"afterthemed-regression-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            // Companion originals are accepted on their embedded Adobe signer alone, so an already
            // themed companion must never be captured as if it were Adobe's original.
            var pristine = Path.Combine(root, "Pristine.dll");
            File.WriteAllBytes(pristine, CreateCompanionFixture(CompanionResourceNames, themed: false));
            Require(!LegacyAeThemePatcher.IsAlreadyThemed(pristine),
                "an untouched companion was mistaken for a themed one");

            var themed = Path.Combine(root, "Themed.dll");
            File.WriteAllBytes(themed, CreateCompanionFixture(CompanionResourceNames, themed: true));
            Require(LegacyAeThemePatcher.IsAlreadyThemed(themed),
                "a themed companion would have been preserved as an Adobe original");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    /// <summary>
    /// Builds a minimal AfterFXLib.dll-shaped PE whose XML resources carry the named native color
    /// themes, optionally padded the way theming leaves them.
    /// </summary>
    private static byte[] CreateCompanionFixture(string[] resourceNames, bool themed)
    {
        const int peOffset = 0x80;
        const int optionalHeader = peOffset + 24;
        const int sectionTable = optionalHeader + 0xF0;
        const int resourceBase = 0x800;
        const uint resourceRva = 0x3000;

        var data = new byte[0x2000];
        data[0] = (byte)'M';
        data[1] = (byte)'Z';
        WriteInt32(data, 0x3C, peOffset);
        WriteUInt32(data, peOffset, 0x00004550);
        WriteUInt16(data, peOffset + 4, 0x8664);
        WriteUInt16(data, peOffset + 6, 2);
        WriteUInt16(data, peOffset + 20, 0xF0);
        WriteUInt16(data, optionalHeader, 0x20B);
        WriteUInt64(data, optionalHeader + 24, 0x0000000180000000);
        WriteUInt32(data, optionalHeader + 128, resourceRva);
        WriteUInt32(data, optionalHeader + 132, 0xC00);

        WriteSection(data, sectionTable, 0, ".text", 0x1000, 0x400, 0x200, 0x60000020);
        WriteSection(data, sectionTable, 1, ".rsrc", resourceRva, (uint)resourceBase, 0xC00, 0x40000040);

        // Type directory: one named "XML" entry pointing at the name directory.
        WriteUInt16(data, resourceBase + 12, 1);
        WriteUInt32(data, resourceBase + 16, 0x80000100);
        WriteUInt32(data, resourceBase + 20, 0x80000020);
        WriteResourceString(data, resourceBase + 0x100, "XML");

        WriteUInt16(data, resourceBase + 0x20 + 12, checked((ushort)resourceNames.Length));
        for (var index = 0; index < resourceNames.Length; index++)
        {
            var nameString = 0x120 + index * 0x30;
            var languageDirectory = 0x60 + index * 0x20;
            var dataEntry = 0x200 + index * 0x10;
            var payload = 0x400 + index * 0x200;

            WriteResourceString(data, resourceBase + nameString, resourceNames[index]);
            WriteUInt32(data, resourceBase + 0x30 + index * 8, 0x80000000u | (uint)nameString);
            WriteUInt32(data, resourceBase + 0x34 + index * 8, 0x80000000u | (uint)languageDirectory);

            // Language directory: a single 1033 entry pointing at the data entry.
            WriteUInt16(data, resourceBase + languageDirectory + 14, 1);
            WriteUInt32(data, resourceBase + languageDirectory + 16, 1033);
            WriteUInt32(data, resourceBase + languageDirectory + 20, (uint)dataEntry);

            WriteUInt32(data, resourceBase + dataEntry, resourceRva + (uint)payload);
            WriteUInt32(data, resourceBase + dataEntry + 4, 0x200);

            var xml = Encoding.UTF8.GetBytes(
                "<?xml version=\"1.0\"?><ThemeColors>" +
                "<KeyFrame name=\"&amp;kColor_ApplicationBackground;\" v=\"0.10\" />" +
                "</ThemeColors>");
            var payloadOffset = resourceBase + payload;
            if (themed)
            {
                // Theming minifies the XML and reclaims the remainder as padding.
                data.AsSpan(payloadOffset, 0x200).Fill((byte)' ');
                xml.CopyTo(data, payloadOffset);
            }
            else
            {
                xml.CopyTo(data, payloadOffset);
                data.AsSpan(payloadOffset + xml.Length, 0x200 - xml.Length).Fill((byte)'\n');
            }
        }

        return data;
    }

    private static void WriteSection(byte[] data, int sectionTable, int index, string name, uint rva,
        uint rawOffset, uint rawSize, uint characteristics)
    {
        var offset = sectionTable + index * 40;
        Encoding.ASCII.GetBytes(name).CopyTo(data, offset);
        WriteUInt32(data, offset + 8, rawSize);
        WriteUInt32(data, offset + 12, rva);
        WriteUInt32(data, offset + 16, rawSize);
        WriteUInt32(data, offset + 20, rawOffset);
        WriteUInt32(data, offset + 36, characteristics);
    }

    private static void WriteResourceString(byte[] data, int offset, string value)
    {
        WriteUInt16(data, offset, checked((ushort)value.Length));
        Encoding.Unicode.GetBytes(value).CopyTo(data, offset + 2);
    }

    private static void WriteSingle(byte[] data, int offset, float value) =>
        WriteInt32(data, offset, BitConverter.SingleToInt32Bits(value));

    private static void WriteUInt16(byte[] data, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset, 2), value);

    private static void WriteUInt32(byte[] data, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset, 4), value);

    private static void WriteUInt64(byte[] data, int offset, ulong value) =>
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(offset, 8), value);

    private static void WriteInt32(byte[] data, int offset, int value) =>
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset, 4), value);

    private static void InstallerUpgradeGuardMatchesApplicationMutex()
    {
        var installerScript = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "AfterThemed.iss"));
        Require(installerScript.Contains($"#define MyAppMutex \"{ApplicationLifetime.UpgradeMutexName}\"",
                StringComparison.Ordinal),
            "the installer and application use different default upgrade mutex names");
        Require(installerScript.Contains("AppMutex={#MyAppMutex}", StringComparison.Ordinal),
            "the installer does not enforce its configured upgrade mutex");
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

    private sealed class CorruptingAndTamperingBackupCommitter(string backupDirectory)
        : NativeDllInstaller.IAtomicCommitter
    {
        public void Replace(string stagedPath, string targetPath)
        {
            File.Move(stagedPath, targetPath, true);
            File.WriteAllText(targetPath, "corrupted after commit");
            File.WriteAllText(Directory.EnumerateFiles(backupDirectory, "dvaui-*.dll").Single(),
                "tampered backup");
        }
    }

    private sealed record HybridDvauiFixture(byte[] Data, int NativeColorOffset, int JsonOffset, int JsonSize);
}
