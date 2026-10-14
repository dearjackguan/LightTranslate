"""Deploy the authorized, pinned standalone OCR subset without a system install."""

import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import urllib.request


ARTIFACTS = Path(r"A:\Agent\Codex\Translation")
CONFIG = Path(__file__).with_name("offline-ocr-assets.json")


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def download(url, path, expected):
    if path.is_file() and digest(path) == expected:
        return path
    path.parent.mkdir(parents=True, exist_ok=True)
    partial = path.with_suffix(path.suffix + ".part")
    with urllib.request.urlopen(url, timeout=60) as response, partial.open("wb") as output:
        shutil.copyfileobj(response, output, 1024 * 1024)
    if digest(partial) != expected:
        raise ValueError("Asset checksum mismatch: " + path.name)
    partial.replace(path)
    return path


def safe_path(root, name):
    relative = PurePosixPath(name.replace("\\", "/"))
    if relative.is_absolute() or ".." in relative.parts or ":" in name:
        raise ValueError("Unsafe archive path")
    path = root.joinpath(*relative.parts).resolve()
    path.relative_to(root.resolve())
    return path


def extract_subset(archive, configuration, unpacked):
    tar = Path(os.environ["WINDIR"]) / "System32" / "tar.exe"
    listing = subprocess.check_output([str(tar), "-tf", str(archive)], text=True, encoding="utf-8")
    for name in listing.splitlines():
        safe_path(unpacked, name)
    members = [configuration["root"] + "/" + name for name in configuration["files"]]
    unpacked.mkdir(parents=True, exist_ok=True)
    subprocess.run([str(tar), "-xf", str(archive), "-C", str(unpacked), *members], check=True)
    return unpacked / configuration["root"]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", default=str(ARTIFACTS / "app" / "ocr"))
    args = parser.parse_args()
    destination = Path(args.output).resolve()
    if not any(destination.is_relative_to(root.resolve()) for root in (Path("A:/Projects"), Path("A:/Agent/Codex"))):
        raise ValueError("OCR output must stay in the designated A-drive project/artifact directories")
    configuration = json.loads(CONFIG.read_text(encoding="utf-8"))
    archive = download(configuration["url"], ARTIFACTS / "downloads" / configuration["archive"],
                       configuration["archiveSha256"])
    source = extract_subset(archive, configuration, ARTIFACTS / "downloads" / "rapid-ocr-subset")
    destination.mkdir(parents=True, exist_ok=True)
    for name in configuration["files"]:
        target = safe_path(destination, name)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(safe_path(source, name), target)
    for notice in configuration["licenses"]:
        cached = download(notice["url"], ARTIFACTS / "ocr-evaluation" / "licenses" / notice["name"], notice["sha256"])
        target = destination / "licenses" / notice["name"]
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(cached, target)
    attribution = destination / "licenses" / "Clipper-attribution.txt"
    attribution.write_text("Clipper 6.4.2. Copyright Angus Johnson 2010-2017.\n"
        "Boost Software License 1.0; see Clipper-Boost-1.0.txt.\n"
        "Source: https://github.com/hiroi-sora/RapidOCR-json/blob/v0.2.0/cpp/src/clipper.cpp\n", encoding="utf-8")
    files = configuration["files"] + ["licenses/" + item["name"] for item in configuration["licenses"]]
    files.append("licenses/Clipper-attribution.txt")
    provenance = dict(configuration, binaryBanner="RapidOCR-json v1.1.0",
        bannerNote="Official v0.2.0 asset reports v1.1.0; original binary is unchanged.",
        linkedRuntimeVersions={"ONNXRuntime": "1.14.0", "OpenCV": "4.6.0"},
        assets={name: dict(sha256=digest(destination / name), bytes=(destination / name).stat().st_size) for name in files})
    (destination / "provenance.json").write_text(json.dumps(provenance, indent=2) + "\n", encoding="utf-8")
    print("DEPLOYED verified offline OCR subset: " + str(sum((destination / name).stat().st_size for name in files)) + " bytes")


if __name__ == "__main__":
    main()
