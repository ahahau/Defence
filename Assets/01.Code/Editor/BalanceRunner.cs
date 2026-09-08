using UnityEditor;
using UnityEngine;

namespace _01.Code.Editor
{
    /// <summary>
    /// 20일 한 판을 시드 넷으로 자동 완주시키고 일차별 기록을 남긴다.
    ///
    /// 밸런스를 눈이 아니라 숫자로 보려고 만든 것이다. 사람이 스무 날을 손으로 돌리면
    /// 표본이 하나뿐이고 그마저 조작 실력에 흔들린다.
    ///
    /// 원래는 세션마다 eval 로 붙여 넣던 스크립트였는데, 그러면 에디터 연결이 끊길 때마다
    /// 실측이 막힌다. 실제로 이 프로젝트에서 여러 번 그랬다. 메뉴로 박아 두면 연결과 무관해진다.
    ///
    /// 기록은 프로젝트 옆 BalanceRuns/ 에 쌓인다. 한 줄이 하루다 —
    /// 시드|일차|총적|격퇴|가한|받은|치명타|금화|부채|민심|유닛수|생존|체력/최대|보스여부
    ///
    /// 멈추려면 BalanceRuns/STOP 파일을 만들면 다음 프레임에 스스로 떨어진다.
    /// 25분이 지나거나 플레이 모드를 끄면 알아서 끝난다.
    /// </summary>
    public static class BalanceRunner
    {
        [MenuItem("Tools/Defence/밸런스/20일 실측 시작 (시드 4개)", priority = 240)]
        public static void Run()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("밸런스 실측",
                    "플레이 모드에서 실행해 주세요. 재생을 누른 뒤 다시 이 메뉴를 고르시면 됩니다.", "확인");
                return;
            }

