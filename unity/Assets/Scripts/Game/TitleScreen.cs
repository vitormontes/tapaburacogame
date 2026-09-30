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
    /// Tela de abertura: logotipo TAPA BURACO com o "O" virado buraco, o vendedor de mate
    /// e os dois botões. Equivale a <c>#tela-titulo</c> do protótipo (index.html 639–722).
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
        private readonly ButtonView _jogar;
        private readonly ButtonView _regras;

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
            meio.offsetMin = new Vector2(0f, UiKit.Css(78f));
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
                "<b>Ô meu rei!</b>\nCava sete fileiras na areia molhada, tapa quantos buracos quiser — mas <b>quem tapar o último, leva um caldo!</b>",
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

            // ---------------------------------------------------------- base: botões
            RectTransform baseLinha = UiKit.Node(_root, "base");
            baseLinha.anchorMin = new Vector2(0.5f, 0f);
            baseLinha.anchorMax = new Vector2(0.5f, 0f);
            baseLinha.pivot = new Vector2(0.5f, 0f);
            baseLinha.sizeDelta = new Vector2(UiKit.ReferenceWidth - UiKit.Css(24f), UiKit.Css(56f));
            baseLinha.anchoredPosition = new Vector2(0f, UiKit.Css(8f));
            UiKit.Row(baseLinha, 10f);

            _jogar = UiKit.Button(baseLinha, "ir-setup", "Jogar", ButtonStyle.Coral);
            UiKit.Size(_jogar.Root, 120f, 52f);
            _jogar.Button.onClick.AddListener(() => PlayClicked?.Invoke());

            _regras = UiKit.Button(baseLinha, "ir-regras", "Como joga", ButtonStyle.Creme);
            UiKit.Size(_regras.Root, 170f, 52f);
            _regras.Button.onClick.AddListener(() => RulesClicked?.Invoke());

            ApplySkin();
        }

        /// <summary>Raiz da subárvore — o integrador só liga/desliga e ordena as telas.</summary>
        public RectTransform Root => _root;

        /// <summary>"Jogar".</summary>
        public event System.Action PlayClicked;

        /// <summary>"Como joga".</summary>
        public event System.Action RulesClicked;

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

            UiKit.RepaintButton(_jogar);
            UiKit.RepaintButton(_regras);
        }
    }
}
