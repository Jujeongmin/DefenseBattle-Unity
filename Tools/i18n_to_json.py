"""원본 번역 표(../DefenseBattle/game/src/i18n/*.ts)를 Unity 용 JSON 으로 바꾼다.

    py -3 Tools/i18n_to_json.py

출력: Assets/Resources/Loc/{en,ja,zh-Hans,zh-Hant}.json  (한국어 원문 → 번역)
원본 표의 알려진 오류도 여기서 바로잡는다 (아래 FIXES).
이식판에서 새로 생긴 문장은 Tools/i18n_extra.json 에 언어별로 넣는다.
"""
import io
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "..", "DefenseBattle", "game", "src", "i18n")
OUT = os.path.join(ROOT, "Assets", "Resources", "Loc")
EXTRA = os.path.join(ROOT, "Tools", "i18n_extra.json")
LANGS = {"en": "en", "ja": "ja", "zh-hans": "zh-Hans", "zh-hant": "zh-Hant"}

# 한 줄: "키": "값",   (JS 큰따옴표 문자열 - JSON 과 같은 이스케이프)
LINE = re.compile(r'^\s*("(?:[^"\\]|\\.)*")\s*:\s*("(?:[^"\\]|\\.)*")\s*,?\s*$')

# 원본 오류: 세 문장의 번역이 한 칸씩 밀려 있다 (모든 언어 같음) → 제자리로
ROTATED = ["30웨이브 클리어", "3배속은 광고 제거 패키지로 해금돼요", "3초마다 앞으로 순간이동"]


def parse(path):
    table = {}
    with io.open(path, encoding="utf-8") as f:
        for line in f:
            m = LINE.match(line)
            if m:
                table[json.loads(m.group(1))] = json.loads(m.group(2))
    return table


def fix(table):
    if all(k in table for k in ROTATED):
        a, b, c = (table[k] for k in ROTATED)
        # 지금: 30웨이브→(3배속 번역), 3배속→(3초마다 번역), 3초마다→(30웨이브 번역)
        table[ROTATED[0]], table[ROTATED[1]], table[ROTATED[2]] = c, a, b


def main():
    extra = {}
    if os.path.exists(EXTRA):
        with io.open(EXTRA, encoding="utf-8") as f:
            extra = json.load(f)
    os.makedirs(OUT, exist_ok=True)
    for src, lang in LANGS.items():
        table = parse(os.path.join(SRC, src + ".ts"))
        if not table:
            sys.exit("번역 표를 못 읽음: " + src)
        fix(table)
        table.update(extra.get(lang, {}))
        with io.open(os.path.join(OUT, lang + ".json"), "w", encoding="utf-8", newline="\n") as f:
            json.dump(table, f, ensure_ascii=False, indent=1, sort_keys=True)
        print(lang, len(table))


if __name__ == "__main__":
    main()
