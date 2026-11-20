import json, os, subprocess, urllib.request, urllib.error
from pathlib import Path

token = json.loads(Path(r"C:\Users\Admin\AppData\Local\Temp\famixa-login.json").read_text(encoding="utf-8"))["accessToken"]
psql = r"C:\Program Files\PostgreSQL\18\bin\psql.exe"
env = {"PGPASSWORD": "kitplatform_dev_2026", "PGCLIENTENCODING": "UTF8"}

def sql(query: str) -> str:
    proc = subprocess.run(
        [psql, "-h", "localhost", "-p", "5432", "-U", "kitplatform", "-d", "kitplatform", "-t", "-A", "-c", query],
        capture_output=True, text=True, encoding="utf-8", errors="replace", env={**os.environ, **env},
        check=True,
    )
    return proc.stdout.strip()

facts = json.loads(sql("""
SELECT json_build_object(
  'story', story,
  'acting', acting_beat,
  'visual', visual_contract,
  'sha', (SELECT byte_sha256 FROM pack_content.production_artifacts WHERE artifact_id = '27872dda-69f0-4758-b1c9-4fd214b292a5'),
  'kfTenant', (SELECT tenant_id::text FROM pack_content.production_artifacts WHERE artifact_id = '27872dda-69f0-4758-b1c9-4fd214b292a5'),
  'sh03', (SELECT status FROM pack_content.production_tasks WHERE task_id = 'e280783f-7691-49f3-9aa2-77636c99bac0'),
  'activeProvider', (SELECT provider_id FROM pack_content.production_ai_selection WHERE selection_id = '6d7ef39b-97cb-4d27-95b2-9035416ee605'),
  'activeStatus', (SELECT status FROM pack_content.production_ai_selection WHERE selection_id = '6d7ef39b-97cb-4d27-95b2-9035416ee605'),
  'runwayJobs', (
    SELECT count(*) FROM pack_content.production_attempts a
    JOIN pack_content.production_tasks t ON t.task_id = a.task_id
    WHERE t.build_id = '30ba4061-a443-4c0d-85aa-1d13ff4fdedb'
      AND t.shot_code = 'EP01-SC01-SH02'
      AND lower(t.provider_id) = 'runway'
      AND coalesce(a.provider_job_id, '') <> ''
  )
)::text
FROM pack_content.production_task_input_snapshots
WHERE snapshot_id = '9e7a2bb3-c8a2-48d0-a699-454f7db96271'
"""))

print("preflight", json.dumps({k: facts[k] for k in facts if k not in ("story", "acting", "visual")}))
if facts["sha"] != "9fe2bd76034203a14531631fd1911e3ef15f1249c588259a17c692c952305d57":
    raise SystemExit("keyframe sha drifted")
if facts["kfTenant"] != "4028941b-49b3-4ba2-8815-778daef4a2d3":
    raise SystemExit("keyframe tenant drifted")
if facts["sh03"] != "RUNNING":
    raise SystemExit("sh03 drifted: " + str(facts["sh03"]))
if facts["activeStatus"] != "ACTIVE" or facts["activeProvider"] != "google":
    raise SystemExit("selection drifted")
if int(facts["runwayJobs"]) != 0:
    raise SystemExit("runway job already exists")

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
        detail = ex.read().decode("utf-8", errors="replace")[:500]
        raise SystemExit(f"{path} http={ex.code} {detail}")

status, superseded = post("/api/content/production/ai-selections/supersede", {
    "expectedSelectionId": "6d7ef39b-97cb-4d27-95b2-9035416ee605",
    "buildId": "30ba4061-a443-4c0d-85aa-1d13ff4fdedb",
    "shotCode": "EP01-SC01-SH02",
    "episodeCode": "EP01",
    "sceneCode": "SC01",
    "stage": "VIDEO",
    "providerId": "runway",
    "modelId": "gen4_turbo",
    "reason": "SH02 CREATE_VIDEO runway gen4_turbo. Veo row retained as history.",
}, 30)
print("supersede", status, superseded["superseded"]["selectionId"], superseded["superseded"]["status"], superseded["superseded"]["providerId"], superseded["active"]["selectionId"], superseded["active"]["providerId"], superseded["active"]["modelId"])

status, permit = post("/api/content/production/execution-permits", {
    "buildId": "30ba4061-a443-4c0d-85aa-1d13ff4fdedb",
    "shotCode": "EP01-SC01-SH02",
    "episodeCode": "EP01",
    "sceneCode": "SC01",
    "action": "CREATE_VIDEO",
    "providerId": "runway",
    "modelId": "gen4_turbo",
    "reason": "SH02 one Runway gen4_turbo start, 5 seconds, 1280:720.",
}, 30)
print("permit", status, permit["permitId"], permit["status"], permit["providerId"], permit["modelId"])

status, created = post("/api/content/production/requests", {
    "buildId": "30ba4061-a443-4c0d-85aa-1d13ff4fdedb",
    "episodeCode": "EP01",
    "sceneCode": "SC01",
    "shotCode": "EP01-SC01-SH02",
    "story": facts["story"],
    "actingBeat": facts["acting"],
    "visualContract": facts["visual"],
    "timing": "5s",
    "aspectRatio": "16:9",
    "durationSec": 5,
    "idempotencyKey": "famixa-sh02-runway-gen4-once-20261018",
    "keyframeArtifactId": "27872dda-69f0-4758-b1c9-4fd214b292a5",
    "providerId": "runway",
    "modelId": "gen4_turbo",
    "authorizePaidNetwork": True,
    "action": "CREATE_VIDEO",
}, 420)
Path(r"C:\Users\Admin\AppData\Local\Temp\sh02-runway-result.json").write_text(json.dumps(created, indent=2), encoding="utf-8")
print("create", status, created.get("providerJobId"), created.get("artifactId"), created.get("byteSha256"), created.get("storageKey"))
