"""Offline tests: never download models, submit GPU work, or launch AppHost."""
import contextlib
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import setup_models as models
import setup_comfyui as art

class SetupTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.out = Path(self.temp.name) / "result.json"
        self.stdout = contextlib.redirect_stdout(io.StringIO())
        self.stdout.__enter__()
        self.addCleanup(self.stdout.__exit__, None, None, None)

    def model_args(self, *extra):
        return models.parser().parse_args(["--output", str(self.out), *extra])

    def art_args(self, *extra):
        return art.parser().parse_args([
            "--workflow", str(models.ROOT / "workflows/fatewake-api.json"),
            "--overlay", str(models.ROOT / "workflows/fatewake-overlay-api.json"),
            "--model", "sd15", "--output", str(self.out), *extra])

    def test_models_dry_run_has_no_effects(self):
        with patch.object(models, "request") as http:
            models.setup(self.model_args())
            http.assert_not_called()
        self.assertFalse(self.out.exists())

    def test_local_origins_only(self):
        for url in ["https://example.com", "http://user:pass@localhost", "http://localhost/api", "http://localhost?key=x"]:
            with self.assertRaises(ValueError):
                models.local_url(url)
        self.assertEqual(models.local_url("http://127.0.0.1:11434/"), "http://127.0.0.1:11434")

    def test_redirect_refused(self):
        with self.assertRaises(ValueError):
            models.NoRedirect().redirect_request(None, None, 302, "", {}, "https://example.com")

    def test_pull_missing_and_preserve_existing(self):
        installed = [{"name": "qwen3:8b", "digest": "old"}]
        calls = []
        def fake(base, path, payload=None, timeout=30):
            calls.append((path, payload))
            if path == "/api/tags": return {"models": installed.copy()}
            if path == "/api/pull":
                installed.append({"name": payload["model"], "digest": "new"})
                return {"status": "success"}
            return {"version": "test"}
        with patch.object(models, "request", side_effect=fake):
            models.setup(self.model_args("--apply"))
        self.assertEqual([p["model"] for path, p in calls if path == "/api/pull"], ["qwen3:14b"])
        self.assertEqual(len(json.loads(self.out.read_text())["models"]), 2)

    def test_pull_failure_does_not_write_success(self):
        with patch.object(models, "request", side_effect=[{}, {"models": []}, {"status": "failed"}]):
            with self.assertRaises(ValueError): models.setup(self.model_args("--apply"))
        self.assertFalse(self.out.exists())

    def test_smoke_is_bounded_and_unloads(self):
        inventory = {"models": [{"name": "qwen3:8b"}]}
        with patch.object(models, "request", side_effect=[{}, inventory, {"done": True, "response": "Hello"}, inventory]) as http:
            models.setup(self.model_args("--apply", "--smoke", "--models", "qwen3:8b"))
        payload = http.call_args_list[2].args[2]
        self.assertEqual(payload["keep_alive"], 0)
        self.assertEqual(payload["options"]["num_predict"], 32)

    def test_art_dry_run_has_no_effects(self):
        with patch.object(art, "request") as http:
            art.setup(self.art_args())
            http.assert_not_called()
        self.assertFalse(self.out.exists())

    def test_art_requires_generation_acknowledgment(self):
        with self.assertRaises(ValueError): art.setup(self.art_args("--apply"))
        with self.assertRaises(ValueError): art.setup(self.art_args("--launch"))

    def test_all_shipped_workflow_templates_parse(self):
        for path in (models.ROOT / "workflows").glob("*-api.json"):
            with self.subTest(path=path): art.workflow(path)

    def test_bad_seed_rejected(self):
        path = Path(self.temp.name) / "bad.json"
        source = (models.ROOT / "workflows/fatewake-api.json").read_text()
        path.write_text(source.replace("__FATEWAKE_SEED__", '"__FATEWAKE_SEED__"'))
        with self.assertRaises(ValueError): art.workflow(path)

    def test_missing_node_or_checkpoint_rejected(self):
        graph = {"1": {"class_type": "Loader", "inputs": {"ckpt_name": "absent"}}}
        with self.assertRaises(ValueError): art.validate_nodes(graph, {})
        with self.assertRaises(ValueError):
            art.validate_nodes(graph, {"Loader": {"input": {"required": {"ckpt_name": [["installed"]]}}}})

    def test_art_apply_writes_only_local_overrides(self):
        args = self.art_args("--apply", "--confirm-workflows-tested")
        catalog = {node["class_type"]: {} for path in (args.workflow, args.overlay) for node in art.workflow(path).values()}
        with patch.object(art, "request", side_effect=[{}, catalog]), patch.object(art.subprocess, "run") as run:
            art.setup(args)
            run.assert_not_called()
        config = json.loads(self.out.read_text())
        self.assertEqual(config["ArtGeneration__ComfyUI__HistoryPath"], "history")
        self.assertTrue(config["ArtGeneration__ComfyUI__ApiKey"])
        self.assertFalse(config["ArtGeneration__ComfyUI__ApiKey"].strip())
        self.assertTrue(Path(config["ArtGeneration__ComfyUI__WorkflowPath"]).is_absolute())

    def test_launch_receives_overrides_without_mutating_environment(self):
        args = self.art_args("--apply", "--launch", "--confirm-workflows-tested")
        catalog = {node["class_type"]: {} for path in (args.workflow, args.overlay) for node in art.workflow(path).values()}
        with patch.dict(art.os.environ, {"ArtGeneration__ComfyUI__ApiKey": "test-secret"}), patch.object(art, "request", side_effect=[{}, catalog]), patch.object(art.subprocess, "run") as run:
            run.return_value.returncode = 0
            art.setup(args)
            self.assertEqual(art.os.environ["ArtGeneration__ComfyUI__ApiKey"], "test-secret")
            self.assertEqual(run.call_args.kwargs["env"]["ArtGeneration__ComfyUI__ApiKey"], " ")

if __name__ == "__main__":
    unittest.main()
