"""Evaluate the isolated trained INT8 model without modifying the product model."""

import importlib.util
import json
from pathlib import Path
import sys
import re

import ctranslate2
import sentencepiece


ROOT = Path(r"A:\Agent\Codex\Translation\training")


class DualTokenizer:
    def __init__(self, checkpoint):
        self.source = sentencepiece.SentencePieceProcessor(model_file=str(checkpoint / "source.spm"))
        self.target = sentencepiece.SentencePieceProcessor(model_file=str(checkpoint / "target.spm"))

    def encode(self, text, out_type=str):
        return self.source.encode(text, out_type=out_type)

    def decode(self, tokens):
        return self.target.decode(tokens)


def main():
    training = json.loads((ROOT / "training-results.json").read_text(encoding="utf-8"))
    floating = json.loads((ROOT / "evaluation-results.json").read_text(encoding="utf-8"))
    acceptance_path = ROOT / "acceptance-float-results.json"
    if acceptance_path.is_file() and not any(row["split"] == "acceptance_semantic_test" for row in floating["cases"]):
        floating["cases"].extend(json.loads(acceptance_path.read_text(encoding="utf-8")))
    candidate = Path(training["int8Candidate"])
    tokenizer = DualTokenizer(Path(training["selectedCheckpoint"]))
    translator = ctranslate2.Translator(str(candidate), device="cpu", compute_type="int8", intra_threads=4)
    spec = importlib.util.spec_from_file_location("worker", Path(__file__).resolve().parents[1] / "scripts" / "translation-worker.py")
    worker = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(worker)
    report = dict(candidate=str(candidate), cases=[], scope="Actual CPU INT8 candidate, with existing sentence/literal handling and no scoped lexical rules.")
    for row in floating["cases"]:
        try:
            translated = worker.translate_paragraph(row["zh"], tokenizer, translator, "en", "")
            error = None
        except worker.WorkerError as exception:
            translated, error = None, str(exception)
        report["cases"].append(dict(row, int8=translated, error=error))
    protected = []
    for text, literal in (("请发送邮件至 support@example.com，不要修改地址。", "support@example.com"),
                          ("打开 https://example.com/docs?q=hello，并发送邮件至 support@example.com。", "https://example.com/docs?q=hello"),
                          ("请运行 `python -V` 查看版本。", "`python -V`"),
                          (r"请保存到 C:\Users\Public\notes.txt，不要修改路径。", r"C:\Users\Public\notes.txt")):
        try:
            translated = worker.translate_paragraph(text, tokenizer, translator, "en", "")
            protected.append(dict(text=text, output=translated, passed=literal in translated))
        except worker.WorkerError as exception:
            protected.append(dict(text=text, error=str(exception), passed=False))
    report["protectedFixtures"] = protected
    acceptance = [row["int8"].lower() if row["int8"] else "" for row in report["cases"] if row["split"] == "acceptance_semantic_test"]
    if acceptance:
        optional = r"no need|not need|not necessary|not required|don't need"
        checks = [bool(re.search(optional, acceptance[0])) and "editor" in acceptance[0] and "computer" in acceptance[0],
                  bool(re.search(r"exit.*browser.*then.*turn off.*computer", acceptance[1])),
                  bool(re.search(optional, acceptance[2])) and "power" in acceptance[2] and "application" in acceptance[2],
                  "must remain connected" in acceptance[3] and bool(re.search(r"application can (close|be closed)", acceptance[3])),
                  bool(re.search(optional, acceptance[4])) and "backup" in acceptance[4] and "must save" in acceptance[4],
                  bool(re.search(r"save.*close.*document.*turn off.*computer", acceptance[5])),
                  "925" in acceptance[6] and "yuan" in acceptance[6] and "tax" in acceptance[6] and "$" not in acceptance[6],
                  "installation" in acceptance[7] and bool(re.search(r"(not|don't).*turn off", acceptance[7]))]
        report["acceptanceChecks"] = checks
        greetings = [row["int8"] or "" for row in report["cases"]
                     if row["split"] == "short_semantic_test" and row["zh"] in ("你好世界", "你好世界。")]
        report["greetingChecks"] = [bool(re.search(r"hello[, ]+world", text.lower())) for text in greetings]
        report["accepted"] = (all(checks) and len(greetings) == 2 and all(report["greetingChecks"])
                              and all(row["error"] is None for row in report["cases"])
                              and all(row["passed"] for row in protected))
    (ROOT / "int8-evaluation-results.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    if acceptance:
        assert report["accepted"], "Candidate semantic/integrity acceptance failed."
    print("Evaluated", len(report["cases"]), "real INT8 outputs; identifier fixtures:", sum(row["passed"] for row in protected), "/", len(protected))
    for row in report["cases"]:
        if row["split"] == "acceptance_semantic_test":
            print(json.dumps({key: row[key] for key in ("zh", "current", "trained", "int8", "error")}, ensure_ascii=False))


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
