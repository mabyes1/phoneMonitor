[CmdletBinding()]
param(
    [string]$LocaleRoot
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($LocaleRoot)) {
    $LocaleRoot = Join-Path $PSScriptRoot "..\src\PhoneMonitor.Host\wwwroot\locales"
}

function Get-LeafKeys {
    param(
        [Parameter(Mandatory = $true)]$Value,
        [string]$Prefix = ""
    )

    if ($Value -is [System.Collections.IDictionary] -or $Value -is [PSCustomObject]) {
        $names = if ($Value -is [System.Collections.IDictionary]) {
            $Value.Keys
        } else {
            $Value.PSObject.Properties.Name
        }

        foreach ($name in $names) {
            $next = if ($Prefix) { "$Prefix.$name" } else { [string]$name }
            $child = if ($Value -is [System.Collections.IDictionary]) { $Value[$name] } else { $Value.$name }
            Get-LeafKeys -Value $child -Prefix $next
        }
        return
    }

    $Prefix
}

$canonicalPath = Join-Path $LocaleRoot "zh-Hant.json"
if (-not (Test-Path -LiteralPath $canonicalPath)) {
    throw "Canonical catalog not found: $canonicalPath"
}

$canonical = Get-Content -LiteralPath $canonicalPath -Raw -Encoding UTF8 | ConvertFrom-Json
$canonicalKeys = @(Get-LeafKeys -Value $canonical | Sort-Object -Unique)

foreach ($locale in "en", "ja") {
    $path = Join-Path $LocaleRoot "$locale.json"
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Catalog not found: $path"
    }

    $catalog = Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json
    $keys = @(Get-LeafKeys -Value $catalog | Sort-Object -Unique)
    $difference = @(Compare-Object -ReferenceObject $canonicalKeys -DifferenceObject $keys)
    if ($difference) {
        $missing = @($difference | Where-Object SideIndicator -eq '<=' | ForEach-Object InputObject)
        $extra = @($difference | Where-Object SideIndicator -eq '=>' | ForEach-Object InputObject)
        $details = @()
        if ($missing) { $details += "missing: $($missing -join ', ')" }
        if ($extra) { $details += "extra: $($extra -join ', ')" }
        throw "Catalog key parity failed for $locale ($($details -join '; '))."
    }
}

Write-Host "i18n catalog key parity passed."
