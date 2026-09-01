[CmdletBinding()]
param(
    [switch]$WriteCoverage,
    [string]$BaseRef
)

$ErrorActionPreference = 'Stop'

function Convert-ToAtlasPath {
    param([Parameter(Mandatory = $true)][string]$Path)
    return $Path.Replace('\', '/').TrimStart('.', '/')
}

function Get-RepoRelativePath {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $fullRoot = [System.IO.Path]::GetFullPath($Root).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    $fullPath = [System.IO.Path]::GetFullPath($Path)

    if ($fullPath.StartsWith($fullRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        return Convert-ToAtlasPath ($fullPath.Substring($fullRoot.Length))
    }

    $rootUri = [System.Uri]::new($fullRoot)
    $pathUri = [System.Uri]::new($fullPath)
    $relativeUri = $rootUri.MakeRelativeUri($pathUri)
    return Convert-ToAtlasPath ([System.Uri]::UnescapeDataString($relativeUri.ToString()))
}

$atlasRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $atlasRoot '..'))
$configPath = Join-Path $atlasRoot 'SYSTEMS.json'
$exclusionsPath = Join-Path $atlasRoot 'EXCLUSIONS.md'

if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) {
    throw "Atlas router not found: $configPath"
}

$config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
$errors = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()
$assignments = [System.Collections.Generic.List[object]]::new()
$excludedAssignments = [System.Collections.Generic.List[string]]::new()

$excluded = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($path in @($config.excludedFiles)) {
    $normalized = Convert-ToAtlasPath ([string]$path)
    [void]$excluded.Add($normalized)

    $absolute = Join-Path $repoRoot $normalized
    if (-not (Test-Path -LiteralPath $absolute -PathType Leaf)) {
        $errors.Add("Excluded file does not exist: $normalized")
    }
}

$sourceFiles = [System.Collections.Generic.List[string]]::new()
foreach ($rootEntry in @($config.coverageRoots)) {
    $normalizedRoot = Convert-ToAtlasPath ([string]$rootEntry)
    $absoluteRoot = Join-Path $repoRoot $normalizedRoot

    if (-not (Test-Path -LiteralPath $absoluteRoot -PathType Container)) {
        $errors.Add("Coverage root does not exist: $normalizedRoot")
        continue
    }

    foreach ($file in Get-ChildItem -LiteralPath $absoluteRoot -Recurse -File -Filter '*.cs') {
        $sourceFiles.Add((Get-RepoRelativePath -Root $repoRoot -Path $file.FullName))
    }
}

$sourceFiles = @($sourceFiles | Sort-Object -Unique)

foreach ($sourceFile in $sourceFiles) {
    if ($excluded.Contains($sourceFile)) {
        $excludedAssignments.Add($sourceFile)
        continue
    }

    $matches = [System.Collections.Generic.List[object]]::new()
    foreach ($system in @($config.systems)) {
        $isMatch = $false

        foreach ($fileEntry in @($system.files)) {
            if ($sourceFile.Equals((Convert-ToAtlasPath ([string]$fileEntry)), [System.StringComparison]::OrdinalIgnoreCase)) {
                $isMatch = $true
                break
            }
        }

        if (-not $isMatch) {
            foreach ($prefixEntry in @($system.prefixes)) {
                $prefix = Convert-ToAtlasPath ([string]$prefixEntry)
                if ($sourceFile.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                    $isMatch = $true
                    break
                }
            }
        }

        if ($isMatch) {
            $matches.Add($system)
        }
    }

    if ($matches.Count -eq 0) {
        $errors.Add("Unowned first-party C# file: $sourceFile")
        continue
    }

    if ($matches.Count -gt 1) {
        $ids = @($matches | ForEach-Object { $_.id }) -join ', '
        $errors.Add("Ambiguous Atlas ownership for $sourceFile`: $ids")
        continue
    }

    $assignments.Add([pscustomobject]@{
        Path = $sourceFile
        SystemId = [string]$matches[0].id
        Page = Convert-ToAtlasPath ([string]$matches[0].page)
    })
}

foreach ($system in @($config.systems)) {
    $page = Convert-ToAtlasPath ([string]$system.page)
    $pageAbsolute = Join-Path $repoRoot $page
    if (-not (Test-Path -LiteralPath $pageAbsolute -PathType Leaf)) {
        $errors.Add("System '$($system.id)' points to a missing page: $page")
    }
}

if (-not (Test-Path -LiteralPath $exclusionsPath -PathType Leaf)) {
    $errors.Add('ProjectAtlas/EXCLUSIONS.md is missing.')
}
else {
    $exclusionText = Get-Content -LiteralPath $exclusionsPath -Raw
    foreach ($excludedFile in @($excluded | Sort-Object)) {
        if ($exclusionText.IndexOf($excludedFile, [System.StringComparison]::OrdinalIgnoreCase) -lt 0) {
            $errors.Add("Excluded file is not documented in EXCLUSIONS.md: $excludedFile")
        }
    }
}

