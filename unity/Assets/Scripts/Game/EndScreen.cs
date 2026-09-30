using UnityEngine;
using UnityEngine.UI;

namespace TapaBuraco.Game
{
    /// <summary>
    /// Tela de fim de partida — equivale a <c>#tela-fim</c> (index.html 822–833) e à função
    /// <c>telaFim()</c> (index.html 1597–1641). O palco encena a comemoração ou o caldo.
    ///
    /// A animação não usa corrotina nem MonoBehaviour: o integrador chama <see cref="Tick"/>
    /// enquanto a tela está visível, e todo o estado cabe em vetores pré-alocados.
    /// </summary>
    public sealed class EndScreen
    {
        private const int Confetes = 16;
        private const float CicloCaldo = 2.4f;
        private const float CicloFesta = 1.1f;

        private readonly RectTransform _root;
        private readonly PanelView _cartao;
        private readonly Text _titulo;
        private readonly Outline _tituloContorno;
        private readonly Text _subtitulo;

        private readonly RectTransform _palco;
        private readonly Image _ceu;
        private readonly Image _mar;
        private readonly Image _areia;
        private readonly Image _palcoBorda;

        private readonly Image _guardaSol;
        private readonly Image _figura;
        private readonly Image _chapeu;
        private readonly Image _onda;

        private readonly RectTransform _confeteRaiz;
        private readonly RectTransform[] _confete = new RectTransform[Confetes];
        private readonly float[] _confeteDuracao = new float[Confetes];
        private readonly float[] _confeteAtraso = new float[Confetes];

        private readonly Text _nome1;
        private readonly Text _nome2;
        private readonly Text _ponto1;
        private readonly Text _ponto2;
        private readonly Text _vezes;

        private readonly ButtonView _menu;
        private readonly ButtonView _denovo;

        private bool _vitoria;
        private bool _reduzido;
        private float _tempo;

