using TapaBuraco.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TapaBuraco.Game
{
    /// <summary>
    /// Ajudantes de layout compartilhados pelas telas do cartaz.
    ///
    /// Motivo: o protótipo web deixa o painel crescer com o texto (<c>.painel</c> é um bloco
    /// comum). No uGUI isso exige um <see cref="VerticalLayoutGroup"/> no painel, mas a sombra,
    /// a borda e o miolo do <see cref="UiKit.Panel"/> são camadas esticadas que NÃO podem entrar
    /// no fluxo — por isso saem do layout com <see cref="LayoutElement.ignoreLayout"/> e
    /// continuam acompanhando o tamanho do painel pelas âncoras.
    /// </summary>
    internal static class ScreenPanels
    {
        /// <summary>Medida do CSS arredondada para inteiro (padding de layout só aceita int).</summary>
        internal static int Px(float cssPx) => Mathf.RoundToInt(UiKit.Css(cssPx));

        /// <summary>Tira um gráfico do fluxo de layout mantendo as âncoras dele.</summary>
        internal static LayoutElement Ignore(Component target)
        {
            LayoutElement element = target.gameObject.GetComponent<LayoutElement>();
            if (element == null)
            {
                element = target.gameObject.AddComponent<LayoutElement>();
            }

            element.ignoreLayout = true;
            return element;
        }

        /// <summary>
        /// Faz o painel crescer conforme o conteúdo e devolve a coluna onde os filhos entram.
        /// Passe <paramref name="fit"/> = false quando o painel já for filho de outro layout —
        /// nesse caso quem manda na altura é o grupo de fora, e um ContentSizeFitter só brigaria.
        /// </summary>
        internal static RectTransform AutoHeight(PanelView panel, float padXCss, float padTopCss, float padBottomCss, float spacingCss, bool fit = true)
        {
            Ignore(panel.Shadow);
            Ignore(panel.Border);
            Ignore(panel.Fill);

            var outer = panel.Root.gameObject.AddComponent<VerticalLayoutGroup>();
            outer.childAlignment = TextAnchor.UpperCenter;
            outer.childControlWidth = true;
            outer.childControlHeight = true;
            outer.childForceExpandWidth = true;
            outer.childForceExpandHeight = false;
            outer.padding = new RectOffset(Px(padXCss), Px(padXCss), Px(padTopCss), Px(padBottomCss));

            if (fit)
            {
                var fitter = panel.Root.gameObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            VerticalLayoutGroup column = UiKit.Column(panel.Content, spacingCss);
            column.childForceExpandWidth = true;
            return panel.Content;
        }
    }

    /// <summary>
    /// Tela de abertura: logotipo TAPA BURACO com o "O" virado buraco, o vendedor de mate e o
    /// menu em dois passos — modo ("Contra o computador" / "2 jogadores") e, contra a máquina,
    /// o adversário. Equivale a <c>#tela-titulo</c> do protótipo.
    /// Não é MonoBehaviour: monta a subárvore no construtor e avisa por eventos.
    /// </summary>
    public sealed class TitleScreen
    {
        private readonly RectTransform _root;
        private readonly Text _tapa;
        private readonly Text _buraco;
        private readonly Text _subtitulo;
        private readonly Outline _tapaContorno;
        private readonly Outline _buracoContorno;
        private readonly Image _cava;
        private readonly PanelView _balao;
        private readonly Image _rabicho;
        private readonly Text _balaoTexto;
        private readonly RectTransform _escolhaModo;
        private readonly RectTransform _escolhaNivel;
        private readonly ButtonView _cpu;
        private readonly ButtonView _doisJogadores;
        private readonly OptionView[] _niveis = new OptionView[3];
        private readonly ButtonView _voltar;
        private readonly ButtonView _regras;
        private readonly ButtonView _ajustes;

        /// <summary>Monta a tela dentro de <paramref name="parent"/> (normalmente o Canvas).</summary>
        public TitleScreen(Transform parent)
        {
            _root = UiKit.Stretch(parent, "tela-titulo", UiKit.Css(10f));

            // ---------------------------------------------------------- topo: logotipo
            RectTransform topo = UiKit.Node(_root, "topo");
            topo.anchorMin = new Vector2(0.5f, 1f);
            topo.anchorMax = new Vector2(0.5f, 1f);
            topo.pivot = new Vector2(0.5f, 1f);
            topo.sizeDelta = new Vector2(UiKit.ReferenceWidth - UiKit.Css(24f), 0f);
            topo.anchoredPosition = new Vector2(0f, -UiKit.Css(14f));

            // line-height .86 do CSS: as duas linhas do logotipo se encavalam de propósito.
            VerticalLayoutGroup coluna = UiKit.Column(topo, -10f);
            coluna.childForceExpandWidth = true;
            var topoFit = topo.gameObject.AddComponent<ContentSizeFitter>();
            topoFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            int tamanho1 = UiKit.FontSize(34f, 11f, 90f);
            _tapa = UiKit.Label(topo, "l1", "TAPA", UiKit.Sign, tamanho1, Palette.Creme);
            _tapa.horizontalOverflow = HorizontalWrapMode.Overflow;
            _tapaContorno = UiKit.InkOutline(_tapa, UiKit.Css(3.6f), Palette.Ink);
            _tapa.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -2f);

            int tamanho2 = UiKit.FontSize(46f, 15.5f, 124f);
            RectTransform linha2 = UiKit.Node(topo, "l2");
            UiKit.Row(linha2, 0f);
            linha2.localRotation = Quaternion.Euler(0f, 0f, 1f);

            _buraco = UiKit.Label(linha2, "texto", "BURAC", UiKit.Sign, tamanho2, Palette.Coral);
            _buraco.horizontalOverflow = HorizontalWrapMode.Overflow;
            _buracoContorno = UiKit.InkOutline(_buraco, UiKit.Css(4f), Palette.Ink);

            // O último "O" é um buraco de verdade: cava desenhada + a pazinha fincada nela.
            RectTransform buracoDoO = UiKit.Node(linha2, "o-buraco");
            UiKit.Size(buracoDoO, 63f, 77f);

            _cava = UiKit.Picture(buracoDoO, "cava", SpriteFactory.Cava(160), Color.white);
            RectTransform cavaRt = _cava.rectTransform;
            cavaRt.anchorMin = new Vector2(0.02f, 0.02f);
            cavaRt.anchorMax = new Vector2(0.98f, 0.66f);
            cavaRt.offsetMin = Vector2.zero;
            cavaRt.offsetMax = Vector2.zero;

            Image pazinha = UiKit.Picture(buracoDoO, "pazinha", UiKit.Art("pazinha"), Color.white);
            pazinha.preserveAspect = true;
            RectTransform pazinhaRt = pazinha.rectTransform;
            pazinhaRt.anchorMin = new Vector2(0.44f, 0.36f);
            pazinhaRt.anchorMax = new Vector2(1.02f, 1.08f);
            pazinhaRt.offsetMin = Vector2.zero;
            pazinhaRt.offsetMax = Vector2.zero;
            pazinhaRt.localRotation = Quaternion.Euler(0f, 0f, -22f);

            RectTransform espaco = UiKit.Node(topo, "espaco");
            UiKit.Size(espaco, 0f, 12f);

            // O CSS aplica text-transform:uppercase e letter-spacing .22em; o Text legado não
            // tem espaçamento entre letras, então o caixa-alta já vem escrito no literal.
            _subtitulo = UiKit.Label(topo, "sub", "PRAIA DE COPACABANA • JOGO DE AREIA", UiKit.BodyItalic, UiKit.FontSize(11f, 3.1f, 17f), Palette.Tinta.WithAlpha(0.75f));

            // ---------------------------------------------------------- meio: mascote + balão
            RectTransform meio = UiKit.Node(_root, "meio");
            meio.anchorMin = Vector2.zero;
            meio.anchorMax = Vector2.one;
            meio.offsetMin = new Vector2(0f, UiKit.Css(132f));
            meio.offsetMax = new Vector2(0f, -UiKit.Css(150f));
            UiKit.Row(meio, 12f);

            Image mascote = UiKit.Picture(meio, "mascote", UiKit.Art("mascote-mate"), Color.white);
            mascote.preserveAspect = true;
            UiKit.Size(mascote, 165f, 320f);

            _balao = UiKit.Panel(meio, "balao", 14f, 3f, 6f);
            RectTransform balaoConteudo = ScreenPanels.AutoHeight(_balao, 14f, 12f, 12f, 4f, false);
            UiKit.Size(_balao.Root, 180f, 0f);

            _balaoTexto = UiKit.Label(
                balaoConteudo,
                "texto",
                "<b>Ô meu rei!</b>\nTapa quantos buracos quiser em <b>linha reta</b>, deitada ou em pé — mas <b>quem tapar o último, leva um caldo!</b>",
                UiKit.Body,
                UiKit.FontSize(12f, 3.4f, 16f),
                Palette.Text,
                TextAnchor.UpperLeft);
            _balaoTexto.lineSpacing = 1.05f;

            // O rabicho do balão (.balao::before) aponta para o mascote.
            _rabicho = UiKit.Picture(_balao.Root, "rabicho", SpriteFactory.TriangleLeft(48), Palette.Ink);
            ScreenPanels.Ignore(_rabicho);
            RectTransform rabichoRt = _rabicho.rectTransform;
            rabichoRt.anchorMin = new Vector2(0f, 0f);
            rabichoRt.anchorMax = new Vector2(0f, 0f);
            rabichoRt.pivot = new Vector2(1f, 0.5f);
            rabichoRt.sizeDelta = new Vector2(UiKit.Css(11f), UiKit.Css(18f));
            rabichoRt.anchoredPosition = new Vector2(UiKit.Css(1f), UiKit.Css(26f));

            // ---------------------------------------------------------- base: modo → adversário
            RectTransform baseColuna = UiKit.Node(_root, "base");
            baseColuna.anchorMin = new Vector2(0.5f, 0f);
            baseColuna.anchorMax = new Vector2(0.5f, 0f);
            baseColuna.pivot = new Vector2(0.5f, 0f);
            baseColuna.sizeDelta = new Vector2(UiKit.ReferenceWidth - UiKit.Css(24f), UiKit.Css(116f));
            baseColuna.anchoredPosition = new Vector2(0f, UiKit.Css(8f));
            VerticalLayoutGroup pilha = UiKit.Column(baseColuna, 12f, TextAnchor.LowerCenter);
            pilha.childForceExpandWidth = true;

            _escolhaModo = UiKit.Node(baseColuna, "escolha-modo");
            UiKit.Row(_escolhaModo, 12f);
            UiKit.Size(_escolhaModo, 0f, 64f);

            _cpu = UiKit.Button(_escolhaModo, "ir-cpu", "Contra o computador", ButtonStyle.Coral);
            UiKit.Size(_cpu.Root, 200f, 52f);
            _cpu.Button.onClick.AddListener(() => CpuClicked?.Invoke());

            _doisJogadores = UiKit.Button(_escolhaModo, "ir-2p", "2 jogadores", ButtonStyle.Creme);
            UiKit.Size(_doisJogadores.Root, 150f, 52f);
            _doisJogadores.Button.onClick.AddListener(() => TwoPlayersClicked?.Invoke());

            _escolhaNivel = UiKit.Node(baseColuna, "escolha-nivel");
            HorizontalLayoutGroup niveis = UiKit.Row(_escolhaNivel, 8f);
            niveis.childForceExpandWidth = true;
            niveis.childForceExpandHeight = true;
            UiKit.Size(_escolhaNivel, 0f, 64f);

            // O CSS aplica text-transform:uppercase; o Text legado não, então o caixa-alta vem no literal.
            _niveis[(int)AiLevel.Turista] = Nivel(AiLevel.Turista, "turista", "TURISTA", "joga no chute");
            _niveis[(int)AiLevel.Banhista] = Nivel(AiLevel.Banhista, "banhista", "BANHISTA", "acerta às vezes");
            _niveis[(int)AiLevel.Rato] = Nivel(AiLevel.Rato, "rato", "RATO DE PRAIA", "quase não erra");

            RectTransform links = UiKit.Node(baseColuna, "links");
            UiKit.Row(links, 6f);
            UiKit.Size(links, 0f, 34f);

            _voltar = Link(links, "nivel-voltar", "← Voltar", 96f);
            _voltar.Button.onClick.AddListener(() => BackClicked?.Invoke());
            _regras = Link(links, "ir-regras", "Como joga", 104f);
            _regras.Button.onClick.AddListener(() => RulesClicked?.Invoke());
            _ajustes = Link(links, "ir-ajustes", "Ajustes", 86f);
            _ajustes.Button.onClick.AddListener(() => SettingsClicked?.Invoke());

            ShowModeChoice();

            ApplySkin();
        }

        /// <summary>Raiz da subárvore — o integrador só liga/desliga e ordena as telas.</summary>
        public RectTransform Root => _root;

        /// <summary>"Contra o computador" — o integrador mostra a escolha do adversário.</summary>
        public event System.Action CpuClicked;

        /// <summary>"2 jogadores" — começa a partida passa-e-joga na hora.</summary>
        public event System.Action TwoPlayersClicked;

        /// <summary>Adversário tocado — começa a partida contra ele na hora.</summary>
        public event System.Action<AiLevel> LevelChosen;

        /// <summary>"← Voltar" da escolha de adversário.</summary>
        public event System.Action BackClicked;

        /// <summary>"Como joga".</summary>
        public event System.Action RulesClicked;

        /// <summary>"Ajustes".</summary>
        public event System.Action SettingsClicked;

        /// <summary>Primeiro passo do menu: os dois modos.</summary>
        public void ShowModeChoice() => ShowStep(false, AiLevel.Banhista);

        /// <summary>Segundo passo: os três adversários, com o último escolhido aceso.</summary>
        public void ShowLevelChoice(AiLevel lastUsed) => ShowStep(true, lastUsed);

        /// <summary>Liga/desliga a tela inteira (desligada não custa nada por quadro).</summary>
        public void SetVisible(bool visible)
        {
            if (_root.gameObject.activeSelf != visible)
            {
                _root.gameObject.SetActive(visible);
            }
        }

        /// <summary>Repinta tudo na skin corrente.</summary>
        public void ApplySkin()
        {
            bool papel = Palette.IsPaper;

            // No Papel de Pão o logotipo perde o contorno e vira caneta (body.skin-papel .logo).
            _tapa.color = papel ? Palette.Caneta : Palette.Creme;
            _buraco.color = papel ? Palette.CanetaEscura : Palette.Coral;
            _tapaContorno.effectColor = Palette.Ink.WithAlpha(papel ? 0f : 1f);
            _buracoContorno.effectColor = Palette.Ink.WithAlpha(papel ? 0f : 1f);
            _subtitulo.color = Palette.Text.WithAlpha(0.75f);

            // A cava vem colorida de fábrica; no papel ela some e sobra o traço do balde.
            _cava.color = Color.white.WithAlpha(papel ? 0.35f : 1f);

            UiKit.RepaintPanel(_balao);
            _rabicho.color = Palette.Ink;
            _balaoTexto.color = Palette.Text;

            UiKit.RepaintButton(_cpu);
            UiKit.RepaintButton(_doisJogadores);
            UiKit.RepaintButton(_voltar);
            UiKit.RepaintButton(_regras);
            UiKit.RepaintButton(_ajustes);
            for (int i = 0; i < _niveis.Length; i++)
            {
                _niveis[i].Repaint();
            }
        }

        private void ShowStep(bool levels, AiLevel lastUsed)
        {
            _escolhaModo.gameObject.SetActive(!levels);
            _escolhaNivel.gameObject.SetActive(levels);
            _voltar.Root.gameObject.SetActive(levels);
            for (int i = 0; i < _niveis.Length; i++)
            {
                _niveis[i].Pressed = levels && i == (int)lastUsed;
            }
        }

        /// <summary>Botão de adversário (o <c>.nivel</c>); aceso = último escolhido (<c>.nivel.ultimo</c>).</summary>
        private OptionView Nivel(AiLevel level, string nome, string rotulo, string dica)
        {
            OptionView opcao = UiKit.Option(_escolhaNivel, nome, rotulo, dica);
            UiKit.Size(opcao.Root, 0f, 64f);

            // `.nivel`: nome em letra de cartaz, maior que o das opções dos ajustes.
            opcao.Label.font = UiKit.Sign;
            opcao.Label.fontSize = UiKit.FontSize(13f, 3.8f, 19f);
            opcao.Button.onClick.AddListener(() => LevelChosen?.Invoke(level));
            return opcao;
        }

        /// <summary>Link discreto do rodapé (o <c>.link</c>).</summary>
        private static ButtonView Link(RectTransform parent, string nome, string rotulo, float larguraCss)
        {
            ButtonView link = UiKit.Button(parent, nome, rotulo, ButtonStyle.Small);
            UiKit.Size(link.Root, larguraCss, 32f);
            return link;
        }
    }
}
