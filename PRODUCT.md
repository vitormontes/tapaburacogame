# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

- **Jogador casual no celular**: abre o jogo para uma partida rápida, sozinho, contra a IA, em qualquer lugar e a qualquer momento.
- **Amigos e família juntos**: jogam passa-e-joga no mesmo aparelho (modo "2 jogadores"), como a brincadeira acontece na praia de verdade.

Os dois públicos têm o mesmo peso. Quem chega pelo interesse na matemática (Nim misère) não é público prioritário.

## Product Purpose

Tapa Buraco é a versão digital de uma brincadeira de praia carioca. São sete fileiras cavadas na areia, em escada: 7 buracos no topo, depois 6, 5… até 1 (28 no total). Na sua vez você tapa quantos buracos quiser **em linha reta**, na horizontal (fileira) ou na vertical (coluna); diagonal não vale e um buraco já tapado bloqueia a linha. **Quem tapar o último buraco perde** e leva um caldo. (Regra definida pelo usuário em 2026-10-05, substituindo as variantes Livre/Vizinhos.)

Critério de sucesso: **em aberto**. Não foi definido se o objetivo é um projeto pessoal ou portfólio, publicar nas lojas via Unity, preservar a brincadeira ou ensinar Nim.

## Positioning

É uma brincadeira de praia carioca real, e não um Nim genérico com um tema aplicado por cima. O cenário (areia, buracos, pazinha, caldo), os nomes dos adversários e o vocabulário vêm da própria brincadeira. O adversário mais forte (Rato de Praia) usa busca exata com livro de abertura; nas primeiras jogadas pode errar, por isso é apresentado como "quase não erra".

## Operating Context

- Navegador do celular ou do computador, sem instalar nada. O jogo é publicado via GitHub Pages a partir de `main`, na raiz: <https://vitormontes.github.io/tapaburacogame/>.
- Tela inicial com um botão principal, **Jogar**, que abre a escolha entre dois modos (pedido do usuário: "menos botões, algo mais clean"; em 2026-10-05 o usuário preferiu o passo extra do Jogar a manter os dois modos na tela inicial): **Contra o computador**, que leva à escolha entre três adversários, e **2 jogadores** (passa-e-joga no mesmo aparelho). "Como joga" é ação secundária; estilo e som ficam em "Ajustes". Na Unity, por enquanto só os textos acompanharam ("Limpar seleção", "TAPAR n BURACOS"); o passo do Jogar ainda não foi portado.
- No iPhone, o Safari silencia o áudio da web quando a chave de silencioso está ligada. O jogo avisa isso no menu.

## Capabilities and Constraints

Estado atual (fato do repositório; o usuário **não** confirmou nenhum destes itens como compromisso fixo):

- A web inteira é um arquivo autossuficiente, `index.html` (~175 KB), que funciona offline depois de carregar. A arte é SVG feita à mão, o som é sintetizado com Web Audio e não há dependências externas. Anton (títulos) e Nunito 600/800 (texto) estão embutidas em WOFF2.
- **Regra**: lance = trecho contíguo de buracos abertos numa fileira ou numa coluna da escada (140 trechos possíveis); misère.
- **Adversários** (confirmado: manter os 3): Turista (joga no chute), Banhista (acerta metade das vezes) e Rato de Praia (busca exata com tempo-limite de 2,5 s, livro de abertura embutido e busca num Web Worker).
- **Skins**: Areia de Copacabana e Papel de Pão (caneta azul).
- **Som**: efeitos, ambiente (ondas e gaivotas) e musiquinha, cada um com chave própria.
- **Persistência**: preferências e placar ficam em `localStorage`/`PlayerPrefs`, sob a chave `tapaburaco.v1`, compartilhada entre web e Unity.
- **Teclado**: `Enter`/`Espaço` tapa, `Backspace` limpa a seleção, `Esc` fecha os painéis ou volta um passo no menu.
- **Acessibilidade já implementada**: `prefers-reduced-motion` (tira o tremor, a areia vira preenchimento simples sem grãos nem pazinha, a medalha entra estática e o cenário para), foco visível de teclado, rótulos ARIA no tabuleiro (com o estado "tapado") e nos botões de ícone, interruptores `role="switch"` no som, modais com foco gerenciado, alvos de toque de 44 px e `lang="pt-BR"`. Estados do buraco se distinguem por forma, não só cor: aberto é côncavo, marcado leva anel e bandeirinha, tapado vira areia remexida nivelada com contorno.
- **Efeito de tapar** (só web): ao confirmar, cada buraco da linha é preenchido por areia num canvas 2D (partículas com gravidade e quique + campo de altura com regra de "falling sand" vista de cima). O lance é aplicado em `J.tab` na confirmação; a física só decide quando cada buraco aparece tapado (`J.visivel`) e quando a vez passa. Parâmetros no objeto `AREIA` do `index.html` (grãos, gravidade, atrito, elasticidade, duração, intervalo, teto de partículas).
- **Porte Unity 6** (6000.0.58f1, uGUI) em `unity/`: núcleo de regras sem `UnityEngine`, verificado em `tools/CoreTests` e `tools/Harness`. O usuário confirmou que mudanças de regra e de menu valem para web **e** Unity. A plataforma de destino do porte está **em aberto**.
- Interface e textos em português (pt-BR).
- **Em pesquisa**: simulação de areia e água (SPH, PIC/FLIP), em `_r1.md` e `_r2.md`. O efeito de tapar não usa essa pesquisa: é um modelo local e barato por buraco.

Terminologia: fileira, buraco, tapar, pazinha (botão TAPAR), caldo (derrota), passa-e-joga, limpar seleção.

## Brand Commitments

Existentes no repositório, sem confirmação do usuário de que sejam obrigatórios:

- Nome: **TAPA BURACO**, com o "O" desenhado como um buraco na areia.
- Voz carioca, coloquial e bem-humorada: "leva um caldo", "Rato de Praia", "Banhista de Domingo", "joga no chute".
- Mascote: vendedor de mate na praia. Personagem do banhista (vitória, derrota, apresentação) nas variantes mar e coral.
- Fontes: Anton (títulos), Nunito (texto da web) e Crimson Text (texto da Unity), todas SIL OFL.
- Autoria: jogo, arte e código por @vitormontes.

## Evidence on Hand

- Jogo publicado e jogável (URL acima).
- Arte de origem em `tools/art/` (SVG) e renders 3D do banhista em `tools/art3d/out/`, copiados para `unity/Assets/Resources/Art/`.
- Material de referência de terceiros em `refs/`: pacotes Kenney (pirate kit, coaster kit, skyboxes, medals), áudio e Blender.
- Verificação automatizada das regras (números acima).
- **Não existem**: depoimentos, números de jogadores, avaliações, imprensa, presença em lojas ou preço. Trabalhos futuros não devem inventar nada disso.

## Product Principles

1. **Partida em segundos.** O jogador casual chega do celular e já está jogando; configurar nunca pode ficar entre ele e a primeira jogada.
2. **Um aparelho, duas pessoas.** O passa-e-joga é modo de primeira classe: de quem é a vez, o que foi marcado e quem ganhou precisam ficar evidentes para quem segura o aparelho e para quem está olhando.
3. **Celular primeiro.** Toque, uma mão, tela pequena, som que pode estar mudo. O computador também é suportado, mas não define as decisões.
4. **A regra cabe numa frase.** "Escolha uma fileira, tape quantos quiser, quem tapar o último perde." A matemática fica nos bastidores, a serviço do adversário.
5. **A brincadeira é a praia.** O tema não é decoração: cenário, vocabulário e consequências vêm da brincadeira carioca real.
