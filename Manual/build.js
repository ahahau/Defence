const PDFDocument = require("pdfkit");
const fs = require("fs");

// 맑은 고딕. 한글 글리프가 있는 시스템 폰트라 임베딩하면 어디서 열어도 글자가 깨지지 않는다.
// 게임이 쓰는 글꼴 그대로. 0.9MB라 임베딩해도 파일이 무거워지지 않는다.
const FONT = "../Assets/08.Font/The Jamsil 5 Bold.ttf";
const FONT_BOLD = FONT;

const INK = "#1B1F2A";
const MUTED = "#5C6478";
const ACCENT = "#9C6A1E";
const RULE = "#D8DCE4";
const BAND = "#F1EEE7";
const BOSS = "#F6E3DF";

const M = 56;                 // 여백
const PW = 595.28, PH = 841.89;  // A4
const W = PW - M * 2;         // 본문 폭
const BOTTOM = PH - M;        // 본문이 끝나는 y

const doc = new PDFDocument({
  size: "A4",
  margins: { top: M, bottom: M, left: M, right: M },
  info: {
    Title: "던전 키퍼 — 게임 설명서",
    Author: "Defence",
    Subject: "게임 규칙과 조작 안내",
  },
});
doc.registerFont("kr", FONT);
doc.registerFont("krb", fs.existsSync(FONT_BOLD) ? FONT_BOLD : FONT);
doc.pipe(fs.createWriteStream("던전키퍼_게임설명서.pdf"));

let pageNo = 0;
function footer() {
  pageNo++;
  const y = PH - 40;
  // 푸터를 그리면 텍스트 커서가 페이지 밑으로 내려간다. 그대로 두면 다음 요소마다 새 페이지가 생긴다.
  const savedX = doc.x, savedY = doc.y;
  const keep = doc.page.margins.bottom;
  doc.page.margins.bottom = 0;   // 이걸 안 끄면 푸터가 여백을 넘겨 페이지를 또 만든다
  doc.save();
  doc.moveTo(M, y - 10).lineTo(M + W, y - 10).lineWidth(0.5).strokeColor(RULE).stroke();
  doc.font("kr").fontSize(8).fillColor(MUTED)
     .text("던전 키퍼 · 게임 설명서", M, y, { width: W / 2, align: "left" })
     .text(String(pageNo), M + W / 2, y, { width: W / 2, align: "right" });
  doc.restore();
  doc.page.margins.bottom = keep;
  doc.x = savedX; doc.y = savedY;
}
doc.on("pageAdded", footer);

/** 남은 높이가 모자라면 새 페이지로 넘긴다. 표가 페이지 경계에서 잘리는 것을 막는다. */
function ensure(space) {
  if (doc.y + space > BOTTOM - 24) doc.addPage();
}

function h1(text) {
  ensure(60);
  doc.moveDown(0.6);
  doc.font("krb").fontSize(17).fillColor(INK).text(text, M, doc.y);
  const y = doc.y + 5;
  doc.moveTo(M, y).lineTo(M + W, y).lineWidth(1.4).strokeColor(ACCENT).stroke();
  doc.y = y + 12;
}

function h2(text) {
  ensure(46);
  doc.moveDown(0.5);
  doc.font("krb").fontSize(11.5).fillColor(ACCENT).text(text, M, doc.y);
  doc.moveDown(0.35);
}

function body(text, opts = {}) {
  ensure(30);
  doc.font("kr").fontSize(9.8).fillColor(opts.muted ? MUTED : INK)
     .text(text, M, doc.y, { width: W, align: "left", lineGap: 2.6 });
  doc.moveDown(0.45);
}

function bullets(items) {
  items.forEach((t) => {
    ensure(24);
    doc.font("kr").fontSize(9.8).fillColor(INK);
    doc.text("·", M + 3, doc.y, { width: 10, continued: false });
    doc.moveUp(1);
    doc.text(t, M + 16, doc.y, { width: W - 16, lineGap: 2.4 });
    doc.moveDown(0.2);
  });
  doc.moveDown(0.3);
}

