# Read-only. Prints VPS / GIT / LOCAL for the pharmacy survey. Changes nothing.

$ErrorActionPreference = "Continue"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

function Row($part, $vps, $git, $local) {
    [pscustomobject]@{ ThanhPhan = $part; VPS = $vps; GIT = $git; LOCAL = $local }
}

$gitMain = (git rev-parse origin/main).Trim()
$localBranch = (git -C E:\KitPlatform rev-parse --abbrev-ref HEAD).Trim()
$siteBranch = (git -C E:\novixa-site-release rev-parse --abbrev-ref HEAD).Trim()
$siteHead = (git -C E:\novixa-site-release rev-parse --short HEAD).Trim()
$paths = @(
    "client/assessment-web", "src/Packs/Survey", "migrations/447_kap_pharmacy_v2.sql",
    "scripts/apply-447-kap-pharmacy-v2.ps1", "scripts/kap-v2-completion-time.sql", "tools/kap-v2"
)
$dirty = @(git -C E:\KitPlatform status --porcelain -- @paths)
$localSurvey = "nhánh $localBranch"
if ($dirty.Count -gt 0) { $localSurvey += "; $($dirty.Count) dòng chưa commit trong phần khảo sát" }
else { $localSurvey += "; không có file khảo sát chưa commit" }

$core = "chưa đọc"
$survey = "chưa đọc"
$webVer = "chưa đọc"
$webIndex = "chưa đọc"
$db = "chưa đọc"
$health = "chưa đọc"
$page = curl.exe -s "https://novixa.vn/vi/health-check/"
if ($page -match "có đang để lọt tiền") { $health = "có 'có đang để lọt tiền'" } else { $health = "không thấy câu" }

$passFile = "E:\Maychu_VPS\tk.txt"
$plink = "C:\Program Files\PuTTY\plink.exe"
if ((Test-Path $passFile) -and (Test-Path $plink)) {
    $passLine = Get-Content $passFile | Where-Object { $_ -match '^Pass' } | Select-Object -First 1
    $pass = ($passLine -replace '^Pass[^:]*:\s*', '').Trim()
    $py = @'
import pathlib, re
api = pathlib.Path("/var/www/kit-platform/api")
web = pathlib.Path("/var/www/kit-platform/assessment-web")
pat = re.compile(r"1\.0\.0\+([0-9a-fA-F]+)")
def rev(name):
    m = pat.search((api/name).read_bytes().decode("utf-16le", "ignore"))
    return m.group(1)[:12] if m else "NO"
print("CORE", rev("KitPlatform.Application.dll"))
print("SURVEY", rev("KitPlatform.Packs.Survey.Infrastructure.dll"))
sv = api/"SURVEY_VERSION.txt"
print("SURVEY_TXT", "co" if sv.exists() else "thieu")
wv = web/"VERSION.txt"
print("WEB_TXT", (wv.read_text(encoding="utf-8", errors="replace").splitlines()[0] if wv.exists() else "thieu"))
'@
    $b64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($py))
    $remote = & $plink -batch -pw $pass root@103.200.23.229 "echo $b64 | base64 -d | python3"
    foreach ($line in $remote) {
        if ($line -like "CORE *") { $core = $line.Substring(5) }
        elseif ($line -like "SURVEY *") { $survey = $line.Substring(7) }
        elseif ($line -like "WEB_TXT *") { $webVer = $line.Substring(8) }
    }
    $sql = @'
SELECT (SELECT status FROM assessment_template WHERE code='PHARMACY_V2'),
       (SELECT count(*) FROM assessment_question q
          JOIN assessment_dimension d ON d.id = q.dimension_id
          JOIN assessment_category c ON c.id = d.category_id
          JOIN assessment_template t ON t.id = c.template_id
         WHERE t.code = 'PHARMACY_V2'),
       (SELECT count(*) FROM assessment_rule r
          JOIN assessment_template t ON t.id = r.template_id
         WHERE t.code = 'PHARMACY_V2');
'@
    $sqlB64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($sql))
    $dbLine = & $plink -batch -pw $pass root@103.200.23.229 "echo $sqlB64 | base64 -d | sudo -u postgres psql -d novixa_prod -t -A -F '|' -f -"
    if ($dbLine) { $db = ($dbLine | Where-Object { $_ -match '\|' } | Select-Object -First 1) }
}

$rows = @(
    (Row "API lõi" $core "origin/main $gitMain" $localSurvey)
    (Row "Survey DLL" $survey "nguồn Survey trong $gitMain" "cùng nhánh local ở trên")
    (Row "assessment-web" $webVer "nguồn web trong origin/main không đổi từ b30403e6 đến $gitMain" "E:\KitPlatform còn sửa assessment-web nếu dòng local ở trên báo chưa commit")
    (Row "CSDL PHARMACY_V2 active|câu|luật" $db "447 có 36 câu và 109 luật, file có trên origin/main" "file 447 trên E:\KitPlatform là bản chưa commit nếu status báo ??")
    (Row "Health Check" $health "commit health-check có trên origin/main" "E:\novixa-site-release $siteBranch $siteHead")
)
$rows | Format-Table -AutoSize | Out-String -Width 220
Write-Host "Đọc xong. Script không sửa git, VPS, hay cơ sở dữ liệu."
