"""Prepare attributed public replay data and project-authored correction pairs."""

import ast
import hashlib
import json
from pathlib import Path
import random
import re
import unicodedata
import zipfile


ROOT = Path(r"A:\Agent\Codex\Translation\training\data")
PROJECT = Path(__file__).resolve().parents[1]


def normalize(text):
    return re.sub(r"[\W\d_]+", "", unicodedata.normalize("NFKC", text).lower())


def excluded_fixtures(include_training_tests=False):
    excluded = set()
    for filename in ("ModelTuningVerification.py", "TranslationQualityComparison.py"):
        tree = ast.parse((PROJECT / "tests" / filename).read_text(encoding="utf-8"))
        for node in ast.walk(tree):
            if isinstance(node, ast.Constant) and isinstance(node.value, str):
                excluded.add(normalize(node.value))
    if include_training_tests:
        for filename in ("ModelTrainingContrasts.json", "ModelTrainingFinalContrasts.json", "ModelTrainingAcceptanceContrasts.json", "ModelTrainingShortTextContrasts.json"):
            for pair in json.loads((PROJECT / "tests" / filename).read_text(encoding="utf-8")):
                excluded.update(normalize(pair[language]) for language in ("zh", "en"))
        for pair in evaluation_pairs():
            excluded.update(normalize(pair[language]) for language in ("zh", "en"))
    return excluded


def public_pairs():
    archive = ROOT / "Tatoeba-v2023-04-12-cmn-en.zip"
    with zipfile.ZipFile(archive) as package:
        chinese = package.read("Tatoeba.cmn-en.cmn").decode("utf-8").splitlines()
        english = package.read("Tatoeba.cmn-en.en").decode("utf-8").splitlines()
        for filename in ("README", "LICENSE"):
            (ROOT / ("Tatoeba-" + filename)).write_bytes(package.read(filename))
    assert len(chinese) == len(english)
    excluded, seen_zh, seen_en, pairs = excluded_fixtures(), set(), set(), []
    for row, (zh, en) in enumerate(zip(chinese, english), 1):
        zh, en = zh.strip(), en.strip()
        a, b = normalize(zh), normalize(en)
        if not 4 <= len(zh) <= 80 or not 12 <= len(en) <= 180:
            continue
        if not re.search(r"[\u4e00-\u9fff]", zh) or not re.search(r"[A-Za-z]", en):
            continue
        if a in seen_zh or b in seen_en or a in excluded or b in excluded:
            continue
        seen_zh.add(a)
        seen_en.add(b)
        pairs.append(dict(zh=zh, en=en, family="public-" + str(row), origin="Tatoeba-v2023-04-12",
                          corpusRow=row, license="CC-BY-2.0-FR"))
    random.Random(20261005).shuffle(pairs)
    return pairs[:1500], hashlib.sha256(archive.read_bytes()).hexdigest()