        /// <summary>Monta a tela (nasce escondida). O conteúdo variável entra em <see cref="Show"/>.</summary>
        public EndScreen(Transform parent)
        {
            _root = UiKit.Stretch(parent, "tela-fim", UiKit.Css(10f));

            _cartao = UiKit.Panel(_root, "fim-card");
            _cartao.Root.sizeDelta = new Vector2(UiKit.ReferenceWidth - UiKit.Css(20f), 0f);
            RectTransform corpo = ScreenPanels.AutoHeight(_cartao, 14f, 14f, 14f, 8f);

            _titulo = UiKit.Label(corpo, "fim-titulo", "FIM", UiKit.Sign, UiKit.FontSize(26f, 9f, 54f), Palette.Coral);
            _titulo.horizontalOverflow = HorizontalWrapMode.Overflow;
            _tituloContorno = UiKit.InkOutline(_titulo, UiKit.Css(3f), Palette.Ink);

            _subtitulo = UiKit.Label(corpo, "fim-sub", string.Empty, UiKit.BodyItalic, UiKit.FontSize(12f, 3.6f, 17f), Palette.Text);

            // ------------------------------------------------------------- palco
            _palco = UiKit.Node(corpo, "palco");
            UiKit.Size(_palco, 0f, 242f);
            _palco.gameObject.AddComponent<RectMask2D>();

            // O gradiente do CSS é em três faixas: céu 0–52%, mar 52–64%, areia 64–100%.
            _ceu = Faixa(_palco, "ceu", Palette.Ceu, 0.48f, 1f);
            _mar = Faixa(_palco, "mar", Palette.Mar, 0.36f, 0.48f);
            _areia = Faixa(_palco, "areia", Palette.Areia, 0f, 0.36f);

            _guardaSol = UiKit.Picture(_palco, "guarda-sol", null, Color.white);
            _guardaSol.preserveAspect = true;
            Ancora(_guardaSol.rectTransform, new Vector2(0.27f, 0.45f), new Vector2(0.73f, 0.98f), new Vector2(0.5f, 1f));

            _onda = UiKit.Picture(_palco, "onda-caldo", UiKit.Art("onda-caldo"), Color.white);
            Ancora(_onda.rectTransform, new Vector2(0f, 0f), new Vector2(1.2f, 0.62f), new Vector2(0f, 0f));

            _figura = UiKit.Picture(_palco, "figura", null, Color.white);
            _figura.preserveAspect = true;
            Ancora(_figura.rectTransform, new Vector2(0.5f, 0.04f), new Vector2(0.5f, 0.86f), new Vector2(0.5f, 0f));
            _figura.rectTransform.sizeDelta = new Vector2(UiKit.Css(190f), 0f);

            _chapeu = UiKit.Picture(_palco, "chapeu-voa", UiKit.Art("chapeu-palha"), Color.white);
            _chapeu.preserveAspect = true;
            Ancora(_chapeu.rectTransform, new Vector2(0.43f, 0.52f), new Vector2(0.57f, 0.66f), new Vector2(0.5f, 0f));

            _confeteRaiz = UiKit.Stretch(_palco, "confetes");
            var sorteio = new System.Random(77);
            for (int i = 0; i < Confetes; i++)
            {
                // Mesmas quatro cores do protótipo, fixas: o confete não segue a skin.
                Color cor;
                switch (i % 4)
                {
                    case 0: cor = Palette.Coral; break;
                    case 1: cor = Palette.Mar; break;
                    case 2: cor = Palette.Creme; break;
                    default: cor = Palette.Amarelo; break;
                }

                Image papelote = UiKit.Picture(_confeteRaiz, "confete", SpriteFactory.Solid(), cor.WithAlpha(0.95f));
                RectTransform rt = papelote.rectTransform;
                float x = 0.05f + ((float)sorteio.NextDouble() * 0.9f);
                rt.anchorMin = new Vector2(x, 1f);
                rt.anchorMax = new Vector2(x, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(UiKit.Css(8f), UiKit.Css(12f));
                rt.anchoredPosition = Vector2.zero;

                _confete[i] = rt;
                _confeteDuracao[i] = 1.6f + ((float)sorteio.NextDouble() * 1.8f);
                _confeteAtraso[i] = (float)sorteio.NextDouble() * 2f;
            }

            _palcoBorda = UiKit.Picture(_palco, "borda", SpriteFactory.RoundedOutline(72, 22, 7), Palette.Ink);
            UiKit.FillParent(_palcoBorda.rectTransform);

            // ------------------------------------------------------------- placar
            RectTransform placar = UiKit.Node(corpo, "placar-fim");
            UiKit.Row(placar, 10f);
            UiKit.Size(placar, 0f, 52f);

            int tamanhoNome = UiKit.FontSize(16f, 5f, 26f);
            int tamanhoPonto = Mathf.RoundToInt(tamanhoNome * 1.5f);

            _nome1 = Marcador(placar, "n1", string.Empty, tamanhoNome, Palette.Text);
            _ponto1 = Marcador(placar, "p1", "0", tamanhoPonto, Palette.Text);
            _vezes = Marcador(placar, "x", "×", tamanhoNome, Palette.AccentDark);
            _ponto2 = Marcador(placar, "p2", "0", tamanhoPonto, Palette.Text);
            _nome2 = Marcador(placar, "n2", string.Empty, tamanhoNome, Palette.Text);

            // ------------------------------------------------------------- botões
            RectTransform linhaBtn = UiKit.Node(corpo, "linha-btn");
            UiKit.Row(linhaBtn, 10f);
            UiKit.Size(linhaBtn, 0f, 56f);

            _menu = UiKit.Button(linhaBtn, "fim-menu", "Menu", ButtonStyle.Small);
            UiKit.Size(_menu.Root, 100f, 40f);
            _menu.Button.onClick.AddListener(() => MenuClicked?.Invoke());

            _denovo = UiKit.Button(linhaBtn, "fim-denovo", "Jogar de novo", ButtonStyle.Coral);
            UiKit.Size(_denovo.Root, 200f, 52f);
            _denovo.Button.onClick.AddListener(() => AgainClicked?.Invoke());

            ApplySkin();
            _root.gameObject.SetActive(false);
        }

        /// <summary>Raiz da subárvore.</summary>
        public RectTransform Root => _root;

        /// <summary>"Jogar de novo".</summary>
        public event System.Action AgainClicked;

        /// <summary>"Menu".</summary>
        public event System.Action MenuClicked;

        /// <summary>
        /// Prepara e mostra a tela. <paramref name="humanWon"/> escolhe a encenação:
        /// comemoração com confete ou o caldo com chapéu voando.
        /// </summary>
        public void Show(bool humanWon, string title, string subtitle, int winner, string name1, string name2, int score1, int score2, bool reducedMotion)
        {
            _vitoria = humanWon;
            _reduzido = reducedMotion;
            _tempo = 0f;

            _titulo.text = title;
            _subtitulo.text = subtitle;
            _nome1.text = name1;
            _nome2.text = name2;
            _ponto1.text = score1.ToString();
            _ponto2.text = score2.ToString();

            // O protagonista da cena é quem ganhou (comemora) ou quem perdeu (leva o caldo);
            // a cor sai do lado dele no HUD — coral para o jogador 1, mar para o 2.
            int protagonista = humanWon ? winner : 1 - winner;
            bool coral = protagonista == 0;

            if (humanWon)
            {
                _figura.sprite = UiKit.Art(coral ? "banhista-vitoria-coral" : "banhista-vitoria-mar");
                _guardaSol.sprite = UiKit.Art(coral ? "guardasol-coral" : "guardasol-mar");
            }
            else
            {
                _figura.sprite = UiKit.Art(coral ? "banhista-derrota-coral" : "banhista-derrota-mar");
            }

            _guardaSol.enabled = humanWon;
            _confeteRaiz.gameObject.SetActive(humanWon);
            _chapeu.enabled = !humanWon;
            _onda.enabled = !humanWon;

            SetVisible(true);
            Quadro();
        }

        /// <summary>Liga/desliga a tela inteira.</summary>
        public void SetVisible(bool visible)
        {
            if (_root.gameObject.activeSelf != visible)
            {
                _root.gameObject.SetActive(visible);
            }
        }

        /// <summary>Avança a encenação. Com movimento reduzido fica no quadro parado.</summary>
        public void Tick(float deltaTime)
        {
            if (_reduzido)
            {
                return;
            }

            _tempo += deltaTime;
            Quadro();
        }

        /// <summary>Repinta cartão, palco, placar e botões na skin corrente.</summary>
        public void ApplySkin()
        {
            bool papel = Palette.IsPaper;

            UiKit.RepaintPanel(_cartao);

            _titulo.color = papel ? Palette.CanetaEscura : Palette.Coral;
            _tituloContorno.effectColor = Palette.Ink.WithAlpha(papel ? 0f : 1f);
            _subtitulo.color = Palette.Text;

            // No Papel de Pão o palco é só uma janela clara (body.skin-papel .palco).
            _ceu.color = papel ? Palette.Sheet.WithAlpha(0.3f) : Palette.Ceu;
            _mar.color = papel ? Palette.Sheet.WithAlpha(0.42f) : Palette.Mar;
            _areia.color = papel ? Palette.Sheet.WithAlpha(0.3f) : Palette.Areia;
            _palcoBorda.color = Palette.Ink;

            _nome1.color = Palette.Text;
            _nome2.color = Palette.Text;
            _ponto1.color = Palette.Text;
            _ponto2.color = Palette.Text;
            _vezes.color = Palette.AccentDark;

            UiKit.RepaintButton(_menu);
            UiKit.RepaintButton(_denovo);
        }

        // ------------------------------------------------------------------ encenação

        private void Quadro()
        {
            if (_vitoria)
            {
                QuadroVitoria();
            }
            else
            {
                QuadroDerrota();
            }
        }

        private void QuadroVitoria()
        {
            RectTransform figura = _figura.rectTransform;

            // @keyframes festa: sobe 8% e gira de -2° a 2° em 1,1 s.
            float k = _reduzido ? 0.5f : 0.5f - (0.5f * Mathf.Cos(_tempo * (Mathf.PI * 2f / CicloFesta)));
            figura.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-2f, 2f, k));
            figura.anchoredPosition = new Vector2(0f, figura.rect.height * 0.08f * k);

