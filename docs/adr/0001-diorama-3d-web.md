# ADR 0001 — Diorama 3D na web (Three.js embutido)

- **Data**: 2026-10-05
- **Estado**: aceito
- **Escopo**: `index.html` (web). A Unity fica para depois (ver "Unity").

## Contexto

O jogo web era um `index.html` autossuficiente (~175 KB, offline, sem dependências externas),
com o tabuleiro em HTML/CSS e o preenchimento de areia num canvas 2D. O pedido foi dar ao jogo
cara de produto acabado com a direção de **pequeno diorama de praia em 3D** (Copacabana), sem
perder a clareza do tabuleiro, as regras nem a personalidade.

O repositório já tinha Three.js 0.169.0 em `tools/art3d` (usado para renderizar o banhista) e o
Pirate Kit 2.1 da Kenney (CC0) em `refs/kenney-pirate-kit`.

## Decisões (entrevista com o usuário em 2026-10-05)

1. **Renderizador: Three.js embutido no próprio `index.html`.** Só as classes usadas, empacotadas
   e minificadas por `tools/diorama/build.mjs` (Bun), versão fixa 0.169.0. O arquivo continua
   único e offline; cresce algumas centenas de KB. CDN foi descartado (quebra o offline) e 2,5D
   também (não entrega cavidade real nem luz coerente).
2. **O 3D só desenha.** As regras continuam em `MOTOR`/`J`. Os 28 buracos continuam sendo
   `<button>` HTML, transparentes e posicionados sobre a projeção de cada buraco pela câmera
   ortográfica. Toque, mouse, teclado e leitor de tela usam o mesmo botão, então objeto visual e
   célula lógica não têm como divergir. Não há raycasting.
3. **Sem WebGL (ou contexto perdido): volta o tabuleiro 2D**, com o canvas de areia e o cenário
   SVG. É o mesmo código de antes, sem o `body.com-3d`.
4. **Papel de Pão vira material 3D**: mesma geometria, materiais de papel e traço azul. No
   tabuleiro 2D de reserva continua o estilo antigo.
5. **O diorama aparece na tela inicial e na partida** (câmera aberta no menu, enquadrando o
   tabuleiro na partida). Regras e resultado continuam como cartões HTML sobre a cena.
6. **Elementos de borda**: calçadão de pedra portuguesa, guarda-sol listrado, conchas e pazinha,
   um coqueiro e um barco a remo (Kenney, repintados com a paleta), morros ao fundo. Espalhados
   nas margens, nunca sobre o tabuleiro.
7. **Areia**: o núcleo da simulação (partículas + campo de altura com regra de falling sand) é
   o mesmo nos dois renderizadores. No 3D, o campo de altura vira a malha que sobe dentro da
   cavidade e os grãos viram um `InstancedMesh`.
8. **Unity: depois.** Não há editor Unity nesta máquina e o `tools/Harness` compila contra um
   shim de `UnityEngine`; um diorama na Unity sairia sem nenhuma verificação visual. O porte
   deve seguir os parâmetros abaixo quando houver editor para validar.

## Acabamento de luz e material (2026-10-06)

Pedido do usuário: normal baking, smooth e chanfros, rim light, AO, color grading com bloom,
vinheta e neblina, e probes/luz assada no lugar de luzes em tempo real. Equivale ao modo
"Baked Indirect" da Unity: a luz direta (um sol, com sombra) continua em tempo real; o
indireto é todo assado.

- **Normal baking na GPU.** O relevo é analítico (`duna`/`relevo`), então um shader o avalia
  na montagem numa grade de 112 texels por unidade (72 no celular) e grava a normal em
  espaço de mundo, já com grão e marolas de vento. A malha guarda a forma; a luz lê a textura.
  O mesmo GLSL tem que acompanhar qualquer mudança no `relevo()` do JS.
- **AO assada**: horizonte do próprio relevo (8 direções) + contato dos objetos no chão, numa
  textura de 1/3 da resolução, multiplicada no albedo do terreno. Ela volta para a CPU para o
  enchimento dos buracos usar o mesmo valor (a cavidade não clareia quando a malha entra).
  Objetos levam AO por vértice no pé.
- **Probe de luz** (SH de ordem 2, assada na CPU) no lugar do `HemisphereLight`: céu quente,
  horizonte do mar puxado para o verde-mar, halo do sol e rebote da areia. Uma probe só: todo o
  conteúdo dinâmico fica em volta do tabuleiro.
- **Rim light por fresnel** nos objetos, sem luz extra.
- **Smooth com vinco** (normais suavizadas abaixo de um ângulo) nos modelos Kenney, morros,
  guarda-sol e conchas; **chanfro arredondado** de verdade na base, no meio-fio e no punho da pá.
