"""Verify trained-model preservation and tokenizer compatibility contracts."""

import hashlib
import importlib.util
import json
from pathlib import Path
import sys


PROJECT = Path(__file__).resolve().parents[1]
OUTPUT = Path(r"A:\Agent\Codex\Translation\verification\trained-deployment")


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def main():
    installer = load("installer", PROJECT / "scripts" / "install-offline-engine.py")
    worker = load("worker", PROJECT / "scripts" / "translation-worker.py")
    assert len(sys.argv) == 2, "Supply the isolated model package to verify."
    staged = Path(sys.argv[1]).resolve()
    assert installer.preserve_trained_model(staged, "zh_en")
    paths = ("model/model.bin", "model/config.json", "model/shared_vocabulary.json", "source.spm", "target.spm")
    hashes = {}
    for name in paths:
        path = OUTPUT / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(b"verification fixture")
        hashes[name] = hashlib.sha256(path.read_bytes()).hexdigest()
    metadata = dict(from_code="zh", to_code="en", training={"test": True}, deployed_asset_hashes=hashes)
    (OUTPUT / "metadata.json").write_text(json.dumps(metadata), encoding="utf-8")
    assert installer.preserve_trained_model(OUTPUT, "zh_en")
    (OUTPUT / "model" / "model.bin").write_bytes(b"corrupted verification fixture")
    try:
        installer.preserve_trained_model(OUTPUT, "zh_en")
        raise AssertionError("Corrupted trained model was accepted.")
    except ValueError as error:
        assert "integrity" in str(error)
    calls = []
    (OUTPUT / "source.spm").write_bytes(b"source.spm")
    (OUTPUT / "target.spm").write_bytes(b"target.spm")

    class Processor:
        def __init__(self, model_proto):
            self.name = model_proto.decode("utf-8")
            calls.append(self.name)

        def encode(self, text, out_type):
            return [self.name, text]

        def decode(self, tokens):
            return self.name

    tokenizer = worker.ModelTokenizer(OUTPUT, dict(source_tokenizer="source.spm", target_tokenizer="target.spm"), Processor)
    assert tokenizer.encode("text") == ["source.spm", "text"]
    assert tokenizer.decode_source([]) == "source.spm" and tokenizer.decode([]) == "target.spm"
    (OUTPUT / "sentencepiece.model").write_bytes(b"sentencepiece.model")
    legacy = worker.ModelTokenizer(OUTPUT, {}, Processor)
    assert legacy.source is legacy.target and calls.count("sentencepiece.model") == 1
    print("PASS isolated model hashes, preservation/corruption guards and dual/legacy tokenizer roles; no semantic deployment acceptance implied")


if __name__ == "__main__":
    main()
