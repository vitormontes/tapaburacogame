using UnityEngine;
using UnityEngine.UI;

namespace TapaBuraco.Game
{
    /// <summary>
    /// Barra de cima da tela de jogo: logotipo miúdo, as duas pastilhas de jogador com
    /// guarda-sol e os quatro botões de ícone. Equivale ao <c>.hud</c> do protótipo
    /// (index.html 780–792, CSS 219–247).
    /// </summary>
    public sealed class HudBar
    {
        private const float PillRadiusCss = 16f;
        private const float BobPeriodo = 1.6f;

        private readonly RectTransform _root;

        private readonly Text _logoTapa;
        private readonly Text _logoBuraco;
        private readonly Outline _logoTapaContorno;
        private readonly Outline _logoBuracoContorno;

        private readonly CanvasGroup[] _pastilhas = new CanvasGroup[2];
        private readonly Image[] _pastilhaBorda = new Image[2];
        private readonly Image[] _pastilhaMiolo = new Image[2];
        private readonly Image[] _pastilhaBrilho = new Image[2];
        private readonly RectTransform[] _guardaSol = new RectTransform[2];
        private readonly Text[] _nomes = new Text[2];
        private readonly Text[] _pontos = new Text[2];

        private readonly ButtonView _som;
        private readonly ButtonView _skin;
        private readonly ButtonView _regras;
        private readonly ButtonView _menu;

        private int _ativo = -1;
        private bool _somLigado = true;
        private int _placarA = -1;
        private int _placarB = -1;

        /// <summary>Monta a barra ancorada no topo do pai.</summary>
        public HudBar(Transform parent)
        {
            _root = UiKit.Node(parent, "hud");
            _root.anchorMin = new Vector2(0f, 1f);
            _root.anchorMax = new Vector2(1f, 1f);
            _root.pivot = new Vector2(0.5f, 1f);
            _root.sizeDelta = new Vector2(0f, UiKit.Css(40f));
            _root.anchoredPosition = Vector2.zero;

            HorizontalLayoutGroup barra = UiKit.Row(_root, 6f);
            barra.childForceExpandHeight = true;
            barra.padding = new RectOffset(
                ScreenPanels.Px(2f),
                ScreenPanels.Px(2f),
                ScreenPanels.Px(2f),
                ScreenPanels.Px(2f));

            // ------------------------------------------------------------- logotipo miúdo
            RectTransform logo = UiKit.Node(_root, "logo-mini");
            VerticalLayoutGroup pilha = UiKit.Column(logo, -3f, TextAnchor.MiddleLeft);
            pilha.childForceExpandWidth = true;
            UiKit.Size(logo, 52f, 0f);

            int tamanhoLogo = UiKit.FontSize(13f, 3.6f, 18f);
            _logoTapa = UiKit.Label(logo, "l1", "TAPA", UiKit.Sign, tamanhoLogo, Palette.Creme, TextAnchor.MiddleLeft);
            _logoTapa.horizontalOverflow = HorizontalWrapMode.Overflow;
            _logoTapaContorno = UiKit.InkOutline(_logoTapa, UiKit.Css(2f), Palette.Ink);

            _logoBuraco = UiKit.Label(logo, "l2", "BURACO", UiKit.Sign, tamanhoLogo, Palette.Coral, TextAnchor.MiddleLeft);
            _logoBuraco.horizontalOverflow = HorizontalWrapMode.Overflow;
            _logoBuracoContorno = UiKit.InkOutline(_logoBuraco, UiKit.Css(2f), Palette.Ink);

            // ------------------------------------------------------------- pastilhas
            RectTransform vezes = UiKit.Node(_root, "vezes");
            HorizontalLayoutGroup fila = UiKit.Row(vezes, 6f);
            fila.childForceExpandWidth = true;
            UiKit.Flexible(vezes);

            Pastilha(vezes, 0, "jog1", "guardasol-coral");
            Pastilha(vezes, 1, "jog2", "guardasol-mar");

            // ------------------------------------------------------------- botões de ícone
            RectTransform botoes = UiKit.Node(_root, "hud-btns");
            UiKit.Row(botoes, 4f);

            _som = Icone(botoes, "b-som", "♪");
            _skin = Icone(botoes, "b-skin", "▲");
            _regras = Icone(botoes, "b-regras2", "?");
            _menu = Icone(botoes, "b-menu", "≡");

            _som.Button.onClick.AddListener(() => SoundClicked?.Invoke());
            _skin.Button.onClick.AddListener(() => SkinClicked?.Invoke());
            _regras.Button.onClick.AddListener(() => RulesClicked?.Invoke());
            _menu.Button.onClick.AddListener(() => MenuClicked?.Invoke());

            SetScore(0, 0);
            SetActivePlayer(0, false);
            ApplySkin();
        }

