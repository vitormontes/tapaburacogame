# 🏖️ TAPA BURACO

> Jogo de praia carioca. Sete fileiras cavadas na areia, em escada: 7 buracos no topo, depois 6, 5… até 1 — **28 no total**.
> Na sua vez você tapa **quantos buracos quiser em linha reta**: numa fileira (horizontal) ou numa coluna (vertical).
> **Quem tapar o último buraco do tabuleiro perde** — e leva um caldo.

## ▶️ Jogar agora

**<https://vitormontes.github.io/tapaburacogame/>**

Roda no navegador do celular ou do computador, sem instalar nada e **sem conexão depois de carregar**:
o jogo inteiro é um arquivo só (`index.html`, ~105 KB) — arte em SVG desenhada à mão, som sintetizado
na hora com Web Audio e nenhuma dependência externa.

> 🔈 **No iPhone**: o Safari silencia o áudio da web quando a chavinha lateral está no mudo.
> Se o jogo ficar sem som, desligue o silencioso.

## Como se joga

- O tabuleiro é uma escada alinhada à esquerda: as fileiras têm 7, 6, 5, 4, 3, 2 e 1 buracos, e as
  colunas também.
- Na sua vez, tape quantos buracos quiser, desde que estejam **em linha reta**: na horizontal (uma
  fileira) ou na vertical (uma coluna). **Diagonal não vale.**
- Um buraco já tapado **bloqueia a linha**: em `O O O X O` dá para tapar até o X, e o último O fica
  separado.
- Toque nos buracos para marcar (tocar no começo e no fim marca o trecho todo) e confirme na
  **pazinha TAPAR**, que mostra quantos buracos vão ser tapados ("TAPAR 3 BURACOS").
- **Quem tapar o último buraco perde.**

Na tela inicial, **Jogar** abre a escolha de modo: **Contra o computador** (escolhe o adversário
e já começa) ou **2 jogadores** no mesmo aparelho. Adversários: **Turista** (joga no chute),
**Banhista** (acerta metade das vezes) e **Rato de Praia** (quase não erra). Estilo visual
(*Areia de Copacabana* ou *Papel de Pão*, com prévia de cada um) e som ficam em **Ajustes**.

Atalhos no teclado: `Enter`/`Espaço` tapa, `Backspace` limpa a seleção, `Esc` fecha os painéis
ou volta um passo no menu. Com o foco num buraco (navegação por `Tab`), `Enter` marca o buraco.

## A areia que tapa o buraco

Ao confirmar, cada buraco da linha recebe uma pazada de areia, na ordem da linha, com 100 ms
entre um e outro. Os grãos saem da pá em arco, quicam pouco, formam um montinho que escorre e
assenta nivelado, com a marca de areia remexida. Tudo num `<canvas>` 2D sobre o tabuleiro:

- **Grãos**: sistema de partículas com pool fixo (posição, velocidade, gravidade, quique com
  atrito), a partir do capítulo de partículas do *The Nature of Code* (Daniel Shiffman).
- **Acúmulo**: um campo de altura de 16×16 por buraco com a regra da *falling sand* (Coding
  Train; Sandspiel, de Max Bittker, MIT) adaptada à vista de cima: a areia escorre para a
  vizinha mais baixa quando passa do ângulo de repouso. Só os conceitos foram usados, nenhum
  código.
- **Recorte**: o enchimento é desenhado com `clip()` no contorno do buraco; grãos no ar ou que
  espirram para fora vão numa camada sem recorte, com sombra no chão.

A jogada vale no instante da confirmação; a animação só decide quando o buraco aparece tapado e
quando a vez passa. Os parâmetros (`graos`, `gravidade`, `atrito`, `elasticidade`, `duracao`,
`intervalo`, `maxParticulas`, `grade`, `repouso`, `profundidade`) ficam no objeto `AREIA` do
`index.html` e podem ser ajustados ao vivo pelo console em `TAPABURACO.areia`. Com movimento
reduzido, o buraco só se enche de areia em 220 ms, sem grãos nem pazinha.

## O diorama 3D

