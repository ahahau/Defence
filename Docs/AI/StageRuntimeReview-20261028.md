# 스테이지 후속 작업 점검 — 2026-10-28

사용자 요청: 토큰/사용량 제한 중 진행된 작업을 확인한다. 코드 수정 없이 현재 파일·작업 이력·직렬화 에셋을 대조하고 런타임/에디터 코드만 다시 빌드했다. 테스트·플레이·프리뷰·밸런스 실측은 실행하지 않았다.

## 반영된 작업

- 고정 스테이지 7개, 부하 9종/함정 7종(편성 대상 16개), 첫 클리어 해금·2/4 스테이지 슬롯 보상.
- 막힌 복귀 자리의 근처 빈 칸 탐색, 준비 중 경로 미리보기, 다중 칸 함정 크기 연결.
- 설정·일시정지·1배/2배, 보스 웨이브/호위와 보스 생존 중 일시정지 잠금.
- 스킬 자동 시전·유닛별 궁극기 게이지/수동 시전·스테이지 스킬 대상 조회.
- 도중 개방 문, 설치 불가/함정 강화 칸, 바리케이드 무시 도약/교전 무시 돌진 표식.
- 불 구멍·여닫는 문·낙석 장치 및 표시, 시작 지연과 문 개방 데이터 검증 보완.

Sequences.md의 후속 기록과 실제 소스/에셋이 대응한다. 초기 스테이지 2개/편성 6개 버전에 멈춘 상태는 아니다. StageDefense 씬 파일의 StageOptionRow 직렬화 블록 16개를 읽었고 label/pick/upgrade/remove의 빈 fileID는 없었다. 이는 텍스트/참조 대조 결과이며 UI 실행 검증은 아니다.

## 후속 수정 상태

2026-10-28 사용자 작업 재개 승인 후 아래 3건을 코드에서 수정했다. ISkillMovement/StageSkillMovement로 격자 경로·착지·대상 방향과 부하 추적 반경을 확인하고, StageActor에 속박/이동 잠금과 스킬 이동 후 경로 초기화를 연결했다. CanSee는 닫힌 문도 반영한다. 구 SampleScene의 스킬 이동 호출은 유지한다. Runtime/Editor 빌드·Unity 재컴파일 오류 0, 현재 편집기 콘솔 오류 0이다. 테스트/플레이 재현 검증은 하지 않았다. 아래는 수정 전 발견 근거를 보존한 기록이다.

## 당시 발견한 수정 대상

### P1 — 이동 스킬이 새 격자 통행 검사를 우회한다

- `Assets/01.Code/Skills/AssassinBackstabSkillEffectSO.cs:26`은 기존 BattleAgent.TeleportToCombatPosition을 호출한다.
- `Assets/01.Code/Skills/DashSkillEffectSO.cs:13`도 기존 DashAwayFromTarget을 호출한다.
- 새 스테이지의 BattleAgent는 StageActor.Initialize에서 비활성화되고 구 방 아레나에 배정하지 않는다. `Assets/01.Code/BT/BattleAgent.cs:1203`은 아레나가 없으면 위치를 검사 없이 반환한다.
- 따라서 이동 스킬의 착지 지점이 고정 벽/닫힌 문/맵 밖이어도 StageBoard.IsPassable을 거치지 않는다. 이후 StagePathfinder가 출발 칸을 바닥으로 인정하지 않으면 해당 적이 이동하지 못해 웨이브 종료를 막을 수 있다. 코드 경로로 확인한 가능성이며 실제 발생을 플레이로 재현하지 않았다.
- 별도로 대시는 SkillContext.Target 대신 구 BattleAgent.CurrentTarget을 참조하므로 새 어댑터가 고른 타깃과 방향이 일치하지 않을 수 있다.
- 제안: 스테이지 전용 스킬 이동 인터페이스로 목표/통행/범위·유효 착지점을 함께 검증하고, 구 SampleScene 경로는 유지한다.

### P2 — 속박 스킬의 이동 제한이 StageActor에 연결되지 않았다

- `Assets/01.Code/Skills/SnareSkillEffectSO.cs:23`은 BattleAgent.ApplySnare로 `_snareUntil`을 설정한다.
- 실제 새 이동을 맡는 `Assets/01.Code/Stages/StageActor.cs:260`의 Advance는 예비 공격/경직과 CombatStatusController 배율만 확인하며 BattleAgent.IsSnared를 확인하지 않는다.
- 속박 상태가 설정돼도 새 이동 경로에서는 계속 걸을 수 있다. 이동 배율 감소 상태와 기존 속박 전용 플래그는 서로 다른 경로다.
- 제안: 실제 이동 어댑터에서 속박/이동 잠금을 공통으로 확인한다.

### P2 — 닫힌 환경 문이 시야를 차단하지 않는다

- 환경 문은 StageBoard.ClosedCells와 IsPassable에 연결돼 이동을 막는다.
- `Assets/01.Code/Stages/StageSession.cs:311`의 CanSee는 Definition.IsFloor와 바리케이드만 확인하고 ClosedCells를 확인하지 않는다.
- 닫힌 문 뒤의 적을 교전 대상으로 선택하거나 사거리 내에서 공격할 수 있다. 기존 고정 벽/바리케이드 시야 차단과 불일치한다.
- 제안: 통행과 시야의 차이를 명시하고 기본 닫힌 문은 시야 검사에도 반영한다.

## 기록 정리 필요

CLAUDE.md의 현재 구현/진입 흐름 일부와 StageRuntimeImplementationDesign.md 끝의 후속 목록은 초기 버전 설명이 남아 있다. 이미 추가된 스킬/설정/기믹이 여전히 미구현인 것처럼 읽히는 부분을 최신 상태로 통일해야 한다. 최초 점검에서는 코드·씬·에셋을 수정하지 않았다. 후속 승인 작업에서 CLAUDE와 현재 구현 요약을 스테이지 7개/편성 16종·스킬/보스·기믹/설정 반영 상태로 동기화했다.

## 이번 확인 범위

- `dotnet build DungeonKeeper.Runtime.csproj -v:q`: 성공, 오류 0.
- `dotnet build Assembly-CSharp-Editor.csproj -v:q`: 성공, 오류 0.
- 스테이지 에셋 7개·카탈로그 부하 9/함정 7·씬 편성 행 16개 및 비어 있는 행 참조 없음: 파일 대조.
- 이전 tmp/stage-runtime-20261028 로그는 초기 버전 시점이므로 최신 후속 구현 검증 근거와 혼동하지 않는다.
- Unity 재컴파일/현재 콘솔 상태는 이번에 재조회하지 않았다. 과거 작업 말미 해당 조회가 사용량 제한으로 자동 승인 검토를 완료하지 못해 실행되지 않은 이력이 있다. 이번에는 로컬 파일 확인과 컴파일로 점검했다.
- 테스트·플레이·밸런스 실측 없음. 위 3건은 수정하지 않은 정적 검토 결과다.

후속 수정 근거는 tmp/stage-review-fixes-20261028의 코드 변경 전 백업/차이와 컴파일 기록이다. 수정 전 지적의 런타임 재현 여부와 컴파일 완료를 혼동하지 않는다. 씬/에셋 수치·사용자 저장은 이번 수정에서 바꾸지 않았다.
