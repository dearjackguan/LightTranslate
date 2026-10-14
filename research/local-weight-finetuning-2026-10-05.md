# Local weight fine-tuning: completed pilot, candidates not released

This report records the completed weight-training pilot and its restoration to 1.0.2. A subsequent, separately authorized OCR paragraph improvement is delivered as version 1.0.3 using the unchanged original Argos weights. Its genuine GUI rerun passes, superseding the historical foreground-blocked restoration rerun below. See `ocr-paragraph-reconstruction-2026-10-05.md`; no trained candidate was promoted.

## Deployment decision

The user explicitly authorized an isolated PyTorch/Transformers environment, official trainable checkpoints, licensed public replay data and a small local experiment. Five completed training rounds genuinely changed model parameters. No trained candidate passed the complete acceptance contract. The working application retains version 1.0.2 and both original Argos 1.9 models. The immutable 1.0.2 release ZIP remains preserved; no 1.0.3 release was published.

Round 4 was temporarily integrated for genuine service verification, failed the unchanged Chinese greeting check, and was immediately rolled back. Its complete package is retained at `A:\Agent\Codex\Translation\training\integration-rejected-round4`. Round 5 remained isolated. The active deployment manifest was reconciled with the restored original model metadata.

## Reproducible environment and assets

- Training root: `A:\Agent\Codex\Translation\training`, isolated venv, caches and temporary files. No global Python or GPU driver change.
- PyTorch 2.7.1+cu126; Transformers 4.51.3; CTranslate2 4.6.0; SentencePiece 0.2.0; sacrebleu 2.5.1. Exact transitive versions are retained in `requirements-lock.txt`. Training packages and checkpoints are outside the application bundle.
- Actual CUDA forward/backward proof: NVIDIA GeForce GTX 1660 Ti, 6,144 MiB VRAM. Final training peak allocated GPU memory was 1,696,225,280 bytes. This excludes other GPU allocations and is not total host memory.
- Official base: `Helsinki-NLP/opus-mt-zh-en`, revision `cf109095479db38d6df799875e34039d4938aaa6`, CC BY 4.0. Base PyTorch file SHA-256: `9d8ceb91d103ef89400c9d9d62328b4858743cf8924878aee3b8afc594242ce0`. This trainable checkpoint is distinct from the deployed Argos model.
- Public replay: OPUS Tatoeba v2023-04-12, `cmn-en`, CC BY 2.0 FR. Archive SHA-256: `751fe5a2167b55a3120f5b21d81f8e9fd22c76327c17c670c2fff86e3833caef`. Original README/LICENSE, source row indices and attribution are retained. Corpus row identifiers are not asserted to be Tatoeba public sentence IDs.
- Project-authored correction templates/references are MIT licensed. No user chat, private screenshot or clipboard was used for training.

## Training protocol and results

Marian encoder/decoder, 77,943,296 trainable parameters; batch 4, accumulation 4, AdamW learning rate 0.00002, weight decay 0.01, bounded 128 tokens and two epochs. Length violations and non-finite losses/gradients fail explicitly. Parameter fingerprints before and after training prove weight changes. Each round restarted from the untouched official base.

| Round | Training examples including repetition/augmentation | First-epoch generic development loss | Outcome |
| --- | ---: | ---: | --- |
| 1 | 1,339 | 0.896832 | Domain gains; editor/power mistakes |
| 2 | 1,564 | 0.903064 | Currency/power gains; optional action interpreted as prohibition |
| 3 | 1,726 | 0.912129 | Simple modal gains; composed order/necessity failures |
| 4 | 2,044 | 0.921152 | Better composed instructions; real integration greeting regression |
| 5 | 4,372 | 0.946443 | Added short replay; CPU greeting repaired, software terminology acceptance failed |

Untouched base generic development loss was 1.011524. Each round selected epoch 1. Round 5 completed 274 optimizer updates per epoch in 212.577 seconds and retained both checkpoints. Its selected parameter fingerprint is `e8ce76f3b897ce18c8249076135c7e58b83f3eb76dfef35e7c89e445db598c8c`, different from base `b40916672f50b707c4adc8be21c0824dc2e1469e8fca861ba0706b8e23214679`.

Round 1 used AMP. A later AMP trial raised a non-finite gradient error; the guard was retained and subsequent successful training used FP32. No warning or failing safety check was disabled.

Round 4 selection used 80% generic and 20% domain development loss. Round 5 used 70% generic, 15% domain and 15% short-text development loss. The 52 previously observed authored domain references are development diagnostics, not an unseen final benchmark. The separate eight acceptance contrasts were excluded from training but became observed diagnostics after their first evaluation. Templates and evaluation can share domain families; lexical disjointness does not establish domain-generalization independence.

## Data and evaluation boundaries

