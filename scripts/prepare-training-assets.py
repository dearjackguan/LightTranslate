"""Fetch a pinned official trainable checkpoint into isolated A-drive storage."""

import hashlib
import json
from pathlib import Path
import shutil
import urllib.request


ROOT = Path(r"A:\Agent\Codex\Translation\training")
REPOSITORY = "Helsinki-NLP/opus-mt-zh-en"
REVISION = "cf109095479db38d6df799875e34039d4938aaa6"
NAMES = {"config.json", "generation_config.json", "pytorch_model.bin", "source.spm",
         "target.spm", "tokenizer_config.json", "vocab.json", "README.md"}


def valid(path, metadata):
    if not path.is_file() or path.stat().st_size != metadata["size"]:
        return False
    if metadata.get("lfs"):
        with path.open("rb") as stream:
            return hashlib.file_digest(stream, "sha256").hexdigest() == metadata["lfs"]["sha256"]
    content = path.read_bytes()
    return hashlib.sha1(b"blob " + str(len(content)).encode() + b"\0" + content).hexdigest() == metadata["blobId"]


def main():
    destination = ROOT / "base-zh-en"
    destination.mkdir(parents=True, exist_ok=True)
    api = "https://huggingface.co/api/models/" + REPOSITORY + "/revision/" + REVISION + "?blobs=true"
    with urllib.request.urlopen(api, timeout=60) as response:
        metadata = json.load(response)
    assert metadata["sha"] == REVISION
    files = [entry for entry in metadata["siblings"] if entry["rfilename"] in NAMES]
    assert {entry["rfilename"] for entry in files} == NAMES
    for entry in files:
        path = destination / entry["rfilename"]
        if valid(path, entry):
            continue
        url = "https://huggingface.co/" + REPOSITORY + "/resolve/" + REVISION + "/" + path.name
        temporary = path.with_suffix(path.suffix + ".part")
        with urllib.request.urlopen(url, timeout=60) as response, temporary.open("wb") as output:
            shutil.copyfileobj(response, output, 1024 * 1024)
        assert valid(temporary, entry), "Checkpoint asset failed size/hash verification: " + path.name
        temporary.replace(path)
        print("Verified", path.name, path.stat().st_size, flush=True)
    manifest = dict(repository=REPOSITORY, revision=REVISION, license="CC-BY-4.0", files=files)
    (ROOT / "checkpoint-manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
