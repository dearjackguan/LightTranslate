# Small-model inference tuning and weight-training proposal

## Delivered scope

Version 1.0.2 tunes inference on the existing local Argos/CTranslate2 models. No package, checkpoint or external corpus was downloaded. Model weights are unchanged: the verification script compares each deployed model's SHA-256 with its bytes in the preserved 1.0.1 ZIP.

This is scoped constrained decoding, not fine-tuning or general semantic verification. It does not establish a general translation accuracy improvement.

## Reproduction and rejected alternatives

- Increasing beam size or changing length/coverage penalties alone did not consistently repair `不含税` -> `free of taxes`. A wider beam introduced a double-negation regression in another probe.
- Suppressing source-SentencePiece encodings of English phrases initially had no effect. The tokenizer has 32,000 pieces; the model's shared vocabulary has 65,000 tokens. The actual decoder emits tokens such as `▁free`, while the tokenizer can encode the word as `▁f`, `ree`.
- All constraints now use actual model vocabulary tokens. Unknown-token constraints are filtered before inference.
- A first tax rule still produced `not subject to additional taxes`, which also changes exclusion into exemption. That probe became a development regression, and the revised rule suppresses this phrase as well.
- Source-text substitution was investigated but not shipped. Rewriting `不含` indiscriminately would risk changing unrelated words such as `不含糊`.

Pilot evidence is retained under `A:\Agent\Codex\Translation\tuning`: `pilot.json`, `constraint-pilot.json`, `vocabulary-constraints-pilot.json`, the immutable `worker-1.0.1.py`, and the comparison reports.

## Algorithm

1. Recognize narrow Chinese-to-English tax-exclusion or computer-power patterns after existing sentence/token splitting.
2. Skip ambiguous compounds and conflicting context. For example, tax consultation, computer vision, power management, free/exempt goods and window/file/application context retain ordinary decoding.
3. Decode matching chunks with beam 4 and four scored candidates. Apply vocabulary constraints against the demonstrated incompatible lexical interpretations.
4. Select the first scored candidate matching the profile's lexical checks. If none qualifies, return `QUALITY_CHECK_FAILED`; the application retains the previous output and shows an actionable error. This does not certify every remaining word or clause.
5. Group chunks by profile, preserve original order and target prefixes, and restore protected identifiers through the existing integrity checks.

Ordinary chunks and all English-to-Chinese chunks retain the previous decoder settings. There is no extra model or new dependency. Four candidates add work only for matching chunks; no overall speed improvement is claimed.

## Evidence

`tests/ModelTuningVerification.py` compares actual CPU INT8 outputs against the preserved 1.0.1 worker using the same unchanged models. It contains 42 authored comparisons and six selection/order/vocabulary/error guards. Seven final validation sentences were added after the initial algorithm changes; previously inspected examples are labeled development/regression, not held-out evaluation. The 19 control cases retain byte-identical translation output.

| Input | Previous output | Revised output |
| --- | --- | --- |
| 每月订阅费用为19.99美元，不含税。 | The monthly subscription fee is $19.99, free of taxes. | The monthly subscription fee is $19.99, without tax. |
| 运费不含附加税。 | Freight charges are free of additional taxes. | Freight charges do not contain additional taxes. |
| 更新安装期间，请勿关闭计算机。 | Do not close the computer while it is being updated. | Do not turn off the computer while the installation is being updated. |
| 不要在下载过程中关闭电脑。 | Do not close the computer while downloading. | Do not turn off the computer while downloading. |

Raw before/after outputs, errors, timings and hashes are in `tuning/semantic-comparison.json`. A passing fixture means its scoped lexical/numeric/control checks passed, not that the entire translation was judged perfect. The existing bilingual quality and identifier gates remain separate.

## Remaining problems

