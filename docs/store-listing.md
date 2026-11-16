# Google Play 스토어 등록 문구 초안 - 디펜스 배틀

> Play Console > 스토어 등록정보에 붙여넣을 초안. 수치는 `Assets/Scripts/Core/Config` 기준 (밸런스를 바꾸면 여기도 고친다).
> 게임은 5개 언어(한국어·English·日本語·简体中文·繁體中文)를 지원하지만 이 문서는 한국어·영어만 있다. 나머지 3개 언어 문구는 아직 없음.

---

## 앱 이름 (30자 이내)

**한국어**: 디펜스 배틀: 실시간 디펜스 대전
**English**: Defense Battle: PvP Defense

## 짧은 설명 (80자 이내)

**한국어**:
소환은 운, 승리는 판단. 유닛을 합성해 몹을 막고, 몹 카드를 던져 상대 전장을 무너뜨리세요.

**English**:
Summon, merge, defend - then throw mob cards to break your rival's field in 1v1.

## 전체 설명 (4000자 이내)

**한국어**:

소환은 운, 승리는 판단.
버튼 한 번에 유닛이 랜덤으로 나오고, 같은 유닛 3개를 합치면 한 등급 위로 올라갑니다. 사각 링 길을 도는 몹이 100마리 쌓이기 전에 막아내세요.

■ 소환하고, 합치고, 배치하기
- 일반부터 태초까지 6등급. 같은 유닛·같은 등급 3개를 합치면 한 등급 위 유닛이 됩니다.
- 유닛은 자기 구역 앞만 지킵니다. 닿지 않는 길(빨간 구멍)이 보이면 구역을 바꿔 메우세요.
- 직업마다 스킬이 다릅니다. 다중 사격, 범위 폭발, 빙결, 보스 특화, 처형까지 덱 4자리를 어떻게 채울지가 전략입니다.

■ 실시간 1:1 대전
- 상대와 같은 웨이브를 동시에 막으면서, 몹 카드 16종 중 덱에 넣은 4장을 상대 전장으로 던집니다.
- 골드를 방어에 쓸지 공격에 쓸지, 판단이 승패를 가릅니다.
- 트로피를 모아 브론즈부터 마스터까지 6단계 아레나를 올라가고, 시즌 패스와 랭킹에 도전하세요.
- 상대가 바로 없으면 봇과 연습전을 할 수 있습니다.

■ 유닛 32종 수집과 성장
- 일반 8 · 희귀 10 · 영웅 8 · 전설 6, 총 32종.
- 상자와 뽑기로 카드를 모아 유닛 레벨을 올리세요.

■ 혼자서도 길게
- 캠페인: 초원의 관문, 어둠의 숲, 망자의 성채. 3챕터 × 10스테이지.
- 무한 모드: 50웨이브 끝까지 버텨 보세요.

■ 이런 분께
- 세로 화면, 한 손으로 플레이합니다.
- 한국어 · English · 日本語 · 简体中文 · 繁體中文 지원.
- 진행 상황은 Google Play 계정에 저장되어 기기를 바꿔도 이어서 할 수 있습니다.
- 인터넷 연결이 필요합니다.

**English**:

Summoning is luck. Winning is judgment.
Tap once to summon a random unit. Merge three of the same unit to raise it a grade. Mobs march around a square ring road - stop them before 100 pile up.

■ Summon, merge, place
- Six grades from Common to Eternal. Three of the same unit at the same grade merge into one unit a grade higher.
- Each unit only guards the stretch of road in front of its zone. See a gap marked in red? Swap zones to cover it.
- Every class has its own skill: multishot, area blasts, freezing, boss slaying, executions. How you fill your four deck slots is the strategy.

■ Real-time 1v1 battles
- Defend the same waves as your opponent at the same time, and throw the 4 mob cards in your deck (out of 16) onto their field.
- Spend gold on defense or on attack - that call decides the match.
- Earn trophies to climb six arenas from Bronze to Master, with a season pass and leaderboard.
- No opponent around? Practice against a bot.

■ Collect and level up 32 units
- 8 Common, 10 Rare, 8 Epic and 6 Legendary units.
- Collect cards from chests and draws to level your units.

■ Plenty to play solo
- Campaign: three chapters of 10 stages each.
- Endless mode: hold out through all 50 waves.

■ Good to know
- Portrait, one-handed play.
- Korean, English, Japanese, Simplified and Traditional Chinese.
- Progress is saved to your Google Play account, so you can switch devices.
- Requires an internet connection.

---

## 필요한 그래픽 자료 (직접 준비)

| 자료 | 규격 | 비고 |
|---|---|---|
| 앱 아이콘 | 512×512 PNG (32bit, 알파 없음) | 필수 |
| 피처 그래픽 | 1024×500 PNG/JPG | 필수. 원본 `../DefenseBattle/launch/store/graphic_1024x578.png` 는 높이가 달라 다시 잘라야 한다 |
| 폰 스크린샷 | 최소 2장, 세로 (권장 1080×1920 이상) | 필수. 원본 `launch/store/screenshots` 는 웹판 화면이라 Unity 판에서 다시 찍는다 |
| 태블릿 스크린샷 | 7"/10" 각 1장 이상 | 태블릿 지원 표시를 원할 때 |

스크린샷 추천 장면:
1. 유닛이 가득 찬 전투 화면 (등급 색이 다양하게)
2. 대전 화면 - 몹 카드를 던지는 순간
3. 신화·태초 소환 배너
4. 아레나·시즌 패스 화면
5. 유닛 도감 (32종)

---

## 확률 고지 (스토어 설명 하단에 추가 권장)

유료 재화(보석)·유료 상품으로 얻는 확률형 아이템이 있어서 확률을 공개한다. 수치 출처는 괄호 안 파일.

```
■ 전투 중 유닛 소환 등급 확률 (Grades.cs)
일반 50% / 고급 33% / 정예 10% / 전설 6.5% / 신화 0.4% / 태초 0.1%
※ '희귀 소환 연구' 레벨이 오르면 전설·신화·태초 확률이 늘어납니다.
※ 게임 안 확률 정보 화면에서 지금 적용되는 확률을 볼 수 있습니다.

■ 유닛 카드 뽑기 (보석 1회 100 / 10회 900) (Chests.cs GachaRates)
일반 70% / 희귀 22% / 영웅 7% / 전설 1%
※ 10회 뽑기는 영웅 이상 1회가 보장됩니다.

■ 상자 (Chests.cs)
- 나무 상자: 영웅 카드 5%
- 은 상자: 영웅 카드 25%, 전설 카드 1%
- 금 상자: 영웅 카드 1장 보장, 전설 카드 4%
- 마법 상자: 영웅 카드 4장 보장, 전설 카드 20%
- 전설 상자: 영웅 카드 2장 보장, 전설 카드 1장 보장
※ 카드 장수는 아레나가 오를수록 늘어납니다.
```
