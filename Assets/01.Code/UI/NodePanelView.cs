using _01.Code.Core;
using _01.Code.Events;
using _01.Code.MapCreateSystem;
using _01.Code.Units;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class NodePanelView : MonoBehaviour
    {
        [field: SerializeField]
        public GameEventChannelSO EventChannel { get; private set; }

        [field: SerializeField]
        public GameObject PanelRoot { get; private set; }

        [field: SerializeField]
        public Text TitleText { get; private set; }

        [field: SerializeField]
        public Button CloseButton { get; private set; }

        [field: SerializeField]
        public string EmptyNodeTitleFormat { get; private set; } = "{0} Unit Hire";

        [field: SerializeField]
        public Transform UnitContentRoot { get; private set; }

        [field: SerializeField]
        public UnitHireEntryView UnitEntryPrefab { get; private set; }

        [field: SerializeField]
        public UnitDataSO[] HireableUnits { get; private set; }
        
        [SerializeField]
        private Unit unitPrefab;

        private Node selectedNode;

        private void Awake()
        {
            PanelRoot.SetActive(false);
            CreateUnitEntries();
        }

        private void OnEnable()
        {
            EventChannel.AddListener<ShowNodePanelEvent>(HandleShowNodePanel);
            CloseButton.onClick.AddListener(HandleCloseClicked);
        }

        private void OnDisable()
        {
            EventChannel.RemoveListener<ShowNodePanelEvent>(HandleShowNodePanel);
            CloseButton.onClick.RemoveListener(HandleCloseClicked);
        }

        private void HandleShowNodePanel(ShowNodePanelEvent evt)
        {
            selectedNode = evt.Node;
            TitleText.text = string.Format(EmptyNodeTitleFormat, evt.Node.Data.Type);
            PanelRoot.SetActive(true);
        }

        private void CreateUnitEntries()
        {
            foreach (var unit in HireableUnits)
            {
                var entry = Instantiate(UnitEntryPrefab, UnitContentRoot);
                entry.Initialize(unit, HandleHireRequested);
            }
        }

        private void HandleHireRequested(UnitDataSO unit)
        {
            selectedNode.AssignUnit(unit);
            Unit unitGo = Instantiate(unitPrefab, selectedNode.UnitPosition.position, Quaternion.identity);
            unitGo.Initialize(unit);
            EventChannel.RaiseEvent(new UnitAssignedToNodeEvent(selectedNode, unit));
            PanelRoot.SetActive(false);
        }

        private void HandleCloseClicked()
        {
            PanelRoot.SetActive(false);
        }
    }
}