Public replay originally filtered Chinese length 4..80 and English length 12..180; only 40 of 2,044 round-4 examples had Chinese length at most 5. The short-input probe found both a pre-existing unpunctuated greeting weakness and a fine-tuning regression.

Round 5 added 500 unique public short pairs, 100 short development pairs, 50 short test pairs and terminal-punctuation-removal augmentation of training pairs only. Train/development/test canonical text intersections are rejected. The 16 short acceptance inputs, including both `你好世界` variants, and the eight semantic acceptance inputs were excluded from training. Training-file SHA-256 is `6f54de3c6883402d3caecce14c7dc5912a1f9049bf58e534ea67e02bd3845dab`.

Round 4's 100-public-reference chrF2 improved from the production model's 57.302 to 57.659, and another 100-reference set from 52.007 to 55.621. Authored diagnostic scores improved more, but those examples informed development. These scores are reference similarity, not translation accuracy percentages. Public replay/test sentences may have appeared in the base model's pretraining. No standardized unseen benchmark or expert-gold evaluation is claimed.

Round 5's focused acceptance was evaluated before a broader benchmark: 24 genuine baseline comparisons, 24 actual CPU INT8 outputs and four protected-identifier outputs. The software-action gate failed, so additional broad scoring was stopped. Current `evaluation-results.json` and `int8-evaluation-results.json` describe this focused round-5 evaluation. Full earlier reports remain under `round-4-*` names; their results must not be attributed to the round-5 checkpoint.

## Conversion and discovered failures

The checkpoint has distinct `source.spm` and `target.spm`. The worker now supports explicit source/target tokenizers while retaining the legacy shared tokenizer behavior. Source chunks must receive EOS: the Marian converter defaults to caller-provided EOS, whereas this worker provides raw SentencePiece tokens. Setting converted `add_source_eos=true` fixed the verified conversion protocol mismatch without changing weights. Each CPU candidate model is 79,567,635 bytes.

GPU FP16 generation and CPU INT8 generation can choose different outputs. For round 5, GPU produced `How are you doing in the world?` for `你好世界`, while the actual CPU candidate produced `Hello world.` for both punctuation variants. A GPU-only result is therefore insufficient to accept or reject the shipped runtime.

The remaining CPU acceptance failure is `电源必须保持接通，应用程序可以关闭。` -> `The power supply must remain connected. The application can be turned off.` The required software action is closing the application, distinct from the hardware power action. This terminology requirement is recorded as a failing gate. Another remaining fluency problem is `维修报价是925元人民币，税费另计。` -> `The price for maintenance is 925 Chinese yuan. Taxes are separately.` Currency and exclusion markers alone do not prove a complete, natural translation.

The final candidate report records seven of eight focused semantic guards passing, both greeting guards passing and all four identifier guards passing, with `accepted=false`. Round 4's earlier, narrower `accepted=true` report did not cover its later service greeting failure; it is not production acceptance.

## Working application and next evidence needed

The restored application was rebuilt with compiler warnings treated as errors. Genuine bidirectional services, default automatic direction, scoped tax/power decoding and Windows OCR pass 15/15. Local service checks and the process/UI audit each pass 11/11. Identifier checks pass 16 actual cases and six corruption/literal guards; sentence/token boundary checks pass 16/16.

The earlier 1.0.2 production GUI acceptance passed 13/13. The final restoration rerun passed typed translation and three other checks, but timed out before any global-shortcut selection overlay appeared. A targeted desktop probe found an unlocked Default input desktop, foreground process `GameViewer`, no held modifiers, and a refused `SetForegroundWindow` attempt for the owned application. Injected Ctrl+Alt+S did not create the overlay. This supports an input/focus interference hypothesis, not proof of the exact hook or a product fix. Current `gui-results.json` remains `accepted=false`, `inputTranslated=true`, `screenshotTranslated=false`. A controlled physical screenshot retest is unfinished; historical GUI evidence must not be presented as a successful new restoration run.

The existing version 1.0.2 ZIP is retained at `A:\Agent\Codex\Translation\release\LightTranslate-1.0.2-win-x64.zip`, SHA-256 `94d5f64102f82156e5f3abc89f4adab519fb741cf76f8742643ae3aff92f8112`. The working tree contains preparatory dual-tokenizer and trained-model preservation support; it does not imply that fine-tuned weights are deployed or that this immutable ZIP contains those later source changes.

A subsequent experiment should first review and diversify natural software-action references, broaden generic replay and define fresh independent acceptance before training. Existing diagnostics must retain their exposed role. Do not optimize directly on production acceptance sentences or add a greeting lookup to force their passage. English-to-Chinese weight training, teacher distillation and OCR paragraph reconstruction were not performed in this pilot.

WeChat investigation is documented in `wechat-ocr-and-translation-2026-10-05.md`. Local plugin assets and the WMT21 research paper do not establish the current WeChat translation model or disconnected client behavior.
