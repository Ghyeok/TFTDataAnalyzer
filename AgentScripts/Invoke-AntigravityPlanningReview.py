"""One supplied-text planning review; no claim of enforced OS/tool isolation."""
import argparse
import hashlib
import json
import pathlib
import queue
import subprocess
import tempfile
import threading
import time


def validate_review(payload, schema):
    """Validate the JSON-schema subset used by this entry point, before publication."""
    kind = schema["type"]
    if kind == "object":
        if not isinstance(payload, dict) or set(payload) != set(schema["required"]):
            raise ValueError("ReviewFormatMismatch")
        for key, value in payload.items():
            validate_review(value, schema["properties"][key])
    elif kind == "array":
        if not isinstance(payload, list) or len(payload) > schema.get("maxItems", float("inf")):
            raise ValueError("ReviewFormatMismatch")
        for value in payload:
            validate_review(value, schema["items"])
    elif kind == "string":
        if not isinstance(payload, str) or ("enum" in schema and payload not in schema["enum"]):
            raise ValueError("ReviewFormatMismatch")
    elif kind == "boolean":
        if not isinstance(payload, bool):
            raise ValueError("ReviewFormatMismatch")
    else:
        raise ValueError("UnsupportedReviewSchema")


def local_path(path, boundary):
    absolute = path.absolute()
    resolved = absolute.resolve()
    if resolved != absolute or resolved == boundary or not resolved.is_relative_to(boundary):
        raise ValueError("CollaborationPathOutsideBoundaryOrLinked")
    if resolved.is_file() and resolved.stat().st_nlink > 1:
        raise ValueError("LinkedCollaborationFile")
    return resolved


parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("task",type=pathlib.Path)
parser.add_argument("--model",default="gemini-4-argon-medium")
parser.add_argument("--scoped-text-review",action="store_true",help="Explicitly use planning text only; tool access isolation is not proven.")
parser.add_argument("--dry-run",action="store_true")
options = parser.parse_args()
if not (options.scoped_text_review or options.dry_run):
    parser.error("Choose --scoped-text-review explicitly; this is not an isolation boundary.")
root = pathlib.Path(__file__).resolve().parents[1]
allowed = local_path(root / "AntigravityCollab/tasks", root)
task = local_path(options.task, allowed)
outputs = local_path(task / "outputs", task)
review_path = local_path(outputs / "review.json", task)
runtime_path = local_path(outputs / "runtime.json", task)
manifest = json.loads(local_path(task / "manifest.json", task).read_text(encoding="utf-8"))
parts = [local_path(task / "request.md", task).read_text(encoding="utf-8"),
         json.dumps({"taskId": manifest["taskId"], "inputDigest": manifest["inputDigest"]})]
for row in manifest["files"]:
    copied_path = local_path(task / row["copy"], task / "inputs")
    if not copied_path.is_relative_to(task / "inputs"):
        raise ValueError("InputOutsideFrozenInputs")
    data = copied_path.read_bytes()
    if hashlib.sha256(data).hexdigest() != row["sha256"]: raise ValueError("InputHashChanged")
    parts.append("\n## " + row["copy"] + "\n" + data.decode("utf-8-sig"))
prompt = "\n".join(parts)
if hashlib.sha256(json.dumps(manifest["files"],sort_keys=True).encode()).hexdigest() != manifest["inputDigest"]:
    raise ValueError("ManifestDigestChanged")
if len(prompt.encode("utf-8")) >= 48 * 1024: raise ValueError("InputBudgetExceeded")
if options.dry_run:
    print(json.dumps({"taskId":manifest["taskId"],"inputDigest":manifest["inputDigest"],
        "promptBytes":len(prompt.encode("utf-8")),"existingResult":(outputs/"review.json").exists(),
        "accessRestrictionVerified":False,"requestSent":False}))
    raise SystemExit(0)
if (outputs / "review.json").exists(): raise ValueError("ExistingReviewWillNotBeOverwritten")
outputs.mkdir(exist_ok=True)
binary = pathlib.Path.home() / "AppData/Local/agy/bin/agy.exe"
version = subprocess.check_output([str(binary),"--version"],text=True,timeout=10).strip()
finding = {"type": "object", "properties": {
    "severity": {"type": "string", "enum": ["P1", "P2", "P3"]},
    "reference": {"type": "string"}, "impact": {"type": "string"},
    "action": {"type": "string"}, "inScope": {"type": "boolean"}},
    "required": ["severity", "reference", "impact", "action", "inScope"], "additionalProperties": False}
