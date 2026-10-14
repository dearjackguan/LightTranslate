"""Run a bounded, reproducible local Marian weight-fine-tuning experiment."""

import hashlib
import argparse
import json
import math
import os
from pathlib import Path
import random
import re
import time

ROOT = Path(r"A:\Agent\Codex\Translation\training")
os.environ["HF_HOME"] = str(ROOT / "cache" / "huggingface")
os.environ["HF_HUB_OFFLINE"] = "1"
os.environ["HF_HUB_DISABLE_TELEMETRY"] = "1"
os.environ["TOKENIZERS_PARALLELISM"] = "false"

import torch
from transformers import AutoModelForSeq2SeqLM, AutoTokenizer


def read_pairs(name):
    return [json.loads(line) for line in (ROOT / "data" / (name + ".jsonl")).read_text(encoding="utf-8").splitlines()]


def encode(tokenizer, pairs):
    batch = tokenizer([pair["zh"] for pair in pairs], text_target=[pair["en"] for pair in pairs],
                      padding=True, return_tensors="pt", truncation=False)
    assert max(batch["input_ids"].shape[1], batch["labels"].shape[1]) <= 128, "Training example exceeds the token budget."
    batch["labels"][batch["labels"] == tokenizer.pad_token_id] = -100
    return {key: value.to("cuda") for key, value in batch.items()}


def dev_loss(model, tokenizer, pairs):
    model.eval()
    losses = []
    with torch.no_grad(), torch.autocast("cuda", dtype=torch.float16):
        for start in range(0, len(pairs), 4):
            batch = pairs[start:start + 4]
            losses.append(float(model(**encode(tokenizer, batch)).loss) * len(batch))
    return sum(losses) / len(pairs)


def train_epoch(model, tokenizer, pairs, optimizer):
    model.train()
    random.shuffle(pairs)
    optimizer.zero_grad(set_to_none=True)
    losses, updates = [], 0
    batches = math.ceil(len(pairs) / 4)
    for index, start in enumerate(range(0, len(pairs), 4)):
        loss = model(**encode(tokenizer, pairs[start:start + 4])).loss
        assert torch.isfinite(loss), "Non-finite loss."
        group_size = min(4, batches - (index // 4) * 4)
        (loss / group_size).backward()
        losses.append(float(loss.detach()))
        if (index + 1) % 4 == 0 or index + 1 == batches:
            torch.nn.utils.clip_grad_norm_(model.parameters(), 1.0, error_if_nonfinite=True)
            optimizer.step()
            optimizer.zero_grad(set_to_none=True)
            updates += 1
        if (index + 1) % 40 == 0:
            print(json.dumps(dict(batch=index + 1, batches=batches, recentLoss=sum(losses[-40:]) / 40)), flush=True)
    return sum(losses) / len(losses), updates


def fingerprint(tensor):
    return hashlib.sha256(tensor.detach().cpu().contiguous().numpy().tobytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--run-name", default="round-1")
    arguments = parser.parse_args()
    assert re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_-]{0,63}", arguments.run_name), "Use a safe run-directory name."
    run_directory = ROOT / arguments.run_name
    run_directory.mkdir(parents=True, exist_ok=False)
    assert torch.cuda.is_available(), "CUDA is unavailable; do not silently switch to CPU."
    torch.manual_seed(20261005)
    random.seed(20261005)
    torch.set_num_threads(4)
    model = AutoModelForSeq2SeqLM.from_pretrained(ROOT / "base-zh-en", local_files_only=True).to("cuda")
    tokenizer = AutoTokenizer.from_pretrained(ROOT / "base-zh-en", local_files_only=True, use_fast=False)
    model.config.use_cache = False
    training, development = read_pairs("train"), read_pairs("development")
    domain_development = read_pairs("domain_development")
    short_development = read_pairs("short_development")
    parameter = model.model.decoder.layers[0].self_attn.q_proj.weight
    before = fingerprint(parameter)
    report = dict(seed=20261005, base=str(ROOT / "base-zh-en"), gpu=torch.cuda.get_device_name(),
                  trainableParameters=sum(p.numel() for p in model.parameters() if p.requires_grad),
                  trainingExamples=len(training), developmentExamples=len(development),
                  batchSize=4, accumulationSteps=4, learningRate=2e-5, maxTokens=128, precision="float32", epochs=[],
                  baselineDevelopmentLoss=dev_loss(model, tokenizer, development), beforeParameterSha256=before)
    print(json.dumps(report), flush=True)
    optimizer = torch.optim.AdamW(model.parameters(), lr=2e-5, weight_decay=0.01)
    start = time.perf_counter()
    for epoch in range(1, 3):
        loss, updates = train_epoch(model, tokenizer, training, optimizer)
        validation = dev_loss(model, tokenizer, development)
        destination = run_directory / ("zh-en-epoch-" + str(epoch))
        model.save_pretrained(destination, safe_serialization=True)
        tokenizer.save_pretrained(destination)
        row = dict(epoch=epoch, trainLoss=loss, developmentLoss=validation, optimizerUpdates=updates,
                   domainDevelopmentLoss=dev_loss(model, tokenizer, domain_development),
                   shortDevelopmentLoss=dev_loss(model, tokenizer, short_development),
                   checkpoint=str(destination), parameterSha256=fingerprint(parameter),
                   elapsedSeconds=round(time.perf_counter() - start, 3), peakAllocatedBytes=torch.cuda.max_memory_allocated())
        assert row["parameterSha256"] != before, "The model parameter did not change."
        report["epochs"].append(row)
        print(json.dumps(row), flush=True)
        (ROOT / "training-results.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    for row in report["epochs"]:
        row["selectionScore"] = 0.7 * row["developmentLoss"] + 0.15 * row["domainDevelopmentLoss"] + 0.15 * row["shortDevelopmentLoss"]
    report["selectedCheckpoint"] = min(report["epochs"], key=lambda row: row["selectionScore"])["checkpoint"]
    report["selectionCriterion"] = "70% generic, 15% domain and 15% short-text development loss; short acceptance and exposed diagnostic fixtures are excluded from training."
    (ROOT / "training-results.json").write_text(json.dumps(report, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
