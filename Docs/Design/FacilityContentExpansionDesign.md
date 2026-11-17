# 기존 그림 기반 시설 콘텐츠 확장

2026-10-27. 사용자 요청: 보스 작업을 보류하고 기존 그림을 활용한 신규 시설부터 진행한다. 보유 에셋과 실제 데이터/프리팹을 확인한 뒤 두 시설 모두 승인받아 구현했다.

## 확인 결과

- 기존 BuildingDataSO16개 중 시설 대부분의 BoardSprite와 프리팹 Visual은 Placeholder/Building.png다. 신규 시설은 실제 팩 그림을 설치 카드와 보드 몸체에 함께 연결한다. 기존 시설 전체의 그림 교체는 이번 범위가 아니다.
- 주점: 비용10G·운영비2G·체류4초·기본 총수입10G·회복2. 대장간: 비용25G·운영비3G·체류3초·총수입20G·모험가 공격+1/방어+2다. 수입과 강화는 내구도·인접 시설·방 특수 효과의 기존 규칙을 따른다.
- 회복실은 같은 방 배치 유닛만 회복하는데, 현재 일반 시설 방에는 유닛 추가 배치가 막혀 있다. 경비 지원형 신설은 공간 규칙의 별도 설계가 필요하므로 이번에는 기존 효과가 검증 가능한 수익 시설 두 가지를 제안한다. 기존 회복실/배치 규칙을 임의 수정하지 않는다.

## 실제 보유 그림

- 취사장: Assets/04.Sprites/Characters/SanctumPixel/42_pixel_art_characters_bundle/healer_package/Sprite/healer_prop/cooking_pot.png — 솥 그림을 직접 열어 확인했다.
- 방어구 공방: Assets/04.Sprites/Characters/SanctumPixel/42_pixel_art_characters_bundle/blacksmith_package/Sprite/props/furnace_2.png — 화덕 그림을 직접 열어 확인했다.
- 두 원본은 Sprite/PPU100이며 화덕은 Bilinear다. 원본을 수정하지 않고 프로젝트 소유 Assets/04.Sprites/Facilities/에 복사한 뒤 Point·PPU25.6·압축 없음·밉맵 없음으로 가져오는 범위다. 실제 중앙 슬롯 배치 크기를 확인해 프리팹 Visual에 맞춘다.

## 시설 제안

| 항목 | 취사장 | 방어구 공방 |
|---|---|---|
| 역할 | 짧은 체류·낮은 건당 수입/운영비 | 높은 건당 수입·모험가 방어 강화 |
| 설치비 | 18G | 35G |
| 일일 운영비 | 1G | 4G |
| 모험가 체류 | 2초 | 4초 |
| 온전한 시설의 기본 이용 수입 | 8G | 24G |
| 이용 대가 | 모험가 HP +1 | 모험가 공격 +0, 방어 +4 |
| 해금 | 5일차·메타 조건0 | 10일차·메타 조건0 |
| 시설 등급 | 1 | 1 |
| 등급 무게 | 1 | 2 |
| 기반 | Inn 프리팹 Variant | Blacksmith 프리팹 Variant |

수치는 구현 승인용 제안값이며 실제 균형 개선을 검증했다는 뜻은 아니다. 기본 이용 수입은 예산·대기 만족도·내구도·인접/방 보정을 적용하기 전 값이다. 기존 주점/대장간과 가격·효과를 비교할 수 있게 설명에 수치를 적는다.

두 시설은 중형/대형 방의 중앙 슬롯을 쓰고, 유닛/함정과 함께 놓지 않는다. 기존 서비스 시설의 마모·수리·휴업·철거·재개장·대기열/동시 이용 규칙을 사용한다. 공용 초기 내구도20과 나머지 운영 설정은 기반 프리팹에서 상속한다. 금화는 기존 이벤트 채널을 거쳐 정산에서 한 번 반영하며, 취사장 수입은 기존 주점 수입 행, 공방 수입은 기존 대장간 수입 행에 합산한다. 새 GoldChangeSource enum은 만들지 않는다.

## 코드·에셋·배선

