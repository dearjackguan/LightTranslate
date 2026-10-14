# OCR paragraph reconstruction, version 1.0.3

## Authorized change and verified cause

The user requested an adjustment based on the preceding WeChat investigation. The implementation adopts the useful separation between recognition geometry and paragraph processing. It uses the existing Windows OCR and original CPU INT8 translation models; it adds no package, proprietary Tencent binary, neural paragraph model or translation service.

The original helper had discarded word rectangles and joined every OCR line with a newline. The translation worker treats newlines as independent paragraphs. A genuine two-line OCR fixture therefore cut the condition after `while`, causing the existing translation model to infer a different condition.

| Stage | Original result | Revised result |
| --- | --- | --- |
| Actual OCR | `DO not turn off the computer while` + newline + `the update is being installed.` | `DO not turn off the computer while the update is being installed.` |
| Actual local translation | `计算机关闭时不要关闭` + newline + `正在安装更新。` | `在安装更新时不要关闭计算机 。` |

The before text is the actual pre-change OCR snapshot of the authored bitmap, retained in `verification/ocr-layout-before.json`. Translation comparison uses the unchanged original model. Raw before/after outputs and their scope are in `verification/ocr-layout-comparison.json`. This example proves improved condition preservation for that fixture, not a general accuracy percentage.

## Implementation

- `scripts/ocr-worker.ps1` retains recognized word text, pixel rectangles, recognizer language and text angle in its local JSON response. Original text remains available for conservative fallbacks. Recognition, temporary files and IPC remain local.
- `src/OcrLayout.cs` owns reconstruction. Large horizontal gaps split an OCR line into separate fragments. Ordinary word spacing is estimated without including column gutters. A sufficiently small measured gap between alphabetic fragments can remove a false internal space.
- Paragraph grouping uses vertical spacing, left/right alignment and the median glyph height. First-line and hanging-list indentation are supported when the geometry agrees. New list entries, large font changes, short ending lines and vertical paragraph gaps retain boundaries.
- Reading order uses visible whitespace cuts: independent columns are read left to right, each top to bottom. A heading spanning columns stays ahead of their body text. Ambiguous layouts use geometric row order.
- Chinese adjacent text is joined without an invented word space; English lines receive a separator. Recognized word content and punctuation are retained. Missing geometry or text angled beyond three degrees keeps the original line layout.
- `src/OcrService.cs` invokes reconstruction after parsing the worker response. The native UI and controller retain their existing responsibilities; manually entered line breaks are unaffected.

## Evidence-driven corrections during implementation

The actual two-column fixture exposed `Save the document befo re`. Native word rectangles put `befo` at x=296..355 and `re` at x=358..382: a 3-pixel internal gap, compared with ordinary 9..11-pixel word spaces. Geometry-driven reconstruction corrected it to `before`; no vocabulary lookup or hardcoded replacement was added.

The same fixture showed different ink heights within the same font: a line without descenders had a 22-pixel box while another line reached 29 pixels. Comparing whole-line rectangle heights incorrectly classified them as different font sizes. Median word height, including the average of both middle values for even sample counts, repaired that classification while retaining the heading guard.

## Accepted gates

- Build: x64 .NET Framework compiler, warning level 4, warnings treated as errors.
- Layout: 19 checks, comprising 16 structured geometry/fallback cases and three genuine Windows OCR bitmaps. Cases cover English/Chinese wraps, paragraph gaps, list boundaries/continuations, first-line and hanging indentation, separate indented blocks, heading sizes, short headings, column order, a heading over columns, combined-column OCR lines, identifiers, legacy text and rotation fallback. Actual fixtures cover English, Chinese and two columns; the English fixture also runs the real local translator.
- Genuine services: 15/15, including English/Chinese input, default automatic direction, scoped semantic decoding, real OCR, blank/error behavior, Unicode temporary paths and cancellation cleanup.
- Focused process/UI audit: 11/11, including cancellation, previous-output preservation, hotkey conflict and oversize OCR.
- Production GUI: 14/14, `accepted=true`, both actual translation flags true and no unavailable checks. It includes the actual global shortcut, physical capture, OCR/translation, Escape/right-click cancellation, automatic/manual directions, copying, close-during-inference cleanup and the new wrapped-screenshot condition check. The final production executable was rebuilt before this successful gate.
- Display evidence: 1920x1080 desktop, 120 DPI. GUI idle working set 40,054,784 bytes; measured pre-close application peak 96,235,520 bytes. These figures exclude the inference child. Mixed-DPI/multimonitor behavior remains unverified.

The earlier foreground-blocked GUI rerun in the weight-training report is historical. The current global shortcut and physical wrapped screenshot passed without changing the product's shortcut handling or disabling the acceptance gate.

## Delivery and limits

Application: `A:\Agent\Codex\Translation\app\LightTranslate.exe`. Portable archive: `A:\Agent\Codex\Translation\release\LightTranslate-1.0.3-win-x64.zip`. The previous 1.0.2 archive and rejected training checkpoints remain preserved. Final CRC, artifact identity and relocated inference results are recorded in `release/package-results.json` and `release/portable-results.json` after packaging.

Finalized ZIP CRC, production executable/worker byte identity, isolated relocated Python prefix and six actual relocated manual/automatic inference requests all pass. Archive: 189,481,122 bytes; extracted files: 306,044,559 bytes, 1,534 files. SHA-256: `5235b34dd7adc983cedfff2bf187ab33c12d664cd8653aad5ae9a8387200cb3d`.

Both original Argos models are unchanged. The five weight-training candidates remain outside the application and are not promoted by this OCR release. Recognized character substitutions, difficult table reading order, ambiguous headings, unusual fonts and arbitrary layout are not solved by paragraph heuristics. Rotated fallback was tested as structured data, not as a separate tilted-image accuracy benchmark. No WeChat-equivalent recognition accuracy or verified current WeChat offline translation architecture is claimed.

The preceding research is in `wechat-ocr-and-translation-2026-10-05.md`. This implementation uses original geometry logic and Windows APIs already present in the project.
