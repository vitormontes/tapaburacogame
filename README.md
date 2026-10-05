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
  **pazinha TAPAR**.
- **Quem tapar o último buraco perde.**

Na tela inicial há só dois modos: **Contra o computador** (escolhe o adversário e já começa) e
**2 jogadores** no mesmo aparelho. Adversários: **Turista** (joga no chute), **Banhista** (acerta
metade das vezes) e **Rato de Praia** (quase não erra). Estilo visual (*Areia de Copacabana* ou
*Papel de Pão*) e som ficam em **Ajustes**.

Atalhos no teclado: `Enter`/`Espaço` tapa, `Backspace` desfaz, `Esc` fecha os painéis.

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
Fontes: [Anton](https://fonts.google.com/specimen/Anton) e
[Crimson Text](https://fonts.google.com/specimen/Crimson+Text), ambas sob SIL Open Font License
(cópias em `unity/Assets/Resources/Fonts/`).