Na web, a praia é uma maquete em 3D (Three.js 0.169.0, só as classes usadas, embutido no próprio
`index.html`, que continua único e offline). O 3D só desenha: as regras ficam em `MOTOR`/`J` e os
28 buracos continuam sendo `<button>` HTML transparentes sobre a projeção de cada buraco na câmera
ortográfica, então mouse, toque, teclado e leitor de tela usam o mesmo botão. No 3D, o campo de
altura da areia vira a malha que sobe dentro da cavidade e os grãos viram um `InstancedMesh`.
Sem WebGL (ou com o contexto perdido), volta o tabuleiro 2D com o canvas de areia descrito acima.

A luz segue o modo "Baked Indirect": um sol em tempo real com sombra; o resto é assado na
montagem. Um shader avalia o relevo analítico uma vez por texel numa textura de altura e, dela,
saem a normal (com grão, marolas e marcas de escavação) e a oclusão ambiental (normal baking na
GPU); o ambiente vira uma probe de harmônicos esféricos no lugar da luz de hemisfério; os objetos
têm smooth com vinco, chanfro e luz de recorte por fresnel. A cena é desenhada em HDR e passa por
bloom, color grading, vinheta e neblina de distância, todos discretos.

Cada buraco tem forma própria (contorno levemente ondulado e ovalado, fundo inclinado, areia
tirada amontoada de um lado), tirada de um hash da posição: estável em toda partida, sem mexer
no centro, no passo nem no alvo de toque. O enchimento segue essa forma, se espalha um pouco
por cima do chão e termina numa superfície remexida mais clara, com a marca curta da pá; o
estado reconstruído sem animação é o mesmo do fim da animação. A seleção, o hover e o perigo
são fitas desenhadas no chão, acompanhando o contorno; a faixa da linha em jogo é pintada no
próprio chão. O vendedor de mate e o banhista são personagens da interface (SVG com o traço de
tinta dos cartões), não do diorama. Capturas antes/depois em [`docs/capturas`](docs/capturas).

Para reduzir a trava da primeira jogada, depois do primeiro quadro o diorama prepara, em
tempo ocioso, os shaders de seleção, enchimento, pá e grãos com `compileAsync`, usando o
mesmo alvo HDR do desenho. A primeira malha de enchimento e a pá preparada ficam invisíveis
e são reutilizadas na partida. Sincronizar o tabuleiro sem mudar as bandeiras não invalida
mais o mapa de sombras; a animação continua atualizando suas sombras normalmente. Resolução,
geometria, materiais, partículas e pós-processamento não foram reduzidos.

Verificação no Edge: no cenário de selecionar e tapar os sete buracos da primeira fileira,
as compilações de shader durante a interação passaram de 14 para 4. Doze sincronizações
do tabuleiro parado passaram de 12 atualizações de sombra para zero. O canvas 3D antes/depois,
com o mar congelado e o enchimento assentado, foi idêntico pixel a pixel. Também foram
exercitados partida completa, teclado, movimento reduzido e viewport móvel de 390×844;
isso não substitui medição de desempenho num celular físico.

Câmera, luz, céu, sombra, pixel ratio, cores e ondas ficam no objeto `DIORAMA` do `index.html`.
Decisões e consequências em [`docs/adr/0001-diorama-3d-web.md`](docs/adr/0001-diorama-3d-web.md).
O Three.js embutido é regenerado por `bun tools/diorama/build.mjs`; o porte para Unity do
diorama fica para quando houver editor para validar.

## A matemática por trás

É um jogo imparcial **misère** sobre a escada de 28 buracos: cada lance tira um trecho contíguo de uma
fileira ou de uma coluna. Os buracos abertos cabem num inteiro de 28 bits e os 140 trechos possíveis
são máscaras fixas; a escada é simétrica pela diagonal, então posição e espelho dividem o mesmo
resultado.

O *Rato de Praia* usa busca exata (memória de 4 Mi de posições, ~20 MB) num Web Worker:

- **Abertura pré-calculada.** O primeiro lance vencedor com o tabuleiro cheio e a resposta vencedora a
  112 dos 140 primeiros lances do adversário vêm embutidos (busca exaustiva offline). Pela regra, quem
  começa ganha com jogo perfeito.
