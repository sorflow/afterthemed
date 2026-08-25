param(
    [Parameter(Mandatory = $true)]
    [string] $InnoCompiler
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$installerScript = Join-Path $repositoryRoot 'src\AfterThemed\Installer\AfterThemed.iss'
$publishedExecutable = Join-Path $repositoryRoot 'src\AfterThemed\artifacts\publish\win-x64\DVAUI Theme Editor.exe'
if (-not (Test-Path -LiteralPath $InnoCompiler -PathType Leaf)) {
    throw "Inno Setup compiler was not found: $InnoCompiler"
}
if (-not (Test-Path -LiteralPath $publishedExecutable -PathType Leaf)) {
    throw "Publish the application before running the installer upgrade test: $publishedExecutable"
}

$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
$testRoot = Join-Path $temporaryRoot ("AfterThemed-installer-upgrade-" + [Guid]::NewGuid().ToString('N'))
$testRoot = [IO.Path]::GetFullPath($testRoot)
if (-not $testRoot.StartsWith($temporaryRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to use a test directory outside the temporary root: $testRoot"
}

$buildRoot = Join-Path $testRoot 'build'
$installRoot = Join-Path $testRoot 'installed'
$setupLog = Join-Path $testRoot 'upgrade-setup.log'
$appId = '{{' + [Guid]::NewGuid().ToString().ToUpperInvariant() + '}'
$uninstallKeyName = $appId.Substring(1) + '_is1'
$mutexName = 'AfterThemed.Integration.' + [Guid]::NewGuid().ToString('N')
$registryPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$uninstallKeyName"

function Invoke-CheckedProcess {
    param(
        [string] $FilePath,
        [string[]] $ArgumentList
    )

    $process = Start-Process -FilePath $FilePath -ArgumentList $ArgumentList `
        -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        throw "$FilePath exited with code $($process.ExitCode)."
    }
}

function Build-TestInstaller {
    param(
        [string] $Version,
        [string] $OutputName
    )

    Invoke-CheckedProcess $InnoCompiler @(
        '/Qp',
        "/DMyAppVersion=$Version",
        "/DMyAppId=$appId",
        "/DMyAppUninstallKey=$uninstallKeyName",
        "/DMyAppDefaultDir=$installRoot",
        "/DMyAppMutex=$mutexName",
        "/O$buildRoot",
        "/F$OutputName",
        ('"' + $installerScript + '"')
    )
}

try {
    New-Item -ItemType Directory -Path $buildRoot -Force | Out-Null
    Build-TestInstaller '1.3.11' 'AfterThemed-Setup-old'
    Build-TestInstaller '1.3.12' 'AfterThemed-Setup-new'

    Invoke-CheckedProcess (Join-Path $buildRoot 'AfterThemed-Setup-old.exe') @(
        '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART'
    )
    $oldRegistration = Get-ItemProperty -LiteralPath $registryPath
    if ($oldRegistration.DisplayVersion -ne '1.3.11') {
        throw "Expected the synthetic old registration to be 1.3.11; got $($oldRegistration.DisplayVersion)."
    }

    Invoke-CheckedProcess (Join-Path $buildRoot 'AfterThemed-Setup-new.exe') @(
        '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/LOG=$setupLog"
    )

    $newRegistration = Get-ItemProperty -LiteralPath $registryPath
    if ($newRegistration.DisplayVersion -ne '1.3.12') {
        throw "Expected the upgraded registration to be 1.3.12; got $($newRegistration.DisplayVersion)."
    }
    $uninstallers = @(Get-ChildItem -LiteralPath $installRoot -Filter 'unins*.exe')
    if ($uninstallers.Count -ne 1) {
        throw "Expected one current uninstaller after upgrade; found $($uninstallers.Count)."
    }
    if (-not (Select-String -LiteralPath $setupLog -SimpleMatch 'Removing AfterThemed 1.3.11 before installing 1.3.12.' -Quiet)) {
        throw 'The setup log does not show the previous-version uninstall path.'
    }

    Write-Host 'PASS: installer removed 1.3.11, installed 1.3.12, and left one current uninstaller.'
}
finally {
    if (Test-Path -LiteralPath $registryPath) {
        $uninstallCommand = (Get-ItemProperty -LiteralPath $registryPath).UninstallString
        $uninstallExecutable = $uninstallCommand.Trim().Trim('"')
        if (Test-Path -LiteralPath $uninstallExecutable -PathType Leaf) {
            Invoke-CheckedProcess $uninstallExecutable @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART')
        }
    }

    if (Test-Path -LiteralPath $testRoot) {
        $resolvedTestRoot = [IO.Path]::GetFullPath($testRoot)
        if (-not $resolvedTestRoot.StartsWith($temporaryRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove a test directory outside the temporary root: $resolvedTestRoot"
        }
        Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force
    }
}
