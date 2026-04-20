#!/usr/bin/env python3
from __future__ import annotations

import argparse
import base64
import hashlib
import json
import mimetypes
import os
import sys
import urllib.error
import urllib.parse
import urllib.request
from datetime import datetime, timedelta, timezone
from pathlib import Path


DEFAULT_MODEL = "gemini-2.5-flash-lite"
DEFAULT_ENDPOINT = "https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent"
DEFAULT_COOLDOWN_SECONDS = 1800


def find_repo_root() -> Path:
    current = Path.cwd().resolve()
    for candidate in [current, *current.parents]:
        if (candidate / "CKAN.sln").exists():
            return candidate
    raise FileNotFoundError("Could not locate repo root from current working directory.")


def load_dotenv(repo_root: Path) -> None:
    dotenv_path = repo_root / ".env"
    if not dotenv_path.exists():
        return

    for raw_line in dotenv_path.read_text(encoding="utf-8").splitlines():
        line = raw_line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue

        key, value = line.split("=", 1)
        key = key.strip()
        value = value.strip().strip("'").strip('"')
        os.environ.setdefault(key, value)


def collect_screenshots(shots_dir: Path) -> list[Path]:
    return sorted(shots_dir.glob("*.png"), key=lambda path: path.name.lower())


def build_prompt(prompt_path: Path, context_path: Path, screenshots: list[Path]) -> str:
    prompt_text = prompt_path.read_text(encoding="utf-8").strip()
    context_text = context_path.read_text(encoding="utf-8").strip()
    screenshot_list = "\n".join(f"- {path.name}" for path in screenshots)

    return (
        f"{prompt_text}\n\n"
        "Additional repository context:\n"
        f"{context_text}\n\n"
        "Screenshots attached to this request:\n"
        f"{screenshot_list}\n\n"
        "Please return Markdown only."
    )


def compute_input_hash(model: str, prompt: str, screenshots: list[Path]) -> str:
    digest = hashlib.sha256()
    digest.update(model.encode("utf-8"))
    digest.update(b"\0")
    digest.update(prompt.encode("utf-8"))
    digest.update(b"\0")

    for path in screenshots:
        digest.update(path.name.encode("utf-8"))
        digest.update(b"\0")
        digest.update(path.read_bytes())
        digest.update(b"\0")

    return digest.hexdigest()


def read_status(status_path: Path) -> dict:
    if not status_path.exists():
        return {}

    try:
        return json.loads(status_path.read_text(encoding="utf-8"))
    except Exception:
        return {}


def reviewed_hash_from_status(status: dict) -> str | None:
    reviewed = status.get("reviewed_input_hash")
    if isinstance(reviewed, str) and reviewed:
        return reviewed

    if status.get("status") == "ok":
        legacy = status.get("input_hash")
        if isinstance(legacy, str) and legacy:
            return legacy

    return None


def encode_image(path: Path) -> dict[str, dict[str, str]]:
    mime_type, _ = mimetypes.guess_type(path.name)
    if mime_type is None:
        mime_type = "image/png"

    data = base64.b64encode(path.read_bytes()).decode("ascii")
    return {
        "inlineData": {
            "mimeType": mime_type,
            "data": data,
        }
    }


def call_gemini(api_key: str, model: str, prompt: str, screenshots: list[Path]) -> dict:
    endpoint = DEFAULT_ENDPOINT.format(model=model)
    url = f"{endpoint}?key={urllib.parse.quote(api_key)}"
    payload = {
        "contents": [
            {
                "role": "user",
                "parts": [{"text": prompt}, *[encode_image(path) for path in screenshots]],
            }
        ],
        "generationConfig": {
            "temperature": 0.3,
            "topP": 0.9,
            "topK": 40,
        },
    }

    request = urllib.request.Request(
        url,
        data=json.dumps(payload).encode("utf-8"),
        headers={"Content-Type": "application/json; charset=utf-8"},
        method="POST",
    )
    with urllib.request.urlopen(request, timeout=120) as response:
        return json.loads(response.read().decode("utf-8"))