/**
 * 표. cols는 [제목, 폭비율, 정렬].
 * 행 높이를 내용에 맞춰 재므로 긴 설명이 들어와도 칸을 넘지 않는다.
 */
function table(cols, rows, opts = {}) {
  const totalRatio = cols.reduce((s, c) => s + c[1], 0);
  const widths = cols.map((c) => (c[1] / totalRatio) * W);
  const xs = [];
  let acc = M;
  widths.forEach((w) => { xs.push(acc); acc += w; });
  const PAD = 5;

  function header() {
    ensure(34);
    const y0 = doc.y;
    doc.save().rect(M, y0, W, 20).fill(BAND).restore();
    doc.font("krb").fontSize(8.6).fillColor(MUTED);
    cols.forEach((c, i) => {
      doc.text(c[0], xs[i] + PAD, y0 + 6, { width: widths[i] - PAD * 2, align: c[2] || "left" });
    });
    doc.y = y0 + 20;
    doc.moveTo(M, doc.y).lineTo(M + W, doc.y).lineWidth(0.7).strokeColor(RULE).stroke();
  }

  header();
  rows.forEach((r) => {
    const cells = Array.isArray(r) ? r : r.cells;
    const tint = Array.isArray(r) ? null : r.tint;

    doc.font("kr").fontSize(9);
    const heights = cells.map((v, i) =>
      doc.heightOfString(String(v), { width: widths[i] - PAD * 2, lineGap: 1.6 }));
    const rowH = Math.max(...heights) + 10;

    if (doc.y + rowH > BOTTOM - 26) { doc.addPage(); header(); }

    const y0 = doc.y;
    if (tint) doc.save().rect(M, y0, W, rowH).fill(tint).restore();

    cells.forEach((v, i) => {
      doc.font(i === 0 && opts.boldFirst !== false ? "krb" : "kr")
         .fontSize(9).fillColor(INK)
         .text(String(v), xs[i] + PAD, y0 + 5,
               { width: widths[i] - PAD * 2, align: cols[i][2] || "left", lineGap: 1.6 });
    });

    doc.y = y0 + rowH;
    doc.moveTo(M, doc.y).lineTo(M + W, doc.y).lineWidth(0.4).strokeColor(RULE).stroke();
  });
  doc.moveDown(0.7);
}

function note(title, text) {
  ensure(64);
  const pad = 10;
  doc.font("kr").fontSize(9.3);
  const h = doc.heightOfString(text, { width: W - pad * 2 - 4, lineGap: 2.4 }) + 30;
  const y0 = doc.y;
  doc.save().rect(M, y0, W, h).fill("#FBF7EF").restore();
  doc.save().rect(M, y0, 3, h).fill(ACCENT).restore();
  doc.font("krb").fontSize(9.6).fillColor(ACCENT).text(title, M + pad, y0 + 8, { width: W - pad * 2 });
  doc.font("kr").fontSize(9.3).fillColor(INK)
     .text(text, M + pad, doc.y + 2, { width: W - pad * 2 - 4, lineGap: 2.4 });
  doc.y = y0 + h + 10;
}

// ─────────────────────────── 표지 ───────────────────────────
footer();
doc.font("krb").fontSize(34).fillColor(INK).text("던전 키퍼", M, 150);
doc.font("kr").fontSize(13).fillColor(ACCENT).text("게임 설명서", M, doc.y + 4);
doc.moveTo(M, doc.y + 14).lineTo(M + 150, doc.y + 14).lineWidth(2).strokeColor(ACCENT).stroke();
doc.y += 40;
doc.font("kr").fontSize(11).fillColor(INK).text(
  "당신은 던전의 주인입니다. 스무 날 동안 몬스터를 부려 던전을 털러 오는 모험가를 막아냅니다.",
  M, doc.y, { width: W - 60, lineGap: 5 });
doc.moveDown(1.2);
doc.font("kr").fontSize(9.6).fillColor(MUTED).text(
  "금을 벌어 방을 짓고 부하를 들이되, 매일 밤 장부가 돌아옵니다. " +
  "버는 것보다 쓰는 것이 많으면 빚이 쌓이고, 빚이 한도를 넘으면 그날로 끝입니다.",
  M, doc.y, { width: W - 60, lineGap: 4 });

