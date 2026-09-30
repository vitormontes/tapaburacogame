using UnityEngine;
using UnityEngine.UI;

namespace TapaBuraco.Game
{
    /// <summary>Papel de cor de um elemento — resolvido pela skin ativa.</summary>
    public enum Tint
    {
        /// <summary>Traço preto do cartaz (azul-caneta no Papel de Pão).</summary>
        Ink = 0,

        /// <summary>Miolo claro de painéis e botões creme.</summary>
        Sheet = 1,

        /// <summary>Cor de destaque (coral / caneta).</summary>
        Accent = 2,

        /// <summary>Destaque escuro (rótulos, placar).</summary>
        AccentDark = 3,

        /// <summary>Texto corrido.</summary>
        Text = 4,

        /// <summary>Areia clara — fundo do tabuleiro.</summary>
        Sand = 5,

        /// <summary>Mar — jogador 2.</summary>
        Sea = 6,

        /// <summary>Fundo geral da tela.</summary>
        Background = 7,
    }

    /// <summary>
    /// Repinta um <see cref="Graphic"/> quando a skin muda. Evita que cada tela precise
    /// guardar listas de imagens para trocar cor — o protótipo web fazia isso com uma
    /// classe no &lt;body&gt;; aqui cada elemento carrega o próprio papel.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkinTint : MonoBehaviour
    {
        [SerializeField] private Tint _role = Tint.Ink;
        [SerializeField] private float _alpha = 1f;

        private Graphic _graphic;

        /// <summary>Papel de cor aplicado.</summary>
        public Tint Role
        {
            get => _role;
            set
            {
                _role = value;
                Apply();
            }
        }

        /// <summary>Opacidade multiplicada sobre a cor da paleta.</summary>
        public float Alpha
        {
            get => _alpha;
            set
            {
                _alpha = value;
                Apply();
            }
        }

        /// <summary>Instala (ou atualiza) o papel de cor de um gráfico.</summary>
        public static SkinTint Attach(Graphic graphic, Tint role, float alpha = 1f)
        {
            SkinTint tint = graphic.GetComponent<SkinTint>();
            if (tint == null)
            {
                tint = graphic.gameObject.AddComponent<SkinTint>();
            }

            tint._graphic = graphic;
            tint._role = role;
            tint._alpha = alpha;
            tint.Apply();
            return tint;
        }

        /// <summary>Cor final de um papel na skin atual.</summary>
        public static Color Resolve(Tint role)
        {
            switch (role)
            {
                case Tint.Ink: return Palette.Ink;
                case Tint.Sheet: return Palette.Sheet;
                case Tint.Accent: return Palette.Accent;
                case Tint.AccentDark: return Palette.AccentDark;
                case Tint.Text: return Palette.Text;
                case Tint.Sand: return Palette.IsPaper ? Color.white.WithAlpha(0.22f) : Palette.Areia;
                case Tint.Sea: return Palette.IsPaper ? Palette.Caneta : Palette.Mar;
                case Tint.Background: return Palette.Background;
                default: return Color.white;
            }
        }

        private void OnEnable()
        {
            if (_graphic == null)
            {
                _graphic = GetComponent<Graphic>();
            }

            Palette.SkinChanged += Apply;
            Apply();
        }

        private void OnDisable()
        {
            Palette.SkinChanged -= Apply;
        }

        private void Apply()
        {
            if (_graphic == null)
            {
                return;
            }

            Color color = Resolve(_role);
            _graphic.color = color.WithAlpha(color.a * _alpha);
        }
    }
}
