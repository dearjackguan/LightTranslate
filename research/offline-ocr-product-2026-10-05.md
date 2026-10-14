# Offline OCR product release 1.1.0

## Authorization and product scope

The user approved the five-step product plan and new OCR dependency request by replying "做". The application remains a Windows x64 typed/screenshot translator, with automatic Chinese/English direction and local inference. No fine-tuning candidate is promoted: both original Argos 1.9 translation models remain unchanged. The 1.0.4 portable archive and its executable baseline are retained for rollback.

## Selected component and provenance

The official RapidOCR-json v0.2.0 release archive has 73,461,693 bytes and SHA-256 `7ad9b283d03436c6cd0296723188699299cb4e5cf9140b410c59543aa5793c40`, matching the published upstream value. Its executable banner reports v1.1.0; the tag and banner are recorded separately rather than assumed identical. The unchanged executable embeds ONNX Runtime 1.14.0 and OpenCV 4.6.0 build information.

Only the executable, Chinese/English-capable PP-OCRv4 detector/recognizer, required classifier model and Chinese dictionary are packaged: 33,065,176 bytes. License/attribution files bring this to 33,443,014 bytes before provenance.json. Other language/model sets are excluded. `scripts/install-offline-ocr.py` uses the standard library and Windows tar, verifies the archive hash, validates archive paths and extracts the selected members. Pinned license URLs/hashes are in `scripts/offline-ocr-assets.json`; all shipped OCR assets have file hashes in `ocr/provenance.json`.