- Some RMB amounts still become dollars. Number preservation alone cannot detect a changed currency.
- `Please turn off the computer after saving your work.` retains the awkward `节省工作后请关闭电脑.` in English-to-Chinese translation.
- Mixed free/exempt/excluded-tax clauses are conservatively left on the old decoder. One control still produces the wrong `Tax-free goods are not tax-free ...`; unchanged controls prove preservation, not correctness.
- File/window context skips the power rule even when a sentence also contains a genuine shutdown instruction. Wider coverage needs additional evidence.
- Tax phrasing and power expressions outside the recognized patterns remain ordinary model output. Candidate checks do not prove negation scope, timing or all propositions.
- Screenshot soft wraps, OCR errors, arbitrary identifiers and mixed-DPI coverage remain outside this tuning.

## Historical weight-training proposal, before explicit authorization

The following proposal records the pre-authorization state. The user subsequently authorized isolated dependencies, official checkpoints and licensed replay data. Actual training and its deployment decision are documented separately in `local-weight-finetuning-2026-10-05.md`; the statements below are historical, not the current environment state.

The deployed CT2 binary is an inference artifact, not a trainable PyTorch checkpoint. The current isolated runtime has no PyTorch/Transformers training installation. The GPU probe reports GTX 1660 Ti, 6,144 MiB total VRAM. That makes a small-model experiment plausible; it does not prove a training configuration fits memory.

The user's AGENTS.md section 9 requires explicit approval before new external packages. Proposed work after that approval:

1. Create an isolated training environment under `A:\Agent\Codex\Translation\training`, with PyTorch, Transformers and required tokenizer/training dependencies pinned from official distributions. Probe CUDA support and memory; do not change the global Python, driver or production runtime.
2. Obtain official trainable `Helsinki-NLP/opus-mt-zh-en` first. Its model card declares CC-BY-4.0. Only then evaluate the separate English-to-Chinese checkpoint, whose card declares Apache-2.0 and a required target-language token. Pin revisions/hashes and retain license/provenance. These are different assets from the current Argos model; no equivalence or superiority is assumed.
3. Prepare licensed Chinese/English parallel examples with author/source attribution, plus reviewed domain correction pairs for negation, before/after conditions, currencies, quantities and desktop instructions. Keep user clipboard/screenshots out of training by default. Normalize/deduplicate and split by source/template family before tuning; exclude acceptance fixtures from training. Tatoeba text is a candidate source, not yet downloaded or approved as a prepared corpus.
4. Start with bounded sequence length, batch 1, gradient accumulation and a short memory/gradient probe. Select full or partial fine-tuning only after that probe. Compare the trained checkpoint with both its untouched trainable base and the current packaged model on locked reference sets and semantic contrasts. Record losses, parameters updated, data manifest and checkpoint hashes. Stop if meaning or generic translation regresses; retain the old model.
5. Convert the winning checkpoint to CT2 INT8. Adapt separate source/target tokenizers, vocabulary and target-language prefixes as required by that checkpoint. Verify quantization quality, protected text, automatic direction, real screenshot translation, latency/memory and portable packaging before changing the production bundle. A lower training loss alone is not a deployment gate.

No weight training has been started, no new checkpoint downloaded, and no future training result is claimed. Training/downloads need setup connectivity; deployed translation must remain local with no API calls.

## Primary references

- [CTranslate2 Translator API](https://opennmt.net/CTranslate2/python/ctranslate2.Translator.html): scored hypotheses and sequence suppression.
- [CTranslate2 project](https://github.com/OpenNMT/CTranslate2): inference and model conversion.
- [Marian conversion](https://opennmt.net/CTranslate2/guides/transformers.html): checkpoint-to-CT2 integration. Subsequent authorized checkpoint conversion and its source-EOS contract are documented in `local-weight-finetuning-2026-10-05.md`.
- [Official Chinese-to-English model card](https://huggingface.co/Helsinki-NLP/opus-mt-zh-en): trainable Transformers model and CC-BY-4.0.
- [Official English-to-Chinese model card](https://huggingface.co/Helsinki-NLP/opus-mt-en-zh): Apache-2.0 and required target-language token.
- [Tatoeba licensing guidance](https://en.wiki.tatoeba.org/articles/show/handling-sentences-with-licensing-issues): compatible sentence licensing and attribution requirements. A downloaded corpus still needs its own provenance review.