doc.y = 640;
doc.font("krb").fontSize(9.6).fillColor(INK).text("이 문서에 담긴 것", M, doc.y);
doc.moveDown(0.4);
doc.font("kr").fontSize(9.2).fillColor(MUTED).text(
  "하루의 흐름 · 조작법 · 자원 · 몬스터 · 건물과 함정 · 던전 권능 · 스무 날 일정 · 정산과 부채 · 그 밖의 것들",
  M, doc.y, { width: W - 60, lineGap: 4 });

// ─────────────────────────── 본문 ───────────────────────────
doc.addPage();

h1("1. 하루의 흐름");
body("하루는 세 단계로 돌아갑니다. 스무 날을 버티면 승리합니다.");
table(
  [["단계", 1.1], ["하는 일", 3.2]],
  [
    ["준비", "방을 짓고 함정을 놓고 몬스터를 배치합니다. 상인이 왔다면 이때 거래하고, 원정을 보낼 수도 있습니다. 시간은 흐르지 않습니다."],
    ["습격", "모험가가 입구로 들어옵니다. 부하가 알아서 싸우고, 당신은 명령을 바꾸거나 던전 권능을 씁니다."],
    ["정산", "그날 번 돈과 쓴 돈을 맞춥니다. 모자라면 빚이 됩니다."],
  ]);

h1("2. 조작");
table(
  [["조작", 1.2], ["기능", 3.1]],
  [
    ["좌클릭", "빈 칸에 방을 짓거나, 지은 방에 몬스터를 배치합니다."],
    ["우클릭", "부하를 누르면 그 부하의 상태창이 열립니다. 빈 바닥에서는 아무 일도 일어나지 않습니다."],
    ["X0 · X1 · X2", "화면 오른쪽의 배속 단추입니다. 습격이 길게 느껴지면 X2로 넘기고, 급하면 X0으로 멈춥니다."],
    ["설정", "화면 왼쪽 위 단추. 효과음과 배경음악 음량을 따로 조절하며, 설정은 저장됩니다."],
  ]);

h1("3. 자원");
body("화면 오른쪽 기둥에 그날의 상태가 모여 있습니다.");
table(
  [["자원", 1.1], ["설명", 3.2]],
  [
    ["운영 자금", "금(G). 건설·고용·유지비가 여기서 나갑니다."],
    ["주둔 마력", "한 번에 부릴 수 있는 부하의 총량입니다. 최대 8이며, 부하마다 차지하는 마력이 다릅니다. 마을을 장악하면 상한이 오릅니다."],
    ["던전 권능", "습격 중에만 차오릅니다. 적을 잡거나 시간이 지나면 늘고, 권능을 시전할 때 씁니다. 습격이 끝나면 사라집니다."],
    ["민심", "0에서 100까지. 낮으면 같은 부하를 데리고 있는 데 돈이 더 들고, 찾아오는 지원자도 줄어듭니다."],
    ["던전 악명", "구역에서 실제로 벌어진 일—전투와 함정 발동—로 쌓입니다."],
    ["결속", "중앙 핵심부 둘레에 건물을 모아 지으면 단계가 오르고 재보 보너스가 붙습니다. 방어에 좋은 자리와 핵심부 고리 중 무엇을 택할지의 문제입니다."],
    ["연속 방어", "한 명도 놓치지 않고 막아낸 날이 이어지면 보상이 오르지만 다음 날 적이 늘어납니다. 한 번 끊기면 보상도 압력도 함께 0으로 돌아갑니다."],
  ]);

