using System;
using Code.Stages;
using UnityEngine;

namespace Code.UI.Stages
{
    /// <summary>편성 목록을 바닥 함정·벽 함정·부하 탭으로 나눈다. 어느 줄을 보일지는 StageHudView가 Matches로 정한다.</summary>
    public sealed class StageOptionTabs : MonoBehaviour
    {
        public enum Tab
        {
            FloorTraps = 0,
            WallTraps = 1,
            Units = 2
        }

        [SerializeField] private UiFrameButton floorTraps;
        [SerializeField] private UiFrameButton wallTraps;
        [SerializeField] private UiFrameButton units;

        public Tab Selected { get; private set; } = Tab.FloorTraps;
        public event Action Changed;

        public bool IsValid => floorTraps != null && wallTraps != null && units != null;

        public bool Matches(StageOption option) => option != null && Selected switch
        {
            Tab.WallTraps => option.WallTrap != null,
            Tab.Units => option.Unit != null,
            _ => option.Trap != null
        };

        /// <summary>벽 함정이 없는 카탈로그(원본 스테이지)는 벽 함정 탭을 숨긴다.</summary>
        public void Configure(bool hasWallTraps)
        {
            wallTraps.SetVisible(hasWallTraps);
            if (!hasWallTraps && Selected == Tab.WallTraps)
                Choose(Tab.FloorTraps);
            Render();
        }

        private void OnEnable()
        {
            if (!IsValid)
                return;
            floorTraps.Button.onClick.AddListener(ChooseFloor);
            wallTraps.Button.onClick.AddListener(ChooseWall);
            units.Button.onClick.AddListener(ChooseUnits);
            Render();
        }

        private void OnDisable()
        {
            if (!IsValid)
                return;
            floorTraps.Button.onClick.RemoveListener(ChooseFloor);
            wallTraps.Button.onClick.RemoveListener(ChooseWall);
            units.Button.onClick.RemoveListener(ChooseUnits);
        }

        private void ChooseFloor() => Choose(Tab.FloorTraps);
        private void ChooseWall() => Choose(Tab.WallTraps);
        private void ChooseUnits() => Choose(Tab.Units);

        private void Choose(Tab tab)
        {
            if (Selected == tab)
                return;
            Selected = tab;
            Render();
            Changed?.Invoke();
        }

        private void Render()
        {
            floorTraps.SetSelected(Selected == Tab.FloorTraps);
            wallTraps.SetSelected(Selected == Tab.WallTraps);
            units.SetSelected(Selected == Tab.Units);
        }
    }
}
