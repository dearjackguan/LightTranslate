"""Translate one JSON request using local CTranslate2 and SentencePiece files."""

import json
import os
from pathlib import Path
import re
import sys
from types import SimpleNamespace
import unicodedata


class WorkerError(Exception):
    pass


PROTECTED_TEXT = re.compile(
    r'`[^`\r\n]+`|"[A-Za-z]:\\[^"\r\n]+"|'
    r'(?:https?://|www\.)[^\s<>"`“”‘’，。！？；、]+|'
    r"[A-Za-z0-9.!#$%&'*+/=?^_`{|}~-]+@(?:[A-Za-z0-9-]+\.)+[A-Za-z]{2,63}|"
    r'[A-Za-z]:\\[^\s<>:"|?*`，。！？；、]+'
)


def protected_spans(text):
    """Recognize literal identifiers while leaving sentence punctuation outside."""
    for match in PROTECTED_TEXT.finditer(text):
        start, end = match.span()
        value = match.group()
        if not value.startswith(('`', '"')):
            while end > start and text[end - 1] in ".,;!?)]}":
                closing = text[end - 1]
                opening = {')': '(', ']': '[', '}': '{'}.get(closing)
                if opening and text[start:end].count(opening) >= text[start:end].count(closing):
                    break
                end -= 1
        if end > start:
            yield start, end


def detect_source(text):
    """Estimate the prose language after excluding immutable technical spans."""
    for start, end in reversed(list(protected_spans(text))):
        text = text[:start] + " " + text[end:]
    text = unicodedata.normalize("NFKC", text)
    chinese = len(re.findall(r"[\u3400-\u9fff]", text))
    english = len(re.findall(r"[A-Za-z]+(?:['’-][A-Za-z]+)*", text))
    if chinese:
        return "zh" if chinese >= 2 * english else "en"
    if english:
        return "en"
    if any(character.isalpha() for character in text):
        raise WorkerError("LANGUAGE_UNSUPPORTED")
    return None


AMOUNT = r"[+-]?(?:\d{1,3}(?:,\d{3})+|\d+)(?:\.\d+)?"
CURRENCIES = r"人民币|美元|美金|欧元|英镑|日元|港币|港元|USD|CNY|RMB|EUR|GBP|JPY|HKD"
MONEY = re.compile(r"(?<![A-Za-z0-9.])(?P<prefix>" + CURRENCIES + r")\s*(?P<first>" + AMOUNT
    + r")(?![\d.,A-Za-z]|万|亿|千|百)(?:元)?"
    + r"|(?<![A-Za-z0-9.])(?<!\d,)(?P<second>" + AMOUNT + r")\s*(?P<suffix>" + CURRENCIES
    + r"|元(?!件|素|组|年|旦|数据))", re.IGNORECASE)


def monetary_spans(text, target, protected):
    """Preserve explicitly written currency identity without inferring exchange rates."""
    aliases = {"人民币": "CNY", "RMB": "CNY", "美元": "USD", "美金": "USD", "欧元": "EUR",
               "英镑": "GBP", "日元": "JPY", "港币": "HKD", "港元": "HKD"}
    names = {"CNY": "人民币", "USD": "美元", "EUR": "欧元", "GBP": "英镑", "JPY": "日元", "HKD": "港元"}
    for match in MONEY.finditer(text):
        start, end = match.span()
        if any(start < stop and end > begin for begin, stop in protected):
            continue
        amount = match.group("first") or match.group("second")
        currency = (match.group("prefix") or match.group("suffix")).upper()
        code = aliases.get(currency, currency)
        if target == "en":
            literal = amount + " yuan" if code == "元" else code + " " + amount
        else:
            literal = names[code] + amount + "元" if code == "CNY" else amount + ("元" if code == "元" else names[code])
        yield start, end, literal


def split_sentences(text):
    """Keep titles, initials, decimals and closing quotes attached to sentences."""
    sentences, start = [], 0
    abbreviations = r"\b(?:Mr|Mrs|Ms|Dr|Prof|Sr|Jr|St|No|vs|etc|e\.g|i\.e|a\.m|p\.m)\.$"
    for match in re.finditer(r'''[.!?。！？]+["”’')\]]*''', text):
        end = match.end()
        punctuation = match.group()
        if punctuation.startswith("."):
            before = text[start:end].rstrip('"”’\')]}')
            if re.search(abbreviations, before, re.IGNORECASE) or re.search(r"\b(?:[A-Za-z]\.)+$", before):
                continue
        if end < len(text) and not text[end].isspace() and not re.search(r"[。！？]", punctuation):
            continue
        sentences.append(text[start:end].strip())
        start = end
    if text[start:].strip():
        sentences.append(text[start:].strip())
    return sentences


