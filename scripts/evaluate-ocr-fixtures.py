"""Compare frozen authored fixtures using a local OCR process and explicit references."""

import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import time


def normalize(text):
    return re.sub(r"\s+", "", text)


def distance(reference, actual):
    previous = list(range(len(actual) + 1))
    for i, expected in enumerate(reference, 1):
        current = [i]
        for j, observed in enumerate(actual, 1):
            current.append(min(previous[j] + 1, current[-1] + 1,
                               previous[j - 1] + (expected != observed)))
        previous = current
    return previous[-1]


def recognize(args, image):
    if args.engine == "windows":
        command = [str(args.executable), "-NoLogo", "-NoProfile", "-NonInteractive",
                   "-ExecutionPolicy", "Bypass", "-File", str(args.worker), "-ImagePath", str(image)]
    else:
        command = [str(args.executable), "--ensureAscii=1", "--numThread=2",
                   "--maxSideLen=2048", "--models=" + str(args.executable.parent / "models"),
                   "--det=ch_PP-OCRv4_det_infer.onnx", "--rec=rec_ch_PP-OCRv4_infer.onnx",
                   "--keys=dict_chinese.txt", "--image_path=" + str(image)]
    started = time.perf_counter()
    child = subprocess.run(command, cwd=args.executable.parent, stdout=subprocess.PIPE,
                           stderr=subprocess.PIPE, timeout=30, check=True,
                           creationflags=subprocess.CREATE_NO_WINDOW)
    lines = child.stdout.decode("utf-8-sig").splitlines()
    payloads = [json.loads(line) for line in lines if line.lstrip().startswith("{")]
    if len(payloads) != 1:
        raise ValueError("OCR did not emit exactly one JSON response")
    response = payloads[0]
    if args.engine == "windows":
        if not response.get("ok"):
            raise ValueError("Windows OCR error: " + str(response.get("error")))
        text = response["text"]
    else:
        if response.get("code") not in (100, 101):
            raise ValueError("Rapid OCR error: " + str(response.get("code")))
        text = "\n".join(item["text"] for item in response["data"]) if response["code"] == 100 else ""
    return text, time.perf_counter() - started, response


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--engine", choices=("windows", "rapid"), required=True)
    parser.add_argument("--executable", type=Path, required=True)
    parser.add_argument("--worker", type=Path)
    parser.add_argument("--fixtures", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        parser.error("Preserve prior evaluation; choose a new report filename")
    manifest_path = args.fixtures / "manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    rows = []
    for fixture in manifest["fixtures"]:
        image = args.fixtures / fixture["path"]
        assert hashlib.sha256(image.read_bytes()).hexdigest() == fixture["sha256"]
        text, elapsed, response = recognize(args, image)
        expected, observed = normalize(fixture["expected"]), normalize(text)
        rows.append(dict(id=fixture["id"], language=fixture["language"], pixels=fixture["pixels"],
                         theme=fixture["theme"], expected=fixture["expected"], actual=text,
                         errors=distance(expected, observed), characters=len(expected),
                         exact=expected == observed,
                         missingCritical=[marker for marker in fixture["critical"] if marker not in observed],
                         elapsedSeconds=round(elapsed, 4), response=response))
        if len(rows) % 12 == 0:
            print(f"Evaluated {len(rows)}/{len(manifest['fixtures'])} {args.engine} fixtures", flush=True)
    report = dict(engine=args.engine, manifestSha256=hashlib.sha256(manifest_path.read_bytes()).hexdigest(),
                  executableSha256=hashlib.sha256(args.executable.read_bytes()).hexdigest(), cases=rows,
                  errors=sum(row["errors"] for row in rows), characters=sum(row["characters"] for row in rows),
                  exact=sum(row["exact"] for row in rows),
                  criticalFailures=sum(bool(row["missingCritical"]) for row in rows),
                  scope=manifest["scope"])
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Completed {args.engine}: errors={report['errors']}; exact={report['exact']}/{len(rows)}; "
          f"critical failures={report['criticalFailures']}")


if __name__ == "__main__":
    main()