$markdownFiles = Get-ChildItem -LiteralPath $atlasRoot -Recurse -File -Filter '*.md'
foreach ($markdownFile in $markdownFiles) {
    $text = Get-Content -LiteralPath $markdownFile.FullName -Raw

    foreach ($match in [regex]::Matches($text, '\[[^\]]+\]\((?<target>[^)]+)\)')) {
        $target = $match.Groups['target'].Value.Trim().Trim('<', '>')
        if ($target -match '^(https?://|mailto:|#)') { continue }

        $targetWithoutAnchor = ($target -split '#', 2)[0]
        if ([string]::IsNullOrWhiteSpace($targetWithoutAnchor)) { continue }

        $resolved = [System.IO.Path]::GetFullPath((Join-Path $markdownFile.DirectoryName $targetWithoutAnchor))
        if (-not (Test-Path -LiteralPath $resolved)) {
            $relativeMarkdown = Get-RepoRelativePath -Root $repoRoot -Path $markdownFile.FullName
            $errors.Add("Broken Markdown link in $relativeMarkdown`: $target")
        }
    }

    foreach ($match in [regex]::Matches($text, '`(?<path>Assets/[^`\r\n]+)`')) {
        $assetPath = $match.Groups['path'].Value.TrimEnd('/')
        $assetAbsolute = Join-Path $repoRoot $assetPath
        if (-not (Test-Path -LiteralPath $assetAbsolute)) {
            $relativeMarkdown = Get-RepoRelativePath -Root $repoRoot -Path $markdownFile.FullName
            $errors.Add("Missing backticked asset path in $relativeMarkdown`: $assetPath")
        }
    }
}

if (-not [string]::IsNullOrWhiteSpace($BaseRef)) {
    $changedOutput = & git -C $repoRoot diff --name-only $BaseRef -- 2>&1
    if ($LASTEXITCODE -ne 0) {
        $errors.Add("Could not compare Atlas impact with '$BaseRef': $($changedOutput -join ' ')")
    }
    else {
        $changed = @($changedOutput | ForEach-Object { Convert-ToAtlasPath ([string]$_) })
        $changedSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
        foreach ($changedPath in $changed) {
            [void]$changedSet.Add($changedPath)
        }

        $affectedPages = @(
            $assignments |
                Where-Object { $changedSet.Contains($_.Path) } |
                Select-Object -ExpandProperty Page -Unique
        )

        foreach ($page in $affectedPages) {
            if (-not $changedSet.Contains($page)) {
                $errors.Add("Changed implementation maps to an unchanged Atlas page since $BaseRef`: $page")
            }
        }
    }
}

if ($WriteCoverage) {
    $generatedRoot = Join-Path $atlasRoot 'Generated'
    if (-not (Test-Path -LiteralPath $generatedRoot -PathType Container)) {
        [void](New-Item -ItemType Directory -Path $generatedRoot)
    }

    $commit = (& git -C $repoRoot rev-parse --short HEAD 2>$null)
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($commit)) { $commit = 'unknown' }

    $coverageLines = [System.Collections.Generic.List[string]]::new()
    $coverageLines.Add('# Atlas C# coverage')
    $coverageLines.Add('')
    $coverageLines.Add(('Generated by `ProjectAtlas/Tools/Validate-Atlas.ps1` on {0} at commit `{1}`.' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz'), $commit))
    $coverageLines.Add('')
    $coverageLines.Add("- First-party C# files: $($sourceFiles.Count)")
    $coverageLines.Add("- Routed: $($assignments.Count)")
    $coverageLines.Add("- Deliberately excluded: $($excludedAssignments.Count)")
    $coverageLines.Add("- Errors at generation: $($errors.Count)")
    $coverageLines.Add('')
    $coverageLines.Add('| System | Page | C# files |')
    $coverageLines.Add('|---|---|---:|')

    foreach ($system in @($config.systems)) {
        $count = @($assignments | Where-Object { $_.SystemId -eq [string]$system.id }).Count
        $page = Convert-ToAtlasPath ([string]$system.page)
        $relativePage = $page.Substring('ProjectAtlas/'.Length)
        $coverageLines.Add("| $($system.id) | [$relativePage](../$relativePage) | $count |")
    }

    $coverageLines.Add('')
    $coverageLines.Add('Excluded-file evidence is maintained in [EXCLUSIONS.md](../EXCLUSIONS.md).')

    Set-Content -LiteralPath (Join-Path $generatedRoot 'coverage.md') -Value $coverageLines -Encoding utf8
}

foreach ($warning in $warnings) {
    Write-Warning $warning
}

if ($errors.Count -gt 0) {
    foreach ($validationError in $errors) {
        Write-Host "ERROR: $validationError" -ForegroundColor Red
    }
    Write-Error "Atlas validation failed with $($errors.Count) error(s)."
    exit 1
}

Write-Host "Atlas validation passed: $($assignments.Count) routed, $($excludedAssignments.Count) excluded, $($sourceFiles.Count) total first-party C# files."
