[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$RootPath = $PSScriptRoot,
    [switch]$IncludeNestedWorkspaceArtifacts = $true
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

$resolvedRoot = (Resolve-Path -Path $RootPath).Path
$projectDirs = Get-ChildItem -Path $resolvedRoot -Directory | Where-Object {
    @(Get-ChildItem -Path $_.FullName -Filter *.csproj -File -ErrorAction SilentlyContinue).Count -gt 0
}

if (-not $projectDirs) {
    Write-Host "No project directories with .csproj files were found under $resolvedRoot."
    return
}

$artifactNames = @('bin', 'obj', 'build_output')
$removedPaths = [System.Collections.Generic.List[string]]::new()

foreach ($projectDir in $projectDirs) {
    foreach ($artifactName in $artifactNames) {
        $artifactPath = Join-Path $projectDir.FullName $artifactName
        if (-not (Test-Path -Path $artifactPath -PathType Container)) {
            continue
        }

        if ($PSCmdlet.ShouldProcess($artifactPath, 'Remove generated build artifacts')) {
            Remove-Item -Path $artifactPath -Recurse -Force
            $removedPaths.Add($artifactPath)
        }
    }

    if (-not $IncludeNestedWorkspaceArtifacts) {
        continue
    }

    $nestedWorkspacePath = Join-Path $projectDir.FullName 'NaitsSmsApi'
    $nestedCopilotBuildPath = Join-Path $nestedWorkspacePath '_copilot_build'
    $containsProjectFile = Get-ChildItem -Path $nestedWorkspacePath -Filter *.csproj -File -Recurse -ErrorAction SilentlyContinue |
        Select-Object -First 1

    if ((Test-Path -Path $nestedCopilotBuildPath -PathType Container) -and -not $containsProjectFile) {
        if ($PSCmdlet.ShouldProcess($nestedWorkspacePath, 'Remove nested workspace build artifacts')) {
            Remove-Item -Path $nestedWorkspacePath -Recurse -Force
            $removedPaths.Add($nestedWorkspacePath)
        }
    }
}

Write-Host "Project root: $resolvedRoot"
Write-Host "Projects scanned: $($projectDirs.Count)"
Write-Host "Removed paths: $($removedPaths.Count)"

if ($removedPaths.Count -eq 0) {
    Write-Host 'Nothing to remove.'
    return
}

$removedPaths | ForEach-Object {
    Write-Host " - $_"
}