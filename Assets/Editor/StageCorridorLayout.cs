using System;
using System.Collections.Generic;
using Code.Stages;
using UnityEngine;

/// <summary>
/// 함정 본편의 굽이치는 복도 맵(2026-11-03 사용자 결정: 던전 워페어처럼 3칸 폭 복도가 꺾여 문→코어 길이를 약 2배로).
/// 홀과 같은 39×15 크기·벽 띠(위 3줄, 아래·옆 1줄)를 지킨다. 위·아래 복도 사이 벽은 5줄이라 벽 그림이 이어지고 벽 함정 자리가 된다.
/// 문 수·여는 웨이브·지형 칸 기믹 종류·불 구멍/문/낙석은 원본 스테이지를 따르고 자리만 복도에 맞춰 옮긴다.
/// </summary>
public static class StageCorridorLayout
{
    public sealed class Map
    {
        public string[] Layout { get; }
        public Vector2Int[] Doors { get; }
        public Vector2Int Core { get; }
        public StageDevice[] Devices { get; }
        public string Gimmick { get; }

        public Map(string[] layout, Vector2Int[] doors, Vector2Int core, StageDevice[] devices, string gimmick)
        {
            Layout = layout;
            Doors = doors;
            Core = core;
            Devices = devices;
            Gimmick = gimmick;
        }
    }

    /// <summary>복도를 그리는 동안 쌓는 칸·장치. 원본 장치(불 구멍·문·낙석)는 종류별 순서대로 새 자리로 옮긴다.</summary>
    private sealed class Draft
    {
        public readonly char[][] Cells;
        public readonly List<StageDevice> Devices = new();
        private readonly StageDevice[] _legacy;

        public Draft(StageDevice[] legacy, bool connectorRight)
        {
            _legacy = legacy ?? Array.Empty<StageDevice>();
            Cells = new char[StageHallLayout.Height][];
            for (int y = 0; y < Cells.Length; y++)
            {
                Cells[y] = new char[StageHallLayout.Width];
                Array.Fill(Cells[y], '#');
            }
            Rect(1, 9, 36, 11, '.');
            Rect(1, 1, 36, 3, '.');
            if (connectorRight)
                Rect(34, 4, 36, 8, '.');
            else
                Rect(1, 4, 3, 8, '.');
        }

        public void Rect(int left, int bottom, int right, int top, char tile)
        {
            for (int y = bottom; y <= top; y++)
            for (int x = left; x <= right; x++)
                Cells[y][x] = tile;
        }

        /// <summary>복도 가장자리에서 벽을 파낸 구덩이와 그쪽으로 미는 장치(기존 함정 본편과 같은 임시 값).</summary>
        public void PushIntoPits(Vector2Int push, Vector2Int direction, int left, int bottom, int right, int top)
        {
            Rect(left, bottom, right, top, '.');
            for (int y = bottom; y <= top; y++)
            for (int x = left; x <= right; x++)
                Devices.Add(new StageDevice(StageDeviceKind.Pit, new Vector2Int(x, y), 6f, 0f, 0f, 0.5f, 0, 0f, 0f));
            Devices.Add(new StageDevice(StageDeviceKind.Push, push, 6f, 0f, 0f, 0.5f, 0, 1.4f, 8f, direction, 3));
        }

        /// <summary>원본의 같은 종류 장치를 순서대로 새 칸에 옮긴다. 원본보다 자리가 많으면 낙석만 새로 만든다.</summary>
        public void Move(StageDeviceKind kind, params Vector2Int[] cells)
        {
            int index = 0;
            foreach (StageDevice device in _legacy)
            {
                if (device == null || device.Kind != kind)
                    continue;
                if (index >= cells.Length)
                    throw new InvalidOperationException($"{kind} 자리가 원본 장치 수보다 적습니다");
                Devices.Add(new StageDevice(device.Kind, cells[index++], device.Period, device.ActiveSeconds, device.StartDelay,
                    device.TickInterval, device.Damage, device.Radius, device.Cooldown, device.Direction, device.PushCells));
            }
            for (; index < cells.Length; index++)
            {
                if (kind != StageDeviceKind.Rockfall)
                    throw new InvalidOperationException($"{kind} 자리가 원본 장치 수보다 많습니다");
                // 원본에 없던 후반 낙석은 기존 함정 본편 생성기와 같은 임시 값
                Devices.Add(new StageDevice(StageDeviceKind.Rockfall, cells[index], 6f, 0f, 0f, 0.5f, 8, 1.6f, 12f));
            }
        }

