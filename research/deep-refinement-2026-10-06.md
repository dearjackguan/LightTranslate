# LightTranslate 1.2.0 deep refinement

## Scope and authorization

The user authorized the five-part refinement plan with `做`. This release retains completely offline Chinese/English input and screenshot translation. No dependencies, OCR weights or translation weights were added or replaced. The 1.1.0 portable archive remains available for rollback.

## Confirmed causes and fixes

- Actual 1.1.0 inference changed `250元` and `500元` into dollar amounts. The existing protected-span restoration now preserves recognized currency identity and amount text. Bare `元` becomes `yuan`; explicit RMB/CNY and other supported currencies use an explicit target representation. Amounts are not converted or rounded. Protected URLs, email, paths and code retain precedence. Malformed/grouped decimal and scaled-amount boundaries cannot receive a partial rewrite.
- Actual inference interpreted `after saving your work` as `节省工作后`. A beam-8 diagnostic on the unchanged model contained a correct `保存` candidate. Only matching desktop-save contexts use that candidate profile; incompatible saving-money wording is rejected. The repaired sentence is `保存工作后请关掉电脑 。`.
- An actual selection-form check reproduced a stuck drag after mouse capture loss. Capture loss now clears the unfinished selection; a subsequent selection succeeds. Valid mouse-up selection still completes normally. The overlay displays crop dimensions in physical pixels without adding the annotation to the captured image.
- Preserved results were visibly labeled as previous output, but copying still reported the generic success message. Copy now reports `上次译文已复制` when the result is stale. The copied content remains intact.

## Verification evidence

Artifact base: `A:\Agent\Codex\Translation`.

| Gate | Observed result | Boundary |
| --- | --- | --- |
| Semantic refinement | 31/31 cases, including 12 unchanged controls | Recognized currency identity, scoped save wording, lexical/number checks; not complete semantic certification |
| Existing translation integrity | 16/16 actual cases and 6/6 corruption/literal guards | Identifier and output integrity |
| Existing model tuning | 42 comparisons and 6 selection checks | Scoped candidate checks; unchanged model weights |
| New OCR development | 24/24 exact text | Authored 12 px fixtures |
| New separate OCR acceptance | 24/24 exact text | Different text families, frozen before evaluation |
| Previous OCR cohort | 72/72 exact text | Regression fixtures |
| Audit / services / layout | 11/11, 15/15, 19/19 | Controller, lifecycle, inference and layout seams |
| Capture loss | Reproduced failure before fix; reset and reselection pass afterward | Actual selection form; not a physical desktop drag |
| Production GUI | 17/17 through window messages | Actual window, pixels and local inference; excludes physical injection acceptance |
| Production GUI with physical injection | 17/17 on the latest run | OS keyboard/mouse injection, actual window/pixels/local inference; current desktop only |

The 48 new OCR images cover low contrast, JPEG quality 70, dark text/background arrangements, wrapping and two columns. Manifest SHA-256: `b8fab41329fe4271825fa80a95708de433f97907f680ee4df7847199201dcaaf`. Because all new development and acceptance fixtures matched, the OCR preprocessing/model pipeline was retained. The combined 120 fixture matches are not a general accuracy percentage.

An initial semantic control predicate incorrectly required `不` while correct Chinese output used `没有`. The test predicate was corrected without changing the source input or model behavior; the original fixture manifest remains alongside `semantic-fixtures-v2.json`.

Reports: `refinement\semantic-results.json`, `refinement\ocr-development-results.json`, `refinement\ocr-acceptance-results.json`, `refinement\capture-loss-results.json`, `ocr-evaluation\integrated-ocr-results.json`, `verification\gui-message-results.json` and `verification\gui-results.json`.

## Resources and offline boundary

The actual GUI report recorded 37,904,384 bytes idle and 95,035,392 bytes peak GUI working set at 120 DPI. Translation uses separate bounded child processes. Short bilingual runtime probes measured 0.988/0.987 seconds and 149.0/149.5 MiB child peak working set; the two 100-line probes measured 5.438/6.954 seconds. The scoped desktop-save request took 1.114 seconds with 160,759,808 bytes sampled child peak working set. These are current-machine fixture measurements with warm OS file caches, not performance guarantees.

Python socket/urllib audit events were denied during actual model inference. Native socket calls were not intercepted and machine connectivity was not globally disabled. Runtime translation and OCR use bundled local assets and process pipes. All 12 translation model assets match the 1.1.0 archive hashes (`refinement\model-preservation-results.json`). The production executable is 56,320 bytes; the portable bundle includes the larger runtime and models.

## Remaining limitations

- Earlier physical keyboard/mouse injection attempts failed: one did not finish its drag; another reached low-confidence review. A later current-desktop run passed all 17 checks without any product-code or confidence-threshold changes. Failed evidence remains in `usability\physical-before.json` and `refinement\gui-physical-before-activation-click.json`. This resolves the missing current-machine physical-input acceptance evidence without establishing why the earlier attempts differed.
- Mixed-DPI/multimonitor behavior and a second clean Windows machine remain unverified.
- Ambiguous `$`/`¥`, unsupported currencies, written-out numbers, scaled amounts such as `万元`, and locale decimal commas are outside the new money parser. Boundary checks prevent partial rewriting; complete preservation is not promised for unsupported forms.
- The OCR review threshold is not a calibrated probability. Difficult real screenshots can still require source correction before translation.
- The small translation models can still make grammar or meaning errors outside the scoped fixes. This release changes decoding/literal handling, not trained model weights.

Final archive size/hash and relocated inference evidence are recorded in `release\package-results.json` and `release\portable-results.json` after packaging. The portable gate checks CRC, executable/worker identity, asset hashes, bundled Python identity, bilingual translation including currency/save, and actual OCR from a Unicode directory containing spaces.
