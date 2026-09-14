using System.Collections.Generic;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Manager;
using _01.Code.Progression;
using _01.Code.Units;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public sealed class ExpeditionMapPanelView : MonoBehaviour
    {
        /// <summary>카탈로그의 정의에 이번 판에서 변하는 장악도를 얹은 런타임 상태.</summary>
        public struct Village
        {
            public string Name;
            public string Purpose;
            public int Reward;
            public int Difficulty;
            public int Conquest;
        }

        [SerializeField, Tooltip("원정 대상 마을 목록. 비어 있으면 작전 지도를 열어도 고를 마을이 없다.")]
        private ExpeditionVillageCatalogSO villageCatalog;

        [SerializeField] private GameEventChannelSO waveEventChannel;
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button mapButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button departButton;
        [SerializeField, Tooltip("마을 버튼이 채워질 스크롤 목록의 Content. 비어 있으면 아래 고정 버튼 배열을 그대로 쓴다.")]
        private RectTransform villageContentRoot;

        [Header("지도 뷰")]
        [SerializeField, Tooltip("지도를 끌어 옮기는 뷰포트. 스크롤바는 붙이지 않는다 — 지도는 목록이 아니라 밀어 보는 그림이다.")]
        private ScrollRect mapScroll;

        [SerializeField, Tooltip("마을 한 칸의 원본. 카탈로그의 마을 수만큼 복제된다.")]
        private Button villageButtonTemplate;

        [SerializeField] private Button[] villageButtons = System.Array.Empty<Button>();
        [SerializeField] private Button[] unitButtons = System.Array.Empty<Button>();
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text detailText;
        [SerializeField] private TMP_Text rosterText;
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TMP_Text resultTitleText;
        [SerializeField] private TMP_Text resultBodyText;
        [SerializeField] private Button resultCloseButton;

        // Button slots, rather than UnitDataSO references, keep duplicate hires selectable.
        private readonly List<int> selectedUnitSlots = new();
        private readonly List<(UnitDataSO Unit, UnitConditionState Condition)> deployedUnits = new();
        private readonly List<UnityAction> villageActions = new();
        private readonly List<UnityAction> unitActions = new();
        private Village[] villages;
        private int selectedVillage;
        private bool hasActiveExpedition;
        private bool isWired;
        private readonly List<RectTransform> mapLinks = new();
        private static RectTransform centerMarkerPrefab;
        private static RectTransform mapLinkPrefab;

        [SerializeField, Tooltip("마을을 잇는 선 색. 앞 마을을 쳐야 뒷 마을이 열린다는 관계를 그린다.")]
        private Color mapLinkColor = new(0.55f, 0.42f, 0.28f, 0.85f);

        [SerializeField, Tooltip("거미줄처럼 깔리는 잔길의 색. 해금 경로보다 흐리게 둔다.")]
        private Color webLinkColor = new(0.42f, 0.33f, 0.22f, 0.45f);

        [SerializeField, Range(0.1f, 1.5f), Tooltip("이 거리 안의 마을끼리 잔길을 잇는다. 크면 전부 이어져 그물이 뭉개진다.")]
        private float webLinkRange = 0.62f;

        private int lastUnlockDay = int.MinValue;
        private bool? lastStandbyState;
        /// <summary>출발 시점에 확정된 편성 전력. 귀환 피로가 섞이기 전 값이라 판정은 이걸로 한다.</summary>
        private int departedPower;

        public void Configure(GameEventChannelSO wave, GameEventChannelSO cost, GameObject panel, Button map, Button close,
            Button depart, Button[] village, Button[] units, TMP_Text title, TMP_Text detail, TMP_Text roster, TMP_Text result)
        {
            waveEventChannel = wave; costEventChannel = cost; panelRoot = panel; mapButton = map; closeButton = close;
            departButton = depart; villageButtons = village; unitButtons = units; titleText = title; detailText = detail;
            rosterText = roster; resultText = result;
            Wire();
            RefreshFeatureAvailability();
        }

        /// <summary>마을 칸을 담을 스크롤 목록과 그 원본 버튼을 연결한다.</summary>
        public void ConfigureVillageList(RectTransform contentRoot, Button template)
        {
            villageContentRoot = contentRoot;
            villageButtonTemplate = template;
        }

        public void ConfigureResultModal(GameObject panel, TMP_Text title, TMP_Text body, Button close)
        {
            resultPanel = panel;
            resultTitleText = title;
            resultBodyText = body;
            resultCloseButton = close;
            SetPanelActive(resultPanel, false);
            if (isWired && resultCloseButton != null)
                resultCloseButton.onClick.AddListener(HideResult);
            Wire();
        }

        private void Awake()
        {
            villages = BuildVillagesFromCatalog();
            BuildVillageButtons();
            SetPanelActive(panelRoot, false);
            SetPanelActive(resultPanel, false);
            RefreshFeatureAvailability();
        }

        /// <summary>
        /// 마을 칸을 카탈로그에 있는 수만큼 만들어 스크롤 목록에 채운다.
        /// 버튼을 프리팹에 고정으로 박아 두면 마을을 하나 늘릴 때마다 프리팹을 손봐야 한다.
        /// </summary>
        private void BuildVillageButtons()
        {
            if (villageButtonTemplate == null || villageContentRoot == null)
                return;

            villageButtonTemplate.gameObject.SetActive(false);

            var built = new List<Button>(villages.Length);
            for (var i = 0; i < villages.Length; i++)
            {
                var button = Instantiate(villageButtonTemplate, villageContentRoot);
                button.name = $"Village{i}";
                button.gameObject.SetActive(true);
                PlaceOnMap(button, i);
                built.Add(button);
            }

            villageButtons = built.ToArray();
            BuildMapLinks();
        }

        /// <summary>
        /// 마을을 지도 좌표에 놓는다. 목록으로 쌓으면 어디가 어디인지가 사라지고,
        /// 앞 마을을 쳐야 뒷 마을이 열린다는 관계도 보이지 않는다.
        /// 앵커로 잡아 두면 지도 크기가 달라져도 상대 위치가 유지된다.
        /// </summary>
        private void PlaceOnMap(Button button, int index)
        {
            var entry = villageCatalog != null ? villageCatalog.Get(index) : null;
            if (button == null || entry == null)
                return;

            var rect = (RectTransform)button.transform;
            var position = entry.MapPosition;
            rect.anchorMin = position;
            rect.anchorMax = position;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
        }
        /// <summary>
        /// 마을들을 길로 잇는다. 굵은 선은 해금 경로 — 앞 마을을 쳐야 뒤가 열린다는 관계다.
        /// 가는 선은 그냥 길이다. 그게 없으면 핀 여섯 개가 허공에 떠 있고,
        /// 있으면 비로소 한 장의 지도로 읽힌다.
        /// </summary>
        private void BuildMapLinks()
        {
            if (villageCatalog == null || villageContentRoot == null)
                return;

            foreach (var existing in mapLinks)
            {
                if (existing != null)
                    Destroy(existing.gameObject);
            }

            mapLinks.Clear();
            var count = villageCatalog.Count;

            // 던전이 한가운데 있고 길은 거기서 뻗어나간다.
            // 중심이 없으면 여섯 점이 그냥 흩어져 있고, 어디서 출발하는 이야기인지가 안 보인다.
            mapLinks.Add(CreateCenterMarker());
            for (var i = 0; i < count; i++)
            {
                var entry = villageCatalog.Get(i);
                if (entry == null || entry.UnlockAfterVillage >= 0)
                    continue;

                mapLinks.Add(CreateLink(MapCenter, entry.MapPosition, $"Spoke{i}", mapLinkColor, 3.5f));
            }

            // 가는 길부터 깔고 그 위에 해금 경로를 얹는다. 순서가 곧 그려지는 순서다.
            for (var a = 0; a < count; a++)
            for (var b = a + 1; b < count; b++)
            {
                var from = villageCatalog.Get(a)?.MapPosition ?? Vector2.zero;
                var to = villageCatalog.Get(b)?.MapPosition ?? Vector2.zero;
                if (Vector2.Distance(from, to) > webLinkRange)
                    continue;

                if (IsUnlockPair(a, b))
                    continue;

                mapLinks.Add(CreateLink(from, to, $"Web{a}_{b}", webLinkColor, 1.5f));
            }

            for (var i = 0; i < count; i++)
            {
                var entry = villageCatalog.Get(i);
                var required = entry != null ? entry.UnlockAfterVillage : -1;
                if (required < 0 || required >= count)
                    continue;

                var from = villageCatalog.Get(required).MapPosition;
                mapLinks.Add(CreateLink(from, entry.MapPosition, $"Link{required}_{i}", mapLinkColor, 3.5f));
            }
        }

        /// <summary>둘이 해금 관계로 묶인 쌍인가. 굵은 선을 가는 선이 덧그리지 않게 가른다.</summary>
        private bool IsUnlockPair(int a, int b)
        {
            var first = villageCatalog.Get(a);
            var second = villageCatalog.Get(b);
            return (first != null && first.UnlockAfterVillage == b)
                   || (second != null && second.UnlockAfterVillage == a);
        }

        /// <summary>지도의 한가운데 — 이 던전. 길이 여기서 뻗어나간다.</summary>
        private static Vector2 MapCenter => new(0.5f, 0.5f);

        private RectTransform CreateCenterMarker()
        {
            centerMarkerPrefab ??= Resources.Load<RectTransform>("UI/ExpeditionMapCenter");
            if (centerMarkerPrefab == null)
            {
                Debug.LogError("Missing UI prefab: UI/ExpeditionMapCenter", this);
                return null;
            }
            var rect = Instantiate(centerMarkerPrefab, villageContentRoot, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = rect.anchorMax = MapCenter;
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        private RectTransform CreateLink(Vector2 from, Vector2 to, string name, Color color, float thickness)
        {
            mapLinkPrefab ??= Resources.Load<RectTransform>("UI/ExpeditionMapLink");
            if (mapLinkPrefab == null)
            {
                Debug.LogError("Missing UI prefab: UI/ExpeditionMapLink", this);
                return null;
            }
            var rect = Instantiate(mapLinkPrefab, villageContentRoot, false);
            rect.name = name;
            // 선은 핀보다 뒤에 있어야 한다. 위에 그리면 이름을 가린다.
            rect.SetAsFirstSibling();

            var mid = (from + to) * 0.5f;
            rect.anchorMin = mid;
            rect.anchorMax = mid;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;

            // 앵커가 정규 좌표라 길이는 실제 픽셀로 재야 한다.
            var size = villageContentRoot.rect.size;
            var delta = new Vector2((to.x - from.x) * size.x, (to.y - from.y) * size.y);
            rect.sizeDelta = new Vector2(delta.magnitude, thickness);
            rect.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

            var image = rect.GetComponent<UnityEngine.UI.Image>();
            image.color = color;
            return rect;
        }

        /// <summary>카탈로그의 정의를 런타임 상태로 옮긴다. 장악도만 판마다 새로 시작한다.</summary>
        private Village[] BuildVillagesFromCatalog()
        {
            if (villageCatalog == null || villageCatalog.Count == 0)
            {
                Debug.LogWarning($"{nameof(ExpeditionMapPanelView)}에 원정 마을 카탈로그가 없습니다. 작전 지도에 고를 마을이 표시되지 않습니다.", this);
                return System.Array.Empty<Village>();
            }

            var source = villageCatalog.Villages;
            var result = new Village[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                var entry = source[i];
                result[i] = new Village
                {
                    Name = entry.DisplayName,
                    Purpose = entry.Purpose,
                    Reward = entry.Reward,
                    Difficulty = entry.Difficulty,
                    Conquest = entry.StartingConquest
                };
            }

            return result;
        }

        /// <summary>
        /// 웨이브 쪽이 읽을 수 있게 시작 장악도를 등록한다.
        /// Awake에서 하면 VillageConquestSystem이 아직 깨지 않았을 수 있어 등록이 조용히 새어나간다.
        /// Start는 모든 Awake 뒤에 돌므로 순서를 걱정하지 않아도 된다.
        /// </summary>
        private void Start()
        {
            var conquest = VillageConquestSystem.Current;
            if (conquest == null || villageCatalog == null)
                return;

            conquest.ResetConquest();
            foreach (var entry in villageCatalog.Villages)
            {
                if (entry != null)
                    conquest.Register(entry.OriginParty, entry.StartingConquest);
            }
        }

        public void SyncConquestFromSystem()
        {
            var conquest = VillageConquestSystem.Current;
            if (conquest == null || villageCatalog == null || villages == null)
                return;
            var entries = villageCatalog.Villages;
            for (var i = 0; i < villages.Length && i < entries.Count; i++)
            {
                var village = villages[i];
                village.Conquest = conquest.GetConquest(entries[i]?.OriginParty);
                villages[i] = village;
            }
            Refresh();
        }

        /// <summary>
        /// 인스펙터에 연결되지 않은 오브젝트를 안전하게 걸러낸다.
        /// null 조건 연산자(?.)는 UnityEngine.Object가 오버로딩한 ==를 건너뛰기 때문에
        /// 미할당 참조를 통과시켜 UnassignedReferenceException을 던진다.
        /// </summary>
        private static void SetPanelActive(GameObject target, bool active)
        {
            if (target != null)
                target.SetActive(active);
        }

        private static void AddClick(Button button, UnityAction action)
        {
            if (button != null)
                button.onClick.AddListener(action);
        }

        private static void RemoveClick(Button button, UnityAction action)
        {
            if (button != null)
                button.onClick.RemoveListener(action);
        }

        private void OnEnable()
        {
            Wire();
            RefreshFeatureAvailability();
        }

        private void Update()
        {
            var isStandby = DayManager.Current != null && DayManager.Current.IsStandby;
            if (CurrentDay != lastUnlockDay || lastStandbyState != isStandby)
                RefreshFeatureAvailability();
        }

        private void Wire()
        {
            if (mapButton == null || panelRoot == null)
                return;
            if (isWired) return;
            isWired = true;
            AddClick(mapButton, Toggle);
            AddClick(closeButton, Hide);
            AddClick(departButton, Depart);
            AddClick(resultCloseButton, HideResult);
            for (var i = 0; i < villageButtons.Length; i++)
            {
                var index = i;
                UnityAction action = () => SelectVillage(index);
                villageActions.Add(action);
                AddClick(villageButtons[i], action);
            }
            for (var i = 0; i < unitButtons.Length; i++)
            {
                var index = i;
                UnityAction action = () => ToggleUnit(index);
                unitActions.Add(action);
                AddClick(unitButtons[i], action);
            }
            if (waveEventChannel != null)
                waveEventChannel.AddListener<WaveEndedEvent>(ResolveExpedition);
        }

        private void OnDisable()
        {
            if (!isWired) return;
            isWired = false;
            RemoveClick(mapButton, Toggle); RemoveClick(closeButton, Hide); RemoveClick(departButton, Depart);
            RemoveClick(resultCloseButton, HideResult);
            for (var i = 0; i < villageButtons.Length && i < villageActions.Count; i++) RemoveClick(villageButtons[i], villageActions[i]);
            for (var i = 0; i < unitButtons.Length && i < unitActions.Count; i++) RemoveClick(unitButtons[i], unitActions[i]);
            villageActions.Clear(); unitActions.Clear();
            if (waveEventChannel != null)
                waveEventChannel.RemoveListener<WaveEndedEvent>(ResolveExpedition);
        }

        private void OnDestroy()
        {
            // The expedition state is currently session-only. Never leave hired units removed when a scene reloads.
            if (!hasActiveExpedition || HiredUnitRoster.Current == null)
                return;

            foreach (var member in deployedUnits)
                HiredUnitRoster.Current.ReturnFromExpedition(member.Unit, member.Condition);
            deployedUnits.Clear();
            hasActiveExpedition = false;
        }

        private void Toggle() { if (panelRoot != null && panelRoot.activeSelf) Hide(); else Show(); }
        private void Show()
        {
            if (!CoreLoopFeatureUnlocks.IsExpeditionUnlocked(CurrentDay)
                || DayManager.Current == null
                || !DayManager.Current.IsStandby)
                return;

            panelRoot.SetActive(true);
            panelRoot.transform.SetAsLastSibling();
            Refresh();
        }

        private void Hide() { if (panelRoot != null) panelRoot.SetActive(false); }

        private void RefreshFeatureAvailability()
        {
            lastUnlockDay = CurrentDay;
            var isStandby = DayManager.Current != null && DayManager.Current.IsStandby;
            lastStandbyState = isStandby;
            var unlocked = CoreLoopFeatureUnlocks.IsExpeditionUnlocked(lastUnlockDay);
            if (mapButton != null)
            {
                mapButton.gameObject.SetActive(unlocked);
                mapButton.interactable = isStandby;
            }
            if (!unlocked || !isStandby)
                Hide();
        }

        /// <summary>
        /// 앞선 마을을 완전히 장악해야 열리는 마을인가. UnlockAfterVillage가 -1이면 처음부터 열려 있다.
        /// 지도를 한 번에 다 열어 두면 제일 싼 곳만 되풀이해 치게 되고, 어디부터 칠지가 결정이 되지 않는다.
        /// </summary>
        private bool IsVillageUnlocked(int index)
        {
            var entry = villageCatalog != null ? villageCatalog.Get(index) : null;
            if (entry == null) return true;
            var required = entry.UnlockAfterVillage;
            if (required < 0 || villages == null || required >= villages.Length) return true;
            return villages[required].Conquest >= 100;
        }

        /// <summary>완전 장악 보상을 미리 보여준다. 무엇이 걸렸는지 알아야 순서가 선택이 된다.</summary>
        private static string DescribeConquestReward(ExpeditionVillageEntry entry)
        {
            if (entry == null || entry.ConquestRewardAmount <= 0)
                return string.Empty;

            switch (entry.ConquestReward)
            {
                case VillageConquestReward.Gold:       return $"\n완전 장악  운영 자금 +{entry.ConquestRewardAmount}G";
                case VillageConquestReward.Applicants: return $"\n완전 장악  고용 지원자 +{entry.ConquestRewardAmount}명";
                case VillageConquestReward.Magic:      return $"\n완전 장악  주둔 마력 +{entry.ConquestRewardAmount}";
                default: return string.Empty;
            }
        }

        /// <summary>
        /// 장악도가 100%에 닿은 순간 한 번만 준다.
        /// 호출부가 "이번에 처음 100이 되었는가"를 판단하므로 여기서는 다시 검사하지 않는다.
        /// </summary>
        private string GrantConquestReward(ExpeditionVillageEntry entry)
        {
            if (entry == null || entry.ConquestRewardAmount <= 0)
                return string.Empty;

            var amount = entry.ConquestRewardAmount;
            switch (entry.ConquestReward)
            {
                case VillageConquestReward.Gold:
                    costEventChannel?.RaiseEvent(new GoldEarnedEvent(amount, GoldChangeSource.General));
                    return $"운영 자금 +{amount}G";
                case VillageConquestReward.Applicants:
                    if (HiredUnitRoster.Current == null) return string.Empty;
                    HiredUnitRoster.Current.AddRecruitmentCandidates(amount);
                    return $"고용 지원자 +{amount}명";
                case VillageConquestReward.Magic:
                    var magic = FindAnyObjectByType<MagicManager>();
                    if (magic == null) return string.Empty;
                    magic.IncreaseMaxMagic(amount);
                    return $"주둔 마력 +{amount}";
                default:
                    return string.Empty;
            }
        }

        private void SelectVillage(int index) { if (index >= 0 && index < villages.Length && IsVillageUnlocked(index)) { selectedVillage = index; selectedUnitSlots.Clear(); Refresh(); } }

        private void ToggleUnit(int buttonIndex)
        {
            var roster = HiredUnitRoster.Current;
            if (roster == null || buttonIndex < 0 || buttonIndex >= roster.AvailableUnits.Count) return;
            if (selectedUnitSlots.Contains(buttonIndex)) selectedUnitSlots.Remove(buttonIndex);
            else if (selectedUnitSlots.Count < MaxPartySize) selectedUnitSlots.Add(buttonIndex);
            Refresh();
        }

        private int MaxPartySize => villageCatalog != null ? villageCatalog.MaxPartySize : 3;

        /// <summary>지금 고른 편성의 전력. 부하의 값어치와 피로를 함께 본다.</summary>
        private int CalculateSelectedPower()
        {
            var roster = HiredUnitRoster.Current;
            if (roster == null || villageCatalog == null)
                return 0;

            var total = 0;
            foreach (var slot in selectedUnitSlots)
            {
                if (slot < 0 || slot >= roster.AvailableUnits.Count)
                    continue;

                var unit = roster.AvailableUnits[slot];
                total += villageCatalog.GetUnitPower(unit, roster.GetBestAvailableCondition(unit));
            }

            return total;
        }

        private static int CurrentDay => DayManager.Current != null ? DayManager.Current.CurrentDay : 0;

        /// <summary>
        /// 장악이 방어에 무슨 도움이 되는지. 금화만 보이면 원정이 그냥 돈벌이 버튼이 된다.
        /// </summary>
        private static string BuildConquestEffectText(Village village)
        {
            var wave = WaveManager.Current;
            if (wave == null)
                return string.Empty;

            var nextDay = DayManager.Current != null ? DayManager.Current.NextWaveDay : 0;
            var incoming = wave.GetPreviewEnemyCount(nextDay);
            if (incoming <= 0)
                return string.Empty;

            var line = $"\n다음 침입 {incoming}명";
            return village.Conquest > 0
                ? line + $"  ·  이 마을 습격대는 {village.Conquest}% 확률로 오지 않음"
                : line;
        }

        private void Depart()
        {
            if (hasActiveExpedition || selectedUnitSlots.Count == 0 || HiredUnitRoster.Current == null) return;
            if (!IsVillageUnlocked(selectedVillage)) return;
            var power = CalculateSelectedPower();
            var chosenUnits = new List<UnitDataSO>();
            foreach (var slot in selectedUnitSlots)
                if (slot >= 0 && slot < HiredUnitRoster.Current.AvailableUnits.Count)
                    chosenUnits.Add(HiredUnitRoster.Current.AvailableUnits[slot]);
            deployedUnits.Clear();
            foreach (var unit in chosenUnits)
                if (HiredUnitRoster.Current.TryTakeAvailableUnit(unit, out var condition)) deployedUnits.Add((unit, condition));
            if (deployedUnits.Count == 0) return;
            hasActiveExpedition = true;
            departedPower = power;
            if (resultText != null) resultText.text = $"{villages[selectedVillage].Name}에 작전대를 보냈습니다. 방어전 종료 후 결과가 도착합니다.";
            selectedUnitSlots.Clear(); Hide();
        }

        private void ResolveExpedition(WaveEndedEvent evt)
        {
            if (!hasActiveExpedition || HiredUnitRoster.Current == null) return;
            if (villages == null || selectedVillage < 0 || selectedVillage >= villages.Length)
            {
                hasActiveExpedition = false;
                return;
            }

            var village = villages[selectedVillage];
            var entry = villageCatalog != null ? villageCatalog.Get(selectedVillage) : null;

            // 전력은 출발할 때 확정된 값을 쓴다. 여기서 다시 재면 이미 귀환 피로가 섞인다.
            var chance = ExpeditionVillageCatalogSO.GetSuccessChance(departedPower, village.Difficulty);
            var success = Random.value < chance;
            var reward = villageCatalog != null
                ? villageCatalog.GetReward(entry, evt.Day, deployedUnits.Count, success)
                : 0;

            var gainedFatigue = villageCatalog != null
                ? (success ? villageCatalog.SuccessFatigue : villageCatalog.FailureFatigue)
                : 0f;
            foreach (var member in deployedUnits)
            {
                HiredUnitRoster.Current.ReturnFromExpedition(member.Unit, new UnitConditionState(
                    member.Condition.Fatigue + gainedFatigue, member.Condition.Injury, member.Condition.HealthRatio,
                    member.Condition.Trait, member.Condition.Personality, member.Condition.Command));
            }
            // 이번 성공으로 처음 100%에 닿았는가. 보상은 그 순간 한 번만 준다.
            var rewardLine = string.Empty;
            if (success)
            {
                var wasFullyHeld = village.Conquest >= 100;
                var gain = villageCatalog != null ? villageCatalog.ConquestPerSuccess : 25;
                village.Conquest = Mathf.Min(100, village.Conquest + gain);
                villages[selectedVillage] = village;
                // 장악한 만큼 이 마을에서 오는 습격이 줄어든다. 웨이브가 이 값을 읽는다.
                VillageConquestSystem.Current?.SetConquest(entry?.OriginParty, village.Conquest);

                if (!wasFullyHeld && village.Conquest >= 100)
                {
                    var granted = GrantConquestReward(entry);
                    if (!string.IsNullOrEmpty(granted))
                        rewardLine = $"\n\n<color=#7ADB8A>{village.Name} 완전 장악</color>\n{granted}";
                    // 이 마을을 조건으로 잠겨 있던 곳이 이제 열린다.
                    Refresh();
                }
            }
            if (costEventChannel != null)
                costEventChannel.RaiseEvent(new GoldEarnedEvent(reward, GoldChangeSource.General));
            var odds = Mathf.RoundToInt(chance * 100f);
            // 결과 창은 제목을 따로 띄운다. 본문에 마을 이름과 성패를 또 적으면 같은 말이 두 번 나온다.
            // 확률과 전력은 떠나기 전에 이미 보고 결정한 값이라 결과에는 남기지 않는다.
            // 피로도는 유닛 카드에 그대로 보이므로 문장으로 설명하지 않는다.
            var result = success
                ? $"확보 자금  +{reward}G\n장악도  {village.Conquest}%" + rewardLine
                : $"회수 자금  +{reward}G\n장악도  변화 없음";
            if (resultText != null) resultText.text = result;
            ShowResult(success ? "작전 성공" : "작전 결과", result);
            deployedUnits.Clear(); hasActiveExpedition = false; departedPower = 0;
        }

        private void ShowResult(string title, string body)
        {
            if (resultPanel == null)
                return;

            if (resultTitleText != null)
                resultTitleText.text = title;
            if (resultBodyText != null)
                resultBodyText.text = body;
            resultPanel.SetActive(true);
            resultPanel.transform.SetAsLastSibling();
        }

        private void HideResult()
        {
            if (resultPanel != null)
                resultPanel.SetActive(false);
        }

        private void Refresh()
        {
            if (villages == null || villages.Length == 0) return;
            var village = villages[selectedVillage];
            if (titleText != null) titleText.text = "작전 지도";
            var roster = HiredUnitRoster.Current;
            var power = CalculateSelectedPower();
            var entry = villageCatalog != null ? villageCatalog.Get(selectedVillage) : null;
            var day = CurrentDay;
            var odds = Mathf.RoundToInt(ExpeditionVillageCatalogSO.GetSuccessChance(power, village.Difficulty) * 100f);
            var payout = villageCatalog != null ? villageCatalog.GetReward(entry, day, selectedUnitSlots.Count, true) : village.Reward;
            var consolation = villageCatalog != null ? villageCatalog.GetReward(entry, day, selectedUnitSlots.Count, false) : 0;
            if (detailText != null)
                detailText.text = $"{village.Name}\n{village.Purpose}\n\n난이도 {village.Difficulty}  ·  장악도 {village.Conquest}%\n"
                                  + $"편성 전력 {power}  ·  성공 확률 {odds}%\n"
                                  + $"성공 {payout}G  ·  실패 {consolation}G\n"
                                  + DescribeConquestReward(entry)
                                  + BuildConquestEffectText(village)
                                  + $"\n대기 유닛 최대 {MaxPartySize}명을 편성하세요. 지친 부하는 전력이 깎입니다.";
            var selectedNames = new List<string>();
            if (roster != null) foreach (var slot in selectedUnitSlots) if (slot >= 0 && slot < roster.AvailableUnits.Count) selectedNames.Add(roster.AvailableUnits[slot].Name);
            if (rosterText != null) rosterText.text = "편성: " + (selectedNames.Count == 0 ? "없음" : string.Join(", ", selectedNames));
            for (var i = 0; i < villageButtons.Length && i < villages.Length; i++)
            {
                // 목록에서 바로 비교할 수 있게 난이도와 장악도를 칸에 같이 적는다.
                var listed = villages[i];
                var open = IsVillageUnlocked(i);
                var marker = !open ? "잠김  " : (i == selectedVillage ? "▶ " : string.Empty);
                if (villageButtons[i] != null) villageButtons[i].interactable = open;
                // 핀은 작다. 잠긴 이유를 문장으로 늘어놓으면 상자를 넘치므로,
                // 어느 마을이 필요한지 이름으로만 알린다 — 어차피 그게 알고 싶은 전부다.
                var gate = villageCatalog != null ? villageCatalog.Get(i) : null;
                var required = gate != null ? gate.UnlockAfterVillage : -1;
                var requiredName = required >= 0 && required < villages.Length ? villages[required].Name : "앞선 마을";
                SetButtonLabel(villageButtons[i], open
                    ? $"{marker}{listed.Name}\n<size=80%>난이도 {listed.Difficulty}  ·  장악 {listed.Conquest}%</size>"
                    : $"{marker}{listed.Name}\n<size=80%>{requiredName} 장악 필요</size>");
            }
            for (var i = 0; i < unitButtons.Length; i++)
            {
                var available = roster != null && i < roster.AvailableUnits.Count ? roster.AvailableUnits[i] : null;
                if (unitButtons[i] == null) continue;
                unitButtons[i].gameObject.SetActive(available != null);
                if (available == null) continue;
                // 전력이 보여야 누구를 보낼지 고를 수 있다. 지친 부하는 여기서 이미 낮게 뜬다.
                var unitPower = villageCatalog != null
                    ? villageCatalog.GetUnitPower(available, roster.GetBestAvailableCondition(available))
                    : 0;
                SetButtonLabel(unitButtons[i], (selectedUnitSlots.Contains(i) ? "● " : string.Empty) + $"{available.Name}\n전력 {unitPower}");
            }
            if (departButton != null) departButton.interactable = selectedUnitSlots.Count > 0 && !hasActiveExpedition;
        }

        private static void SetButtonLabel(Button button, string value)
        {
            var label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
            if (label != null) label.text = value;
        }
    }
}
