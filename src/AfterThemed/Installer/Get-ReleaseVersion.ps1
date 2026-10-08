param(
    [string] $Tag,
    [string] $ProjectFile = (Join-Path $PSScriptRoot '..\DvauiThemeEditor.csproj')
)

$ErrorActionPreference = 'Stop'
$project = [xml](Get-Content -LiteralPath $ProjectFile -Raw)
$version = [string]$project.Project.PropertyGroup.Version
if ($version -cnotmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$') {
    throw "Project Version must be a stable major.minor.patch version; found '$version'."
}
# Windows file-version components are unsigned 16-bit integers.
if (@($version.Split('.') | Where-Object { [decimal]$_ -gt 65535 }).Count -gt 0) {
    throw "Project Version components must not exceed 65535; found '$version'."
}
if ($Tag -and $Tag -cne "v$version") {
    throw "Release tag '$Tag' does not match project Version 'v$version'."
}
return $version
