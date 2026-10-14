# Minimal translation software: source analysis and implementation plan

Scope confirmed by the user: a lightweight desktop application with screenshot translation and input translation only. Implementation is pending confirmation under the user-supplied AGENTS.md section 14.

## Source snapshots

| Reference | Commit | Role |
| --- | --- | --- |
| https://github.com/pot-app/pot-desktop | 594d32ede96acd106b0256deaa8bb440ffcdff40 | Main workflow reference; GPL-3.0 |
| https://github.com/light-misty/SanpTranslate | 38bac40180f557e5579ce585f50f84c649790de0 | Comparison of screenshot and input service separation; MIT |

The unmodified shallow clones are in `A:\Projects\Translation\references`. No dependency installation or build was performed in the reference repositories.

## Pot workflow mapping

- Screenshot entry: `references/pot-desktop/src-tauri/src/window.rs:368` opens a screenshot window and forwards successful capture to image translation.
- Full-screen capture: `references/pot-desktop/src-tauri/src/screenshot.rs:4` captures the selected display and saves PNG pixels.
- Region selection: `references/pot-desktop/src/window/Screenshot/index.jsx:81` converts logical coordinates to image pixels before cropping at line 92.
- Windows OCR: `references/pot-desktop/src-tauri/src/system_ocr.rs:34` chooses the system/user language recognizer; line 46 calls `RecognizeAsync` on a decoded bitmap.
- Input entry: `references/pot-desktop/src-tauri/src/window.rs:241` opens the translation window with an input marker.
- Text provider: `references/pot-desktop/src/services/translate/google/index.jsx:3` sends text to the translation endpoint and joins returned translation segments.

SnapTranslate also separates capture, OCR and translation, but its Windows OCR path bundles or discovers Tesseract and language data. Its Tauri 2, web UI, database and updater are additional components for the current two-feature scope.

## Verified environment probes

- Installed .NET Framework C# compiler: `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`, file version 4.8.9221.0.
- Rust/cargo and a .NET SDK were not found in the checked command paths and standard locations. No toolchain was installed.
- Windows OCR exposes the installed `zh-Hans-CN` recognizer and a maximum image dimension of 10000 pixels.
- The system OCR recognized a synthetic image as `HELLO WORLD 123` in 116 ms for the WinRT decode/recognize portion. This does not include GUI capture, process startup or network translation.
- The Google text endpoint used by Pot returned `Hello world` -> `你好世界` from this machine. This is an unofficial online endpoint; availability and stability are not guaranteed by the probe.

## Proposed implementation

1. Windows-native C# executable using the already-installed .NET Framework, WinForms and system libraries; no new external packages.
2. Screenshot: global shortcut or button -> physical-pixel region selection -> local Windows OCR -> the shared translation service.
3. Input: text editor -> the same translation service -> translated text and copy action; Chinese/English targets.
4. Separate UI, workflow controller, capture, OCR and translation responsibilities. Use bounded asynchronous operations, explicit cancellation and visible error states.
5. Verify actual input and screenshot flows, cancellation, scaling/cropping, network errors and output size/memory; publish a runnable artifact under `A:\Agent\Codex\Translation`.

The source analysis identifies reusable behavior and platform APIs. It does not establish that a finished application has been built or that multi-monitor/DPI behavior works. Those are implementation acceptance checks.

## Superseding offline decision

Implementation was authorized. The user subsequently required fully offline translation. The initial Google probe was followed by HTTP 429 failures; the online provider has now been removed. See `offline-implementation-status.md` for current decisions, passed local checks and remaining inference/deployment gates.
