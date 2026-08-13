# Xuat ho so CQD sang Word (.docx)
# Cach 1 (uu tien): pandoc  — winget install JohnMacFarlane.Pandoc
# Cach 2 (mac dinh): python export-md-to-docx.py

$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot
$OutDir = Join-Path $Root "word"
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$pandoc = Get-Command pandoc -ErrorAction SilentlyContinue
if ($pandoc) {
    Write-Host "Xuat bang pandoc..."
    $docs = @(
        @{ In = "01-software-functional-specification-v1.md"; Out = "NVX-CQD-SFS-01.docx" },
        @{ In = "02-api-integration-specification-v1.md"; Out = "NVX-CQD-API-01.docx" },
        @{ In = "03-system-architecture-document-v1.md"; Out = "NVX-CQD-ARCH-01.docx" },
        @{ In = "README.md"; Out = "NVX-CQD-00-Muc-luc-ho-so.docx" },
        @{ In = "phu-luc-a-qd540-field-map-v1.md"; Out = "NVX-CQD-PL-A-QD540-Field-Map.docx" },
        @{ In = "04-bien-ban-kiem-thu-api-csdl-duoc-sandbox-v1.md"; Out = "NVX-CQD-UAT-01-Bien-ban-kiem-thu-API-Sandbox.docx" }
    )
    foreach ($d in $docs) {
        $inPath = Join-Path $Root $d.In
        $outPath = Join-Path $OutDir $d.Out
        if (-not (Test-Path $inPath)) { continue }
        pandoc $inPath -o $outPath --from markdown --to docx --resource-path="$Root;$Root\assets\screenshots" --standalone
        Write-Host "  OK $($d.Out)"
    }
} else {
    Write-Host "pandoc chua cai — dung Python (python-docx)..."
    pip install python-docx --quiet
    python (Join-Path $Root "export-md-to-docx.py")
}

Write-Host ""
Write-Host "File Word tai: $OutDir"
