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

## Parâmetros que o porte (e ajustes futuros) devem seguir

Ficam no objeto `DIORAMA` do `index.html`: ângulo da câmera ortográfica, folga do enquadramento,
direção e cor do sol, céu/chão do hemisfério, resolução do mapa de sombra, teto e piso de pixel
ratio, densidade da malha da areia (`malha`) e do enchimento dos buracos (`discoAneis`,
`discoSegmentos`), cores dos materiais e altura das ondas. Os da areia continuam no objeto `AREIA`.

## Consequências

- Edição do Three.js embutido exige rodar `tools/diorama/build.mjs`; o resto continua editável
  direto no `index.html`.
- Dois caminhos de desenho para manter (3D e 2D de reserva), com a mesma lógica e a mesma
  simulação de areia.
- A Unity fica visualmente atrás da web até o porte.
