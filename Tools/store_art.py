"""스토어·앱 아이콘 그림 만들기 (원본 ../DefenseBattle/launch/store 그림을 크기만 맞춘다).

  python Tools/store_art.py

- docs/store/icon-512.png               Play 스토어 아이콘 (512x512, 원본 썸네일 전체)
- docs/store/feature-graphic-1024x500.png  Play 그래픽 이미지 (1024x500, 원본 1024x578 에서 제목이 남게 잘라냄)
- Assets/Art/DefenseBattle/Icon/icon.png          런처 아이콘 (Legacy·Round, 원본 썸네일 1024)
- Assets/Art/DefenseBattle/Icon/icon_adaptive_bg.png   적응형 아이콘 배경 (제목 없이 가운데 캐릭터 부분 - 기기 마스크가 가장자리를 자른다)
- Assets/Art/DefenseBattle/Icon/icon_adaptive_fg.png   적응형 아이콘 전경 (투명 - 배경 그림 하나로 쓴다)
"""
import os
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "..", "DefenseBattle", "launch", "store")
STORE = os.path.join(ROOT, "docs", "store")
ICON = os.path.join(ROOT, "Assets", "Art", "DefenseBattle", "Icon")


def main():
    os.makedirs(STORE, exist_ok=True)
    os.makedirs(ICON, exist_ok=True)
    thumb = Image.open(os.path.join(SRC, "thumbnail.jpg")).convert("RGB")
    graphic = Image.open(os.path.join(SRC, "graphic_1024x578.png")).convert("RGB")

    thumb.resize((512, 512), Image.LANCZOS).save(os.path.join(STORE, "icon-512.png"))
    thumb.save(os.path.join(ICON, "icon.png"))

    # 그래픽 이미지: 위 제목(y 25~115)이 남도록 위에서 15px, 아래에서 63px 를 자른다
    graphic.crop((0, 15, 1024, 515)).save(os.path.join(STORE, "feature-graphic-1024x500.png"))

    # 적응형 배경: 제목 아래 캐릭터가 모인 정사각형 (도깨비·경고 고리·전사·마법사·궁수)
    thumb.crop((180, 190, 1004, 1014)).resize((432, 432), Image.LANCZOS).save(os.path.join(ICON, "icon_adaptive_bg.png"))
    Image.new("RGBA", (432, 432), (0, 0, 0, 0)).save(os.path.join(ICON, "icon_adaptive_fg.png"))
    print("ok")


if __name__ == "__main__":
    main()
