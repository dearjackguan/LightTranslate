"""Verify automatic direction, mixed prose, technical literals and unsupported text."""

import importlib.util
import json
from pathlib import Path
import subprocess


ROOT = Path(__file__).resolve().parents[1]
ENGINE = Path(r"A:\Agent\Codex\Translation\app\engine")
REPORT = Path(r"A:\Agent\Codex\Translation\accuracy\automatic-direction-results-2026-10-05.json")
spec = importlib.util.spec_from_file_location("worker", ROOT / "scripts" / "translation-worker.py")
worker = importlib.util.module_from_spec(spec)
spec.loader.exec_module(worker)


def main():
    detection = [
        ("Hello world", "en"), ("请打开翻译窗口。", "zh"),
        ("请检查 API 接口，然后重启电脑。", "zh"),
        ("Windows 是什么？", "zh"), ("Open 设置 and click Save.", "en"),
        ("Please translate 你好世界 into English.", "en"),
        ("请发邮件至 developer-support@example.com。", "zh"),
        ("执行 `python -V` 后打开 https://example.com/docs。", "zh"),
        ("Ｈｅｌｌｏ ｗｏｒｌｄ", "en"), ("123.45", None),
        ("support@example.com", None), ("`python -V`", None),
    ]
    for text, expected in detection:
        assert worker.detect_source(text) == expected, "Unexpected direction: " + text
    try:
        worker.detect_source("Привет")
    except worker.WorkerError as error:
        assert str(error) == "LANGUAGE_UNSUPPORTED"
    else:
        raise AssertionError("Unsupported-only prose was treated as English.")
    results = []
    for text, expected in (detection[0], detection[1], detection[2], detection[7], detection[9], detection[10], detection[11]):
        child = subprocess.run([str(ENGINE / "python" / "python.exe"), "-I", "-B", str(ENGINE / "translation-worker.py")],
            input=json.dumps(dict(text=text, source="auto", target="auto")).encode("ascii"),
            stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=True, timeout=30)
        response = json.loads(child.stdout.decode("utf-8"))
        assert response["ok"] and response["source"] == (expected or "none"), response
        assert response["target"] == ("none" if expected is None else "en" if expected == "zh" else "zh")
        for start, end in worker.protected_spans(text):
            assert text[start:end] in response["text"], "Auto input identifier changed."
        if expected is None:
            assert response["text"] == text
        results.append(dict(input=text, response=response, passed=True))
    REPORT.write_text(json.dumps(dict(detectionChecks=13, actualWorkerChecks=results,
        scope="Authored English/Chinese/mixed fixtures; deterministic heuristic, not a universal language classifier."),
        ensure_ascii=False, indent=2), encoding="utf-8")
    print("PASS detection 13/13 and actual automatic worker requests 7/7")


if __name__ == "__main__":
    main()
