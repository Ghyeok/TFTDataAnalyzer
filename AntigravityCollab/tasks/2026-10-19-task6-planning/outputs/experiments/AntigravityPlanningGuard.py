"""Per-session CLI hook. Only completion is allowed; never record tool arguments."""
import argparse
import json
import pathlib
import sys

parser = argparse.ArgumentParser()
parser.add_argument("--audit", type=pathlib.Path, required=True)
parser.add_argument("--event", choices=["session", "tool", "invocation"], required=True)
args = parser.parse_args()
try:
    incoming = json.load(sys.stdin)
    name = incoming.get("toolCall", {}).get("name", "")
    if args.event == "session":
        result = {}
    elif args.event == "invocation":
        result = {"terminationBehavior": "terminate"} if incoming.get("invocationNum", 0) >= 2 else {}
    else:
        result = {"decision": "allow" if name == "finish" else "deny",
                  "reason": "TFT_PLANNING_GUARD: completion only; inspect supplied text."}
    with args.audit.open("a", encoding="utf-8") as stream:
        stream.write(json.dumps({"event": args.event, "tool": name,
                                 "decision": result.get("decision"),
                                 "invocation": incoming.get("invocationNum")}) + "\n")
except Exception:
    result = {"decision": "deny", "reason": "TFT_PLANNING_GUARD: invalid invocation."}
print(json.dumps(result))
