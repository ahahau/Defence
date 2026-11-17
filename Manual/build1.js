const PDFDocument = require("pdfkit");
const fs = require("fs");

const FONT = "../Assets/08.Font/The Jamsil 5 Bold.ttf";
const ART = "art/";

// 게임 화면의 색을 그대로 가져왔다. 돌벽의 자주빛 검정과 UI의 황동색.
const NIGHT = "#140E1B";      // 페이지 바탕
const STONE = "#241A2E";      // 패널
const STONE2 = "#2E2238";     // 표 머리글
const GOLD = "#C79A50";
const GOLD_DIM = "#8A6B36";
const PAPER = "#EDE4D6";      // 본문 글자
const FAINT = "#A295A8";      // 흐린 글자
const BOSS = "#4A1E22";       // 보스 날 줄

const PW = 595.28, PH = 841.89;
const M = 24;
const GUTTER = 12;
const COLW = (PW - M * 2 - GUTTER) / 2;
const COL_TOP = 128;
const BOTTOM = PH - M - 8;

const doc = new PDFDocument({
  size: "A4",
  margin: 0,
  info: { Title: "던전 키퍼 · 한 장 설명서", Author: "Defence" },
});
doc.registerFont("kr", FONT);
doc.pipe(fs.createWriteStream("던전키퍼_설명서_1장.pdf"));

let col = 0, y = COL_TOP, overflow = 0;
const colX = () => M + col * (COLW + GUTTER);

function room(h) {
  if (y + h <= BOTTOM) return;
  if (col === 0) { col = 1; y = COL_TOP; }
  else overflow += h;
}

/**
 * 제목 띠. 바로 그리지 않고 예약만 한다 — 뒤따르는 표가 어느 단으로 갈지 정해진 뒤
 * 그 자리에 함께 그려야 제목만 단 끝에 홀로 남지 않는다.
 */
let pending = null;
const HEADING_H = 17;
function heading(text, icon) { pending = { text, icon }; }

function drawHeading() {
  if (!pending) return;
  const { text, icon } = pending;
  pending = null;
  doc.save().rect(colX(), y, COLW, 13).fill(STONE2).restore();
  doc.save().rect(colX(), y, 2.5, 13).fill(GOLD).restore();
  doc.font("kr").fontSize(7.4).fillColor(GOLD)
     .text(text, colX() + 8, y + 3.2, { width: COLW - 30, lineBreak: false });
  if (icon) doc.image(ART + icon + ".png", colX() + COLW - 14, y - 0.5, { width: 13 });
  y += 13 + 4;
}

function para(text, size = 6.3, color = FAINT) {
  doc.font("kr").fontSize(size);
  const h = doc.heightOfString(text, { width: COLW, lineGap: 1.4 });
  if (y + h + (pending ? HEADING_H : 0) > BOTTOM && col === 0) { col = 1; y = COL_TOP; }
  drawHeading();
  doc.fillColor(color).text(text, colX(), y, { width: COLW, lineGap: 1.4 });
  y += h + 4;
}

