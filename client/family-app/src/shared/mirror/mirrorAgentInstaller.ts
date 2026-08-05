import { useSessionStore } from '@/shared/auth/session.store';

export type MirrorAgentInstallInput = {
  familyId: string;
  childMemberId: string;
  childShort?: string;
  deviceLabel?: string;
};

/** Local Vite proxies /api → :5290; agent must call API host directly. */
export function resolveMirrorApiBaseUrl(): string {
  const { hostname, origin } = window.location;
  if (hostname === 'localhost' || hostname === '127.0.0.1') {
    return 'http://localhost:5290';
  }
  return origin.replace(/\/$/, '');
}

export function resolveMirrorAgentAssetsBaseUrl(): string {
  return window.location.origin.replace(/\/$/, '');
}

function utf8ToBase64(text: string): string {
  const bytes = new TextEncoder().encode(text);
  let bin = '';
  for (let i = 0; i < bytes.length; i++) bin += String.fromCharCode(bytes[i]!);
  return btoa(bin);
}

function buildInstallPowerShell(input: MirrorAgentInstallInput & { accessToken: string }): string {
  const apiBase = resolveMirrorApiBaseUrl();
  const assetsBase = `${resolveMirrorAgentAssetsBaseUrl()}/mirror-agent`;
  const config = {
    apiBaseUrl: apiBase,
    familyId: input.familyId,
    childMemberId: input.childMemberId,
    accessToken: input.accessToken,
    deviceId: '',
    deviceLabel: input.deviceLabel?.trim() || `PC học ${input.childShort || ''}`.trim() || 'PC học',
  };
  const configB64 = utf8ToBase64(JSON.stringify(config, null, 2));
  const assetsB64 = utf8ToBase64(assetsBase);

  return `
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Write-Host '=== Famixa Mirror Agent — cai dat 1 lan ===' -ForegroundColor Cyan

$assetsBase = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('${assetsB64}'))
$configJson = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('${configB64}'))
$installDir = Join-Path $env:LOCALAPPDATA 'Famixa\\MirrorAgent'
New-Item -ItemType Directory -Force -Path $installDir | Out-Null
Write-Host ("Thu muc: {0}" -f $installDir)

function Get-AgentFile([string]$Name) {
  $url = "$assetsBase/$Name"
  $out = Join-Path $installDir $Name
  Write-Host ("Tai {0} ..." -f $Name)
  Invoke-WebRequest -Uri $url -OutFile $out -UseBasicParsing -TimeoutSec 60
  if (-not (Test-Path $out) -or (Get-Item $out).Length -lt 50) {
    throw "Khong tai duoc $Name tu $url — mo Famixa tren may Windows nay roi tai lai."
  }
}

Get-AgentFile 'Run-FamixaMirrorAgent.ps1'
Get-AgentFile 'Register-ScheduledTask.ps1'

$configPath = Join-Path $installDir 'config.json'
Set-Content -Path $configPath -Value $configJson -Encoding UTF8
Write-Host 'Da ghi config.json (da dien family / con / token).' -ForegroundColor Green

Push-Location $installDir
try {
  & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $installDir 'Register-ScheduledTask.ps1')
} finally {
  Pop-Location
}

Write-Host 'Khoi dong Agent...' -ForegroundColor Cyan
Start-Process -FilePath 'powershell.exe' -ArgumentList @(
  '-NoProfile',
  '-ExecutionPolicy', 'Bypass',
  '-WindowStyle', 'Minimized',
  '-File', (Join-Path $installDir 'Run-FamixaMirrorAgent.ps1')
) -WorkingDirectory $installDir

Write-Host ''
Write-Host 'XONG. Agent dang chay nen + se tu mo lai khi dang nhap Windows.' -ForegroundColor Green
Write-Host 'Mo lai Famixa (bo me) → Gương tối → Làm mới gương.' -ForegroundColor Yellow
Write-Host 'Bam phim bat ky de dong...'
[void][Console]::ReadKey($true)
`.trim();
}

/**
 * Downloads a double-clickable .cmd that installs + starts the Windows Mirror Agent
 * with family/child/token from the current parent session.
 *
 * Payload lives inside the .cmd file (not argv) to avoid Windows 8191-char limits.
 */
export function downloadMirrorAgentInstaller(input: MirrorAgentInstallInput): {
  ok: true;
  fileName: string;
} | {
  ok: false;
  reason: string;
} {
  const accessToken = useSessionStore.getState().accessToken;
  if (!accessToken) {
    return { ok: false, reason: 'Cần đăng nhập bố/mẹ trước khi tải Agent' };
  }
  if (!input.familyId?.trim()) {
    return { ok: false, reason: 'Thiếu familyId' };
  }
  if (!input.childMemberId?.trim()) {
    return { ok: false, reason: 'Chọn một con (không phải “Cả nhà”) rồi tải lại' };
  }

  const ps = buildInstallPowerShell({ ...input, accessToken });
  const payloadB64 = utf8ToBase64(ps);
  const short = (input.childShort || 'con').replace(/[^\w\u00C0-\u024F-]+/g, '') || 'con';
  const fileName = `Famixa-Cai-Agent-${short}.cmd`;

  const cmd = [
    '@echo off',
    'title Famixa Mirror Agent',
    'echo.',
    'echo Famixa — dang cai Agent Mirror cho Windows...',
    'echo.',
    'powershell -NoProfile -ExecutionPolicy Bypass -Command ^',
    "  \"$raw = Get-Content -LiteralPath '%~f0' -Raw; ^",
    "   $m = [regex]::Match($raw, '____PAYLOAD____\\r?\\n([\\s\\S]*?)\\r?\\n____END____'); ^",
    "   if (-not $m.Success) { throw 'Thieu payload cai dat trong file .cmd' }; ^",
    "   $ps1 = Join-Path $env:TEMP ('Famixa-Install-' + [guid]::NewGuid().ToString('n') + '.ps1'); ^",
    "   $bytes = [Convert]::FromBase64String(($m.Groups[1].Value -replace '\\s','')); ^",
    "   [IO.File]::WriteAllBytes($ps1, $bytes); ^",
    "   & powershell -NoProfile -ExecutionPolicy Bypass -File $ps1; ^",
    "   $code = $LASTEXITCODE; Remove-Item -Force $ps1 -ErrorAction SilentlyContinue; exit $code\"",
    'if errorlevel 1 (',
    '  echo.',
    '  echo Cai dat gap loi. Thu: chuot phai file nay - Run as administrator.',
    '  pause',
    ')',
    'exit /b %ERRORLEVEL%',
    '____PAYLOAD____',
    payloadB64,
    '____END____',
    '',
  ].join('\r\n');

  const blob = new Blob([cmd], { type: 'application/x-bat' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  document.body.appendChild(a);
  a.click();
  a.remove();
  window.setTimeout(() => URL.revokeObjectURL(url), 2_000);
  return { ok: true, fileName };
}
