// 설명서에 넣을 그림을 프로젝트 스프라이트에서 줄여 art/ 에 만든다.
//
// 원본은 한 장에 1254x1254, 2MB 가까이 된다. 그대로 PDF에 넣으면 파일이 20MB를 넘고
// pdfkit이 압축 도중 스택을 넘긴다. 여기서 미리 줄여 두고 build1.js가 그것을 가져다 쓴다.
//
//   node prepare-art.js && node build1.js

const { Jimp } = require("jimp");
const fs = require("fs");

const G = "../Assets/05.Graphs/";

// [원본 경로, 저장 이름, 가로 픽셀]
const JOBS = [
  ["Unit/Slime/Blue/Idle.png", "slime_blue", 72],
  ["Unit/Slime/Green/Idle.png", "slime_green", 72],
  ["Unit/Slime/Red/Idle.png", "slime_red", 72],
  ["Player/DemonIdle.png", "demon", 200],
  ["Enemey/1/Archer/Idle.png", "archer", 110],
  ["Enemey/1/Sword/Idle.png", "sword", 110],
  ["Enemey/1/Healter/Idle.png", "healer", 110],
  ["Bulding/Portal.png", "portal", 72],
  ["Bulding/Store.png", "store", 72],
  ["Bulding/Inn.png", "inn", 72],
  ["Mine/Mine.png", "mine", 72],
  ["Trap/Trap.png", "trap", 72],
  ["BG/Background.png", "bg", 900],
  ["Node/Node.png", "node", 96],
];

(async () => {
  fs.mkdirSync("art", { recursive: true });
  let done = 0;
  for (const [src, name, w] of JOBS) {
    try {
      const img = await Jimp.read(G + src);
      img.resize({ w });
      await img.write("art/" + name + ".png");
      done++;
    } catch (e) {
      console.log("건너뜀 " + name + ": " + e.message.slice(0, 60));
    }
  }
  console.log(`그림 ${done}/${JOBS.length}개 준비 완료`);
})();