/** rows: 배열, 또는 {cells, tint, icon}. icon은 첫 칸 앞에 붙는 작은 그림. */
function table(header, cols, rows) {
  const total = cols.reduce((s, c) => s + c[0], 0);
  const w = cols.map((c) => (c[0] / total) * COLW);
  const PAD = 3;

  function head() {
    room(11);
    doc.save().rect(colX(), y, COLW, 10).fill(STONE2).restore();
    doc.font("kr").fontSize(5.8).fillColor(GOLD_DIM);
    let x = colX();
    header.forEach((t, i) => {
      doc.text(t, x + PAD, y + 2.6, { width: w[i] - PAD * 2, align: cols[i][1] || "left", lineBreak: false });
      x += w[i];
    });
    y += 10;
  }

  // 행 높이를 먼저 다 재서 표 전체가 이 단에 들어가는지 본다.
  // 한 줄만 다음 단으로 떨어지면 그 줄이 어느 표의 것인지 알 수 없게 된다.
  doc.font("kr").fontSize(6.1);
  const rowHs = rows.map((r) => {
    const cells = Array.isArray(r) ? r : r.cells;
    const icon = Array.isArray(r) ? null : r.icon;
    const indent = icon ? 11 : 0;
    const hs = cells.map((v, i) =>
      doc.heightOfString(String(v), { width: w[i] - PAD * 2 - (i === 0 ? indent : 0), lineGap: 1 }));
    return Math.max(Math.max(...hs) + 4.5, icon ? 12 : 0);
  });
  const tableH = 10 + rowHs.reduce((s, h) => s + h, 0) + (pending ? HEADING_H : 0);
  if (y + tableH > BOTTOM && col === 0) { col = 1; y = COL_TOP; }
  drawHeading();

  head();
  rows.forEach((r, ri) => {
    const cells = Array.isArray(r) ? r : r.cells;
    const tint = Array.isArray(r) ? null : r.tint;
    const icon = Array.isArray(r) ? null : r.icon;
    const indent = icon ? 11 : 0;
    const rh = rowHs[ri];

    const before = col;
    room(rh);
    if (col !== before) head();

    // 줄무늬를 아주 옅게 넣어 돌바닥 위에서도 행이 구분되게 한다.
    if (tint) doc.save().rect(colX(), y, COLW, rh).fill(tint).restore();
    else if (ri % 2 === 1) doc.save().opacity(0.35).rect(colX(), y, COLW, rh).fill(STONE).opacity(1).restore();

    let x = colX();
    cells.forEach((v, i) => {
      if (i === 0 && icon) doc.image(ART + icon + ".png", x + PAD, y + 1.2, { width: 9 });
      doc.font("kr").fontSize(6.1).fillColor(i === 0 ? PAPER : FAINT)
         .text(String(v), x + PAD + (i === 0 ? indent : 0), y + 2,
               { width: w[i] - PAD * 2 - (i === 0 ? indent : 0), align: cols[i][1] || "left", lineGap: 1 });
      x += w[i];
    });
    y += rh;
    doc.save().opacity(0.5).moveTo(colX(), y).lineTo(colX() + COLW, y)
       .lineWidth(0.3).strokeColor(GOLD_DIM).stroke().opacity(1).restore();
  });
  y += 5;
}

// ─────────────── 바탕 ───────────────
doc.save().rect(0, 0, PW, PH).fill(NIGHT).restore();
// 게임의 던전 배경을 아주 옅게 깔아 종이가 아니라 돌바닥처럼 보이게 한다.
doc.save().opacity(0.16).image(ART + "bg.png", 0, PH - 470, { width: PW }).opacity(1).restore();
doc.save().opacity(0.13).image(ART + "bg.png", 0, 100, { width: PW }).opacity(1).restore();

// ─────────────── 머리글 ───────────────
doc.save().rect(0, 0, PW, 112).fill("#0E0912").restore();
doc.save().opacity(0.30).image(ART + "bg.png", 0, -60, { width: PW }).opacity(1).restore();
doc.save().rect(0, 110, PW, 2).fill(GOLD).restore();

doc.image(ART + "demon.png", PW - M - 74, 16, { width: 74 });

doc.font("kr").fontSize(24).fillColor(PAPER).text("던전 키퍼", M, 22, { lineBreak: false });
doc.font("kr").fontSize(9).fillColor(GOLD).text("게임 설명서", M + 2, 52, { lineBreak: false });
doc.save().rect(M, 68, 44, 1.4).fill(GOLD_DIM).restore();
doc.font("kr").fontSize(7).fillColor("#B6A9BE").text(
  "당신은 던전의 주인입니다. 스무 날 동안 몬스터를 부려 던전을 털러 오는 모험가를 막아냅니다.\n" +
  "금을 벌어 방을 짓고 부하를 들이되, 매일 밤 장부가 돌아옵니다. 빚이 300을 넘으면 그날로 끝입니다.",
  M, 76, { width: PW - M * 2 - 90, lineGap: 1.6 });

// 침입자 미리보기 · 누구를 상대하는지 표지에서 바로 보이게 한다.
const foes = [["archer", "궁수"], ["sword", "용병 검사"], ["healer", "종군 사제"]];
let fx = PW - M - 74 - 128;
doc.font("kr").fontSize(5.4).fillColor(GOLD_DIM).text("침입자", fx, 20, { lineBreak: false });
foes.forEach(([f, label], i) => {
  const x = fx + i * 42;
  doc.image(ART + f + ".png", x, 30, { width: 26 });
  doc.font("kr").fontSize(5).fillColor(FAINT)
     .text(label, x - 5, 60, { width: 36, align: "center", lineBreak: false });
});

y = COL_TOP;

// ─────────────── 왼쪽 단 ───────────────
heading("하루의 흐름");
table(["단계", "하는 일"], [[0.9], [3.0]], [
  ["준비", "방·함정을 짓고 부하를 배치합니다. 상인 거래와 원정도 이때. 시간은 멈춰 있습니다."],
  ["습격", "모험가가 입구로 들어옵니다. 부하는 알아서 싸우고, 당신은 명령과 권능을 씁니다."],
  ["정산", "번 돈과 쓴 돈을 맞춥니다. 모자라면 빚이 됩니다."],
]);