        /// <summary>Raiz da subárvore.</summary>
        public RectTransform Root => _root;

        /// <summary>Ícone do alto-falante.</summary>
        public event System.Action SoundClicked;

        /// <summary>Ícone de troca de estilo.</summary>
        public event System.Action SkinClicked;

        /// <summary>Ícone de regras.</summary>
        public event System.Action RulesClicked;

        /// <summary>Ícone de menu.</summary>
        public event System.Action MenuClicked;

        /// <summary>Nomes das pastilhas ("VOCÊ"/"CPU" ou "JOGADOR 1"/"JOGADOR 2").</summary>
        public void SetNames(string player1, string player2)
        {
            _nomes[0].text = player1;
            _nomes[1].text = player2;
        }

        /// <summary>Placar acumulado. Só mexe no texto quando o número muda de verdade.</summary>
        public void SetScore(int a, int b)
        {
            if (_placarA != a)
            {
                _placarA = a;
                _pontos[0].text = a.ToString();
            }

            if (_placarB != b)
            {
                _placarB = b;
                _pontos[1].text = b.ToString();
            }
        }

        /// <summary>
        /// Acende a pastilha de quem joga. Com <paramref name="gameOver"/> as duas apagam,
        /// como no <c>classList.toggle("ativo", J.vez===i &amp;&amp; !J.acabou)</c> do protótipo.
        /// </summary>
        public void SetActivePlayer(int player, bool gameOver)
        {
            int ativo = gameOver ? -1 : player;
            if (_ativo == ativo)
            {
                return;
            }

            _ativo = ativo;
            for (int i = 0; i < 2; i++)
            {
                bool ligado = i == ativo;
                _pastilhas[i].alpha = ligado ? 1f : 0.5f;
                _pastilhaBrilho[i].enabled = ligado;

                if (!ligado)
                {
                    // Sai da animação no lugar certo para não congelar torto.
                    _guardaSol[i].localRotation = Quaternion.identity;
                    _guardaSol[i].anchoredPosition = Vector2.zero;
                }
            }
        }

        /// <summary>Estado do ícone de som (o <c>.icobtn.off</c> do CSS).</summary>
        public void SetSoundOn(bool on)
        {
            _somLigado = on;
            _som.Text = on ? "♪" : "♪̸";
            PintaIconeSom();
        }

