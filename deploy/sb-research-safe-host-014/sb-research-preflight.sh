#!/bin/bash
# Version: sb-research-safe-host-016
# Read-only production preflight for Research Safe Host.
# Result file: /tmp/sb-research-preflight-result.txt
# Logs go to one file descriptor on a stage file inside a mode-0700 directory
# owned by this user. Later log lines do not reopen that path. Publication is
# one rename from that directory onto the result name in /tmp opened with
# O_DIRECTORY|O_NOFOLLOW. On Linux that rename replaces the final entry and
# does not follow a symlink. A name check before the rename is not a barrier.
# Threat model: other users. The private directory is mode 0700 and /tmp must
# be owned by this user or by root; group or other write requires the sticky
# bit. A concurrent process with the same UID is the operator, not a separate
# attacker. This script does not claim that window is closed.
# TERM, INT, HUP, and PIPE discard the private directory. SIGKILL cannot be caught. A later run
# removes a leftover private directory only when this user owns it, its mode
# is 0700, it is not a symlink, and it contains only regular files named
# stage, acl, or psql-err. Any other leftover is left untouched.
# Exit 0 only when every gate passed and the rename was confirmed.
# Exit 2 when a gate is blocked. Exit 3 when the log channel itself fails.
# Does not print ExecStart, Environment values, connection strings, passwords, or tokens.
# Publication root is the directory FileResearchPublicationReader reads. The API
# has no compiled default: an empty value returns MISSING. The canonical
# directory is outside every nginx root observed on this host.
CANONICAL_PUBLICATION=/var/lib/kit-platform/research-publication
path_is_inside() {
  local candidate=$1 root=$2
  case "$candidate" in
    "$root"|"$root"/*) return 0 ;;
  esac
  return 1
}
cs_field() {
  local data=$1 want=$2
  local i=0 n=${#data} ch inq=0 token="" key="" val=""
  want=$(printf '%s' "$want" | tr '[:upper:]' '[:lower:]')
  while true; do
    if [ "$i" -ge "$n" ]; then
      ch=""
    else
      ch=${data:i:1}
    fi
    if [ "$i" -ge "$n" ] || { [ "$inq" -eq 0 ] && [ "$ch" = ";" ]; }; then
      key=${token%%=*}
      if [ "$token" != "$key" ]; then
        val=${token#*=}
        key=$(printf '%s' "$key" | tr '[:upper:]' '[:lower:]')
        key=${key#"${key%%[![:space:]]*}"}
        key=${key%"${key##*[![:space:]]}"}
        if [ "$key" = "$want" ]; then
          printf '%s' "$val"
          return 0
        fi
      fi
      token=""
      if [ "$i" -ge "$n" ]; then
        return 1
      fi
      i=$((i + 1))
      continue
    fi
    if [ "$ch" = "'" ]; then
      if [ "$inq" -eq 1 ] && [ "${data:i+1:1}" = "'" ]; then
        token="${token}'"
        i=$((i + 2))
        continue
      fi
      if [ "$inq" -eq 0 ]; then inq=1; else inq=0; fi
      i=$((i + 1))
      continue
    fi
    token="${token}${ch}"
    i=$((i + 1))
  done
}
psql_error_code() {
  local f=$1
  if [ ! -s "$f" ]; then
    printf '%s\n' empty
    return
  fi
  if grep -q -i -E 'no password supplied|password authentication failed|authentication failed' "$f"; then
    printf '%s\n' auth
    return
  fi
  if grep -q -i -E 'pg_hba' "$f"; then
    printf '%s\n' pg_hba
    return
  fi
  if grep -q -i -E 'could not connect|connection refused|timeout expired|could not translate host' "$f"; then
    printf '%s\n' connect
    return
  fi
  if grep -q -i -E 'database .* does not exist' "$f"; then
    printf '%s\n' database
    return
  fi
  if grep -q -i -E 'role .* does not exist' "$f"; then
    printf '%s\n' role
    return
  fi
  printf '%s\n' unrecognized
}
if [ "${1:-}" = "--logic-test" ]; then
  logic_fail=0
  if path_is_inside /var/backups /var/backups; then
    printf '%s\n' "PASS backup-equals-root"
  else
    printf '%s\n' "FAIL backup-equals-root"
    logic_fail=1
  fi
  if path_is_inside /var/backups/kit-research /var/backups; then
    printf '%s\n' "PASS backup-child"
  else
    printf '%s\n' "FAIL backup-child"
    logic_fail=1
  fi
  if path_is_inside /var/backup /var/backups; then
    printf '%s\n' "FAIL prefix-not-inside"
    logic_fail=1
  else
    printf '%s\n' "PASS prefix-not-inside"
  fi
  if path_is_inside /tmp/sb-fix-pub /tmp/sb-fix-web; then
    printf '%s\n' "FAIL publication-outside"
    logic_fail=1
  else
    printf '%s\n' "PASS publication-outside"
  fi
  if path_is_inside /tmp/sb-fix-web/pub /tmp/sb-fix-web; then
    printf '%s\n' "PASS publication-inside"
  else
    printf '%s\n' "FAIL publication-inside"
    logic_fail=1
  fi
  canon_fail=0
  for root in /var/www/kit-platform /var/www/kit-platform/admin /var/www/kit-platform/customer-app /var/www/kit-platform/staff-app /var/www/kit-platform/assessment-web /var/www/kit-platform/prescriber-portal /var/www/kit-platform/partner-portal /var/www/kit-platform/family-app /var/backups; do
    if path_is_inside "$CANONICAL_PUBLICATION" "$root"; then
      canon_fail=1
    fi
  done
  if [ "$CANONICAL_PUBLICATION" = "/var/lib/kit-platform/research-publication" ] && [ "$canon_fail" -eq 0 ]; then
    printf '%s\n' "PASS canonical-publication-outside"
  else
    printf '%s\n' "FAIL canonical-publication-outside"
    logic_fail=1
  fi
  sample="Host=127.0.0.1;Port=5432;Database=novixa_prod;Username=KitPlatform;Password='p@ss word;x''y'"
  parsed=$(cs_field "$sample" password || true)
  if [ "$parsed" = "p@ss word;x'y" ] && [ "$(cs_field "$sample" username || true)" = "KitPlatform" ] && [ "$(cs_field "$sample" database || true)" = "novixa_prod" ]; then
    printf '%s\n' "PASS connection-string-quotes"
  else
    printf '%s\n' "FAIL connection-string-quotes"
    logic_fail=1
  fi
  if [ "$logic_fail" -eq 0 ]; then
    printf '%s\n' "LOGIC_TEST_DONE pass"
    exit 0
  fi
  printf '%s\n' "LOGIC_TEST_DONE fail"
  exit 1
fi
set -u
set -o pipefail
trap '' PIPE
OUT=/tmp/sb-research-preflight-result.txt
OUT_NAME=sb-research-preflight-result.txt
published=0
PRIV=""
LOG_IN=""
LOG_OUT=""
if ! command -v perl >/dev/null 2>&1; then
  printf '%s\n' "BLOCKED cannot prove a no-follow result file"
  exit 2
fi
coproc PF {
  perl /proc/self/fd/3 "$OUT_NAME" 3<<'PERL'
use strict;
use warnings;
use Fcntl qw(O_RDONLY O_DIRECTORY O_NOFOLLOW O_CREAT O_EXCL O_WRONLY S_ISREG S_ISLNK S_ISDIR);
$| = 1;
binmode STDIN;
binmode STDOUT;
my $CLOEXEC = eval { Fcntl::O_CLOEXEC() } || 0;
my ($dest) = @ARGV;
if (!defined $dest || $dest ne "sb-research-preflight-result.txt") {
  print "BLOCKED result name is not the expected name\n";
  exit 4;
}
umask 0077;
my ($dh, $ph, $stage, $priv);
my ($stage_dev, $stage_ino, $priv_dev, $priv_ino);
my $discarded = 0;
sub discard {
  return if $discarded;
  $discarded = 1;
  if (defined $ph) {
    chdir($ph);
    foreach my $extra (qw(stage acl psql-err)) {
      my @n = lstat($extra);
      next if !@n || S_ISDIR($n[2]);
      if ($extra eq "stage") {
        next if !defined $stage_dev || S_ISLNK($n[2]) || !S_ISREG($n[2]);
        next if $n[0] != $stage_dev || $n[1] != $stage_ino;
      }
      unlink($extra);
    }
  }
  if (defined $dh && defined $priv) {
    chdir($dh);
    my @p = lstat($priv);
    if (@p && !S_ISLNK($p[2]) && S_ISDIR($p[2]) && defined $priv_dev && $p[0] == $priv_dev && $p[1] == $priv_ino) {
      rmdir($priv);
    }
  }
}
$SIG{TERM} = sub { discard(); exit 4; };
$SIG{INT} = sub { discard(); exit 4; };
$SIG{HUP} = sub { discard(); exit 4; };
$SIG{PIPE} = sub { discard(); exit 4; };
END { discard(); }
sysopen($dh, "/tmp", O_RDONLY | O_DIRECTORY | O_NOFOLLOW | $CLOEXEC) or do {
  print "BLOCKED cannot open /tmp without following a symlink\n";
  exit 4;
};
my $fdpath = readlink("/proc/self/fd/" . fileno($dh));
if (!defined $fdpath || $fdpath ne "/tmp") {
  print "BLOCKED /tmp directory identity is not proven\n";
  exit 4;
}
my @dir = stat($dh);
if (!@dir || !S_ISDIR($dir[2])) {
  print "BLOCKED /tmp is not a directory\n";
  exit 4;
}
my $mode = $dir[2] & 07777;
my $owner = $dir[4];
my $euid = $>;
my $sticky = ($mode & 01000) ? 1 : 0;
my $gwrite = ($mode & 020) ? 1 : 0;
my $owrite = ($mode & 02) ? 1 : 0;
if (($gwrite || $owrite) && !$sticky) {
  print "BLOCKED /tmp is writable by others without the sticky bit\n";
  exit 4;
}
if ($owner != $euid && $owner != 0) {
  print "BLOCKED /tmp owner is not proven\n";
  exit 4;
}
chdir($dh) or do { print "BLOCKED cannot use the /tmp directory\n"; exit 4; };
my @cwd = stat(".");
if (!@cwd || $cwd[0] != $dir[0] || $cwd[1] != $dir[1]) {
  print "BLOCKED /tmp directory identity changed\n";
  exit 4;
}
my $stale_cleaned = 0;
my $stale_left = 0;
opendir(my $scan, ".") or do { print "BLOCKED cannot read /tmp\n"; exit 4; };
my @stale_names = ();
while (my $name = readdir($scan)) {
  next if $name !~ /\Asb-research-preflight\.[A-Za-z0-9]{8}\z/;
  push @stale_names, $name;
}
closedir($scan);
foreach my $stale_name (@stale_names) {
  my @st = lstat($stale_name);
  if (!@st || S_ISLNK($st[2]) || !S_ISDIR($st[2]) || (($st[2] & 07777) != 0700) || $st[4] != $euid) {
    $stale_left++;
    next;
  }
  my $old;
  if (!sysopen($old, $stale_name, O_RDONLY | O_DIRECTORY | O_NOFOLLOW | $CLOEXEC)) {
    $stale_left++;
    next;
  }
  my @ost = stat($old);
  if (!@ost || $ost[0] != $st[0] || $ost[1] != $st[1]) {
    close($old);
    $stale_left++;
    next;
  }
  if (!chdir($old)) {
    close($old);
    $stale_left++;
    next;
  }
  opendir(my $inner, ".") or do { close($old); $stale_left++; chdir($dh); next; };
  my $bad = 0;
  my @kids = ();
  while (my $child = readdir($inner)) {
    next if $child eq "." || $child eq "..";
    if ($child !~ /\A(?:stage|acl|psql-err)\z/) {
      $bad = 1;
      last;
    }
    push @kids, $child;
  }
  closedir($inner);
  if (!$bad) {
    foreach my $child (@kids) {
      my @cs = lstat($child);
      if (!@cs || S_ISLNK($cs[2]) || S_ISDIR($cs[2]) || $cs[4] != $euid || !unlink($child)) {
        $bad = 1;
        last;
      }
    }
  }
  chdir($dh) or do { print "BLOCKED cannot use the /tmp directory\n"; exit 4; };
  close($old);
  if ($bad) {
    $stale_left++;
    next;
  }
  my @again = lstat($stale_name);
  if (@again && !S_ISLNK($again[2]) && S_ISDIR($again[2]) && $again[0] == $st[0] && $again[1] == $st[1] && rmdir($stale_name)) {
    $stale_cleaned++;
  } else {
    $stale_left++;
  }
}
my $alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
my $made = 0;
for (my $try = 0; $try < 20 && !$made; $try++) {
  sysopen(my $rnd, "/dev/urandom", O_RDONLY | $CLOEXEC) or last;
  my $raw = "";
  my $got = sysread($rnd, $raw, 8);
  close $rnd;
  next if !defined $got || $got != 8;
  my $suffix = "";
  for my $i (0 .. 7) {
    $suffix .= substr($alphabet, ord(substr($raw, $i, 1)) % 62, 1);
  }
  $priv = "sb-research-preflight.$suffix";
  next if lstat($priv);
  next if !mkdir($priv, 0700);
  if (!sysopen($ph, $priv, O_RDONLY | O_DIRECTORY | O_NOFOLLOW | $CLOEXEC)) {
    rmdir($priv);
    next;
  }
  my @pst = stat($ph);
  my @pn = lstat($priv);
  if (!@pst || !@pn || S_ISLNK($pn[2]) || !S_ISDIR($pst[2]) || ($pst[2] & 07777) != 0700 || $pst[4] != $euid || $pst[0] != $pn[0] || $pst[1] != $pn[1]) {
    rmdir($priv);
    next;
  }
  ($priv_dev, $priv_ino) = ($pst[0], $pst[1]);
  $made = 1;
}
if (!$made) {
  print "BLOCKED cannot create a private result directory\n";
  exit 4;
}
chdir($ph) or do { print "BLOCKED cannot use the private result directory\n"; exit 4; };
if (!sysopen($stage, "stage", O_CREAT | O_EXCL | O_WRONLY | O_NOFOLLOW | $CLOEXEC, 0600)) {
  print "BLOCKED cannot create the stage file\n";
  exit 4;
}
eval {
  local $SIG{__WARN__} = sub {};
  require POSIX;
  POSIX::fchmod(fileno($stage), 0600);
};
if (!chmod(0600, "stage")) {
  print "BLOCKED cannot set stage file mode\n";
  exit 4;
}
my @sst = stat($stage);
my @sn = lstat("stage");
if (!@sst || !@sn || (($sn[2] & 07777) != 0600) || (($sst[2] & 07777) != 0600)) {
  print "BLOCKED stage file mode is not 0600\n";
  exit 4;
}
if (!@sst || !@sn || S_ISLNK($sn[2]) || !S_ISREG($sst[2]) || $sst[0] != $sn[0] || $sst[1] != $sn[1]) {
  print "BLOCKED stage file identity is not proven\n";
  exit 4;
}
($stage_dev, $stage_ino) = ($sst[0], $sst[1]);
chdir($dh) or do { print "BLOCKED cannot use the /tmp directory\n"; exit 4; };
print "READY $priv\n";
print "STALE cleaned=$stale_cleaned left=$stale_left\n";
while (defined(my $line = <STDIN>)) {
  chomp $line;
  if ($line eq "C DISCARD") {
    discard();
    print "DISCARDED\n";
    exit 0;
  }
  if ($line eq "C PUBLISH") {
    my @fdst = stat($stage);
    chdir($ph) or do { print "BLOCKED cannot use the private result directory\n"; exit 4; };
    my @now = lstat("stage");
    # This lstat can be stale before rename. It is not a TOCTOU barrier.
    if (!@fdst || !@now || S_ISLNK($now[2]) || !S_ISREG($now[2]) || $fdst[0] != $now[0] || $fdst[1] != $now[1] || $fdst[0] != $stage_dev || $fdst[1] != $stage_ino) {
      print "BLOCKED stage file identity changed\n";
      exit 4;
    }
    my $synced = eval { require IO::Handle; $stage->sync(); };
    if ($@ || !defined $synced) {
      print "BLOCKED result file sync failed\n";
      exit 4;
    }
    chdir($dh) or do { print "BLOCKED cannot use the /tmp directory\n"; exit 4; };
    my @here = stat(".");
    if (!@here || $here[0] != $dir[0] || $here[1] != $dir[1]) {
      print "BLOCKED /tmp directory identity changed\n";
      exit 4;
    }
    my @pnow = lstat($priv);
    if (!@pnow || S_ISLNK($pnow[2]) || !S_ISDIR($pnow[2]) || $pnow[0] != $priv_dev || $pnow[1] != $priv_ino) {
      print "BLOCKED private directory name changed\n";
      exit 4;
    }
    # A symlink or directory at the result name is refused before rename.
    # This look can be stale. It is not a TOCTOU barrier.
    my @destnow = lstat($dest);
    if (@destnow && (S_ISLNK($destnow[2]) || S_ISDIR($destnow[2]) || !S_ISREG($destnow[2]))) {
      print "BLOCKED cannot publish result file\n";
      exit 4;
    }
    if (!rename("$priv/stage", $dest)) {
      print "BLOCKED cannot publish result file\n";
      exit 4;
    }
    chmod(0600, $dest);
    my @after = lstat($dest);
    if (!@after || S_ISLNK($after[2]) || !S_ISREG($after[2]) || $after[0] != $stage_dev || $after[1] != $stage_ino || (($after[2] & 07777) != 0600)) {
      if (@after && !S_ISLNK($after[2]) && S_ISREG($after[2]) && $after[0] == $stage_dev && $after[1] == $stage_ino) {
        unlink($dest);
      }
      print "BLOCKED published result is not the stage file\n";
      exit 4;
    }
    $discarded = 0;
    discard();
    $discarded = 1;
    print "PUBLISHED\n";
    exit 0;
  }
  if ($line =~ /\AL (.*)\z/) {
    my $buf = $1 . "\n";
    my $off = 0;
    while ($off < length($buf)) {
      my $n = syswrite($stage, $buf, length($buf) - $off, $off);
      if (!defined $n) {
        print "BLOCKED result log write failed\n";
        exit 4;
      }
      $off += $n;
    }
    next;
  }
  print "BLOCKED result log protocol failed\n";
  exit 4;
}
exit 4;
PERL
}
exec {LOG_IN}<&"${PF[0]}"
exec {LOG_OUT}>&"${PF[1]}"
ready=""
stale_line=""
IFS= read -r ready <&"$LOG_IN" || ready=""
case "$ready" in
  READY\ sb-research-preflight.[A-Za-z0-9][A-Za-z0-9][A-Za-z0-9][A-Za-z0-9][A-Za-z0-9][A-Za-z0-9][A-Za-z0-9][A-Za-z0-9])
    PRIV="/tmp/${ready#READY }"
    ;;
  BLOCKED*)
    printf '%s\n' "$ready"
    exit 2
    ;;
  *)
    printf '%s\n' "BLOCKED cannot prove a no-follow result file"
    exit 2
    ;;
esac
IFS= read -r stale_line <&"$LOG_IN" || stale_line=""
case "$stale_line" in
  STALE\ cleaned=[0-9]*\ left=[0-9]*) ;;
  *)
    printf '%s\n' "BLOCKED result log protocol failed"
    exit 2
    ;;
esac
channel_stop() {
  local rc=$?
  if [ "${published:-0}" -ne 1 ] && [ -n "${LOG_OUT:-}" ]; then
    printf 'C DISCARD\n' >&"$LOG_OUT" 2>/dev/null || true
    local dstatus=""
    IFS= read -r -t 2 dstatus <&"$LOG_IN" 2>/dev/null || true
  fi
  exit "$rc"
}
trap channel_stop EXIT
trap 'exit 2' INT TERM
log() {
  printf '%s\n' "$*" || true
  if ! printf 'L %s\n' "$*" >&"$LOG_OUT"; then
    printf '%s\n' "BLOCKED result log write failed"
    exit 3
  fi
}
publish_result() {
  if ! printf 'C PUBLISH\n' >&"$LOG_OUT"; then
    printf '%s\n' "BLOCKED result publication is not proven"
    exit 2
  fi
  local status=""
  IFS= read -r status <&"$LOG_IN" || status=""
  if [ "$status" = "PUBLISHED" ]; then
    published=1
    return 0
  fi
  printf '%s\n' "${status:-BLOCKED result publication is not proven}"
  exit 2
}
finish_blocked() { log "PREFLIGHT_DONE blocked"; publish_result; exit 2; }
note_block() { log "BLOCKED $1"; blocked=1; }
blocked=0
skip_db=0
nginx_ok=0
log "STALE_PRIVATE ${stale_line#STALE }"
if [ "${1:-}" = "--publish-self-test" ]; then
  log "PUBLISH_SELFTEST marker"
  publish_result
  exit 0
fi
log "PREFLIGHT_RESULT ${OUT}"
script_hash=$(sha256sum "$0" | awk '{print $1}') || script_hash=""
script_hash=${script_hash#\\}
if [ -z "${script_hash:-}" ]; then
  log "BLOCKED script hash could not be read"
  finish_blocked
fi
log "SCRIPT_LINES $(wc -l <"$0" | tr -d '[:space:]')"
log "SCRIPT_SHA256 ${script_hash}"
lines=$(wc -l <"$0" | tr -d '[:space:]')
if [ "${lines:-0}" -lt 80 ]; then
  log "BLOCKED script body is too short"
  finish_blocked
fi
if [ "$(id -u)" -ne 0 ]; then
  log "BLOCKED NEED_ROOT"
  finish_blocked
fi
if ! command -v ip >/dev/null 2>&1; then
  log "BLOCKED ip command is missing"
  finish_blocked
fi
if ! ip -4 -o addr show | grep -qE '(^|[^0-9])103\.200\.23\.229([^0-9]|$)'; then
  log "BLOCKED host IPv4 list does not include 103.200.23.229"
  while IFS= read -r line; do
    log "$line"
  done < <(ip -4 -o addr show | awk '{print "ADDR", $2, $4}')
  finish_blocked
fi
log "HOST_IPV4 103.200.23.229"
if ! command -v systemctl >/dev/null 2>&1; then
  log "BLOCKED systemctl is missing"
  finish_blocked
fi
active=$(systemctl is-active kit-platform-api 2>/dev/null || true)
log "SERVICE_ACTIVE ${active:-missing}"
if [ "$active" != "active" ]; then
  log "BLOCKED kit-platform-api is not active"
  finish_blocked
fi
wd=$(systemctl show kit-platform-api -p WorkingDirectory --value 2>/dev/null || true)
user=$(systemctl show kit-platform-api -p User --value 2>/dev/null || true)
group=$(systemctl show kit-platform-api -p Group --value 2>/dev/null || true)
frag=$(systemctl show kit-platform-api -p FragmentPath --value 2>/dev/null || true)
envfiles=$(systemctl show kit-platform-api -p EnvironmentFiles --value 2>/dev/null || true)
log "WORKING_DIRECTORY ${wd:-missing}"
log "SERVICE_USER ${user:-missing}"
log "SERVICE_GROUP ${group:-missing}"
log "FRAGMENT ${frag:-missing}"
if [ "$wd" != "/var/www/kit-platform/api" ]; then
  log "BLOCKED working directory is not /var/www/kit-platform/api"
  finish_blocked
fi
if [ -z "${user:-}" ]; then
  note_block "service user is unset"
elif [ "$user" != "www-data" ]; then
  note_block "service user is not www-data"
elif ! id "$user" >/dev/null 2>&1; then
  note_block "service user account cannot be resolved"
else
  log "SERVICE_USER_OK www-data"
fi
if [ "${group:-}" != "www-data" ]; then
  note_block "service group is not www-data"
fi
seen_units=$'\n'
scan_unit() {
  local unitpath=$1
  case "$seen_units" in
    *$'\n'"$unitpath"$'\n'*) return 1 ;;
  esac
  seen_units="${seen_units}${unitpath}"$'\n'
  if [ ! -r "$unitpath" ]; then
    note_block "a systemd unit file could not be read"
    return 0
  fi
  if grep -q 'ResearchSafeHost__SuppressBackgroundWriters' "$unitpath"; then
    note_block "suppress flag is present in a systemd unit file"
  fi
  if grep -q 'PersonalIntelligence__ExchangeCredentialKey' "$unitpath"; then
    note_block "exchange credential setting is present in a systemd unit file"
  fi
  if grep -q 'ExecStart' "$unitpath"; then
    if grep -q 'KitPlatform.Api.dll' "$unitpath"; then
      log "UNIT_EXECSTART_DLL referenced"
    else
      note_block "a unit file sets ExecStart without the API DLL path"
    fi
  fi
  return 0
}
if [ -z "${frag:-}" ] || [ ! -r "$frag" ]; then
  note_block "systemd unit fragment could not be read"
else
  scan_unit "$frag"
fi
dropin_count=0
unit_cat=$(systemctl cat kit-platform-api 2>/dev/null) || unit_cat=""
unit_cat_rc=$?
if [ "$unit_cat_rc" -ne 0 ] || [ -z "${unit_cat:-}" ]; then
  note_block "systemd unit text could not be read; drop-ins were not proven"
else
  while IFS= read -r cat_line; do
    case "$cat_line" in
      "# /"*)
        unitpath=${cat_line#"# "}
        case "$unitpath" in
          /*) ;;
          *) note_block "a systemd fragment path is not absolute"; continue ;;
        esac
        if scan_unit "$unitpath"; then
          if [ "$unitpath" != "${frag:-}" ]; then
            dropin_count=$((dropin_count + 1))
          fi
        fi
        ;;
    esac
  done <<< "$unit_cat"
fi
unset unit_cat unit_cat_rc cat_line unitpath
if ! command -v find >/dev/null 2>&1; then
  note_block "find is missing; drop-in directories were not proven"
else
for dir in /etc/systemd/system/kit-platform-api.service.d /run/systemd/system/kit-platform-api.service.d /usr/lib/systemd/system/kit-platform-api.service.d; do
  if [ -d "$dir" ] && [ ! -r "$dir" ]; then
    note_block "a systemd drop-in directory could not be read"
  elif [ -d "$dir" ]; then
    while IFS= read -r -d '' unitpath; do
      if [ -d "$unitpath" ] && [ ! -L "$unitpath" ]; then
        continue
      fi
      if scan_unit "$unitpath"; then
        dropin_count=$((dropin_count + 1))
      fi
    done < <(find "$dir" -mindepth 1 -maxdepth 1 ! -type d -print0 2>/dev/null)
  fi
done
fi
log "SYSTEMD_DROPIN_SEEN ${dropin_count}"
env_paths=()
seen_env=" "
while IFS= read -r envpath; do
  [ -n "$envpath" ] || continue
  case "$seen_env" in
    *" $envpath "*) continue ;;
  esac
  seen_env="${seen_env}${envpath} "
  env_paths+=("$envpath")
done < <(printf '%s\n' "$envfiles" | grep -oE '/[[:graph:]]+' || true)
unset seen_env
log "ENV_FILE_COUNT ${#env_paths[@]}"
if [ "${#env_paths[@]}" -eq 0 ]; then
  note_block "no systemd environment file path could be read"
fi
has_api_env=0
pub=""
cs=""
if [ "${#env_paths[@]}" -gt 0 ]; then
  for envpath in "${env_paths[@]}"; do
    log "ENV_FILE_PATH ${envpath}"
    if [ "$envpath" = "/etc/kit-platform/api.env" ]; then
      has_api_env=1
    fi
    if [ -L "$envpath" ]; then
      note_block "a systemd environment file is a symlink"
      continue
    fi
    if [ ! -r "$envpath" ]; then
      note_block "a systemd environment file is not readable"
      continue
    fi
    log "ENV_FILE_BYTES ${envpath} $(stat -c '%s' "$envpath")"
    if grep -q 'ResearchSafeHost__SuppressBackgroundWriters' "$envpath"; then
      note_block "suppress flag is present in a systemd environment file"
    fi
    if grep -q 'PersonalIntelligence__ExchangeCredentialKey' "$envpath"; then
      note_block "exchange credential setting is present in a systemd environment file"
    fi
    next_pub=$(awk -F= 'index($0,"PersonalIntelligence__ResearchPublication__Root=")==1 {v=substr($0,index($0,"=")+1)} END{print v}' "$envpath")
    next_pub=${next_pub%$'\r'}
    next_pub=${next_pub%\"}
    next_pub=${next_pub#\"}
    if [ -n "$next_pub" ]; then
      pub=$next_pub
    fi
    next_cs=$(awk -F= 'index($0,"ConnectionStrings__Default=")==1 {v=substr($0,index($0,"=")+1)} END{print v}' "$envpath")
    next_cs=${next_cs%$'\r'}
    next_cs=${next_cs%\"}
    next_cs=${next_cs#\"}
    if [ -n "$next_cs" ]; then
      cs=$next_cs
    fi
    unset next_pub next_cs
  done
fi
if [ "$has_api_env" -ne 1 ]; then
  note_block "systemd EnvironmentFiles does not include /etc/kit-platform/api.env"
fi
sys_env=$(systemctl show kit-platform-api -p Environment --value 2>/dev/null)
sys_env_rc=$?
if [ "$sys_env_rc" -ne 0 ]; then
  note_block "systemd Environment property could not be read"
else
  if printf '%s' "$sys_env" | grep -q 'ResearchSafeHost__SuppressBackgroundWriters'; then
    note_block "suppress flag is present in systemd Environment"
  else
    log "SYSTEMD_ENVIRONMENT_SUPPRESS_FLAG ABSENT"
  fi
  if printf '%s' "$sys_env" | grep -q 'PersonalIntelligence__ExchangeCredentialKey'; then
    note_block "exchange credential setting is present in systemd Environment"
  else
    log "SYSTEMD_ENVIRONMENT_EXCHANGE_CREDENTIAL ABSENT"
  fi
  if printf '%s' "$sys_env" | grep -q 'PersonalIntelligence__ResearchPublication__Root='; then
    token=$(printf '%s' "$sys_env" | tr ' ' '\n' | awk -F= 'index($0,"PersonalIntelligence__ResearchPublication__Root=")==1 {v=substr($0,index($0,"=")+1)} END{print v}')
    if [[ "$token" =~ ^/[A-Za-z0-9._/-]+$ ]]; then
      pub=$token
      log "PUBLICATION_SOURCE systemd-environment"
    else
      pub=""
      note_block "publication path in systemd Environment is not a plain absolute path"
    fi
    unset token
  fi
  if printf '%s' "$sys_env" | grep -q 'ConnectionStrings__Default='; then
    skip_db=1
    note_block "connection string is set in systemd Environment; it was not parsed and the catalog was not run"
  fi
fi
unset sys_env
if ! command -v nginx >/dev/null 2>&1; then
  note_block "nginx command is missing"
else
  nginx_dump=$(nginx -T 2>/dev/null)
  nginx_rc=$?
  if [ "$nginx_rc" -ne 0 ] || [ -z "${nginx_dump:-}" ]; then
    note_block "effective nginx configuration could not be read"
  else
    nginx_kw=$(printf '%s\n' "$nginx_dump" | grep -cE '^[[:space:]]*(root|alias)[[:space:]]+' || true)
    nginx_roots=$(printf '%s\n' "$nginx_dump" | sed -n -E 's/^[[:space:]]*(root|alias)[[:space:]]+//p' | sed -E 's/[[:space:]]*;.*$//; s:/*$::')
    log "NGINX_EFFECTIVE_BEGIN"
    nginx_count=0
    nginx_bad=0
    while IFS= read -r rootdir; do
      [ -n "$rootdir" ] || continue
      nginx_count=$((nginx_count + 1))
      case "$rootdir" in
        *'$'*|*' '*) nginx_bad=1; note_block "an effective nginx root is not a plain path" ;;
        /*) log "NGINX_WEB_ROOT ${rootdir}" ;;
        *) nginx_bad=1; note_block "an effective nginx root is not absolute" ;;
      esac
    done <<< "$nginx_roots"
    log "NGINX_WEB_ROOT_COUNT ${nginx_count}"
    if [ "${nginx_kw:-0}" -ne "$nginx_count" ] || [ "$nginx_bad" -ne 0 ]; then
      nginx_ok=0
      note_block "effective nginx root directives were not fully parsed"
    else
      nginx_ok=1
    fi
    log "NGINX_EFFECTIVE_END"
  fi
  unset nginx_dump
fi
if [ -z "${pub:-}" ]; then
  log "PUBLICATION_CANONICAL ${CANONICAL_PUBLICATION}"
  log "PUBLICATION_CONFIGURED no"
  log "PUBLICATION_PATH ABSENT"
  note_block "publication path is not set; the API returns MISSING until PersonalIntelligence__ResearchPublication__Root is ${CANONICAL_PUBLICATION}"
  pub=$CANONICAL_PUBLICATION
elif [ "$pub" = "$CANONICAL_PUBLICATION" ]; then
  log "PUBLICATION_CANONICAL ${CANONICAL_PUBLICATION}"
  log "PUBLICATION_CONFIGURED yes"
else
  log "PUBLICATION_CANONICAL ${CANONICAL_PUBLICATION}"
  log "PUBLICATION_CONFIGURED no"
  note_block "publication path is not the canonical directory"
fi
if [ -n "${pub:-}" ]; then
  case "$pub" in
    *[Pp]assword*|*ConnectionString*|*://*|*Token*|*Secret*)
      log "PUBLICATION_PATH REDACTED"
      note_block "publication value is not a plain directory path"
      pub=""
      ;;
    /*) log "PUBLICATION_PATH ${pub}" ;;
    *)
      log "PUBLICATION_PATH REDACTED"
      note_block "publication path is not absolute"
      pub=""
      ;;
  esac
fi
if [ -n "${pub:-}" ]; then
  pub_real=$(readlink -f "$pub" 2>/dev/null || true)
  if [ -z "${pub_real:-}" ]; then
    note_block "publication path could not be resolved"
  else
    log "PUBLICATION_REALPATH ${pub_real}"
    case "$pub_real" in
      /var/www/kit-platform|/var/www/kit-platform/*)
        note_block "publication path is inside /var/www/kit-platform"
        ;;
    esac
    if [ "$nginx_ok" -ne 1 ]; then
      note_block "publication path was not compared with the effective nginx configuration"
    else
      unsafe=0
      while IFS= read -r rootdir; do
        [ -n "$rootdir" ] || continue
        root_real=$(readlink -f "$rootdir" 2>/dev/null || true)
        if [ -z "${root_real:-}" ]; then
          unsafe=1
          note_block "an effective nginx root could not be resolved"
          continue
        fi
        if path_is_inside "$pub_real" "$root_real"; then
          log "PUBLICATION_INSIDE_WEB_ROOT ${root_real}"
          unsafe=1
        fi
      done <<< "${nginx_roots:-}"
      if [ "$unsafe" -ne 0 ]; then
        note_block "publication path is inside an effective nginx web root"
      else
        log "PUBLICATION_OUTSIDE_WEB_ROOTS yes"
      fi
    fi
  fi
  if [ -d "$pub" ]; then
    log "PUBLICATION_EXISTS yes"
    log "PUBLICATION_OWNER $(stat -c '%U' "$pub")"
    log "PUBLICATION_MODE $(stat -c '%A' "$pub")"
    mode=$(stat -c '%a' "$pub" 2>/dev/null || true)
    perm=${mode: -3}
    if [ -z "${mode:-}" ] || [ "${#perm}" -ne 3 ]; then
      note_block "publication mode could not be read"
    else
      group_digit=${perm:1:1}
      other_digit=${perm:2:1}
      case "$group_digit" in
        2|3|6|7) note_block "publication directory is writable by group" ;;
        *) log "PUBLICATION_GROUP_WRITE no" ;;
      esac
      case "$other_digit" in
        2|3|6|7) note_block "publication directory is writable by others" ;;
        *) log "PUBLICATION_OTHER_WRITE no" ;;
      esac
    fi
    ls_bits=$(ls -ld "$pub" 2>/dev/null | awk '{print $1}')
    if [ -z "${ls_bits:-}" ]; then
      note_block "publication directory listing could not be read"
    else
    case "$ls_bits" in
      *+)
        if ! command -v getfacl >/dev/null 2>&1; then
          note_block "publication path has an ACL and getfacl is missing"
        else
          acl_text=$(getfacl -c -p "$pub" 2>/dev/null) || acl_text=""
          acl_rc=$?
          acl_user=0
          acl_group=0
          acl_other=0
          acl_mask=0
          acl_extended=0
          acl_write=0
          if [ "$acl_rc" -ne 0 ] || [ -z "${acl_text:-}" ]; then
            note_block "publication ACL could not be read"
          else
            while IFS= read -r acl_line || [ -n "${acl_line:-}" ]; do
              [ -n "$acl_line" ] || continue
              if [[ "$acl_line" == user::* ]]; then
                acl_user=1
              elif [[ "$acl_line" == group::* ]]; then
                acl_group=1
              elif [[ "$acl_line" == other::* ]]; then
                acl_other=1
              elif [[ "$acl_line" == mask::* ]]; then
                acl_mask=1
                acl_extended=1
              elif [[ "$acl_line" =~ ^user:[^:]+: ]]; then
                acl_extended=1
              elif [[ "$acl_line" =~ ^group:[^:]+: ]]; then
                acl_extended=1
              elif [[ "$acl_line" == default:* ]]; then
                acl_extended=1
              fi
              acl_perm=${acl_line##*:}
              case "$acl_perm" in
                *w*)
                  case "$acl_line" in
                    user::*|default:user::*|mask::*) ;;
                    *) acl_write=1 ;;
                  esac
                  ;;
              esac
            done <<< "$acl_text"
            if [ "$acl_user" -ne 1 ] || [ "$acl_group" -ne 1 ] || [ "$acl_other" -ne 1 ]; then
              note_block "publication ACL is incomplete"
            elif [ "$acl_extended" -ne 1 ] || [ "$acl_mask" -ne 1 ]; then
              note_block "publication ACL is incomplete"
            elif [ "$acl_write" -ne 0 ]; then
              note_block "publication directory ACL grants write beyond the owner"
            else
              log "PUBLICATION_ACL_GROUP_OTHER_WRITE no"
            fi
          fi
          unset acl_text acl_line acl_perm
        fi
        ;;
      *)
        log "PUBLICATION_ACL_GROUP_OTHER_WRITE no"
        ;;
    esac
    fi
    if [ "${user:-}" = "www-data" ] && id www-data >/dev/null 2>&1; then
      if sudo -u www-data test -r "$pub" 2>/dev/null && sudo -u www-data test -x "$pub" 2>/dev/null; then
        log "PUBLICATION_SERVICE_ACCESS read-execute"
      else
        note_block "service user www-data cannot read and traverse the publication path"
      fi
    else
      note_block "publication access was not tested because the service user is not www-data"
    fi
  else
    log "PUBLICATION_EXISTS no"
    note_block "publication path is not an existing directory"
  fi
fi
if [ "$skip_db" -eq 0 ]; then
  if [ -z "${cs:-}" ]; then
    note_block "API connection string could not be read from systemd environment files"
  else
    db=$(cs_field "$cs" database || true)
    dbhost=$(cs_field "$cs" host || true)
    dbport=$(cs_field "$cs" port || true)
    dbuser=$(cs_field "$cs" username || true)
    if [ -z "${dbuser:-}" ]; then dbuser=$(cs_field "$cs" "user id" || true); fi
    if [ -z "${dbuser:-}" ]; then dbuser=$(cs_field "$cs" uid || true); fi
    dbpass=$(cs_field "$cs" password || true)
    if [ -z "${dbpass:-}" ]; then dbpass=$(cs_field "$cs" pwd || true); fi
  fi
fi
unset cs
endpoint_host=""
if [ "$skip_db" -eq 0 ] && [ -n "${db:-}" ]; then
  if [[ ! "$db" =~ ^[A-Za-z0-9_]+$ ]]; then
    unset db dbhost dbport
    note_block "API database name is not a plain identifier"
  elif [[ ! "${dbport:-}" =~ ^[0-9]+$ ]]; then
    unset db dbhost dbport
    note_block "database port is not set; catalog was not run"
  else
    db_transport=""
    case "${dbhost:-}" in
      127.0.0.1|::1) endpoint_host=$dbhost; db_transport=tcp ;;
      localhost) endpoint_host=localhost; db_transport=tcp ;;
      "")
        unset db dbhost dbport
        note_block "database host is not set; default socket was not used and catalog was not run"
        ;;
      /*)
        if [[ "$dbhost" =~ ^/[A-Za-z0-9._/-]+$ ]]; then
          endpoint_host=$dbhost
          db_transport=socket
        else
          unset db dbhost dbport
          note_block "database socket directory is not a plain absolute path; catalog was not run"
        fi
        ;;
      *)
        unset db dbhost dbport
        note_block "API database host is not a proven local endpoint; catalog was not run"
        ;;
    esac
  fi
fi
if [ "$skip_db" -eq 0 ] && [ -n "${db:-}" ]; then
  if ! command -v psql >/dev/null 2>&1 || ! command -v ss >/dev/null 2>&1; then
    unset db dbhost dbport endpoint_host
    note_block "psql or ss is missing; database endpoint was not proven and catalog was not run"
    log "NARRATIVE_CATALOG skipped"
  else
    if [ "$endpoint_host" = "localhost" ]; then
      listen_v4=$(ss -H -ltn sport = :"$dbport" 2>/dev/null | awk -v p="$dbport" '$4 == ("127.0.0.1:" p) {found=1} END{if (found) print "127.0.0.1"}')
      listen_v6=$(ss -H -ltn sport = :"$dbport" 2>/dev/null | awk -v p="$dbport" '$4 == ("[::1]:" p) {found=1} END{if (found) print "::1"}')
      if [ -n "$listen_v4" ] && [ -z "$listen_v6" ]; then
        endpoint_host=127.0.0.1
      elif [ -z "$listen_v4" ] && [ -n "$listen_v6" ]; then
        endpoint_host=::1
      else
        unset db dbhost dbport endpoint_host
        note_block "localhost does not map to one local database listener; catalog was not run"
        log "NARRATIVE_CATALOG skipped"
      fi
    fi
  fi
fi
if [ "$skip_db" -eq 0 ] && [ -n "${db:-}" ]; then
  listener_rc=0
  if [ "${db_transport:-}" = "socket" ]; then
    listener_pid=$(ss -H -xlp 2>/dev/null | awk -v dir="$endpoint_host" -v port="$dbport" '{
      if (index($0, "postgres") == 0) next
      suffix = ".s.PGSQL." port
      pos = index($0, suffix)
      if (pos == 0) next
      start = pos
      while (start > 1 && substr($0, start - 1, 1) != " ") start--
      path = substr($0, start, pos + length(suffix) - start)
      n = split(path, parts, "/")
      file = parts[n]
      dirpath = substr(path, 1, length(path) - length(file) - 1)
      if (dirpath != dir) next
      pidpos = index($0, "pid=")
      if (pidpos == 0) next
      pid = substr($0, pidpos + 4)
      sub(/[^0-9].*/, "", pid)
      if (pid ~ /^[0-9]+$/) print pid
    }')
    listener_rc=$?
  else
    listener_pid=$(ss -H -ltnp sport = :"$dbport" 2>/dev/null | awk -v host="$endpoint_host" -v port="$dbport" 'index($0, "postgres") == 0 {next} {addr=$4; if (addr == (host ":" port) || addr == ("[" host "]:" port)) {if (match($0, /pid=[0-9]+/)) print substr($0, RSTART + 4, RLENGTH - 4)}}')
    listener_rc=$?
  fi
  listener_count=$(printf '%s\n' "${listener_pid:-}" | awk 'NF{c++} END{print c+0}')
  if [ "$listener_rc" -ne 0 ]; then
    unset db dbhost dbport endpoint_host listener_pid db_transport
    note_block "database listener listing could not be read; catalog was not run"
    log "NARRATIVE_CATALOG skipped"
  elif [ "$listener_count" -ne 1 ]; then
    unset db dbhost dbport endpoint_host listener_pid
    note_block "configured database endpoint has no single postgres listener; catalog was not run"
    log "NARRATIVE_CATALOG skipped"
  else
    log "DB_ENDPOINT ${endpoint_host}:${dbport}"
    log "DB_TRANSPORT_WANTED ${db_transport}"
    log "DB_LISTENER_PID ${listener_pid}"
    psql_host=$endpoint_host
    identity=""
    psql_rc=1
    if [ -z "${dbuser:-}" ] || [[ ! "$dbuser" =~ ^[A-Za-z0-9_.-]+$ ]]; then
      unset db dbhost dbuser dbpass
      note_block "database identity failed: username; catalog was not run"
      log "NARRATIVE_CATALOG skipped"
    elif [ -z "${dbpass:-}" ] || [[ "$dbpass" == *$'\n'* ]] || [[ "$dbpass" == *$'\r'* ]]; then
      unset db dbhost dbuser dbpass
      note_block "database identity failed: password-absent; catalog was not run"
      log "NARRATIVE_CATALOG skipped"
    else
    errfile=$(umask 077; mktemp /tmp/sb-research-psql-err.XXXXXX)
    identity=$(PGPASSWORD="$dbpass" PGPASSFILE=/dev/null psql -h "$endpoint_host" -p "$dbport" -U "$dbuser" -d "$db" -w -v ON_ERROR_STOP=1 -Atc "SELECT current_database() || '|' || current_setting('port') || '|' || COALESCE(host(inet_server_addr()), 'socket') || '|' || COALESCE(inet_server_port()::text, 'socket');" 2>"$errfile")
    psql_rc=$?
    identity_code=""
    if [ "$psql_rc" -ne 0 ]; then
      identity_code=$(psql_error_code "$errfile")
    fi
    rm -f "$errfile"
    unset errfile db dbhost
    identity_safe=0
    if [ "$psql_rc" -eq 0 ] && [[ "${identity:-}" =~ ^[A-Za-z0-9_]+\|[0-9]+\|(127\.0\.0\.1|::1|socket)\|([0-9]+|socket)$ ]]; then
      identity_safe=1
    fi
    if [ "$identity_safe" -ne 1 ] || [ -z "${psql_host:-}" ]; then
      unset dbport endpoint_host listener_pid psql_host identity db_transport
      if [ "$psql_rc" -ne 0 ]; then
        case "${identity_code:-}" in
          auth|pg_hba|connect|database|role|empty|unrecognized) ;;
          *) identity_code=unrecognized ;;
        esac
        note_block "database identity failed: ${identity_code}; catalog was not run"
      else
        note_block "database identity failed: format; catalog was not run"
      fi
      log "NARRATIVE_CATALOG skipped"
      unset identity_code
    else
      log "DB_IDENTITY ${identity}"
      if [ "$db_transport" = "tcp" ]; then
        log "DB_TRANSPORT tcp"
      else
        log "DB_TRANSPORT socket"
      fi
      id_name=${identity%%|*}
      id_rest=${identity#*|}
      id_port=${id_rest%%|*}
      id_tail=${id_rest#*|}
      id_addr=${id_tail%%|*}
      id_seen_port=${id_tail#*|}
      endpoint_ok=0
      if [ "$id_name" = "novixa_prod" ] && [ "$id_port" = "$dbport" ] && [ "$psql_host" = "$endpoint_host" ]; then
        if [ "$db_transport" = "tcp" ] && [ "$id_addr" = "$endpoint_host" ] && [ "$id_seen_port" = "$dbport" ]; then
          endpoint_ok=1
        elif [ "$db_transport" = "socket" ] && [ "$id_addr" = "socket" ] && [ "$id_seen_port" = "socket" ]; then
          endpoint_ok=1
        fi
      fi
      if [ "$endpoint_ok" -ne 1 ]; then
        unset dbport endpoint_host listener_pid psql_host db_transport
        note_block "identity result does not match the configured database endpoint; catalog was not run"
        log "NARRATIVE_CATALOG skipped"
      else
        column=$(PGPASSWORD="$dbpass" PGPASSFILE=/dev/null psql -h "$psql_host" -p "$dbport" -U "$dbuser" -d novixa_prod -w -v ON_ERROR_STOP=1 -Atc "SELECT data_type FROM information_schema.columns WHERE table_schema = 'pack_content' AND table_name = 'brand' AND column_name = 'narrative_json';" 2>/dev/null)
        col_rc=$?
        unset dbport endpoint_host listener_pid psql_host db_transport
        if [ "$col_rc" -ne 0 ]; then
          note_block "narrative_json catalog query failed"
          log "NARRATIVE_CATALOG skipped"
        elif [ -z "${column:-}" ]; then
          log "NARRATIVE_DATA_TYPE ABSENT"
          note_block "pack_content.brand.narrative_json is absent"
        else
          case "$column" in
            jsonb|json)
              log "NARRATIVE_DATA_TYPE ${column}"
              log "NARRATIVE_TYPE_OK yes"
              ;;
            [A-Za-z]*)
              if [[ "$column" =~ ^[A-Za-z][A-Za-z0-9_]*$ ]]; then
                log "NARRATIVE_DATA_TYPE ${column}"
                note_block "narrative_json type is not json or jsonb"
              else
                note_block "narrative_json catalog result was not a plain type name"
              fi
              ;;
            *)
              note_block "narrative_json catalog result was not a plain type name"
              ;;
          esac
        fi
      fi
      unset id_name id_rest id_port id_tail id_addr id_seen_port
    fi
    fi
  fi
