import * as THREE from "three";
import { P, mat, matLiso } from "./paleta.js";

/**
 * Banhista low-poly modelado em código — nada de pacote de terceiros, para o boneco ter
 * a cara do jogo (chapéu de palha, sunga na cor do jogador, pazinha na mão).
 *
 * Esqueleto (em unidades do mundo, chão em y=0):
 *   pé 0.00 · quadril 0.86 · cintura 0.98 · ombro 1.66 · queixo 1.78 · centro da cabeça 2.06
 * Cabeça de raio 0.34 → ~3,4 cabeças de altura: proporção de mascote, legível a 200 px.
 */

const R_CABECA = 0.34;
const Y_OMBRO = 1.66;
const Y_QUADRIL = 0.86;

function capsula(raio, comprimento, material) {
  const m = new THREE.Mesh(new THREE.CapsuleGeometry(raio, comprimento, 4, 14), material);
  m.castShadow = true;
  m.receiveShadow = true;
  return m;
}

/** Membro com pivô no ombro/quadril: girar o grupo gira o membro inteiro, como articulação. */
function membro(raio, comprimento, material, ancora, rotacao) {
  const pivo = new THREE.Group();
  pivo.position.set(...ancora);
  const carne = capsula(raio, comprimento, material);
  carne.position.y = -(comprimento / 2 + raio * 0.3);
  pivo.add(carne);
  pivo.rotation.set(...rotacao);
  pivo.userData.pontaY = -(comprimento + raio * 1.2);   // onde fica a mão/pé
  return pivo;
}

function chapeuPalha() {
  const g = new THREE.Group();

  const copa = new THREE.Mesh(
    new THREE.SphereGeometry(R_CABECA * 1.04, 18, 12, 0, Math.PI * 2, 0, Math.PI * 0.52),
    mat(P.palha, { rugosidade: 0.95 }));
  copa.scale.y = 0.85;
  copa.castShadow = false;
  g.add(copa);

  // A aba NÃO projeta sombra de propósito: com o sol baixo ela jogava o rosto inteiro
  // na sombra, e rosto escuro mata a simpatia do mascote.
  const aba = new THREE.Mesh(
    new THREE.CylinderGeometry(R_CABECA * 1.62, R_CABECA * 1.72, R_CABECA * 0.09, 20),
    mat(P.palhaEsc, { rugosidade: 0.95 }));
  aba.castShadow = false;
  aba.receiveShadow = true;
  g.add(aba);

  const fita = new THREE.Mesh(
    new THREE.CylinderGeometry(R_CABECA * 1.06, R_CABECA * 1.08, R_CABECA * 0.22, 20),
    matLiso(P.coral));
  fita.position.y = R_CABECA * 0.12;
  g.add(fita);

  return g;
}

/** Pazinha de praia: cabo, punho em T e a pá côncava. */
export function pazinha() {
  const g = new THREE.Group();

  const cabo = new THREE.Mesh(new THREE.CylinderGeometry(0.032, 0.032, 0.5, 10), matLiso(P.coralEsc));
  cabo.castShadow = true;
  g.add(cabo);

  const punho = new THREE.Mesh(new THREE.CapsuleGeometry(0.035, 0.16, 3, 10), matLiso(P.creme));
  punho.rotation.z = Math.PI / 2;
  punho.position.y = 0.26;
  punho.castShadow = true;
  g.add(punho);

  const pa = new THREE.Mesh(
    new THREE.SphereGeometry(0.17, 16, 12, 0, Math.PI * 2, Math.PI * 0.45, Math.PI * 0.55),
    mat(P.coral, { rugosidade: 0.7 }));
  pa.position.y = -0.3;
  pa.scale.set(1, 0.8, 0.5);
  pa.castShadow = true;
  g.add(pa);

  return g;
}

function rosto(cabeca, { boquiaberto = false } = {}) {
  const tinta = matLiso(P.tinta, 0.55);
  const olho = new THREE.SphereGeometry(0.042, 12, 10);

  for (const x of [-0.12, 0.12]) {
    const e = new THREE.Mesh(olho, tinta);
    e.scale.set(1, 1.15, 0.6);
    e.position.set(x, 0.05, R_CABECA * 0.92);
    cabeca.add(e);
  }

  if (boquiaberto) {
    const boca = new THREE.Mesh(new THREE.SphereGeometry(0.075, 14, 12), tinta);
    boca.scale.set(0.85, 1.2, 0.45);
    boca.position.set(0, -0.12, R_CABECA * 0.9);
    cabeca.add(boca);
  } else {
    const boca = new THREE.Mesh(new THREE.TorusGeometry(0.095, 0.019, 8, 18, Math.PI), tinta);
    boca.position.set(0, -0.045, R_CABECA * 0.88);
    boca.rotation.z = Math.PI;
    boca.scale.set(1, 0.8, 1);
    cabeca.add(boca);
  }

  const rosa = matLiso(P.coral, 1);
  for (const x of [-0.2, 0.2]) {
    const b = new THREE.Mesh(new THREE.SphereGeometry(0.055, 10, 8), rosa);
    b.scale.set(1.1, 0.7, 0.3);
    b.position.set(x, -0.03, R_CABECA * 0.85);
    cabeca.add(b);
  }
}