heading("조작");
table(["조작", "기능"], [[1.0], [2.9]], [
  ["좌클릭", "빈 칸에 방을 짓거나, 지은 방에 부하를 배치"],
  ["우클릭", "부하를 누르면 상태창. 빈 바닥에서는 반응 없음"],
  ["X0·X1·X2", "배속. 길면 X2, 급하면 X0으로 정지"],
  ["설정", "왼쪽 위. 효과음·배경음악 음량을 따로 조절"],
]);

heading("자원");
table(["자원", "설명"], [[1.0], [2.9]], [
  ["운영 자금", "금(G). 건설·고용·유지비가 나갑니다."],
  ["주둔 마력", "동시에 부릴 수 있는 총량. 최대 8. 마을을 장악하면 오릅니다."],
  ["던전 권능", "습격 중에만 차오릅니다. 권능 시전에 쓰고, 끝나면 사라집니다."],
  ["민심", "0~100. 낮으면 유지비가 오르고 지원자가 줄어듭니다."],
  ["던전 악명", "그 구역에서 벌어진 전투와 함정 발동으로 쌓입니다."],
  ["결속", "중앙 핵심부 둘레에 건물을 모으면 단계가 올라 재보 보너스가 붙습니다."],
  ["연속 방어", "완벽히 막은 날이 이어지면 보상이 오르고 적도 늘어납니다. 끊기면 둘 다 0."],
]);

heading("몬스터", "slime_blue");
table(["몬스터", "금", "마력", "성격"], [[1.3], [0.4, "right"], [0.48, "right"], [2.4]], [
  { cells: ["블루 슬라임", "14", "1", "가장 싼 소모품. 머릿수로 시간을 법니다."], icon: "slime_blue" },
  { cells: ["그린 슬라임", "18", "1", "블루와 같은 자리를 다툽니다."], icon: "slime_green" },
  { cells: ["레드 슬라임", "20", "2", "셋 중 가장 단단합니다."], icon: "slime_red" },
  ["정찰병", "18", "1", "쿨이 가장 짧습니다. 뒤로 파고들어 무르게 만듭니다."],
  ["창병", "28", "2", "방어를 깎아 뒤에 선 딜러의 피해를 키웁니다."],
  ["석궁병", "42", "2", "독과 둔화로 발을 묶는 주력 원거리."],
  ["수호병", "48", "3", "버티며 성역으로 아군을 회복시키는 유일한 회복원."],
  ["전투 마도사", "64", "4", "예고한 자리를 터뜨리는 광역 딜러."],
  ["선봉대", "82", "5", "혼자 마력 5. 밀어내서 대열을 흩뜨립니다."],
]);

// 아래부터는 남는 자리를 따라 자연스럽게 오른쪽 단으로 넘어간다.
heading("던전 권능 · 습격 중에만");
table(["권능", "비용", "쿨", "효과"], [[1.15], [0.42, "right"], [0.4, "right"], [2.3]], [
  ["돌팔매", "12", "4초", "값싼 돌무더기. 자주 씁니다."],
  ["약점 노출", "20", "10초", "갑주를 벌려 이어지는 피해를 키웁니다."],
  ["낙석", "25", "7초", "구역의 침입자 전원에게 피해."],
  ["어둠의 축복", "30", "12초", "구역의 부하를 회복시킵니다."],
  ["광란의 북", "30", "15초", "구역의 부하를 광란 상태로."],
  ["서리 손아귀", "35", "14초", "침입자의 발을 묶습니다."],
  ["낙반", "40", "18초", "천장을 무너뜨려 6초간 길을 끊습니다."],
  ["지하수 범람", "60", "24초", "물길을 텁니다. 가장 강한 한 방."],
]);

heading("모험가 · 쳐들어오는 쪽", "sword");
table(["모험가", "성격"], [[1.2], [2.7]], [
  { cells: ["떠돌이 모험가", "가장 흔한 잡졸. 머릿수로 밀고 들어옵니다."], icon: "archer" },
  ["척후병", "뒤로 파고들어 후열을 노립니다."],
  { cells: ["용병 검사", "앞줄에서 버티며 부하를 밀어냅니다."], icon: "sword" },
  { cells: ["궁수", "뒤에서 정밀 사격. 화살비로 전체를 덮습니다."], icon: "archer" },
  { cells: ["종군 사제", "동료를 회복시킵니다. 먼저 끊지 않으면 앞줄이 무너집니다."], icon: "healer" },
  ["관광객 3종", "겁많은·부유한·순례. 잘 싸우지 않지만 잡으면 값이 됩니다."],
  { cells: ["보스 4종", "토벌대장 · 탐욕의 기사 · 사냥꾼 두목 · 왕국 기사단장"], tint: BOSS },
]);

