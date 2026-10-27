# 민심 GameScope 이전 설계 검토

2026-10-26 상태: 추가 검토 후 사용자가 보완 설계로 구현을 승인했다. 검토 당시 근거와 구현·검증 계약을 아래에 기록한다.

## 확인한 현재 경로

- MoralePolicyManager.Current 생산 코드 접근은6곳: HiredUnitRoster1, ManagementSettlementManager3, Unit1, MetaRunStarter1. 옛 PolicyChoiceToolkitView 호출은 현재 없다.
- MetaRunStarter는 최초 런타임 AfterSceneLoad 및 이후 sceneLoaded에서 시작 보정을 실행한다. 따라서 일반 Start보다 앞서 실행하던 의미를 보존해야 한다.
- HiredUnitRoster.Start는 첫 주 지원자를 생성하며 민심을 읽는다. GameScope.Start로 호출만 이동하면 두 Start 사이 순서가 명시되지 않아 시작 민심 보정 전 후보를 만들 수 있다. 이전 제안에서 누락한 순서 의존성이다.
- DungeonGraphController.Start는 한 프레임을 기다린 뒤 저장 복원을 실행한다. 저장 에이전트 배열 순서는 유지한다. 복원 실패 시 저장 차단도 유지한다.
- 유닛 생산 생성 경로는 UnitDeployment.Deploy와 DungeonGraphController.RestoreUnits다. 이동은 기존 인스턴스를 옮기고 부활은 같은 인스턴스를 회복하므로 다시 생성하지 않는다.
- Unit.Start는 현재 전역 매니저에 연결한다. MoralePolicyManager.RestoreCheckpoint는 정책 복구 후 CombatModifiersChanged를 발행한다. 주입 시점의 즉시 적용과 이후 이벤트 구독을 모두 보장해야 한다.

## 권장안: 실행 순서를 명시한 조립 루트

1. GameScope에 DefaultExecutionOrder(-100)를 명시하고 Start에서 시작 보정을 한 번 실행한다. Awake에서는 보정을 적용하지 않는다. 다른 매니저 Awake의 초기 금화·민심·부활 마력이 준비된 뒤, 기본 실행 순서0인 HiredUnitRoster.Start의 후보 생성 전에 적용하는 목적이다. 기존 ArtifactEffectController의 -1000 순서는 유지한다. 구현 검증에서 Editor의 실제 MonoImporter 실행 순서도 확인한다.
2. GameScope에 직렬화 CostManager 참조를 추가하고 씬에서 연결한다. 기존 MoralePolicy와 UnitRevival 참조와 함께 MetaRunStarter에 인자로 전달한다. MetaRunStarter의 런타임 설치/sceneLoaded 구독/정적 _listening 상태를 제거한다. 함수 안에서 Current나 Find로 참조를 다시 얻지 않는다.
3. 새 판은 기존 HasSave 판정과 BeginNewRun 장착 보정 확정을 유지하고 금화·민심·부활 마력을 적용한다. 이어하기는 기존 currentRun의 지속형 보정만 적용한다. 저장 손상·백업 판정 정책은 이번 리팩터링에서 바꾸지 않는다.
4. 시작 보정을 OnEnable에서 호출하지 않는다. 씬의 단일 활성 GameScope가 Start에서 호출하며 재활성화로 금화·민심·최대 마력을 더하지 않는다. 타이틀 왕복으로 생긴 새 루트는 새 씬 매니저에 적용한다. 재컴파일은 정지 상태에서 수행하며 플레이 중 스크립트 재로드의 전체 상태 보존은 별도 검증 대상으로 남긴다.
5. GameScope가 없는 게임 씬에서는 보정이 실행되지 않을 수 있으므로 AssetWiringTests에서 단일 활성 루트와 Cost/Morale/Revival 참조를 검증한다. 테스트가 함수만 직접 호출하는 것에 더해 최초 에디터 재생과 타이틀에서 게임 진입 양쪽을 확인한다.

## 유닛 주입 계약

