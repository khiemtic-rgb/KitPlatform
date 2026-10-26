# Builds the research host and dashboard on this machine.
# Does not copy files to a server, does not restart a service, and does not read production secrets.
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$dist = Join-Path $PSScriptRoot 'dist'
if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
New-Item -ItemType Directory -Path $dist | Out-Null

$projects = @(
    @{
        Name = 'research-host'
        Project = Join-Path $root 'src\Packs\PersonalIntelligence\KitPlatform.Packs.PersonalIntelligence.ResearchHost\KitPlatform.Packs.PersonalIntelligence.ResearchHost.csproj'
    },
    @{
        Name = 'research-dashboard'
        Project = Join-Path $root 'src\Packs\PersonalIntelligence\KitPlatform.Packs.PersonalIntelligence.ResearchDashboard\KitPlatform.Packs.PersonalIntelligence.ResearchDashboard.csproj'
    }
)

foreach ($item in $projects) {
    $out = Join-Path $dist $item.Name
    dotnet publish $item.Project -c Release -f net10.0 -r linux-x64 --self-contained false -o $out
    if ($LASTEXITCODE -ne 0) { throw "publish failed: $($item.Name)" }
}

$manifest = Join-Path $dist 'SHA256SUMS.txt'
$lines = Get-ChildItem $dist -Recurse -File | Where-Object { $_.Name -ne 'SHA256SUMS.txt' } | ForEach-Object {
    $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    $relative = $_.FullName.Substring($dist.Length + 1).Replace('\', '/')
    '{0}  {1}' -f $hash, $relative
}
$lines | Set-Content -Path $manifest -Encoding ascii
Write-Output ("PACKAGE=" + $dist)
Write-Output ("FILES=" + @($lines).Count)
