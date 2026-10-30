"""Read the exported Unity vocabulary without importing audio/model libraries."""
from __future__ import annotations

from contextlib import contextmanager
from dataclasses import dataclass
import json
from pathlib import Path
import tempfile
import unicodedata


def normalize_phrase(value: str) -> str:
    """Same whitespace/punctuation rule as the Unity skill configuration."""
    return "".join(c for c in value if not c.isspace() and not unicodedata.category(c).startswith("P"))


def _label(value, context):
    if not isinstance(value, str):
        raise ValueError(f"{context} 必须是非空字符串")
    label = normalize_phrase(value)
    if not label or label == "all" or any(c in label for c in "@:#") or any(unicodedata.category(c).startswith("C") for c in label):
        raise ValueError(f"{context} 为空、包含控制字符或占用保留标记")
    return label


def _port(value, name):
    if type(value) is not int or not 1024 <= value <= 65535:
        raise ValueError(f"{name} 必须是 1024 至 65535 的整数")
    return value


@dataclass(frozen=True)
class Keyword:
    phrase: str
    tokens: str
    options: str = ""


@dataclass(frozen=True)
class VoiceConfiguration:
    keywords: tuple[Keyword, ...]
    unity_port: int
    service_port: int
    cancel_phrase: str | None
    source: str

    @property
    def phrases(self):
        return tuple(item.phrase for item in self.keywords)


def _check_keywords(items, cancel_phrase=None):
    if not items:
        raise ValueError("词表至少需要一个口令")
    for index, item in enumerate(items):
        if not item.tokens or any(token[0] in "@:#" for token in item.tokens.split()):
            raise ValueError(f"口令 {item.phrase} 缺少拼音 token 或包含词表控制标记")
        for other in items[:index]:
            if item.phrase in other.phrase or other.phrase in item.phrase:
                raise ValueError(f"口令重复或包含冲突：{other.phrase} / {item.phrase}")
    if cancel_phrase is not None and sum(item.phrase == cancel_phrase for item in items) != 1:
        raise ValueError("取消口令必须在导出词表中恰好出现一次")
    return tuple(items)


def read_keyword_file(path):
    """Parse sherpa labels plus optional per-keyword score/threshold modifiers."""
    items = []
    for number, raw in enumerate(Path(path).read_text(encoding="utf-8-sig").splitlines(), 1):
        line = raw.strip()
        if not line or line.startswith("#"):
            continue
        fields = line.split()
        labels = [field[1:] for field in fields if field.startswith("@")]
        if len(labels) != 1:
            raise ValueError(f"词表第 {number} 行必须有且仅有一个 @口令标签")
        tokens = [field for field in fields if field[0] not in "@:#"]
        options = [field for field in fields if field[0] in ":#"]
        for option in options:
            try:
                import math
                if not math.isfinite(float(option[1:])):
                    raise ValueError()
            except ValueError as error:
                raise ValueError(f"词表第 {number} 行包含无效分数/阈值：{option}") from error
        items.append(Keyword(_label(labels[0], f"词表第 {number} 行标签"), " ".join(tokens), " ".join(options)))
    return _check_keywords(items)


def load_configuration(root, *, config_path=None, keywords_path=None, unity_port=None, service_port=None):
    """Explicit CLI keywords override JSON vocabulary; ports still inherit then override.

    Only a missing *default* JSON falls back to keywords.txt. An explicitly named
    missing file or a malformed existing config fails visibly instead of using stale defaults.
    """
    root = Path(root)
    path = Path(config_path) if config_path is not None else root / "skill-voice-config.json"
    data = None
    cancel = None
    json_keywords = None
    if path.exists() or config_path is not None:
        data = json.loads(path.read_text(encoding="utf-8-sig"))
        if not isinstance(data, dict) or type(data.get("schema")) is not int or data["schema"] != 1:
            raise ValueError("语音配置 schema 必须为 1")
        cancel = _label(data.get("cancelPhrase"), "取消口令")
        raw_keywords = data.get("keywords")
        if not isinstance(raw_keywords, list):
            raise ValueError("语音配置 keywords 必须为数组")
        items = []
        for raw in raw_keywords:
            if not isinstance(raw, dict) or not isinstance(raw.get("tokens"), str):
                raise ValueError("每个口令必须包含 phrase 与 tokens 字符串")
            items.append(Keyword(_label(raw.get("phrase"), "口令标签"), " ".join(raw["tokens"].split())))
        json_keywords = _check_keywords(items, cancel)
        configured_unity = _port(data.get("unityPort"), "unityPort")
        configured_service = _port(data.get("servicePort"), "servicePort")
        if configured_unity == configured_service:
            raise ValueError("Unity 与语音服务端口不能相同")
    else:
        configured_unity, configured_service = 47831, 47832

    if keywords_path is not None:
        source = str(Path(keywords_path).resolve())
        keywords = read_keyword_file(keywords_path)
        # A standalone experiment can intentionally omit the cancellation keyword.
        cancel = cancel if cancel in (item.phrase for item in keywords) else None
    elif data is not None:
        source, keywords = str(path.resolve()), json_keywords
    else:
        source = str((root / "keywords.txt").resolve())
        keywords = read_keyword_file(source)
    target = _port(configured_unity if unity_port is None else unity_port, "Unity 接收端口")
    service = _port(configured_service if service_port is None else service_port, "语音服务端口")
    if target == service:
        raise ValueError("Unity 与语音服务端口不能相同")
    return VoiceConfiguration(keywords, target, service, cancel, source)


@contextmanager
def materialize_keywords(configuration):
    """Use a private temporary file; never overwrite the user's source vocabulary."""
    with tempfile.TemporaryDirectory(prefix="naruto-kws-") as directory:
        path = Path(directory) / "keywords.txt"
        lines = [f"{item.tokens} {item.options} @{item.phrase}".strip() for item in configuration.keywords]
        path.write_text("\n".join(lines) + "\n", encoding="utf-8")
        yield path
