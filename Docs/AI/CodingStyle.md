# C# Coding Style

`RPG_2025_winter/Assets/Code`의 실사용 관례를 `Defence`에 적용한다. 이 문서는 새 코드와 리팩터링 대상에 적용하며, 기존 직렬화 필드 이름을 일괄 변경하지 않는다.

## Formatting

- 들여쓰기는 공백 4칸, 중괄호는 Allman 스타일로 쓴다.
- 간단하고 부수 효과가 명확한 전달 메서드·읽기 전용 프로퍼티는 식 본문(`=>`)을 사용할 수 있다.
- 변수 선언은 타입이 코드 의도를 드러낼 때 명시적 타입을 우선한다.

## Naming

- 네임스페이스는 `Code.<Feature>` 또는 재사용 영역의 `GameLib.<Area>`를 쓴다.
- 타입, 메서드, 프로퍼티, 이벤트는 PascalCase다. 이벤트는 `On` 또는 발생 사실을 드러내는 이름을 쓴다.
- `[SerializeField] private` 설정값은 기존 프리팹과 호환되도록 `camelCase`를 유지한다.
- Inspector에 노출되지 않는 런타임 캐시·상태 필드는 `_camelCase`를 쓴다.
- 상수는 PascalCase, 인터페이스는 `I` 접두사를 쓴다.

## Unity conventions

- 멤버 순서는 직렬화 필드, 런타임 상태, 공개 API, Unity 생명주기, 이벤트 처리, 보조 메서드 순으로 둔다.
- 구독은 `OnEnable`/`Awake`에서 만들었다면 `OnDisable`/`OnDestroy`에서 같은 짝으로 해제한다.
- 공개 API와 비자명한 생명주기·상태 전환에는 XML 요약 또는 짧은 한국어 의도 주석을 남긴다. 구현을 그대로 읽어 주는 주석은 쓰지 않는다.

## Serialization safety

- 이름 변경 전에는 프리팹·씬·ScriptableObject 사용처를 검색한다.
- 이미 저장된 `[SerializeField]` 필드는 유지하거나 `FormerlySerializedAs`로 마이그레이션한다.