/**
 * Poses. Convenção: girar o pivô em Z por θ leva o membro (que aponta para baixo) para
 * (sen θ, −cos θ) — ou seja, **θ positivo ergue para a DIREITA da tela**. Então o braço
 * direito abre com θ positivo e o esquerdo com θ negativo; trocar isso cruza os braços
 * na frente do rosto (foi o primeiro erro deste modelo).
 */
const POSES = {
  apresenta: {
    bracoE: [0.2, 0, -0.45],
    bracoD: [0.3, 0, 2.15],      // ergue a pazinha ao lado, apresentando o jogo
    pernaE: [0.05, 0, -0.1],
    pernaD: [-0.05, 0, 0.08],
    corpo: { y: 0, rz: 0, ry: 0.28 },
  },
  vitoria: {
    bracoE: [0.3, 0, -2.25],
    bracoD: [0.3, 0, 2.25],      // os dois braços para o alto, abertos
    pernaE: [0.3, 0, -0.34],
    pernaD: [-0.34, 0, 0.3],     // pulando, pernas abertas
    corpo: { y: 0.28, rz: 0.03, ry: 0.2 },
  },
  derrota: {
    bracoE: [-0.3, 0, -2.3],
    bracoD: [-0.3, 0, 2.4],      // braços para o alto, debatendo-se
    pernaE: [-0.95, 0, -0.35],
    pernaD: [-0.7, 0, 0.3],      // pernas chutadas para a frente
    corpo: { y: 0.2, rz: -0.75, ry: 0.25 },
  },
};

export function banhista({ sunga = P.jogadorUm, pose = "apresenta", comPazinha = true, comChapeu = true } = {}) {
  const raiz = new THREE.Group();
  // `corpo` gira pelo QUADRIL (y≈1): girar pela origem nos pés jogava o boneco
  // inteiro para fora do quadro na pose de derrota.
  const corpo = new THREE.Group();
  const dentro = new THREE.Group();
  dentro.position.y = -1;
  corpo.add(dentro);
  raiz.add(corpo);

  const pele = matLiso(P.pele);
  const peleSombra = matLiso(P.peleSombra);
  const p = POSES[pose] ?? POSES.apresenta;

  // ---- tronco: cápsula achatada, ombro largo e cintura fina ----
  const tronco = capsula(0.3, 0.42, matLiso(P.creme));
  tronco.scale.set(1.12, 1, 0.72);
  tronco.position.y = 1.36;
  dentro.add(tronco);

  const sungaMesh = capsula(0.29, 0.14, matLiso(sunga));
  sungaMesh.scale.set(1.12, 1, 0.78);
  sungaMesh.position.y = 0.96;
  dentro.add(sungaMesh);

  // ---- cabeça ----
  const pescoco = capsula(0.1, 0.06, peleSombra);
  pescoco.position.y = 1.72;
  dentro.add(pescoco);

  const cabeca = new THREE.Mesh(new THREE.SphereGeometry(R_CABECA, 22, 16), pele);
  cabeca.scale.set(1, 1.06, 0.95);
  cabeca.position.y = 2.06;
  cabeca.castShadow = true;
  cabeca.receiveShadow = true;
  dentro.add(cabeca);
  rosto(cabeca, { boquiaberto: pose === "derrota" });

  // Na queda, a cabeça acompanha só metade do giro do corpo: rosto virado de lado
  // apaga a leitura da carinha, que é o que dá graça ao caldo.
  cabeca.rotation.z = -p.corpo.rz * 0.55;

  if (comChapeu) {
    const chapeu = chapeuPalha();
    chapeu.position.set(0, 2.3, -0.02);
    chapeu.rotation.z = -0.12;
    dentro.add(chapeu);
  }

  // ---- pernas ----
  const pernaE = membro(0.115, 0.6, pele, [-0.16, Y_QUADRIL, 0], p.pernaE);
  const pernaD = membro(0.115, 0.6, peleSombra, [0.16, Y_QUADRIL, 0], p.pernaD);
  dentro.add(pernaE, pernaD);

  for (const perna of [pernaE, pernaD]) {
    const pe = new THREE.Mesh(new THREE.SphereGeometry(0.13, 14, 10), matLiso(P.coralEsc));
    pe.scale.set(0.85, 0.5, 1.3);
    pe.position.set(0, perna.userData.pontaY + 0.06, 0.06);
    pe.castShadow = true;
    perna.add(pe);
  }

  // ---- braços (presos FORA do tronco, senão somem dentro dele) ----
  // z levemente à frente: com o ombro no eixo do tronco os braços sumiam atrás dele.
  const bracoE = membro(0.095, 0.52, pele, [-0.42, Y_OMBRO, 0.06], p.bracoE);
  const bracoD = membro(0.095, 0.52, peleSombra, [0.42, Y_OMBRO, 0.06], p.bracoD);
  dentro.add(bracoE, bracoD);

  for (const braco of [bracoE, bracoD]) {
    const mao = new THREE.Mesh(new THREE.SphereGeometry(0.105, 12, 10), pele);
    mao.position.y = braco.userData.pontaY + 0.04;
    mao.castShadow = true;
    braco.add(mao);
  }

  if (comPazinha) {
    const pa = pazinha();
    pa.position.set(0, bracoD.userData.pontaY - 0.22, 0.04);
    pa.rotation.z = 0.12;
    bracoD.add(pa);
  }

  corpo.position.y = 1 + p.corpo.y;
  corpo.rotation.z = p.corpo.rz;
  raiz.rotation.y = p.corpo.ry;
  return raiz;
}