- **Pós**: cena num alvo HDR (meia precisão, MSAA 4×, 2× no celular), bloom dual de 4 níveis,
  tone mapping Neutral, céu do CSS composto por trás (o canvas fica opaco), grading
  (saturação, contraste, tons de sombra e luz), vinheta quente e dither. Sem
  `EXT_color_buffer_float`, o alvo é de 8 bits e o bloom sai. Neblina linear do `Fog` na cena e
  a mesma conta no shader do mar. Papel de Pão: sem relevo assado, sem neblina e sem grading.

## Segunda passada de arte (2026-10-06)

Pedido do usuário: buracos menos "de molde", enchimento integrado ao chão, areia, objetos,
mascote, espuma e acabamento coerentes, sem mexer em regras, alinhamento, modos ou acessibilidade.

- **Forma por buraco** (`FORMA`, 20 números por buraco, hash da posição): ondulação do contorno
  (harmônicos 3 e 5), ovalização com área igual, fundo mais ou menos fundo e inclinado, parede
  mais íngreme de um lado e a areia tirada em dois montes (lóbulos de von Mises), não num anel.
  Centro, passo e alvo de toque iguais. JS (`relevo`) e GLSL (`GLSL_RELEVO`, `uForma`) usam a
  mesma conta, sem `atan`/`sin` por vizinho; as marcas de escavação ficam só na normal.
- **Bake em três passadas**: altura (relevo uma vez por texel, 16 bits por valor numa RGBA8
  comum, sem extensão de float), normal e AO lendo essa textura. Antes cada texel avaliava o
  relevo ~44 vezes.
- **Enchimento**: o disco segue a forma do buraco e tem cinco anéis por fora. A fração cheia
  (campo/alvo do `Areia`, mesma suavização nos dois) leva o chão de agora até a superfície
  tapada; a cor e a normal (atributo `mistura`) passam da areia do chão para a remexida, então o
  disco não aparece antes da areia chegar. O tapado fica na altura média do lábio (sem anel),
  com acúmulos, a marca curta da pá e uma costura falhada na borda antiga. `tapadoParado` é o
  mesmo `moldaFill` com fração 1: o fim da animação e o estado reconstruído são idênticos
  (diferença de pixel 0 na verificação).
- **Pá**: a pose é montada a partir da ponta, e os grãos saem dela (`t.emissor`); chega,
  gira no cabo e despeja, bate as costas na areia (quando a marca aparece) e sai. Os grãos
  ficam sobre o enchimento ou o chão de verdade. Movimento reduzido continua sem pá e sem grãos.
- **Seleção**: anéis, hover e perigo viram fitas no chão que seguem o contorno; a faixa da
  linha em jogo é pintada no shader da areia (o plano antigo era cortado pelos lábios).
- **Areia**: manchas largas por ruído, grão por pixel que some abaixo do pixel, areia molhada
  menos áspera, marolas em manchas fora do tabuleiro. Luz: sol 2,5 e ambiente 1,1, AO 0,62,
  rim 0,2, bloom 0,08, vinheta 0,16 só nos cantos; os véus CSS (vinheta, halftone) saem no 3D.
- **Mar**: espuma em faixa com espessura, borda e falhas irregulares, renda que se desfaz
  quando a onda volta, areia aparecendo no raso, brilho em manchas.
- **Objetos** (`LUGAR`/`CONTATOS`): barraca (guarda-sol, cadeira de lona, isopor com copo de
  mate), canga com chinelos, coqueiro e barco à esquerda; Kenney menos saturado e mais macio.
- **Mascote**: selo de interface com traço de tinta (`currentColor`); a tela inicial 3D
  enquadra o tabuleiro no espaço livre do vendedor, do balão e dos botões (`retanguloTitulo`).

Medido no Chrome sem janela, GPU Radeon RX 550, 1440×900, média de 3 cargas: montagem
(`domContentLoaded − responseEnd`) 2,06 s → 1,75 s; CPU da thread principal parada 96 → 91 ms/s;
numa jogada de 7 buracos, 818 → 748 ms/s e 31,6 → 31,8 quadros/s. Celular não foi medido.

## Parâmetros que o porte (e ajustes futuros) devem seguir

Ficam no objeto `DIORAMA` do `index.html`: ângulo da câmera ortográfica, folga do enquadramento,
direção e cor do sol, ambiente da probe (`ambiente`), resolução do mapa de sombra, teto e piso de
pixel ratio, densidade da malha da areia (`malha`) e do enchimento dos buracos (`discoAneis`,
`discoSegmentos`), resolução do relevo assado (`assado`), força da AO (`ao`), rim (`rim`),
neblina (`nevoa`), pós (`pos`: bloom, limiar, contraste, saturação, tons, vinheta, céu), cores
dos materiais e altura das ondas. Os da areia continuam no objeto `AREIA`.

## Consequências

- Edição do Three.js embutido exige rodar `tools/diorama/build.mjs`; o resto continua editável
  direto no `index.html`.
- Dois caminhos de desenho para manter (3D e 2D de reserva), com a mesma lógica e a mesma
  simulação de areia.
- A Unity fica visualmente atrás da web até o porte.
