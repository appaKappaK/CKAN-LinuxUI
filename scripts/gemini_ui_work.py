#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import os
import sys
import urllib.error
from datetime import datetime, timedelta, timezone
from pathlib import Path

from gemini_ui_review import (
    DEFAULT_COOLDOWN_SECONDS,
    call_gemini,
    collect_screenshots,
    compute_input_hash,
    extract_text,
    find_repo_root,
    load_dotenv,
    read_status,
    reviewed_hash_from_status,
    write_error,
    write_skip,
)


DEFAULT_MODEL = "gemini-2.5-flash"


def collect_code_files(code_dir: Path) -> list[Path]:
    return sorted([path for path in code_dir.rglob("*") if path.is_file()],
                  key=lambda path: str(path.relative_to(code_dir)).lower())


def build_prompt(prompt_path: Path,
                 context_path: Path,
                 screenshots: list[Path],
                 code_dir: Path,
                 code_files: list[Path],
                 focus: str | None) -> str:
    prompt_text = prompt_path.read_text(encoding="utf-8").strip()
    context_text = context_path.read_text(encoding="utf-8").strip()
    screenshot_list = "\n".join(f"- {path.name}" for path in screenshots)
    code_blocks: list[str] = []

    for path in code_files:
        relative = path.relative_to(code_dir).as_posix()
        language = "xml" if path.suffix == ".axaml" else "csharp"
        code_blocks.append(
            f"File: {relative}\n```{language}\n{path.read_text(encoding='utf-8')}\n```"
        )

    focus_text = f"\nAdditional focus for this request:\n{focus.strip()}\n" if focus and focus.strip() else ""

    return (
        f"{prompt_text}\n\n"
        "Additional repository context:\n"
        f"{context_text}\n\n"
        "Screenshots attached to this request:\n"
        f"{screenshot_list}\n"
        f"{focus_text}\n"
        "Focused source snapshots:\n\n"
        f"{'\n\n'.join(code_blocks)}\n\n"
        "Please return Markdown only. When suggesting code, use fenced code blocks and label each block with the target file path."
    )


def write_feedback(work_dir: Path,
                   model: str,
                   screenshots: list[Path],
                   code_files: list[Path],
                   input_hash: str,
                   markdown: str,
                   prompt: str,
                   response: dict) -> None:
    generated_at = datetime.now(timezone.utc).isoformat()
    work_dir.mkdir(parents=True, exist_ok=True)

    header = [
        "# Gemini UI Work",
        "",
        f"- Generated: `{generated_at}`",
        f"- Model: `{model}`",
        "- Screenshots:",
        *[f"  - `{path.name}`" for path in screenshots],
        "- Code Files:",
        *[f"  - `{path.name}`" for path in code_files],
        "",
    ]

    (work_dir / "latest.md").write_text("\n".join(header) + markdown + "\n", encoding="utf-8")
    (work_dir / "latest.json").write_text(json.dumps(response, indent=2), encoding="utf-8")
    (work_dir / "request.md").write_text(prompt + "\n", encoding="utf-8")
    (work_dir / "status.json").write_text(
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
                "code_files": [path.name for path in code_files],
            },
            indent=2,
        ),
        encoding="utf-8",
    )


def main() -> int:
    parser = argparse.ArgumentParser(description="Generate Gemini implementation-oriented UI work for CKAN Linux.")
    parser.add_argument("--repo-root", help="Path to the repository root")
    parser.add_argument("--optional", action="store_true",
                        help="Do not fail if the API key is missing or the API request fails")
    parser.add_argument("--model", default=os.environ.get("GEMINI_WORK_MODEL", DEFAULT_MODEL),
                        help="Gemini model to use")
    parser.add_argument("--force", action="store_true",
                        help="Ignore cooldown and unchanged-input checks")
    parser.add_argument("--focus", help="Extra implementation focus to append to the prompt")
    args = parser.parse_args()

    repo_root = Path(args.repo_root).resolve() if args.repo_root else find_repo_root()
    bundle_dir = repo_root / ".gemini-review" / "work"
    screenshots_dir = bundle_dir / "screenshots"
    code_dir = bundle_dir / "code"
    status_path = bundle_dir / "status.json"

    load_dotenv(repo_root)
    api_key = os.environ.get("GEMINI_API_KEY", "").strip()
    if not api_key:
        reason = "GEMINI_API_KEY is not set. Add it to the environment or .env to enable Gemini UI work."
        write_skip(bundle_dir, reason)
        print(f"Gemini work skipped: {reason}")
        return 0 if args.optional else 1

    prompt_path = bundle_dir / "GEMINI-WORK-PROMPT.md"
    context_path = bundle_dir / "CONTEXT.md"
    screenshots = collect_screenshots(screenshots_dir)
    code_files = collect_code_files(code_dir)

    if not prompt_path.exists() or not context_path.exists() or not screenshots or not code_files:
        reason = "Gemini work bundle is incomplete. Run LinuxGUI visual tests first."
        write_skip(bundle_dir, reason)
        print(f"Gemini work skipped: {reason}")
        return 0 if args.optional else 1

    prompt = build_prompt(prompt_path, context_path, screenshots, code_dir, code_files, args.focus)
    input_hash = compute_input_hash(args.model, prompt, screenshots)
    cooldown_seconds = int(os.environ.get("GEMINI_WORK_MIN_INTERVAL_SECONDS",
                                          str(DEFAULT_COOLDOWN_SECONDS)))
    previous_status = read_status(status_path)
    previous_reviewed_hash = reviewed_hash_from_status(previous_status)

    if not args.force:
        if previous_reviewed_hash == input_hash and (bundle_dir / "latest.md").exists():
            reason = "Skipping Gemini work because the prompt, code bundle, and screenshots have not changed."
            write_skip(bundle_dir,
                       reason,
                       preserve_latest=True,
                       previous_status=previous_status,
                       model=args.model,
                       input_hash=input_hash)
            print(f"Gemini work skipped: {reason}")
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
                        "Skipping Gemini work because the cooldown window is still active "
                        f"({remaining}s remaining)."
                    )
                    write_skip(bundle_dir,
                               reason,
                               preserve_latest=True,
                               previous_status=previous_status,
                               model=args.model,
                               input_hash=input_hash)
                    print(f"Gemini work skipped: {reason}")
                    return 0
            except ValueError:
                pass

    try:
        response = call_gemini(api_key, args.model, prompt, screenshots)
        markdown = extract_text(response)
        write_feedback(bundle_dir, args.model, screenshots, code_files, input_hash, markdown, prompt, response)
        print(f"Gemini work written to {bundle_dir / 'latest.md'}")
        return 0
    except urllib.error.HTTPError as exc:
        details = exc.read().decode("utf-8", errors="replace")
        write_error(bundle_dir, f"HTTP {exc.code}", details, model=args.model, input_hash=input_hash)
        print(f"Gemini work failed with HTTP {exc.code}")
        return 0 if args.optional else 1
    except Exception as exc:
        details = str(exc)
        write_error(bundle_dir, exc.__class__.__name__, details, model=args.model, input_hash=input_hash)
        print(f"Gemini work failed: {details}")
        return 0 if args.optional else 1


if __name__ == "__main__":
    sys.exit(main())
