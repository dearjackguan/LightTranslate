"""Verify a completed portable archive with isolated inference after relocation."""

import hashlib
import json
from pathlib import Path
import subprocess
import re
import sys
import zipfile


ARTIFACTS = Path(r"A:\Agent\Codex\Translation")


def main():
    archive = ARTIFACTS / "release" / "LightTranslate-1.2.1-win-x64.zip"
    destination = ARTIFACTS / "portable 1.2.1 中文 check with spaces"
    destination.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(archive) as package:
        assert package.testzip() is None, "Archive CRC failed."
        for name in package.namelist():
            (destination / name).resolve().relative_to(destination.resolve())
        package.extractall(destination)
    application = destination / "LightTranslate"
    engine = application / "engine"
    executable = engine / "python" / "python.exe"
    prefix = subprocess.check_output([str(executable), "-I", "-B", "-c",
        "import sys;print(sys.prefix)"], cwd=application, text=True).strip()
    assert Path(prefix).resolve() == executable.parent.resolve(), "External Python runtime used."
    for name in ("LightTranslate.exe", "LightTranslate.exe.config", "LICENSE", "THIRD_PARTY_NOTICES.md"):
        assert (application / name).is_file(), "Missing distribution file: " + name
    assert (application / "LightTranslate.exe").read_bytes() == (ARTIFACTS / "app" / "LightTranslate.exe").read_bytes()
    assert (engine / "translation-worker.py").read_bytes() == (Path(__file__).resolve().parents[1] / "scripts" / "translation-worker.py").read_bytes()
    provenance = json.loads((application / "ocr" / "provenance.json").read_text(encoding="utf-8"))
    for relative, asset in provenance["assets"].items():
        path = (application / "ocr" / relative).resolve()
        path.relative_to((application / "ocr").resolve())
        assert hashlib.sha256(path.read_bytes()).hexdigest() == asset["sha256"], "Relocated OCR integrity failed."
    cases = []
    for text, source, target, expected in (("Hello world 123", "en", "zh", "世界"),
                                         ("请打开翻译窗口。", "zh", "en", "window"),
                                         ("请打开翻译窗口。", "auto", "auto", "window"),
                                         ("support@example.com", "auto", "auto", "support@example.com"),
                                         ("每月订阅费用为19.99美元，不含税。", "auto", "auto", "tax"),
                                         ("更新安装期间，请勿关闭计算机。", "auto", "auto", "turn off"),
                                         ("本次报价为250元，不包含税费。", "auto", "auto", "250 yuan"),
                                         ("Please turn off the computer after saving your work.", "auto", "auto", "保存")):
        child = subprocess.run([str(executable), "-I", "-B", str(engine / "translation-worker.py")],
            cwd=application, input=json.dumps(dict(text=text, source=source, target=target)).encode("ascii"),
            stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=30, check=True)
        response = json.loads(child.stdout.decode("utf-8"))
        assert response["ok"] and expected in response["text"], "Portable inference failed."
        if "不含税" in text:
            assert "19.99" in response["text"] and re.search(r"without tax|exclud|not (include|contain)", response["text"].lower())
            assert "free" not in response["text"].lower()
        if "关闭计算机" in text:
            assert re.search(r"(not|don't).*?(turn off|shut down)", response["text"].lower())
        if "250元" in text:
            assert "$250" not in response["text"]
            assert re.search(r"without tax|exclud|not (include|contain)", response["text"].lower())
        if "saving your work" in text:
            assert "后" in response["text"]
            assert not any(word in response["text"] for word in ("节省", "节约"))
        cases.append(dict(source=source, target=target, passed=True, output=response["text"]))
    subprocess.run([str(ARTIFACTS / "verification" / "LightTranslate.Verification.exe"),
                    "portable-ocr", str(application)], check=True, timeout=60)
    report = dict(archive=str(archive), relocatedDirectory=str(application), pythonPrefix=prefix,
                  crcPassed=True, inference=cases,
                  ocrAdapterPassed=True, ocrAssetHashesPassed=True,
                  scope="Relocated bundled OCR adapter, automatic translation and model inference; not a physical keyboard gate.")
    (ARTIFACTS / "release" / "portable-results.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print("PASS finalized ZIP CRC, verified executable/worker and relocated manual/automatic inference")


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
