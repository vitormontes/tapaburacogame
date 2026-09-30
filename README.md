# 🏖️ TAPA BURACO

> Jogo de praia carioca. Sete fileiras cavadas na areia — 1, 2, 3, 4, 5, 6 e 7 buracos, **28 no total**.
> Na sua vez você escolhe **uma única fileira** e tapa **quantos buracos quiser** dela.
> **Quem tapar o último buraco do tabuleiro perde** — e leva um caldo.

## ▶️ Jogar agora

**<https://vitormontes.github.io/tapaburacogame/>**

Roda no navegador do celular ou do computador, sem instalar nada e **sem conexão depois de carregar**:
o jogo inteiro é um arquivo só (`index.html`, ~79 KB) — arte em SVG desenhada à mão, som sintetizado
na hora com Web Audio e nenhuma dependência externa.

> 🔈 **No iPhone**: o Safari silencia o áudio da web quando a chavinha lateral está no mudo.
> Se o jogo ficar sem som, desligue o silencioso.

## Como se joga

- Escolha uma fileira e marque os buracos que quiser tapar; confirme na **pazinha TAPAR**.
- Tocar num buraco de outra fileira limpa a marcação anterior.
- Variante **Livre**: valem quaisquer buracos da fileira.
- Variante **Vizinhos**: só valem buracos grudados em sequência — e tapar no meio parte a fileira em duas.
- **Quem tapar o último buraco perde.**

Adversários: **Turista** (joga no chute), **Banhista de Domingo** (acerta metade das vezes) e
**Rato de Praia** (estratégia perfeita de Nim misère — só erra se você obrigar). Também dá para jogar
2 jogadores no mesmo aparelho. Dois estilos visuais: *Areia de Copacabana* e *Papel de Pão*.

Atalhos no teclado: `Enter`/`Espaço` tapa, `Backspace` desfaz, `Esc` fecha as regras.

## A matemática por trás

É **Nim misère**. Cada fileira é uma pilha:

- Na variante **Livre**, tirar $k$ buracos de uma fileira é exatamente tirar $k$ peças de uma pilha —
  Nim clássico, com a regra invertida de quem tira a última peça.
- Na variante **Vizinhos**, tapar no meio parte a pilha em duas: o jogo vira o **jogo octal .777**.

A estratégia perfeita do *Rato de Praia* não é heurística: é busca exaustiva memoizada sobre as
partições com partes $\le 7$ e soma $\le 28$ (8.560 estados por variante), e na variante Livre ela é
cruzada com a fórmula fechada do Nim misère.

## Estrutura do repositório

| Caminho | Papel |
|---|---|
| `index.html` | O jogo web completo — **é o que a página publicada serve** |
| `unity/` | Porte para Unity 6 (6000.0.58f1), uGUI, sem prefabs e sem cena montada |
| `tools/CoreTests/` | Verificação headless das regras (240.253 verificações) |
| `tools/Harness/` | Shim de `UnityEngine` + simulação headless da camada de apresentação |
| `tools/art/` | SVGs de origem e rasterização para `unity/Assets/Resources/Art` |
| `_r1.md`, `_r2.md` | Pesquisa para a fase futura de simulação de areia/água (SPH e PIC/FLIP) |

## O porte para Unity

- `Assets/Scripts/Core` (`TapaBuraco.Core`, asmdef com `noEngineReferences`): regras, tabuleiro em
  bitmask de 28 bits, gerador de lances, solver misère memoizado e IA. **Zero `UnityEngine`** — por
  isso roda no `dotnet` puro e é testado de verdade.
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
cd tools/CoreTests && dotnet run -c Release   # 240.253 verificações das regras misère
cd tools/Harness   && dotnet run -c Release   # 64 verificações: boot → título → setup → partida → fim → revanche
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