            float altura = _palco.rect.height;
            for (int i = 0; i < Confetes; i++)
            {
                // Com movimento reduzido o confete fica parado, espalhado pelo palco.
                float fase = _reduzido
                    ? (i + 1) / (float)(Confetes + 1)
                    : Mathf.Repeat((_tempo + _confeteAtraso[i]) / _confeteDuracao[i], 1f);

                RectTransform papelote = _confete[i];
                papelote.anchoredPosition = new Vector2(0f, Mathf.Lerp(altura * 0.05f, -altura * 1.4f, fase));
                papelote.localRotation = Quaternion.Euler(0f, 0f, -720f * fase);
            }
        }

        private void QuadroDerrota()
        {
            float u = _reduzido ? 0f : Mathf.Repeat(_tempo / CicloCaldo, 1f);

            // @keyframes caldo: fica de pé, é derrubado pela onda e volta.
            float dx;
            float dy;
            float giro;
            if (u < 0.22f)
            {
                dx = 0f;
                dy = 0f;
                giro = 0f;
            }
            else if (u < 0.34f)
            {
                float s = Mathf.InverseLerp(0.22f, 0.34f, u);
                dx = Mathf.Lerp(0f, 0.10f, s);
                dy = 0f;
                giro = Mathf.Lerp(0f, -28f, s);
            }
            else if (u < 0.48f)
            {
                float s = Mathf.InverseLerp(0.34f, 0.48f, u);
                dx = Mathf.Lerp(0.10f, 0.18f, s);
                dy = Mathf.Lerp(0f, -0.12f, s);
                giro = Mathf.Lerp(-28f, -82f, s);
            }
            else if (u < 0.76f)
            {
                dx = 0.18f;
                dy = -0.12f;
                giro = -82f;
            }
            else if (u < 0.92f)
            {
                float s = Mathf.InverseLerp(0.76f, 0.92f, u);
                dx = Mathf.Lerp(0.18f, 0f, s);
                dy = Mathf.Lerp(-0.12f, 0f, s);
                giro = Mathf.Lerp(-82f, 0f, s);
            }
            else
            {
                dx = 0f;
                dy = 0f;
                giro = 0f;
            }

            RectTransform figura = _figura.rectTransform;
            Rect caixaFigura = figura.rect;
            figura.anchoredPosition = new Vector2(caixaFigura.width * dx, caixaFigura.height * dy);
            figura.localRotation = Quaternion.Euler(0f, 0f, giro);

            // @keyframes passaOnda: entra pela esquerda e some pela direita.
            float larguraPalco = _palco.rect.width;
            float avanco = Mathf.InverseLerp(0.10f, 0.55f, u);
            _onda.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-1.2f, 1.05f, avanco) * larguraPalco, 0f);

            // @keyframes voaChapeu: aparece no tranco e roda pra fora do quadro.
            RectTransform chapeu = _chapeu.rectTransform;
            Rect caixaChapeu = chapeu.rect;
            float alfa;
            float cx;
            float cy;
            float rodada;
            if (u < 0.26f)
            {
                alfa = 0f;
                cx = 0f;
                cy = 0f;
                rodada = 0f;
            }
            else if (u < 0.30f)
            {
                alfa = Mathf.InverseLerp(0.26f, 0.30f, u);
                cx = 0f;
                cy = 0f;
                rodada = 0f;
            }
            else if (u < 0.55f)
            {
                float s = Mathf.InverseLerp(0.30f, 0.55f, u);
                alfa = 1f;
                cx = Mathf.Lerp(0f, 0.6f, s);
                cy = Mathf.Lerp(0f, 1.2f, s);
                rodada = Mathf.Lerp(0f, -200f, s);
            }
            else if (u < 0.70f)
            {
                float s = Mathf.InverseLerp(0.55f, 0.70f, u);
                alfa = 1f - s;
                cx = Mathf.Lerp(0.6f, 1.5f, s);
                cy = Mathf.Lerp(1.2f, 0.4f, s);
                rodada = Mathf.Lerp(-200f, -420f, s);
            }
            else
            {
                alfa = 0f;
                cx = 1.5f;
                cy = 0.4f;
                rodada = -420f;
            }

            chapeu.anchoredPosition = new Vector2(caixaChapeu.width * cx, caixaChapeu.height * cy);
            chapeu.localRotation = Quaternion.Euler(0f, 0f, rodada);
            _chapeu.color = Color.white.WithAlpha(alfa);
        }

        // ------------------------------------------------------------------ montagem

        private static Image Faixa(RectTransform palco, string nome, Color cor, float baixo, float cima)
        {
            Image faixa = UiKit.Picture(palco, nome, SpriteFactory.Solid(), cor);
            Ancora(faixa.rectTransform, new Vector2(0f, baixo), new Vector2(1f, cima), new Vector2(0.5f, 0.5f));
            return faixa;
        }

        private static void Ancora(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.pivot = pivot;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }

        private static Text Marcador(RectTransform linha, string nome, string conteudo, int tamanho, Color cor)
        {
            Text texto = UiKit.Label(linha, nome, conteudo, UiKit.Sign, tamanho, cor);
            texto.horizontalOverflow = HorizontalWrapMode.Overflow;
            return texto;
        }
    }
}
