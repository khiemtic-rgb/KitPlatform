"""Provider adapters for image-to-video. This module plans a job. It does not spend credit unless a caller passes a transport."""

from __future__ import annotations

import os

PROVIDERS = {
    "runway": {
        "secretEnv": "RUNWAY_API_KEY",
        "url": "https://api.dev.runwayml.com/v1/image_to_video",
        "model": "gen4_turbo",
        "modes": ("image_to_video", "first_last"),
    },
    "veo": {
        "secretEnv": "VEO_JOIN_FILE",
        "url": "https://{location}-aiplatform.googleapis.com/v1/projects/{project}/locations/{location}/publishers/google/models/veo-3.1-generate-001:predictLongRunning",
        "model": "veo-3.1",
        "modes": ("image_to_video",),
    },
    "fal": {
        "secretEnv": "FAL_KEY",
        "url": "https://queue.fal.run/fal-ai/wan-i2v",
        "model": "wan-2.1",
        "modes": ("image_to_video",),
    },
}


def credential_present(env_name: str) -> bool:
    return bool(os.environ.get(env_name, "").strip())


_REDESIGN = ("redraw", "redesign", "restyle", "replace the", "new layout", "recreate the screen", "different application")


def motion_instruction(scene: dict, provider: dict | None = None) -> str:
    """Motion, action, camera, and duration only. The source picture is not redesigned."""
    spec = provider or {}
    duration = int(spec.get("durationSeconds") or scene.get("durationSeconds") or 5)
    camera = str(scene.get("camera") or spec.get("camera") or "hold").strip()
    action = str(scene.get("action") or "").strip()
    raw = str(scene.get("prompt") or "").strip()
    if not action and raw and not any(token in raw.lower() for token in _REDESIGN):
        action = raw
    if not action:
        action = "Hold the approved picture."
    return (
        "Motion only. Do not redesign, restyle, or replace the source image. "
        f"Duration {duration} seconds. Camera: {camera}. Action: {action}."
    )


def build_request(provider: dict, scene: dict, image_uri: str, last_uri: str | None = None) -> dict:
    provider_id = str(provider.get("id") or "")
    spec = PROVIDERS.get(provider_id)
    if spec is None:
        raise ValueError(f"Provider không có adapter: {provider_id}")
    mode = str(provider.get("mode") or "image_to_video")
    if mode not in spec["modes"] and mode != "image_to_video":
        raise ValueError(f"Provider {provider_id} không nhận mode {mode}.")
    duration = int(provider.get("durationSeconds") or 5)
    ratio = str(provider.get("ratio") or "1280:720")
    model = str(provider.get("model") or spec["model"])
    prompt = motion_instruction(scene, provider)
    if provider_id == "runway":
        image = image_uri if not last_uri else [
            {"uri": image_uri, "position": "first"},
            {"uri": last_uri, "position": "last"},
        ]
        body = {
            "model": model,
            "promptImage": image,
            "promptText": prompt,
            "duration": duration,
            "ratio": ratio,
        }
    else:
        body = {
            "model": model,
            "image": image_uri,
            "prompt": prompt,
            "duration": duration,
            "ratio": ratio,
        }
        if last_uri:
            body["lastImage"] = last_uri
    return {
        "provider": provider_id,
        "method": "POST",
        "url": spec["url"],
        "json": body,
        "authEnv": spec["secretEnv"],
    }


def submit(request: dict, secret: str, transport=None) -> dict:
    """Send only through an injected transport. An empty secret never calls it."""
    if not str(secret or "").strip():
        return {
            "status": "BLOCKED",
            "sent": False,
            "code": "CREDENTIAL_MISSING",
            "reason": f"Thiếu {request.get('authEnv')}. Không gửi request.",
        }
    if transport is None:
        return {
            "status": "READY_TO_SUBMIT",
            "sent": False,
            "code": "NETWORK_NOT_ENABLED",
            "reason": "Credential có mặt nhưng lần chạy này không gửi mạng.",
        }
    payload = transport(request, secret)
    task_id = ""
    if isinstance(payload, dict):
        task_id = str(payload.get("id") or "")
    return {
        "status": "SUBMITTED",
        "sent": True,
        "providerTaskId": task_id,
        "visualReview": "VISUAL_REVIEW_REQUIRED",
    }