else
  unset db dbhost dbport dbuser dbpass endpoint_host
  log "NARRATIVE_CATALOG skipped"
fi
unset dbpass dbuser
if [ -L /var/www/kit-platform/api/KitPlatform.Api.dll ]; then
  note_block "live API DLL path is a symlink"
elif [ -f /var/www/kit-platform/api/KitPlatform.Api.dll ]; then
  dll_hash=$(sha256sum /var/www/kit-platform/api/KitPlatform.Api.dll | awk '{print $1}') || dll_hash=""
  if [ -z "${dll_hash:-}" ]; then
    note_block "live API DLL hash could not be read"
  else
    log "LIVE_DLL_SHA256 ${dll_hash}"
  fi
else
  note_block "live API DLL is missing"
fi
log "CANDIDATE_DLL_SHA256 F394E67433952A70E26F80C22F1D5B96822BFE00ED59C59D8D9A53DAF24A0BA9"
if [ -L /var/www/kit-platform/api/uploads ]; then
  note_block "uploads path is a symlink"
  backup_known=0
elif [ -d /var/www/kit-platform/api/uploads ]; then
  log "UPLOADS_BYTES $(du -sb /var/www/kit-platform/api/uploads | awk '{print $1}')"
else
  log "UPLOADS_ABSENT"