        public Map Finish(Vector2Int[] doors, Vector2Int core, string gimmick)
        {
            string[] rows = new string[Cells.Length];
            for (int y = 0; y < Cells.Length; y++)
                rows[y] = new string(Cells[y]);
            return new Map(rows, doors, core, Devices.ToArray(), gimmick);
        }
    }

    /// <param name="legacyDevices">원본 스테이지의 환경 장치. 불 구멍·문·낙석 설정을 그대로 옮긴다.</param>
    public static bool TryCreate(int number, StageDevice[] legacyDevices, out Map map)
    {
        map = number switch
        {
            1 => StageOne(legacyDevices),
            2 => StageTwo(legacyDevices),
            3 => StageThree(legacyDevices),
            4 => StageFour(legacyDevices),
            5 => StageFive(legacyDevices),
            6 => StageSix(legacyDevices),
            7 => StageSeven(legacyDevices),
            _ => null
        };
        return map != null;
    }

    // 아래 그림 설명의 위·아래 복도는 y 9~11·1~3, 꺾임은 x 34~36(오른쪽) 또는 1~3(왼쪽)이다.

    /// <summary>문(왼쪽 위)→오른쪽 꺾임→코어(왼쪽 아래), 약 73칸. 위 복도 가운데 밀치기로 아래 구덩이에 떨어뜨린다.</summary>
    private static Map StageOne(StageDevice[] legacy)
    {
        Draft draft = new(legacy, true);
        draft.PushIntoPits(new Vector2Int(26, 10), Vector2Int.down, 25, 7, 27, 8);
        return draft.Finish(new[] { new Vector2Int(2, 10) }, new Vector2Int(2, 2),
            "꺾이는 복도를 지나는 적을 밀치기로 구덩이에 떨어뜨리세요");
    }

    /// <summary>둘째 문은 3웨이브부터 꺾임 한가운데에서 열려 길이 절반이다. 위·아래 복도에 밀치기 하나씩.</summary>
    private static Map StageTwo(StageDevice[] legacy)
    {
        Draft draft = new(legacy, true);
        draft.PushIntoPits(new Vector2Int(20, 10), Vector2Int.down, 19, 7, 21, 8);
        draft.PushIntoPits(new Vector2Int(14, 2), Vector2Int.up, 13, 4, 15, 5);
        return draft.Finish(new[] { new Vector2Int(2, 10), new Vector2Int(36, 6) }, new Vector2Int(2, 2),
            "3웨이브부터 꺾임에서 열리는 짧은 길을 아래 복도 밀치기로 막으세요");
    }

    /// <summary>오른쪽 위 문→왼쪽 꺾임→코어(오른쪽 아래). 강화 칸·설치 금지·물웅덩이를 복도에 나눠 둔다.</summary>
    private static Map StageThree(StageDevice[] legacy)
    {
        Draft draft = new(legacy, false);
        draft.Rect(16, 9, 18, 10, '+');
        draft.Rect(30, 10, 33, 11, 'x');
        draft.Rect(6, 9, 7, 10, '~');
        draft.Rect(20, 1, 21, 2, '~');
        draft.Rect(2, 5, 3, 6, '~');
        draft.PushIntoPits(new Vector2Int(24, 10), Vector2Int.down, 23, 7, 25, 8);
        draft.PushIntoPits(new Vector2Int(12, 2), Vector2Int.up, 11, 4, 13, 5);
        draft.Move(StageDeviceKind.Rockfall, new Vector2Int(28, 2));
        return draft.Finish(new[] { new Vector2Int(36, 10) }, new Vector2Int(36, 2),
            "강화 칸에 함정을 잇고 밀치기 다음 낙석으로 마무리하세요");
    }

