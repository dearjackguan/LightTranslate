# Translation integrity and automatic direction

## Authorized scope

The user requested a further completeness repair, then requested automatic English/Chinese direction without having to choose it. Version 1.0.1 reuses the existing isolated Python runtime and Argos models. No package, model or system component was installed.

## Reproduced defects and repairs

| Finding | Evidence | Repair |
| --- | --- | --- |
| Email local parts were translated | `support@example.com` became `支持@ example.com`; a two-address sentence changed both local parts. | Recognize literal spans; mask them during inference; restore only when each unique marker is present exactly once. Corrupt markers cause an explicit retry error. |
| Paths/code were damaged | `C:\Users\Public\notes.txt` gained spaces; `` `python -V` `` became a quoted phrase with changed spacing. | Protect recognized Windows paths, quoted paths containing spaces and backtick-delimited inline code. |
| An email instruction hallucinated an unrelated software error | `请发送邮件至 support@example.com，不要修改地址。` produced `Can not append message to maildir folder: %s: %s`. | Whole-sentence masking retains context. The repaired fixture produces `Please send an email to support@example.com, do not change the address.` |
| Previous output could appear current after editing | Source inspection showed the label changed only when a new request started. | Source edits and direction changes mark existing output as previous. A focused UI regression passes. |
| Recoverable engine errors were generic | `OUTPUT_LIMIT` used the general model-error path. | Separate messages for output length, literal preservation failure and unsupported-language automatic input. |
| Close-during-real-inference acceptance was missing | Earlier reports covered controlled child cancellation and real cancel-button behavior. | New GUI gate observes an active genuine worker over 100 MiB, closes the parent, verifies parent/child exit within 2000 ms, and relaunches the app. This observes an active loaded model request; it does not instrument individual decoder tokens. |

The first proposed fragment-by-fragment protection was rejected after a probe: it caused repeated punctuation and lost sentence context. Five placeholder formats were compared locally. The final implementation keeps the sentence together and validates marker integrity; it does not guess how a damaged marker should map back to an address.

## Automatic direction

- Default dropdown option is automatic. Explicit en->zh and zh->en remain as overrides.
- Language detection runs in the worker after excluding recognized technical spans. Chinese characters are compared against twice the English word count; Chinese wins ties. Fullwidth text is normalized for detection only, not rewritten in the user's source.
- English prose selects the Chinese model; Chinese prose selects the English model. The worker returns the actual source/target pair, and the controller shows it in the completed status.
- Technical/numeric-only automatic input is returned unchanged without model loading. Unsupported-only alphabetic prose produces a clear unsupported-language error.
- This is a two-language heuristic, not a general language identification model. Short, evenly mixed text, unformatted code and other languages can require a manual choice or be outside scope.

## Verification scope

- 16 real-model bilingual identifier cases, plus 6 corruption/literal/collision guards.
- 13 direction-detection cases and 7 real worker automatic requests, including mixed prose, ignored code/link/email and unchanged literal-only input.
- Existing 28-case bilingual quality comparison and 16 boundary checks remain applicable and pass.
- Service, focused UI/process audit, physical screenshot GUI and portable relocation reports are retained under `A:\Agent\Codex\Translation`.
- Final gates pass: services 14/14, audit 11/11, GUI 13/13 (`accepted=true`, no unavailable checks). Portable CRC plus executable/worker identity and four relocated manual/automatic requests pass. The 1.0.1 archive is 189,473,641 bytes; SHA-256 `ab19f4bb2c045f7770764b39eb980625543cb39e310dad5ff7bc591dbcf24b39`.
- These are authored fixtures and targeted regression checks; they are not a standardized translation accuracy score.

## Remaining imperfections

1. **Model semantics:** Small local models still mistranslate phrases such as `不含税` -> `free of taxes` and use awkward terms such as `close the computer`. Literal protection does not repair general semantic errors.
2. **Screenshot wrapping:** The worker treats each OCR line as a separate paragraph. A probe of `Do not turn off the computer while\nthe update is being installed.` loses the relationship, whereas the same sentence on one line translates correctly. No automatic reflow was added: blindly merging lines would also damage headings, lists and columns. Reliable repair needs layout-aware OCR paragraph reconstruction.
3. **Recognition quality:** Small/blurred text, complex columns and OCR mistakes are not comprehensively evaluated. Windows OCR currently uses the user's installed profile recognizer.
4. **Display coverage:** Only the available 1920x1080 display at 120 DPI was tested. Multimonitor/mixed-DPI acceptance remains unverified.
5. **Literal coverage:** Protection covers the recognized formats, not arbitrary filenames, bare commands, fenced multiline code, every legal email syntax or every possible URL punctuation boundary.

Source: `A:\Projects\Translation`. Evidence: `accuracy/identifier-baseline-2026-10-05.json`, `accuracy/protection-experiment-2026-10-05.json`, `accuracy/integrity-results-2026-10-05.json`, `accuracy/automatic-direction-results-2026-10-05.json`, `accuracy/wrapped-text-probe-2026-10-05.json`, `verification/`, `audit/`, `release/` under the Codex task artifact directory.