References: [official release](https://github.com/hiroi-sora/RapidOCR-json/releases/tag/v0.2.0), [protocol/source](https://github.com/hiroi-sora/RapidOCR-json), [Pot Paddle plugin](https://github.com/pot-app/pot-app-recognize-plugin-paddle). Pot delegates neural OCR to a separate JSON executable. No Pot source is copied, and no claim is made about WeChat's private production implementation.

## Diagnosed causes and implementation

The initial native v4 candidate matched 70 of 72 frozen fixtures after whitespace removal, with 26 character edits and one critical-marker failure. Its orientation classifier incorrectly rotated a normal dark English line by 180 degrees. A dark Chinese price fixture yielded overlapping detection boxes and an extra digit. Separate interventions reproduced both causes: disabling orientation corrected the English line; RGB inversion corrected the Chinese detector output. The integration applies upright-screen orientation and dark-border normalization, not guessed spelling/word substitutions or fixture lookups.

The image preparation samples 64 border pixels and inverts RGB only when at least 48 have luminance below 96. Images stay at their original size. Detection maximum side is bounded at 4096, with service input limits of 8192 per dimension and 16,000,000 pixels. Heterogeneous backgrounds, photos and rotations beyond the upright-screen scope remain unbenchmarked.

`OcrProcess` starts a hidden child, waits for `OCR init completed.`, sends an ASCII-escaped JSON image path and reads one UTF-8 JSON response. Relative model paths avoid an upstream Unicode CLI-path issue. The upstream worker loops indefinitely and handles EOF poorly, so the adapter terminates its own child after the response, cancellation or actual 20-second timeout, waits for exit and drains stderr before returning. This is not a permanent server. `OcrService` deletes the unique temporary PNG in all exits.

`OcrResponse` validates codes, text, finite scores and nondegenerate four-corner rectangles. Each detected line is supplied as an honest line rectangle to existing paragraph reconstruction; no synthetic word positions are invented. A score below 0.90 on any line prompts source review instead of starting automatic translation. Full source and previous output remain visible, and the user can correct the source then press Translate. The threshold is a heuristic on model scores, not a correctness probability; high-score errors remain possible.

## Actual evaluation

Frozen fixture manifest SHA-256: `4e0a5db8061930be39655c57e39e87849ec40a765de7e3464cb013ddfe8901bb`. The 72 authored single-line ClearType cases cover Chinese, English and mixed text, 12/14/16 physical pixels, light/dark backgrounds, negations, prices and versions. They were frozen before candidate inference, then used to diagnose the two candidate errors. Accordingly, this is a fixed local comparison, not an untouched final holdout or a general accuracy estimate.

| Engine | Whitespace-excluded character edits / 1686 | Exact cases | Missing critical markers |
| --- | --- | --- | --- |
| Windows OCR 1.0.4 | 187 | 13/72 | 32/72 |
| Native Rapid v4, initial settings | 26 | 70/72 | 1/72 |
| Integrated 1.1.0 service | 0 | 72/72 | 0/72 |

The independent historical eight fixtures at 10/12/14/16 pixels improved from 69 edits with Windows 1.0.3 to zero with the new service, across 200 characters. These sentences were used in earlier diagnosis and are not a new independent accuracy benchmark.

Evidence in `A:\Agent\Codex\Translation`:

- `ocr-evaluation/windows-1.0.4.json`, `rapid-v4-native.json`, `integrated-ocr-results.json`: complete per-case text, markers and differences.
- `verification/small-text-results.json`: eight historical actual-engine comparisons.
- `verification/ocr-layout-results.json`: 19 paragraph/geometry checks, including three actual OCR fixtures and actual wrapped-condition translation.
- `verification/service-results.json`: 15 actual service checks including both directions, automatic input, literals and scoped semantic decoding.
- `audit/audit-probes.json`: 11 controller/process/DPI/keyboard-message/oversize checks.
- `ocr-lifecycle`: actual 20-second deadline, cancellation before ready and after request read, abnormal exit, Unicode/emoji transport, missing component, actual worker cancellation/recovery and temporary-file cleanup. Post-ready cancellation waits for the probe's request-read marker.
- `ocr-quality`: malformed replies, low-score text preservation and controller review workflow, in addition to 72 actual OCR calls.
- `verification/gui-message-results.json`: 17 checks in the actual production window, including actual screenshot pixels, wrapped condition, 12-pixel English, 12-pixel Chinese on light/dark backgrounds, automatic translation, copying, blank feedback, cancellation and closing during real inference. The Chinese screenshot prohibition is asserted in the English translation.

The physical input-injection GUI attempt timed out before selection. Diagnostics observed GameViewer owning the foreground, modifiers registering but injected letter keys not registering, and foreground requests failing. No unrelated process or remote-control configuration was changed. The message-mode GUI test uses explicit window messages, so it verifies actual capture/OCR/translation/UI paths but does not claim physical Ctrl+Alt+S or physical mouse injection passed. The unsuccessful physical report remains in `verification/gui-results.json`; registration/conflict handling passed separately. Mixed-DPI/multimonitor and another clean Windows machine remain unverified.

## Measured resource use

On this machine, the 72 integrated requests had median 0.456 seconds and maximum 0.557 seconds, including short-lived process startup with warm operating-system file caches. These are short fixture timings, not a cold-machine or arbitrary-image guarantee. The actual OCR child sampled peak was 127,655,936 bytes across service/layout runs. The message GUI gate recorded idle UI working set 37,892,096 bytes and UI peak 94,953,472 bytes during capture. Model/OCR processes are short-lived and are not included in those UI memory values.

## Delivery verification

The first Unicode relocation found a genuine preexisting translation defect: actual requests returned `ENGINE_FAILURE`, and direct tracing located SentencePiece 0.2.0's filename loader (`OSError: Not found ... No such file or directory Error #2`) even though Python confirmed the file existed. Direct testing showed CTranslate2's model-directory loader worked at the same Unicode path. The minimal fix loads the existing SentencePiece model bytes through Python and passes `model_proto`; neither model weights nor decoding settings change. Both source/target tokenizer roles remain supported. The archive was rebuilt after this fix and portable verification rerun; the failed initial package report is not final acceptance evidence.

The complete 1.1.0 portable archive includes the application, both translation models, isolated Python runtime, selected OCR assets, provenance and notices. Portable verification checks ZIP CRC and asset hashes, relocates to a directory containing Chinese characters and spaces, runs both manual/automatic translation directions and semantic regression cases, then invokes the actual C# OCR adapter plus automatic translation in that relocated directory. Final package size/hash and relocated results are recorded in `release/package-results.json`, `portable-results.json` and `portable-ocr-results.json`.

All application inference uses local files and process pipes. Downloads occur during authorized installation only. Existing Python offline tests reject Python socket/urllib audit events; they do not intercept arbitrary native socket calls or disconnect the machine. Native OCR has no configured network endpoint or model download path in this integration. No firewall/system-network change was made.