- Assets/03.Prefabs/Buildings/Facilities/FieldKitchen.prefab, ArmorWorkshop.prefab: 기존 프리팹을 수정하지 않는 Variant로 만들고 해당 회복/수입/강화 값과 Visual 그림을 설정한다.
- Assets/07.SO/Buildings/FieldKitchenBuildingData.asset, ArmorWorkshopBuildingData.asset: 제목·수치 포함 설명·BoardSprite·Prefab·설치 조건·운영비·등급 무게를 작성한다.
- DungeonUnlockCatalog와 SampleScene의 NodePanelView.installableBuildings에 연결한다. 기존 고용 명단/카탈로그 기반 건물 복원 경로에서 새 에셋 키를 찾을 수 있는지도 확인한다.
- Unity CLI Editor API로 생성/연결한다. YAML 직접 편집, 새 런타임 효과 타입, 기존 시설/보스 변경과 저장 형식 변경은 없다.

## 최소 확인과 기록

일정·카드/실제 몸체 그림·프리팹 수치·설치 제한·기존 저장 키 해석/정산 경로를 짧게 확인한다. 필요한 AssetWiring/설치 관련 검사만 수행하며 장시간 영업·밸런스 실측은 하지 않는다. 플레이 확인을 하게 되면 원본 런/백업/메타를 먼저 백업하고 종료 후 해시 복원한다. UI/씬 확인 결과와 모든 변경은 Sequences.md에 남긴다.

## 구현 결과 (2026-10-27)

- FieldKitchen/ArmorWorkshop 프리팹 Variant·BuildingData·프로젝트 소유 PNG를 생성했다. 원본 PNG/가져오기 설정·기존 주점/대장간 프리팹과 데이터8파일은 작업 전 SHA256과 동일하다. 신규 그림은 Point/PPU25.6/압축·밉맵 없음이다. 씬 변경은 설치 목록 참조2줄 추가뿐이며 해금 카탈로그에5/10일차 항목을 추가했다.
- BuildingDataSO에 선택적 description/Description을 추가하고 InstallCardTextFormatter가 새 시설의 수입/대가를 기존 상세 영역에 표시하도록 했다. 설명 없는 기존 시설은 자동 설명을 유지한다. UI 배치·효과 코드·저장 형식 변경은 없다. 내부 식별자와 Unity 오브젝트/에셋 이름은 사용자 지시에 따라 영어, 플레이어에게 보이는 이름·설명은 한국어다.
- Runtime/Tests 빌드 각각 경고0·오류0, Unity 재컴파일 오류0. AssetWiringTests52/52·RoomManagementTests7/7 통과했다. 에셋 읽기로 Variant 상속·수치·그림·해금/설치 목록·저장 키·설명과 초기 수입8/24를 확인했다.
- 생성 eval의 초기 응답은 메인 스레드5000ms 시간 초과였다. 재실행하지 않고 에셋과 연결을 읽어 모두 생성·저장됐음을 확인했다. 최초 요청의 빈 응답을 성공 증거로 삼지 않았다.
- 실제 설치 확인은 검증용10일차/두 시설 해금 상태를 명시적으로 구성했다. 자연 진행/밸런스 표본은 아니다. 실제 방 확장8G씩 후184G에서 취사장18G→166G, 공방35G→131G로 한 번씩 결제됐다. 두 시설의 초기 이용 수입8/24와 합계 운영비5, 실제 설치 카드/배치 그림을 확인했다. 웨이브 영업과 실제 이용 수입 획득은 이번에 실측하지 않았다.
- 검증용 저장 후 실제 타이틀 Start 도착을 확인하고 ContinueRun으로 복원했다.10일차/131G/각 내구도20/그림FieldKitchen·ArmorWorkshop/수입8·24/RestoreIncomplete=false를 확인했다. 원본 런/백업/메타3파일을 종료 후 복원하고 SHA256 전부 일치했다. 에디터 stopped·SampleScene dirty=false, 기준 커서 이후 새 콘솔 오류0(reset/dropped=false)다.
- 캡처: tmp/facility-content-20261027/kitchen.png·workshop.png·restored.png. 증거·검증용 저장·작업 전 사본도 같은 폴더에 있다. Assets의 임시 캡처는 Editor API로 삭제했다. 사용자 요청(여기까지)에 따라 추가 작업 없이 기록 후 종료한다.
