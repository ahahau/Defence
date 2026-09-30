---
paths:
  - "Assets/**/*.cs"
---
# C# 코딩 규칙

## 형식·명명
- `.editorconfig`를 따른다. 공백 4칸, Allman 중괄호, UTF-8, CRLF. `var`보다 명시적 타입.
- 네임스페이스는 `Code.<Feature>`(게임) / `GameLib.<Area>`(재사용). 타입·메서드·프로퍼티·상수는 PascalCase, 지역 변수는 camelCase, 인터페이스는 `I` 접두사.
- 인스펙터 노출은 `[SerializeField] private`, public 필드 금지. 기존 직렬화 필드는 camelCase를 유지하고(프리팹 호환), 새 런타임 전용 private은 `_camelCase`.
- 직렬화 필드 이름을 바꾸기 전에 프리팹·씬·SO 사용처를 검색하고 `FormerlySerializedAs`를 단다.
- 멤버 순서: 직렬화 필드 → 런타임 상태 → 공개 API → Unity 생명주기 → 이벤트 처리 → 보조 메서드.
- 주석은 비자명한 분기 위의 짧은 한국어 의도 주석으로 쓰고, 재사용 공개 API에는 XML 요약을 단다. 구현을 읽어 주는 주석은 쓰지 않는다.
- 로그는 `Debug.Log`를 직접 쓴다(로그 래퍼 없음).

## 성능·설계
- `Update()`/`FixedUpdate()` 안에서 `GetComponent`/`Find`를 호출하지 않는다. `Awake()`에서 캐싱한다.
- 매 프레임 class를 `new`로 할당하지 않는다. 꼭 필요하면 사전 승인을 받는다.
- 한 파일에 public class 하나. 예외: `Code/Events/*Events.cs`는 기능별 이벤트 타입을 한 파일에 모은다. God class 금지. 서로 다른 시스템 간 결합은 인터페이스나 이벤트 채널로 한다.

## 아키텍처
- 어셈블리:
  - `Assets/GameLib/Entity` → `DungeonKeeper.GameLib`. 게임 코드를 참조하지 않는다.
  - `Assets/Code` → `DungeonKeeper.Runtime`. 새 패키지 의존을 추가하면 asmdef `references`도 고친다.
  - `Assets/Tests/EditMode/Reflected` → `Defence.EditMode.Tests`(Runtime 참조).
- 모듈 시스템: `ModuleOwner`가 자식의 `IModule`을 모아 `Initialize` → `AfterInitialize`(`IAfterInitModule`) 2단계로 깨운다. 모듈 간 배선은 `AfterInitialize`에서 하고, 조회는 `GetModule<T>()`로 한다.
- 이벤트: `GameEventChannelSO` 기반 pub/sub이고 이벤트 타입은 `Code/Events`에 있다. 채널은 SO라 구독 상태가 플레이 종료 후에도 남으므로, 구독은 `OnEnable`/`Awake` ↔ `OnDisable`/`OnDestroy`로 반드시 짝을 맞춰 해제한다.
- 매니저는 정적 `Current` 접근자를 가진다. `Awake`에서 설정하고, `OnDestroy`에서 자기 자신일 때만 해제한다.
- 데이터는 ScriptableObject(`Assets/GameModules/Data`)에, 진행 중 변하는 값은 런타임 상태에 둔다. 플레이 중에 SO를 쓰면 에셋이 오염된다.
- 입력: `Code/Core/Input/Controls.cs`는 Input System 생성 코드다. 편집하지 말고 `.inputactions`에서 재생성한다.
