[CmdletBinding()]
param(
    [Parameter(Mandatory=$true, Position=0)]
    [string]$ClassName,

    [Parameter(Mandatory=$false)]
    [ValidateSet('in', 'out', 'both', 'community')]
    [string]$Direction = 'both'
)

$graphPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\docs\graph\graph.json'))

if (-not (Test-Path $graphPath)) {
    Write-Host "graph.json not found at: $graphPath" -ForegroundColor Yellow
    Write-Host "Build Perpetuum.Server once to generate it:"
    Write-Host "  dotnet build src/Perpetuum.Server/Perpetuum.Server.csproj -c Release -p:Platform=x64"
    exit 0
}

Write-Host "Loading graph..." -ForegroundColor DarkGray
$g = Get-Content $graphPath -Raw | ConvertFrom-Json

$nodeIndex = @{}
$g.nodes | ForEach-Object { $nodeIndex[$_.id] = $_ }

$matchedNodes = @($g.nodes | Where-Object { $_.label -like "*$ClassName*" -and $_.label -like '*.cs' -and $_.id -notlike 'file:*' })

if ($matchedNodes.Count -eq 0) {
    Write-Host "No node found matching '$ClassName'"
    exit 0
}

if ($matchedNodes.Count -gt 1) {
    Write-Host "$($matchedNodes.Count) nodes match '$ClassName'. Use a more specific name:" -ForegroundColor Yellow
    $matchedNodes | ForEach-Object { Write-Host "  $($_.label)  ($($_.file_path))" }
    exit 0
}

$node = $matchedNodes[0]
Write-Host ""
Write-Host "Node: $($node.label)  [community $($node.community)]" -ForegroundColor Cyan
Write-Host "File: $($node.file_path)"
Write-Host ""

if ($Direction -eq 'in' -or $Direction -eq 'both') {
    $inEdges = @($g.edges | Where-Object { $_.target -eq $node.id })
    Write-Host "=== Inbound ($($inEdges.Count)): who depends on this ===" -ForegroundColor Green
    if ($inEdges.Count -eq 0) {
        Write-Host "  (none)"
    } else {
        foreach ($edge in $inEdges) {
            $srcNode = $nodeIndex[$edge.source]
            $srcLabel = if ($srcNode) { $srcNode.label } else { $edge.source }
            Write-Host "  [$($edge.relationship)]  $srcLabel"
        }
    }
    Write-Host ""
}

if ($Direction -eq 'out' -or $Direction -eq 'both') {
    $outEdges = @($g.edges | Where-Object { $_.source -eq $node.id })
    Write-Host "=== Outbound ($($outEdges.Count)): what this depends on ===" -ForegroundColor Green
    if ($outEdges.Count -eq 0) {
        Write-Host "  (none)"
    } else {
        foreach ($edge in $outEdges) {
            $tgtNode = $nodeIndex[$edge.target]
            $tgtLabel = if ($tgtNode) { $tgtNode.label } else { $edge.target }
            Write-Host "  [$($edge.relationship)]  $tgtLabel"
        }
    }
    Write-Host ""
}

if ($Direction -eq 'community') {
    $communityNodes = @($g.nodes | Where-Object { $_.community -eq $node.community -and $_.id -ne $node.id -and $_.id -notlike 'file:*' -and $_.label -like '*.cs' } | Sort-Object label)
    Write-Host "=== Community $($node.community) ($($communityNodes.Count + 1) members) ===" -ForegroundColor Green
    Write-Host "  [self]  $($node.label)"
    foreach ($member in $communityNodes) {
        Write-Host "  [$($member.type)]  $($member.label)  ($($member.file_path))"
    }
    Write-Host ""
}