schema = {"type": "object", "properties": {
    "taskId": {"type": "string", "enum": [manifest["taskId"]]},
    "inputDigest": {"type": "string", "enum": [manifest["inputDigest"]]},
    "verdict": {"type": "string", "enum": ["pass", "revise", "blocked"]},
    "findings": {"type": "array", "items": finding, "maxItems": 6},
    "unverified": {"type": "array", "items": {"type": "string"}}},
    "required": ["taskId", "inputDigest", "verdict", "findings", "unverified"], "additionalProperties": False}
report = {"taskId": manifest["taskId"], "inputDigest": manifest["inputDigest"],
          "requestedModel": options.model, "requestedEffort": "medium", "cliVersion": version,
          "profile": "scoped supplied planning text only", "accessRestrictionVerified": False,
          "requestSent": False, "toolAttempts": [], "protocolSucceeded": False, "failure": None}
started = time.monotonic()
with tempfile.TemporaryDirectory(prefix="tft-agy-review-") as folder:
    schema_path = pathlib.Path(folder) / "schema.json"
    schema_path.write_text(json.dumps(schema), encoding="utf-8")
    proc = subprocess.Popen([str(binary),
        "--input-format", "stream-json", "--output-format", "stream-json", "--mode", "plan",
        "--model", report["requestedModel"], "--effort", "medium", "--disable-slash-commands",
        "--json-schema", str(schema_path), "--print-timeout", "180s"], cwd=folder,
        stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
        text=True, encoding="utf-8", errors="replace", creationflags=subprocess.CREATE_NO_WINDOW)
    report["ownedPid"] = proc.pid
    events = queue.Queue()
    def read(stream, kind):
        for line in stream:
            events.put((kind, line))
        events.put((kind, None))
    for stream, kind in [(proc.stdout, "out"), (proc.stderr, "err")]:
        threading.Thread(target=read, args=(stream, kind), daemon=True).start()
    result = None
    try:
        while time.monotonic() - started < 195:
            try:
                kind, line = events.get(timeout=.25)
            except queue.Empty:
                if proc.poll() is not None:
                    break
                continue
            if kind != "out" or line is None:
                continue
            try:
                event = json.loads(line)
            except ValueError:
                continue
            if event.get("event") == "init":
                report["initModel"] = event.get("init", {}).get("model")
                if report["initModel"] != report["requestedModel"]:
                    raise ValueError("ModelMismatch")
                proc.stdin.write(json.dumps({"event": "user", "message": {"content": prompt}}) + "\n")
                proc.stdin.flush()
                proc.stdin.close()
                report["requestSent"] = True
            elif event.get("event") == "step_update":
                step = event.get("step_update", {})
                if step.get("step_type") == "tool" and step.get("tool_name") != "finish":
                    report["toolAttempts"].append(step.get("tool_name"))
                    raise ValueError("ToolAttemptDuringTextOnlyReview")
            elif event.get("event") == "result":
                result = event.get("result", {})
                break
        if result is None:
            raise ValueError("NoTerminalResult")
        report["status"] = result.get("status")
        report["usage"] = result.get("usage")
        report["responseSeconds"] = result.get("duration_seconds")
        if result.get("status") != "SUCCESS":
            raise ValueError("AgentDidNotReturnSuccess")
        payload = result.get("structured_output")
        if payload is None:
            payload = json.loads(result.get("response", ""))
        if payload.get("taskId") != manifest["taskId"] or payload.get("inputDigest") != manifest["inputDigest"]:
            raise ValueError("ResponseIdentityMismatch")
        if payload.get("verdict") not in ("pass", "revise", "blocked") or len(payload.get("findings", [])) > 6:
            raise ValueError("ReviewFormatMismatch")
        validate_review(payload, schema)
        report["protocolSucceeded"] = True
    except Exception as error:
        report["failure"] = type(error).__name__ + ":" + str(error)[:80]
    finally:
        if not proc.stdin.closed:
            proc.stdin.close()
        try:
            proc.wait(timeout=4)
        except subprocess.TimeoutExpired:
            subprocess.run(["taskkill.exe", "/PID", str(proc.pid), "/T", "/F"], capture_output=True, timeout=10)
            proc.wait(timeout=3)
            report["failure"] = "OwnedProcessCleanupTimeout"
            report["protocolSucceeded"] = False
        report["exitCode"] = proc.returncode
        if proc.returncode != 0:
            report["protocolSucceeded"] = False
        report["durationSeconds"] = round(time.monotonic() - started, 3)
        if report["protocolSucceeded"]:
            local_path(review_path, task).write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        local_path(runtime_path, task).write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(json.dumps(report, ensure_ascii=True))
raise SystemExit(0 if report["protocolSucceeded"] else 1)
