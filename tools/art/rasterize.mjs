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
const outDir = resolve(root, "unity/Assets/Resources/Art");
mkdirSync(outDir, { recursive: true });

// Largura de saída por arquivo. Tudo potência de 2 onde dá, para ASTC não desperdiçar.
const widths = {
  "cena-praia.svg": 2048,
  "mascote-mate.svg": 1024,
  "espuma.svg": 1024,
  "estaca.svg": 256,
  "pazinha.svg": 256,
  "chapeu-palha.svg": 256,
  "onda-caldo.svg": 1024,
  "guardasol-coral.svg": 256,
  "guardasol-mar.svg": 256,
  "guardasol-amarelo.svg": 256,
  "banhista-vitoria-coral.svg": 512,
  "banhista-vitoria-mar.svg": 512,
  "banhista-derrota-coral.svg": 512,
  "banhista-derrota-mar.svg": 512,
};

let count = 0;
for (const file of readdirSync(svgDir).filter((f) => f.endsWith(".svg"))) {
  const svg = readFileSync(resolve(svgDir, file), "utf8");
  const width = widths[file] ?? 512;

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