h1("4. 몬스터");
body("금과 마력을 써서 들입니다. 등급이 높을수록 세지만 마력을 많이 차지해, 여덟이라는 상한 안에서 무엇을 포기할지 고르게 됩니다.");
table(
  [["몬스터", 1.5], ["금", 0.6, "right"], ["마력", 0.7, "right"], ["성격", 3.1]],
  [
    ["블루 슬라임", "14", "1", "가장 싼 소모품. 머릿수로 시간을 법니다."],
    ["그린 슬라임", "18", "1", "블루와 같은 자리를 놓고 다툽니다."],
    ["레드 슬라임", "20", "2", "셋 중 가장 단단합니다."],
    ["정찰병", "18", "1", "쿨이 가장 짧습니다. 뒤로 파고들어 한 대상을 무르게 만듭니다."],
    ["창병", "28", "2", "적의 방어를 깎아 뒤에 선 딜러의 피해를 키웁니다."],
    ["석궁병", "42", "2", "독과 둔화로 적의 발을 묶는 주력 원거리입니다."],
    ["수호병", "48", "3", "버티면서 성역으로 아군을 회복시킵니다. 유일한 회복원입니다."],
    ["전투 마도사", "64", "4", "예고한 자리를 터뜨리는 광역 딜러입니다."],
    ["선봉대", "82", "5", "혼자 마력 상한의 절반 이상을 씁니다. 밀어내서 대열을 흩뜨립니다."],
  ]);
note("마력을 어떻게 쓸 것인가",
  "선봉대 하나에 마력 5를 몰아줄지, 값싼 부하 여럿으로 여덟 칸을 채울지가 이 게임의 중심 선택입니다. " +
  "정해진 답은 없고, 그날 오는 모험가의 구성에 따라 달라집니다.");

h1("5. 건물과 함정");
h2("방");
table(
  [["건물", 1.3], ["금", 0.55, "right"], ["하는 일", 3.0]],
  [
    ["포탈", "0", "모험가가 들어오는 입구입니다. 먼저 놓아야 습격이 시작됩니다."],
    ["벽", "8", "길을 막습니다. 가장 싼 방어 수단입니다."],
    ["상점", "15", "수입을 만듭니다."],
    ["무기 상점", "30", "상점의 상위. 수입이 더 큽니다."],
    ["광산", "20", "꾸준한 수입원입니다."],
    ["깊은 광산", "35", "광산의 상위입니다."],
    ["주점", "10", "부하가 쉬는 곳입니다."],
    ["큰 주점", "25", "주점의 상위입니다."],
    ["휴게실", "22", "부하의 회복을 돕습니다."],
  ]);
h2("함정");
body("모험가가 지나가면 발동합니다. 부하와 달리 마력을 쓰지 않아, 마력이 모자랄 때 빈자리를 메웁니다.", { muted: true });
table(
  [["함정", 1.3], ["금", 0.55, "right"], ["함정", 1.3], ["금", 0.55, "right"]],
  [
    ["기본 함정", "0", "가시 함정", "40"],
    ["마름쇠 함정", "12", "처형 톱날", "46"],
    ["올가미 함정", "20", "칼날 함정", "60"],
    ["매복 그물", "22", "압쇄 함정", "90"],
    ["갑옷분쇄 함정", "32", "", ""],
  ], { boldFirst: true });

h1("6. 던전 권능");
body("습격 중에만 쓸 수 있습니다. 권능 게이지를 소모하고, 각자 재사용 대기시간이 있습니다.");
table(
  [["권능", 1.2], ["비용", 0.6, "right"], ["쿨", 0.5, "right"], ["효과", 2.8]],
  [
    ["돌팔매", "12", "4초", "값싼 돌무더기를 떨굽니다. 자주 쓸 수 있습니다."],
    ["약점 노출", "20", "10초", "갑주의 이음새를 벌려 이어지는 피해를 키웁니다."],
    ["낙석", "25", "7초", "구역의 침입자 전원에게 피해를 줍니다."],
    ["어둠의 축복", "30", "12초", "구역의 부하를 회복시킵니다."],
    ["광란의 북", "30", "15초", "구역의 부하를 광란 상태로 만듭니다."],
    ["서리 손아귀", "35", "14초", "침입자를 붙잡아 발을 묶습니다."],
    ["낙반", "40", "18초", "천장을 무너뜨려 구역을 6초 동안 막습니다."],
    ["지하수 범람", "60", "24초", "막아 둔 물길을 텁니다. 가장 강한 한 방입니다."],
  ]);

