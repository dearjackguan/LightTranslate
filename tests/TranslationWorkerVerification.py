"""Verify sentence and token boundaries without downloading or loading models."""

import importlib.util
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("worker", ROOT / "scripts" / "translation-worker.py")
worker = importlib.util.module_from_spec(spec)
spec.loader.exec_module(worker)


def main():
    cases = [
        ("Dr. Smith arrived. He left.", ["Dr. Smith arrived.", "He left."]),
        ("The U.S. team won. We celebrated.", ["The U.S. team won.", "We celebrated."]),
        ("Meet at 10:30 a.m. on Monday.", ["Meet at 10:30 a.m. on Monday."]),
        ("Mr. Jones saw Prof. Smith.", ["Mr. Jones saw Prof. Smith."]),
        ("J. Smith measured 3.14 volts. Done.", ["J. Smith measured 3.14 volts.", "Done."]),
        ("Version 2.5.1 works.", ["Version 2.5.1 works."]),
        ('He said, "Stop." Then left.', ['He said, "Stop."', "Then left."]),
        ("他说：“停止。”然后离开。", ["他说：“停止。”", "然后离开。"]),
        ("真的吗？！是的。", ["真的吗？！", "是的。"]),
        ("https://example.com/docs?q=x support@example.com", ["https://example.com/docs?q=x support@example.com"]),
        ("   First.  Second.   ", ["First.", "Second."]),
        ("", []),
    ]
    for text, expected in cases:
        assert worker.split_sentences(text) == expected, repr(text)
    tokens = ["▁word"] * 255 + ["▁electro", "magnetic", "▁field"] + ["▁word"] * 300
    chunks = list(worker.token_chunks(tokens))
    assert [t for chunk in chunks for t in chunk] == tokens, "Input tokens lost or reordered."
    assert all(len(chunk) <= 256 for chunk in chunks), "Token budget exceeded."
    assert all(chunk[0].startswith("▁") for chunk in chunks), "Word split in the middle."
    assert any(["▁electro", "magnetic"] == chunk[:2] for chunk in chunks)
    clauses = ["字"] * 190 + ["，"] + ["字"] * 100
    clause_chunks = list(worker.token_chunks(clauses))
    assert clause_chunks[0][-1] == "，" and sum(map(len, clause_chunks)) == len(clauses)
    report = dict(passed=16, scope="12 sentence cases plus lossless token, size, word and clause boundary checks.")
    output = Path(r"A:\Agent\Codex\Translation\accuracy\boundary-results.json")
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print("PASS sentence/token boundaries 16/16")


if __name__ == "__main__":
    main()
