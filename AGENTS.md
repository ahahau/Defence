# Repository Guidelines

## 안내

### 대상과 적용 범위

이 문서는 Unity 6000.6.3f1·URP 2D 기반 던전 경영 게임에 기여하는 개발자와 에이전트를 위한 작업 안내입니다. 코드, UI, 데이터, 씬, 테스트 및 문서 변경에 적용합니다.

### 단일 기준 문서

작업 규칙의 기준은 `CLAUDE.md`입니다. 매 작업 전에 이를 읽고, 작업 종류에 맞춰 그 문서가 지정한 `.claude/rules/`와 스킬을 적용하세요. 규칙 파일이나 작업 유형이 추가·변경되어도 이 참조 구조로 즉시 반영되므로, 이 문서에 목록을 중복 관리하지 않습니다.

## 작업 순서와 원칙

### 시작 전

구현 전에는 `Docs/`에서 관련 기획서를 찾아 읽고, 기획 판단은 그 문서와 씬·에셋의 실제 설정을 우선합니다. 새 기능의 기획이나 기존 기획의 변경은 `Docs/Design/<Feature>Design.md`로 작성·갱신한 뒤 구현합니다. C# 기본값만으로 게임 규칙을 판단하지 마세요. 코드·에셋·설정 변경은 범위와 방식을 먼저 설명하고 사용자 승인을 받은 뒤 시작합니다.

### 구현 시 주의

게임 런타임 코드는 `Assets/Code/`에 기능별로 두며 네임스페이스는 `Code.<Feature>`를 사용합니다. 재사용 엔티티·스탯은 `Assets/GameLib/Entity/`에 둡니다. 외부 에셋(`Feel`, `Plugins`, `csiimnida`, `vHierarchy`, `TextMesh Pro`)은 수정하지 말고, `.unity`·프리팹 YAML도 직접 편집하지 않습니다.

### 검증

새 C# 파일을 추가했다면 Unity 재컴파일 후 EditMode 테스트를 실행합니다. 경고 개수가 아니라 오류 0개가 완료 기준입니다.

```powershell
dotnet build DungeonKeeper.Runtime.csproj -v:q
dotnet build Defence.EditMode.Tests.csproj -v:q
unity cmd recompile
unity cmd run_tests --mode EditMode --filter <테스트_클래스_또는_이름>
unity cmd console --level error --tail 50
```

## 코드와 테스트 기준

`.editorconfig`와 `Docs/AI/CodingStyle.md`를 따릅니다. C#은 공백 4칸과 Allman 중괄호를 사용합니다. 타입·메서드·공개 멤버는 `PascalCase`, 직렬화 private 필드는 `camelCase`, 런타임 전용 private 필드는 `_camelCase`입니다. 기존 직렬화 필드의 이름을 바꿀 때는 참조를 검색하고 `FormerlySerializedAs`로 호환성을 유지합니다.

NUnit EditMode 테스트는 `Assets/Tests/EditMode/Reflected/`에 두고 `Feature_Behavior` 형식으로 이름을 짓습니다. asmdef 경계를 넘는 검증은 기존 리플렉션 패턴을 따릅니다.

## 제출과 기록

모든 변경은 `Sequences.md`에 날짜별로 남깁니다. 커밋은 `feat:`, `fix:`, `refactor:`, `test:`, `chore:` 형식을 사용합니다. PR에는 목적, 핵심 변경, 실행한 검증과 결과를 적고, UI·씬 변경에는 스크린샷을 첨부합니다. `WaveManager`, 저장·복원, 정산처럼 민감한 흐름은 영향 범위와 회귀 위험을 분명히 적으세요.
