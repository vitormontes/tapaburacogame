using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TapaBuraco.Game
{
    /// <summary>
    /// Afunda o botão ao toque, como no CSS do protótipo:
    /// <c>.btn:active{transform:translateY(4px);box-shadow:0 1px 0}</c>.
    /// O conteúdo desce e a sombra encolhe — nada de tween genérico, só dois retângulos.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PressPop : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private RectTransform _content;
        private RectTransform _shadow;
        private Selectable _selectable;
        private float _travel = 4f;
        private float _shadowDepth = 5f;
        private Vector2 _contentHome;
        private bool _down;

        /// <summary>Liga o efeito a um botão já montado.</summary>
        public static PressPop Attach(GameObject target, RectTransform content, RectTransform shadow, float travel, float shadowDepth)
        {
            PressPop pop = target.GetComponent<PressPop>();
            if (pop == null)
            {
                pop = target.AddComponent<PressPop>();
            }

            pop._content = content;
            pop._shadow = shadow;
            pop._selectable = target.GetComponent<Selectable>();
            pop._travel = travel;
            pop._shadowDepth = shadowDepth;
            pop._contentHome = content.anchoredPosition;
            pop.Release();
            return pop;
        }

        /// <summary>Solta o botão à força (usado quando a tela troca no meio do toque).</summary>
        public void Release()
        {
            _down = false;
            if (_content != null)
            {
                _content.anchoredPosition = _contentHome;
            }

            if (_shadow != null)
            {
                _shadow.anchoredPosition = new Vector2(_shadow.anchoredPosition.x, -_shadowDepth);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_selectable != null && !_selectable.IsInteractable())
            {
                return;
            }

            _down = true;
            if (_content != null)
            {
                _content.anchoredPosition = _contentHome + new Vector2(0f, -_travel);
            }

            if (_shadow != null)
            {
                _shadow.anchoredPosition = new Vector2(_shadow.anchoredPosition.x, -Mathf.Min(1f, _shadowDepth));
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_down)
            {
                return;
            }

            Release();
        }

        private void OnDisable() => Release();
    }
}
