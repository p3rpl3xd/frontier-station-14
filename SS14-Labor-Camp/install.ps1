# SPDX-FileCopyrightText: 2026 p3rpl3xd
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Copy the labor camp pack into an SS14 fork and import it from Directory.Build.targets.
# Safe to run again. A destination file whose text contains LABORCAMP-LOCAL is left alone.
# Written for Windows PowerShell 5.1: no &&, no ternary, Join-Path of two arguments.
#
# Usage: powershell -File .\install.ps1 -Target C:\fork [-Ref v1.0.0]

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Target,
    [Parameter(Mandatory = $false, Position = 1)]
    [string]$Ref
)

$ErrorActionPreference = "Stop"
$Utf8 = New-Object System.Text.UTF8Encoding $false
$script:Kept = 0

function Test-LocalAdaptation {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) {
        return $false
    }
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        $text = $Utf8.GetString($bytes, 3, $bytes.Length - 3)
    } else {
        $text = $Utf8.GetString($bytes)
    }
    if ($text.IndexOf([char]0) -ge 0) {
        return $false
    }
    return $text.Contains("LABORCAMP-LOCAL")
}

function Copy-One {
    param(
        [string]$Source,
        [string]$Destination
    )
    $parent = [System.IO.Path]::GetDirectoryName($Destination)
    if (-not (Test-Path -LiteralPath $parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
    if (Test-LocalAdaptation -Path $Destination) {
        Write-Output "Kept local adaptation $Destination"
        $script:Kept = $script:Kept + 1
        return
    }
    Copy-Item -LiteralPath $Source -Destination $Destination -Force
}

function Copy-Tree {
    param(
        [string]$Source,
        [string]$Destination
    )
    if (-not (Test-Path -LiteralPath $Source)) {
        throw "Missing source folder $Source"
    }
    $files = Get-ChildItem -LiteralPath $Source -Recurse -File -Force
    foreach ($file in $files) {
        $rel = $file.FullName.Substring($Source.Length).TrimStart('\', '/')
        if ($rel -like ".git*") {
            continue
        }
        $dest = [System.IO.Path]::Combine($Destination, $rel)
        Copy-One -Source $file.FullName -Destination $dest
    }
}

function Get-TextNewline {
    param([string]$Text)
    if ($Text.Contains("`r`n")) {
        return "`r`n"
    }
    return "`n"
}

function Write-Text {
    param(
        [string]$Path,
        [string]$Text
    )
    [System.IO.File]::WriteAllText($Path, $Text, $Utf8)
}

if (-not (Test-Path -LiteralPath $Target)) {
    throw "Fork root does not exist: $Target"
}
$Target = (Resolve-Path -LiteralPath $Target).Path
$serverProject = [System.IO.Path]::Combine($Target, "Content.Server", "Content.Server.csproj")
if (-not (Test-Path -LiteralPath $serverProject)) {
    throw "No Content.Server project at $Target. Pass the root of an SS14 fork."
}

$root = $PSScriptRoot
if (-not $root) {
    throw "Run this file with powershell -File so the script path is known."
}

$source = $root
$temp = $null
if ($Ref) {
    $check = & git -C $root rev-parse --verify --quiet ($Ref + "^{commit}")
    if ($LASTEXITCODE -ne 0) {
        throw "Git ref $Ref was not found in $root. Fetch it with: git fetch --tags origin"
    }
    $temp = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "labor-camp-" + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $temp -Force | Out-Null
    $zip = [System.IO.Path]::Combine($temp, "src.zip")
    & git -C $root archive --format=zip --output=$zip $Ref
    if ($LASTEXITCODE -ne 0) {
        throw "git archive $Ref failed."
    }
    $source = [System.IO.Path]::Combine($temp, "src")
    Expand-Archive -LiteralPath $zip -DestinationPath $source
}

Write-Output "Copying labor camp into $Target"
Copy-Tree -Source ([System.IO.Path]::Combine($source, "Content.LaborCamp.Shared")) -Destination ([System.IO.Path]::Combine($Target, "Content.LaborCamp.Shared"))
Copy-Tree -Source ([System.IO.Path]::Combine($source, "Content.LaborCamp.Server")) -Destination ([System.IO.Path]::Combine($Target, "Content.LaborCamp.Server"))
Copy-Tree -Source ([System.IO.Path]::Combine($source, "Content.LaborCamp.Client")) -Destination ([System.IO.Path]::Combine($Target, "Content.LaborCamp.Client"))
$tests = [System.IO.Path]::Combine($source, "Content.LaborCamp.Tests")
if (Test-Path -LiteralPath $tests) {
    Copy-Tree -Source $tests -Destination ([System.IO.Path]::Combine($Target, "Content.LaborCamp.Tests"))
}
Copy-Tree -Source ([System.IO.Path]::Combine($source, "Resources", "Locale", "en-US", "_LaborCamp")) -Destination ([System.IO.Path]::Combine($Target, "Resources", "Locale", "en-US", "_LaborCamp"))
Copy-Tree -Source ([System.IO.Path]::Combine($source, "Resources", "Prototypes", "_LaborCamp")) -Destination ([System.IO.Path]::Combine($Target, "Resources", "Prototypes", "_LaborCamp"))
Copy-Tree -Source ([System.IO.Path]::Combine($source, "Resources", "Textures", "_LaborCamp")) -Destination ([System.IO.Path]::Combine($Target, "Resources", "Textures", "_LaborCamp"))
$preset = [System.IO.Path]::Combine($source, "Resources", "ConfigPresets", "LaborCamp")
if (Test-Path -LiteralPath $preset) {
    Copy-Tree -Source $preset -Destination ([System.IO.Path]::Combine($Target, "Resources", "ConfigPresets", "LaborCamp"))
}
if ($temp) {
    Remove-Item -LiteralPath $temp -Recurse -Force
}

$marker = "Content.LaborCamp.Shared/Content.LaborCamp.Install.targets"
$import = '  <Import Project="$(MSBuildThisFileDirectory)Content.LaborCamp.Shared/Content.LaborCamp.Install.targets" Condition="Exists(' + "'" + '$(MSBuildThisFileDirectory)Content.LaborCamp.Shared/Content.LaborCamp.Install.targets' + "'" + ')" />'
$targetsPath = [System.IO.Path]::Combine($Target, "Directory.Build.targets")

if (-not (Test-Path -LiteralPath $targetsPath)) {
    Write-Text -Path $targetsPath -Text ("<Project>`r`n" + $import + "`r`n</Project>`r`n")
    Write-Output "Created Directory.Build.targets"
} else {
    $targetsText = [System.IO.File]::ReadAllText($targetsPath)
    if ($targetsText.Contains($marker)) {
        Write-Output "Directory.Build.targets already imports labor camp"
    } else {
        $closeAt = $targetsText.LastIndexOf("</Project>")
        if ($closeAt -lt 0) {
            throw "Directory.Build.targets has no </Project> element. Add the labor camp Import by hand."
        }
        $lineAt = $targetsText.LastIndexOf("`n", $closeAt)
        if ($lineAt -lt 0) {
            $lineAt = 0
        } else {
            $lineAt = $lineAt + 1
        }
        $nl = Get-TextNewline -Text $targetsText
        $targetsText = $targetsText.Insert($lineAt, $import + $nl)
        Write-Text -Path $targetsPath -Text $targetsText
        Write-Output "Patched Directory.Build.targets"
    }
}

Write-Output "Done. Kept $script:Kept local adaptation(s). Rebuild the client and the server."
Write-Output "Do not add a project reference to the labor camp folders."