def extract_text(response: dict) -> str:
    candidates = response.get("candidates") or []
    for candidate in candidates:
        content = candidate.get("content") or {}
        for part in content.get("parts") or []:
            text = part.get("text")
            if text:
                return text.strip()
    raise ValueError("Gemini response did not contain any text parts.")


def write_feedback(feedback_dir: Path,
                   model: str,
                   screenshots: list[Path],
                   input_hash: str,
                   markdown: str,
                   response: dict) -> None:
    feedback_dir.mkdir(parents=True, exist_ok=True)
    generated_at = datetime.now(timezone.utc).isoformat()

    header = [
        "# Gemini UI Review",
        "",
        f"- Generated: `{generated_at}`",
        f"- Model: `{model}`",
        "- Screenshots:",
        *[f"  - `{path.name}`" for path in screenshots],
        "",
    ]

    (feedback_dir / "latest.md").write_text("\n".join(header) + markdown + "\n", encoding="utf-8")
    (feedback_dir / "latest.json").write_text(json.dumps(response, indent=2), encoding="utf-8")
    (feedback_dir / "status.json").write_text(
        json.dumps(
            {
                "status": "ok",
                "generated_at": generated_at,
                "last_attempted_at": generated_at,
                "reviewed_input_hash": input_hash,
                "pending_input_hash": None,
                "model": model,
                "input_hash": input_hash,
                "screenshots": [path.name for path in screenshots],
            },
            indent=2,
        ),
        encoding="utf-8",
    )


def write_skip(feedback_dir: Path,
               reason: str,
               *,
               preserve_latest: bool = False,
               previous_status: dict | None = None,
               model: str | None = None,
               input_hash: str | None = None) -> None:
    feedback_dir.mkdir(parents=True, exist_ok=True)
    skipped_at = datetime.now(timezone.utc).isoformat()
    previous_status = previous_status or {}
    last_attempted_at = previous_status.get("last_attempted_at") or previous_status.get("generated_at")
    reviewed_input_hash = reviewed_hash_from_status(previous_status) if preserve_latest else None
    pending_input_hash = None
    if preserve_latest and input_hash and input_hash != reviewed_input_hash:
        pending_input_hash = input_hash
    (feedback_dir / "status.json").write_text(
        json.dumps(
            {
                "status": "skipped",
                "generated_at": previous_status.get("generated_at"),
                "last_attempted_at": last_attempted_at,
                "skipped_at": skipped_at,
                "reason": reason,
                "model": model,
                "reviewed_input_hash": reviewed_input_hash,
                "pending_input_hash": pending_input_hash,
                "input_hash": input_hash,
            },
            indent=2,
        ),
        encoding="utf-8",
    )
    if not preserve_latest:
        (feedback_dir / "latest.md").write_text(
            "# Gemini UI Review\n\n"
            f"- Generated: `{skipped_at}`\n"
            f"- Status: skipped\n"
            f"- Reason: {reason}\n",
            encoding="utf-8",
        )


def write_error(feedback_dir: Path,
                reason: str,
                details: str,
                *,
                model: str | None = None,
                input_hash: str | None = None) -> None:
    feedback_dir.mkdir(parents=True, exist_ok=True)
    generated_at = datetime.now(timezone.utc).isoformat()
    (feedback_dir / "status.json").write_text(
        json.dumps(
            {
                "status": "error",
                "generated_at": generated_at,
                "last_attempted_at": generated_at,
                "reason": reason,
                "details": details,
                "model": model,
                "reviewed_input_hash": None,
                "pending_input_hash": input_hash,
                "input_hash": input_hash,
            },
            indent=2,
        ),
        encoding="utf-8",
    )
    (feedback_dir / "latest.md").write_text(
        "# Gemini UI Review\n\n"
        f"- Generated: `{generated_at}`\n"
        f"- Status: error\n"
        f"- Reason: {reason}\n\n"
        "```text\n"
        f"{details}\n"
        "```\n",
        encoding="utf-8",
    )


