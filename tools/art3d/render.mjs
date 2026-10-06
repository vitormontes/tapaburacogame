// Render dos sprites 3D do TAPA BURACO.
//
// Por que um Chrome e não o Blender: os modelos do Kenney Pirate Kit (CC0) e a geometria
// autoral do banhista já vivem em three.js, e o Chrome está na máquina — `node render.mjs`
// gera os PNGs sem instalar 300 MB de editor e sem passo manual nenhum.
//
//   node render.mjs            → renderiza tudo
//   node render.mjs banhista   → só os alvos cujo nome casa com o filtro
import http from "node:http";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import puppeteer from "puppeteer-core";

const AQUI = path.dirname(fileURLToPath(import.meta.url));
const RAIZ = path.resolve(AQUI, "../..");
const SAIDA_UNITY = path.join(RAIZ, "unity/Assets/Resources/Art");
const SAIDA_WEB = path.join(AQUI, "out");

const CHROME = [
  "C:/Program Files/Google/Chrome/Application/chrome.exe",
  "C:/Program Files (x86)/Google/Chrome/Application/chrome.exe",
  "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
].find(p => fs.existsSync(p));

const TIPOS = {
  ".html": "text/html; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".mjs": "text/javascript; charset=utf-8",
  ".png": "image/png",
  ".obj": "text/plain",
  ".mtl": "text/plain",
  ".json": "application/json",
};

/** Lista de sprites: nome do arquivo, cena, tamanho e opções. */
const ALVOS = [
  ["banhista-apresenta-coral", "personagem", 640, 960, { pose: "apresenta", cor: "coral" }],
  ["banhista-apresenta-mar", "personagem", 640, 960, { pose: "apresenta", cor: "mar" }],
  ["banhista-vitoria-coral", "personagem", 640, 960, { pose: "vitoria", cor: "coral" }],
  ["banhista-vitoria-mar", "personagem", 640, 960, { pose: "vitoria", cor: "mar" }],
  ["banhista-derrota-coral", "personagem", 640, 960, { pose: "derrota", cor: "coral" }],
  ["banhista-derrota-mar", "personagem", 640, 960, { pose: "derrota", cor: "mar" }],
  ["guardasol-coral", "prop", 400, 416, { nome: "guarda-sol", cor: "coral" }],
  ["guardasol-mar", "prop", 400, 416, { nome: "guarda-sol", cor: "mar" }],
  ["guardasol-amarelo", "prop", 400, 416, { nome: "guarda-sol", cor: "amarelo" }],
  ["monte", "prop", 256, 256, { nome: "monte" }],
  ["tabuleiro-areia", "tabuleiro", 1024, 1024, {}],
  ["cena-praia", "praia", 2048, 1280, {}],
  ["chapeu-palha", "prop", 320, 200, { nome: "chapeu" }],
];

function servidor() {
  return new Promise(resolve => {
    const s = http.createServer((q, r) => {
      let p = decodeURIComponent(new URL(q.url, "http://x").pathname);
      if (p === "/") p = "/tools/art3d/page.html";
      if (p.startsWith("/node_modules") || p.startsWith("/lib")) p = "/tools/art3d" + p;
      const arquivo = path.join(RAIZ, p);
      fs.readFile(arquivo, (e, d) => {
        if (e) {
          r.writeHead(404);
          r.end("nao achei " + p);
          return;
        }
        r.writeHead(200, { "content-type": TIPOS[path.extname(arquivo)] ?? "application/octet-stream" });
        r.end(d);
      });
    });
    s.listen(0, "127.0.0.1", () => resolve({ servidor: s, porta: s.address().port }));
  });
}

async function principal() {
  if (!CHROME) {
    throw new Error("Chrome/Edge não encontrado — o render precisa de um navegador com WebGL.");
  }

  const filtro = process.argv[2];
  const alvos = filtro ? ALVOS.filter(a => a[0].includes(filtro)) : ALVOS;
  if (alvos.length === 0) {
    console.log(`nenhum alvo casa com "${filtro}"`);
    return;
  }

  fs.mkdirSync(SAIDA_UNITY, { recursive: true });
  fs.mkdirSync(SAIDA_WEB, { recursive: true });

  const { servidor: s, porta } = await servidor();
  const navegador = await puppeteer.launch({
    executablePath: CHROME,
    headless: false,          // WebGL com sombra sai melhor com GPU de verdade
    args: ["--window-size=1280,900", "--mute-audio"],
  });

  try {
    const pagina = await navegador.newPage();
    await pagina.goto(`http://127.0.0.1:${porta}/`, { waitUntil: "load" });
    await pagina.waitForFunction("window.pronto === true", { timeout: 30000 });

    for (const [arquivo, cena, largura, altura, opcoes] of alvos) {
      const dados = await pagina.evaluate(
        (c, l, a, o) => window.bancada.render(c, l, a, o),
        cena, largura, altura, opcoes);
      const png = Buffer.from(String(dados).split(",")[1], "base64");
      fs.writeFileSync(path.join(SAIDA_UNITY, arquivo + ".png"), png);
      fs.writeFileSync(path.join(SAIDA_WEB, arquivo + ".png"), png);
      console.log(`${arquivo.padEnd(26)} ${largura}×${altura}  ${(png.length / 1024).toFixed(0)} KB`);
    }
  } finally {
    await navegador.close();
    s.close();
  }
}

principal().catch(e => {
  console.error(e);
  process.exit(1);
});