fi
settings_seen=0
for cfg in /var/www/kit-platform/api/appsettings.json /var/www/kit-platform/api/appsettings.Production.json /var/www/kit-platform/api/appsettings.Development.json; do
  if [ -L "$cfg" ]; then
    note_block "an appsettings file is a symlink"
    backup_known=0
  elif [ -f "$cfg" ]; then
    settings_seen=1
    log "APPSETTINGS_BYTES $(basename "$cfg") $(stat -c '%s' "$cfg")"
  fi
done
if [ "$settings_seen" -eq 0 ]; then
  log "APPSETTINGS_PRESENT no"
fi
backup_need=0
if [ "${backup_known:-1}" -ne 0 ]; then
  backup_known=1
fi
if [ -L /var/www/kit-platform/api ]; then
  api_bytes=0
  backup_known=0
  note_block "API tree path is a symlink"
elif [ -d /var/www/kit-platform/api ]; then
  api_bytes=$(du -sb /var/www/kit-platform/api 2>/dev/null | awk '{print $1}')
  if [[ "${api_bytes:-}" =~ ^[0-9]+$ ]] && [ "$api_bytes" -gt 0 ]; then
    log "API_TREE_BYTES ${api_bytes}"
    backup_need=$api_bytes
  else
    api_bytes=0
    backup_known=0
    note_block "API tree size could not be read"
  fi
