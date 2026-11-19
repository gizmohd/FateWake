#!/usr/bin/env python3
"""Prepare local Ollama evaluation models. No downloads without --apply."""
import argparse
import json
from pathlib import Path
import sys
import urllib.error
import urllib.parse
import urllib.request

ROOT = Path(__file__).resolve().parents[2]

class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        raise ValueError("Redirect refused; use the direct local service URL")

def local_url(value):
    parsed = urllib.parse.urlsplit(value)
    if (parsed.scheme not in ("http", "https") or parsed.hostname not in
        ("localhost", "127.0.0.1", "::1") or parsed.username or parsed.password
        or parsed.query or parsed.fragment or parsed.path not in ("", "/")):
        raise ValueError("Use a loopback service origin (or a local SSH tunnel), without credentials/path")
    return value.rstrip("/")

def request(base, path, payload=None, timeout=30):
    data = None if payload is None else json.dumps(payload).encode()
    req = urllib.request.Request(local_url(base) + path, data=data,
                                 headers={"Content-Type": "application/json"})
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), NoRedirect())
    with opener.open(req, timeout=timeout) as response:
        result = json.load(response)
    if isinstance(result, dict) and result.get("error"):
        raise ValueError(str(result["error"]))
    return result

def save_json(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")
    temporary.replace(path)

def setup(args):
    base = local_url(args.url)
    models = list(dict.fromkeys(args.models))
    if not models or any(not model.strip() for model in models):
        raise ValueError("At least one nonempty model name is required")
    print(json.dumps({"action": "pull missing models and record inventory", "url": base,
                      "models": models, "smoke": args.smoke, "output": str(args.output)}, indent=2))
    if not args.apply:
        print("DRY RUN: no requests, downloads, or files written. Add --apply to execute.")
        return
    request(base, "/api/version")
    inventory = request(base, "/api/tags").get("models", [])
    existing = {entry.get("name") for entry in inventory}
    for model in models:
        if model not in existing:
            print("Pulling " + model + " (large download; wait for completion)", flush=True)
            result = request(base, "/api/pull", {"model": model, "stream": False}, args.timeout)
            if result.get("status") != "success":
                raise ValueError("Pull did not report success: " + model)
        else:
            print("Already installed; preserving current digest: " + model)
        if args.smoke:
            # Sequential requests, bounded output, unload immediately to avoid two resident LLMs.
            result = request(base, "/api/generate", {
                "model": model, "prompt": "Reply with one short greeting.", "think": False,
                "stream": False, "keep_alive": 0,
                "options": {"num_predict": 32, "num_ctx": 2048}}, args.timeout)
            if not result.get("done") or not result.get("response", "").strip():
                raise ValueError("Inference smoke test returned no completed text: " + model)
            print("Inference smoke passed: " + model)
    inventory = request(base, "/api/tags").get("models", [])
    selected = [entry for entry in inventory if entry.get("name") in models]
    if {entry.get("name") for entry in selected} != set(models):
        raise ValueError("Downloaded models missing from final inventory")
    save_json(args.output, {"provider": "ollama", "baseUrl": base, "models": selected,
                           "smokePassed": args.smoke,
                           "note": "Evaluation inventory only; Fatewake narrative integration is not implemented."})
    print("Verified inventory saved. No Fatewake/OpenClaw provider configuration changed.")

def parser():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--url", default="http://127.0.0.1:11434")
    p.add_argument("--models", nargs="+", default=["qwen3:14b", "qwen3:8b"])
    p.add_argument("--output", type=Path, default=ROOT / "data/models/ollama-inventory.json")
    p.add_argument("--timeout", type=int, default=7200, help="Per pull/inference timeout in seconds")
    p.add_argument("--smoke", action="store_true", help="Run one short inference per model, sequentially")
    p.add_argument("--apply", action="store_true", help="Allow downloads, inference, and inventory write")
    return p

if __name__ == "__main__":
    try:
        setup(parser().parse_args())
    except (ValueError, OSError, urllib.error.URLError) as error:
        print("Setup failed: " + str(error), file=sys.stderr)
        sys.exit(1)