def token_chunks(tokens, limit=256):
    """Prefer clause boundaries, then whole words when a sentence is too long."""
    while len(tokens) > limit:
        candidates = range(limit, limit // 2, -1)
        end = next((i for i in candidates if tokens[i - 1].endswith(tuple(",;:，；：、"))), None)
        if end is None:
            end = next((i for i in candidates if tokens[i].startswith("▁")), limit)
        yield tokens[:end]
        tokens = tokens[end:]
    if tokens:
        yield tokens


class ModelTokenizer:
    """Use the model's declared source and target SentencePiece files."""

    def __init__(self, package, metadata, processor):
        source = package / metadata.get("source_tokenizer", "sentencepiece.model")
        target = package / metadata.get("target_tokenizer", source.name)
        if not source.is_file() or not target.is_file():
            raise WorkerError("MODEL_MISSING")
        self.source = processor(model_proto=source.read_bytes())
        self.target = self.source if source == target else processor(model_proto=target.read_bytes())

    def encode(self, text, out_type=str):
        return self.source.encode(text, out_type=out_type)

    def decode(self, tokens):
        return self.target.decode(tokens)

    def decode_source(self, tokens):
        return self.source.decode(tokens)


class QualityTranslator:
    """Apply scoped lexical constraints using the model's actual target vocabulary."""

    def __init__(self, translator, tokenizer, vocabulary, target):
        self.translator = translator
        self.tokenizer = tokenizer
        self.vocabulary = set(vocabulary)
        self.target = target

    def profile(self, tokens):
        text = getattr(self.tokenizer, "decode_source", self.tokenizer.decode)(tokens)
        if self.target == "zh":
            desktop_save = r"\b(?:save|saving|saved)\s+(?:your\s+work|(?:the|your|this|all)\s+(?:changes|files?|documents?))\b"
            return "desktop-save" if re.search(desktop_save, text, re.IGNORECASE) else "default"
        if self.target != "en":
            return "default"
        exclusion = r"(?:不含|不包含|不包括|未含|未包含|未包括)(?:\d+(?:\.\d+)?[%％]的?)?(?:增值|销售|消费|进口|出口|附加)?税(?:费|款)?(?!务)"
        if re.search(exclusion, text) and not re.search(r"免费|免税|自由", text):
            return "tax-exclusion"
        power = r"(?:关闭|关掉)(?:这台|该|你的|您的|所有|这些)?(?:电脑|计算机|电源)(?!视觉|的|上|里|内|游戏|管理|按钮|开关|模块|设置)"
        if re.search(power, text) and not re.search(r"窗口|文件|文档|页面|标签|对话框|应用|软件|程序|连接", text):
            return "power-action"
        return "default"

    def forbidden(self, profile):
        if profile == "desktop-save":
            return [[token] for token in ("节省", "节约", "▁节省", "▁节约") if token in self.vocabulary]
        words = ("free", "exempt", "untaxed") if profile == "tax-exclusion" else ("close", "closing", "closed")
        tokens = [boundary + form for word in words for form in (word, word.capitalize()) for boundary in ("", "▁")]
        forbidden = [[token] for token in tokens if token in self.vocabulary]
        if profile == "tax-exclusion":
            for first in ("▁not", "not", "▁Not"):
                for second in ("▁subject", "▁liable"):
                    if first in self.vocabulary and second in self.vocabulary:
                        forbidden.append([first, second])
        return forbidden

    def acceptable(self, profile, tokens):
        text = self.tokenizer.decode(tokens).replace("▁", " ").lower()
        if profile == "desktop-save":
            return "保存" in text and not re.search("节省|节约", text)
        if profile == "tax-exclusion":
            return bool(re.search(r"\bnot\s+(?:include|contain)|\b(?:does|do|did)n['’]t\s+(?:include|contain)|\bexclud|\bexclusive\s+of|\bwithout\b|\bbefore\s+tax|\bpre[- ]tax|\btax[- ]exclusive|\bnon[- ]inclusive", text))
        return not re.search(r"\bclos(?:e|ed|ing)\b", text)

    def decode_group(self, chunks, profile, options):
        options = dict(options)
        if profile == "default":
            return self.translator.translate_batch(chunks, **options)
        candidates = 8 if profile == "desktop-save" else 4
        options.update(num_hypotheses=candidates, beam_size=max(candidates, options.get("beam_size", 4)),
                       return_scores=True, suppress_sequences=self.forbidden(profile))
        decoded = self.translator.translate_batch(chunks, **options)
        selected = []
        for result in decoded:
            index = next((i for i, tokens in enumerate(result.hypotheses) if self.acceptable(profile, tokens)), None)
            if index is None:
                raise WorkerError("QUALITY_CHECK_FAILED")
            selected.append(SimpleNamespace(hypotheses=[result.hypotheses[index]], scores=[result.scores[index]]))
        return selected

    def translate_batch(self, chunks, **options):
        profiles = [self.profile(tokens) for tokens in chunks]
        if all(profile == "default" for profile in profiles):
            return self.translator.translate_batch(chunks, **options)
        results = [None] * len(chunks)
        for profile in dict.fromkeys(profiles):
            indices = [i for i, value in enumerate(profiles) if value == profile]
            group_options = dict(options)
            if options.get("target_prefix"):
                group_options["target_prefix"] = [options["target_prefix"][i] for i in indices]
            decoded = self.decode_group([chunks[i] for i in indices], profile, group_options)
            for index, result in zip(indices, decoded):
                results[index] = result
        return results


def translate_plain(text, tokenizer, translator, target, prefix):
    sentences = split_sentences(text)
    chunks = []
    for sentence in sentences:
        tokens = tokenizer.encode(sentence, out_type=str)
        chunks.extend(token_chunks(tokens))
    if not chunks:
        return ""
    options = {"target_prefix": [[prefix]] * len(chunks)} if prefix else {}
    results = translator.translate_batch(
        chunks, beam_size=4, max_batch_size=8, max_input_length=0,
        max_decoding_length=512, replace_unknowns=True, length_penalty=0.2, **options
    )
    translated = []
    for result in results:
        tokens = result.hypotheses[0]
        if len(tokens) >= 512:
            raise WorkerError("OUTPUT_LIMIT")
        if prefix and tokens and tokens[0] == prefix:
            tokens = tokens[1:]
        translated.append(tokenizer.decode(tokens).replace("▁", " ").strip())
    return (" " if target == "en" else "").join(translated)


def translate_paragraph(text, tokenizer, translator, target, prefix):
    """Keep sentence context while restoring only verified literal placeholders."""
    protected = list(protected_spans(text))
    spans = [(start, end, text[start:end]) for start, end in protected]
    spans.extend(monetary_spans(text, target, protected))
    spans.sort(key=lambda item: item[0])
    if not spans:
        return translate_plain(text, tokenizer, translator, target, prefix)
    marker_prefix = "LTKEEP"
    while marker_prefix in text:
        marker_prefix += "X"
    pieces, literals, prose, start = [], {}, [], 0
    for index, (begin, end, literal) in enumerate(spans):
        marker = marker_prefix + str(index + 1)
        literals[marker] = literal
        prose.append(text[start:begin])
        pieces.extend((text[start:begin], " " + marker + " "))
        start = end
    prose.append(text[start:])
    if not re.search(r"[^\W\d_]", "".join(prose)):
        pieces.append(text[start:])
        return re.sub(" " + re.escape(marker_prefix) + r"\d+ ", lambda match: literals[match.group().strip()], "".join(pieces)).strip()
    pieces.append(text[start:])
    output = translate_plain("".join(pieces), tokenizer, translator, target, prefix)
    markers = re.compile(re.escape(marker_prefix) + r"\d+")
    actual = markers.findall(output)
    if len(actual) != len(literals) or set(actual) != set(literals):
        raise WorkerError("PROTECTED_TEXT_CHANGED")
    return markers.sub(lambda match: literals[match.group()], output)


def translate(request):
    text, source, target = request["text"], request["source"], request["target"]
    if not isinstance(text, str) or not text.strip() or len(text) > 5000:
        raise WorkerError("INVALID_INPUT")
    if source == "auto" and target == "auto":
        source = detect_source(text)
        if source is None:
            return {"ok": True, "text": text.strip(), "source": "none", "target": "none"}
        target = "en" if source == "zh" else "zh"
    if (source, target) not in (("en", "zh"), ("zh", "en")):
        raise WorkerError("INVALID_DIRECTION")
    package = Path(__file__).resolve().parent / "models" / (source + "_" + target)
    if not (package / "model" / "model.bin").is_file():
        raise WorkerError("MODEL_MISSING")
    try:
        import ctranslate2
        import sentencepiece
    except ImportError:
        raise WorkerError("DEPENDENCY_MISSING")
    metadata = json.loads((package / "metadata.json").read_text(encoding="utf-8"))
    prefix = metadata.get("target_prefix", "")
    tokenizer = ModelTokenizer(package, metadata, sentencepiece.SentencePieceProcessor)
    translator = ctranslate2.Translator(
        str(package / "model"), device="cpu", compute_type="int8",
        inter_threads=1, intra_threads=min(4, os.cpu_count() or 1)
    )
    vocabulary = json.loads((package / "model" / "shared_vocabulary.json").read_text(encoding="utf-8"))
    translator = QualityTranslator(translator, tokenizer, vocabulary, target)
    lines = [translate_paragraph(line, tokenizer, translator, target, prefix)
             if line.strip() else "" for line in text.splitlines()]
    translated = "\n".join(lines).strip()
    if not translated:
        raise WorkerError("EMPTY_RESULT")
    return {"ok": True, "text": translated, "source": source, "target": target}


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    try:
        response = translate(json.loads(sys.stdin.read()))
    except WorkerError as error:
        response = {"ok": False, "error": str(error)}
    except Exception:
        response = {"ok": False, "error": "ENGINE_FAILURE"}
    sys.stdout.write(json.dumps(response, ensure_ascii=False))


if __name__ == "__main__":
    main()
