"""Compare actual trained weights with their base and the current offline model."""

import importlib.util
import json
import os
from pathlib import Path
import subprocess
import time

ROOT = Path(r"A:\Agent\Codex\Translation\training")
os.environ["HF_HOME"] = str(ROOT / "cache" / "huggingface")
os.environ["HF_HUB_OFFLINE"] = "1"
os.environ["HF_HUB_DISABLE_TELEMETRY"] = "1"
os.environ["CUDA_CACHE_PATH"] = str(ROOT / "cache" / "cuda")


def fixtures():
    data = {name: [json.loads(line) for line in (ROOT / "data" / (name + ".jsonl")).read_text(encoding="utf-8").splitlines()]
            for name in ("test", "semantic_test", "fresh_test", "fresh_semantic_test", "final_semantic_test", "short_test", "short_semantic_test")
            if (ROOT / "data" / (name + ".jsonl")).is_file()}
    path = Path(__file__).resolve().parents[1] / "tests" / "ModelTrainingAcceptanceContrasts.json"
    data["acceptance_semantic_test"] = json.loads(path.read_text(encoding="utf-8"))
    training = [json.loads(line) for line in (ROOT / "data" / "train.jsonl").read_text(encoding="utf-8").splitlines()]
    assert not {pair["zh"] for pair in training}.intersection(pair["zh"] for pair in data["acceptance_semantic_test"])
    return data


def current_outputs():
    import ctranslate2
    import sentencepiece
    spec = importlib.util.spec_from_file_location("worker", Path(__file__).resolve().parent / "translation-worker.py")
    worker = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(worker)
    package = ROOT.parent / "app" / "engine" / "models" / "zh_en"
    tokenizer = sentencepiece.SentencePieceProcessor(model_file=str(package / "sentencepiece.model"))
    raw = ctranslate2.Translator(str(package / "model"), device="cpu", compute_type="int8", intra_threads=4)
    vocabulary = json.loads((package / "model" / "shared_vocabulary.json").read_text(encoding="utf-8"))
    translator = worker.QualityTranslator(raw, tokenizer, vocabulary, "en")
    outputs = {}
    for name, pairs in fixtures().items():
        outputs[name] = []
        for pair in pairs:
            try:
                output = worker.translate_paragraph(pair["zh"], tokenizer, translator, "en", "")
            except worker.WorkerError as error:
                output = "ERROR:" + str(error)
            outputs[name].append(output)
    return outputs


def checkpoint_outputs(path):
    import torch
    from transformers import AutoModelForSeq2SeqLM, AutoTokenizer
    torch.set_num_threads(4)
    model = AutoModelForSeq2SeqLM.from_pretrained(path, local_files_only=True).to("cuda").eval()
    tokenizer = AutoTokenizer.from_pretrained(path, local_files_only=True, use_fast=False)
    outputs = {}
    with torch.inference_mode(), torch.autocast("cuda", dtype=torch.float16):
        for name, pairs in fixtures().items():
            outputs[name] = []
            for index in range(0, len(pairs), 4):
                inputs = tokenizer([pair["zh"] for pair in pairs[index:index + 4]], padding=True, return_tensors="pt")
                generated = model.generate(**{key: value.to("cuda") for key, value in inputs.items()},
                    num_beams=4, length_penalty=0.2, max_new_tokens=256, use_cache=True)
                outputs[name].extend(tokenizer.batch_decode(generated, skip_special_tokens=True))
    del model
    torch.cuda.empty_cache()
    return outputs


def main():
    import sys
    if len(sys.argv) > 1 and sys.argv[1] == "current":
        (ROOT / "current-model-outputs.json").write_text(json.dumps(current_outputs(), ensure_ascii=False, indent=2), encoding="utf-8")
        return
    import sacrebleu
    data = fixtures()
    training = json.loads((ROOT / "training-results.json").read_text(encoding="utf-8"))
    executable = ROOT.parent / "app" / "engine" / "python" / "python.exe"
    subprocess.run([str(executable), "-I", "-B", str(Path(__file__).resolve()), "current"], check=True, timeout=180)
    outputs = {"current": json.loads((ROOT / "current-model-outputs.json").read_text(encoding="utf-8"))}
    outputs["base"] = checkpoint_outputs(ROOT / "base-zh-en")
    outputs["trained"] = checkpoint_outputs(training["selectedCheckpoint"])
    report = dict(selectedCheckpoint=training["selectedCheckpoint"], cases=[], metrics={},
                  scope="Locked authored semantic contrasts and 100 public replay references; not standardized benchmark accuracy.")
    for name, pairs in data.items():
        references = [[pair["en"] for pair in pairs]]
        report["metrics"][name] = {variant: dict(chrF2=sacrebleu.corpus_chrf(values[name], references).score,
            bleu=sacrebleu.corpus_bleu(values[name], references).score) for variant, values in outputs.items()}
        for index, pair in enumerate(pairs):
            report["cases"].append(dict(split=name, **pair, **{variant: values[name][index] for variant, values in outputs.items()}))
    (ROOT / "evaluation-results.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(report["metrics"], indent=2))
    for row in report["cases"]:
        if row["split"] in ("semantic_test", "fresh_semantic_test", "final_semantic_test", "acceptance_semantic_test"):
            print(json.dumps(row, ensure_ascii=False))


if __name__ == "__main__":
    import sys
    sys.stdout.reconfigure(encoding="utf-8")
    main()
