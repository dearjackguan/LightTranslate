"""Verify local model identifier fidelity and rejected placeholder corruption."""

import importlib.util
import json
from pathlib import Path
from types import SimpleNamespace

import ctranslate2
import sentencepiece


ROOT = Path(__file__).resolve().parents[1]
ENGINE = Path(r"A:\Agent\Codex\Translation\app\engine")
REPORT = Path(r"A:\Agent\Codex\Translation\accuracy\integrity-results-2026-10-05.json")
spec = importlib.util.spec_from_file_location("worker", ROOT / "scripts" / "translation-worker.py")
worker = importlib.util.module_from_spec(spec)
spec.loader.exec_module(worker)

CASES = [
    ("en", "Open https://example.com/docs?q=hello and send feedback to support@example.com."),
    ("en", "Contact billing@example.com or support@example.org; do not send passwords."),
    ("en", "Save the file to C:\\Users\\Public\\notes.txt and run `python -V`."),
    ("en", "support@example.com"),
    ("en", 'Open "C:\\Program Files\\My App\\settings.json" without changing it.'),
    ("en", "Visit https://example.com/a_(b). Do not change the address."),
    ("en", "Open https://example.com/path?q=hello%20world&x=1#section."),
    ("en", "Send a copy to Support.Team+alerts@example.co.uk."),
    ("en", "Run `git status --short` and then `python -V`."),
    ("en", "Email support@example.com twice: support@example.com."),
    ("en", "Keep LTKEEP1 unchanged and email support@example.com."),
    ("zh", "请发送邮件至 support@example.com，不要修改地址。"),
    ("zh", "请打开 https://example.com/docs?q=hello 并复制链接。"),
    ("zh", "文件保存在 C:\\Users\\Public\\notes.txt，请勿删除。"),
    ("zh", "请执行 `python -V`，不要修改命令。"),
    ("zh", "发邮件到 billing@example.com 或 support@example.org，不要发送密码。"),
]


class EchoTokenizer:
    def encode(self, text, out_type):
        return [text]

    def decode(self, tokens):
        return "".join(tokens)


class CorruptTranslator:
    def __init__(self, output):
        self.output = output

    def translate_batch(self, chunks, **options):
        return [SimpleNamespace(hypotheses=[[self.output]])]


def verify_guards():
    for output in ("Marker missing", "LTKEEP1 LTKEEP1", "LTKEEP2", "LTKEEP1 LTKEEP20"):
        try:
            worker.translate_paragraph("Email support@example.com.", EchoTokenizer(), CorruptTranslator(output), "zh", "")
        except worker.WorkerError as error:
            assert str(error) == "PROTECTED_TEXT_CHANGED"
            continue
        raise AssertionError("Corrupt placeholder accepted: " + output)
    literal = "support@example.com; https://example.com/; `python -V`"
    assert worker.translate_paragraph(literal, None, None, "zh", "") == literal
    text = "Email LTKEEP1 and support@example.com."
    echo = CorruptTranslator("Email LTKEEP1 and LTKEEPX1.")
    assert worker.translate_paragraph(text, EchoTokenizer(), echo, "zh", "") == text
    return 6


def main():
    results = []
    for source in ("en", "zh"):
        target = "zh" if source == "en" else "en"
        package = ENGINE / "models" / (source + "_" + target)
        metadata = json.loads((package / "metadata.json").read_text(encoding="utf-8"))
        tokenizer = worker.ModelTokenizer(package, metadata, sentencepiece.SentencePieceProcessor)
        translator = ctranslate2.Translator(str(package / "model"), device="cpu", compute_type="int8", intra_threads=4)
        vocabulary = json.loads((package / "model" / "shared_vocabulary.json").read_text(encoding="utf-8"))
        translator = worker.QualityTranslator(translator, tokenizer, vocabulary, target)
        for case_source, text in CASES:
            if case_source != source:
                continue
            output = worker.translate_paragraph(text, tokenizer, translator, target, "")
            literals = [text[start:end] for start, end in worker.protected_spans(text)]
            assert literals, "Fixture has no protected identifier."
            for literal in set(literals):
                assert output.count(literal) == literals.count(literal), "Identifier changed or duplicated: " + literal
            results.append(dict(source=source, input=text, output=output, literals=literals, passed=True))
    mail = next(r["output"] for r in results if r["input"].startswith("请发送邮件至"))
    assert "send" in mail.lower() and "not" in mail.lower() and "maildir" not in mail.lower()
    guards = verify_guards()
    REPORT.write_text(json.dumps(dict(results=results, corruptionGuardsPassed=guards,
        scope="Exact preservation of recognized literal spans; semantic spot checks, not a general quality score."),
        ensure_ascii=False, indent=2), encoding="utf-8")
    print("PASS real bilingual identifier cases " + str(len(results)) + "/16; corruption/literal guards " + str(guards) + "/6")


if __name__ == "__main__":
    main()
