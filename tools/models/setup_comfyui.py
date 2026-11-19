#!/usr/bin/env python3
"""Validate installed ComfyUI workflows and prepare opt-in local AppHost settings."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
from setup_models import ROOT, local_url, request, save_json

TOKENS = ("__FATEWAKE_PROMPT__", "__FATEWAKE_NEGATIVE__", "__FATEWAKE_SEED__")

def workflow(path):
    text = Path(path).read_text(encoding="utf-8-sig")
    if any(token not in text for token in (TOKENS[0], TOKENS[2])):
        raise ValueError("Workflow must contain Fatewake prompt and seed tokens: " + str(path))
    # Distilled FLUX graphs can omit negative conditioning entirely.
    if '"__FATEWAKE_SEED__"' in text:
        raise ValueError("Seed token must be unquoted: " + str(path))
    graph = json.loads(text.replace(TOKENS[2], "1"))
    if not isinstance(graph, dict) or not graph or any(
        not isinstance(node, dict) or not isinstance(node.get("inputs"), dict)
        or not isinstance(node.get("class_type"), str) for node in graph.values()):
        raise ValueError("Expected ComfyUI API node graph, not UI export: " + str(path))
    if not any(node["class_type"] == "SaveImage" for node in graph.values()):
        raise ValueError("Workflow must contain SaveImage: " + str(path))
    return graph

def validate_nodes(graph, catalog):
    for node in graph.values():
        kind = node["class_type"]
        if kind not in catalog:
            raise ValueError("ComfyUI node not installed: " + kind)
        schema = catalog[kind].get("input", {})
        fields = dict(schema.get("required", {}))
        fields.update(schema.get("optional", {}))
        for name, value in node["inputs"].items():
            spec = fields.get(name)
            # Validate literal combo values, including checkpoint/model/LoRA filenames.
            if spec and isinstance(spec[0], list) and isinstance(value, str) and not value.startswith("__FATEWAKE_"):
                if value not in spec[0]:
                    raise ValueError("Unavailable input/model: " + kind + "." + name + " = " + value)

def settings(args):
    return {
        "ArtGeneration__Provider": "ComfyUI",
        "ArtGeneration__ComfyUI__Enabled": "true",
        "ArtGeneration__ComfyUI__BaseUrl": local_url(args.url) + "/",
        "ArtGeneration__ComfyUI__HistoryPath": "history",
        # Nonempty whitespace is forwarded by AppHost, then normalized to null by the provider.
        "ArtGeneration__ComfyUI__ApiKey": " ",
        "ArtGeneration__ComfyUI__WorkflowPath": str(args.workflow.resolve()),
        "ArtGeneration__ComfyUI__OverlayWorkflowPath": str(args.overlay.resolve()),
        "ArtGeneration__ComfyUI__Model": args.model,
        "ArtGeneration__ComfyUI__TimeoutSeconds": "900",
    }

def setup(args):
    if args.launch and not args.apply:
        raise ValueError("--launch requires --apply")
    if args.apply and not args.confirm_workflows_tested:
        raise ValueError("Generate with both workflows in ComfyUI first, verify overlay alpha, then pass --confirm-workflows-tested")
    config = settings(args)
    graphs = [workflow(args.workflow), workflow(args.overlay)]
    print(json.dumps(config, indent=2))
    if not args.apply:
        print("DRY RUN: templates parsed; no network calls or configuration writes.")
        return
    request(args.url, "/system_stats")
    catalog = request(args.url, "/object_info")
    for graph in graphs:
        validate_nodes(graph, catalog)
    save_json(args.output, config)
    print("Validated node/model availability; settings saved to " + str(args.output))
    print("No generation submitted. Existing appsettings and user secrets are unchanged.")
    if args.launch:
        env = os.environ.copy()
        env.update(config)
        # Environment overrides only this process tree; cloud credentials are cleared locally.
        result = subprocess.run(["dotnet", "run", "--project", str(ROOT / "src/Fatewake.AppHost" )],
                                cwd=ROOT, env=env, check=False)
        if result.returncode:
            raise ValueError("AppHost exited with code " + str(result.returncode))

def parser():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--url", default="http://127.0.0.1:8188")
    p.add_argument("--workflow", type=Path, required=True, help="Locally tested background API template")
    p.add_argument("--overlay", type=Path, required=True, help="Locally tested alpha-producing overlay API template")
    p.add_argument("--model", required=True, help="Accurate installed model provenance label")
    p.add_argument("--output", type=Path, default=ROOT / "data/models/comfyui-environment.json")
    p.add_argument("--confirm-workflows-tested", action="store_true")
    p.add_argument("--apply", action="store_true")
    p.add_argument("--launch", action="store_true", help="Run AppHost with process-local overrides")
    return p

if __name__ == "__main__":
    try:
        setup(parser().parse_args())
    except (ValueError, OSError) as error:
        print("Setup failed: " + str(error), file=sys.stderr)
        sys.exit(1)
