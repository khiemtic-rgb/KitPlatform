import json, os, subprocess, urllib.request, urllib.error
from pathlib import Path

token = json.loads(Path(r"C:\Users\Admin\AppData\Local\Temp\famixa-login.json").read_text(encoding="utf-8"))["accessToken"]
psql = r"C:\Program Files\PostgreSQL\18\bin\psql.exe"

def sql(query: str) -> str:
    proc = subprocess.run(
        [psql, "-h", "localhost", "-p", "5432", "-U", "kitplatform", "-d", "kitplatform", "-t", "-A", "-c", query],
        capture_output=True, text=True, encoding="utf-8", errors="replace",
        env={**os.environ, "PGPASSWORD": "kitplatform_dev_2026", "PGCLIENTENCODING": "UTF8"},
        check=True,
    )
    return proc.stdout.strip()

facts = json.loads(sql("""
SELECT json_build_object(
  'sha', (SELECT byte_sha256 FROM pack_content.production_artifacts WHERE artifact_id = '27872dda-69f0-4758-b1c9-4fd214b292a5'),
  'sh03', (SELECT status || '|' || action FROM pack_content.production_tasks WHERE task_id = 'e280783f-7691-49f3-9aa2-77636c99bac0'),
  'selection', (SELECT status || '|' || provider_id || '|' || model_id FROM pack_content.production_ai_selection WHERE selection_id = '81a491a4-0ad6-451f-8bc2-2082920dfc37'),
  'veo', (SELECT status FROM pack_content.production_ai_selection WHERE selection_id = '6d7ef39b-97cb-4d27-95b2-9035416ee605'),
  'prior', (SELECT status || '|' || coalesce(provider_job_id,'') FROM pack_content.production_attempts WHERE attempt_id = '5691d6c5-e45a-4873-8378-0605a5548b56'),
  'issued', (SELECT count(*) FROM pack_content.production_execution_permit WHERE build_id = '30ba4061-a443-4c0d-85aa-1d13ff4fdedb' AND shot_code = 'EP01-SC01-SH02' AND action = 'CREATE_VIDEO' AND provider_id = 'runway' AND status = 'ISSUED')
)::text
"""))
print("preflight", json.dumps(facts), flush=True)
if facts["sha"] != "9fe2bd76034203a14531631fd1911e3ef15f1249c588259a17c692c952305d57":
    raise SystemExit("keyframe sha drifted")
if facts["sh03"] != "RUNNING|CREATE_IMAGE":
    raise SystemExit("sh03 drifted")
if facts["selection"] != "ACTIVE|runway|gen4_turbo":
    raise SystemExit("selection drifted")
if facts["veo"] != "SUPERSEDED":
    raise SystemExit("veo history drifted")
if not str(facts["prior"]).startswith("PROVIDER_FAILED|e256f3e9-"):
    raise SystemExit("prior job state unclear")
if int(facts["issued"]) != 0:
    raise SystemExit("unexpected issued permit")

def post(path, body, timeout):
    req = urllib.request.Request(
        "http://localhost:5290" + path,
        data=json.dumps(body).encode("utf-8"),
        headers={"Authorization": "Bearer " + token, "Content-Type": "application/json"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(req, timeout=timeout) as res:
            return res.status, json.loads(res.read().decode("utf-8"))
    except urllib.error.HTTPError as ex:
        detail = ex.read().decode("utf-8", errors="replace")[:800]
        raise SystemExit(f"{path} http={ex.code} {detail}")

status, permit = post("/api/content/production/execution-permits", {
    "buildId": "30ba4061-a443-4c0d-85aa-1d13ff4fdedb",
    "shotCode": "EP01-SC01-SH02",
    "episodeCode": "EP01",
    "sceneCode": "SC01",
    "action": "CREATE_VIDEO",
    "providerId": "runway",
    "modelId": "gen4_turbo",
    "reason": "SH02 motion revision after INTERNAL.BAD_OUTPUT.CODE01. JSON visual contract removed. 5 seconds.",
}, 30)
print("permit", status, permit["permitId"], permit["status"], flush=True)

story = "(Minh ngập ngừng, đặt cặp xuống ghế rồi chậm chạp kéo khóa. Cậu rút ra tờ bài kiểm tra đã bị gấp góc, đưa hai tay cho mẹ rồi cúi gằm mặt.)"
visual = "Medium framing, eye level, 40mm. Minh is the focus, looking down at a folded paper. Linh stays in the foreground. The bag rests on the chair. Two people only, each once."
status, created = post("/api/content/production/requests", {
    "buildId": "30ba4061-a443-4c0d-85aa-1d13ff4fdedb",
    "episodeCode": "EP01",
    "sceneCode": "SC01",
    "shotCode": "EP01-SC01-SH02",
    "story": story,
    "actingBeat": "MOTION_PROMPT_REVISION",
    "visualContract": visual,
    "timing": "5s",
    "aspectRatio": "16:9",
    "durationSec": 5,
    "idempotencyKey": "famixa-sh02-runway-gen4-motion-revision-20261018",
    "keyframeArtifactId": "27872dda-69f0-4758-b1c9-4fd214b292a5",
    "providerId": "runway",
    "modelId": "gen4_turbo",
    "authorizePaidNetwork": True,
    "action": "CREATE_VIDEO",
}, 420)
Path(r"C:\Users\Admin\AppData\Local\Temp\sh02-runway-result.json").write_text(json.dumps(created, indent=2), encoding="utf-8")
print("create", status, created.get("providerJobId"), created.get("artifactId"), created.get("byteSha256"), created.get("storageKey"), flush=True)
