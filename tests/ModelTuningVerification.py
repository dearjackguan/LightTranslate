"""Compare scoped decoding against an immutable baseline on authored fixtures."""

import hashlib
import importlib.util
import json
from pathlib import Path
import re
import sys
import time
from types import SimpleNamespace
import zipfile

import ctranslate2
import sentencepiece


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = Path(r"A:\Agent\Codex\Translation\tuning")
ENGINE = OUTPUT.parent / "portable 1.0.1 check with spaces" / "LightTranslate" / "engine"
CASES = [
    ("development", "zh", "每月订阅费用为19.99美元，不含税。", "tax-exclusion"),
    ("development", "zh", "更新安装期间，请勿关闭计算机。", "power-action"),
    ("heldout", "zh", "商品售价不包括销售税。", "tax-exclusion"),
    ("heldout", "zh", "这项服务收费为500元，未包含税费。", "tax-exclusion"),
    ("heldout", "zh", "报价不含6%的增值税。", "tax-exclusion"),
    ("heldout", "zh", "合同金额为1200美元，不包含进口税。", "tax-exclusion"),
    ("heldout", "zh", "显示的价格未包括消费税。", "tax-exclusion"),
    ("heldout", "zh", "运费不含附加税。", "tax-exclusion"),
    ("heldout", "zh", "总计99元，未含税款。", "tax-exclusion"),
    ("heldout", "zh", "报价不包括出口税，付款期限为30天。", "tax-exclusion"),
    ("heldout", "zh", "安装完成之前，不要关闭这台电脑。", "power-action"),
    ("heldout", "zh", "请在关闭电源之前保存工作。", "power-action"),
    ("heldout", "zh", "备份完成后可以关闭计算机。", "power-action"),
    ("heldout", "zh", "设备过热时，请立即关掉电源。", "power-action"),
    ("heldout", "zh", "不要在升级过程中关掉你的电脑。", "power-action"),
    ("heldout", "zh", "离开办公室之前，请关闭所有电脑。", "power-action"),
    ("control", "zh", "这项服务免费，不收取任何费用。", "default"),
    ("control", "zh", "这家商店销售免税商品。", "default"),
    ("control", "zh", "这个回答并不含糊。", "default"),
    ("control", "zh", "关闭窗口之前，请保存文件。", "default"),
    ("control", "zh", "请关闭电脑上的应用程序。", "default"),
    ("control", "zh", "请不要关闭计算机上的文件。", "default"),
    ("control", "zh", "电脑关闭后，窗口会自动关闭。", "default"),
    ("control", "zh", "他没有同意，也没有拒绝。", "default"),
    ("control", "zh", "含税价为100元，不含运费。", "default"),
    ("control", "zh", "免税商品不含税，普通商品需要缴税。", "default"),
    ("regression", "en", "Please turn off the computer after saving your work.", "desktop-save"),
    ("control", "en", "The price excludes sales tax.", "default"),
    ("control", "en", "Do not close the window until the download finishes.", "default"),
    ("control", "en", "The software is free, but the hardware is not.", "default"),
    ("control", "en", "The file was not deleted; it was moved to the backup folder.", "default"),
    ("validation", "zh", "本次报价为250元，不包含税费。", "tax-exclusion"),
    ("validation", "zh", "租金每月800美元，不包括销售税。", "tax-exclusion"),
    ("validation", "zh", "页面显示的金额未含增值税。", "tax-exclusion"),
    ("validation", "zh", "维修费用不含消费税。", "tax-exclusion"),
    ("control", "zh", "文件复制结束后，请关闭这台计算机。", "default"),
    ("validation", "zh", "不要在下载过程中关闭电脑。", "power-action"),
    ("validation", "zh", "下班时请关掉所有电脑。", "power-action"),
    ("validation", "zh", "测试完成以后，可以关闭电源。", "power-action"),
    ("control", "zh", "报价不含税务咨询费用。", "default"),
    ("control", "zh", "请关闭计算机视觉模块。", "default"),
    ("control", "zh", "请关闭电源管理功能。", "default"),
]


def load(path, name):
    spec = importlib.util.spec_from_file_location(name, path)
    worker = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(worker)
    return worker


