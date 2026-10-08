$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$versionScript = Join-Path $repositoryRoot 'src\AfterThemed\Installer\Get-ReleaseVersion.ps1'
$publishScript = Join-Path $repositoryRoot 'scripts\Publish-Release.ps1'
$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
$testRoot = Join-Path $temporaryRoot ("AfterThemed-release-tests-" + [Guid]::NewGuid().ToString('N'))
$testRoot = [IO.Path]::GetFullPath($testRoot)
if (-not $testRoot.StartsWith($temporaryRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to use a test directory outside the temporary root: $testRoot"
}

function Assert-Throws([scriptblock] $Action, [string] $ExpectedMessage) {
    try { & $Action | Out-Null }
    catch {
        if ($_.Exception.Message -notlike "*$ExpectedMessage*") { throw }
        return
    }
    throw "Expected failure containing: $ExpectedMessage"
}

# Shadow the CLI within this test scope. These tests cannot publish a real release.
function gh {
    $command = "$($args[0]) $($args[1])"
    $releaseTestState.Commands.Add($command)
    $global:LASTEXITCODE = 0
    switch ($command) {
        'release view' {
            if ($releaseTestState.State -eq 'missing') { $global:LASTEXITCODE = 1 }
            else { @{ isDraft = ($releaseTestState.State -eq 'draft') } | ConvertTo-Json -Compress }
        }
        'release create' {
            if ($args -notcontains '--draft' -or $args -notcontains '--verify-tag') { throw 'Unsafe release creation.' }
            if ($releaseTestState.FailCommand -eq $command) { $global:LASTEXITCODE = 1 }
        }
        'release upload' { if ($releaseTestState.FailCommand -eq $command) { $global:LASTEXITCODE = 1 } }
        'release edit' {
            if ($args -notcontains '--draft=false') { throw 'Unexpected publish arguments.' }
            if ($releaseTestState.FailCommand -eq $command) { $global:LASTEXITCODE = 1 }
        }
        default { throw "Unexpected GitHub command: $command" }
    }
}

try {
    New-Item -ItemType Directory -Path $testRoot | Out-Null
    $projectFile = Join-Path $testRoot 'test.csproj'
    Set-Content -LiteralPath $projectFile -Value '<Project><PropertyGroup><Version>2.4.6</Version></PropertyGroup></Project>'
    if ((& $versionScript -ProjectFile $projectFile -Tag 'v2.4.6') -cne '2.4.6') { throw 'Wrong release version.' }
    Assert-Throws { & $versionScript -ProjectFile $projectFile -Tag 'v2.4.7' } 'does not match'
    Assert-Throws { & $versionScript -ProjectFile $projectFile -Tag 'v2.4.6-beta' } 'does not match'
    Set-Content -LiteralPath $projectFile -Value '<Project><PropertyGroup><Version>2.4.6-beta</Version></PropertyGroup></Project>'
    Assert-Throws { & $versionScript -ProjectFile $projectFile } 'stable major.minor.patch'
    Set-Content -LiteralPath $projectFile -Value '<Project><PropertyGroup><Version>2.4.65536</Version></PropertyGroup></Project>'
    Assert-Throws { & $versionScript -ProjectFile $projectFile } 'must not exceed'

    $installer = Join-Path $testRoot 'AfterThemed-Setup-2.4.6.exe'
    Set-Content -LiteralPath $installer -Value 'test fixture, not executable'
    Set-Content -LiteralPath (Join-Path $testRoot 'EULA.txt') -Value 'test EULA'
    Set-Content -LiteralPath (Join-Path $testRoot 'LICENSE.txt') -Value 'test license'
    $checksumFile = Join-Path $testRoot 'SHA256SUMS.txt'
    $hash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -LiteralPath $checksumFile -Value "$hash  AfterThemed-Setup-2.4.6.exe"
    $commands = [Collections.Generic.List[string]]::new()
    $releaseTestState = @{ Commands = $commands; FailCommand = ''; State = 'missing' }
    & $publishScript -Tag 'v2.4.6' -Version '2.4.6' -ArtifactDirectory $testRoot
    if (($commands -join ',') -ne 'release view,release create,release edit') { throw 'Fresh release order is wrong.' }

    $commands.Clear()
    $releaseTestState.State = 'draft'
    & $publishScript -Tag 'v2.4.6' -Version '2.4.6' -ArtifactDirectory $testRoot
    if (($commands -join ',') -ne 'release view,release upload,release edit') { throw 'Draft retry order is wrong.' }

    $commands.Clear()
    $releaseTestState.State = 'published'
    Assert-Throws { & $publishScript -Tag 'v2.4.6' -Version '2.4.6' -ArtifactDirectory $testRoot } 'already published'
    if ($commands.Count -ne 1) { throw 'A published release was modified.' }

    foreach ($failure in @('release create', 'release upload', 'release edit')) {
        $commands.Clear()
        $releaseTestState.State = if ($failure -eq 'release create') { 'missing' } else { 'draft' }
        $releaseTestState.FailCommand = $failure
        Assert-Throws { & $publishScript -Tag 'v2.4.6' -Version '2.4.6' -ArtifactDirectory $testRoot } ''
        if ($failure -ne 'release edit' -and $commands.Contains('release edit')) { throw 'Published after an asset failure.' }
    }

    $commands.Clear()
    Assert-Throws { & $publishScript -Tag 'v2.4.7' -Version '2.4.6' -ArtifactDirectory $testRoot } 'must match'
    Set-Content -LiteralPath $installer -Value 'changed bytes'
    Assert-Throws { & $publishScript -Tag 'v2.4.6' -Version '2.4.6' -ArtifactDirectory $testRoot } 'SHA-256'
    Remove-Item -LiteralPath $installer
    Assert-Throws { & $publishScript -Tag 'v2.4.6' -Version '2.4.6' -ArtifactDirectory $testRoot } 'asset missing'
    if ($commands.Count -ne 0) { throw 'Invalid release inputs reached GitHub.' }
    Write-Host 'PASS: release version validation, checksums, draft publication, retries, and failure handling'
}
finally {
    Remove-Item -LiteralPath $testRoot -Recurse -Force
    $global:LASTEXITCODE = 0
}
