# Finished offline translator plan

## Request and acceptance

The user authorized continued repair and asked for a plan followed by implementation. Existing scope remains a lightweight Windows application for typed translation and screen-region translation, automatic Chinese/English direction and fully local inference. Version 1.0.4 remains available for rollback.

1. Audit the capture -> OCR -> paragraph reconstruction -> translation path and current dependencies.
2. Freeze new small Chinese, English and mixed-text screenshots at 12/14/16 physical pixels on light/dark backgrounds before running a candidate. Track critical negations, digits and versions separately from aggregate character error.
3. Evaluate a documented redistributable local OCR component against Windows OCR. Prefer a compact standalone component with bounded short-lived processes and no system installation.
4. Integrate the selected engine through the OCR service, preserve paragraph processing, cancellation, visible failures, complete source text and cleanup. Uncertain OCR must be surfaced without presenting confidence as calibrated correctness.
5. Verify actual native UI capture and inference, package the model/runtime/licenses, test relocation and publish a runnable offline bundle with evidence and limitations.

## Inspected alternatives

Pot's Paddle plugin delegates to PaddleOCR-json rather than implementing the OCR neural model itself. The upstream PaddleOCR-json project also points to RapidOCR-json as a lower-overhead alternative. Both project README size figures vary across revisions/model sets; they are indicative, not verified package measurements.

RapidOCR-json v0.2.0 exposes local JSON input/output, boxes, text and recognition scores. It bundles ONNX inference and Chinese/English-capable OCR models. Its wrapper is MIT licensed; bundled model/runtime notices must also be collected before distribution. The checked API permits a standalone child process, avoiding an additional Python environment or a permanent server. Subsequent actual quality and memory measurements are recorded in offline-ocr-product-2026-10-05.md.

Sources: https://github.com/hiroi-sora/RapidOCR-json ; https://github.com/hiroi-sora/PaddleOCR-json ; https://github.com/pot-app/pot-app-recognize-plugin-paddle .

## Dependency authorization boundary

The user replied "做" after the plan and explicit OCR dependency request. This authorizes isolated download, evaluation and integration of RapidOCR-json and its offline OCR models. The official archive hash has been verified locally, the selected subset is installed and the replacement is implemented. See offline-ocr-product-2026-10-05.md for final evidence and limits.

## Current risks

- 1.0.4 still confuses Chinese characters and loses punctuation; enlargement alone does not recover lost strokes.
- New-engine geometry is line-level, while existing Windows OCR returns word-level boxes. The adapter must preserve complete text and honest geometry without synthesizing word positions.
- Recognition scores are model scores, not a probability of correctness; required negations/digits are evaluated against references.
- Cold startup, peak memory, Unicode/space paths, missing models and cancellation require actual process tests.
- No promise is made for unreadable 10-pixel rasters, arbitrary tables or mixed-DPI multimonitor capture.

## Frozen baseline completed

Manifest: A:\Agent\Codex\Translation\ocr-evaluation\fixtures\manifest.json.
Manifest SHA-256: 4e0a5db8061930be39655c57e39e87849ec40a765de7e3464cb013ddfe8901bb.
Windows 1.0.4 baseline: 72 actual OCR cases; 187 whitespace-excluded character edits across 1686 reference characters; 13/72 exact after whitespace removal; 32/72 cases missing at least one critical reference marker. Full raw output and per-case markers are preserved in windows-1.0.4.json. This authored cohort is frozen before candidate inference and does not overlap the previous eight diagnosis sentences.

The RapidOCR-json v0.2.0 official release asset is 73,461,693 bytes and includes multiple language/model sets. Its published SHA-256 is 7ad9b283d03436c6cd0296723188699299cb4e5cf9140b410c59543aa5793c40. After approval the downloaded archive matched this exact hash. The selected executable/models total 33,065,176 bytes; including preserved license/attribution files, 33,443,014 bytes before provenance.json. The final release records the whole package size separately.
