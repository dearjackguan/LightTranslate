# LightTranslate

Author: JackGuan.

A minimal Windows desktop translator with two workflows: typed text and screen-region capture.

## Current status

Version 1.2.1 bundles local Chinese/English OCR, an isolated translation runtime and both translation models. It adds an original multiresolution application/window icon to the 1.2.0 currency preservation, scoped desktop-save decoding, crop dimensions and capture-loss recovery. Typed translation and screenshot -> OCR -> translation have passed actual inference checks. No API key or online translation endpoint is used.

Extract the complete portable ZIP, then run `LightTranslate.exe`. Keep the adjacent `ocr` and `engine` directories intact. No separate Python installation, Windows OCR language pack or model download is needed during use.

## Scope

- English to simplified Chinese and Chinese to English.
- Automatic direction by default: English prose -> Chinese, Chinese prose -> English. Manual directions remain available for mixed/ambiguous input.
- Local screen-region selection and bundled Chinese/English PP-OCRv4 inference.
- Dark screenshot normalization; low recognition scores require source review before translation.
- `Ctrl+Alt+S`: capture a region; `Esc` or right-click: cancel capture.
- `Ctrl+Enter`: translate input; cancellation during an active operation.
- Copy translated text; reject input above 5000 UTF-16 characters without truncating the editor.
- No online translation endpoint, account, history database, or background server.

## Runtime architecture

WinForms UI -> workflow controller -> capture/OCR or text input -> local translation worker.

The OCR service starts the bundled RapidOCR-json executable for each request. It sends an ASCII-escaped JSON image path through standard input and parses UTF-8 text, line rectangles and scores. The temporary PNG is deleted after success, failure, timeout or cancellation; the child process exits before the operation completes. Models use relative paths from the child's working directory, including when the application directory contains Chinese characters and spaces. No OCR server or PowerShell helper runs in production. The translation worker receives JSON through standard input and returns UTF-8 JSON; the original text is not written to a translation request file.

The inference engine is CTranslate2 on CPU with INT8 execution and SentencePiece tokenization. Only the requested direction's model is loaded in a short-lived worker process. The process exits after each translation to keep idle memory low; cold startup will cost latency. Long paragraphs are split into sentences and then bounded token chunks, retaining every input token. Chunk boundaries can affect translation quality.

SentencePiece receives serialized model bytes read through Python's Unicode-aware filesystem APIs. Its Windows 0.2.0 filename API failed during actual relocation to a Chinese directory; byte loading removes that dependency without changing tokenizer or translation weights. The CTranslate2 model directory API worked in the same relocation and is retained.

The accuracy repair keeps English titles/initials/acronyms together and attaches closing quotes to their sentence. The token budget is now 256 instead of 120, with clause and whole-word boundaries preferred for longer sentences. Beam size 4 and length penalty 0.2 were retained after local comparison; larger values did not give consistent semantic gains. See `research/translation-quality-2026-10-04.md` for the inspectable 28-case comparison and remaining model errors.

Version 1.0.1 protects recognized email addresses, HTTP/www links, Windows paths and backtick-delimited inline code with verified placeholders. The complete sentence remains available to the model; a missing/duplicated/changed marker produces an explicit retry error instead of a damaged identifier. Automatic direction excludes these literal spans, compares Chinese characters with English words, and reports the selected direction in the status line. Literal-only or numeric-only automatic input is preserved. This is a deterministic two-language heuristic; ambiguous mixed text can require the manual override. See `research/integrity-and-auto-direction-2026-10-05.md`.

Version 1.0.2 adds scoped Chinese-to-English constrained decoding for tax exclusion and computer power actions. Matching chunks use four candidates and constraints from the actual model vocabulary; incompatible candidates are rejected. Ordinary input and English-to-Chinese decoding retain the previous settings. These are inference rules, not trained weights or general semantic verification. No additional model or package is installed. See `research/model-tuning-2026-10-05.md` for the comparison, limitations and the separate weight-training proposal.

Version 1.0.3 retains OCR word rectangles and reconstructs paragraphs before translation. Compatible soft wraps are joined, headings/lists and visible column gutters remain separate, and clear columns are read independently. Measured word spacing can repair an unusually small false space inside an English word. Missing geometry or text rotated beyond three degrees retains the original line layout. This uses the existing Windows OCR and original offline models. See `research/ocr-paragraph-reconstruction-2026-10-05.md`.

