"""Compare actual unchanged models on currency, desktop-save and semantic controls."""

import importlib.util
import json
from pathlib import Path
import re
import time


ROOT = Path(r"A:\Agent\Codex\Translation")
ENGINE = ROOT / "app" / "engine"
PROJECT = Path(__file__).resolve().parents[1]
CASES = [
    ("development", "本次报价为250元，不包含税费。", ["250 yuan"], [r"\$250", "free of tax"], False),
    ("development", "这项服务收费为500元，未包含税费。", ["500 yuan"], [r"\$500"], False),
    ("development", "Please turn off the computer after saving your work.", ["保存", "后"], ["节省", "节约"], False),
    ("acceptance", "押金为650元，可以全额退还。", ["650 yuan"], [r"\$650"], False),
    ("acceptance", "总费用为人民币1,280.50元，运费另算。", ["CNY 1,280.50"], [r"\$"], False),
    ("acceptance", "每件售价42.75美元。", ["USD 42.75"], [], False),
    ("acceptance", "预算是75欧元，不得超支。", ["EUR 75"], [], False),
    ("acceptance", "余额为-12.50元。", ["-12.50 yuan"], [r"\$"], False),
    ("acceptance", "退款为900日元。", ["JPY 900"], [], False),
    ("acceptance", "手续费为35港元。", ["HKD 35"], [], False),
    ("acceptance", "USD 43.20", ["43.20美元"], [], False),
    ("acceptance", "EUR 61.90", ["61.90欧元"], [], False),
    ("acceptance", "RMB 780", ["人民币780元"], [], False),
    ("acceptance", "Save your work before restarting the computer.", ["保存", "前"], ["节省", "节约"], False),
    ("acceptance", "After saving your work, close the document.", ["保存", "后"], ["节省", "节约"], False),
    ("acceptance", "Do not exit without saving your work.", ["保存", "不"], ["节省", "节约"], False),
    ("acceptance", "Save all changes before closing the file.", ["保存", "前"], ["节省", "节约"], False),
    ("control", "Save energy when the computer is idle.", [], [], True),
    ("control", "Save your money for emergencies.", [], [], True),
    ("control", "The reusable template saves work.", [], [], True),
    ("control", "采购清单中有250元件，不是250美元。", ["250"], [], False),
    ("control", "请检查250个元件，不要关闭窗口。", ["250"], [], True),
    ("control", "Please do not delete the profile.", ["不"], [], True),
    ("control", "The temperature is 85°C and the voltage is 3.3V.", ["85", "3.3"], [], True),
    ("control", "质量为2.5kg，压力为120kPa。", ["2.5", "120"], [], True),
    ("control", "Version v6.8.2 supports Windows 11.", ["v6.8.2", "11"], [], True),
    ("control", "If saving fails, do not close the editor.", ["不"], [], True),
    ("control", "Do not close the window until the download finishes.", ["不", "前"], [], True),
    ("control", "只有完成备份后才能关闭计算机。", ["Only after", "backup"], [], True),
    ("control", "He did not say the service was free.", ["没有", "免费"], [], True),
    ("acceptance", "请发送邮件至 billing@example.com，并支付27美元。", ["billing@example.com", "USD 27"], [], False),
]


def load(path, name):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    module.__file__ = str(ENGINE / "translation-worker.py")
    return module


def main():
    current = load(PROJECT / "scripts" / "translation-worker.py", "current")
    previous = load(ROOT / "refinement" / "baseline-1.1.0" / "translation-worker.py", "previous")
    for text in ("EUR 19,99", "人民币10万元", "1,28元", "250元件", "CAD 45.00"):
        assert not list(current.monetary_spans(text, "en", [])), "Unsupported/ambiguous amount was partially rewritten."
    fixtures = ROOT / "refinement" / "semantic-fixtures-v2.json"
    if fixtures.exists():
        assert json.loads(fixtures.read_text(encoding="utf-8"))["cases"] == [list(case) for case in CASES], "Frozen semantic cases changed."
    else:
        fixtures.write_text(json.dumps(dict(cases=CASES, scope="Authored development, unseen validation and preservation controls; not general semantic accuracy."),
                           ensure_ascii=False, indent=2), encoding="utf-8")
    results = []
    for split, text, required, forbidden, unchanged in CASES:
        request = dict(text=text, source="auto", target="auto")
        baseline = previous.translate(request)["text"]
        start = time.perf_counter()
        try:
            output = current.translate(request)["text"]
            passed = all(value.lower() in output.lower() for value in required)
            passed &= not any(re.search(value, output, re.IGNORECASE) for value in forbidden)
            passed &= not unchanged or output == baseline
            error = None
        except current.WorkerError as exception:
            output, error, passed = None, str(exception), False
        results.append(dict(split=split, input=text, before=baseline, after=output, passed=passed,
                            required=required, forbidden=forbidden, unchangedControl=unchanged, error=error,
                            currentSeconds=time.perf_counter()-start))
    report = dict(passed=all(r["passed"] for r in results), count=len(results), cases=results,
                  scope="Exact recognized currency identity, scoped save wording and lexical/number/control checks, not full meaning certification.")
    (ROOT / "refinement" / "semantic-results.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    for result in results:
        if not result["passed"]:
            print("FAIL " + json.dumps(result, ensure_ascii=True))
    assert report["passed"], "Semantic refinement gate failed; inspect saved actual output."
    print("PASS semantic refinement " + str(len(results)) + " actual before/after cases")


if __name__ == "__main__":
    main()
