# GitHub translation software landscape

Research snapshot: 2026-10-04 (America/Los_Angeles). Sources are the linked project repositories, README files, package manifests, license files, and GitHub repository metadata. This is a source review; no repository was cloned, built, or run. A recent push or an unarchived repository is not proof of active support.

## Product references

| Project | Platform and implementation | Distinct workflow to study | License and status |
| --- | --- | --- | --- |
| [Pot](https://github.com/pot-app/pot-desktop) | Windows/macOS/Linux; Tauri 1, React, Rust | Selection, input, clipboard, screenshot OCR and translation; parallel providers; plugins; local HTTP calls | GPL-3.0; owner archived the repository on 2026-09-07 |
| [Easydict](https://github.com/tisfeng/Easydict) | macOS; Swift/SwiftUI | Selection popup, input translation, local OCR, dictionaries, multiple providers | GPL-3.0; not archived |
| [CopyTranslator](https://github.com/CopyTranslator/CopyTranslator) | Desktop; Electron/Vue/TypeScript | Clipboard translation and PDF line-break cleanup; OpenAI-compatible model providers | GPL-2.0; not archived |
| [TTime](https://github.com/InkTimeRecord/TTime) | Desktop; Electron/Vue/TypeScript | Selection and screenshot OCR workflows; configurable translation/OCR providers | Custom license with Apache-2.0 text plus commercial-use and competing-service restrictions; not archived, last push 2024-12-15 |
| [Crow Translate](https://github.com/crow-translate/crow-translate) | Windows/Linux; Qt/C++ | Lightweight global-shortcut translation and text-to-speech | GPL-3.0; archived on 2024-07-04 |
| [Click'n'Translate](https://github.com/jabrailkhalil/clickntranslate) | Windows/macOS/Linux; Python | Screen OCR, selection translation, dynamic region overlays, optional offline models and document workspace | GPL-3.0; not archived; small project |
| [Translumo](https://github.com/ramjke/Translumo) | Windows; C#/.NET | Low-latency repeated screen capture, OCR selection, game subtitle overlays | Apache-2.0; not archived |
| [LunaTranslator](https://github.com/HIllya51/LunaTranslator) | Primarily Windows; mixed native/Python code | Game text hooks, OCR, multiple engines, glossary and language-learning integrations | GPL-3.0; not archived; specialized game scope |
| [Saladict](https://github.com/crimx/ext-saladict) | Chrome/Firefox WebExtension; TypeScript/React | Browser selection popup, PDF word lookup, dictionary composition | MIT for source; name and brand assets excluded; not a desktop application |
| [SnapTranslate](https://github.com/light-misty/SanpTranslate) | Windows/macOS/Linux; Tauri 2/Rust | Compact screenshot -> local OCR -> AI translation flow | MIT; not archived; early-stage, 6 stars at snapshot |

## Translation engines and services

| Project | Role | License and integration note |
| --- | --- | --- |
| [Argos Translate](https://github.com/argosopentech/argos-translate) | Offline Python translation library and installable language models | README states MIT or CC0; useful when an offline engine is required, but model size, quality, and packaging need measurement |
| [LibreTranslate](https://github.com/LibreTranslate/LibreTranslate) | Self-hosted translation HTTP API built on Argos Translate | AGPL-3.0; useful as a separate service reference; evaluate network/service obligations before reuse |

## Pot-specific findings

- Pot's source tree separates `src` from `src-tauri`, and its package manifest identifies Tauri 1, React 18 and Vite 5. Its README confirms Windows, macOS, Linux, Wayland, OCR, plugins and an HTTP integration endpoint. [Repository](https://github.com/pot-app/pot-desktop) · [package.json](https://github.com/pot-app/pot-desktop/blob/master/package.json)
- The original repository is read-only after the owner's 2026-09-07 archive. Treat it as a product and architecture reference; check any fork's maintenance and code history separately. [Repository](https://github.com/pot-app/pot-desktop)
- Pot code is GPL-3.0. Copying or adapting implementation for distribution requires license compatibility and source obligations. Referencing workflows and independently implementing them leaves more flexibility. [License](https://github.com/pot-app/pot-desktop/blob/master/LICENSE)
- TTime's `LICENSE.txt` adds restrictions beyond Apache-2.0. Do not classify its source as ordinary Apache-2.0 or plan code reuse without resolving those terms. [License](https://github.com/InkTimeRecord/TTime/blob/main/LICENSE.txt)

## Suggested study order for this project

1. Pot: enumerate the core user journeys and split platform functions from translation providers.
2. CopyTranslator and Easydict: study clipboard/PDF cleanup and compact selection interaction.
3. Translumo: study Windows screen capture, OCR latency and overlays if real-time translation is in scope.
4. Argos Translate and LibreTranslate: compare offline in-process translation with a local service only after target languages and quality requirements are known.
5. Saladict: study browser-specific interactions if browser extension support becomes a requirement.

Potential first product slice, subject to requirements: Windows global shortcut -> selected text or manual input -> one configurable provider -> compact result window, followed by screenshot OCR. Separate capture/selection, OCR, provider adapters and UI so platform APIs and service credentials remain outside view components.

## Verification boundary

Repository descriptions, metadata, manifests and licenses were checked on 2026-10-04. Runtime behavior, buildability, quality, latency, API availability and current terms of third-party translation providers were not tested. No external package was installed.
