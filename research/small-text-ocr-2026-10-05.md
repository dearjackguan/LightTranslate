# Small-text OCR repair in 1.0.4

## Mechanism and reproduction

The user requested continued small-text repair and comparison with Pot and WeChat. The production OCR worker previously decoded a native screenshot and recognized it once without small-text preparation. Authored ClearType bitmaps reproduce omissions and substitutions at 10/12/14/16 physical font pixels. The configured Windows recognizer is zh-Hans-CN; results are specific to this machine, recognizer and fixtures.

Three hypotheses were examined: insufficient glyph pixels, ClearType color fringes and interpolation/contrast effects. The same 12-pixel English bitmap becomes exact after 2x enlargement. Grayscale, stronger contrast, nearest/linear/Fant interpolation and 3x enlargement do not consistently improve Chinese; some variants add errors. Diagnostic artifacts retain all observations rather than only successful examples.

## Implementation

- Recognize the original bitmap and estimate median word-box height from words containing letters/digits. Ignore punctuation-only boxes.
- If the median height is below 18 pixels, or the original recognition is empty, retry once with Windows BitmapTransform cubic interpolation, BGRA8 and ignored alpha. The transform reads the original PNG rather than enlarging an already enlarged intermediate image.
- Limit scale to 2x, the current OcrEngine.MaxImageDimension and an 8,000,000-pixel enlargement budget. Never reduce a native image. Native oversize images retain the previous explicit error.
- Keep original text when the enlarged pass is empty. Otherwise pass text and geometry from the same selected result to the existing paragraph reconstruction. Record selected scale and original dimensions in worker JSON; relative layout geometry remains internally consistent.
- Both SoftwareBitmap instances and the stream are disposed. Existing 20-second process timeout, cancellation and temporary-file cleanup cover both passes.
- No package, neural OCR model, Tencent component or translation weight was added. Both original offline translation models remain active.

## Local comparison

Eight fixtures, each compared with the preserved 1.0.3 executable and the new worker through actual Windows OCR:

| Language | Font pixels | Old character edits | New character edits |
|---|---:|---:|---:|
| English | 10 | 26 | 9 |
| English | 12 | 6 | 0 |
| English | 14 | 5 | 0 |
| English | 16 | 0 | 0 |
| Chinese | 10 | 15 | 11 |
| Chinese | 12 | 10 | 6 |
| Chinese | 14 | 5 | 3 |
| Chinese | 16 | 2 | 2 |

Total 69 -> 31 Levenshtein character edits against 200 reference characters after whitespace removal. Exact production strings are additionally required for the 12/14/16-pixel English fixtures. These authored fixtures were used during diagnosis and are not an independent corpus or a general OCR accuracy benchmark.

The Chinese 16-pixel sample still reads 尚耒 instead of 尚未 and loses punctuation. The 10-pixel Chinese sample remains unusable despite fewer character edits. Enlarging raster pixels cannot reconstruct genuinely lost strokes. No dictionary substitution, translation-driven source correction or invented confidence was added.

## Pot and WeChat evidence

Pot's checked local reference revision is `594d32ede96acd106b0256deaa8bb440ffcdff40`. Its [capture](https://github.com/pot-app/pot-desktop/blob/594d32ede96acd106b0256deaa8bb440ffcdff40/src-tauri/src/screenshot.rs) and [crop](https://github.com/pot-app/pot-desktop/blob/594d32ede96acd106b0256deaa8bb440ffcdff40/src-tauri/src/cmd.rs) preserve a PNG region. The [Windows system OCR adapter](https://github.com/pot-app/pot-desktop/blob/594d32ede96acd106b0256deaa8bb440ffcdff40/src-tauri/src/system_ocr.rs) calls Windows.Media.Ocr directly; that checked path contains no adaptive enlargement. This is one backend, not every Pot OCR service.

The official organization also maintains the [Paddle OCR plugin](https://github.com/pot-app/pot-app-recognize-plugin-paddle). Its [main.js](https://github.com/pot-app/pot-app-recognize-plugin-paddle/blob/main/main.js) invokes PaddleOCR-json.exe with angle classification and language config. Its [build workflow](https://github.com/pot-app/pot-app-recognize-plugin-paddle/blob/main/.github/workflows/build.yml) bundles PaddleOCR-json 1.3.1. The plugin delegates OCR to a separate engine rather than revealing a special Pot small-text algorithm. No plugin code/assets were incorporated.

The existing local WeChat plugin contains text_det_fp16_v2.xnet, text_rec_zh13562_fp16_v2.xnet and paragraph_det_fp16_v2.xnet. Names suggest detection/recognition/paragraph roles; they do not reveal resize thresholds or architecture. The [third-party calling implementation](https://github.com/swigger/wechat-ocr/blob/e32d4af10d8045f8613078bac2df442662c76b03/src/wechatocr.cpp) submits an image path and returns text/geometry; it is not Tencent's open OCR implementation.

A further read-only check finds DeepSR_REPSR_R_M4C16_Flip_ep24.xnet (45,388 bytes) in the same package. DLL strings mentioning super_resolution_net include a QBar initialization message. This is insufficient to establish that text OCR invokes super resolution; the visible QBar context must not be presented as an OCR algorithm. No WeChat host or plugin was executed.

## Verification and remaining scope

- Small-text comparison: eight actual paired OCR cases, cleanup and exact English assertions.
- Scale boundaries: seven checks for ordinary text, punctuation exclusion, the even-count median, small/empty recognition, dimension cap, pixel budget and native images above the enlargement budget.
- Paragraph layout: 19 checks, including three actual bitmaps and wrapped offline translation.
- Genuine services: 15/15; focused audit: 11/11 after closing the existing application that owned the global shortcut.
- Production GUI: 15 checks, accepted, including a physical 12-pixel ClearType screenshot and its actual offline translation, global shortcut, wrapped context, blank feedback, cancellation, copy and real-model close cleanup.

Limits: tiny Chinese substitutions remain; blurred/compressed screenshots, colored or dark backgrounds, complex tables, arbitrary font mixtures and mixed-DPI multimonitor displays have no broad acceptance benchmark. The same Windows OCR engine and language selection remain in use.

Artifacts: `A:\Agent\Codex\Translation\probes\small-text`, `A:\Agent\Codex\Translation\verification\small-text-results.json`, and the preserved `LightTranslate-pre-small-text-1.0.3.exe`.

## Final delivery

Version 1.0.4.0 executable: 53,760 bytes, A:\Agent\Codex\Translation\app\LightTranslate.exe. Final GUI acceptance is true, 15 checks, no unavailable gates; 1920x1080 at 120 DPI. Idle app memory 40,120,320 bytes and pre-close peak 96,751,616 bytes exclude the inference child.

Portable archive: A:\Agent\Codex\Translation\release\LightTranslate-1.0.4-win-x64.zip. SHA-256 a996e41f5ac747c800a9e2dc68187423a7af286ef6c67ffedbb778df2e6d40f6. Archive 189,482,027 bytes; extracted 306,046,958 bytes; 1,534 files. CRC, executable/worker identity and six actual relocated inference requests pass. Relocation directory includes spaces. Previous 1.0.3 release remains preserved.

The scale helper uses the average of the central pair for an even number of heights. Final source was rebuilt and the physical GUI gate rerun successfully after this boundary correction. A locked-executable build attempt was resolved by graceful closure of the exact application; no process was forcibly terminated.

Read-only WeChat asset hashes and inference boundary: A:\Agent\Codex\Translation\research\wechat-small-text-evidence.json. No Tencent assets are bundled.