else
  api_bytes=0
  backup_known=0
  note_block "API tree is missing"
fi
if [ -L /etc/kit-platform/api.env ]; then
  backup_known=0
  note_block "api.env path is a symlink"
  env_size=""
else
  env_size=$(stat -c '%s' /etc/kit-platform/api.env 2>/dev/null || true)
fi
if [[ "${env_size:-}" =~ ^[0-9]+$ ]]; then
  backup_need=$((backup_need + env_size))
  log "BACKUP_ENV_BYTES /etc/kit-platform/api.env ${env_size}"
else
  backup_known=0
  note_block "api.env size could not be read"
fi
if [ "${#env_paths[@]}" -gt 0 ]; then
  for envpath in "${env_paths[@]}"; do
    [ "$envpath" = "/etc/kit-platform/api.env" ] && continue
    if [ -L "$envpath" ]; then
      backup_known=0
      note_block "a systemd environment file is a symlink"
      continue
    fi
    env_size=$(stat -c '%s' "$envpath" 2>/dev/null || true)
    if [[ "${env_size:-}" =~ ^[0-9]+$ ]]; then
      backup_need=$((backup_need + env_size))
      log "BACKUP_ENV_BYTES ${envpath} ${env_size}"
    else
      backup_known=0
      note_block "a systemd environment file size could not be read"
    fi
  done
