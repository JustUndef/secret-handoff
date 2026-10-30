# Static validation only; actual EXE integration is covered by Build-Prototype.ps1.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
function Assert-Skill([bool]$Condition) {
    if (-not $Condition) { throw [InvalidOperationException]::new() }
}
try {
    $skill = Join-Path $root 'skills\secret-handoff'
    # Validate the deliberately simple YAML subset used here without an external YAML dependency.
    $entry = [IO.File]::ReadAllText((Join-Path $skill 'SKILL.md'))
    $frontmatter = [regex]::Match($entry, '\A---\r?\nname: (secret-handoff)\r?\ndescription: ([^\r\n]+)\r?\n---\r?\n')
    Assert-Skill $frontmatter.Success
    $description = $frontmatter.Groups[2].Value
    Assert-Skill ($description.Length -gt 0 -and $description.Length -le 1024 -and $description -notmatch '[:#<>\[\]{}]')
    $policy = [IO.File]::ReadAllText((Join-Path $skill 'agents\openai.yaml'))
    Assert-Skill ($policy -match '\Apolicy:\r?\n  allow_implicit_invocation: true\r?\n?\z')
    foreach ($document in (Get-ChildItem -LiteralPath $skill -Recurse -File -Filter '*.md')) {
        $body = [IO.File]::ReadAllText($document.FullName)
        Assert-Skill (($body -split '(?m)^```').Count % 2 -eq 1)
        foreach ($link in [regex]::Matches($body, '\[[^\]]+\]\(([^)]+)\)')) {
            $target = $link.Groups[1].Value
            if ($target -match '^https://') { continue }
            $resolved = [IO.Path]::GetFullPath((Join-Path $document.DirectoryName $target))
            Assert-Skill ($resolved.StartsWith($skill + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase))
            Assert-Skill ([IO.File]::Exists($resolved))
        }
    }
    Write-Output 'PASS SKILL_METADATA_STRUCTURE_AND_REFERENCES'
    $files = @(Get-ChildItem -LiteralPath $skill -Recurse -File)
    Assert-Skill ($files.Count -eq 4)
    Assert-Skill (@($files | Where-Object Extension -NotIn '.md', '.yaml').Count -eq 0)
    Assert-Skill ($entry.Contains('Output contract: passthrough') -and -not $entry.Contains('Import-Module'))
    Write-Output 'PASS SKILL_DESCRIPTION_ONLY'
    Write-Output 'ALL_SKILL_TESTS_PASSED'
} catch {
    Write-Output 'SKILL_TESTS_FAILED'
    exit 1
}