heading("건물과 함정", "portal");
table(["건물", "금", "함정", "금"], [[1.15], [0.38, "right"], [1.1], [0.38, "right"]], [
  { cells: ["포탈 (먼저 필수)", "0", "기본 함정", "0"], icon: "portal" },
  ["벽", "8", "마름쇠 함정", "12"],
  { cells: ["주점", "10", "올가미 함정", "20"], icon: "inn" },
  { cells: ["상점", "15", "매복 그물", "22"], icon: "store" },
  { cells: ["광산", "20", "갑옷분쇄 함정", "32"], icon: "mine" },
  ["휴게실", "22", "가시 함정", "40"],
  ["큰 주점", "25", "처형 톱날", "46"],
  ["무기 상점", "30", "칼날 함정", "60"],
  ["깊은 광산", "35", "압쇄 함정", "90"],
]);
para("함정은 마력을 쓰지 않습니다. 마력이 다 찼는데 자금이 남았다면 함정에 쓰십시오.");

heading("스무 날 일정");
const waves = [
  [1, 2, 32], [2, 4, 40], [3, 5, 42], [4, 6, 48], [5, 8, 56], [6, 9, 64], [7, 11, 72],
  [8, 12, 82], [9, 4, 94, 1], [10, 15, 106], [11, 17, 120], [12, 8, 170, 1],
  [13, 20, 152], [14, 21, 170], [15, 23, 190], [16, 24, 200], [17, 25, 210],
  [18, 6, 225, 1], [19, 28, 240], [20, 8, 300, 1],
];
table(["일", "적", "보상", "일", "적", "보상"],
  [[0.32, "right"], [0.4, "right"], [0.54, "right"], [0.32, "right"], [0.4, "right"], [0.54, "right"]],
  Array.from({ length: 10 }, (_, i) => {
    const a = waves[i], b = waves[i + 10];
    return {
      cells: [`${a[0]}`, `${a[1]}`, `${a[2]}G`, `${b[0]}`, `${b[1]}`, `${b[2]}G`],
      tint: (a[3] || b[3]) ? BOSS : null,
    };
  }));
para("붉은 줄이 보스 날입니다 · 9일 토벌대장 · 12일 탐욕의 기사 · 18일 사냥꾼 두목 · 20일 왕국 기사단장. 머릿수가 적다고 방심하면 그날로 끝납니다.");

heading("정산 · 원정 · 상인 · 정책");
table(["항목", "요점"], [[0.85], [3.0]], [
  ["정산", "남으면 자금, 모자라면 빚. 이자가 붙고 300을 넘으면 파산."],
  ["금고", "넣어 둔 금은 이자를 받지만 도굴꾼이 노립니다."],
  ["원정", "부하를 마을로 보내 금·지원자·마력 상한을 얻습니다. 보낸 부하는 그날 방어에 못 씁니다."],
  ["상인", "정해진 날에만 옵니다. 같은 물건을 살수록 값이 오릅니다."],
  ["유물", "혼자보다 서로 맞는 것끼리 모을 때 값을 합니다."],
  ["정책", "두 갈래 중 하나. 얻는 것과 잃는 것이 함께 오고, 정책끼리 맞물리면 새 효과가 생깁니다."],
]);

heading("처음 하는 분에게");
para(
  "먼저 포탈을 놓아야 습격이 시작됩니다.   ·   초반엔 값싼 슬라임으로 머릿수를 채우고 번 돈으로 수입 건물을 늘리십시오.   ·   " +
  "적 사제를 먼저 끊지 못하면 앞줄이 아무리 강해도 밀립니다.   ·   9일차 보스 전에는 자금을 남겨 두십시오.   ·   " +
  "연속 방어가 버겁다 싶으면 한 번 끊고 숨을 돌리는 것도 방법입니다.", 6.3, PAPER);

doc.end();
process.on("exit", () => {
  console.log(overflow > 0
    ? `경고: 내용이 ${Math.round(overflow)}pt 넘칩니다.`
    : "완료: 던전키퍼_설명서_1장.pdf  (넘침 없음)");
});
