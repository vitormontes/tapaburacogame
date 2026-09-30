// Extrai a arte vetorial do protótipo (index.html) para arquivos .svg independentes.
// Os desenhos inline viram arquivos diretos; as funções geradoras (svgGuardaSol, svgBanhista…)
// são recortadas do <script> e executadas aqui, sem DOM.
//
//   node tools/art/extract-svg.mjs
//
// Saída: tools/art/svg/*.svg

import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const root = resolve(here, "../..");
const outDir = resolve(here, "svg");
mkdirSync(outDir, { recursive: true });

const html = readFileSync(resolve(root, "index.html"), "utf8");

/** Recorta uma função do script pelo nome, casando chaves. */
function cutFunction(source, name) {
  const start = source.indexOf(`function ${name}(`);
  if (start < 0) throw new Error(`função ${name} não encontrada`);
  let depth = 0;
  let i = source.indexOf("{", start);
  const bodyStart = i;
  for (; i < source.length; i++) {
    const c = source[i];
    if (c === "{") depth++;
    else if (c === "}") {
      depth--;
      if (depth === 0) return source.slice(start, i + 1);
    }
  }
  throw new Error(`fim da função ${name} não encontrado (body em ${bodyStart})`);
}

/** Recorta um elemento <svg …id="x"…> … </svg> completo. */
function cutSvgById(source, id) {
  const anchor = source.indexOf(`id="${id}"`);
  if (anchor < 0) throw new Error(`svg #${id} não encontrado`);
  const start = source.lastIndexOf("<svg", anchor);
  const end = source.indexOf("</svg>", anchor);
  if (start < 0 || end < 0) throw new Error(`svg #${id} malformado`);
  return source.slice(start, end + 6);
}

/** Recorta o n-ésimo <svg> que contenha um trecho característico. */
function cutSvgContaining(source, needle) {
  const anchor = source.indexOf(needle);
  if (anchor < 0) throw new Error(`trecho não encontrado: ${needle.slice(0, 40)}`);
  const start = source.lastIndexOf("<svg", anchor);
  const end = source.indexOf("</svg>", anchor);
  return source.slice(start, end + 6);
}

const script = html.slice(html.indexOf("<script>"), html.lastIndexOf("</script>"));

// Funções puras do protótipo, reexecutadas fora do navegador.
const generators = ["svgGuardaSol", "svgEstaca", "svgPazinha", "svgBanhista", "svgChapeu", "svgOndaCaldo"];
const code = generators.map((n) => cutFunction(script, n)).join("\n");
const make = new Function(`${code}\nreturn { ${generators.join(", ")} };`)();

/** Garante xmlns e viewBox para o SVG virar arquivo válido. */
function normalize(svg, { width, height } = {}) {
  let out = svg.trim();
  if (!out.includes("xmlns=")) out = out.replace("<svg", '<svg xmlns="http://www.w3.org/2000/svg"');
  if (width && !/\swidth="/.test(out)) out = out.replace("<svg", `<svg width="${width}" height="${height}"`);
  return out + "\n";
}

const files = {
  // Cenário e mascote (desenhos inline).
  "cena-praia.svg": normalize(cutSvgById(html, "cena")),
  "mascote-mate.svg": normalize(cutSvgById(html, "mascote")),
  "espuma.svg": normalize(cutSvgContaining(html, 'class="espuma"')),

  // Peças do tabuleiro.
  "estaca.svg": normalize(make.svgEstaca()),
  "pazinha.svg": normalize(make.svgPazinha()),
  "chapeu-palha.svg": normalize(make.svgChapeu()),
  "onda-caldo.svg": normalize(make.svgOndaCaldo()),

  // Guarda-sóis nas três cores usadas no cenário e no HUD.
  "guardasol-coral.svg": normalize(make.svgGuardaSol("#E8765C", 256)),
  "guardasol-mar.svg": normalize(make.svgGuardaSol("#2BA6A4", 256)),
  "guardasol-amarelo.svg": normalize(make.svgGuardaSol("#E8B23C", 256)),

  // Banhistas: comemorando (braços pra cima) e levando o caldo.
  "banhista-vitoria-coral.svg": normalize(make.svgBanhista("#E8765C", "cima")),
  "banhista-vitoria-mar.svg": normalize(make.svgBanhista("#2BA6A4", "cima")),
  "banhista-derrota-coral.svg": normalize(make.svgBanhista("#E8765C", "lado")),
  "banhista-derrota-mar.svg": normalize(make.svgBanhista("#2BA6A4", "lado")),
};

let total = 0;
for (const [name, content] of Object.entries(files)) {
  writeFileSync(resolve(outDir, name), content, "utf8");
  total += content.length;
  console.log(`${name.padEnd(30)} ${String(content.length).padStart(6)} bytes`);
}
console.log(`\n${Object.keys(files).length} arquivos, ${total} bytes em ${outDir}`);