fi
if [ -z "${pub:-}" ] || [ ! -e "${pub:-}" ]; then
  log "BACKUP_PUBLICATION_BYTES not-in-copy"
elif [ -L "$pub" ]; then
  backup_known=0
  note_block "publication path is a symlink; its backup size was not proven"
elif [ -d "$pub" ]; then
  pub_bytes=$(du -sb "$pub" 2>/dev/null | awk '{print $1}')
  if [[ "${pub_bytes:-}" =~ ^[0-9]+$ ]]; then
    backup_need=$((backup_need + pub_bytes))
    log "BACKUP_PUBLICATION_BYTES ${pub_bytes}"
  else
    backup_known=0
    note_block "publication tree size could not be read"
  fi
  unset pub_bytes
else
  log "BACKUP_PUBLICATION_BYTES not-in-copy"
fi
log "BACKUP_COUNTED_RULE api-tree plus api.env plus other environment files plus the publication tree when that directory exists"
if [ "$backup_known" -eq 1 ]; then
  backup_margin=$((backup_need / 5))
  if [ "$backup_margin" -lt 67108864 ]; then
    backup_margin=67108864
  fi
  backup_required=$((backup_need + backup_margin))
  log "BACKUP_CONTENT_BYTES ${backup_need}"
  log "BACKUP_MARGIN_BYTES ${backup_margin}"
  log "BACKUP_REQUIRED_BYTES ${backup_required}"