def domain_pairs():
    pairs = []
    for cn, en in (("电脑", "computer"), ("笔记本电脑", "laptop"), ("设备", "device"), ("电源", "power")):
        for phase_cn, phase_en in (("安装", "installation"), ("升级", "upgrade"), ("备份", "backup"), ("复制", "copying")):
            pairs.extend([
                (f"{phase_cn}期间，不要关闭{cn}。", f"Do not turn off the {en} during {phase_en}.", "power-during"),
                (f"{phase_cn}结束后，可以关闭{cn}。", f"You can turn off the {en} after {phase_en} finishes.", "power-after"),
                (f"{phase_cn}尚未完成，不能关闭{cn}。", f"The {phase_en} is not finished, so do not turn off the {en}.", "power-unfinished"),
            ])
    for object_cn, object_en in (("文件", "file"), ("窗口", "window"), ("文档", "document"), ("应用程序", "application"),
                                 ("浏览器", "browser"), ("编辑器", "editor"), ("标签页", "tab"), ("对话框", "dialog")):
        pairs.extend([
            (f"请关闭这个{object_cn}。", f"Please close this {object_en}.", "close-object"),
            (f"请不要关闭这个{object_cn}。", f"Please do not close this {object_en}.", "do-not-close-object"),
            (f"关闭{object_cn}之前，请保存所有修改。", f"Save all changes before closing the {object_en}.", "save-before-close"),
            (f"完成工作之后，可以关闭{object_cn}。", f"You can close the {object_en} after finishing your work.", "close-after-work"),
            (f"保存成功之前，不能关闭{object_cn}。", f"Do not close the {object_en} before saving succeeds.", "close-until-saved"),
        ])
    for amount in (15, 49, 120, 560, 1800):
        for currency_cn, currency_en in (("元人民币", "Chinese yuan"), ("元", "yuan"), ("美元", "US dollars"), ("欧元", "euros")):
            pairs.extend([
                (f"商品价格是{amount}{currency_cn}，不含税。", f"The price is {amount} {currency_en}, excluding tax.", "price-excludes-tax"),
                (f"商品价格是{amount}{currency_cn}，已经包含税费。", f"The price is {amount} {currency_en}, including tax.", "price-includes-tax"),
                (f"这项服务收费{amount}{currency_cn}。", f"This service costs {amount} {currency_en}.", "service-currency"),
                (f"报价总额为{amount}{currency_cn}，税费另行支付。", f"The total quote is {amount} {currency_en}. Taxes are paid separately.", "quote-currency"),
            ])
    for tax_cn, tax_en in (("销售税", "sales tax"), ("增值税", "VAT"), ("进口税", "import duties"), ("消费税", "excise tax")):
        pairs.extend([
            (f"标示的金额不包含{tax_cn}。", f"The displayed amount does not include {tax_en}.", "display-excludes-tax"),
            (f"这些商品免征{tax_cn}。", f"These goods are exempt from {tax_en}.", "goods-tax-exemption"),
        ])
    for task_cn, task_en in (("数据传输", "data transfer"), ("系统更新", "system update"), ("文件复制", "file copying"), ("驱动安装", "driver installation")):
        pairs.extend([
            (f"{task_cn}完成前，请不要切断电源。", f"Do not disconnect the power supply before the {task_en} is complete.", "disconnect-power-before"),
            (f"{task_cn}完成后，可以断开设备的电源。", f"You can disconnect the device's power supply after the {task_en} is complete.", "disconnect-power-after"),
            (f"{task_cn}正在进行，请保持设备通电。", f"The {task_en} is in progress. Please keep the device powered on.", "keep-powered-on"),
        ])
    actions = (("关闭电脑", "turn off the computer"), ("关闭设备", "turn off the device"),
               ("关闭窗口", "close the window"), ("退出浏览器", "exit the browser"),
               ("保存文件", "save the file"), ("关闭编辑器", "close the editor"),
               ("重新启动电脑", "restart the computer"), ("删除文件", "delete the file"))
    for cn, en in actions:
        pairs.extend([
            (f"你不必{cn}。", f"You do not need to {en}.", "not-necessary"),
            (f"你必须{cn}。", f"You must {en}.", "necessary"),
            (f"禁止{cn}。", f"Do not {en}.", "forbidden"),
            (f"你可以{cn}，但不一定要这样做。", f"You can {en}, but you do not have to.", "optional"),
            (f"完成工作之后，无需{cn}。", f"There is no need to {en} after finishing your work.", "no-need-after"),
            (f"完成工作之前，不能{cn}。", f"Do not {en} before finishing your work.", "forbidden-before"),
        ])
    pairs.extend([
        ("退出应用程序之后，不必关闭电脑。", "You do not need to turn off the computer after exiting the application.", "optional-power-after-software"),
        ("关闭窗口后无需关机。", "There is no need to shut down after closing the window.", "optional-power-after-software"),
        ("可以关闭编辑器，但不要关机。", "You can close the editor, but do not shut down the computer.", "forbidden-power-after-software"),
        ("即使安装完成了，也不必断开电源。", "Even if installation is complete, you do not need to disconnect the power supply.", "optional-disconnection"),
        ("不要让这台设备断电。", "Do not disconnect the power supply of this device.", "power-loss"),
        ("需要保持设备通电，不能拔掉电源。", "Keep the device powered on. Do not unplug its power supply.", "power-loss"),
    ])
    software = (("关闭浏览器", "close the browser"), ("关闭编辑器", "close the editor"),
                ("关闭文本编辑器", "close the text editor"), ("关闭脚本编辑器", "close the script editor"),
                ("退出应用程序", "exit the application"), ("关闭文档", "close the document"))
    hardware = (("关闭电脑", "turn off the computer"), ("关闭笔记本电脑", "turn off the laptop"),
                ("断开电源", "disconnect the power supply"))
    for a_cn, a_en in software:
        for b_cn, b_en in hardware:
            pairs.extend([
                (f"完成工作后，可以{a_cn}，不必{b_cn}。", f"You can {a_en} after finishing your work. You do not need to {b_en}.", "mixed-optional"),
                (f"你必须{a_cn}，但是无需{b_cn}。", f"You must {a_en}, but you do not need to {b_en}.", "mixed-required-optional"),
                (f"可以{a_cn}，但是禁止{b_cn}。", f"You can {a_en}, but do not {b_en}.", "mixed-prohibition"),
                (f"先{a_cn}，再{b_cn}。", f"First {a_en}. Then {b_en}.", "software-then-hardware"),
                (f"先{b_cn}，再{a_cn}。", f"First {b_en}. Then {a_en}.", "hardware-then-software"),
                (f"{a_cn}之前，不要{b_cn}。", f"Do not {b_en} before you {a_en}.", "hardware-not-before-software"),
            ])
    excluded = excluded_fixtures(include_training_tests=True)
    return [dict(zh=zh, en=en, family=family, origin="project-authored-template", license="MIT")
            for zh, en, family in pairs if normalize(zh) not in excluded and normalize(en) not in excluded]


