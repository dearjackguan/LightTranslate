"""Deploy an isolated Windows translation runtime after explicit user authorization."""

import argparse
from concurrent.futures import ThreadPoolExecutor
import hashlib
import json
from pathlib import Path, PurePosixPath
import shutil
import urllib.request
import zipfile


ROOT = Path(__file__).resolve().parents[1]
CACHE = Path(r"A:\Agent\Codex\Translation\downloads")
PACKAGES = {"ctranslate2": "4.6.0", "sentencepiece": "0.2.0", "numpy": "2.2.6",
            "PyYAML": "6.0.2", "setuptools": "80.9.0"}
MIRROR_REVISION = "9dc7a2c8f29ac18e020c93f8208476d1e3fe9a9f"
MODELS = {
    "en_zh": "433e7c4f034d87fbe2353161e05f18646d7999452f801a4e1f0378522b9850ab",
    "zh_en": "62e7af5a3a48b530e47b7b3e5c78c2de79073ecd815750d2bf3ab35b4a67da2d",
}


def sha256(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def get_json(url):
    with urllib.request.urlopen(url, timeout=30) as response:
        return json.load(response)


def resolve_assets():
    assets = [{"kind": "python", "name": "python-3.12.10-embed-amd64.zip",
               "url": "https://www.python.org/ftp/python/3.12.10/python-3.12.10-embed-amd64.zip"}]
    for name, version in PACKAGES.items():
        release = get_json("https://pypi.org/pypi/" + name + "/" + version + "/json")
        wheels = [item for item in release["urls"] if item["filename"].endswith(".whl")
                  and ("cp312-cp312-win_amd64" in item["filename"]
                       or "py3-none-any" in item["filename"])]
        if len(wheels) != 1:
            raise ValueError("Expected one Windows/Python 3.12 wheel for " + name)
        item = wheels[0]
        assets.append({"kind": "wheel", "package": name, "version": version,
                       "name": item["filename"], "url": item["url"],
                       "sha256": item["digests"]["sha256"]})
    for direction, digest in MODELS.items():
        name = "translate-" + direction + "-1_9.argosmodel"
        assets.append({"kind": "model", "direction": direction, "version": "1.9",
                       "name": name, "sha256": digest, "mirror": True,
                       "official_index": "https://raw.githubusercontent.com/argosopentech/argospm-index/main/index.json",
                       "url": "https://huggingface.co/TiberiuCristianLeon/Argostranslate/resolve/"
                       + MIRROR_REVISION + "/" + name})
    return assets


def download(asset):
    path = CACHE / asset["name"]
    digest = asset.get("sha256")
    if path.is_file() and (not digest or sha256(path) == digest):
        asset["sha256"] = sha256(path)
        return path
    partial = path.with_name(path.name + ".part")
    with urllib.request.urlopen(asset["url"], timeout=60) as response, partial.open("wb") as output:
        shutil.copyfileobj(response, output, 1024 * 1024)
    actual = sha256(partial)
    if digest and actual != digest:
        raise ValueError("Download checksum mismatch: " + asset["name"])
    partial.replace(path)
    asset["sha256"] = actual
    print("VERIFIED " + asset["name"], flush=True)
    return path


def extract(archive, destination):
    with zipfile.ZipFile(archive) as package:
        for item in package.infolist():
            relative = PurePosixPath(item.filename.replace("\\", "/"))
            if relative.is_absolute() or ".." in relative.parts or ":" in item.filename:
                raise ValueError("Unsafe archive member")
            target = destination.joinpath(*relative.parts).resolve()
            if not target.is_relative_to(destination.resolve()):
                raise ValueError("Archive member escapes destination")
            if item.is_dir():
                target.mkdir(parents=True, exist_ok=True)
                continue
            target.parent.mkdir(parents=True, exist_ok=True)
            with package.open(item) as source, target.open("wb") as output:
                shutil.copyfileobj(source, output)


def preserve_trained_model(destination, direction):
    metadata_path = destination / "metadata.json"
    if not metadata_path.is_file():
        return False
    metadata = json.loads(metadata_path.read_text(encoding="utf-8"))
    if not metadata.get("training"):
        return False
    if metadata.get("from_code") + "_" + metadata.get("to_code") != direction:
        raise ValueError("Installed trained model language pair mismatch")
    hashes = metadata.get("deployed_asset_hashes", {})
    if not {"model/model.bin", "model/config.json", "model/shared_vocabulary.json", "source.spm", "target.spm"}.issubset(hashes):
        raise ValueError("Installed trained model has no complete integrity manifest")
    for relative, digest in hashes.items():
        path = (destination / relative).resolve()
        path.relative_to(destination.resolve())
        if not path.is_file() or sha256(path) != digest:
            raise ValueError("Installed trained model failed integrity verification; restore the portable package")
    return True


def deploy_model(archive, destination, direction):
    if preserve_trained_model(destination, direction):
        print("PRESERVED verified trained model " + direction, flush=True)
        return
    unpacked = CACHE / (direction + "-unpacked")
    unpacked.mkdir(parents=True, exist_ok=True)
    extract(archive, unpacked)
    metadata_files = list(unpacked.rglob("metadata.json"))
    if len(metadata_files) != 1:
        raise ValueError("Model package metadata is ambiguous")
    source = metadata_files[0].parent
    metadata = json.loads(metadata_files[0].read_text(encoding="utf-8"))
    if metadata.get("from_code") + "_" + metadata.get("to_code") != direction:
        raise ValueError("Model language pair mismatch")
    if metadata.get("package_version") != "1.9":
        raise ValueError("Model package version mismatch")
    for relative in ["model/model.bin", "sentencepiece.model"]:
        if not (source / relative).is_file():
            raise ValueError("Model asset is absent: " + relative)
    shutil.copytree(source, destination, dirs_exist_ok=True)
    print("DEPLOYED model " + direction, flush=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", default=r"A:\Agent\Codex\Translation\app\engine")
    args = parser.parse_args()
    engine = Path(args.output).resolve()
    if engine.drive.upper() != "A:":
        raise ValueError("The isolated runtime must be deployed under A:")
    CACHE.mkdir(parents=True, exist_ok=True)
    assets = resolve_assets()
    with ThreadPoolExecutor(max_workers=3) as pool:
        archives = list(pool.map(download, assets))
    python = engine / "python"
    for asset, archive in zip(assets, archives):
        if asset["kind"] == "python":
            extract(archive, python)
        elif asset["kind"] == "wheel":
            extract(archive, python / "Lib" / "site-packages")
        else:
            deploy_model(archive, engine / "models" / asset["direction"], asset["direction"])
    (python / "python312._pth").write_text(
        "python312.zip\n.\nLib/site-packages\nimport site\n", encoding="utf-8")
    shutil.copyfile(ROOT / "scripts" / "translation-worker.py", engine / "translation-worker.py")
    manifest = {"runtime": "CPython 3.12.10 Windows x64 embeddable", "packages": PACKAGES,
                "modelMirrorRevision": MIRROR_REVISION, "assets": assets,
                "deployedModels": {direction: json.loads((engine / "models" / direction / "metadata.json").read_text(encoding="utf-8"))
                                   for direction in ("en_zh", "zh_en")}}
    (engine / "deployment-manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    (ROOT / "research" / "offline-assets-lock.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print("PASS isolated offline engine deployed", flush=True)


if __name__ == "__main__":
    main()