            Install();
        }

        private static void Install()
        {
        // 20일 완주 자동 드라이버. EditorApplication.update에 붙어 스스로 굴러간다.
        // 매 프레임 폴링을 바깥에서 하면 왕복이 수십 번 나므로 안쪽에서 돈다.
        // 멈추려면 STOP 파일을 만들면 된다 — 손댈 수 없는 루프를 남기지 않기 위한 안전장치다.
        var dir = System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", "BalanceRuns");
                System.IO.Directory.CreateDirectory(dir);
        var log = System.IO.Path.Combine(dir, "run_" + System.DateTime.Now.ToString("MMdd_HHmm") + ".tsv");
        var stopFlag = System.IO.Path.Combine(dir, "STOP");
        System.IO.File.WriteAllText(log, "");
        if (System.IO.File.Exists(stopFlag)) System.IO.File.Delete(stopFlag);

        System.Action<string> Say = s => System.IO.File.AppendAllText(log, s + "\n");

        // 칸 건물(금고·함정 등)을 InstallCentral로 세우면 TrapGrid.PlacedBuildings에 등록되지 않는다.
        // 그러면 node.EnumerateTreasuries()/FindTreasuryWithGold()가 못 찾아서
        // 이자도 안 붙고 약탈 대상도 되지 않는 '유령 건물'이 된다.
        // 게임의 실제 경로(NodePanelView)는 UsesGridCell로 갈라 InstallOnCell을 부른다. 그걸 그대로 따른다.
        System.Func<_01.Code.MapCreateSystem.Node, _01.Code.Buildings.BuildingDataSO, bool> PlaceLikeGame = (node, data) =>
        {
            if (node == null || data == null) return false;
            if (_01.Code.Buildings.BuildingPlacement.UsesGridCell(data))
            {
                var g = node.TrapGrid;
                if (g == null || !g.HasFreeCell) return false;
                for (int c = 0; c < 10; c++)
                    for (int r = 0; r < 10; r++)
                        if (g.IsValidCell(c, r) && g.IsCellFree(c, r) && !g.IsCentralBuildingSlotCell(c, r))
                            return _01.Code.Buildings.BuildingPlacement.InstallOnCell(node, c, r, data) != null;
                return false;
            }
            return _01.Code.Buildings.BuildingPlacement.InstallCentral(node, data) != null;
        };

        // ── 관리 단계 ────────────────────────────────────────────────────────
        System.Func<string> Manage = () =>
        {
            var roster = _01.Code.Manager.HiredUnitRoster.Current;
            var cost = _01.Code.Manager.CostManager.Current;
            var dgc = UnityEngine.Object.FindAnyObjectByType<_01.Code.MapCreateSystem.DungeonGraphController>();
            var t = typeof(_01.Code.MapCreateSystem.DungeonGraphController);
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var nodeCh = (_01.Code.Core.GameEventChannelSO)t.GetField("nodeEventChannel", flags).GetValue(dgc);
            var costCh = (_01.Code.Core.GameEventChannelSO)t.GetField("costEventChannel", flags).GetValue(dgc);
            var artField = t.GetField("artifactEventChannel", flags);
            var artCh = artField != null ? (_01.Code.Core.GameEventChannelSO)artField.GetValue(dgc) : null;
            var buildCost = (int)t.GetField("buildGoldCost", flags).GetValue(dgc);
            if (roster == null || cost == null) return "NOAPI";

            // 포탈이 없으면 DayManager가 웨이브를 시작하지 않는다. 입구에 먼저 세운다.
            int portals = 0;
            if (UnityEngine.Object.FindAnyObjectByType<_01.Code.Buildings.Portal>() == null)
            {
                var pd = UnityEditor.AssetDatabase.LoadAssetAtPath<_01.Code.Buildings.BuildingDataSO>("Assets/03.SO/Buildings/PortalBuildingData.asset");
                _01.Code.MapCreateSystem.Node entrance = null;
                foreach (var n in UnityEngine.Object.FindObjectsByType<_01.Code.MapCreateSystem.Node>(UnityEngine.FindObjectsInactive.Include))
                    if (n.name.Contains("Entrance")) entrance = n;
                if (pd != null && entrance != null && !entrance.HasAssignedBuilding)
                {
                    var portal = _01.Code.Buildings.BuildingPlacement.InstallCentral(entrance, pd);
                    if (portal != null)
                    {
                        nodeCh.RaiseEvent(new _01.Code.Events.BuildingInstalledEvent(entrance, pd));
                        nodeCh.RaiseEvent(new _01.Code.Events.PortalInstalledEvent(entrance));
                        portals = 1;
                    }
                }
            }

            int built = 0, hired = 0, deployed = 0, potions = 0;

            // 함정 몫을 먼저 떼어 둔다.
            //
            // 예전에는 방 → 경제 → 물약 → 고용 → 배치 → 함정 순으로 있는 돈을 그냥 앞에서부터 썼다.
            // 함정이 맨 끝이라 남는 게 40~51G뿐이었고, 문턱을 조금만 올려도(가격 40G) 1일차 뒤로는
            // 한 개도 못 지었다. 그래서 함정의 가격도 내구도도 잴 수가 없었다 —
            // 유보액을 낮추면 하루 25개씩 폭증하고 올리면 0이 되는 이분법만 나왔다(2026-09-01 배치6·8·9).
            //
            // 앞선 항목들은 이 몫을 침범하지 않고 멈춘다. 사람이 예산을 나눠 쓰는 것에 가깝다.
            var trapBudget = UnityEngine.Mathf.RoundToInt(cost.CurrentGold * 0.3f);
            // 저축 몫도 함께 뗀다. 예치를 소비 순서 맨 끝에 두면 늘 잉여가 0이라 한 푼도 못 넣는다
            // (함정에서 겪은 것과 같은 기아 현상 — 2026-09-01에 두 번 반복했다).
            // 여기서 떼어야 "은행에 넣을까 지금 쓸까"가 실제 선택이 된다.
            var saveBudget = UnityEngine.Mathf.RoundToInt(cost.CurrentGold * 0.25f);

            // 방 짓기 — 성공은 "열린 방이 늘었나"로 본다. 잠긴 노드 수로 세면 오판한다.
            System.Func<int> openRooms = () => {
                int c = 0;
                foreach (var n in UnityEngine.Object.FindObjectsByType<_01.Code.MapCreateSystem.Node>(UnityEngine.FindObjectsInactive.Include))
                    if (!n.name.StartsWith("LockedNode")) c++;
                return c;
            };
            for (int k = 0; k < 3; k++)
            {
                if (cost.CurrentGold - trapBudget - saveBudget < buildCost) break;
                _01.Code.MapCreateSystem.Node target = null;
                foreach (var n in UnityEngine.Object.FindObjectsByType<_01.Code.MapCreateSystem.Node>(UnityEngine.FindObjectsInactive.Include))
                {
                    if (!n.name.StartsWith("LockedNode")) continue;
                    if (n.FromNode == null || n.FromNode.FreePorts <= 0) continue;
                    target = n; break;
                }
                if (target == null) break;
                var beforeRooms = openRooms();
                costCh.RaiseEvent(new _01.Code.Events.BuildCostRequestedEvent(target, buildCost));
                if (openRooms() <= beforeRooms) break;
                built++;
            }

            // 던전이 스스로 벌게 한다 — 광산, 그다음 금고.
            int economy = 0;
            {
                string[] wanted = {
                    // 휴게실이 먼저다. 22G밖에 안 하는데 드라이버가 여태 안 지어서
                    // 네 판 전부 회복 경로 없이 굴렀고, 전부 주인공 사망으로 끝났다.
                    "Assets/03.SO/Buildings/RecoveryRoomBuildingData.asset",
                    "Assets/03.SO/Buildings/MineBuildingData.asset",
                    "Assets/03.SO/Buildings/MineBuildingData.asset",
                    "Assets/Resources/Buildings/TreasuryBuildingData.asset"
                };
                foreach (var path in wanted)
                {
                    var bd = UnityEditor.AssetDatabase.LoadAssetAtPath<_01.Code.Buildings.BuildingDataSO>(path);
                    if (bd == null || cost.CurrentGold - trapBudget - saveBudget < bd.Cost) continue;
                    _01.Code.MapCreateSystem.Node spot = null;
                    foreach (var n in UnityEngine.Object.FindObjectsByType<_01.Code.MapCreateSystem.Node>(UnityEngine.FindObjectsInactive.Include))
                    {
                        if (n.name.StartsWith("LockedNode") || n.name.Contains("Entrance")) continue;
                        if (n.HasAssignedBuilding) continue;
                        spot = n; break;
                    }
                    if (spot == null) break;
                    costCh.RaiseEvent(new _01.Code.Events.BuildCostRequestedEvent(spot, bd.Cost));
                    if (PlaceLikeGame(spot, bd)) economy++;
                }
            }

            // 물약 — 상인이 와 있는 날만. 비싼 것부터 보되 못 사면 싼 것으로 내려간다.
            var merchant = UnityEngine.Object.FindAnyObjectByType<_01.Code.UI.MerchantPanelView>(UnityEngine.FindObjectsInactive.Include);
            var player = UnityEngine.Object.FindAnyObjectByType<_01.Code.Units.MainUnit>();
            if (merchant != null && merchant.IsMerchantHere && player != null && player.Health != null)
            {
                var shelf = new string[] {
                    "Assets/03.SO/Artifacts/Potions/Potion_Large.asset",
                    "Assets/03.SO/Artifacts/Potions/Potion_MasterTonic.asset",
                    "Assets/03.SO/Artifacts/Potions/Potion_Small.asset"
                };
                for (int k = 0; k < 5; k++)
                {
                    if (player.Health.CurrentRatio >= 0.85f) break;
                    var bought = false;
                    foreach (var path in shelf)
                    {
                        var potion = UnityEditor.AssetDatabase.LoadAssetAtPath<_01.Code.Artifacts.ArtifactDataSO>(path);
                        if (potion == null || cost.CurrentGold < potion.Price) continue;
                        var beforeHp = player.Health.CurrentHealth;
                        costCh.RaiseEvent(new _01.Code.Events.ArtifactPurchaseRequestedEvent(potion, potion.Price));
                        if (player.Health.CurrentHealth <= beforeHp) continue;
                        potions++; bought = true; break;
                    }
                    if (!bought) break;
                }
            }

            System.Func<_01.Code.Units.UnitDataSO, int> dataPower = u => {
                var pf = u != null ? u.Prefab : null;
                var cb = pf != null ? pf.GetComponentInChildren<_01.Code.Combat.Combatant>(true) : null;
                return cb != null ? cb.AttackDamage : 0;
            };


            // 마력 한 칸이 사 오는 공격력. 주둔 마력은 5뿐이라 이게 진짜 통화다.
            // "공격력 센 유닛부터"로 고르면 선봉대(공격 7, 마력 5)가 마력을 통째로 먹어
            // 유닛 한 명으로 판을 치른다. 잡몹은 그래도 막히지만 보스 파티에는 전열이 없어 진다
            // (2026-09-01 18일차 1/6 패배가 정확히 이것). 같은 마력 5면
            // 석궁수(2)+창병(2)+정찰(1) = 3명에 공격력 합 10이 나온다.
            System.Func<_01.Code.Units.UnitDataSO, float> dataValue = u =>
                u == null ? -1f : dataPower(u) / (float)UnityEngine.Mathf.Max(1, u.MagicCost);

            // 고용 — 마력당 공격력이 높은 순.
            var buyable = new System.Collections.Generic.List<_01.Code.Units.UnitDataSO>(roster.UnlockedUnits);
            buyable.Sort((a, b) => dataValue(b).CompareTo(dataValue(a)));
            foreach (var unit in buyable)
            {
                if (unit == null) continue;
                for (int k = 0; k < 2; k++)
                {
                    if (cost.CurrentGold - trapBudget - saveBudget < unit.Cost) break;
                    var before = roster.GetAvailableUnitCount(unit);
                    costCh.RaiseEvent(new _01.Code.Events.RosterHireRequestedEvent(unit, UnityEngine.Mathf.Max(0, unit.Cost)));
                    if (roster.GetAvailableUnitCount(unit) <= before) break;
                    hired++;
                }
            }

            // 교체 — 마력이 꽉 찼는데 대기석에 더 센 유닛이 있으면 약한 쪽을 회수한다.
            var magic = UnityEngine.Object.FindAnyObjectByType<_01.Code.Manager.MagicManager>();
            int swapped = 0;
            if (magic != null)
            {
                var mgmt = new _01.Code.Manager.UnitManagementSystem(nodeCh, costCh, _01.Code.Manager.DayManager.Current);
                for (int guard = 0; guard < 6; guard++)
                {
                    _01.Code.Units.UnitDataSO benchBest = null; int benchPower = -1;
                    foreach (var c in roster.AvailableUnits) { var pw = dataPower(c); if (pw > benchPower) { benchPower = pw; benchBest = c; } }
                    if (benchBest == null) break;

                    _01.Code.Units.Unit weakest = null; _01.Code.MapCreateSystem.Node weakNode = null; int weakPower = int.MaxValue;
                    foreach (var u in UnityEngine.Object.FindObjectsByType<_01.Code.Units.Unit>(UnityEngine.FindObjectsSortMode.None))
                    {
                        if (u == null || u is _01.Code.Units.MainUnit || u.Combatant == null) continue;
                        _01.Code.MapCreateSystem.Node owner;
                        _01.Code.MapCreateSystem.Node.UnitPlacement placement;
                        if (!_01.Code.MapCreateSystem.Node.TryFindUnit(u, out owner, out placement)) continue;
                        var pw = u.Combatant.AttackDamage;
                        if (pw < weakPower) { weakPower = pw; weakest = u; weakNode = owner; }
                    }
                    if (weakest == null || weakNode == null) break;
                    if (benchPower <= weakPower) break;
                    if (magic.UsedMagic + benchBest.MagicCost <= magic.MaxMagic) break;

                    string why;
                    if (!mgmt.TryRecall(weakNode, weakest, out why)) break;
                    swapped++;
                }
            }

            // 배치 — 적은 입구로 들어온다. 입구부터, 그다음은 입구에서 가까운 방 순으로.
            var rooms = new System.Collections.Generic.List<_01.Code.MapCreateSystem.Node>();
            foreach (var n in UnityEngine.Object.FindObjectsByType<_01.Code.MapCreateSystem.Node>())
                if (n != null && !n.name.StartsWith("LockedNode") && n.TrapGrid != null) rooms.Add(n);
            _01.Code.MapCreateSystem.Node entranceRoom = null;
            foreach (var n in rooms) if (n.name.Contains("Entrance")) entranceRoom = n;
            var origin = entranceRoom != null ? entranceRoom.transform.position : UnityEngine.Vector3.zero;
            rooms.Sort((x, y) => {
                bool xe = x.name.Contains("Entrance"), ye = y.name.Contains("Entrance");
                if (xe != ye) return xe ? -1 : 1;
                return UnityEngine.Vector3.SqrMagnitude(x.transform.position - origin)
                    .CompareTo(UnityEngine.Vector3.SqrMagnitude(y.transform.position - origin));
            });
            foreach (var node in rooms)
            {
                while (node.CanAcceptAdditionalUnit && roster.AvailableUnits.Count > 0)
                {
                    // 배치도 같은 기준 — 마력 한 칸당 화력이 큰 쪽부터. 단 남은 마력에 들어가는 것만 본다.
                    // 예전에는 제일 좋은 것 하나만 보고 안 들어가면 그냥 그만뒀다. 그래서 마력 1이
                    // 남아도 정찰(마력 1)을 안 세우고 자리를 비워 뒀다.
                    var freeMagic = magic != null ? magic.MaxMagic - magic.UsedMagic : int.MaxValue;
                    _01.Code.Units.UnitDataSO unit = null; float best = -1f;
                    foreach (var candidate in roster.AvailableUnits) {
                        if (candidate == null || candidate.MagicCost > freeMagic) continue;
                        var value = dataValue(candidate);
                        if (value > best) { best = value; unit = candidate; }
                    }
                    if (unit == null) break;
                    int col, row;
                    if (!node.TryGetFirstFreeUnitSlot(out col, out row)) break;
                    if (_01.Code.Units.UnitDeployment.Deploy(node, unit, col, row, null, nodeCh, artCh) == null) break;
                    if (magic != null) costCh.RaiseEvent(new _01.Code.Events.UnitDeployMagicRequestedEvent(node, unit, unit.MagicCost));
                    deployed++;
                }
            }

            // 배치가 끝나면 전원에게 공격 명령을 준다.
            // 게임의 대응 힌트가 "공격 명령 유닛으로 치유사를 먼저 끊으세요"라고 적어 두었는데
            // 드라이버는 여태 전부 Standby로 뒀다. Assault는 후열(치유사·궁수)을 노린다.
            int commanded = 0;
            foreach (var u in UnityEngine.Object.FindObjectsByType<_01.Code.Units.Unit>(UnityEngine.FindObjectsSortMode.None))
            {
                if (u == null || u is _01.Code.Units.MainUnit) continue;
                if (u.CurrentCommand == _01.Code.Units.UnitCommand.Assault) continue;
                u.SetCommand(_01.Code.Units.UnitCommand.Assault);
                commanded++;
            }

            // 함정 — 유닛 배치가 끝난 뒤에 남은 칸을 채운다. 순서를 바꾸면 함정이 자리를 먹어
            // 유닛을 못 세운다. 창 함정 10G인데 드라이버가 여태 하나도 안 지었다.
            int traps = 0;
            {
                var trapData = UnityEditor.AssetDatabase.LoadAssetAtPath<_01.Code.Buildings.BuildingDataSO>(
                    "Assets/03.SO/Buildings/SpikeTrapBuildingData.asset");
                if (trapData != null)
                {
                    foreach (var node in rooms)
                    {
                        if (node == null || node.TrapGrid == null) continue;
                        // 방마다 두 개까지만. 다 채우면 다음 웨이브에 유닛 세울 칸이 없다.
                        for (int k = 0; k < 2; k++)
                        {
                            // 유보액을 60에서 15로 낮춘다. 함정 건설이 관리 단계 맨 끝이라 남는 돈이 보통 40~60인데
                            // 문턱이 70이면 한 번도 못 짓는다(2026-09-01 진단: 2~8일차 함정=0).
                            if (cost.CurrentGold - saveBudget < trapData.Cost) break;   // 몫을 미리 뗐으니 여기서는 끝까지 쓴다
                            int tc, tr;
                            if (!node.TrapGrid.TryGetNearestFreeCell(node.transform.position, out tc, out tr)) break;
                            costCh.RaiseEvent(new _01.Code.Events.BuildCostRequestedEvent(node, trapData.Cost));
                            if (_01.Code.Buildings.BuildingPlacement.InstallOnCell(node, tc, tr, trapData) == null) break;
                            traps++;
                        }
                    }
                }
            }

            // 남는 금화는 금고에 예치한다.
            //
            // 여태 드라이버가 한 번도 예치를 안 해서 금고 잔액이 늘 0이었고, 그래서
            // "예치하면 이자, 대신 침입자에게 털린다"는 위험·보상 층이 측정에 들어온 적이 없다.
            // 운영 자금은 다음 날 물약·보수용으로 조금 남긴다.
            int deposited = 0;
            {
                const int operatingReserve = 20;
                foreach (var tr in UnityEngine.Object.FindObjectsByType<_01.Code.Buildings.Treasury>(UnityEngine.FindObjectsSortMode.None))
                {
                    var surplus = UnityEngine.Mathf.Min(saveBudget, cost.CurrentGold - operatingReserve);
                    if (surplus <= 0) break;
                    deposited += tr.DepositFromOperatingFunds(surplus);
                }
            }
            int vault = 0;
            foreach (var tr in UnityEngine.Object.FindObjectsByType<_01.Code.Buildings.Treasury>(UnityEngine.FindObjectsSortMode.None))
                vault += tr.StoredGold;
            var hp = player != null && player.Health != null ? player.Health.CurrentHealth + "/" + player.Health.MaxHealth : "-";
            var here = merchant != null && merchant.IsMerchantHere ? "상인O" : "상인X";
            return "포탈=" + portals + " 방=" + built + " 함정=" + traps + " 경제=" + economy + " 고용=" + hired + " 배치=" + deployed + " 교체=" + swapped
                + " 물약=" + potions + " 예치=" + deposited + " 금고=" + vault + " " + here + " 금화=" + cost.CurrentGold + " 주인공=" + hp;
        };

        // ── 한 판 결과 한 줄 ─────────────────────────────────────────────────
        System.Func<string> Record = () =>
        {
            var wm = _01.Code.Manager.WaveManager.Current;
            var dm = _01.Code.Manager.DayManager.Current;
            var c = _01.Code.Manager.CostManager.Current;
            var mo = _01.Code.Manager.MoralePolicyManager.Current;
            var units = UnityEngine.Object.FindObjectsByType<_01.Code.Units.Unit>(UnityEngine.FindObjectsSortMode.None);
            int alive = 0; int hp = 0; int hpMax = 0;
            foreach (var u in units)
            {
                if (u == null) continue;
                if (u.Combatant != null && u.Combatant.IsAlive) alive++;
                // 파괴 예정인 유닛은 Health가 이미 떨어져 있을 수 있다. 여기서 터지면 그날 표본을 통째로 잃는다.
                if (u.Health != null) { hp += u.Health.CurrentHealth; hpMax += u.Health.MaxHealth; }
            }
            return dm.CurrentDay + "|" + wm.TotalEnemyCount + "|" + wm.KillCount + "|" + wm.WaveDamageDealt + "|" + wm.WaveDamageTaken
              + "|" + wm.WaveCriticalHits + "|" + c.CurrentGold + "|" + c.CurrentDebt + "|" + mo.CurrentMorale
              + "|" + units.Length + "|" + alive + "|" + hp + "/" + hpMax + "|" + (wm.IsBossWave ? "BOSS" : "-")
              + "|" + (_01.Code.Manager.DefenseStreakSystem.Current != null ? _01.Code.Manager.DefenseStreakSystem.Current.CurrentStreak : -1)
              + "|" + (_01.Code.Manager.DefenseStreakSystem.Current != null ? _01.Code.Manager.DefenseStreakSystem.Current.ExtraEnemies : -1);
        };


        // ── 배치 루프 ────────────────────────────────────────────────────────
        // 시드를 바꿔 가며 20일 런을 여러 번 돌린다. 한 판이 끝나면 세이브를 지우고
        // 씬을 다시 올려 다음 시드로 넘어간다. EditorApplication.update는 씬 로드에도 살아남는다.
        //
        // 앞의 두 시드를 일부러 같은 값으로 뒀다 — 시드 고정만으로 재현이 되는지부터 보려는 것이다.
        // 공격 타이머가 Time.deltaTime으로 도니 완전히 같기를 기대하진 않는다.
        int[] seeds = { 1, 1, 2, 3 };
        int seedIndex = 0;
        string phase = "load";
        int framesInPhase = 0;
        // ── 참여 계측 ────────────────────────────────────────────────────────
        // 종료 일차가 아니라 "이 콘텐츠가 한 번이라도 판에 나왔는가"를 센다.
        // 어제 넣은 권능 4개 중 둘이 죽어 있었다(적중률 50%). 나머지도
        // "작동은 하는데 아무도 안 쓰는" 형태로 남아 있을 수 있고, 그건 없는 것과 같다.
        // 0이 찍히는 항목이 곧 고칠 대상이다.
        var powerCasts = new System.Collections.Generic.Dictionary<string,int>();
        var policyOffers = new System.Collections.Generic.Dictionary<string,int>();
        var policyPicks = new System.Collections.Generic.Dictionary<string,int>();
        var enemySeen = new System.Collections.Generic.Dictionary<string,int>();
        System.Action<System.Collections.Generic.Dictionary<string,int>, string> Bump = (d, k) =>
        { if (string.IsNullOrEmpty(k)) return; d[k] = d.ContainsKey(k) ? d[k] + 1 : 1; };
        System.Func<System.Collections.Generic.Dictionary<string,int>, string> Dump = d =>
        {
            if (d.Count == 0) return "(없음)";
            var keys = new System.Collections.Generic.List<string>(d.Keys);
            keys.Sort((a,b) => d[b].CompareTo(d[a]));
            var s = new System.Text.StringBuilder();
            foreach (var k in keys) s.Append(k + "×" + d[k] + " ");
            return s.ToString();
        };

        int lastRecordedDay = 0;
        int lastManagedDay = -1;
        int settleFrames = 0;
        int startRetries = 0;
        var runDeadline = System.DateTime.UtcNow;
        var batchDeadline = System.DateTime.UtcNow.AddMinutes(150);

        System.Action WipeSaves = () =>
        {
            foreach (var name in new[] { "defence-run-v2.json", "defence-run-v2.backup.json" })
            {
                var p = System.IO.Path.Combine(UnityEngine.Application.persistentDataPath, name);
                if (System.IO.File.Exists(p)) System.IO.File.Delete(p);
            }
        };

        UnityEditor.EditorApplication.CallbackFunction step = null;
        step = () =>
        {
            try
            {
                if (System.IO.File.Exists(stopFlag) || !UnityEngine.Application.isPlaying
                    || System.DateTime.UtcNow > batchDeadline)
                { Say("BATCH_HALT"); UnityEditor.EditorApplication.update -= step; return; }

                if (UnityEngine.Time.timeScale < 7.9f) UnityEngine.Time.timeScale = 8f;

                var dm = _01.Code.Manager.DayManager.Current;
                var wm = _01.Code.Manager.WaveManager.Current;
                framesInPhase++;

                if (phase == "load")
                {
                    // 씬이 올라와 매니저가 살아나기를 기다린다.
                    if (dm == null || wm == null || framesInPhase < 60) return;
                    UnityEngine.Random.InitState(seeds[seedIndex]);
                    lastRecordedDay = 0; lastManagedDay = -1; settleFrames = 0; startRetries = 0;
                    runDeadline = System.DateTime.UtcNow.AddMinutes(30);
                    Say("=== SEED " + seeds[seedIndex] + " (" + (seedIndex + 1) + "/" + seeds.Length + ") ===");
                    phase = "run"; framesInPhase = 0;
                    return;
                }

                if (phase == "reset")
                {
                    if (framesInPhase < 30) return;
                    WipeSaves();
                    seedIndex++;
                    if (seedIndex >= seeds.Length)
                    Say("=== 참여 집계 (배치 전체 누적) ===");
                    Say("권능 시전: " + Dump(powerCasts));
                    Say("정책 후보: " + Dump(policyOffers));
                    Say("정책 선택: " + Dump(policyPicks));
                    Say("적 조우:   " + Dump(enemySeen));
                    Say("BATCH_DONE");
                    UnityEngine.SceneManagement.SceneManager.LoadScene(
                        UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
                    phase = "load"; framesInPhase = 0;
                    return;
                }

                // ── 한 판 진행 ──
                if (dm == null || wm == null) return;

                System.Action<string> EndRun = why =>
                {
                    string row;
                    try { row = Record(); } catch (System.Exception ex) { row = "RECORD_FAIL " + ex.GetType().Name; }
                    Say(seeds[seedIndex] + "|" + row);
                    Say("--- SEED " + seeds[seedIndex] + " 종료: " + why + " (" + dm.CurrentDay + "일차)");
                    phase = "reset"; framesInPhase = 0;
                };

                if (System.DateTime.UtcNow > runDeadline) { EndRun("시간초과"); return; }

                var over = UnityEngine.Object.FindAnyObjectByType<_01.Code.Manager.GameOverManager>();
                if (over != null && over.IsGameOver) { EndRun("게임오버"); return; }

                // 모달이 떠 있으면 timeScale이 0으로 묶인다. 첫 정책을 골라 닫는다.
                var policy = _01.Code.UI.PolicyChoicePanelView.Current;
                if (policy != null && policy.IsPanelOpen)
                {
                    var buttons = (UnityEngine.UI.Button[])typeof(_01.Code.UI.PolicyChoicePanelView)
                        .GetField("policyButtons", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        .GetValue(policy);
                    // 어떤 정책이 후보로 떴고 무엇을 골랐는지 — 10개를 넣어도 안 뜨면 없는 것과 같다.
                    var choicesField = typeof(_01.Code.UI.PolicyChoicePanelView)
                        .GetField("currentChoices", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var choices = choicesField != null
                        ? choicesField.GetValue(policy) as System.Collections.Generic.IList<_01.Code.Manager.PolicyDataSO>
                        : null;
                    if (choices != null)
                    {
                        for (int ci = 0; ci < choices.Count; ci++)
                            if (choices[ci] != null) Bump(policyOffers, choices[ci].DisplayName);
                        if (choices.Count > 0 && choices[0] != null) Bump(policyPicks, choices[0].DisplayName);
                    }
                    if (buttons != null && buttons.Length > 0 && buttons[0] != null) buttons[0].onClick.Invoke();
                }

                // 웨이브가 도는 동안 던전 권능을 쓴다. 게이지는 격퇴로 차고 웨이브 중에만 쓸 수 있어서
                // 안 쓰면 그대로 버려진다. 드라이버가 여태 한 번도 안 썼다.
                if (!dm.IsStandby || wm.IsWaveRunning)
                {
                    settleFrames = 0;
                    // 어떤 적을 실제로 만났는가. 정찰병처럼 후반 파티에만 넣은 적은
                    // 그 날까지 가는 런이 없으면 영영 안 나온다.
                    foreach (var en in UnityEngine.Object.FindObjectsByType<_01.Code.Enemies.Enemy>(UnityEngine.FindObjectsSortMode.None))
                        if (en != null && !enemySeen.ContainsKey(en.DisplayName)) Bump(enemySeen, en.DisplayName);

                    // 권능을 다시 켠다. 배치5~14는 꺼 둔 상태였다.
                    var power = _01.Code.Manager.DungeonPowerSystem.Current;
                    if (power != null && power.CurrentPower > 0 && power.Powers != null)
                    {
                        foreach (var p in power.Powers)
                        {
                            if (p == null || power.IsOnCooldown(p)) continue;
                            var cast = false;
                            foreach (var n in UnityEngine.Object.FindObjectsByType<_01.Code.MapCreateSystem.Node>(UnityEngine.FindObjectsSortMode.None))
                            {
                                string why;
                                if (!power.CanCast(p, n, out why)) continue;
                                if (power.TryCast(p, n, out why)) { cast = true; Bump(powerCasts, p.DisplayName); break; }
                            }
                            if (cast) break;   // 한 프레임에 하나만
                        }
                    }
                    return;
                }

                settleFrames++;
                if (settleFrames < 45) return;
                settleFrames = 0;

                if (dm.CurrentDay > lastRecordedDay)
                {
                    Say(seeds[seedIndex] + "|" + Record());
                    lastRecordedDay = dm.CurrentDay;
                }
                if (dm.CurrentDay >= 20) { EndRun("20일 완주"); return; }

                if (lastManagedDay != dm.CurrentDay)
                {
                    Say("# " + (dm.CurrentDay + 1) + "일차 " + Manage());   // 조건이 실제로 적용됐는지 보려면 이게 있어야 한다
                    lastManagedDay = dm.CurrentDay;
                    startRetries = 0;
                }

                var before = dm.CurrentDay;
                dm.StartWave();
                if (dm.CurrentDay != before) { startRetries = 0; return; }

                startRetries++;
                if (startRetries > 20) EndRun("StartWave 20회 실패");
            }
            catch (System.Exception e)
            {
                Say("ERR " + e.GetType().Name + " " + e.Message);
                UnityEditor.EditorApplication.update -= step;
            }
        };
        UnityEditor.EditorApplication.update += step;
        UnityEngine.Debug.Log("[밸런스] " + seeds.Length + "개 시드 실측 시작 -> " + log);

        }
    }
}