def evaluation_pairs():
    examples = [
        ("更新还在进行中，请保持电脑开启。", "The update is still in progress. Please keep the computer on.", "power"),
        ("完成数据传输之前，请勿切断设备电源。", "Do not disconnect the device's power before the data transfer is complete.", "power"),
        ("等进度达到100%后再关机。", "Wait until progress reaches 100% before shutting down.", "power"),
        ("明天重启电脑前，先保存今天的工作。", "Save today's work before restarting the computer tomorrow.", "power"),
        ("正在下载，不要退出浏览器。", "The download is in progress. Do not exit the browser.", "close"),
        ("可以关闭这个标签页，但不能关闭整台电脑。", "You can close this tab, but you must not turn off the whole computer.", "close"),
        ("保存完成之后才关闭编辑器。", "Close the editor only after saving is complete.", "close"),
        ("这张发票的税前金额为875元人民币。", "The pre-tax amount on this invoice is 875 Chinese yuan.", "currency"),
        ("维修总共需要230元人民币。", "The repair costs 230 Chinese yuan in total.", "currency"),
        ("押金是75欧元，租金另付。", "The deposit is 75 euros. The rent is paid separately.", "currency"),
        ("每份许可证收取38美元。", "Each license costs 38 US dollars.", "currency"),
        ("报价为760元，税费需要额外支付。", "The quote is 760 yuan. Taxes must be paid separately.", "tax"),
        ("运费已经计入总价，销售税尚未计入。", "Shipping is included in the total price, but sales tax is not included yet.", "tax"),
        ("该费用不是免税，只是尚未加税。", "This fee is not tax-exempt; tax has simply not been added yet.", "tax"),
        ("这里出售的商品可以免税购买。", "The goods sold here can be purchased tax-free.", "tax"),
        ("软件免费，但安装服务需要付费。", "The software is free, but the installation service costs money.", "tax"),
        ("我没有删除备份，只是改变了名称。", "I did not delete the backup; I only changed its name.", "negation"),
        ("没有密码也能使用本地翻译功能。", "You can use the local translation feature without a password.", "negation"),
        ("失败原因是磁盘空间不足，并非网络断开。", "The failure was caused by insufficient disk space, not a disconnected network.", "negation"),
        ("连接虽然已断开，离线识别仍然可用。", "Although the connection is disconnected, offline recognition is still available.", "negation"),
    ]
    return [dict(zh=zh, en=en, family="locked-" + str(index), origin="project-authored-reference",
                 category=category, license="MIT") for index, (zh, en, category) in enumerate(examples)]


