# Offline implementation status

## Current verified delivery (2026-10-05)

Version 1.0.4 adds bounded adaptive 2x cubic enlargement for small OCR glyphs. Eight actual paired ClearType cases improve from 69 to 31 whitespace-excluded character edits; tiny Chinese errors remain. Scale boundary checks cover native sizes and enlargement budgets. Layout 19, services 15, audit 11 and production GUI 15 pass, including physical 12-pixel screenshot recognition and offline translation. No new dependency or model is deployed. See `small-text-ocr-2026-10-05.md`.

## Previous paragraph delivery (1.0.3)

Version 1.0.3 reconstructs OCR paragraphs using word coordinates, preserves list/heading boundaries and orders clear columns independently. The original Argos weights and offline runtime remain active. The focused layout gate passes 19 checks, including three actual OCR bitmaps and actual offline translation of a wrapped condition. Genuine services pass 15/15 and the focused audit passes 11/11. Physical GUI verification includes an additional wrapped-screenshot condition check. See `ocr-paragraph-reconstruction-2026-10-05.md` for its final production and portable acceptance evidence.

## Previous delivery and training pilot (1.0.2)

The released version is now 1.0.2, with scoped tax-exclusion and power-action decoding. Its preserved ZIP is `A:\Agent\Codex\Translation\release\LightTranslate-1.0.2-win-x64.zip`, SHA-256 `94d5f64102f82156e5f3abc89f4adab519fb741cf76f8742643ae3aff92f8112`. Five authorized local weight-training rounds changed actual parameters, but no candidate passed all acceptance conditions. Both original Argos models remain active. The temporary 1.0.3 integration was rolled back and no 1.0.3 ZIP was published. See `local-weight-finetuning-2026-10-05.md` for exact results and evaluation limitations.

## Previous verified delivery (1.0.1)

Version 1.0.1 adds default automatic English/Chinese direction, protected identifiers, previous-result labeling after edits, actionable length/integrity errors and an actual close-during-model-request gate.

- Real services: 14/14. Focused process/UI audit: 11/11. Production GUI: 13/13, `accepted=true`, both actual-translation flags true, no unavailable checks. Both automatic directions, manual override, physical screenshot translation and parent/child close cleanup pass.
- Direction checks: 13 heuristic cases and 7 actual automatic worker requests. Identifier fidelity: 16 actual bilingual cases and 6 corruption/literal/collision guards. Existing 28 quality fixtures and 16 boundary checks pass.
- Finalized 1.0.1 ZIP CRC passes. The relocated executable and worker match the verified artifacts; four manual/automatic inference requests pass in a folder containing spaces.
- Distribution: `A:\Agent\Codex\Translation\release\LightTranslate-1.0.1-win-x64.zip`, 189,473,641 bytes compressed, 306,026,222 bytes extracted, 1534 files. SHA-256: `ab19f4bb2c045f7770764b39eb980625543cb39e310dad5ff7bc591dbcf24b39`.
- Latest GUI idle: 37,740,544 bytes; pre-close app peak: 93,470,720 bytes. These do not include the inference child's memory.

Remaining limits include model semantic/terminology errors, screenshot soft-wrap reconstruction, unrecognized literal formats, ambiguous mixed language and multimonitor/mixed-DPI coverage. See `integrity-and-auto-direction-2026-10-05.md` for reproduced evidence and exact boundaries.

## Historical verified delivery (2026-10-04)

This section supersedes the historical pre-deployment notes below. The user requested repair of the missing-engine error and subsequently authorized accuracy optimization. The isolated runtime and both pinned models are deployed under `A:\Agent\Codex\Translation\app\engine`.

- Real services pass 13/13; production GUI passes 11/11, including typed translation, screenshot/OCR/translation, real-model cancellation and copying. The GUI report has `accepted=true`, both actual-translation flags true and no unavailable checks.
- Sentence/token boundary regression passes 16/16. A fixed 28-case bilingual comparison and selected semantic/numeric checks pass; this is not a standardized accuracy score.
- Actual CPU inference passes scoped Python network-API denial tests and two 100-line tests. Native sockets are not intercepted; the machine remains connected.
- Final ZIP CRC and real bilingual inference after extraction into an A-drive folder containing spaces pass. Isolated Python prefix matches the extracted bundle.
- Distribution: `A:\Agent\Codex\Translation\release\LightTranslate-1.0.0-win-x64.zip`, 189,470,945 bytes compressed, 306,019,319 bytes extracted, 1534 files. SHA-256: `f83aad39e3920344eae35b98a3a0a0f1d56c2ba40ff28b10f2b42f8bb3a7a599`.
- Latest short cold latency: 0.915/0.991 seconds for en->zh/zh->en. Child peak approximately 145 MiB. GUI idle 57,131,008 bytes and app peak 99,033,088 bytes. These separately measured peaks are not an exact concurrent total.

Remaining limits: small-model terminology/meaning errors; multimonitor/mixed-DPI testing; comprehensive OCR-wrap fidelity and translation-quality evaluation. See `translation-quality-2026-10-04.md`. Active worker cancellation is verified; close-during-genuine-inference has not received a separate end-to-end gate.

## Historical pre-deployment state

The following record describes the earlier missing-runtime audit and its then-pending gates. It is retained for traceability and does not describe the current installation.

## Decisions

- Implementation was authorized by the user.
- The user subsequently required offline translation; the online provider is obsolete and was removed from application source.
- Retain the native C# application and Windows OCR. Add local CTranslate2 CPU inference and SentencePiece with two dedicated models, conditional on package/download approval.
- Run inference on demand in a hidden process. No HTTP translation fallback is permitted.

## Evidence

- Framework compilation passes with warnings treated as errors.
- The current local gate passes 11 checks, including actual English/Chinese OCR, Unicode paths, blank images, cancellation cleanup, validation, crop bounds and local protocol errors.
- Before the offline change, external GUI checks passed capture/hotkey/OCR and cancellation. That report recorded idle working set 42,995,712 bytes at 120 DPI and a 1920x1080 desktop. This is historical UI/OCR evidence, not an offline inference measurement.
- Installed Python does not contain CTranslate2 or SentencePiece. The existing Ollama cache contains only an embedding model; it cannot supply translation.
- Official model download HEAD probes returned HTTP 403. Alternate mirrors require provenance/checksum review before use.

## Remaining gates

1. Obtain explicit installation/download approval under AGENTS.md section 9.
2. Deploy isolated runtime packages and licensed, verified model files entirely under A:.
3. Verify real bidirectional input translation, screenshot -> OCR -> translation, copy, active inference cancellation and cleanup.
4. Measure cold latency, peak total working set including the inference child, idle memory and complete package size.
5. Verify operation with outbound connections unavailable, without changing the machine's global network settings.
6. Package the complete application and record the exact verification boundary.

## Completeness audit update

The audit reproduced and repaired stdin cancellation, previous-output loss and missing WinForms DPI configuration. Local service checks (11), focused audit checks (10), worker error-contract checks (4) and external GUI local checks (7) pass. The GUI acceptance gate now explicitly fails when actual translation is unavailable. Runtime/models are still absent and have not been approved for installation. See the dated completeness audit for evidence and remaining gates.
