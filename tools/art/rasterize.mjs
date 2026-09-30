// Rasteriza os SVGs extraídos para PNGs prontos para o Unity.
//
//   node tools/art/rasterize.mjs
//
// Entrada:  tools/art/svg/*.svg
// Saída:    unity/Assets/Resources/Art/*.png   (importados como Sprite pelo ArtImporter.cs)
import { Resvg } from "@resvg/resvg-js";
import { readFileSync, writeFileSync, mkdirSync, readdirSync } from "node:fs";
import { dirname, resolve, basename } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const root = resolve(here, "../..");
const svgDir = resolve(here, "svg");
const outDir = resolve(root, "unity/Assets/Resources/Art");
mkdirSync(outDir, { recursive: true });

// Densidade única: 4 px por unidade de viewBox (D7). Assim uma espessura de traço em
// unidades de viewBox vale o mesmo peso de tela em qualquer arquivo — sem isso, estaca
// (40 un) e banhista (160 un) sairiam com pesos diferentes apesar do mesmo número.
const DENSIDADE = 4;

// Exceção: cena-praia é fundo de tela cheia (1200×800 un). A 4 px/un daria 4800×3200,
// desperdício de memória no celular — um fundo é visto esticado e sem detalhe fino,
// então fica travado em 2048 de largura (~1,71 px/un).
const larguraMaxima = { "cena-praia.svg": 2048 };

/** Largura do viewBox, em unidades. */
function larguraViewBox(svg) {
  const m = svg.match(/viewBox\s*=\s*"\s*[-\d.]+\s+[-\d.]+\s+([\d.]+)\s+([\d.]+)/);
  if (!m) throw new Error("viewBox ausente ou malformado");
  return Number(m[1]);
}

let count = 0;
for (const file of readdirSync(svgDir).filter((f) => f.endsWith(".svg"))) {
  const svg = readFileSync(resolve(svgDir, file), "utf8");
  const width = Math.min(Math.round(larguraViewBox(svg) * DENSIDADE), larguraMaxima[file] ?? Infinity);

  const resvg = new Resvg(svg, {
    fitTo: { mode: "width", value: width },
    background: "rgba(0,0,0,0)",
    shapeRendering: 2, // geometricPrecision
    imageRendering: 0,
  });

  const png = resvg.render().asPng();
  const outName = basename(file, ".svg") + ".png";
  writeFileSync(resolve(outDir, outName), png);
  console.log(`${outName.padEnd(30)} ${String(width).padStart(5)} px  ${String(png.length).padStart(8)} bytes`);
  count++;
}

console.log(`\n${count} PNGs em ${outDir}`);
