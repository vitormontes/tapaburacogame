using System.Collections.Generic;
using TapaBuraco.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TapaBuraco.Game
{
    /// <summary>
    /// Tela de preferências ("Ajeita a areia") — equivale a <c>#tela-setup</c> do protótipo
    /// (index.html 725–776). Escreve direto no <see cref="GameSettings"/> recebido e avisa
    /// por <see cref="Changed"/>; quem salva em disco é o integrador.
    /// </summary>
    public sealed class SetupScreen
    {
        private readonly RectTransform _root;
        private readonly GameSettings _settings;

        private readonly PanelView _wrap;
        private readonly Text _titulo;
        private readonly List<Text> _rotulos = new List<Text>(5);
        private readonly List<Image> _reguas = new List<Image>(5);
        private readonly List<OptionView> _opcoes = new List<OptionView>(12);

        private readonly OptionView _modoCpu;
        private readonly OptionView _modoDupla;

        private readonly RectTransform _grupoNivel;
        private readonly OptionView _nivelTurista;
        private readonly OptionView _nivelBanhista;
        private readonly OptionView _nivelRato;

        private readonly OptionView _varianteLivre;
        private readonly OptionView _varianteVizinhos;

        private readonly OptionView _skinPraia;
        private readonly OptionView _skinPapel;

        private readonly OptionView _somEfeitos;
        private readonly OptionView _somAmbiente;
        private readonly OptionView _somMusica;

        private readonly ButtonView _voltar;
        private readonly ButtonView _comecar;

        /// <summary>Monta a tela e já reflete o estado de <paramref name="settings"/>.</summary>
        public SetupScreen(Transform parent, GameSettings settings)
        {
            _settings = settings;
            _root = UiKit.Stretch(parent, "tela-setup");

            // A lista é comprida em telas baixas; o CSS usava overflow:auto, aqui um ScrollRect.
            Image fundo = UiKit.Picture(_root, "area-rolagem", SpriteFactory.Solid(), Color.clear, true);
            UiKit.FillParent(fundo.rectTransform);

            RectTransform viewport = UiKit.Stretch(_root, "viewport");
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform conteudo = UiKit.Node(viewport, "conteudo");
            conteudo.anchorMin = new Vector2(0f, 1f);
            conteudo.anchorMax = new Vector2(1f, 1f);
            conteudo.pivot = new Vector2(0.5f, 1f);
            conteudo.offsetMin = new Vector2(0f, 0f);
            conteudo.offsetMax = new Vector2(0f, 0f);
            VerticalLayoutGroup pilha = UiKit.Column(conteudo, 0f);
            pilha.childForceExpandWidth = true;
            pilha.padding = new RectOffset(
                ScreenPanels.Px(10f),
                ScreenPanels.Px(10f),
                ScreenPanels.Px(10f),
                ScreenPanels.Px(10f));
            var fitter = conteudo.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = _root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = UiKit.Css(18f);
            scroll.viewport = viewport;
            scroll.content = conteudo;

            _wrap = UiKit.Panel(conteudo, "setup-wrap");
            RectTransform caixa = ScreenPanels.AutoHeight(_wrap, 14f, 14f, 18f, 12f, false);

            _titulo = UiKit.Label(caixa, "titulo", "Ajeita a areia", UiKit.Sign, UiKit.FontSize(22f, 7f, 40f), Palette.Creme);
            UiKit.InkOutline(_titulo, UiKit.Css(2f), Palette.Ink);

            // ------------------------------------------------------------- Modo
            RectTransform gModo = Grupo(caixa, "g-modo", "MODO");
            RectTransform lModo = Linha(gModo);
            _modoCpu = Opcao(lModo, "cpu", "Contra o computador", null, 46f);
            _modoDupla = Opcao(lModo, "2p", "2 jogadores", "passa-e-joga", 46f);
            _modoCpu.Button.onClick.AddListener(() => TrocaModo(GameMode.Cpu));
            _modoDupla.Button.onClick.AddListener(() => TrocaModo(GameMode.DoisJogadores));

            // ------------------------------------------------------------- Nível
            _grupoNivel = Grupo(caixa, "grupo-nivel", "NÍVEL");
            RectTransform lNivel = Linha(_grupoNivel);
            _nivelTurista = Opcao(lNivel, "turista", "Turista", "joga no chute", 64f);
            _nivelBanhista = Opcao(lNivel, "banhista", "Banhista de Domingo", "acerta às vezes", 64f);
            _nivelRato = Opcao(lNivel, "rato", "Rato de Praia", "não erra nunca", 64f);
            _nivelTurista.Button.onClick.AddListener(() => TrocaNivel(AiLevel.Turista));
            _nivelBanhista.Button.onClick.AddListener(() => TrocaNivel(AiLevel.Banhista));
            _nivelRato.Button.onClick.AddListener(() => TrocaNivel(AiLevel.Rato));

            // ------------------------------------------------------------- Variante
            RectTransform gVariante = Grupo(caixa, "g-variante", "VARIANTE");
            RectTransform lVariante = Linha(gVariante);
            _varianteLivre = Opcao(lVariante, "livre", "Livre", "quaisquer buracos da fileira", 56f);
            _varianteVizinhos = Opcao(lVariante, "vizinhos", "Vizinhos", "só em sequência", 56f);
            _varianteLivre.Button.onClick.AddListener(() => TrocaVariante(Variant.Livre));
            _varianteVizinhos.Button.onClick.AddListener(() => TrocaVariante(Variant.Vizinhos));

            // ------------------------------------------------------------- Estilo
            RectTransform gSkin = Grupo(caixa, "g-skin", "ESTILO");
            RectTransform lSkin = Linha(gSkin);
            _skinPraia = Opcao(lSkin, "praia", "Areia de Copacabana", null, 46f);
            _skinPapel = Opcao(lSkin, "papel", "Papel de Pão", "caneta azul", 46f);
            _skinPraia.Button.onClick.AddListener(() => TrocaSkin(Skin.Praia));
            _skinPapel.Button.onClick.AddListener(() => TrocaSkin(Skin.Papel));

            // ------------------------------------------------------------- Som
            RectTransform gSom = Grupo(caixa, "g-som", "SOM");
            RectTransform lSom = Linha(gSom);
            _somEfeitos = Opcao(lSom, "sfx", "Efeitos", null, 38f);
            _somAmbiente = Opcao(lSom, "amb", "Ondas e gaivotas", null, 38f);
            _somMusica = Opcao(lSom, "mus", "Musiquinha", null, 38f);
            _somEfeitos.Button.onClick.AddListener(AlternaEfeitos);
            _somAmbiente.Button.onClick.AddListener(AlternaAmbiente);
            _somMusica.Button.onClick.AddListener(AlternaMusica);

            // ------------------------------------------------------------- botões
            RectTransform linhaBtn = UiKit.Node(caixa, "linha-btn");
            UiKit.Row(linhaBtn, 10f);
            UiKit.Size(linhaBtn, 0f, 56f);

            _voltar = UiKit.Button(linhaBtn, "setup-voltar", "Voltar", ButtonStyle.Small);
            UiKit.Size(_voltar.Root, 100f, 40f);
            _voltar.Button.onClick.AddListener(() => BackClicked?.Invoke());

            _comecar = UiKit.Button(linhaBtn, "comecar", "Começar", ButtonStyle.Coral);
            UiKit.Size(_comecar.Root, 150f, 52f);
            _comecar.Button.onClick.AddListener(() => StartClicked?.Invoke());

            Refresh();
            ApplySkin();
        }

        /// <summary>Raiz da subárvore.</summary>
        public RectTransform Root => _root;

        /// <summary>Alguma preferência mudou — o integrador salva e aplica o mix de som.</summary>
        public event System.Action Changed;

        /// <summary>"Começar".</summary>
        public event System.Action StartClicked;

        /// <summary>"Voltar".</summary>
        public event System.Action BackClicked;

        /// <summary>Liga/desliga a tela inteira.</summary>
        public void SetVisible(bool visible)
        {
            if (_root.gameObject.activeSelf != visible)
            {
                _root.gameObject.SetActive(visible);
            }
        }

        /// <summary>Relê o <see cref="GameSettings"/> e repinta o estado de cada opção.</summary>
        public void Refresh()
        {
            _modoCpu.Pressed = _settings.mode == GameMode.Cpu;
            _modoDupla.Pressed = _settings.mode == GameMode.DoisJogadores;

            // Sem máquina no jogo, o grupo de nível não faz sentido (igual ao display:none do JS).
            bool contraMaquina = _settings.mode == GameMode.Cpu;
            if (_grupoNivel.gameObject.activeSelf != contraMaquina)
            {
                _grupoNivel.gameObject.SetActive(contraMaquina);
            }

            _nivelTurista.Pressed = _settings.level == AiLevel.Turista;
            _nivelBanhista.Pressed = _settings.level == AiLevel.Banhista;
            _nivelRato.Pressed = _settings.level == AiLevel.Rato;

            _varianteLivre.Pressed = _settings.variant == Variant.Livre;
            _varianteVizinhos.Pressed = _settings.variant == Variant.Vizinhos;

            _skinPraia.Pressed = _settings.skin == Skin.Praia;
            _skinPapel.Pressed = _settings.skin == Skin.Papel;

            _somEfeitos.Pressed = _settings.sfx;
            _somAmbiente.Pressed = _settings.ambience;
            _somMusica.Pressed = _settings.music;
        }

        /// <summary>Repinta painel, rótulos, opções e botões na skin corrente.</summary>
        public void ApplySkin()
        {
            UiKit.RepaintPanel(_wrap);

            _titulo.color = Palette.IsPaper ? Palette.Caneta : Palette.Creme;

            for (int i = 0; i < _rotulos.Count; i++)
            {
                _rotulos[i].color = Palette.AccentDark;
            }

            for (int i = 0; i < _reguas.Count; i++)
            {
                _reguas[i].color = Palette.Ink.WithAlpha(0.3f);
            }

            for (int i = 0; i < _opcoes.Count; i++)
            {
                _opcoes[i].Repaint();
            }

            UiKit.RepaintButton(_voltar);
            UiKit.RepaintButton(_comecar);
        }

        // ------------------------------------------------------------------ montagem

        /// <summary>Um <c>.grupo</c>: rótulo de cartaz, régua pontilhada e a linha de opções.</summary>
        private RectTransform Grupo(RectTransform parent, string nome, string rotulo)
        {
            RectTransform grupo = UiKit.Node(parent, nome);
            VerticalLayoutGroup coluna = UiKit.Column(grupo, 6f, TextAnchor.UpperLeft);
            coluna.childForceExpandWidth = true;

            RectTransform cabeca = UiKit.Node(grupo, "cabeca");
            VerticalLayoutGroup pilha = UiKit.Column(cabeca, 2f, TextAnchor.UpperLeft);
            pilha.childForceExpandWidth = true;

            Text texto = UiKit.Label(cabeca, "rot", rotulo, UiKit.Sign, UiKit.FontSize(13f, 3.6f, 17f), Palette.AccentDark, TextAnchor.LowerLeft);
            texto.horizontalOverflow = HorizontalWrapMode.Overflow;
            _rotulos.Add(texto);

            // O CSS usa border-bottom dotted; aqui uma régua fina de tinta translúcida basta.
            Image regua = UiKit.Picture(cabeca, "regua", SpriteFactory.Solid(), Palette.Ink.WithAlpha(0.3f));
            UiKit.Size(regua, 0f, 1.2f);
            _reguas.Add(regua);

            return grupo;
        }

        /// <summary>Linha de opções: todas com a mesma largura, como o <c>flex:1 1 auto</c>.</summary>
        private static RectTransform Linha(RectTransform grupo)
        {
            RectTransform linha = UiKit.Node(grupo, "opts");
            HorizontalLayoutGroup row = UiKit.Row(linha, 7f);
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = true;
            return linha;
        }

        private OptionView Opcao(RectTransform linha, string nome, string rotulo, string dica, float alturaCss)
        {
            OptionView opcao = UiKit.Option(linha, nome, rotulo, dica);
            UiKit.Size(opcao.Root, 0f, alturaCss);
            _opcoes.Add(opcao);
            return opcao;
        }

        // ------------------------------------------------------------------ reações

        private void TrocaModo(GameMode modo)
        {
            _settings.mode = modo;

            // Trocar de modo troca de adversário: o placar acumulado perde o sentido.
            _settings.score[0] = 0;
            _settings.score[1] = 0;

            Refresh();
            Changed?.Invoke();
        }

        private void TrocaNivel(AiLevel nivel)
        {
            _settings.level = nivel;
            Refresh();
            Changed?.Invoke();
        }

        private void TrocaVariante(Variant variante)
        {
            _settings.variant = variante;
            Refresh();
            Changed?.Invoke();
        }

        private void TrocaSkin(Skin skin)
        {
            _settings.skin = skin;
            Palette.SetSkin(skin);

            // A própria tela é a primeira a ter de mudar de cor — o resto o integrador repinta.
            Refresh();
            ApplySkin();
            Changed?.Invoke();
        }

        private void AlternaEfeitos()
        {
            _settings.sfx = !_settings.sfx;
            Refresh();
            Changed?.Invoke();
        }

        private void AlternaAmbiente()
        {
            _settings.ambience = !_settings.ambience;
            Refresh();
            Changed?.Invoke();
        }

        private void AlternaMusica()
        {
            _settings.music = !_settings.music;
            Refresh();
            Changed?.Invoke();
        }
    }
}