def short_pairs(used):
    excluded = excluded_fixtures(include_training_tests=True)
    excluded.update(normalize(pair[language]) for pair in used for language in ("zh", "en"))
    with zipfile.ZipFile(ROOT / "Tatoeba-v2023-04-12-cmn-en.zip") as package:
        zh = package.read("Tatoeba.cmn-en.cmn").decode("utf-8").splitlines()
        en = package.read("Tatoeba.cmn-en.en").decode("utf-8").splitlines()
    pairs = []
    for row, (a, b) in enumerate(zip(zh, en), 1):
        a, b = a.strip(), b.strip()
        if not 2 <= len(a) <= 8 or not 2 <= len(b) <= 70:
            continue
        if not re.search(r"[\u4e00-\u9fff]", a) or not re.search(r"[A-Za-z]", b):
            continue
        keys = {normalize(a), normalize(b)}
        if keys.intersection(excluded):
            continue
        excluded.update(keys)
        pairs.append(dict(zh=a, en=b, family="short-public-" + str(row), corpusRow=row,
                          origin="Tatoeba-v2023-04-12", license="CC-BY-2.0-FR"))
    random.Random(20261006).shuffle(pairs)
    return pairs[:650]


def unpunctuated_pairs(pairs):
    augmented = []
    for pair in pairs:
        text = pair["zh"].rstrip("。.!！?？")
        if text != pair["zh"]:
            augmented.append(dict(pair, zh=text, augmentation="terminal-punctuation-removal"))
    return augmented


def main():
    generic, archive_hash = public_pairs()
    domain = domain_pairs()
    splits = dict(train=generic[:1000] + domain * 3, development=generic[1000:1100],
                  test=generic[1100:1200], semantic_test=evaluation_pairs(), fresh_test=generic[1200:1300])
    contrasts = json.loads((PROJECT / "tests" / "ModelTrainingContrasts.json").read_text(encoding="utf-8"))
    splits["fresh_semantic_test"] = [dict(pair, family="fresh-" + str(index), origin="project-authored-reference", license="MIT")
                                     for index, pair in enumerate(contrasts)]
    final = json.loads((PROJECT / "tests" / "ModelTrainingFinalContrasts.json").read_text(encoding="utf-8"))
    splits["final_semantic_test"] = [dict(pair, family="final-" + str(index), origin="project-authored-reference", license="MIT")
                                     for index, pair in enumerate(final)]
    splits["domain_development"] = splits["semantic_test"] + splits["fresh_semantic_test"] + splits["final_semantic_test"]
    short = short_pairs([pair for group in splits.values() for pair in group])
    splits["short_development"] = short[500:600]
    splits["short_test"] = short[600:650]
    splits["short_semantic_test"] = json.loads((PROJECT / "tests" / "ModelTrainingShortTextContrasts.json").read_text(encoding="utf-8"))
    splits["train"] += short[:500] + unpunctuated_pairs(generic[:1000] + domain + short[:500])
    groups = [set(normalize(pair[language]) for pair in pairs for language in ("zh", "en"))
              for name, pairs in splits.items() if name != "domain_development"]
    for index, group in enumerate(groups):
        assert all(not group.intersection(other) for other in groups[index + 1:]), "Cross-split overlap."
    manifest = dict(seed=20261005, shortSeed=20261006, shortReplayPairs=500, terminalPunctuationAugmentation=True, corpusSha256=archive_hash, domainUniquePairs=len(domain),
                    domainRepeats=3, excludedProductionFixtures=True, splits={},
                    publicAttribution="Tatoeba contributors; OPUS compilation by Jorg Tiedemann. Original README/LICENSE retained.",
                    limitation="Public replay/test may have appeared in base-model pretraining; this is not an unseen external benchmark.")
    for name, pairs in splits.items():
        path = ROOT / (name + ".jsonl")
        path.write_text("".join(json.dumps(pair, ensure_ascii=False) + "\n" for pair in pairs), encoding="utf-8")
        manifest["splits"][name] = dict(count=len(pairs), sha256=hashlib.sha256(path.read_bytes()).hexdigest())
    (ROOT / "data-manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(json.dumps(manifest, indent=2))


if __name__ == "__main__":
    main()