- **Meio e fim.** Com 23 buracos abertos ou menos a prova sai praticamente na hora; antes disso cada
  lance tem 2,5 s. Se o tempo acaba sem achar vitória (ou se não existe vitória), ele tapa um buraco só
  e deixa o tabuleiro complicado. Por isso é "quase": nas primeiras jogadas ele pode errar.

## Estrutura do repositório

| Caminho | Papel |
|---|---|
| `index.html` | O jogo web completo — **é o que a página publicada serve** |
| `unity/` | Porte para Unity 6 (6000.0.58f1), uGUI, sem prefabs e sem cena montada |
| `tools/CoreTests/` | Verificação headless das regras (~78 mil verificações) |
| `tools/Harness/` | Shim de `UnityEngine` + simulação headless da camada de apresentação |
| `tools/art/` | SVGs de origem e rasterização para `unity/Assets/Resources/Art` |
| `_r1.md`, `_r2.md` | Pesquisa para a fase futura de simulação de areia/água (SPH e PIC/FLIP) |

## O porte para Unity

- `Assets/Scripts/Core` (`TapaBuraco.Core`, asmdef com `noEngineReferences`): regras da escada,
  tabuleiro em bitmask de 28 bits, os 140 trechos, solver misère com tabela de transposição e
  tempo-limite, livro de abertura (o mesmo da web) e IA. **Zero `UnityEngine`**: por isso roda no
  `dotnet` puro e é testado de verdade.
  A busca do Rato roda no thread pool (`GameSession.ThinkMachineMove`), então um build WebGL, que
  não tem threads, ainda precisaria de outra estratégia.
- `Assets/Scripts/Game` (`TapaBuraco.Game`): apresentação. `GameApp` é o único MonoBehaviour dono do
  fluxo; as telas são classes normais que montam a própria subárvore de UI.
  - Arte de interface **gerada em código** (`SpriteFactory`) e tingida pela `Palette` — a troca de
    skin é instantânea.
  - Áudio **sintetizado em runtime** (`ProceduralAudio`), portado do grafo Web Audio do protótipo:
    areia, toque, erro, vitória, derrota, ondas, gaivotas e a musiquinha de 76 bpm.
  - Preferências e placar em `PlayerPrefs`, chave `tapaburaco.v1` (a mesma do `localStorage` da web).
- A cena `Assets/Scenes/Main.unity` é **vazia de propósito**: `GameApp.Boot` roda em
  `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` e cria câmera, `AudioListener`, canvas e telas.
  Não há referência serializada para quebrar.

Para rodar: abrir a pasta `unity/` no Unity 6000.0.58f1 e dar Play.

## Verificação (sem precisar do Unity)

```bash
cd tools/CoreTests && dotnet run -c Release   # ~78 mil verificações: lances, bloqueio, solver x minimax, livro de abertura
cd tools/Harness   && dotnet run -c Release   # 120 verificações: boot → título → nível → partida → fim → revanche → menu → 2 jogadores
```

O harness compila o núcleo **e** a camada de apresentação contra um shim de `UnityEngine`
(`tools/Harness/Shim`), com hierarquia de objetos, ciclo de vida, corrotinas em tempo virtual, eventos
de ponteiro e leitura real dos PNGs de `Resources`. Ele joga uma partida inteira só por cliques.

**O que ele não prova** (exige abrir o editor): layout resolvido pelos `LayoutGroup`/`ContentSizeFitter`,
raycast e ordem de desenho, rasterização/fontes e o áudio audível.

## Publicação

A página vem da branch `main`, pasta raiz, via **GitHub Pages** (Settings ▸ Pages ▸ *Deploy from a
branch* ▸ `main` / `/ (root)`). Como o jogo é um `index.html` autossuficiente, publicar é só dar push.

## Licença e créditos

Jogo, arte e código por [@vitormontes](https://github.com/vitormontes).
Fontes: [Anton](https://fonts.google.com/specimen/Anton) (títulos, web e Unity),
[Nunito](https://fonts.google.com/specimen/Nunito) (texto da web, embutida em WOFF2) e
[Crimson Text](https://fonts.google.com/specimen/Crimson+Text) (texto da Unity), todas sob SIL
Open Font License (cópias da Unity em `unity/Assets/Resources/Fonts/`).
