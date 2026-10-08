using System.Buffers.Binary;
using System.Drawing;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DvauiThemeEditor;

namespace AfterThemed.RegressionTests;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        var failures = new List<string>();
        Run("DLL theme extraction reads native and JSON colors without mutating its donor", DllPaletteExtraction, failures);
        Run("DLL theme import rejects malformed binaries", InvalidDllPaletteRejected, failures);
        Run("new originals are grouped by AE release with distinct builds and installations", ReadableOriginalCapture, failures);
        Run("legacy library organization preserves bytes and active restore provenance", OrganizeLegacyOriginals, failures);
        Run("interrupted pointer migration still selects the active original", InterruptedLibraryMigration, failures);
        Run("organization preserves conflicting and malformed backups", LibraryMigrationConflicts, failures);
        Run("organization uses the recorded AE release even when the target is missing", MissingTargetLibraryMigration, failures);
        Run("a visible rejected snapshot is not reported as missing", VisibleRejectedSnapshotIsExplained, failures);
        Run("signed backup import repairs the original store without replacing the installed DLL", ImportBackupPreservesTarget, failures);
        Run("backup import rejects a different executable build", ImportBackupRejectsDifferentBuild, failures);
        Run("backup import verifies stored bytes and rejects unsigned input", ImportBackupRejectsUnsigned, failures);
        Run("unsealed legacy snapshots are not trusted by hash alone", UnsealedSnapshotRejected, failures);
        Run("protected originals survive a deleted primary and installed DLL", ProtectedOriginalRecovery, failures);
        Run("edited metadata cannot authenticate a changed original", ProtectedMetadataTamperRejected, failures);
        Run("capture validates the stored bytes and leaves no incomplete snapshot", CaptureRejectsChangedBytes, failures);
        Run("restore handles a truncated installed DLL", TruncatedTargetRestore, failures);
        Run("restore rejects originals after the Adobe host changes", ChangedHostRejectsRestore, failures);
        Run("restore output cannot overwrite the vault", RestoreCannotOverwriteVault, failures);
        Run("restore file-set recreates missing native files", MissingFileSetRestore, failures);
        Run("a committer that throws after replacement is rolled back", ThrowAfterCommitRollsBack, failures);
        Run("simultaneous captures publish one complete protected original", ConcurrentCapture, failures);
        Run("native installer failure is not reported as a final hash failure",
            NativeInstallerFailureIsNotReportedAsFinalHashFailure, failures);
        Run("native installer reports the failing stage",
            NativeInstallerReportsTheFailingStage, failures);
        Run("native installer success is backed up and verified",
            NativeInstallerSuccessIsBackedUpAndVerified, failures);
        Run("a read-only installed target is cleared before replacement",
            ReadOnlyInstalledTargetIsClearedBeforeReplacement, failures);
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
        Run("restore trusts an existing snapshot hash when Authenticode rejects Adobe dvaui",
            RestoreTrustsExistingSnapshotHashWhenAuthenticodeRejectsAdobeDvaui, failures);
        Run("capture still rejects unsigned dvaui when no original exists",
            CaptureStillRejectsUnsignedDvauiWhenNoOriginalExists, failures);
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
        Run("release order follows the release year, not the dvaui file version",
            ReleaseOrderFollowsReleaseYearNotFileVersion, failures);
        Run("a browsed dvaui.dll is described with its release and companion",
            BrowsedDllIsDescribedWithReleaseAndCompanion, failures);
        Run("a diagnostics bundle never carries an Adobe binary",
            DiagnosticsBundleNeverCarriesAnAdobeBinary, failures);
        Run("an imported light palette keeps its own light surfaces",
            ImportedLightPaletteKeepsItsLightSurfaces, failures);
        Run("update checker detects a newer GitHub release",
            UpdateCheckerDetectsNewerGithubRelease, failures);
        Run("update checker ignores current and prerelease versions",
            UpdateCheckerIgnoresCurrentAndPrereleaseVersions, failures);
        Run("update checks compare numeric versions and ignore drafts",
            UpdateCheckerVersionBoundaries, failures);
        Run("ignored updates survive restart and allow the next release",
            IgnoredUpdatesPersist, failures);
        Run("invalid or unreadable update preferences do not suppress updates",
            InvalidUpdatePreferences, failures);
        Run("update popup distinguishes Ignore from session dismissal",
            UpdatePopupIgnoreAndDismiss, failures);
        Run(".afterthemed documents round-trip and reject incomplete themes", ThemeDocumentRoundTrip, failures);
        Run("share codes round-trip and reject damaged input", ShareCodeRoundTrip, failures);
        Run("gallery index keeps only complete themes", GalleryIndexSkipsInvalidThemes, failures);
        Run("theme history keeps ten installs and notices replaced targets", ThemeHistoryTracksInstalls, failures);
        Run("AEP downgrade to 24.x changes only the version header", AepDowngradeTo24, failures);
        Run("AEP downgrade to 23.x removes Shadow Color and keeps everything else", AepDowngradeTo23, failures);
        Run("AEP downgrade never overwrites and rejects unsuitable input", AepDowngradeRefusals, failures);
        Run("AEP downgrade to 22.x and 18.x converts layer records and falls back to 18.x", AepDowngradeOlderFormats, failures);

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
            DeleteTestDirectory(root);
        }
    }

    private static void UnsealedSnapshotRejected()
    {
        var root = NewTempDirectory("unsealed-snapshot");
        try
        {
            var target = Path.Combine(root, "dvaui.dll");
            File.Copy(typeof(OriginalDllStore).Assembly.Location, target);
            var originals = Path.Combine(root, "Originals");
            var snapshot = CreateHistoricalSnapshot(originals, "legacy", target,
                DateTimeOffset.UtcNow, mutateLastByte: false);
            var receipt = Path.Combine(Path.GetDirectoryName(snapshot)!, "snapshot.proof");
            if (File.Exists(receipt)) { File.SetAttributes(receipt, FileAttributes.Normal); File.Delete(receipt); }
            Require(OriginalDllStore.ExistingFor(target, originals, requireAdobeSignature: false) is null,
                "an unsigned legacy file with matching JSON hash was accepted as an Adobe original");
        }
        finally { DeleteTestDirectory(root); }
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
            DeleteTestDirectory(root);
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
            DeleteTestDirectory(root);
        }
    }

    private static void ReadOnlyInstalledTargetIsClearedBeforeReplacement()
    {
        // Adobe ships, or a Creative Cloud repair later marks, dvaui.dll read-only. File.Move
        // refuses to overwrite a read-only destination with "Access to the path is denied"
        // regardless of the caller's actual permissions, so this must be cleared first.
        var root = NewTempDirectory("installer-readonly-target");
        try
        {
            var input = Path.Combine(root, "generated.dll");
            var target = Path.Combine(root, "dvaui.dll");
            var backups = Path.Combine(root, "Backups");
            File.WriteAllText(input, "generated");
            File.WriteAllText(target, "original");
            File.SetAttributes(target, File.GetAttributes(target) | FileAttributes.ReadOnly);

            var report = NativeDllInstaller.Install(input, target, backups,
                requireAfterEffectsClosed: false);

            Require(report.Succeeded, $"install failed during {report.Stage}: {report.Message}");
            Require(OriginalDllStore.Sha256(input) == OriginalDllStore.Sha256(target),
                "a read-only installed target was not replaced");
            Require((File.GetAttributes(target) & FileAttributes.ReadOnly) == 0,
                "the installed target was left read-only after replacement");
            var backup = Directory.EnumerateFiles(backups, "dvaui-*.dll").Single();
            Require((File.GetAttributes(backup) & FileAttributes.ReadOnly) == 0,
                "File.Copy carried the read-only attribute onto the backup, which AfterThemed's " +
                "own rollback and restore must be free to replace");
        }
        finally
        {
            DeleteTestDirectory(root);
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
            DeleteTestDirectory(root);
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
            DeleteTestDirectory(root);
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
            DeleteTestDirectory(root);
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
            DeleteTestDirectory(root);
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
            DeleteTestDirectory(root);
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
            DeleteTestDirectory(root);
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
            DeleteTestDirectory(root);
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
            DeleteTestDirectory(root);
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
            DeleteTestDirectory(root);
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
            DeleteTestDirectory(root);
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
            DeleteTestDirectory(root);
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
            DeleteTestDirectory(root);
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
            DeleteTestDirectory(root);
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
            DeleteTestDirectory(root);
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
            DeleteTestDirectory(root);
        }
    }

    private static void RestoreTrustsExistingSnapshotHashWhenAuthenticodeRejectsAdobeDvaui()
    {
        var root = NewTempDirectory("restore-bad-digest");
        try
        {
            var target = Path.Combine(root, "dvaui.dll");
            File.Copy(typeof(OriginalDllStore).Assembly.Location, target);
            var originals = Path.Combine(root, "Originals");
            var restoreOutput = Path.Combine(root, "restore", "dvaui.dll");
            var snapshot = CreateHistoricalSnapshot(originals, "adobe-bad-digest", target,
                DateTimeOffset.Parse("2026-06-01T00:00:00Z"), mutateLastByte: false);

            OriginalDllStore.AdobeSignature BadDigest(string _) =>
                throw new InvalidDataException("Windows Authenticode verification returned 0x80096010.");

            var restored = OriginalDllStore.CreateRestoreDll(target, originals, restoreOutput, BadDigest);

            Require(OriginalDllStore.Sha256(restored) == OriginalDllStore.Sha256(snapshot),
                "restore refused or changed an existing SHA-pinned snapshot after Authenticode rejected it");
        }
        finally
        {
            DeleteTestDirectory(root);
        }
    }

    private static void CaptureStillRejectsUnsignedDvauiWhenNoOriginalExists()
    {
        var root = NewTempDirectory("capture-no-original");
        try
        {
            var target = Path.Combine(root, "dvaui.dll");
            File.Copy(typeof(OriginalDllStore).Assembly.Location, target);
            var originals = Path.Combine(root, "Originals");

            OriginalDllStore.AdobeSignature InvalidSigner(string _) =>
                throw new InvalidDataException("The signer is not Adobe.");

            var exception = Capture(() => OriginalDllStore.CaptureIfMissing(
                target, originals, out _, InvalidSigner));

            Require(exception is InvalidDataException, "invalid dvaui was captured without an existing original");
        }
        finally
        {
            DeleteTestDirectory(root);
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
        SnapshotProtection.Seal(snapshot, Path.Combine(directory, "snapshot.json"));
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

            var ae2025 = Path.Combine(root, "Ae2025.dll");
            File.WriteAllBytes(ae2025, CreateCompanionFixture(CompanionResourceNames[1..], themed: false));
            Require(LegacyAeThemePatcher.HasNativeThemeResources(ae2025),
                "AE 2025's three-resource native color layout was silently skipped");

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
            DeleteTestDirectory(root);
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
            DeleteTestDirectory(root);
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

    /// <summary>
    /// Measured on a real machine: After Effects CC 2019 ships dvaui 16.1.2.55 while After Effects
    /// 2021 ships dvaui 15.4.1.5. Ordering the picker by file version therefore offers a 2019 release
    /// ahead of a 2021 one, which is the same version-is-not-release trap that already had to be
    /// fixed in companion detection.
    /// </summary>
    private static void ReleaseOrderFollowsReleaseYearNotFileVersion()
    {
        var cc2019 = SyntheticInstall("After Effects CC 2019", new Version(16, 1, 2, 55));
        var ae2021 = SyntheticInstall("After Effects 2021", new Version(15, 4, 1, 5));
        var ae2025 = SyntheticInstall("After Effects 2025", new Version(25, 6, 0, 101));

        var ordered = AfterEffectsCatalog.InReleaseOrder([cc2019, ae2021, ae2025]);

        Require(ordered[0].DisplayName == "After Effects 2025", $"expected 2025 first, got {ordered[0].DisplayName}");
        Require(ordered[1].DisplayName == "After Effects 2021",
            $"expected 2021 ahead of CC 2019 despite its lower dvaui version, got {ordered[1].DisplayName}");
        Require(ordered[2].DisplayName == "After Effects CC 2019", $"expected CC 2019 last, got {ordered[2].DisplayName}");
    }

    private static AfterEffectsInstall SyntheticInstall(string displayName, Version version)
    {
        var root = Path.Combine("C:\\Program Files\\Adobe", $"Adobe {displayName}");
        return AfterEffectsCatalog.Describe(Path.Combine(root, "Support Files", "dvaui.dll"))
               ?? new AfterEffectsInstall(
                   Path.Combine(root, "Support Files", "dvaui.dll"), root, displayName, version, null, "Synthetic")
               {
                   ReleaseYear = ReleaseYearOf(displayName)
               };
    }

    private static int ReleaseYearOf(string displayName)
    {
        foreach (var token in displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            if (token.Length == 4 && int.TryParse(token, out var year)) return year;
        return 0;
    }

    private static void BrowsedDllIsDescribedWithReleaseAndCompanion()
    {
        var root = NewTempDirectory("catalog-describe");
        try
        {
            var supportFiles = Path.Combine(root, "Adobe After Effects 2024", "Support Files");
            Directory.CreateDirectory(supportFiles);
            var dll = Path.Combine(supportFiles, "dvaui.dll");
            File.WriteAllText(dll, "dvaui");

            var withoutCompanion = AfterEffectsCatalog.Describe(dll);
            Require(withoutCompanion is not null, "a real dvaui.dll was not described");
            Require(withoutCompanion!.DisplayName == "After Effects 2024",
                $"expected the folder to name the release, got {withoutCompanion.DisplayName}");
            Require(withoutCompanion.ReleaseYear == 2024, $"expected 2024, got {withoutCompanion.ReleaseYear}");
            Require(!withoutCompanion.HasNativeCompanion, "a missing AfterFXLib.dll was reported as present");

            File.WriteAllText(Path.Combine(supportFiles, "AfterFXLib.dll"), "companion");
            var withCompanion = AfterEffectsCatalog.Describe(dll);
            Require(withCompanion!.HasNativeCompanion, "an adjacent AfterFXLib.dll was not detected");

            Require(AfterEffectsCatalog.Describe(Path.Combine(supportFiles, "missing.dll")) is null,
                "a nonexistent path was described as an installation");
        }
        finally
        {
            DeleteTestDirectory(root);
        }
    }

    /// <summary>
    /// dvaui.dll and AfterFXLib.dll are Adobe's proprietary binaries. The bug reporter describes them
    /// but must never package them, because the bundle is meant to be attached to a public issue.
    /// </summary>
    private static void DiagnosticsBundleNeverCarriesAnAdobeBinary()
    {
        var root = NewTempDirectory("bug-report");
        try
        {
            var supportFiles = Path.Combine(root, "Adobe After Effects 2025", "Support Files");
            Directory.CreateDirectory(supportFiles);
            var dll = Path.Combine(supportFiles, "dvaui.dll");
            const string secretBytes = "PROPRIETARY-ADOBE-CONTENT";
            File.WriteAllText(dll, secretBytes);

            var reports = Path.Combine(root, "Reports");
            Directory.CreateDirectory(reports);
            File.WriteAllText(Path.Combine(reports, "install.json"), "{\"Stage\":\"DLL replacement\"}");

            var bundle = BugReportBuilder.Create(new BugReportContext(
                dll, null, root, reports, "Test-Theme", "Nord", "[00:00:00]  something failed"));

            Require(File.Exists(bundle.BundlePath), "no diagnostics bundle was written");
            Require(bundle.Summary.Contains(OriginalDllStore.Sha256(dll), StringComparison.OrdinalIgnoreCase),
                "the report omits the dvaui.dll SHA-256 that identifies the build");
            Require(bundle.Summary.Contains("DLL replacement", StringComparison.Ordinal),
                "the report omits the last install report, which names the failing stage");
            Require(!bundle.Summary.Contains(secretBytes, StringComparison.Ordinal),
                "the report embedded dvaui.dll content");

            using var archive = System.IO.Compression.ZipFile.OpenRead(bundle.BundlePath);
            foreach (var entry in archive.Entries)
            {
                Require(!entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase),
                    $"the bundle packaged an Adobe binary: {entry.FullName}");
                using var reader = new StreamReader(entry.Open());
                Require(!reader.ReadToEnd().Contains(secretBytes, StringComparison.Ordinal),
                    $"the bundle embedded dvaui.dll content in {entry.FullName}");
            }
            Require(archive.Entries.Any(entry => entry.FullName == "report.md"), "the bundle has no report.md");
        }
        finally
        {
            DeleteTestDirectory(root);
        }
    }

    private static void UpdateCheckerDetectsNewerGithubRelease()
    {
        const string json = """
        {
          "tag_name": "v1.3.13",
          "html_url": "https://github.com/sorflow/afterthemed/releases/tag/v1.3.13",
          "draft": false,
          "prerelease": false,
          "assets": [
            {
              "name": "AfterThemed-Setup-1.3.13.exe",
              "browser_download_url": "https://github.com/sorflow/afterthemed/releases/download/v1.3.13/AfterThemed-Setup-1.3.13.exe"
            },
            {
              "name": "source.zip",
              "browser_download_url": "https://github.com/sorflow/afterthemed/archive/refs/tags/v1.3.13.zip"
            }
          ]
        }
        """;

        var update = UpdateChecker.ParseLatestRelease(json, new Version(1, 3, 12));

        Require(update is not null, "newer release was not detected");
        Require(update!.LatestVersion == new Version(1, 3, 13), $"wrong version: {update.LatestVersion}");
        Require(update.DownloadUrl.EndsWith("AfterThemed-Setup-1.3.13.exe", StringComparison.Ordinal),
            $"installer asset was not selected: {update.DownloadUrl}");
    }

    private static void UpdateCheckerIgnoresCurrentAndPrereleaseVersions()
    {
        Require(UpdateChecker.ParseVersion("v1.3.12+abc1234") == new Version(1, 3, 12),
            "version parser did not strip tag prefix and build metadata");

        const string currentJson = """
        {
          "tag_name": "v1.3.12",
          "html_url": "https://github.com/sorflow/afterthemed/releases/tag/v1.3.12",
          "draft": false,
          "prerelease": false,
          "assets": []
        }
        """;
        Require(UpdateChecker.ParseLatestRelease(currentJson, new Version(1, 3, 12)) is null,
            "current version was reported as an update");

        const string prereleaseJson = """
        {
          "tag_name": "v9.0.0-beta",
          "html_url": "https://github.com/sorflow/afterthemed/releases/tag/v9.0.0-beta",
          "draft": false,
          "prerelease": true,
          "assets": []
        }
        """;
        Require(UpdateChecker.ParseLatestRelease(prereleaseJson, new Version(1, 3, 12)) is null,
            "prerelease was reported as a stable update");
    }

    private static void UpdatePopupIgnoreAndDismiss()
    {
        var update = new UpdateInfo(new Version(9, 0, 0), "v9.0.0",
            UpdateChecker.LatestReleasePageUrl, UpdateChecker.LatestReleasePageUrl);
        foreach (var ignore in new[] { true, false })
        {
            using var dialog = new UpdateAvailableForm(update)
            {
                ShowInTaskbar = false,
                StartPosition = System.Windows.Forms.FormStartPosition.Manual,
                Location = new Point(-32000, -32000)
            };
            dialog.Shown += (_, _) =>
            {
                if (ignore)
                    dialog.Controls.OfType<System.Windows.Forms.Button>().Single(button => button.Text == UpdateAvailableForm.SkipText).PerformClick();
                else
                    dialog.Close();
            };
            var result = dialog.ShowDialog();
            Require((result == System.Windows.Forms.DialogResult.Ignore) == ignore,
                "Ignore and session dismissal produced the same result");
        }
    }

    private static void UpdateCheckerVersionBoundaries()
    {
        Require(UpdateChecker.ParseLatestRelease("""{"tag_name":"v1.3.13.0"}""", new Version(1, 3, 13)) is null,
            "an equivalent four-component version was reported as newer");
        Require(UpdateChecker.ParseLatestRelease("""{"tag_name":"v1.3.13"}""", new Version(1, 3, 13, 0)) is null,
            "an equivalent three-component version was reported as newer");
        Require(UpdateChecker.ParseLatestRelease("""{"tag_name":"v1.3.9"}""", new Version(1, 3, 13)) is null,
            "an older release was reported as an update");
        Require(UpdateChecker.ParseLatestRelease("""{"tag_name":"v1.3.100"}""", new Version(1, 3, 13)) is not null,
            "numeric version ordering was replaced by lexical ordering");
        Require(UpdateChecker.ParseLatestRelease("""{"tag_name":"v9.0.0","draft":true}""", new Version(1, 3, 13)) is null,
            "a draft release was offered before it was published");
        Require(UpdateChecker.ParseLatestRelease("""{"tag_name":"v9.0.0-beta","prerelease":false}""", new Version(1, 3, 13)) is null,
            "a prerelease tag was offered as a stable update");
        Require(UpdateChecker.ParseLatestRelease("""{"tag_name":"invalid"}""", new Version(1, 3, 13)) is null,
            "an invalid version was accepted");
        var withoutInstaller = UpdateChecker.ParseLatestRelease("""{"tag_name":"v9.0.0","assets":[]}""", new Version(1, 3, 13));
        Require(withoutInstaller?.DownloadUrl == UpdateChecker.LatestReleasePageUrl,
            "a release without an installer should link to the release page");
    }

    private static void IgnoredUpdatesPersist()
    {
        var root = NewTempDirectory("ignored-updates");
        try
        {
            var data = Path.Combine(root, "preferences");
            var preferences = new UpdatePreferences(data);
            Require(preferences.ShouldNotify(new Version(1, 3, 14)), "a fresh profile suppressed an update");
            preferences.Ignore(new Version(1, 3, 14));
            var restarted = new UpdatePreferences(data);
            Require(!restarted.ShouldNotify(new Version(1, 3, 14, 0)), "ignored version did not survive restart");
            Require(!restarted.ShouldNotify(new Version(1, 3, 13)), "an older release bypassed the ignore preference");
            Require(restarted.ShouldNotify(new Version(1, 3, 15)), "ignoring one version suppressed the next release");
            restarted.Ignore(new Version(1, 3, 15));
            Require(!new UpdatePreferences(data).ShouldNotify(new Version(1, 3, 15)), "a second ignore was not persisted");
            Require(Directory.GetFiles(data, "*.tmp").Length == 0, "temporary preference files were left behind");
        }
        finally { DeleteTestDirectory(root); }
    }

    private static ThemeDocument SampleTheme(string name = "Sample Theme") => new(name, ThemeSettings.SunsetDusk)
    {
        Author = "Tester",
        Font = "Inter",
        TextReplacements = "Composition => Comp",
        ThemePanels = false,
        Thumbnail = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="
    };

    private static void RequireSameSettings(ThemeSettings expected, ThemeSettings actual, string context)
    {
        Require(ThemeDocuments.Colors(expected).Select(ThemeDocuments.Hex).SequenceEqual(ThemeDocuments.Colors(actual).Select(ThemeDocuments.Hex)),
            $"{context}: colors changed");
        Require(Math.Abs(expected.TextCutoff - actual.TextCutoff) < .006f, $"{context}: cutoff changed");
        Require(expected.ExactAccents == actual.ExactAccents, $"{context}: exact accents changed");
        Require(Math.Abs(expected.ForegroundAlphaFloor - actual.ForegroundAlphaFloor) < .006f, $"{context}: alpha floor changed");
    }

    private static void ThemeDocumentRoundTrip()
    {
        var original = SampleTheme();
        var parsed = ThemeDocuments.Parse(ThemeDocuments.Serialize(original));
        RequireSameSettings(original.Settings, parsed.Settings, "document");
        Require(parsed.Name == original.Name && parsed.Author == "Tester" && parsed.Font == "Inter" &&
                parsed.TextReplacements == original.TextReplacements && parsed.ThemePanels == false &&
                parsed.Thumbnail == original.Thumbnail, "optional fields were lost");
        Require(ThemeDocuments.Parse(ThemeDocuments.Serialize(original, includeThumbnail: false)).Thumbnail is null,
            "thumbnail was kept when excluded");

        var json = ThemeDocuments.Serialize(original);
        Require(Capture(() => ThemeDocuments.Parse(json.Replace("\"afterthemed\"", "\"other\""))) is InvalidDataException,
            "a foreign format was accepted");
        Require(Capture(() => ThemeDocuments.Parse(json.Replace("\"danger\": \"#FF6F86\"", "\"danger\": \"red\""))) is InvalidDataException,
            "an invalid color was accepted");
        Require(Capture(() => ThemeDocuments.Parse(json.Replace("\"danger\": \"#FF6F86\"", "\"danger\": 5"))) is InvalidDataException,
            "a non-string color was accepted");
        var huge = ThemeDocuments.Parse(json.Replace(original.Thumbnail!, "data:image/png;base64," + new string('A', 300_000)));
        Require(huge.Thumbnail is null, "an oversized thumbnail was kept");
        var script = ThemeDocuments.Parse(json.Replace(original.Thumbnail!, "javascript:alert(1)"));
        Require(script.Thumbnail is null, "a non-PNG thumbnail was kept");
    }

    private static void ShareCodeRoundTrip()
    {
        var original = SampleTheme("Sunset · Dusk ✨");
        var code = ThemeDocuments.ToShareCode(original);
        Require(code.StartsWith("AT1-") && code.Length < 80, $"share code is not short: {code}");
        var parsed = ThemeDocuments.FromShareCode(" " + code[..10] + "\n" + code[10..] + " ");
        RequireSameSettings(original.Settings, parsed.Settings, "share code");
        Require(parsed.Name == original.Name, "share code lost the name");
        var longName = ThemeDocuments.FromShareCode(ThemeDocuments.ToShareCode(SampleTheme(new string('é', 60))));
        Require(longName.Name.Length > 0 && longName.Name.All(c => c == 'é'), "a long name was cut mid-character");
        Require(Capture(() => ThemeDocuments.FromShareCode("AT1-abc")) is InvalidDataException, "a truncated code was accepted");
        Require(Capture(() => ThemeDocuments.FromShareCode("XYZ-" + code[4..])) is InvalidDataException, "a wrong prefix was accepted");
        Require(Capture(() => ThemeDocuments.FromShareCode("AT1-!!!!")) is InvalidDataException, "a damaged code was accepted");
    }

    private static void GalleryIndexSkipsInvalidThemes()
    {
        var valid = ThemeDocuments.Serialize(SampleTheme("Good"), includeThumbnail: false);
        var themes = ThemeGallery.Parse($"{{\"themes\":[{valid},{{\"format\":\"afterthemed\",\"name\":\"Broken\"}},null,42,\"text\"]}}");
        Require(themes.Count == 1 && themes[0].Name == "Good", $"gallery kept {themes.Count} themes");
        Require(Capture(() => ThemeGallery.Parse("{\"nothing\":true}")) is InvalidDataException, "an index without themes was accepted");
        Require(ThemeGallery.SubmitUrl(SampleTheme()).StartsWith("https://github.com/sorflow/afterthemed/issues/new?"),
            "submission does not open the AfterThemed repository");
    }

    private static void ThemeHistoryTracksInstalls()
    {
        var root = NewTempDirectory("theme-history");
        try
        {
            var path = Path.Combine(root, "theme-history.json");
            var targetA = Path.Combine(root, "A", "dvaui.dll");
            var targetB = Path.Combine(root, "B", "dvaui.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(targetA)!);
            Directory.CreateDirectory(Path.GetDirectoryName(targetB)!);
            File.WriteAllText(targetA, "theme A");
            File.WriteAllText(targetB, "theme B");

            ThemeHistory.RecordInstall(path, "1", SampleTheme("Two targets"), targetA, OriginalDllStore.Sha256(targetA));
            ThemeHistory.RecordInstall(path, "1", SampleTheme("Two targets"), targetB, OriginalDllStore.Sha256(targetB));
            var file = ThemeHistory.Load(path);
            Require(file.History.Count == 1 && file.History[0].Targets.Length == 2, "one install into two targets was not grouped");
            Require(file.Installed.Count == 2, "installed targets were not recorded");

            for (var i = 2; i <= 12; i++)
                ThemeHistory.RecordInstall(path, i.ToString(), SampleTheme($"Theme {i}"), targetA, OriginalDllStore.Sha256(targetA));
            file = ThemeHistory.Load(path);
            Require(file.History.Count == ThemeHistory.Limit && file.History[0].Id == "12", "history did not keep the ten newest installs");
            Require(file.Installed.Count == 2 && file.Installed.Single(item => item.Target == targetA).EntryId == "12",
                "a reinstall did not replace the target record");

            Require(ThemeHistory.FindReplaced(file, OriginalDllStore.Sha256).Count == 0, "an untouched install was reported as replaced");
            File.WriteAllText(targetB, "Adobe update");
            var replaced = ThemeHistory.FindReplaced(file, OriginalDllStore.Sha256);
            Require(replaced.Count == 1 && replaced[0].Target == targetB, "a replaced install was not detected");
            Require(ThemeDocuments.Parse(replaced[0].Theme).Name == "Two targets", "the replaced theme cannot be re-applied");

            ThemeHistory.Forget(path, targetB);
            Require(ThemeHistory.FindReplaced(ThemeHistory.Load(path), OriginalDllStore.Sha256).Count == 0, "a forgotten target was still reported");
            File.WriteAllText(path, "{ not json");
            Require(ThemeHistory.Load(path).History.Count == 0, "a corrupt history file was not tolerated");
        }
        finally { DeleteTestDirectory(root); }
    }

    private static byte[] AepChunk(string id, params byte[][] parts)
    {
        var data = parts.SelectMany(part => part).ToArray();
        var bytes = new List<byte>(Encoding.Latin1.GetBytes(id));
        var size = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(size, (uint)data.Length);
        bytes.AddRange(size);
        bytes.AddRange(data);
        if (data.Length % 2 == 1) bytes.Add(0);
        return bytes.ToArray();
    }

    private static byte[] AepList(string type, params byte[][] children) =>
        AepChunk("LIST", [Encoding.Latin1.GetBytes(type), .. children]);

    private static byte[] AepMatchName(string name)
    {
        var data = new byte[40];
        Encoding.ASCII.GetBytes(name).CopyTo(data, 0);
        return AepChunk("tdmn", data);
    }

    private static readonly byte[] AepTrailer = Encoding.UTF8.GetBytes("<?xpacket begin=\"\"?><x:xmpmeta/><?xpacket end=\"w\"?>");

    /// <summary>A small After Effects 25.6 project with the structures the downgrader must preserve.</summary>
    private static byte[] SampleAep()
    {
        byte[] head = [0x00, 0x60, 0x00, 0x09, 0x0F, 0x0B, 0x06, 0x65, 0x80, 0, 0, 0, 0, 0, 0, 0x0F, 0, 0, 0, 0x3A];
        var nhed = new byte[32];
        new byte[] { 0x55, 0x3F, 0xD1, 0x01, 0, 0, 0, 0x1F, 0x55, 0x3F, 0xD1, 0x00 }.CopyTo(nhed, 0x14);
        var property = (string name, byte value) => new[] { AepMatchName(name), AepList("tdbs", AepChunk("tdb4", [value, 1, 2]), AepChunk("cdat", new byte[8])) };
        var material = AepList("tdgp", [.. property("ADBE Accepts Lights", 1), .. property("ADBE Light Transmission", 5),
            .. property("ADBE Shadow Color", 2), .. property("ADBE Appears in Reflections", 3), AepMatchName("ADBE Group End")]);
        var ldta = Enumerable.Range(0, 164).Select(i => (byte)(i < 160 ? i : 0xAA)).ToArray();
        var spatial = new byte[124];
        spatial[0] = 0xDB;
        spatial[119] = 1;
        var position = AepList("tdgp", AepMatchName("ADBE Position"), AepList("tdbs", AepChunk("tdb4", spatial)), AepMatchName("ADBE Group End"));
        var body = AepList("Fold",
            AepChunk("t\u00e9st", [0xE9, 0xFF, 0x00]),
            AepList("btdk", [0x4C, 0x49, 0x53, 0x54, 0xFF, 0xFF, 0xFF, 0xFF, 9]),
            AepList("Layr", AepChunk("ldta", ldta), position, material),
            AepList("FEE ", AepChunk("ppSn", [0x40, 0x62, 0xC0, 0, 0, 0, 0, 0])),
            AepList("SecL", AepList("tdgp", property("ADBE Shadow Color", 4))));
        var form = AepChunk("RIFX", Encoding.ASCII.GetBytes("Egg!"), AepChunk("svap", [0x0F, 0x0B, 0x06, 0x65]),
            AepChunk("head", head), AepChunk("nhed", nhed), body);
        return [.. form, .. AepTrailer];
    }

    private static void AepDowngradeTo24()
    {
        var root = NewTempDirectory("aep-24");
        try
        {
            var input = Path.Combine(root, "scene.aep");
            var original = SampleAep();
            File.WriteAllBytes(input, original);
            Require(AepDowngrader.ReadVersion(input) == new AepVersion(25, 6, 0, 101), $"read {AepDowngrader.ReadVersion(input)}");
            var output = AepDowngrader.OutputPathFor(input, 24);
            Require(Path.GetFileName(output) == "scene (AE 24.x).aep", output);
            var result = AepDowngrader.Downgrade(input, output, 24);
            Require(result.Changes.Count == 1 && AepDowngrader.ReadVersion(output) == new AepVersion(24, 6, 0, 65), "header not rewritten to 24.6");
            var bytes = File.ReadAllBytes(output);
            Require(bytes.Length == original.Length, "a 24.x downgrade changed the file size");
            var changed = Enumerable.Range(0, bytes.Length).Where(i => bytes[i] != original[i]).ToList();
            var headData = IndexOf(original, "head") + 8;
            var nhedData = IndexOf(original, "nhed") + 8;
            Require(changed.All(i => (i >= headData && i < headData + 8) || (i >= nhedData + 0x14 && i < nhedData + 0x20)),
                "bytes outside the version header changed");
            Require(File.ReadAllBytes(input).SequenceEqual(original), "the original project was modified");
        }
        finally { DeleteTestDirectory(root); }
    }

    private static void AepDowngradeTo23()
    {
        var root = NewTempDirectory("aep-23");
        try
        {
            var input = Path.Combine(root, "scene.aep");
            var original = SampleAep();
            File.WriteAllBytes(input, original);
            var result = AepDowngrader.Downgrade(input, AepDowngrader.OutputPathFor(input, 23), 23);
            var bytes = File.ReadAllBytes(result.OutputPath);
            var text = Encoding.Latin1.GetString(bytes);
            Require(AepDowngrader.ReadVersion(result.OutputPath).Major == 23, "header not rewritten to 23.x");
            Require(!text.Contains("ADBE Shadow Color") && text.Contains("ADBE Accepts Lights") && text.Contains("ADBE Appears in Reflections"),
                "Shadow Color was not removed cleanly");
            Require(result.Changes.Any(change => change.Contains("Shadow Color (2×")), string.Join("; ", result.Changes));
            // Each removed property is a 48-byte tdmn plus its 40-byte tdbs list (8 + 4 + padded tdb4 12 + cdat 16).
            Require(original.Length - bytes.Length == 2 * 88, $"removed {original.Length - bytes.Length} bytes");
            Require(BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(4)) + 8 == bytes.Length - AepTrailer.Length, "RIFX size was not updated");
            Require(bytes.AsSpan(bytes.Length - AepTrailer.Length).SequenceEqual(AepTrailer), "XMP trailer was not kept");
            Require(text.Contains("t\u00e9st") && bytes.AsSpan(IndexOf(bytes, "t\u00e9st") + 8, 3).SequenceEqual(new byte[] { 0xE9, 0xFF, 0x00 }),
                "a non-ASCII chunk was not copied byte for byte");
            Require(text.Contains("btdk") && bytes.AsSpan(IndexOf(bytes, "btdk") + 4, 9).SequenceEqual(new byte[] { 0x4C, 0x49, 0x53, 0x54, 0xFF, 0xFF, 0xFF, 0xFF, 9 }),
                "an opaque btdk payload changed");
            var again = AepDowngrader.Downgrade(result.OutputPath, Path.Combine(root, "nope.aep"), 24);
            Require(false, "an already-older project was converted: " + again.OutputPath);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already from After Effects 23")) { }
        finally { DeleteTestDirectory(root); }
    }

    private static void AepDowngradeRefusals()
    {
        var root = NewTempDirectory("aep-refuse");
        try
        {
            var input = Path.Combine(root, "scene.aep");
            File.WriteAllBytes(input, SampleAep());
            Require(Capture(() => AepDowngrader.Downgrade(input, input, 24)) is InvalidOperationException, "the original could be overwritten");
            var output = Path.Combine(root, "taken.aep");
            File.WriteAllText(output, "keep me");
            Require(Capture(() => AepDowngrader.Downgrade(input, output, 24)) is IOException, "an existing file was replaced");
            Require(File.ReadAllText(output) == "keep me" && !File.Exists(output + ".partial"), "a refused write left changes behind");
            File.WriteAllText(Path.Combine(root, "scene (AE 24.x).aep"), "first");
            Require(Path.GetFileName(AepDowngrader.OutputPathFor(input, 24)) == "scene (AE 24.x) 2.aep", "output name was not numbered");
            var text = Path.Combine(root, "notes.aep");
            File.WriteAllText(text, "not a project");
            Require(Capture(() => AepDowngrader.ReadVersion(text)) is InvalidDataException, "a non-project was accepted");
            Require(Capture(() => AepDowngrader.Downgrade(input, Path.Combine(root, "x.aep"), 17)) is ArgumentOutOfRangeException, "a target older than 18.x was accepted");
            var truncated = Path.Combine(root, "cut.aep");
            File.WriteAllBytes(truncated, SampleAep()[..200]);
            Require(Capture(() => AepDowngrader.Downgrade(truncated, Path.Combine(root, "cut-out.aep"), 24)) is InvalidDataException, "a truncated project was accepted");
            Require(!File.Exists(Path.Combine(root, "cut-out.aep")), "a failed conversion left a file");
        }
        finally { DeleteTestDirectory(root); }
    }

    private static void AepDowngradeOlderFormats()
    {
        var root = NewTempDirectory("aep-older");
        try
        {
            var input = Path.Combine(root, "scene.aep");
            var original = SampleAep();
            File.WriteAllBytes(input, original);

            var to22 = AepDowngrader.Downgrade(input, AepDowngrader.OutputPathFor(input, 22), 22);
            var bytes = File.ReadAllBytes(to22.OutputPath);
            var text = Encoding.Latin1.GetString(bytes);
            Require(AepDowngrader.ReadVersion(to22.OutputPath) == new AepVersion(22, 6, 0, 59), $"22.x header: {AepDowngrader.ReadVersion(to22.OutputPath)}");
            var head = IndexOf(bytes, "head") + 8;
            Require(bytes[head + 19] == 0x48, "head byte 19 was not set for the 22.x format");
            Require(!text.Contains("ADBE Light Transmission") && !text.Contains("ADBE Shadow Color") && text.Contains("ADBE Accepts Lights"),
                "22.x did not remove the 23+ properties");
            var ldta = IndexOf(bytes, "ldta");
            Require(BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(ldta + 4)) == 160 &&
                    bytes.AsSpan(ldta + 8, 160).SequenceEqual(Enumerable.Range(0, 160).Select(i => (byte)i).ToArray()),
                "the layer record was not trimmed to its first 160 bytes");
            var spatial = IndexOf(bytes, "ADBE Position") + 40 + 8 + 4;
            Require(Encoding.Latin1.GetString(bytes, spatial, 4) == "tdb4" && bytes[spatial + 8 + 119] == 0, "the 23+ spatial flag was not cleared");
            var nhed = IndexOf(bytes, "nhed") + 8;
            Require(bytes.AsSpan(nhed + 0x14, 8).IndexOfAnyExcept((byte)0) < 0 &&
                    bytes.AsSpan(nhed + 0x1C, 4).SequenceEqual(new byte[] { 0x55, 0x3F, 0xD1, 0x00 }),
                "nhed was not zeroed with the project identifier kept");
            Require(text.Contains("ppSn"), "22.x removed a chunk 22.x still has");
            Require(BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(4)) + 8 == bytes.Length - AepTrailer.Length, "RIFX size was not updated");

            var to18 = AepDowngrader.Downgrade(input, AepDowngrader.OutputPathFor(input, 18), 18);
            Require(AepDowngrader.ReadVersion(to18.OutputPath) == new AepVersion(18, 4, 0, 38), "18.x header");
            Require(!Encoding.Latin1.GetString(File.ReadAllBytes(to18.OutputPath)).Contains("ppSn"), "18.x kept the ppSn chunk");

            var to20 = AepDowngrader.Downgrade(input, AepDowngrader.OutputPathFor(input, 20), 20);
            Require(Path.GetFileName(to20.OutputPath) == "scene (AE 20.x).aep" && AepDowngrader.ReadVersion(to20.OutputPath).Major == 18 &&
                    to20.Changes.Any(change => change.Contains("18.x format")), "20.x did not fall back to the 18.x format");
            Require(File.ReadAllBytes(to20.OutputPath).AsSpan(0, 100).SequenceEqual(File.ReadAllBytes(to18.OutputPath).AsSpan(0, 100)),
                "the 20.x fallback differs from the 18.x format");
            Require(AepDowngrader.FormatFor(21) == 18 && AepDowngrader.FormatFor(22) == 22 && AepDowngrader.FormatFor(30) == 24, "format lookup");

            Require(Capture(() => AepDowngrader.Downgrade(to22.OutputPath, Path.Combine(root, "same.aep"), 22)) is InvalidOperationException,
                "a 22.x project was converted to 22.x");
            Require(Capture(() => AepDowngrader.Downgrade(to22.OutputPath, Path.Combine(root, "x.aep"), 23)) is InvalidOperationException,
                "a 22.x project was converted up to 23.x");
            Require(AepDowngrader.ReadVersion(AepDowngrader.Downgrade(to22.OutputPath, Path.Combine(root, "22-to-18.aep"), 18).OutputPath).Major == 18,
                "a 22.x project could not go down to 18.x");
            Require(File.ReadAllBytes(input).SequenceEqual(original), "the original project was modified");
        }
        finally { DeleteTestDirectory(root); }
    }

    private static int IndexOf(byte[] bytes, string id)
    {
        var pattern = Encoding.Latin1.GetBytes(id);
        for (var i = 0; i <= bytes.Length - pattern.Length; i++)
            if (bytes.AsSpan(i, pattern.Length).SequenceEqual(pattern)) return i;
        throw new InvalidDataException($"{id} not found");
    }

    private static void InvalidUpdatePreferences()
    {
        var root = NewTempDirectory("invalid-update-preferences");
        try
        {
            var path = Path.Combine(root, "ignored-update.txt");
            File.WriteAllText(path, "not a version");
            var preferences = new UpdatePreferences(root);
            Require(preferences.ShouldNotify(new Version(1, 3, 14)), "corrupt preferences suppressed an update");
            File.Delete(path);
            Directory.CreateDirectory(path);
            Require(preferences.ShouldNotify(new Version(1, 3, 14)), "an unreadable preference suppressed an update");
            Require(Capture(() => preferences.Ignore(new Version(1, 3, 14))) is IOException or UnauthorizedAccessException,
                "a failed save silently reported success");
            Require(Directory.GetFiles(root, "*.tmp").Length == 0, "a failed save leaked a temporary file");
        }
        finally { DeleteTestDirectory(root); }
    }

    private static void ProtectedOriginalRecovery()
    {
        var root = NewTempDirectory("protected-recovery");
        try
        {
            var target = Path.Combine(root, "dvaui.dll");
            File.Copy(typeof(OriginalDllStore).Assembly.Location, target);
            var originals = Path.Combine(root, "Originals");
            var original = OriginalDllStore.CaptureIfMissing(target, originals, out _,
                _ => new("CN=Adobe Test", "TEST"));
            var expected = OriginalDllStore.Sha256(target);
            Require((File.GetAttributes(original) & FileAttributes.ReadOnly) != 0, "original is not protected from accidental edits");
            File.SetAttributes(original, FileAttributes.Normal);
            File.Delete(original);
            File.Delete(target);
            var output = OriginalDllStore.CreateRestoreDll(target, originals, Path.Combine(root, "restore.dll"));
            Require(OriginalDllStore.Sha256(output) == expected, "recovery copy did not restore exact bytes");
            var installed = NativeDllInstaller.Install(output, target, Path.Combine(root, "Backups"),
                requireAfterEffectsClosed: false, allowMissingTarget: true);
            Require(installed.Succeeded && OriginalDllStore.Sha256(target) == expected,
                "restore cannot recreate a deleted installed DLL: " + installed.Message);
            var replica = Path.Combine(Path.GetDirectoryName(original)!, "original.recovery");
            File.SetAttributes(replica, FileAttributes.Normal);
            File.WriteAllText(replica, "corrupt");
            Require(Capture(() => OriginalDllStore.RequireExistingOriginal(target, originals)) is not null,
                "both damaged copies were accepted");
        }
        finally { DeleteTestDirectory(root); }
    }

    private static void ProtectedMetadataTamperRejected()
    {
        var root = NewTempDirectory("protected-tamper");
        try
        {
            var target = Path.Combine(root, "dvaui.dll");
            File.Copy(typeof(OriginalDllStore).Assembly.Location, target);
            var originals = Path.Combine(root, "Originals");
            var original = OriginalDllStore.CaptureIfMissing(target, originals, out _,
                _ => new("CN=Adobe Test", "TEST"));
            var metadata = Path.Combine(Path.GetDirectoryName(original)!, "snapshot.json");
            File.SetAttributes(metadata, FileAttributes.Normal);
            File.AppendAllText(metadata, " ");
            Require(OriginalDllStore.ExistingFor(target, originals, false) is null,
                "changed metadata was accepted despite its protection record");
        }
        finally { DeleteTestDirectory(root); }
    }

    private static void CaptureRejectsChangedBytes()
    {
        var root = NewTempDirectory("capture-change");
        try
        {
            var target = Path.Combine(root, "dvaui.dll");
            File.Copy(typeof(OriginalDllStore).Assembly.Location, target);
            var originals = Path.Combine(root, "Originals");
            var failure = Capture(() => OriginalDllStore.CaptureIfMissing(target, originals, out _, path =>
            {
                Require(path != target, "signature inspector did not inspect the staged snapshot");
                File.WriteAllText(path, "changed during inspection");
                return new("CN=Adobe Test", "TEST");
            }));
            Require(failure is IOException, "changed capture was published");
            Require(!Directory.EnumerateFiles(originals, "snapshot.json", SearchOption.AllDirectories).Any(),
                "failed capture became a selectable snapshot");
            Require(!Directory.EnumerateFiles(originals, "*.adobe-original", SearchOption.AllDirectories).Any(),
                "failed capture leaked its staged original");
        }
        finally { DeleteTestDirectory(root); }
    }

    private static void TruncatedTargetRestore()
    {
        var root = NewTempDirectory("truncated-restore");
        try
        {
            var target = Path.Combine(root, "dvaui.dll");
            File.Copy(typeof(OriginalDllStore).Assembly.Location, target);
            var originals = Path.Combine(root, "Originals");
            var snapshot = OriginalDllStore.CaptureIfMissing(target, originals, out _, _ => new("CN=Adobe Test", "TEST"));
            File.WriteAllText(target, "truncated");
            var restored = OriginalDllStore.CreateRestoreDll(target, originals, Path.Combine(root, "restored.dll"));
            Require(OriginalDllStore.Sha256(restored) == OriginalDllStore.Sha256(snapshot), "truncated target prevented restore");
        }
        finally { DeleteTestDirectory(root); }
    }

    private static void ChangedHostRejectsRestore()
    {
        var root = NewTempDirectory("changed-host");
        try
        {
            var target = Path.Combine(root, "dvaui.dll");
            var host = Path.Combine(root, "AfterFX.exe");
            File.Copy(typeof(OriginalDllStore).Assembly.Location, target);
            File.WriteAllText(host, "Adobe version A");
            var originals = Path.Combine(root, "Originals");
            _ = OriginalDllStore.CaptureIfMissing(target, originals, out _, _ => new("CN=Adobe Test", "TEST"));
            File.WriteAllText(host, "Adobe version B");
            File.Delete(target);
            Require(Capture(() => OriginalDllStore.CreateRestoreDll(target, originals, Path.Combine(root, "restore.dll"))) is not null,
                "a missing DLL was restored from a different Adobe host build");
            Require(!File.Exists(Path.Combine(root, "restore.dll")), "a stale restore file was published");
        }
        finally { DeleteTestDirectory(root); }
    }

    private static void RestoreCannotOverwriteVault()
    {
        var root = NewTempDirectory("vault-alias");
        try
        {
            var target = Path.Combine(root, "dvaui.dll");
            File.Copy(typeof(OriginalDllStore).Assembly.Location, target);
            var originals = Path.Combine(root, "Originals");
            var snapshot = OriginalDllStore.CaptureIfMissing(target, originals, out _, _ => new("CN=Adobe Test", "TEST"));
            var expected = OriginalDllStore.Sha256(snapshot);
            Require(Capture(() => OriginalDllStore.CreateRestoreDll(target, originals, snapshot)) is InvalidOperationException,
                "restore permitted its output to overwrite a protected original");
            Require(OriginalDllStore.Sha256(snapshot) == expected, "vault was changed");
        }
        finally { DeleteTestDirectory(root); }
    }

    private static void MissingFileSetRestore()
    {
        var root = NewTempDirectory("missing-set");
        try
        {
            var source = Path.Combine(root, "original.dll");
            File.WriteAllText(source, "verified restore bytes");
            var manifest = new ThemeFileSetManifest(Path.Combine(root, "Backups"),
                [new(source, Path.Combine(root, "AfterFXLib.dll")), new(source, Path.Combine(root, "dvaui.dll"))],
                RestoreMissingTargets: true);
            var result = ThemeFileSetInstaller.Install(manifest, requireAfterEffectsClosed: false);
            Require(result.Succeeded && result.Files.Count == 2, "missing native set could not be recovered");
            foreach (var file in manifest.Files)
                Require(OriginalDllStore.Sha256(file.TargetPath) == OriginalDllStore.Sha256(source), "recovered file hash differs");
        }
        finally { DeleteTestDirectory(root); }
    }

    private static void ThrowAfterCommitRollsBack()
    {
        var root = NewTempDirectory("throw-after-commit");
        try
        {
            var source = Path.Combine(root, "generated.dll");
            var target = Path.Combine(root, "dvaui.dll");
            File.WriteAllText(source, "generated");
            File.WriteAllText(target, "original");
            var report = NativeDllInstaller.Install(source, target, Path.Combine(root, "Backups"),
                requireAfterEffectsClosed: false, committer: new ThrowingCommitter());
            Require(!report.Succeeded && report.RollbackSucceeded && File.ReadAllText(target) == "original",
                "an exception after replacement left the changed DLL installed");
        }
        finally { DeleteTestDirectory(root); }
    }

    private sealed class ThrowingCommitter : NativeDllInstaller.IAtomicCommitter
    {
        public void Replace(string stagedPath, string targetPath)
        {
            File.Move(stagedPath, targetPath, true);
            throw new IOException("Failure after commit");
        }
    }

    private static void ConcurrentCapture()
    {
        var root = NewTempDirectory("concurrent-capture");
        try
        {
            var target = Path.Combine(root, "dvaui.dll");
            File.Copy(typeof(OriginalDllStore).Assembly.Location, target);
            var originals = Path.Combine(root, "Originals");
            var captured = new string[4];
            Parallel.For(0, captured.Length, index =>
            {
                captured[index] = OriginalDllStore.CaptureIfMissing(target, originals, out _,
                    _ => new("CN=Adobe Test", "TEST"));
            });
            Require(captured.Distinct().Count() == 1, "concurrent capture selected different originals");
            Require(Directory.EnumerateFiles(originals, "snapshot.json", SearchOption.AllDirectories).Count() == 1,
                "concurrent capture published duplicate or incomplete records");
            SnapshotProtection.Verify(Path.Combine(Path.GetDirectoryName(captured[0])!, "snapshot.json"));
        }
        finally { DeleteTestDirectory(root); }
    }

    private static void ReadableOriginalCapture()
    {
        var root = NewTempDirectory("readable-library");
        try
        {
            var originals = Path.Combine(root, "Originals");
            var paths = new List<string>();
            foreach (var relative in new[] { @"drive-a\Adobe After Effects 2020\Support Files\dvaui.dll",
                         @"drive-b\Adobe After Effects 2020\Support Files\dvaui.dll",
                         @"drive-a\Adobe After Effects 2020\Support Files\AfterFXLib.dll" })
            {
                var target = Path.Combine(root, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(typeof(OriginalDllStore).Assembly.Location, target);
                var stored = OriginalDllStore.CaptureIfMissing(target, originals, out var captured, _ => new("Adobe Test", "TEST"));
                Require(captured, "new original was not captured");
                Require(Path.GetRelativePath(originals, stored).StartsWith(@"After Effects 2020\"), "DLL internal version was mistaken for AE release");
                Require(Path.GetFileName(Path.GetDirectoryName(stored)!).StartsWith(Path.GetFileName(target)), "DLL role was not labeled");
                Require(OriginalDllStore.CaptureIfMissing(target, originals, out captured) == stored && !captured,
                    "readable capture could not be reused");
                paths.Add(stored);
            }
            Require(paths.Distinct().Count() == 3, "different installations or companions were merged");
        }
        finally { DeleteTestDirectory(root); }
    }

    private static void OrganizeLegacyOriginals()
    {
        var root = NewTempDirectory("organize-library");
        try
        {
            var target = Path.Combine(root, @"Adobe After Effects 2020\Support Files\dvaui.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(typeof(OriginalDllStore).Assembly.Location, target);
            var originals = Path.Combine(root, "Originals");
            var saved = CreateHistoricalSnapshot(originals, "0123456789ABCDEF", target, DateTimeOffset.UtcNow, false);
            OriginalDllStore.MarkActiveSnapshot(target, originals, saved);
            var hashes = Directory.GetFiles(Path.GetDirectoryName(saved)!).ToDictionary(path => Path.GetFileName(path)!,
                OriginalDllStore.Sha256);
            var result = OriginalLibraryLayout.Organize(originals);
            Require(result.Moved == 1 && result.Warnings.Count == 0, "legacy original was not organized");
            var selected = OriginalDllStore.RequireExistingOriginal(target, originals);
            Require(selected != saved && selected.Contains("After Effects 2020"), "restore did not discover the organized snapshot");
            foreach (var pair in hashes)
                Require(OriginalDllStore.Sha256(Path.Combine(Path.GetDirectoryName(selected)!, pair.Key!)) == pair.Value,
                    "organization changed snapshot bytes");
            var pointer = File.ReadAllText(Directory.GetFiles(Path.Combine(originals, "_active"), "*.json").Single());
            Require(pointer.Contains("After Effects 2020"), "active pointer was not updated");
            Require(OriginalLibraryLayout.Organize(originals).Moved == 0, "organization is not idempotent");
            File.SetAttributes(selected, FileAttributes.Normal);
            File.Delete(selected);
            File.Delete(target);
            var restored = OriginalDllStore.CreateRestoreDll(target, originals, Path.Combine(root, "restored.dll"));
            Require(OriginalDllStore.Sha256(restored) == hashes["dvaui.dll.adobe-original"], "recovery failed after migration");
        }
        finally { DeleteTestDirectory(root); }
    }

    private static void InterruptedLibraryMigration()
    {
        var root = NewTempDirectory("interrupted-library");
        try
        {
            var target = Path.Combine(root, @"Adobe After Effects 2023\Support Files\dvaui.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(typeof(OriginalDllStore).Assembly.Location, target);
            var originals = Path.Combine(root, "Originals");
            var active = CreateHistoricalSnapshot(originals, "1111111111111111", target, DateTimeOffset.UtcNow.AddDays(-1), false);
            CreateHistoricalSnapshot(originals, "2222222222222222", target, DateTimeOffset.UtcNow, true);
            OriginalDllStore.MarkActiveSnapshot(target, originals, active);
            var hash = OriginalDllStore.Sha256(active);
            var pointer = Directory.GetFiles(Path.Combine(originals, "_active"), "*.json").Single();
            var oldPointer = File.ReadAllBytes(pointer);
            Require(OriginalLibraryLayout.Organize(originals).Moved == 2, "two originals were not organized");
            File.WriteAllBytes(pointer, oldPointer); // Simulate a crash before active-pointer publication.
            Require(OriginalDllStore.Sha256(OriginalDllStore.RequireExistingOriginal(target, originals)) == hash,
                "stale pointer lost active provenance to a newer snapshot");
            OriginalLibraryLayout.Organize(originals);
            Require(File.ReadAllText(pointer).Contains("After Effects 2023"), "retry did not heal the pointer");
        }
        finally { DeleteTestDirectory(root); }
    }

    private static void LibraryMigrationConflicts()
    {
        var root = NewTempDirectory("conflicting-library");
        try
        {
            var target = Path.Combine(root, "dvaui.dll");
            File.Copy(typeof(OriginalDllStore).Assembly.Location, target);
            var originals = Path.Combine(root, "Originals");
            var saved = CreateHistoricalSnapshot(originals, "0123456789ABCDEF", target, DateTimeOffset.UtcNow, false);
            var destination = OriginalLibraryLayout.Destination(originals, target,
                FileVersionInfo.GetVersionInfo(target).FileVersion, "0123456789ABCDEF");
            Directory.CreateDirectory(destination);
            File.WriteAllText(Path.Combine(destination, "keep.txt"), "keep me");
            var broken = Path.Combine(originals, "0000000000000000");
            Directory.CreateDirectory(broken);
            File.WriteAllText(Path.Combine(broken, "snapshot.json"), "broken JSON");
            var pending = Path.Combine(originals, "_pending", "1111111111111111");
            Directory.CreateDirectory(pending);
            File.WriteAllText(Path.Combine(pending, "snapshot.json"), "{}");
            var result = OriginalLibraryLayout.Organize(originals);
            Require(result.Moved == 0 && result.Warnings.Count == 2, "conflicts were not reported and preserved");
            Require(File.Exists(saved) && File.ReadAllText(Path.Combine(destination, "keep.txt")) == "keep me", "conflict overwrote evidence");
            Require(!OriginalLibraryLayout.SnapshotDirectories(originals).Any(path => path.Contains("_pending")), "pending captures became selectable");
        }
        finally { DeleteTestDirectory(root); }
    }

    private static void MissingTargetLibraryMigration()
    {
        var root = NewTempDirectory("missing-target-library");
        try
        {
            var target = Path.Combine(root, @"Adobe After Effects CC 2019\Support Files\dvaui.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(typeof(OriginalDllStore).Assembly.Location, target);
            var originals = Path.Combine(root, "Originals");
            var saved = CreateHistoricalSnapshot(originals, "0123456789ABCDEF", target, DateTimeOffset.UtcNow, false);
            var hash = OriginalDllStore.Sha256(saved);
            File.Delete(target);
            Require(OriginalLibraryLayout.Organize(originals).Moved == 1, "missing target blocked organization");
            var selected = OriginalDllStore.RequireExistingOriginal(target, originals);
            Require(selected.Contains("After Effects CC 2019") && OriginalDllStore.Sha256(selected) == hash,
                "missing target lost its release label or original");
        }
        finally { DeleteTestDirectory(root); }
    }

    private static void VisibleRejectedSnapshotIsExplained()
    {
        var root = NewTempDirectory("visible-rejected-snapshot");
        try
        {
            var target = Path.Combine(root, @"Adobe After Effects 2025\Support Files\dvaui.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(typeof(OriginalDllStore).Assembly.Location, target);
            var originals = Path.Combine(root, "Originals");
            var snapshot = CreateHistoricalSnapshot(originals, "0123456789ABCDEF", target, DateTimeOffset.UtcNow, false);
            var proof = Path.Combine(Path.GetDirectoryName(snapshot)!, "snapshot.proof");
            File.SetAttributes(proof, FileAttributes.Normal);
            File.Delete(proof);
            var error = Capture(() => OriginalDllStore.RequireExistingOriginal(target, originals));
            Require(error is InvalidOperationException && error.Message.Contains("stored snapshot exists", StringComparison.OrdinalIgnoreCase) &&
                    error.Message.Contains(snapshot, StringComparison.OrdinalIgnoreCase) &&
                    error.Message.Contains("failed verification", StringComparison.OrdinalIgnoreCase),
                "visible rejected snapshot was misleadingly reported as absent");
        }
        finally { DeleteTestDirectory(root); }
    }

    private static void ImportBackupPreservesTarget()
    {
        var root = NewTempDirectory("import-original");
        try
        {
            var target = Path.Combine(root, "dvaui.dll");
            var clean = Path.Combine(root, "old-backup.dll");
            File.Copy(typeof(OriginalDllStore).Assembly.Location, target);
            File.Copy(target, clean);
            using (var stream = new FileStream(target, FileMode.Open, FileAccess.ReadWrite))
            { stream.Position = stream.Length - 1; stream.WriteByte(123); }
            var installedHash = OriginalDllStore.Sha256(target);
            var originals = Path.Combine(root, "Originals");
            var stored = OriginalDllStore.ImportVerifiedOriginal(target, originals, clean, path =>
            {
                Require(path != clean && path != target, "import did not verify the staged copy");
                return new("Adobe Test", "TEST");
            });
            Require(OriginalDllStore.Sha256(target) == installedHash, "import replaced the installed DLL");
            Require(OriginalDllStore.Sha256(stored) == OriginalDllStore.Sha256(clean), "import saved the themed target");
            Require(OriginalDllStore.RequireExistingOriginal(target, originals) == stored, "imported original was not selected");
            var restored = OriginalDllStore.CreateRestoreDll(target, originals, Path.Combine(root, "restore.dll"),
                _ => throw new InvalidDataException("themed target"));
            Require(OriginalDllStore.Sha256(restored) == OriginalDllStore.Sha256(clean), "restore did not use the recovered original");
        }
        finally { DeleteTestDirectory(root); }
    }

    private static void ImportBackupRejectsDifferentBuild()
    {
        var root = NewTempDirectory("import-wrong-build");
        try
        {
            var target = Path.Combine(root, "dvaui.dll");
            var backup = Path.Combine(root, "other-build.dll");
            File.Copy(typeof(OriginalDllStore).Assembly.Location, target);
            var bytes = File.ReadAllBytes(target);
            var header = BitConverter.ToInt32(bytes, 0x3C);
            bytes[header + 8] ^= 1; // Same version resource, different linker timestamp/build identity.
            File.WriteAllBytes(backup, bytes);
            var originals = Path.Combine(root, "Originals");
            var error = Capture(() => OriginalDllStore.ImportVerifiedOriginal(target, originals, backup, _ => new("Adobe Test", "TEST")));
            Require(error is InvalidDataException && error.Message.Contains("build layout"), "mismatched build was imported");
            Require(!Directory.EnumerateFiles(originals, "snapshot.json", SearchOption.AllDirectories).Any(), "rejected import published a snapshot");
        }
        finally { DeleteTestDirectory(root); }
    }

    private static void ImportBackupRejectsUnsigned()
    {
        var root = NewTempDirectory("import-unsigned");
        try
        {
            var target = Path.Combine(root, "dvaui.dll");
            File.Copy(typeof(OriginalDllStore).Assembly.Location, target);
            var originals = Path.Combine(root, "Originals");
            Require(Capture(() => OriginalDllStore.ImportVerifiedOriginal(target, originals, target)) is InvalidDataException,
                "unsigned backup bypassed Adobe validation");
            Require(!Directory.EnumerateFiles(originals, "snapshot.json", SearchOption.AllDirectories).Any(), "unsigned import published a snapshot");
        }
        finally { DeleteTestDirectory(root); }
    }

    private static void DllPaletteExtraction()
    {
        var fixture = CreateHybridDvauiFixture(false);
        var json = "{" + string.Join(',', Enumerable.Range(0, 8).Select(i => $"\"background-{i}\":\"rgb(38, 38, 38)\"")) +
            ",\"text\":\"rgb(240, 240, 240)\",\"accent\":\"rgb(80, 160, 240)\",\"hidden\":\"rgba(255, 0, 255, 0)\"}";
        fixture.Data.AsSpan(fixture.JsonOffset, fixture.JsonSize).Fill((byte)' ');
        Encoding.UTF8.GetBytes(json).CopyTo(fixture.Data, fixture.JsonOffset);
        var before = fixture.Data.ToArray();
        var extracted = ThemePatcher.ExtractThemeForTesting(fixture.Data, 14, "14.6-test");
        Require(extracted.Colors.Count == 3, "did not merge native and JSON colors or excluded transparency incorrectly");
        Require(extracted.Colors.Any(color => color.R == 80 && color.G == 160 && color.B == 240), "JSON accent missing");
        Require(before.SequenceEqual(fixture.Data), "extraction modified the source DLL");
        var target = CreateHybridDvauiFixture(true);
        var generated = ThemePatcher.GenerateForTesting(target.Data, 14, "14.6-other-target", extracted.Suggested);
        Require(generated.Length == target.Data.Length, "transfer changed target layout size");
        Require(Math.Abs(BitConverter.ToSingle(generated, target.NativeColorOffset) - extracted.Suggested.Panel.R / 255f) < .000001f,
            "extracted palette did not apply to the different target encoding");
    }

    private static void InvalidDllPaletteRejected()
    {
        var root = NewTempDirectory("invalid-dll-palette");
        try
        {
            var path = Path.Combine(root, "dvaui.dll");
            File.WriteAllText(path, "not a DLL #ABCDEF #123456");
            Require(Capture(() => ThemeImporter.Load(path)) is InvalidDataException, "invalid DLL was imported as plain text");
        }
        finally { DeleteTestDirectory(root); }
    }

    private static void DeleteTestDirectory(string root)
    {
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(root, true);
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
