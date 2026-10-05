using System.Collections.Generic;
using TapaBuraco.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TapaBuraco.Game
{
    /// <summary>
    /// Modal "Ajustes" — equivale a <c>#modal-ajustes</c> do protótipo: estilo visual e som.
    /// Escreve direto no <see cref="GameSettings"/> recebido e avisa por <see cref="Changed"/>;
    /// quem salva em disco e aplica o mix é o integrador. O véu escuro também fecha.
    /// </summary>
    public sealed class SettingsModal
    {
        private readonly RectTransform _root;
        private readonly GameSettings _settings;

        private readonly PanelView _cartao;
        private readonly Text _titulo;
        private readonly Text _nota;
        private readonly List<Text> _rotulos = new List<Text>(2);
        private readonly List<Image> _reguas = new List<Image>(2);
        private readonly List<OptionView> _opcoes = new List<OptionView>(5);

        private readonly OptionView _skinPraia;
        private readonly OptionView _skinPapel;
        private readonly OptionView _somEfeitos;
        private readonly OptionView _somAmbiente;
        private readonly OptionView _somMusica;
        private readonly ButtonView _pronto;

        /// <summary>Monta o modal (nasce escondido) já refletindo <paramref name="settings"/>.</summary>
        public SettingsModal(Transform parent, GameSettings settings)
        {
            _settings = settings;
            _root = UiKit.Stretch(parent, "modal-ajustes");

            Image fundo = UiKit.Picture(_root, "fundo", SpriteFactory.Solid(), new Color(20f / 255f, 50f / 255f, 55f / 255f, 0.62f), true);
            UiKit.FillParent(fundo.rectTransform);
            var fechaFora = fundo.gameObject.AddComponent<Button>();
            fechaFora.transition = Selectable.Transition.None;
            fechaFora.targetGraphic = fundo;
            fechaFora.onClick.AddListener(() => Closed?.Invoke());

            _cartao = UiKit.Panel(_root, "modal-card");
            _cartao.Root.sizeDelta = new Vector2(UiKit.ReferenceWidth - UiKit.Css(36f), UiKit.Css(400f));

            RectTransform corpo = UiKit.Stretch(_cartao.Content, "corpo");
            VerticalLayoutGroup coluna = UiKit.Column(corpo, 12f);
            coluna.childForceExpandWidth = true;
            coluna.padding = new RectOffset(
                ScreenPanels.Px(16f),
                ScreenPanels.Px(16f),
                ScreenPanels.Px(16f),
                ScreenPanels.Px(16f));

            _titulo = UiKit.Label(corpo, "titulo", "Ajustes", UiKit.Sign, UiKit.FontSize(20f, 6f, 30f), Palette.AccentDark, TextAnchor.UpperLeft);
            _titulo.horizontalOverflow = HorizontalWrapMode.Overflow;

            // ------------------------------------------------------------- Estilo
            RectTransform gSkin = Grupo(corpo, "g-skin", "ESTILO");
            RectTransform lSkin = Linha(gSkin);
            _skinPraia = Opcao(lSkin, "praia", "Areia de Copacabana", null, 46f);
            _skinPapel = Opcao(lSkin, "papel", "Papel de Pão", "caneta azul", 46f);
            _skinPraia.Button.onClick.AddListener(() => TrocaSkin(Skin.Praia));
            _skinPapel.Button.onClick.AddListener(() => TrocaSkin(Skin.Papel));

            // ------------------------------------------------------------- Som
            RectTransform gSom = Grupo(corpo, "g-som", "SOM");
            RectTransform lSom = Linha(gSom);
            _somEfeitos = Opcao(lSom, "sfx", "Efeitos", null, 38f);
            _somAmbiente = Opcao(lSom, "amb", "Ondas e gaivotas", null, 38f);
            _somMusica = Opcao(lSom, "mus", "Musiquinha", null, 38f);
            _somEfeitos.Button.onClick.AddListener(() => Alterna(ref _settings.sfx));
            _somAmbiente.Button.onClick.AddListener(() => Alterna(ref _settings.ambience));
            _somMusica.Button.onClick.AddListener(() => Alterna(ref _settings.music));

            _nota = UiKit.Label(gSom, "nota", "No iPhone, se não sair som, desligue a chavinha de silencioso.", UiKit.BodyItalic, UiKit.FontSize(11f, 3f, 14f), Palette.Text.WithAlpha(0.8f), TextAnchor.UpperLeft);

            RectTransform linhaBtn = UiKit.Node(corpo, "linha-btn");
            UiKit.Row(linhaBtn, 10f);
            UiKit.Size(linhaBtn, 0f, 52f);

            _pronto = UiKit.Button(linhaBtn, "fechar-ajustes", "Pronto", ButtonStyle.Coral);
            UiKit.Size(_pronto.Root, 140f, 48f);
            _pronto.Button.onClick.AddListener(() => Closed?.Invoke());

            Refresh();
            ApplySkin();
            _root.gameObject.SetActive(false);
        }

        /// <summary>Raiz da subárvore.</summary>
        public RectTransform Root => _root;

        /// <summary>Alguma preferência mudou — o integrador salva e aplica o mix de som.</summary>
        public event System.Action Changed;

        /// <summary>Fechado por "Pronto" ou por um toque fora do cartão.</summary>
        public event System.Action Closed;

        /// <summary>Abre/fecha o modal (abrir relê as preferências).</summary>
        public void SetVisible(bool visible)
        {
            if (visible)
            {
                Refresh();
            }

            if (_root.gameObject.activeSelf != visible)
            {
                _root.gameObject.SetActive(visible);
            }
        }

        /// <summary>Relê o <see cref="GameSettings"/> e repinta o estado de cada opção.</summary>
        public void Refresh()
        {
            _skinPraia.Pressed = _settings.skin == Skin.Praia;
            _skinPapel.Pressed = _settings.skin == Skin.Papel;
            _somEfeitos.Pressed = _settings.sfx;
            _somAmbiente.Pressed = _settings.ambience;
            _somMusica.Pressed = _settings.music;
        }

        /// <summary>Repinta cartão, rótulos, opções e botão na skin corrente.</summary>
        public void ApplySkin()
        {
            UiKit.RepaintPanel(_cartao);
            _titulo.color = Palette.AccentDark;
            _nota.color = Palette.Text.WithAlpha(0.8f);

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

            UiKit.RepaintButton(_pronto);
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

        private void TrocaSkin(Skin skin)
        {
            _settings.skin = skin;
            Palette.SetSkin(skin);
            Refresh();
            Changed?.Invoke();
        }

        private void Alterna(ref bool chave)
        {
            chave = !chave;
            Refresh();
            Changed?.Invoke();
        }
    }
}
