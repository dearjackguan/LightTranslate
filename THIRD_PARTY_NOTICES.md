# Third-party notices

LightTranslate's original native UI, capture workflow and process adapter are MIT licensed. Pot (GPL-3.0) and SnapTranslate (MIT) were inspected as workflow references. Their source repositories, branding and assets are not distributed in this application bundle.

## Distributed runtime

| Component | Version | Notice location |
| --- | --- | --- |
| CPython Windows x64 embeddable | 3.12.10 | `engine/python/LICENSE.txt` |
| CTranslate2 | 4.6.0 | `licenses/CTranslate2-MIT.txt` |
| SentencePiece | 0.2.0 | `licenses/SentencePiece-Apache-2.0.txt` |
| NumPy and its bundled libraries | 2.2.6 | `engine/python/Lib/site-packages/numpy-2.2.6.dist-info/LICENSE.txt` |
| PyYAML | 6.0.2 | `engine/python/Lib/site-packages/PyYAML-6.0.2.dist-info/LICENSE` |
| setuptools | 80.9.0 | `engine/python/Lib/site-packages/setuptools-80.9.0.dist-info/licenses/LICENSE` |

CTranslate2's Windows wheel includes Intel OpenMP and the NVIDIA cuDNN interface DLL. These files are preserved because the wheel's loader requires them even for CPU execution; LightTranslate does not select CUDA execution. Vendor licensing information: [Intel LLVM OpenMP source license](https://github.com/intel/llvm/blob/sycl/openmp/LICENSE.TXT), [NVIDIA cuDNN license](https://docs.nvidia.com/deeplearning/cudnn/backend/latest/reference/eula.html). Source license copies are under `licenses/`. The Python bundle also retains its supplied Microsoft runtime DLLs.

## Offline OCR distribution

`ocr/RapidOCR-json.exe` and the four selected model/dictionary assets are copied unchanged from the official [RapidOCR-json v0.2.0 release](https://github.com/hiroi-sora/RapidOCR-json/releases/tag/v0.2.0). The official archive SHA-256 is `7ad9b283d03436c6cd0296723188699299cb4e5cf9140b410c59543aa5793c40`. The executable reports a v1.1.0 banner; distribution provenance records the release tag and banner separately. No Pot plugin/source is packaged.

| Component | License / notice |
| --- | --- |
| RapidOCR-json, copyright 2023 hiroi-sora | `ocr/licenses/RapidOCR-json-MIT.txt` |
| RapidOCR and PaddleOCR model sources | `ocr/licenses/RapidOCR-Apache-2.0.txt`, `PaddleOCR-Apache-2.0.txt` |
| ONNX Runtime 1.14.0 | `ocr/licenses/ONNXRuntime-MIT.txt`, `ONNXRuntime-ThirdPartyNotices.txt` |
| OpenCV 4.6.0 | `ocr/licenses/OpenCV-Apache-2.0.txt` |
| nlohmann/json | `ocr/licenses/nlohmann-json-MIT.txt` |
| Clipper 6.4.2, copyright Angus Johnson 2010-2017 | `ocr/licenses/Clipper-Boost-1.0.txt`, `Clipper-attribution.txt` |

The executable embeds OpenCV build information identifying zlib 1.2.12, libjpeg-turbo 2.1.2, libpng 1.6.37, libtiff 4.2.0, OpenJPEG 2.4.0 and OpenEXR 2.3.0. Their license/attribution files are preserved under `ocr/licenses`, including the IJG README required by the JPEG notices. Additional supplied OpenCV dependency notices for WebP, protobuf, ITT and quirc are retained conservatively; their presence in this notice folder does not establish that all those components execute during OCR. License download URLs and exact file hashes are recorded in `ocr/provenance.json` and `scripts/offline-ocr-assets.json`.

## Translation models

- English -> Chinese 1.9 from the Argos Translate language package catalog. Its model/tokenizer assets remain unchanged.
- Chinese -> English 1.9 from the Argos Translate language package catalog. Its model/tokenizer assets remain unchanged. The experimental fine-tuned candidates failed acceptance and remain outside the application; no fine-tuned translation model is distributed in the OCR paragraph update.
- The packaged models' original OPUS-MT authors are Jörg Tiedemann and Santhosh Thottingal. Citation: *OPUS-MT — Building open translation services for the World*, Proceedings of the 22nd Annual Conference of the European Association for Machine Translation, Lisbon, 2020.
- The model packages state that their original OPUS models are licensed under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). Their original `README.md` notices are preserved, and license text is under `licenses/Models-CC-BY-4.0.txt`.
- The separate fine-tuning experiment used the official `Helsinki-NLP/opus-mt-zh-en` checkpoint, revision `cf109095479db38d6df799875e34039d4938aaa6`, [CC BY 4.0](https://huggingface.co/Helsinki-NLP/opus-mt-zh-en), and Tatoeba v2023-04-12 via OPUS, CC BY 2.0 FR. Attribution: Tatoeba contributors and OPUS compiler Jörg Tiedemann; [original corpus](https://tatoeba.org/en/downloads), [OPUS](https://opus.nlpl.eu/). Original README/LICENSE and training manifests are retained in the isolated training directory. Project-authored synthetic corrections are MIT licensed; none came from user chat, clipboard or screenshot data.
- Baseline Argos assets used a third-party Hugging Face mirror, **not an official upstream mirror**, pinned to revision `9dc7a2c8f29ac18e020c93f8208476d1e3fe9a9f`. Archive hashes and metadata were verified. The fine-tuning base came directly from the official Helsinki-NLP repository and its Git/LFS hashes were verified.

PyTorch, Transformers and all experimental checkpoints are kept outside the application bundle. The deployed original models use the existing CPU INT8 CTranslate2 runtime; there is no training or external translation API call in the application.

Exact download URLs, versions and archive hashes are in `engine/deployment-manifest.json`.
