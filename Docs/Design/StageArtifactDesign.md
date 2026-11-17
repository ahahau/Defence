# 스테이지 유물

2026-10-29 사용자 결정으로 구 방 경로의 유물을 StageDefense로 옮겼다. 구 SampleScene의 상인·보유 목록(`ObtainedArtifactInventory`)·유물 에셋은 그대로 둔다.

## 확정 규칙

- **획득:** 스테이지 첫 클리어 때 아직 없는 유물 중 무작위 3개(`ArtifactChoices`)를 보여 주고 하나를 고른다. 재클리어는 주지 않는다. 남은 유물이 3개보다 적으면 있는 만큼, 없으면 주지 않는다.
- **후보 보존:** 후보는 첫 클리어 기록·강화 재화와 같은 저장에 넣는다. 고르기 전에 나가도 같은 후보가 남고, 유물 창이 스스로 열려 고를 때까지 닫히지 않는다. 보상 모드에서는 화면 전체 바탕이 다른 HUD 조작을 막는다. 현재 카탈로그에 없는 후보만 남았으면 고를 것이 없는 것으로 보고 다음 첫 클리어 때 새 후보로 바꾼다.
- **보유·장착:** 고른 유물은 영구 보유(새 게임에서도 유지되는 스테이지 프로필)다. 스테이지마다 2칸(`ArtifactSlots`)까지 장착하고, 편성과 같이 첫 웨이브 전까지만 바꾼다. 새 스테이지로 들어가면 장착은 비운다.
- **조합:** 두 유물을 모두 장착했을 때만 성립한다(보유만으로는 성립하지 않는다).
- **대상:** 부하만. 스테이지에는 메인 유닛이 없으므로 유물·조합의 대상(모든 유닛/고용 유닛/플레이어 유닛)을 구분하지 않고 모든 부하에게 적용한다. 함정에는 적용하지 않는다.
- **개작:** 대장의 인장은 "모든 부하 공격력 +3, 최대 체력 +10"으로 읽는다(인장+탑 방패 조합도 모든 부하). 고립 늑대 부적은 "주변 2칸(`LoneAllyRadius`) 안에 다른 부하가 없을 때 피해 ×1.35"로 바꿨다. 물약 3종은 부하가 웨이브 뒤 자동 복구되므로 후보에서 뺐다.
- 칸 수 2·후보 3·늑대 부적 거리 2칸은 임시 값이다. 유물/조합 수치는 바꾸지 않았다.

## 구현

| 위치 | 역할 |
|---|---|
| `StageCatalogSO.Artifacts`·`ArtifactCombos`·`ArtifactSlots`·`ArtifactChoices`·`LoneAllyRadius` | 후보 12종(ID = `artifact:` + 에셋 GUID)과 스테이지 설명. 생성기 `StageDefenseBuilder.ApplyArtifactTable()` |
| `StageArtifact` | 후보 한 줄. 스테이지 설명이 비면 유물 원래 설명을 쓴다(인장·늑대 부적만 따로 적음) |
| `StageArtifacts` | 장착 목록, 능력치·조합 적용(`Unit.ApplyArtifactBonus`), 공격 때 조건부 피해, `IArtifactField` 구현. 피해 이벤트는 세션 전용 실행 중 채널로 받아 구 `ArtifactEffectController`와 섞이지 않고, 조건부 효과가 있는 유물을 장착했을 때만 건다 |
| `IArtifactField`, `ArtifactEffectContext.Field` | 유물 효과가 방 그래프 대신 묻는 전장. null이면 구 방 판정(고립 늑대: 인접 방 적)을 그대로 쓴다 |
| `StageProfile` | `artifacts`(보유)·`artifactChoices`(남은 후보). 없던 구 파일은 빈 목록. `TryClear`가 후보를 같은 저장에 넣고 `TryPickArtifact`가 보유로 옮긴다 |
| `StageCheckpointData.artifacts` | 장착 유물. 복원 때 칸 수·중복·보유를 검사하고, 없는 구 저장은 장착 없이 복원한다 |
| `StageArtifactWindow`·`StageArtifactRow`·`StageArtifactPresenter` | 가운데 창(840×720, 줄 12개 미리 생성·스크롤). 장착 모드/보상 모드. 조합은 원래 설명 대신 실제 능력치로 적는다. 열린 동안 맵 배치 입력을 막고 ESC 목록에 들어간다 |
| `StageHudView` | 편성 패널의 유물 두 줄(장착·조합)과 "유물 장착 · 보상" 버튼. 클리어 후 후보가 남으면 주 버튼이 "유물 보상 고르기" |

씬은 `StageDefenseBuilder.AddArtifacts()`로 고쳤다. 편성 목록 높이 600→470, 유물 칸 위 720~826, 메시지 자리는 그대로다.

## 검증 한계

Runtime/Editor 빌드·Unity 재컴파일 오류 0, 씬 배선(HUD 3필드·창 줄 12·ESC 목록 2)과 카탈로그 12종을 편집 모드에서 확인했다. 테스트·플레이·실제 보상 선택/장착/저장 왕복·화면 배치 실측은 하지 않았다. 유물 창 글자 넘침과 공격 때 피해 보정의 실제 동작은 미검증이다.
