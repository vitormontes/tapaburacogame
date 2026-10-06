import * as THREE from "three";

/**
 * A MESMA paleta do jogo 2D (unity/Assets/Scripts/Game/Palette.cs e o :root do index.html).
 * O mundo agora é renderizado em 3D, mas a cor continua sendo a do cartaz — é isso que
 * segura a coesão entre o mundo (com volume) e a interface (chapada).
 */
export const P = {
  areia: 0xE9D3A8,
  areiaMol: 0xD3B786,
  areiaEsc: 0xB9985F,
  areiaSom: 0x8E7038,
  areiaFunda: 0x6E5426,
  areiaClara: 0xFBEFD5,
  mar: 0x2BA6A4,
  marFundo: 0x1C7E80,
  marClaro: 0x57C2B4,
  ceu: 0x7EC8D8,
  coral: 0xE8765C,
  coralEsc: 0xC4503A,
  creme: 0xF4EBD9,
  tinta: 0x1A1A1A,
  madeira: 0xA9713F,
  madeiraEsc: 0x7A4C26,
  amarelo: 0xE8B23C,
  pele: 0xE8B98D,
  peleSombra: 0xD8A87C,
  palha: 0xEBD097,
  palhaEsc: 0xE0C07A,
  verde: 0x4E9A5B,
  verdeEsc: 0x2F6B40,
  pedra: 0x9A9AA0,
  jogadorUm: 0xF3937A,
  jogadorDois: 0x155F61,
};

/** Material padrão do mundo: fosco, sem metal, facetado (a leitura low-poly do cartaz). */
export function mat(cor, { rugosidade = 0.85, facetado = true } = {}) {
  return new THREE.MeshStandardMaterial({
    color: cor,
    roughness: rugosidade,
    metalness: 0,
    flatShading: facetado,
  });
}

/** Pele e panos ficam mais suaves — facetar tudo endurece demais o personagem. */
export function matLiso(cor, rugosidade = 0.9) {
  return mat(cor, { rugosidade, facetado: false });
}