- Unit에 명시적 BindPolicies(MoralePolicyManager) API를 추가한다. 이전 매니저 구독 해제 → 새 참조 지정 → 새 매니저 구독 → 현재 정책 효과 즉시 재계산 순서다. 같은 참조를 다시 주어도 중복 구독/효과 중첩이 없어야 하며 null은 구독 해제와 정책 보정 제거를 뜻한다.
- 일반 배치: Instantiate → Initialize(data) → BindPolicies(전달받은 매니저) → 노드 등록/전장 입장/유물 이벤트. NodePanelView는 이미 가진 scope에서 매니저를 해석해 UnitDeployment에 전달한다. 새 인자는 필수로 하여 호출 누락을 컴파일에서 검출한다.
- 저장 복원: Instantiate → Initialize(data) → BindPolicies(scope의 매니저) → ApplyConditionState → 노드 등록/유물 이벤트. 이후 정책 복원 이벤트를 받으면 다시 계산한다. 반대로 이미 정책이 복원된 상태에 생성돼도 BindPolicies의 즉시 계산으로 반영된다.
- Unit.Start의 전역 참조 획득을 제거하고 OnDestroy의 구독 해제는 유지한다. 비활성 유닛까지 구독을 유지하는 기존 생명주기 의미는 바꾸지 않는다. 이동·회수·부활·상태 보정·유물 보정 수식은 변경하지 않는다.
- MoralePolicyManager.Current 및 등록/해제만 제거하고 Awake의 초기 민심 설정·이벤트·정책 계산은 유지한다. 고용/정산은 기존 scope에서 매니저를 캐시한다.

## 대안과 판단

- 대안: MetaRunStarter를 씬의 부트스트랩 컴포넌트로 바꾸고 sceneLoaded에서 직렬화 참조로 실행한다. 기존 타이밍을 가깝게 유지하지만 별도 컴포넌트/배선/구독 해제와 최초 씬 중복 방지가 필요하다. 작은 조립 루트로 통일하는 권장안보다 관리할 실행 진입점이 늘어난다.
- 제외: 순서 지정 없는 GameScope.Start, GameScope.Awake에서 보정 적용, Unit에서 Find/Current 폴백. 각각 후보 생성 선후관계·매니저 초기값 덮어쓰기·주입 누락 은폐 문제가 있다.

## 구현 승인 후 검증

- 독립 민심 매니저의 지원자 수·유지비·문구 분리와 재활성화 시 참조 갱신.
- 정책 시행 전/후 유닛 생성, 두 매니저 교체, 같은 참조 반복 바인딩, null 해제, 정책 복원 전후의 공격력·방어력. 기존 피로·부상·유물 효과가 함께 유지되는지 검사.
- 새 판 시작 금화/민심 보정이 후보 생성 전에 적용되는지, 보정 없는 판과 있는 판, 이어하기에서 금화·민심 추가 없음과 지속 마력 보정1회, 루트 재활성화 및 타이틀 왕복.
- 원본 런·백업·메타3개 백업 후 실제 고용→배치→정책 반영→저장→이어하기. 원본 복원 SHA256 확인, 에디터 정지·씬 clean 확인.
- Runtime/Tests 빌드, Unity 재컴파일, 전체 EditMode와 콘솔 오류 증분 확인. Current 생산 코드0건 및 프로퍼티 부재 검사. 저장 형식·에이전트 순서·게임 수치 변경 없음 확인.

## 참고

- Docs/AI/GameScopeDesign.md
- Unity 생명주기: https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/runtimeinitializeonloadmethodattribute
- 실행 순서 구현 전 확인: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/DefaultExecutionOrder.html
- 구현 중 Editor MonoImporter 조회값0을 확인해 Editor API로 -100을 명시했다(GameScope.cs.meta). 코드 속성과 함께 유지하며 AssetWiringTests가 실제 임포터 순서를 검사한다. 씬의 Cost 참조는 Editor API로 연결했다.

## 검증 결과 (2026-10-27 마무리)

- 전체 EditMode500/500, Runtime/Tests 빌드 및 Unity 재컴파일 오류0. 생산 민심 Current 접근0건.
- 보정3종 새 판300G/민심65/최대마력40, 루트 재활성화 중복 없음. 실제 고용·배치·긴급 징집 후311G/민심57/마력40·유닛 공격 원시값1.1/방어3을 저장하고 실제 타이틀 이어하기에서 동일 복원했다. 보정 없는 새 판200G/민심50/최대마력30도 확인했다.
- 원본3파일 복원 해시 일치, 에디터 stopped·씬 clean. 콘솔 세션이 재시작돼 전날 전체 오류 증분은 확인하지 못했다. 재개 세션 오류1건은 Pipeline 명령 조회 오류다. 상세와 잘못된 검증 시도 구분은 Sequences.md 및 tmp/morale-scope-20261026 증거를 따른다.
