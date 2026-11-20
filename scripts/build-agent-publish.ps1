# Build + kiem tra + chay lai API sau ban sua "Agent dang bai" (03/11/2026).
# 1. Dung API dev + watchdog  2. dotnet build  3. test quy tac Dang lai  4. tsc trang Admin
# 5. Bat lai API (nen + watchdog). Job dang qua gio se tu dang trong ~15 giay sau khi API len.
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root
$DevDir = Join-Path $Root ".dev"
New-Item -ItemType Directory -Force -Path $DevDir | Out-Null
$Log = Join-Path $DevDir "build-agent-publish.log"
Start-Transcript -Path $Log -Force | Out-Null

function Step($text) { Write-Host ""; Write-Host "=== $text ===" -ForegroundColor Cyan }
function Fail($text) {
    Write-Host "[LOI] $text" -ForegroundColor Red
    Write-Host "Gui file log nay cho Claude: $Log" -ForegroundColor Yellow
    Stop-Transcript | Out-Null
    exit 1
}

. (Join-Path $PSScriptRoot "api-dev.ps1")

Step "1/5 Dung API dev va watchdog (de build khong bi khoa file)"
Stop-ApiDevAll

Step "2/5 dotnet build"
$env:NUGET_PACKAGES = "$env:USERPROFILE\.nuget\packages"
dotnet build (Join-Path $Root "src\KitPlatform.Api\KitPlatform.Api.csproj") --verbosity minimal
if ($LASTEXITCODE -ne 0) { Fail "Build API that bai." }
dotnet build (Join-Path $Root "tests\KitPlatform.Content.Tests\KitPlatform.Content.Tests.csproj") --verbosity minimal
if ($LASTEXITCODE -ne 0) { Fail "Build project test that bai." }

Step "3/5 Test quy tac duyet / huy / dang lai"
dotnet test (Join-Path $Root "tests\KitPlatform.Content.Tests\KitPlatform.Content.Tests.csproj") --no-build `
    --filter "FullyQualifiedName~ContentScheduleApprovalRulesTests" --logger "console;verbosity=normal"
if ($LASTEXITCODE -ne 0) { Fail "Test ContentScheduleApprovalRulesTests that bai." }

Step "4/5 Kiem tra kieu TypeScript trang Admin (chi canh bao, khong chan bat API)"
$adminDir = Join-Path $Root "client\admin"
$tscOut = Join-Path $DevDir "tsc-agent-publish.txt"
if (Test-Path (Join-Path $adminDir "node_modules")) {
    Push-Location $adminDir
    try {
        cmd /c "npx tsc --noEmit -p tsconfig.json > ""$tscOut"" 2>&1"
        $tscCode = $LASTEXITCODE
    }
    finally { Pop-Location }
    if ($tscCode -ne 0) {
        Write-Host "[!] tsc bao loi, xem $tscOut (gui file nay cho Claude). Van tiep tuc bat API." -ForegroundColor Yellow
        Get-Content $tscOut -Tail 15 | Write-Host
    } else {
        Write-Host "[OK] tsc khong co loi." -ForegroundColor Green
    }
} else {
    Write-Host "[!] client\admin chua co node_modules, bo qua buoc tsc." -ForegroundColor Yellow
}

Step "5/5 Bat lai API (nen + watchdog)"
$code = Ensure-ApiDev -SkipBuild
if ($code -ne 0) { Fail "API khong len. Xem .dev\api.err.log" }

Write-Host ""
Write-Host "[OK] Xong. API: http://localhost:5290  |  Admin: http://localhost:5173" -ForegroundColor Green
Write-Host "Job dang qua gio se tu dang trong ~15 giay. Mo Lich dang & Agent -> Agent dang bai, chon loc 'Da dang' de xem." -ForegroundColor Green
Write-Host "Neu Admin (Vite) chua chay: chay run-dev.bat hoac 'npm run dev:vite' trong client\admin."
Stop-Transcript | Out-Null
exit 0
