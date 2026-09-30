using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TapaBuraco.Game
{
    /// <summary>
    /// Modal "Como se joga" — equivale a <c>#modal-regras</c> do protótipo (index.html 837–852).
    /// O fundo escuro também fecha, como o clique fora do cartão no protótipo.
    /// </summary>
    public sealed class RulesModal
    {
        private static readonly string[] Regras =
        {
            "Na sua vez, escolha <b>uma única fileira</b> e tape <b>quantos buracos quiser</b> dela (no mínimo um).",
            "Na variante <b>Livre</b>, valem quaisquer buracos daquela fileira.",
            "Na variante <b>Vizinhos</b>, só valem buracos <b>grudados em sequência</b> — e tapar no meio parte a fileira em duas.",
            "Toque nos buracos para marcar e confirme na <b>pazinha TAPAR</b>.",
            "Tocar num buraco de outra fileira limpa a marcação anterior.",
            "<b>Quem tapar o último buraco do tabuleiro perde</b> — e leva um caldo.",
        };

        private readonly RectTransform _root;
        private readonly Image _fundo;
        private readonly PanelView _cartao;
        private readonly Text _titulo;
        private readonly Text _nota;
        private readonly List<Text> _corpo = new List<Text>(8);
        private readonly ButtonView _entendi;

        /// <summary>Monta o modal (nasce escondido).</summary>
        public RulesModal(Transform parent)
        {
            _root = UiKit.Stretch(parent, "modal-regras");

            // rgba(20,50,55,.62) do CSS — o véu também é o botão de fechar.
            _fundo = UiKit.Picture(_root, "fundo", SpriteFactory.Solid(), new Color(20f / 255f, 50f / 255f, 55f / 255f, 0.62f), true);
            UiKit.FillParent(_fundo.rectTransform);
            var fechaFora = _fundo.gameObject.AddComponent<Button>();
            fechaFora.transition = Selectable.Transition.None;
            fechaFora.targetGraphic = _fundo;
            fechaFora.onClick.AddListener(() => Closed?.Invoke());

            _cartao = UiKit.Panel(_root, "modal-card");
            _cartao.Root.sizeDelta = new Vector2(UiKit.ReferenceWidth - UiKit.Css(36f), UiKit.Css(560f));

            RectTransform corpo = UiKit.Stretch(_cartao.Content, "corpo");
            VerticalLayoutGroup coluna = UiKit.Column(corpo, 10f);
            coluna.childForceExpandWidth = true;
            coluna.padding = new RectOffset(
                ScreenPanels.Px(16f),
                ScreenPanels.Px(16f),
                ScreenPanels.Px(16f),
                ScreenPanels.Px(16f));

            _titulo = UiKit.Label(corpo, "titulo", "Como se joga", UiKit.Sign, UiKit.FontSize(20f, 6f, 30f), Palette.AccentDark, TextAnchor.UpperLeft);
            _titulo.horizontalOverflow = HorizontalWrapMode.Overflow;

            // Área rolável: o CSS limitava o cartão a 86vh com overflow:auto.
            RectTransform area = UiKit.Node(corpo, "rolagem");
            UiKit.Flexible(area);
            Image captura = UiKit.Picture(area, "captura", SpriteFactory.Solid(), Color.clear, true);
            UiKit.FillParent(captura.rectTransform);

            RectTransform viewport = UiKit.Stretch(area, "viewport");
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform lista = UiKit.Node(viewport, "lista");
            lista.anchorMin = new Vector2(0f, 1f);
            lista.anchorMax = new Vector2(1f, 1f);
            lista.pivot = new Vector2(0.5f, 1f);
            lista.offsetMin = Vector2.zero;
            lista.offsetMax = Vector2.zero;
            VerticalLayoutGroup pilha = UiKit.Column(lista, 8f, TextAnchor.UpperLeft);
            pilha.childForceExpandWidth = true;
            var fitter = lista.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = area.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = UiKit.Css(18f);
            scroll.viewport = viewport;
            scroll.content = lista;

            int tamanhoCorpo = UiKit.FontSize(12f, 3.4f, 15f);
            Paragrafo(lista, "p1", "Sete fileiras cavadas na areia: 1, 2, 3, 4, 5, 6 e 7 buracos. <b>28 buracos</b> no total.", UiKit.Body, tamanhoCorpo);

            for (int i = 0; i < Regras.Length; i++)
            {
                // O <li> do CSS; o Text legado não tem marcador, então o ponto vem no texto.
                Paragrafo(lista, "li" + (i + 1), "•  " + Regras[i], UiKit.Body, tamanhoCorpo);
            }

            // A nota tem cor própria (opacity .8 no CSS), por isso fica fora da lista do corpo.
            _nota = Paragrafo(
                lista,
                "nota",
                "O \"Rato de Praia\" joga a estratégia perfeita: nim misère. Ele só erra se você o obrigar.",
                UiKit.BodyItalic,
                tamanhoCorpo,
                false);

            RectTransform linhaBtn = UiKit.Node(corpo, "linha-btn");
            UiKit.Row(linhaBtn, 10f);
            UiKit.Size(linhaBtn, 0f, 52f);

            _entendi = UiKit.Button(linhaBtn, "fechar-regras", "Entendi", ButtonStyle.Coral);
            UiKit.Size(_entendi.Root, 140f, 48f);
            _entendi.Button.onClick.AddListener(() => Closed?.Invoke());

            ApplySkin();
            _root.gameObject.SetActive(false);
        }

        /// <summary>Raiz da subárvore.</summary>
        public RectTransform Root => _root;

        /// <summary>Fechado pelo botão "Entendi" ou por um toque fora do cartão.</summary>
        public event System.Action Closed;

        /// <summary>Abre/fecha o modal.</summary>
        public void SetVisible(bool visible)
        {
            if (_root.gameObject.activeSelf != visible)
            {
                _root.gameObject.SetActive(visible);
            }
        }

        /// <summary>Repinta cartão, textos e botão na skin corrente.</summary>
        public void ApplySkin()
        {
            UiKit.RepaintPanel(_cartao);
            _titulo.color = Palette.AccentDark;

            for (int i = 0; i < _corpo.Count; i++)
            {
                _corpo[i].color = Palette.Text;
            }

            _nota.color = Palette.Text.WithAlpha(0.8f);
            UiKit.RepaintButton(_entendi);
        }

        private Text Paragrafo(RectTransform parent, string nome, string conteudo, Font fonte, int tamanho, bool registra = true)
        {
            Text texto = UiKit.Label(parent, nome, conteudo, fonte, tamanho, Palette.Text, TextAnchor.UpperLeft);
            texto.lineSpacing = 1.1f;

            // A altura sai do próprio Text (ILayoutElement) — sem ContentSizeFitter, que brigaria
            // com a coluna que já controla o tamanho do filho.
            if (registra)
            {
                _corpo.Add(texto);
            }

            return texto;
        }
    }
}