        /// <summary>
        /// Balanço do guarda-sol de quem está jogando (o <c>@keyframes bob</c>).
        /// Fica fora de um MonoBehaviour de propósito: quem chama por quadro é o integrador,
        /// que já sabe se o movimento reduzido está ligado.
        /// </summary>
        public void Tick(float time)
        {
            if (_ativo < 0)
            {
                return;
            }

            RectTransform sol = _guardaSol[_ativo];
            float k = 0.5f - (0.5f * Mathf.Cos(time * (Mathf.PI * 2f / BobPeriodo)));
            sol.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-3f, 3f, k));
            sol.anchoredPosition = new Vector2(0f, UiKit.Css(3f) * k);
        }

        /// <summary>Liga/desliga a barra inteira.</summary>
        public void SetVisible(bool visible)
        {
            if (_root.gameObject.activeSelf != visible)
            {
                _root.gameObject.SetActive(visible);
            }
        }

        /// <summary>Repinta logotipo, pastilhas e botões na skin corrente.</summary>
        public void ApplySkin()
        {
            bool papel = Palette.IsPaper;

            _logoTapa.color = papel ? Palette.Caneta : Palette.Creme;
            _logoBuraco.color = papel ? Palette.CanetaEscura : Palette.Coral;
            _logoTapaContorno.effectColor = Palette.Ink.WithAlpha(papel ? 0f : 1f);
            _logoBuracoContorno.effectColor = Palette.Ink.WithAlpha(papel ? 0f : 1f);

            for (int i = 0; i < 2; i++)
            {
                _pastilhaBorda[i].color = Palette.Ink;
                _pastilhaMiolo[i].color = Palette.Sheet.WithAlpha(papel ? 0.4f : 0.86f);
                _pastilhaBrilho[i].color = Palette.Accent.WithAlpha(0.35f);
                _nomes[i].color = Palette.Text;
                _pontos[i].color = Palette.AccentDark;
            }

            UiKit.RepaintButton(_som);
            UiKit.RepaintButton(_skin);
            UiKit.RepaintButton(_regras);
            UiKit.RepaintButton(_menu);
            PintaIconeSom();
        }

        // ------------------------------------------------------------------ montagem

        private void Pastilha(RectTransform parent, int indice, string nome, string arte)
        {
            RectTransform pastilha = UiKit.Node(parent, nome);
            UiKit.Size(pastilha, 80f, 34f);
            UiKit.Flexible(pastilha);

            int raio = Mathf.RoundToInt(UiKit.Css(PillRadiusCss));
            Sprite caixa = SpriteFactory.RoundedRect(raio * 3, raio);

            // O realce do turno (box-shadow 0 0 0 3px coral) vira uma moldura extra atrás.
            Image brilho = UiKit.Picture(pastilha, "brilho", caixa, Palette.Accent.WithAlpha(0.35f));
            UiKit.FillParent(brilho.rectTransform, -UiKit.Css(3f));
            ScreenPanels.Ignore(brilho);
            _pastilhaBrilho[indice] = brilho;

            Image borda = UiKit.Picture(pastilha, "borda", caixa, Palette.Ink);
            UiKit.FillParent(borda.rectTransform);
            ScreenPanels.Ignore(borda);
            _pastilhaBorda[indice] = borda;

            Image miolo = UiKit.Picture(pastilha, "miolo", caixa, Palette.Sheet.WithAlpha(0.86f));
            UiKit.FillParent(miolo.rectTransform, UiKit.Css(2.5f));
            ScreenPanels.Ignore(miolo);
            _pastilhaMiolo[indice] = miolo;

            HorizontalLayoutGroup linha = UiKit.Row(pastilha, 4f);
            linha.padding = new RectOffset(
                ScreenPanels.Px(4f),
                ScreenPanels.Px(7f),
                ScreenPanels.Px(3f),
                ScreenPanels.Px(3f));

            // O guarda-sol balança dentro de um berço fixo: assim o layout nunca desfaz o bob.
            RectTransform berco = UiKit.Node(pastilha, "sol");
            UiKit.Size(berco, 20f, 24f);
            Image sol = UiKit.Picture(berco, "arte", UiKit.Art(arte), Color.white);
            sol.preserveAspect = true;
            UiKit.FillParent(sol.rectTransform);
            _guardaSol[indice] = sol.rectTransform;

            Text nomeTexto = UiKit.Label(pastilha, "nm", string.Empty, UiKit.Sign, UiKit.FontSize(10f, 2.9f, 14f), Palette.Text);
            nomeTexto.verticalOverflow = VerticalWrapMode.Truncate;
            UiKit.Flexible(nomeTexto);
            _nomes[indice] = nomeTexto;

            Text pontosTexto = UiKit.Label(pastilha, "pt", "0", UiKit.Sign, UiKit.FontSize(13f, 3.6f, 18f), Palette.AccentDark);
            pontosTexto.horizontalOverflow = HorizontalWrapMode.Overflow;
            _pontos[indice] = pontosTexto;

            _pastilhas[indice] = pastilha.gameObject.AddComponent<CanvasGroup>();
        }

        private static ButtonView Icone(RectTransform parent, string nome, string glifo)
        {
            ButtonView botao = UiKit.Button(parent, nome, glifo, ButtonStyle.Icon);
            UiKit.Size(botao.Root, 32f, 32f);
            return botao;
        }

        private void PintaIconeSom()
        {
            if (_som.Label == null)
            {
                return;
            }

            _som.Label.color = Palette.Text.WithAlpha(_somLigado ? 1f : 0.45f);
            _som.Fill.color = Palette.Sheet.WithAlpha(_somLigado ? 1f : 0.45f);
        }
    }
}