    /// <summary>불 구멍 둘·꺾임의 여닫는 문(한 칸이라 닫혀도 옆으로 지나간다)·풀섬.</summary>
    private static Map StageFour(StageDevice[] legacy)
    {
        Draft draft = new(legacy, true);
        draft.Rect(16, 9, 17, 10, 'g');
        draft.Rect(22, 2, 23, 3, 'g');
        draft.Rect(34, 8, 35, 9, 'g');
        draft.PushIntoPits(new Vector2Int(26, 10), Vector2Int.down, 25, 7, 27, 8);
        draft.PushIntoPits(new Vector2Int(8, 2), Vector2Int.up, 7, 4, 9, 5);
        draft.Move(StageDeviceKind.FireVent, new Vector2Int(12, 10), new Vector2Int(28, 2));
        draft.Move(StageDeviceKind.Gate, new Vector2Int(35, 5));
        draft.Move(StageDeviceKind.Rockfall, new Vector2Int(18, 2));
        return draft.Finish(new[] { new Vector2Int(2, 10) }, new Vector2Int(2, 2),
            "불 구멍과 여닫는 문 주기에 맞춰 구덩이 앞에 적을 모으세요");
    }

    /// <summary>오른쪽 위 문과 왼쪽 꺾임 가운데 문이 함께 열린다. 암반 칸.</summary>
    private static Map StageFive(StageDevice[] legacy)
    {
        Draft draft = new(legacy, false);
        draft.Rect(10, 9, 11, 10, 'r');
        draft.Rect(26, 9, 27, 10, 'r');
        draft.Rect(26, 1, 27, 2, 'r');
        draft.PushIntoPits(new Vector2Int(16, 10), Vector2Int.down, 15, 7, 17, 8);
        draft.PushIntoPits(new Vector2Int(22, 2), Vector2Int.up, 21, 4, 23, 5);
        draft.Move(StageDeviceKind.Rockfall, new Vector2Int(31, 2));
        return draft.Finish(new[] { new Vector2Int(36, 10), new Vector2Int(2, 6) }, new Vector2Int(36, 2),
            "긴 길과 짧은 길의 적을 바리케이드로 아래 복도에 모으고 장치를 번갈아 쓰세요");
    }

    /// <summary>세 문(셋째는 3웨이브부터 위 복도 가운데). 불 구멍·문·낙석·기름·물웅덩이.</summary>
    private static Map StageSix(StageDevice[] legacy)
    {
        Draft draft = new(legacy, true);
        draft.Rect(22, 10, 25, 11, 'o');
        draft.Rect(6, 1, 9, 2, '~');
        draft.PushIntoPits(new Vector2Int(29, 10), Vector2Int.down, 28, 7, 30, 8);
        draft.PushIntoPits(new Vector2Int(20, 2), Vector2Int.up, 19, 4, 21, 5);
        draft.Move(StageDeviceKind.FireVent, new Vector2Int(10, 10), new Vector2Int(26, 2));
        draft.Move(StageDeviceKind.Gate, new Vector2Int(35, 5));
        draft.Move(StageDeviceKind.Rockfall, new Vector2Int(14, 2));
        return draft.Finish(new[] { new Vector2Int(2, 10), new Vector2Int(36, 6), new Vector2Int(18, 11) },
            new Vector2Int(2, 2), "세 입구·주기 장치 사이에서 밀치기와 낙석 시점을 선택하세요");
    }

    /// <summary>세 문·모든 지형 칸·불 구멍·문 둘·낙석이 모인 마지막 복도.</summary>
    private static Map StageSeven(StageDevice[] legacy)
    {
        Draft draft = new(legacy, false);
        draft.Rect(30, 10, 33, 11, 'x');
        draft.Rect(14, 9, 16, 10, '+');
        draft.Rect(6, 9, 7, 10, '~');
        draft.Rect(16, 2, 17, 3, 'g');
        draft.Rect(20, 1, 21, 2, 'o');
        draft.Rect(32, 2, 33, 3, 'r');
        draft.PushIntoPits(new Vector2Int(12, 10), Vector2Int.down, 11, 7, 13, 8);
        draft.PushIntoPits(new Vector2Int(24, 2), Vector2Int.up, 23, 4, 25, 5);
        draft.Move(StageDeviceKind.FireVent, new Vector2Int(24, 10), new Vector2Int(10, 2));
        draft.Move(StageDeviceKind.Gate, new Vector2Int(2, 4), new Vector2Int(2, 8));
        draft.Move(StageDeviceKind.Rockfall, new Vector2Int(28, 2));
        return draft.Finish(new[] { new Vector2Int(36, 10), new Vector2Int(2, 6), new Vector2Int(18, 11) },
            new Vector2Int(36, 2), "보스는 구덩이에 떨어지지 않습니다 · 호위를 떨어뜨리고 보스는 함정으로 잡으세요");
    }
}