h1("7. 스무 날 일정");
body("일차가 오를수록 모험가가 늘고 보상도 큽니다. 보스가 오는 날은 수가 적은 대신 하나하나가 강합니다.");
const waves = [
  [1, 2, 32], [2, 4, 40], [3, 5, 42], [4, 6, 48], [5, 8, 56],
  [6, 9, 64], [7, 11, 72], [8, 12, 82], [9, 4, 94, "토벌대장"], [10, 15, 106],
  [11, 17, 120], [12, 8, 170, "탐욕의 기사"], [13, 20, 152], [14, 21, 170], [15, 23, 190],
  [16, 24, 200], [17, 25, 210], [18, 6, 225, "사냥꾼 두목"], [19, 28, 240], [20, 8, 300, "왕국 기사단장"],
];
table(
  [["일차", 0.5, "right"], ["모험가", 0.7, "right"], ["보상", 0.7, "right"], ["보스", 1.6]],
  waves.map(([d, e, g, boss]) => ({
    cells: [`${d}일차`, `${e}명`, `${g}G`, boss || ""],
    tint: boss ? BOSS : null,
  })));
note("보스가 오는 날",
  "9일차 토벌대장, 12일차 탐욕의 기사, 18일차 사냥꾼 두목, 그리고 마지막 20일차에 왕국 기사단장이 옵니다. " +
  "머릿수가 적다고 방심하면 그날로 끝납니다.");

h1("8. 정산과 부채");
body("하루가 끝나면 장부를 맞춥니다. 남으면 자금이 되고, 모자라면 그만큼 빚이 됩니다.");
bullets([
  "빚에는 이자가 붙습니다. 미루면 미룰수록 커집니다.",
  "빚이 300을 넘으면 파산으로 판이 끝납니다.",
  "금고에 넣어 둔 금은 이자를 받지만, 도굴꾼이 노리는 대상이 되기도 합니다.",
  "민심이 낮으면 같은 부하를 유지하는 데 더 많은 돈이 듭니다.",
]);

h1("9. 그 밖의 것들");
h2("상인");
body("정해진 날에만 찾아옵니다. 유물과 소모품을 파는데, 같은 물건을 살수록 값이 오릅니다. " +
     "유물은 혼자 두면 효과가 크지 않고, 서로 맞는 것끼리 모을 때 값을 합니다.");
h2("원정");
body("부하를 마을로 내보내 금·지원자·마력 상한을 얻어 옵니다. 보낸 부하는 그날 방어에 쓸 수 없고, " +
     "지친 부하는 성공률을 깎습니다. 마을을 장악하면 그 마을이 보내던 모험가가 줄어듭니다.");
h2("정책");
body("며칠에 한 번 두 갈래 중 하나를 고릅니다. 어느 쪽이든 얻는 것과 잃는 것이 함께 옵니다. " +
     "정책끼리 맞물리면 따로 걸 때는 없던 효과가 생깁니다.");

h1("10. 처음 하는 분에게");
bullets([
  "먼저 포탈을 놓아야 습격이 시작됩니다. 아무것도 안 지어졌다면 이것부터입니다.",
  "초반에는 값싼 슬라임으로 머릿수를 채우고, 번 돈으로 수입 건물을 늘리는 편이 안정적입니다.",
  "함정은 마력을 쓰지 않습니다. 마력이 다 찼는데 자금이 남았다면 함정에 쓰십시오.",
  "적 사제는 아군을 회복시킵니다. 사제를 먼저 끊지 못하면 앞줄이 아무리 강해도 밀립니다.",
  "9일차 보스 전에는 자금을 남겨 두는 편이 좋습니다. 여기서 판이 갈리는 일이 잦습니다.",
  "연속 방어를 오래 이어 가면 보상은 크지만 적도 함께 늘어납니다. 버겁다 싶으면 한 번 끊고 숨을 돌리는 것도 방법입니다.",
]);

doc.end();
console.log("완료: 던전키퍼_게임설명서.pdf");
