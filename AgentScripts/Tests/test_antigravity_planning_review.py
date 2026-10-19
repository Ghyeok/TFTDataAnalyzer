"""Exercise the review entry point with local CLI events, without an AI request."""
import contextlib
import hashlib
import io
import json
import os
import pathlib
import runpy
import shutil
import unittest
import uuid
from unittest import mock


ROOT = pathlib.Path(__file__).resolve().parents[2]
ENTRY = ROOT / "AgentScripts/Invoke-AntigravityPlanningReview.py"
TASK_ID = "offline-schema-regression"
DIGEST = hashlib.sha256(json.dumps([], sort_keys=True).encode()).hexdigest()


@contextlib.contextmanager
def owned_directory(parent, prefix="offline-review-"):
    parent = parent.resolve(strict=True)
    path = parent / (prefix + uuid.uuid4().hex)
    path.mkdir()
    try:
        yield str(path)
    finally:
        if path.resolve().parent != parent or path.is_symlink():
            raise RuntimeError("Owned test directory boundary changed")
        shutil.rmtree(path)


class FakeProcess:
    pid = 100000000
    returncode = 0

    def __init__(self, payload, init_first=True):
        self.stdin = io.StringIO()
        self.stderr = io.StringIO()
        events = [{"event": "init", "init": {"model": "gemini-4-argon-medium"}}] if init_first else []
        events.append({"event": "result", "result": {"status": "SUCCESS", "structured_output": payload}})
        self.stdout = io.StringIO("\n".join(json.dumps(event) for event in events) + "\n")

    def poll(self):
        return self.returncode

    def wait(self, timeout=None):
        return self.returncode


class StructuredReviewTests(unittest.TestCase):
    def run_review(self, payload, prepare=None, init_first=True):
        allowed = (ROOT / "AntigravityCollab/tasks").resolve()
        with owned_directory(allowed) as folder:
            task = pathlib.Path(folder).resolve()
            self.assertTrue(task.is_relative_to(allowed))
            (task / "request.md").write_text("Supplied text only.", encoding="utf-8")
            (task / "manifest.json").write_text(json.dumps({
                "taskId": TASK_ID, "inputDigest": DIGEST, "files": []
            }), encoding="utf-8")
            if prepare is not None:
                prepare(task)
            with mock.patch("sys.argv", [str(ENTRY), str(task), "--scoped-text-review"]), \
                    mock.patch("subprocess.check_output", return_value="1.4.2"), \
                    mock.patch("subprocess.Popen", return_value=FakeProcess(payload, init_first)), \
                    mock.patch("tempfile.TemporaryDirectory", side_effect=lambda prefix: owned_directory(ROOT / "Library/TftAnalyzerValidation", prefix)), \
                    contextlib.redirect_stdout(io.StringIO()):
                with self.assertRaises(SystemExit) as stopped:
                    runpy.run_path(str(ENTRY), run_name="__main__")
            runtime = json.loads((task / "outputs/runtime.json").read_text(encoding="utf-8"))
            review_path = task / "outputs/review.json"
            review = json.loads(review_path.read_text(encoding="utf-8")) if review_path.exists() else None
            return stopped.exception.code, runtime, review

    @staticmethod
    def valid_payload():
        return {"taskId": TASK_ID, "inputDigest": DIGEST, "verdict": "pass",
                "findings": [], "unverified": []}

    def test_complete_review_is_preserved(self):
        payload = self.valid_payload()
        code, runtime, review = self.run_review(payload)
        self.assertEqual(code, 0)
        self.assertTrue(runtime["protocolSucceeded"])
        self.assertEqual(review, payload)

    def test_success_before_init_and_request_is_rejected(self):
        code, runtime, review = self.run_review(self.valid_payload(), init_first=False)
        self.assertEqual(code, 1)
        self.assertFalse(runtime["requestSent"])
        self.assertFalse(runtime["protocolSucceeded"])
        self.assertEqual(runtime["failure"], "ValueError:ResultBeforeRequest")
        self.assertIsNone(review)

    def test_incomplete_or_wrongly_typed_review_is_rejected(self):
        invalid = [
            {"taskId": TASK_ID, "inputDigest": DIGEST, "verdict": "pass"},
            dict(self.valid_payload(), findings=""),
            dict(self.valid_payload(), findings=[{}]),
            dict(self.valid_payload(), unverified="none"),
        ]
        for payload in invalid:
            with self.subTest(payload=payload):
                code, runtime, review = self.run_review(payload)
                self.assertEqual(code, 1)
                self.assertFalse(runtime["protocolSucceeded"])
                self.assertIsNone(review)

    def test_control_files_cannot_alias_files_outside_the_task(self):
        for relative in ("request.md", "manifest.json", "outputs/review.json", "outputs/runtime.json"):
            with self.subTest(path=relative), owned_directory(ROOT / "Library/TftAnalyzerValidation") as folder:
                target = pathlib.Path(folder) / "outside.txt"

                def prepare(task):
                    path = task / relative
                    path.parent.mkdir(exist_ok=True)
                    target.write_bytes(path.read_bytes() if path.exists() else b"sentinel")
                    if path.exists():
                        path.unlink()
                    os.link(target, path)

                with self.assertRaisesRegex(ValueError, "LinkedCollaborationFile"):
                    self.run_review(self.valid_payload(), prepare)
                self.assertTrue(target.exists())


if __name__ == "__main__":
    unittest.main()
