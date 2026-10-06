// Gera o bloco embutido do diorama dentro do index.html:
//   1. Three.js 0.169.0 só com as classes usadas, minificado, exposto em window.THREE;
//   2. modelos do Pirate Kit da Kenney (CC0) convertidos para arrays compactos, já
//      repintados com a paleta do jogo (cor por face, sem textura).
// Rodar a partir da raiz do repositório:  bun tools/diorama/build.mjs
// (precisa de tools/art3d/node_modules/three — `npm install` em tools/art3d).
import { readFileSync, writeFileSync } from "node:fs";
import { inflateSync } from "node:zlib";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const RAIZ = join(dirname(fileURLToPath(import.meta.url)), "..", "..");
const KIT = join(RAIZ, "refs/kenney-pirate-kit/Models/OBJ format");
const HTML = join(RAIZ, "index.html");
const INICIO = "<!-- diorama:gerado:inicio -->", FIM = "<!-- diorama:gerado:fim -->";

/* ---------- 1. Three.js reduzido ---------- */
const build = await Bun.build({
  entrypoints: [join(RAIZ, "tools/diorama/three-sub.js")],
  format: "iife", minify: true, target: "browser",
});
if (!build.success) { console.error(build.logs); process.exit(1); }
const three = (await build.outputs[0].text()).replace(/<\/script/gi, "<\\/script");

/* ---------- 2. modelos Kenney ---------- */
function decodePNG(buf) {
  let p = 8, w, h, ct; const idat = [];
  while (p < buf.length) {
    const len = buf.readUInt32BE(p), tipo = buf.toString("ascii", p + 4, p + 8), d = buf.subarray(p + 8, p + 8 + len);
    if (tipo === "IHDR") { w = d.readUInt32BE(0); h = d.readUInt32BE(4); ct = d[9]; }
    else if (tipo === "IDAT") idat.push(d);
    else if (tipo === "IEND") break;
    p += 12 + len;
  }
  const raw = inflateSync(Buffer.concat(idat)), bpp = ct === 6 ? 4 : 3, linha = w * bpp;
  const out = new Uint8Array(w * h * 3), ant = new Uint8Array(linha), cur = new Uint8Array(linha);
  let q = 0;
  for (let y = 0; y < h; y++) {
    const f = raw[q++];
    for (let x = 0; x < linha; x++) {
      const a = x >= bpp ? cur[x - bpp] : 0, b = ant[x], c = x >= bpp ? ant[x - bpp] : 0;
      let v = raw[q++];
      if (f === 1) v += a; else if (f === 2) v += b; else if (f === 3) v += (a + b) >> 1;
      else if (f === 4) { const pp = a + b - c, pa = Math.abs(pp - a), pb = Math.abs(pp - b), pc = Math.abs(pp - c); v += pa <= pb && pa <= pc ? a : pb <= pc ? b : c; }
      cur[x] = v & 255;
    }
    for (let x = 0; x < w; x++) for (let k = 0; k < 3; k++) out[(y * w + x) * 3 + k] = cur[x * bpp + k];
    ant.set(cur);
  }
  return { w, h, data: out };
}
const mapa = decodePNG(readFileSync(join(KIT, "Textures/colormap.png")));
const corUV = ([u, v]) => {
  const x = Math.min(mapa.w - 1, Math.max(0, Math.floor(u * mapa.w)));
  const y = Math.min(mapa.h - 1, Math.max(0, Math.floor((1 - v) * mapa.h)));
  const o = (y * mapa.w + x) * 3; return [mapa.data[o], mapa.data[o + 1], mapa.data[o + 2]];
};
function obj(nome) {
  const v = [], vt = [], tris = [];
  for (const l of readFileSync(join(KIT, nome + ".obj"), "utf8").split("\n")) {
    const s = l.trim().split(/\s+/);
    if (s[0] === "v") v.push(s.slice(1, 4).map(Number));
    else if (s[0] === "vt") vt.push(s.slice(1, 3).map(Number));
    else if (s[0] === "f") {
      const ix = s.slice(1).map(t => { const [a, b] = t.split("/"); return [a - 1, b ? b - 1 : -1]; });
      for (let i = 1; i < ix.length - 1; i++) tris.push([ix[0], ix[i], ix[i + 1]]);
    }
  }
  return { v, vt, tris };
}
const hex = h => [(h >> 16) & 255, (h >> 8) & 255, h & 255];
const mix = (a, b, t) => a.map((x, i) => Math.round(x + (b[i] - x) * t));
const lum = c => (c[0] * .3 + c[1] * .59 + c[2] * .11) / 255;

/* repintura: folhas puxadas para o verde-mar do jogo, madeira para a madeira do jogo;
   o barco ganha as cores dos barcos da colônia de pescadores (casco mar, borda creme,
   faixa coral, madeira por dentro) */
const repinta = {
  "palm-bend": (c) => c[1] > c[0]
    ? mix(hex(0x1F6E5C), hex(0x5CB88A), Math.min(1, Math.max(0, (lum(c) - .4) / .3)))
    : mix(hex(0x7A4C26), hex(0xB98250), Math.min(1, Math.max(0, (lum(c) - .5) / .12))),
  "boat-row-small": (c, cy, ny) => ny > .6 ? hex(0xA9713F)
    : cy < .42 ? hex(0x2BA6A4) : cy < .56 ? hex(0xE8765C) : hex(0xF4EBD9),
};
function converte(nome) {
  const o = obj(nome), pos = [], cor = [];
  let max = 0;
  for (const p of o.v) for (const x of p) max = Math.max(max, Math.abs(x));
  const esc = 32767 / max;
  for (const t of o.tris) {
    const [a, b, c] = t.map(([iv]) => o.v[iv]);
    const n = [(b[1] - a[1]) * (c[2] - a[2]) - (b[2] - a[2]) * (c[1] - a[1]),
               (b[2] - a[2]) * (c[0] - a[0]) - (b[0] - a[0]) * (c[2] - a[2]),
               (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])];
    const nl = Math.hypot(...n) || 1, cy = (a[1] + b[1] + c[1]) / 3;
    const col = repinta[nome](corUV(o.vt[t[0][1]]), cy, n[1] / nl);
    for (const p of [a, b, c]) { for (const x of p) pos.push(Math.round(x * esc)); cor.push(...col); }
  }
  return { escala: +(1 / esc).toPrecision(8),
    pos: Buffer.from(new Int16Array(pos).buffer).toString("base64"),
    cor: Buffer.from(new Uint8Array(cor)).toString("base64") };
}
const modelos = { coqueiro: converte("palm-bend"), barco: converte("boat-row-small") };

/* ---------- 3. injeta no index.html ---------- */
const bloco = `${INICIO}
<!-- Gerado por tools/diorama/build.mjs. Não editar à mão.
     Three.js 0.169.0 (MIT, three.js authors) reduzido; modelos do Pirate Kit 2.1 da
     Kenney (CC0, www.kenney.nl) repintados com a paleta do jogo. -->
<script id="three-lib">${three}</script>
<script type="application/json" id="kenney-modelos">${JSON.stringify(modelos)}</script>
${FIM}`;
const html = readFileSync(HTML, "utf8");
const a = html.indexOf(INICIO), b = html.indexOf(FIM);
if (a < 0 || b < 0) { console.error("marcadores do diorama não encontrados no index.html"); process.exit(1); }
writeFileSync(HTML, html.slice(0, a) + bloco + html.slice(b + FIM.length));
console.log(`three: ${(three.length / 1024).toFixed(0)} KB · modelos: ${(JSON.stringify(modelos).length / 1024).toFixed(0)} KB`);
