import * as THREE from "three";
import { OBJLoader } from "three/addons/loaders/OBJLoader.js";
import { P, mat, matLiso } from "./paleta.js";
import { banhista } from "./banhista.js";

const KIT = "/refs/kenney-pirate-kit/Models/OBJ format/";
const cache = new Map();

/** Modelos do Pirate Kit (CC0) usados como cenário — repintados com a paleta do jogo. */
async function kit(nome, cor, escala = 1) {
  if (!cache.has(nome)) {
    cache.set(nome, await new OBJLoader().loadAsync(KIT + nome + ".obj"));
  }

  const obj = cache.get(nome).clone(true);
  obj.traverse(n => {
    if (n.isMesh) {
      n.material = mat(cor, { rugosidade: 0.9 });
      n.castShadow = true;
      n.receiveShadow = true;
    }
  });
  obj.scale.setScalar(escala);
  return obj;
}

/**
 * Areia com relevo de verdade: `cavas` viram funis (com lábio de areia empurrada para fora)
 * e `montes` viram montinhos. A sombra dentro do buraco é calculada pela luz, não pintada —
 * é isso que faz o tabuleiro parecer cavado de fato.
 */
export function areia(tamanho, { cavas = [], montes = [], raio = 0.42, ondulacao = 1 } = {}) {
  const seg = Math.min(420, Math.round(tamanho * 30));
  const geo = new THREE.PlaneGeometry(tamanho, tamanho, seg, seg);
  const pos = geo.attributes.position;

  for (let i = 0; i < pos.count; i++) {
    const x = pos.getX(i);
    const y = pos.getY(i);
    let z = (Math.sin(x * 2.1 + y * 0.6) * 0.02 + Math.sin(x * 0.7 - y * 1.9) * 0.03) * ondulacao;

    for (const [cx, cy] of cavas) {
      const d = Math.hypot(x - cx, y - cy) / raio;
      if (d < 2) {
        z -= Math.max(0, 1 - d * d) * 0.55;
        z += Math.max(0, 1 - Math.abs(d - 1.3) * 3.4) * 0.06;
      }
    }

    for (const [mx, my] of montes) {
      const d = Math.hypot(x - mx, y - my) / (raio * 1.3);
      if (d < 1.7) {
        z += Math.max(0, 1 - d * d) * 0.3;
      }
    }

    pos.setZ(i, z);
  }

  geo.computeVertexNormals();
  const chao = new THREE.Mesh(geo, mat(P.areia, { rugosidade: 0.97, facetado: false }));
  chao.rotation.x = -Math.PI / 2;
  chao.receiveShadow = true;
  return chao;
}

/** Guarda-sol de gomos alternados — o mesmo desenho do vetor, agora com volume. */
export function guardaSol(cor) {
  const g = new THREE.Group();

  const mastro = new THREE.Mesh(new THREE.CylinderGeometry(0.045, 0.055, 2.3, 10), mat(P.madeira));
  mastro.position.y = 1.15;
  mastro.castShadow = true;
  g.add(mastro);

  const gomos = 10;
  for (let i = 0; i < gomos; i++) {
    const geo = new THREE.SphereGeometry(
      1.15, 6, 6,
      (i / gomos) * Math.PI * 2, (Math.PI * 2) / gomos,
      0, Math.PI * 0.42);
    const gomo = new THREE.Mesh(geo, mat(i % 2 ? P.creme : cor, { rugosidade: 0.8 }));
    gomo.position.y = 2.06;
    gomo.scale.y = 0.62;
    gomo.castShadow = true;
    g.add(gomo);
  }

  const ponta = new THREE.Mesh(new THREE.SphereGeometry(0.07, 10, 8), matLiso(P.tinta, 0.5));
  ponta.position.y = 2.55;
  g.add(ponta);

  return g;
}

/** Cena do tabuleiro: câmera ORTOGRÁFICA de cima, para o grid continuar batendo pixel a pixel. */
export function tabuleiro({ passo = 1.2, tapados = [] } = {}) {
  const grupo = new THREE.Group();
  const cavas = [];
  const montes = [];

  for (let r = 0; r < 7; r++) {
    for (let i = 0; i <= r; i++) {
      const ponto = [(i - r / 2) * passo, (3 - r) * passo];
      const tapado = tapados.some(([tr, ti]) => tr === r && ti === i);
      (tapado ? montes : cavas).push(ponto);
    }
  }

  grupo.add(areia(11.5, { cavas, montes, raio: 0.44 }));
  return grupo;
}

/** Um montinho isolado, para virar sprite com fundo transparente. */
export function monte() {
  const grupo = new THREE.Group();
  const g = new THREE.SphereGeometry(0.62, 22, 14, 0, Math.PI * 2, 0, Math.PI * 0.5);
  const m = new THREE.Mesh(g, mat(P.areiaMol, { rugosidade: 0.97, facetado: false }));
  m.scale.y = 0.52;
  m.castShadow = true;
  m.receiveShadow = true;
  grupo.add(m);

  // Marca da pazinha no topo do montinho (o `::after` do CSS, agora em geometria).
  const marca = new THREE.Mesh(
    new THREE.TorusGeometry(0.2, 0.022, 6, 14, Math.PI * 1.1),
    mat(P.areiaEsc, { rugosidade: 1, facetado: false }));
  marca.position.set(-0.02, 0.3, 0.02);
  marca.rotation.set(-Math.PI / 2, 0, 0.4);
  grupo.add(marca);

  return grupo;
}

/** Cenário da tela de título: mar ao fundo, areia na frente, palmeiras e guarda-sóis. */
export async function praia() {
  const g = new THREE.Group();

  g.add(areia(60, { ondulacao: 1.4 }));

  const mar = new THREE.Mesh(new THREE.PlaneGeometry(90, 40), matLiso(P.mar, 0.35));
  mar.rotation.x = -Math.PI / 2;
  mar.position.set(0, 0.06, -26);
  g.add(mar);

  const espuma = new THREE.Mesh(new THREE.PlaneGeometry(90, 2.2), matLiso(P.creme, 0.6));
  espuma.rotation.x = -Math.PI / 2;
  espuma.position.set(0, 0.09, -6.4);
  g.add(espuma);

  const itens = [
    ["palm-detailed-bend", -7.4, -1.2, 1.5, P.verde, 0.6],
    ["palm-straight", 8.2, -3.0, 1.25, P.verdeEsc, -0.4],
    ["rocks-sand-a", -11.5, 1.6, 0.8, P.pedra, 0],
    ["rocks-sand-c", 11.8, 0.4, 0.7, P.pedra, 1.2],
    ["boat-row-small", 9.6, 3.6, 0.9, P.creme, 0.7],
  ];

  for (const [nome, x, z, escala, cor, giro] of itens) {
    const m = await kit(nome, cor, escala);
    m.position.set(x, 0, z);
    m.rotation.y = giro;
    g.add(m);
  }

  for (const [x, z, cor, giro] of [[-4.2, 3.4, P.coral, 0.3], [4.6, 2.2, P.mar, -0.5], [0.4, 5.2, P.amarelo, 0.9]]) {
    const gs = guardaSol(cor);
    gs.position.set(x, 0, z);
    gs.rotation.set(0.12, giro, 0.06);
    g.add(gs);
  }

  return g;
}

export { banhista };