Version 1.0.4 retries small-text OCR with cubic enlargement up to 2x when the initial median letter/digit box height is below 18 pixels or no text is found. It keeps the Windows OCR dimension limit and an 8,000,000-pixel enlargement budget. Normal-size text uses the original pass; an empty enlarged result retains the original result. No new package or OCR model is installed. Eight authored ClearType fixtures improve from 69 to 31 character edits across 200 reference characters, excluding whitespace. The 12/14/16-pixel English fixtures match exactly, while tiny Chinese text still contains omissions and substitutions. This is a local fixture comparison, not a general accuracy score. See `research/small-text-ocr-2026-10-05.md`.

After explicit user authorization, five local GPU fine-tuning rounds changed all 77,943,296 parameters of an official trainable Chinese-to-English checkpoint. Some domain contrasts improved, but the candidates did not pass all acceptance conditions. The OCR update retains both original Argos models. Experimental weights, dependencies and licensed replay data remain outside the application. Separate-tokenizer and model-preservation support does not imply a deployed fine-tuned model. See `research/local-weight-finetuning-2026-10-05.md` and `research/wechat-ocr-and-translation-2026-10-05.md`.

References: [CTranslate2 installation](https://opennmt.net/CTranslate2/installation.html), [Argos Translate](https://github.com/argosopentech/argos-translate), [official language model index](https://raw.githubusercontent.com/argosopentech/argospm-index/main/index.json).

## Deployment layout

Version 1.2.0 recognizes decimal amounts with explicit CNY/RMB, USD, EUR, GBP, JPY, HKD or supported Chinese currency names. Their currency identity and original numeric strings are restored through verified placeholders after translation. Bare `元` is rendered as `yuan`, without assigning a dollar symbol or applying an exchange rate. Protected URLs, emails, paths and code remain unchanged; compounds such as `元件` are excluded. Ambiguous `$`/`¥`, unsupported currencies, written-out amounts and scaled units such as `万元` remain outside this new parser. Scoped English desktop-save phrases use eight scored hypotheses and require `保存`, rejecting `节省`/`节约`; other existing decoding settings remain unchanged. This is inference/data-integrity work, not retrained weights or universal semantic verification. See `research/deep-refinement-2026-10-06.md`.

The capture overlay shows physical-pixel dimensions, resets an interrupted drag when mouse capture is lost and supports selecting again. Copy feedback explicitly identifies retained previous output. Source/translation remain separate and the original two workflows are unchanged.

Version 1.1.0 replaces the Windows OCR helper with the verified official RapidOCR-json v0.2.0 release asset and a selected PP-OCRv4 Chinese/English model set. The bundled executable's banner says v1.1.0; the archive hash and this upstream discrepancy are recorded in `ocr/provenance.json`. Screen text is treated as upright, avoiding a demonstrated false 180-degree orientation decision. Predominantly dark image borders trigger RGB inversion. Existing paragraph reconstruction receives honest line rectangles, without invented word geometry. A line score below 0.90 retains the full source and previous output and prompts manual review; this threshold is not a calibrated probability. No guessed words or fixture-specific text replacements are applied. See `research/offline-ocr-product-2026-10-05.md`.

```text
LightTranslate.exe
LightTranslate.exe.config
ocr/
  RapidOCR-json.exe
  provenance.json
  licenses/
  models/
    ch_PP-OCRv4_det_infer.onnx
    ch_ppocr_mobile_v2.0_cls_infer.onnx
    rec_ch_PP-OCRv4_infer.onnx
    dict_chinese.txt
engine/
  translation-worker.py
  python/
    python.exe
    python312.dll / python312.zip / Lib/site-packages/
  models/
    en_zh/
      metadata.json
      sentencepiece.model
      model/model.bin
    zh_en/
      metadata.json
      sentencepiece.model
      model/model.bin
```

Windows 10/11 x64 and .NET Framework 4.8 are required. The bundle includes pinned runtime packages, model assets, provenance hashes and license notices. Windows PowerShell and installed OCR languages are only needed for historical Windows-OCR comparison tests. See `THIRD_PARTY_NOTICES.md`. The earlier 2 MB budget for an online client does not apply to the full offline model bundle.

## Build and verification

The application icon is embedded in both the executable's Windows resources and its managed window resource. `assets/app-icon.svg` contains outlined glyphs and requires no runtime fonts for the logo; `assets/app.ico` supplies 16, 20, 24, 32, 40, 48, 64, 96, 128 and 256 pixel frames. Regenerate the assets with `scripts/prepare-app-icon.ps1` using the installed Microsoft YaHei UI and Segoe UI fonts. This visual patch does not modify translation-worker algorithms or model weights.

From PowerShell:

```powershell
& 'A:\Projects\Translation\scripts\build.ps1'
& 'A:\Projects\Translation\scripts\verify.ps1' -Mode local
& 'A:\Projects\Translation\scripts\verify.ps1' -Mode audit
& 'A:\Projects\Translation\scripts\verify.ps1' -Mode services
& 'A:\Projects\Translation\scripts\verify.ps1' -Mode gui
& 'A:\Projects\Translation\scripts\verify.ps1' -Mode gui-messages
& 'A:\Projects\Translation\scripts\verify.ps1' -Mode layout
& 'A:\Projects\Translation\scripts\verify.ps1' -Mode small-text
& 'A:\Projects\Translation\scripts\verify.ps1' -Mode ocr-quality
& 'A:\Projects\Translation\scripts\verify.ps1' -Mode ocr-lifecycle
& 'A:\Projects\Translation\scripts\verify.ps1' -Mode refinement-interactions
& 'A:\Projects\Translation\scripts\verify.ps1' -Mode ocr-development
& 'A:\Projects\Translation\scripts\verify.ps1' -Mode ocr-acceptance
& 'A:\Agent\Codex\Translation\app\engine\python\python.exe' -I -B 'A:\Projects\Translation\tests\SemanticRefinementVerification.py'
& 'A:\Projects\Translation\tests\OcrScaleVerification.ps1'
python -I -B 'A:\Projects\Translation\tests\TranslationWorkerVerification.py'
& 'A:\Agent\Codex\Translation\app\engine\python\python.exe' -I -B 'A:\Projects\Translation\tests\TranslationQualityComparison.py' optimized
& 'A:\Agent\Codex\Translation\app\engine\python\python.exe' -I -B 'A:\Projects\Translation\tests\TranslationIntegrityVerification.py'
& 'A:\Agent\Codex\Translation\app\engine\python\python.exe' -I -B 'A:\Projects\Translation\tests\ModelTuningVerification.py'
python -I -B 'A:\Projects\Translation\tests\AutomaticDirectionVerification.py'
```

`local` checks input validation, worker response parsing, missing-engine errors, crop geometry, actual bundled OCR and cleanup; it does not run translation inference. `services` runs actual bilingual model inference, automatic direction and scoped semantic checks. `audit` covers translation process pipes/cancellation, Unicode, recovery, previous-output preservation, hotkey registration/conflict, DPI/layout and oversized OCR. `layout` covers paragraph geometry and three actual OCR fixtures. `small-text` compares eight historical Windows 1.0.3 fixtures against the current engine. `ocr-quality` runs the 72 frozen fixtures, strict OCR parsing and the manual-review workflow. `ocr-lifecycle` verifies actual 20-second timeout, pre/post-ready cancellation, abnormal exit, Unicode paths, missing assets, actual worker recovery and cleanup.

`gui` requires an unlocked desktop accepting physical keyboard/mouse injection. The latest 1.2.0 run passed all 17 checks, including injected Ctrl+Alt+S, physical mouse selection, actual screenshot OCR/translation, small Chinese light/dark text, copying, cancellation and close-during-inference. Earlier physical attempts failed and remain archived; the later pass does not establish the cause of those desktop-dependent differences. `gui-messages` separately passed all 17 checks through explicit window messages and actual screen pixels. Neither mode claims a different desktop environment was tested. The portable verification additionally runs the actual C# OCR adapter and both models from a Unicode/space directory, including the currency/save checks.

Previous output is retained after a failed or cancelled new request and visibly labeled as the previous result. Editing the source or changing direction also marks the existing result as previous output. The request timeout and cancellation cover the process input pipe as well as model execution. The GUI gate additionally closes the app during an active genuine model request and checks that the app and child exit within 2000 ms, then restarts the application.

Build artifacts belong under `A:\Agent\Codex\Translation`; reference repositories remain unchanged under `references/`.

## Runtime deployment and performance verification

```powershell
python -I -B 'A:\Projects\Translation\scripts\install-offline-engine.py'
python -I -B 'A:\Projects\Translation\scripts\install-offline-ocr.py'
python -I -B 'A:\Projects\Translation\tests\OfflineRuntimeVerification.py'
& 'A:\Projects\Translation\scripts\package.ps1'
```

The installer downloads assets during setup only. Its model mirror is third-party and pinned by revision/hash. Runtime requests use local files exclusively. The historical 1.0.0 cold short requests took about 0.93 seconds; model child peak working set was about 145 MiB. Its two 100-line fixtures completed in about 4.9 and 6.3 seconds. Later measurements are retained in the runtime report; these historical timings are not a performance guarantee for newer versions. These are fixture measurements, not a general translation-quality benchmark.

Offline verification denies Python socket/urllib audit events while running the actual models. Native socket calls are not intercepted, and global machine connectivity is unchanged. Multimonitor/mixed-DPI behavior remains unverified; the available display was tested at 120 DPI.