def main() -> int:
    parser = argparse.ArgumentParser(description="Generate Gemini UI/UX feedback for CKAN Linux screenshots.")
    parser.add_argument("--repo-root", help="Path to the repository root")
    parser.add_argument("--optional", action="store_true",
                        help="Do not fail if the API key is missing or the API request fails")
    parser.add_argument("--model", default=os.environ.get("GEMINI_REVIEW_MODEL", DEFAULT_MODEL),
                        help="Gemini model to use")
    parser.add_argument("--force", action="store_true",
                        help="Ignore cooldown and unchanged-input checks")
    args = parser.parse_args()

    repo_root = Path(args.repo_root).resolve() if args.repo_root else find_repo_root()
    bundle_dir = repo_root / ".gemini-review"
    feedback_dir = bundle_dir / "feedback"

    load_dotenv(repo_root)
    api_key = os.environ.get("GEMINI_API_KEY", "").strip()
    if not api_key:
        reason = "GEMINI_API_KEY is not set. Add it to the environment or .env to enable automatic review."
        write_skip(feedback_dir, reason)
        print(f"Gemini review skipped: {reason}")
        return 0 if args.optional else 1

    prompt_path = bundle_dir / "GEMINI-REVIEW-PROMPT.md"
    context_path = bundle_dir / "CONTEXT.md"
    screenshots = collect_screenshots(bundle_dir / "screenshots")
    status_path = feedback_dir / "status.json"

    if not prompt_path.exists() or not context_path.exists() or not screenshots:
        reason = "Gemini review bundle is incomplete. Run LinuxGUI visual tests first."
        write_skip(feedback_dir, reason)
        print(f"Gemini review skipped: {reason}")
        return 0 if args.optional else 1

    prompt = build_prompt(prompt_path, context_path, screenshots)
    input_hash = compute_input_hash(args.model, prompt, screenshots)
    cooldown_seconds = int(os.environ.get("GEMINI_REVIEW_MIN_INTERVAL_SECONDS",
                                          str(DEFAULT_COOLDOWN_SECONDS)))
    previous_status = read_status(status_path)
    previous_reviewed_hash = reviewed_hash_from_status(previous_status)

    if not args.force:
        if previous_reviewed_hash == input_hash and (feedback_dir / "latest.md").exists():
            reason = "Skipping Gemini review because the prompt and screenshots have not changed."
            write_skip(feedback_dir,
                       reason,
                       preserve_latest=True,
                       previous_status=previous_status,
                       model=args.model,
                       input_hash=input_hash)
            print(f"Gemini review skipped: {reason}")
            return 0

        previous_attempt_at = previous_status.get("last_attempted_at") or previous_status.get("generated_at")
        if previous_attempt_at:
            try:
                previous_time = datetime.fromisoformat(previous_attempt_at)
                next_allowed = previous_time + timedelta(seconds=cooldown_seconds)
                now = datetime.now(timezone.utc)
                if now < next_allowed:
                    remaining = int((next_allowed - now).total_seconds())
                    reason = (
                        "Skipping Gemini review because the cooldown window is still active "
                        f"({remaining}s remaining)."
                    )
                    write_skip(feedback_dir,
                               reason,
                               preserve_latest=True,
                               previous_status=previous_status,
                               model=args.model,
                               input_hash=input_hash)
                    print(f"Gemini review skipped: {reason}")
                    return 0
            except ValueError:
                pass

    try:
        response = call_gemini(api_key, args.model, prompt, screenshots)
        markdown = extract_text(response)
        write_feedback(feedback_dir, args.model, screenshots, input_hash, markdown, response)
        print(f"Gemini review written to {feedback_dir / 'latest.md'}")
        return 0
    except urllib.error.HTTPError as exc:
        details = exc.read().decode("utf-8", errors="replace")
        write_error(feedback_dir, f"HTTP {exc.code}", details, model=args.model, input_hash=input_hash)
        print(f"Gemini review failed with HTTP {exc.code}")
        return 0 if args.optional else 1
    except Exception as exc:
        details = str(exc)
        write_error(feedback_dir, exc.__class__.__name__, details, model=args.model, input_hash=input_hash)
        print(f"Gemini review failed: {details}")
        return 0 if args.optional else 1


if __name__ == "__main__":
    sys.exit(main())