def selection_checks(worker):
    class Tokenizer:
        def decode(self, tokens):
            return " ".join(tokens)

    class Translator:
        def __init__(self, candidates):
            self.candidates = candidates
            self.calls = []

        def translate_batch(self, chunks, **options):
            self.calls.append((chunks, options))
            return [SimpleNamespace(hypotheses=self.candidates, scores=[-i for i in range(len(self.candidates))])
                    if options.get("num_hypotheses") else SimpleNamespace(hypotheses=[tokens]) for tokens in chunks]

    raw = Translator([["Prices", "are", "tax-free"], ["Prices", "exclude", "tax"]])
    tuned = worker.QualityTranslator(raw, Tokenizer(), ["▁free", "▁exempt", "▁close"], "en")
    chunks = [["价格不含税"], ["普通文本"], ["请关闭电源"]]
    decoded = tuned.translate_batch(chunks, target_prefix=[["a"], ["b"], ["c"]])
    assert decoded[0].hypotheses[0] == ["Prices", "exclude", "tax"]
    assert decoded[1].hypotheses[0] == chunks[1]
    assert [call[1]["target_prefix"] for call in raw.calls] == [[["a"]], [["b"]], [["c"]]]
    assert tuned.forbidden("tax-exclusion") == [["▁free"], ["▁exempt"]]
    raw.candidates = [["Please", "close", "the", "computer"], ["Please", "turn", "off", "the", "computer"]]
    assert tuned.translate_batch([["请关闭电源"]])[0].hypotheses[0] == raw.candidates[1]
    raw.candidates = [["Prices", "are", "tax-free"]]
    try:
        tuned.translate_batch([["价格不含税"]])
        raise AssertionError("Semantic failure was accepted.")
    except worker.WorkerError as error:
        assert str(error) == "QUALITY_CHECK_FAILED"
    return 6


def main():
    worker = load(ROOT / "scripts" / "translation-worker.py", "tuned_worker")
    baseline = load(OUTPUT / "worker-1.0.1.py", "baseline_worker")
    report = dict(selectionChecks=selection_checks(worker), cases=[], weightsChanged=False,
                  scope="Scoped algorithm parity on archived unchanged 1.0.1 models; independent of the trained production candidate.")
    for source in ("zh", "en"):
        target = "en" if source == "zh" else "zh"
        package = ENGINE / "models" / (source + "_" + target)
        tokenizer = sentencepiece.SentencePieceProcessor(model_file=str(package / "sentencepiece.model"))
        raw = ctranslate2.Translator(str(package / "model"), device="cpu", compute_type="int8", intra_threads=4)
        vocabulary = json.loads((package / "model" / "shared_vocabulary.json").read_text(encoding="utf-8"))
        tuned = worker.QualityTranslator(raw, tokenizer, vocabulary, target)
        report[source + "ModelSha256"] = hashlib.sha256((package / "model" / "model.bin").read_bytes()).hexdigest()
        with zipfile.ZipFile(OUTPUT.parent / "release" / "LightTranslate-1.0.1-win-x64.zip") as archive:
            with archive.open("LightTranslate/engine/models/" + source + "_" + target + "/model/model.bin") as previous:
                assert hashlib.file_digest(previous, "sha256").hexdigest() == report[source + "ModelSha256"]
        for split, language, text, expected in CASES:
            if source != language:
                continue
            profile = tuned.profile(tokenizer.encode(text, out_type=str))
            assert profile == expected, (text, profile, expected)
            start = time.perf_counter()
            before = baseline.translate_paragraph(text, tokenizer, raw, target, "")
            baseline_seconds = time.perf_counter() - start
            start = time.perf_counter()
            try:
                after = worker.translate_paragraph(text, tokenizer, tuned, target, "")
                error = None
            except worker.WorkerError as exception:
                after, error = None, str(exception)
            passed = error is None
            monetary = list(worker.monetary_spans(text, target, list(worker.protected_spans(text))))
            if expected == "default":
                passed &= bool(monetary) or after == before
            elif after is not None:
                passed &= bool(tuned.acceptable(profile, tokenizer.encode(after, out_type=str)))
                passed &= all(number in re.sub(r"(?<=\d),(?=\d{3}(?:\D|$))", "", after)
                              for number in re.findall(r"\d+(?:\.\d+)?", text))
            if monetary and after is not None:
                passed &= all(literal in after for _, _, literal in monetary)
            split = "regression" if split == "heldout" else split
            report["cases"].append(dict(split=split, source=source, text=text, profile=profile,
                before=before, after=after, error=error, passed=passed,
                baselineSeconds=round(baseline_seconds, 3), tunedSeconds=round(time.perf_counter() - start, 3)))
    report["passed"] = all(case["passed"] for case in report["cases"])
    (OUTPUT / "semantic-comparison.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    for case in report["cases"]:
        print(json.dumps({key: case[key] for key in ("split", "text", "before", "after", "passed")}, ensure_ascii=False))
    assert report["passed"], "Semantic fixtures failed; inspect saved comparison."
    print("PASS", len(report["cases"]), "authored comparisons and", report["selectionChecks"], "selection checks")


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
