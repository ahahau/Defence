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
        /// <summary>출발 시점에 확정된 편성 전력. 귀환 피로가 섞이기 전 값이라 판정은 이걸로 한다.</summary>
        private int departedPower;

        public void Configure(GameEventChannelSO wave, GameEventChannelSO cost, GameObject panel, Button map, Button close,
            Button depart, Button[] village, Button[] units, TMP_Text title, TMP_Text detail, TMP_Text roster, TMP_Text result)
        {
            waveEventChannel = wave; costEventChannel = cost; panelRoot = panel; mapButton = map; closeButton = close;
            departButton = depart; villageButtons = village; unitButtons = units; titleText = title; detailText = detail;
            rosterText = roster; resultText = result;
            Wire();
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
            SetPanelActive(panelRoot, false);
            SetPanelActive(resultPanel, false);
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
        private void Show() { if (DayManager.Current == null || !DayManager.Current.IsStandby) return; panelRoot.SetActive(true); panelRoot.transform.SetAsLastSibling(); Refresh(); }
        private void Hide() { if (panelRoot != null) panelRoot.SetActive(false); }
        private void SelectVillage(int index) { if (index >= 0 && index < villages.Length) { selectedVillage = index; selectedUnitSlots.Clear(); Refresh(); } }

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

        private void Depart()
        {
            if (hasActiveExpedition || selectedUnitSlots.Count == 0 || HiredUnitRoster.Current == null) return;
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
            if (success)
            {
                var gain = villageCatalog != null ? villageCatalog.ConquestPerSuccess : 25;
                village.Conquest = Mathf.Min(100, village.Conquest + gain);
                villages[selectedVillage] = village;
            }
            if (costEventChannel != null)
                costEventChannel.RaiseEvent(new GoldEarnedEvent(reward, GoldChangeSource.General));
            var odds = Mathf.RoundToInt(chance * 100f);
            var result = success
                ? $"{village.Name} 작전 성공\n\n확보 자금  +{reward}G\n장악도  {village.Conquest}%\n\n전력 {departedPower} / 난이도 {village.Difficulty}  ·  성공 확률 {odds}%\n귀환한 유닛은 피로도가 누적되었습니다."
                : $"{village.Name} 작전 난항\n\n회수 자금  +{reward}G\n장악도 변화 없음\n\n전력 {departedPower} / 난이도 {village.Difficulty}  ·  성공 확률 {odds}%\n귀환한 유닛의 피로도가 크게 누적되었습니다.";
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
                                  + $"성공 {payout}G  ·  실패 {consolation}G\n\n"
                                  + $"대기 유닛 최대 {MaxPartySize}명을 편성하세요. 지친 부하는 전력이 깎입니다.";
            var selectedNames = new List<string>();
            if (roster != null) foreach (var slot in selectedUnitSlots) if (slot >= 0 && slot < roster.AvailableUnits.Count) selectedNames.Add(roster.AvailableUnits[slot].Name);
            if (rosterText != null) rosterText.text = "편성: " + (selectedNames.Count == 0 ? "없음" : string.Join(", ", selectedNames));
            for (var i = 0; i < villageButtons.Length && i < villages.Length; i++) SetButtonLabel(villageButtons[i], villages[i].Name);
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
                SetButtonLabel(unitButtons[i], (selectedUnitSlots.Contains(i) ? "✓ " : string.Empty) + $"{available.Name}\n전력 {unitPower}");
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
