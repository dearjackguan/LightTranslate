"""Compare local decoding settings on a fixed, inspectable bilingual corpus."""

import importlib.util
import json
from pathlib import Path
import re
import sys
import time

import ctranslate2
import sentencepiece


ENGINE = Path(r"A:\Agent\Codex\Translation\app\engine")
OUTPUT = Path(r"A:\Agent\Codex\Translation\accuracy")
CASES = {
    "en": [
        "Hello world 123.",
        "Do not turn off the computer while the update is being installed.",
        "The file was not deleted; it was moved to the backup folder.",
        "Dr. Smith measured 3.14 volts at 10:30 a.m. on Monday.",
        "The U.S. team released version 2.5.1 yesterday.",
        "If the battery temperature exceeds 45 degrees Celsius, stop charging immediately.",
        "The server returned an error because the connection timed out, not because the password was incorrect.",
        "Please save the document before closing the window. Your changes will otherwise be lost.",
        "The monthly subscription costs $19.99, excluding tax.",
        "She put the book on the table because it was too heavy to carry.",
        "Open https://example.com/docs?q=hello and send feedback to support@example.com.",
        "The engineer reviewed the installation instructions, checked the power supply, inspected the cables, "
        "verified that the network connection was stable, and confirmed that the backup had completed "
        "before starting the update, because interrupting the installation could damage the existing "
        "configuration and prevent the device from restarting, so she asked every member of the team "
        "to keep the equipment connected until the progress indicator reached 100 percent and the "
        "system displayed a message confirming that all changes had been saved successfully.",
        "Before the maintenance team replaces the damaged cooling system in the main laboratory, "
        "the project manager must confirm that every sensor has been disconnected safely, that all "
        "experimental measurements have been copied to the encrypted backup drive, that the emergency "
        "power supply can support the remaining equipment for at least 30 minutes, that the technicians "
        "understand how to reconnect the temperature controllers without changing the existing calibration, "
        "and that a supervisor will remain available throughout the operation to check each connector "
        "and record every modification, because even a minor wiring error could produce unreliable "
        "results during the next experiment and make it difficult for researchers to distinguish a "
        "genuine change in the sample from an unexpected fluctuation caused by the replacement hardware, "
        "so nobody should restart the equipment until the complete inspection report has been reviewed "
        "and the supervisor has explicitly confirmed that no part of the original configuration was lost.",
        'He said, "Do not delete the backup." Then he closed the window.',
    ],
    "zh": [
        "你好，世界！",
        "更新安装期间，请勿关闭计算机。",
        "文件并没有被删除，而是被移动到了备份文件夹。",
        "史密斯医生在周一上午10:30测得电压为3.14伏。",
        "如果电池温度超过45摄氏度，请立即停止充电。",
        "服务器报错是因为连接超时，并不是因为密码错误。",
        "关闭窗口前请保存文档，否则修改会丢失。",
        "每月订阅费用为19.99美元，不含税。",
        "这项功能只在离线模式下可用，不需要注册账号。",
        "虽然网络已经断开，但是截图翻译仍然可以正常使用。",
        "打开 https://example.com/docs?q=hello，并发送邮件至 support@example.com。",
        "工程师首先检查安装说明、电源和连接线，确认网络连接稳定且备份已完成后才开始更新，"
        "因为安装过程中断可能损坏现有配置并导致设备无法重新启动，因此她要求团队所有成员"
        "在进度达到100%并且系统确认全部修改已成功保存之前，始终保持设备连接，不能关闭电源。",
        "维修人员更换实验室中损坏的冷却系统之前，项目负责人必须确认所有传感器已经安全断开，"
        "实验测量数据已经复制到加密备份硬盘，应急电源能够让其余设备至少继续运行30分钟，"
        "技术人员知道如何在不改变现有校准设置的情况下重新连接温度控制器，并且有监督人员"
        "在整个过程中检查每个连接器并记录每一项修改，因为即使很小的接线错误也可能导致"
        "下一次实验结果不可靠，使研究人员无法区分样品的真实变化与更换硬件引起的意外波动，"
        "因此，在完整检查报告经过审核且监督人员明确确认原始配置没有丢失之前，任何人都不能重新启动设备。",
        "他说：“不要删除备份。”然后关闭了窗口。",
    ],
}


def load_worker(path):
    spec = importlib.util.spec_from_file_location("quality_worker", path)
    worker = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(worker)
    return worker


class OptionsTranslator:
    def __init__(self, translator, overrides):
        self.translator, self.overrides = translator, overrides

    def translate_batch(self, chunks, **options):
        options.update(self.overrides)
        return self.translator.translate_batch(chunks, **options)


def compare(worker_path, report_name, variants):
    worker = load_worker(worker_path)
    report = dict(worker=str(worker_path), cases=[])
    for source, texts in CASES.items():
        target = "zh" if source == "en" else "en"
        package = ENGINE / "models" / (source + "_" + target)
        metadata = json.loads((package / "metadata.json").read_text(encoding="utf-8"))
        tokenizer = worker.ModelTokenizer(package, metadata, sentencepiece.SentencePieceProcessor) if hasattr(worker, "ModelTokenizer") else sentencepiece.SentencePieceProcessor(model_file=str(package / "sentencepiece.model"))
        translator = ctranslate2.Translator(str(package / "model"), device="cpu", compute_type="int8", intra_threads=4)
        if hasattr(worker, "QualityTranslator"):
            vocabulary = json.loads((package / "model" / "shared_vocabulary.json").read_text(encoding="utf-8"))
            translator = worker.QualityTranslator(translator, tokenizer, vocabulary, target)
        for name, overrides in variants.items():
            start = time.perf_counter()
            for index, text in enumerate(texts):
                result = worker.translate_paragraph(text, tokenizer, OptionsTranslator(translator, overrides), target, "")
                numbers = re.findall(r"\d+(?:\.\d+)*", text)
                report["cases"].append(dict(id=source + "-" + str(index + 1), variant=name,
                    source=text, output=result, missingNumbers=[n for n in numbers if n not in result]))
            print(name, source, "seconds=" + str(round(time.perf_counter() - start, 2)))
    if set(variants) == {"optimized"}:
        assert all(not c["missingNumbers"] for c in report["cases"]), "Fixture number missing."
        results = {c["id"]: c["output"] for c in report["cases"]}
        assert "美国" in results["en-5"] and "美国." not in results["en-5"]
        assert re.search(r"星期一|周一", results["en-4"]) and "3.14" in results["en-4"]
        assert re.search(r"不要|请勿|不能", results["en-2"]) and "没有" in results["en-3"]
        assert all("not" in results[key].lower() for key in ("zh-2", "zh-3", "zh-6", "zh-9"))
        assert "until" in results["zh-13"].lower() and "reviewed" in results["zh-13"].lower()
        report["fixtureChecksPassed"] = True
    (OUTPUT / report_name).write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    OUTPUT.mkdir(parents=True, exist_ok=True)
    if sys.argv[1] == "baseline":
        compare(OUTPUT / "baseline-worker.py", "decoding-comparison.json", {
            "baseline": {}, "length-0.6": {"length_penalty": 0.6},
            "length-1.0": {"length_penalty": 1.0}, "beam-6": {"beam_size": 6},
        })
    else:
        compare(Path(__file__).resolve().parents[1] / "scripts" / "translation-worker.py",
                "optimized-comparison.json", {"optimized": {}})
