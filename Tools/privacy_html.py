"""docs/privacy-policy.md → docs/privacy-policy.html (스토어에 넣을 공개 URL 용 한 장짜리 페이지).

  python Tools/privacy_html.py

md 의 제목·표·목록·문단·굵게·링크만 쓴다 (이 문서에 필요한 만큼만 변환).
"""
import html
import os
import re

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "docs", "privacy-policy.md")
DST = os.path.join(ROOT, "docs", "privacy-policy.html")


def inline(text):
    t = html.escape(text, quote=False)
    t = re.sub(r"\*\*(.+?)\*\*", r"<strong>\1</strong>", t)
    t = re.sub(r"`(.+?)`", r"<code>\1</code>", t)
    t = re.sub(r"(https?://[^\s<)]+)", r'<a href="\1">\1</a>', t)
    return t


def convert(md):
    out, para, table, items = [], [], [], []

    def flush():
        if para:
            out.append("<p>" + "<br>".join(inline(p) for p in para) + "</p>")
            para.clear()
        if items:
            out.append("<ul>" + "".join("<li>" + inline(i) + "</li>" for i in items) + "</ul>")
            items.clear()
        if table:
            rows = [r for r in table if not re.match(r"^\|[\s\-|]+\|$", r)]
            cells = [[c.strip() for c in r.strip("|").split("|")] for r in rows]
            head = "".join("<th>" + inline(c) + "</th>" for c in cells[0])
            body = "".join("<tr>" + "".join("<td>" + inline(c) + "</td>" for c in r) + "</tr>" for r in cells[1:])
            out.append("<table><thead><tr>" + head + "</tr></thead><tbody>" + body + "</tbody></table>")
            table.clear()

    for line in md.splitlines():
        s = line.rstrip()
        if not s:
            flush()
            continue
        if s == "---":
            flush()
            out.append("<hr>")
        elif s.startswith("#"):
            flush()
            level = len(s) - len(s.lstrip("#"))
            out.append("<h%d>%s</h%d>" % (level, inline(s[level:].strip()), level))
        elif s.startswith("|"):
            if para or items:
                flush()
            table.append(s)
        elif s.startswith("- "):
            if para or table:
                flush()
            items.append(s[2:])
        else:
            if items or table:
                flush()
            para.append(s)
    flush()
    return "\n".join(out)


PAGE = """<!doctype html>
<html lang="ko">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>디펜스 배틀 개인정보처리방침 / Defense Battle Privacy Policy</title>
<style>
  body {{ font-family: -apple-system, "Segoe UI", "Malgun Gothic", sans-serif; max-width: 860px; margin: 0 auto; padding: 24px 16px 64px; line-height: 1.65; color: #222; background: #fff; }}
  h1 {{ font-size: 1.6em; }} h2 {{ margin-top: 2em; border-bottom: 2px solid #eee; padding-bottom: .2em; }} h3 {{ margin-top: 1.6em; }}
  table {{ border-collapse: collapse; width: 100%; font-size: .93em; display: block; overflow-x: auto; }}
  th, td {{ border: 1px solid #ddd; padding: 6px 8px; text-align: left; vertical-align: top; }}
  th {{ background: #f6f6f6; }} hr {{ border: 0; border-top: 1px solid #ddd; margin: 2em 0; }}
  code {{ background: #f3f3f3; padding: 0 4px; border-radius: 3px; }} a {{ color: #0b63c5; word-break: break-all; }}
</style>
</head>
<body>
{body}
</body>
</html>
"""


def main():
    with open(SRC, encoding="utf-8") as f:
        body = convert(f.read())
    with open(DST, "w", encoding="utf-8", newline="\n") as f:
        f.write(PAGE.format(body=body))
    print("ok")


if __name__ == "__main__":
    main()