else
  backup_required=0
  note_block "backup size could not be proven"
fi
if [ ! -d /var/backups ]; then
  note_block "backup parent /var/backups is absent"
else
  parent=$(readlink -f /var/backups 2>/dev/null || true)
  if [ -z "${parent:-}" ]; then
    note_block "backup parent could not be resolved"
  else
  if path_is_inside "$parent" /var/www/kit-platform; then
    note_block "backup parent is inside the web tree"
  else
      backup_unsafe=0
      backup_compared=0
      if [ "$nginx_ok" -eq 1 ]; then
        backup_compared=1
        while IFS= read -r rootdir; do
          [ -n "$rootdir" ] || continue
          root_real=$(readlink -f "$rootdir" 2>/dev/null || true)
          if [ -z "${root_real:-}" ]; then
            backup_compared=0
            note_block "an effective nginx root could not be resolved"
            continue
          fi
          if path_is_inside "$parent" "$root_real"; then
            backup_unsafe=1
          fi
        done <<< "${nginx_roots:-}"
      else
        note_block "backup parent was not compared with the effective nginx configuration"
      fi
      if [ "$backup_unsafe" -ne 0 ]; then
        note_block "backup parent is inside an effective nginx web root"
      fi
      avail=$(df -B1 --output=avail "$parent" 2>/dev/null | tail -n 1 | tr -d '[:space:]')
      log "BACKUP_PARENT ${parent}"
      if [[ ! "${avail:-}" =~ ^[0-9]+$ ]]; then
        note_block "backup free space could not be read"
      else
        log "BACKUP_PARENT_AVAIL_BYTES ${avail}"
        log "BACKUP_PROPOSAL ${parent}/kit-research-$(date -u +%Y%m%dT%H%M%SZ)"
        if [ "$backup_known" -ne 1 ] || [ "${backup_required:-0}" -le 0 ]; then
          note_block "backup free space was not compared because the required size is unknown"
        elif [ "$nginx_ok" -ne 1 ] || [ "$backup_compared" -ne 1 ] || [ "$backup_unsafe" -ne 0 ]; then
          note_block "backup free space was not accepted because the backup location was not proven outside every nginx web root"
        elif [ "$avail" -lt "$backup_required" ]; then
          note_block "backup parent does not have enough free space"
        else
          log "BACKUP_SPACE OK"
        fi
      fi
  fi
  fi
fi
log "BACKUP_CREATED no"
log "DISK_BEGIN"
disk_ok=0
while IFS= read -r line; do
  [ -n "$line" ] || continue
  disk_ok=1
  log "$line"
done < <(df -B1 --output=target,avail,pcent / /var /var/www /var/backups 2>/dev/null || df -B1 --output=target,avail,pcent / /var /var/www 2>/dev/null || true)
if [ "$disk_ok" -ne 1 ]; then
  note_block "disk listing could not be read"
fi
log "DISK_END"
if [ "$blocked" -ne 0 ]; then
  finish_blocked
fi
log "PREFLIGHT_DONE complete"
publish_result
exit 0
