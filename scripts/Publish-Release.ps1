param(
    [Parameter(Mandatory = $true)] [string] $Tag,
    [Parameter(Mandatory = $true)] [string] $Version,
    [Parameter(Mandatory = $true)] [string] $ArtifactDirectory
)

$ErrorActionPreference = 'Stop'
if ($Version -cnotmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$' -or $Tag -cne "v$Version") {
    throw 'Release tag and stable version must match.'
}
$installerName = "AfterThemed-Setup-$Version.exe"
$installer = Join-Path $ArtifactDirectory $installerName
$checksumFile = Join-Path $ArtifactDirectory 'SHA256SUMS.txt'
$assets = @($installer, $checksumFile, (Join-Path $ArtifactDirectory 'EULA.txt'), (Join-Path $ArtifactDirectory 'LICENSE.txt'))
foreach ($asset in $assets) {
    if (-not (Test-Path -LiteralPath $asset -PathType Leaf)) { throw "Release asset missing: $asset" }
}
$expected = (Get-Content -LiteralPath $checksumFile -Raw).Trim()
$actual = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
if ($expected -cne "$actual  $installerName") { throw 'Installer SHA-256 does not match the release checksum.' }

# Drafts keep update checks from seeing a release before every asset is uploaded.
# A failed upload can be retried; a published release is never overwritten.
$existing = gh release view $Tag --json isDraft 2>$null
if ($LASTEXITCODE -eq 0) {
    if (($existing | ConvertFrom-Json).isDraft -ne $true) {
        throw "Release $Tag is already published. Use a new version instead of replacing its binaries."
    }
    gh release upload $Tag @assets --clobber
    if ($LASTEXITCODE -ne 0) { throw 'Release upload failed; the release remains a draft.' }
}
else {
    gh release create $Tag @assets --draft --verify-tag --generate-notes --title "AfterThemed $Version"
    if ($LASTEXITCODE -ne 0) { throw 'Release creation failed; no release was published.' }
}

gh release edit $Tag --draft=false
if ($LASTEXITCODE -ne 0) { throw 'Unable to publish the completed draft release.' }
Write-Host "Published AfterThemed $Version."
