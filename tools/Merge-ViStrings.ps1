# Adds the Vietnamese strings of UC1 (Place and Manage Orders) to SharedResource.vi.resx.
# Strings whose name already exists in the resx are skipped, so it is safe to run twice.
# Run from the repository root (the folder that contains SaigonBite\):
#     powershell -ExecutionPolicy Bypass -File .\tools\Merge-ViStrings.ps1
param(
    [string]$Resx = ".\SaigonBite\Resources\SharedResource.vi.resx",
    [string]$Strings = ".\SaigonBite\Resources\uc1-vi-strings.xml"
)

$resxPath = (Resolve-Path $Resx).Path
[xml]$doc = Get-Content -Raw -Encoding UTF8 $resxPath
[xml]$new = "<root>" + (Get-Content -Raw -Encoding UTF8 $Strings) + "</root>"

$existing = @{}
foreach ($d in $doc.root.data) { $existing[$d.name] = $true }

$added = 0
foreach ($d in $new.root.data) {
    if (-not $existing.ContainsKey($d.name)) {
        $node = $doc.ImportNode($d, $true)
        [void]$doc.root.AppendChild($node)
        $added++
    }
}

$doc.Save($resxPath)
Write-Host "Added $added new strings to $resxPath"
