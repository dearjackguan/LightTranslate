"""Convert the selected trained checkpoint to an isolated CPU INT8 candidate."""

import json
import os
from pathlib import Path

ROOT = Path(r"A:\Agent\Codex\Translation\training")
os.environ["HF_HOME"] = str(ROOT / "cache" / "huggingface")
os.environ["HF_HUB_OFFLINE"] = "1"
os.environ["HF_HUB_DISABLE_TELEMETRY"] = "1"

import torch
from ctranslate2.converters import TransformersConverter


def main():
    report = json.loads((ROOT / "training-results.json").read_text(encoding="utf-8"))
    checkpoint = Path(report["selectedCheckpoint"])
    destination = checkpoint.parent / "candidate-int8"
    TransformersConverter(str(checkpoint)).convert(str(destination), quantization="int8")
    config_path = destination / "config.json"
    config = json.loads(config_path.read_text(encoding="utf-8"))
    assert not config["add_source_eos"] and config["eos_token"] == "</s>"
    config["add_source_eos"] = True
    config_path.write_text(json.dumps(config, indent=2), encoding="utf-8")
    print(json.dumps(dict(checkpoint=str(checkpoint), candidate=str(destination),
                          modelBytes=(destination / "model.bin").stat().st_size)))
    report["int8Candidate"] = str(destination)
    (ROOT / "training-results.json").write_text(json.dumps(report, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
