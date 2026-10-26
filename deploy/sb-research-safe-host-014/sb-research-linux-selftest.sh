#!/bin/bash
# Isolated checks for the Research preflight publisher.
# Safe on the production host before deployment: it uses only /tmp and does not
# call systemctl, nginx, psql, or touch /var/www, /etc, or the API service.
# On a non-Linux machine the Linux-only cases are NOT RUN. They are not PASS.
set -u
SCRIPT=${1:-$(cd "$(dirname "$0")" && pwd)/sb-research-preflight.sh}
PASS=0
FAIL=0
NOTRUN=0
ok() { PASS=$((PASS + 1)); printf 'PASS %s\n' "$1"; }
bad() { FAIL=$((FAIL + 1)); printf 'FAIL %s\n' "$1"; }
notrun() { NOTRUN=$((NOTRUN + 1)); printf 'NOT RUN %s\n' "$1"; }
linux=0
uname -s | grep -q Linux && linux=1

bash "$SCRIPT" --logic-test > /tmp/sb-research-logic.out
logic_rc=$?
if [ "$logic_rc" -eq 0 ] && grep -q "LOGIC_TEST_DONE pass" /tmp/sb-research-logic.out; then
  ok "path logic"
else
  bad "path logic"
  cat /tmp/sb-research-logic.out
fi

if [ -e /dev/full ]; then
  perl -e 'use Fcntl; sysopen(my $f, "/dev/full", O_WRONLY) or die $!; my $n = syswrite($f, "x"); exit(defined $n ? 0 : 1);' >/dev/null 2>/tmp/sb-research-full.err
  full_rc=$?
  if [ "$full_rc" -ne 0 ]; then
    ok "syswrite reports an error on /dev/full; this is not the production stage file"
  else
    bad "syswrite to /dev/full succeeded"
  fi
else
  notrun "syswrite on /dev/full: /dev/full is absent"
fi
if [ "$linux" -ne 1 ]; then
  notrun "symlink destination: this machine is not Linux and cannot create a real symlink"
  notrun "symlink-to-directory: this machine is not Linux"
  notrun "durable mode 0600: Git bash reports 644 to a second process; prove it on the host with this script"
  notrun "stage file on a full filesystem: do not fill /tmp"
else
  work=$(mktemp -d /tmp/sb-research-selftest.XXXXXX)
  canary=$work/canary
  printf '%s\n' 'CANARY' >"$canary"
  rm -f /tmp/sb-research-preflight-result.txt
  ln -s "$canary" /tmp/sb-research-preflight-result.txt
  bash "$SCRIPT" --publish-self-test >/tmp/sb-research-self-symlink.out 2>/tmp/sb-research-self-symlink.err
  pub_rc=$?
  if [ "$(cat "$canary")" = "CANARY" ] && [ ! -e "$canary/stage" ]; then
    ok "symlink target unchanged rc=$pub_rc"
  else
    bad "symlink target changed"
  fi
  if [ -L /tmp/sb-research-preflight-result.txt ] || [ -f /tmp/sb-research-preflight-result.txt ]; then
    if [ -d /tmp/sb-research-preflight-result.txt ]; then
      bad "symlink destination became a directory"
    else
      ok "symlink destination was not followed into another directory"
    fi
  else
    bad "symlink destination missing"
  fi
  rm -f /tmp/sb-research-preflight-result.txt

  target=$work/target-dir
  mkdir -p "$target"
  printf '%s\n' 'KEEP' >"$target/keep"
  ln -s "$target" /tmp/sb-research-preflight-result.txt
  bash "$SCRIPT" --publish-self-test >/tmp/sb-research-self-dirlink.out 2>/tmp/sb-research-self-dirlink.err || true
  if [ "$(cat "$target/keep")" = "KEEP" ] && [ ! -e "$target/stage" ]; then
    ok "symlink-to-directory not written inside"
  else
    bad "symlink-to-directory written inside"
  fi
  rm -f /tmp/sb-research-preflight-result.txt

  bash "$SCRIPT" --publish-self-test >/tmp/sb-research-self-mode.out 2>/tmp/sb-research-self-mode.err
  mode_rc=$?
  mode=$(stat -c '%a' /tmp/sb-research-preflight-result.txt 2>/dev/null || echo missing)
  if [ "$mode_rc" -eq 0 ] && [ "$mode" = "600" ] && [ ! -L /tmp/sb-research-preflight-result.txt ]; then
    ok "durable mode 0600"
  else
    bad "durable mode 0600 mode=$mode rc=$mode_rc"
  fi
  rm -f /tmp/sb-research-preflight-result.txt
  notrun "stage file on a full filesystem: do not fill the production /tmp"
  rm -rf "$work"
fi

printf 'SUMMARY pass=%s fail=%s not_run=%s\n' "$PASS" "$FAIL" "$NOTRUN"
if [ "$FAIL" -ne 0 ]; then
  exit 1
fi
exit 0
