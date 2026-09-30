// Shim de UnityEngine.UI.
// Os componentes guardam estado de verdade (cor, sprite, texto, interatividade) e a hierarquia
// é real, mas NÃO há malha, layout resolvido nem raycast: veja o relatório do harness.
using System;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace UnityEngine
{
    public enum RenderMode
    {
        ScreenSpaceOverlay = 0,
        ScreenSpaceCamera = 1,
        WorldSpace = 2,
    }

    public sealed class CanvasRenderer : Component
    {
        public bool cull { get; set; }

        public int absoluteDepth => 0;

        public bool hasMoved => false;

        public void SetAlpha(float alpha)
        {
        }

        public void SetColor(Color color)
        {
        }

        public void Clear()
        {
        }
    }

    public sealed class Canvas : Behaviour
    {
        public RenderMode renderMode { get; set; } = RenderMode.ScreenSpaceOverlay;

        public bool pixelPerfect { get; set; }

        public int sortingOrder { get; set; }

        public string sortingLayerName { get; set; } = "Default";

        public Camera worldCamera { get; set; }

        public bool overrideSorting { get; set; }

        public float referencePixelsPerUnit { get; set; } = 100f;

        public float scaleFactor { get; set; } = 1f;

        public float planeDistance { get; set; } = 100f;

        public Canvas rootCanvas => GetComponentInParent<Canvas>() ?? this;

        public bool isRootCanvas => rootCanvas == this;

        public Rect pixelRect => new Rect(0f, 0f, Screen.width, Screen.height);

        public AdditionalCanvasShaderChannels additionalShaderChannels { get; set; }
    }

    [Flags]
    public enum AdditionalCanvasShaderChannels
    {
        None = 0,
        TexCoord1 = 1,
        TexCoord2 = 2,
        TexCoord3 = 4,
        Normal = 8,
        Tangent = 16,
    }
}

namespace UnityEngine.UI
{
    public enum HorizontalWrapMode
    {
        Wrap = 0,
        Overflow = 1,
    }

    public enum VerticalWrapMode
    {
        Truncate = 0,
        Overflow = 1,
    }

    public interface ILayoutElement
    {
        float minWidth { get; }

        float preferredWidth { get; }

        float flexibleWidth { get; }

        float minHeight { get; }

        float preferredHeight { get; }

        float flexibleHeight { get; }

        int layoutPriority { get; }

        void CalculateLayoutInputHorizontal();

        void CalculateLayoutInputVertical();
    }

    public interface ILayoutController
    {
        void SetLayoutHorizontal();

        void SetLayoutVertical();
    }

    public interface ILayoutGroup : ILayoutController
    {
    }

    public interface ILayoutSelfController : ILayoutController
    {
    }

    public interface ILayoutIgnorer
    {
        bool ignoreLayout { get; }
    }

    /// <summary>Base gráfica: cor, sprite/texto e o retângulo. Sem malha — não há renderizador.</summary>
    public abstract class Graphic : UIBehaviour
    {
        private Color _color = Color.white;

        /// <summary>Harness: quantas vezes o gráfico foi marcado como sujo.</summary>
        internal int DirtyCount { get; private set; }

        public virtual Color color
        {
            get => _color;
            set
            {
                _color = value;
                SetVerticesDirty();
            }
        }

        public bool raycastTarget { get; set; } = true;

        public Vector4 raycastPadding { get; set; }

        public bool maskable { get; set; } = true;

        public int depth => 0;

        public Material material { get; set; }

        public Material materialForRendering => material;

        public RectTransform rectTransform => (RectTransform)transform;

        public Canvas canvas => GetComponentInParent<Canvas>();

        public CanvasRenderer canvasRenderer => GetComponent<CanvasRenderer>() ?? gameObject.AddComponent<CanvasRenderer>();

        public static Material defaultGraphicMaterial { get; } = new Material();

        public virtual void SetAllDirty()
        {
            DirtyCount++;
        }

        public virtual void SetVerticesDirty()
        {
            DirtyCount++;
        }

        public virtual void SetMaterialDirty()
        {
            DirtyCount++;
        }

        public virtual void SetLayoutDirty()
        {
            DirtyCount++;
        }

        public virtual void SetNativeSize()
        {
        }

        public virtual void Rebuild(object update)
        {
        }

        public void CrossFadeColor(Color targetColor, float duration, bool ignoreTimeScale, bool useAlpha)
            => color = targetColor;

        public void CrossFadeAlpha(float alpha, float duration, bool ignoreTimeScale)
            => color = new Color(color.r, color.g, color.b, alpha);
    }

    public abstract class MaskableGraphic : Graphic, IMaskable
    {
        public bool isMaskingGraphic { get; set; }

        public virtual void RecalculateMasking()
        {
        }
    }

    public interface IMaskable
    {
        void RecalculateMasking();
    }

    public class Image : MaskableGraphic, ILayoutElement
    {
        public enum Type
        {
            Simple = 0,
            Sliced = 1,
            Tiled = 2,
            Filled = 3,
        }

        public enum FillMethod
        {
            Horizontal = 0,
            Vertical = 1,
            Radial90 = 2,
            Radial180 = 3,
            Radial360 = 4,
        }

        public enum OriginHorizontal
        {
            Left = 0,
            Right = 1,
        }

        public Sprite sprite { get; set; }

        public Sprite overrideSprite { get; set; }

        public Sprite activeSprite => overrideSprite != null ? overrideSprite : sprite;

        public Type type { get; set; } = Type.Simple;

        public bool preserveAspect { get; set; }

        public bool fillCenter { get; set; } = true;

        public FillMethod fillMethod { get; set; } = FillMethod.Radial360;

        public float fillAmount { get; set; } = 1f;

        public bool fillClockwise { get; set; } = true;

        public int fillOrigin { get; set; }

        public float pixelsPerUnitMultiplier { get; set; } = 1f;

        public float alphaHitTestMinimumThreshold { get; set; }

        public bool useSpriteMesh { get; set; }

        public float minWidth => 0f;

        public float preferredWidth => activeSprite != null ? activeSprite.rect.width : 0f;

        public float flexibleWidth => -1f;

        public float minHeight => 0f;

        public float preferredHeight => activeSprite != null ? activeSprite.rect.height : 0f;

        public float flexibleHeight => -1f;

        public int layoutPriority => 0;

        public void CalculateLayoutInputHorizontal()
        {
        }

        public void CalculateLayoutInputVertical()
        {
        }

        public override void SetNativeSize()
        {
            if (activeSprite == null)
            {
                return;
            }

            rectTransform.sizeDelta = new Vector2(activeSprite.rect.width, activeSprite.rect.height);
        }
    }

    public class RawImage : MaskableGraphic
    {
        public Texture texture { get; set; }

        public Rect uvRect { get; set; } = new Rect(0f, 0f, 1f, 1f);
    }

    /// <summary>
    /// Text do uGUI. As medidas (<see cref="preferredWidth"/>/<see cref="preferredHeight"/>) são
    /// APROXIMADAS por largura média de glifo — não há fonte rasterizada no shim.
    /// </summary>
    public class Text : MaskableGraphic, ILayoutElement
    {
        private const float AverageGlyphWidth = 0.55f;

        private const float LineHeightFactor = 1.16f;

        public virtual string text { get; set; } = string.Empty;

        public Font font { get; set; }

        public int fontSize { get; set; } = 14;

        public FontStyle fontStyle { get; set; } = FontStyle.Normal;

        public TextAnchor alignment { get; set; } = TextAnchor.UpperLeft;

        public bool alignByGeometry { get; set; }

        public bool supportRichText { get; set; } = true;

        public HorizontalWrapMode horizontalOverflow { get; set; } = HorizontalWrapMode.Wrap;

        public VerticalWrapMode verticalOverflow { get; set; } = VerticalWrapMode.Truncate;

        public bool resizeTextForBestFit { get; set; }

        public int resizeTextMinSize { get; set; } = 10;

        public int resizeTextMaxSize { get; set; } = 40;

        public float lineSpacing { get; set; } = 1f;

        public string cachedTextGenerator => text;

        public float minWidth => 0f;

        public float preferredWidth => MeasureLongestLine() * fontSize * AverageGlyphWidth;

        public float flexibleWidth => -1f;

        public float minHeight => 0f;

        public float preferredHeight => LineCount() * fontSize * LineHeightFactor * lineSpacing;

        public float flexibleHeight => -1f;

        public int layoutPriority => 0;

        public void CalculateLayoutInputHorizontal()
        {
        }

        public void CalculateLayoutInputVertical()
        {
        }

        /// <summary>Texto sem as tags de rich text — o que o harness compara.</summary>
        internal string PlainText => StripTags(text);

        private int LineCount()
        {
            string plain = PlainText;
            int lines = 1;
            for (int i = 0; i < plain.Length; i++)
            {
                if (plain[i] == '\n') lines++;
            }

            return lines;
        }

        private int MeasureLongestLine()
        {
            string plain = PlainText;
            int best = 0;
            int run = 0;
            for (int i = 0; i < plain.Length; i++)
            {
                if (plain[i] == '\n')
                {
                    if (run > best) best = run;
                    run = 0;
                    continue;
                }

                run++;
            }

            return run > best ? run : best;
        }

        internal static string StripTags(string raw)
        {
            if (string.IsNullOrEmpty(raw) || raw.IndexOf('<') < 0)
            {
                return raw ?? string.Empty;
            }

            var sb = new System.Text.StringBuilder(raw.Length);
            bool inside = false;
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                if (c == '<')
                {
                    inside = true;
                    continue;
                }

                if (c == '>')
                {
                    inside = false;
                    continue;
                }

                if (!inside) sb.Append(c);
            }

            return sb.ToString();
        }
    }

    [Serializable]
    public struct ColorBlock
    {
        public Color normalColor;
        public Color highlightedColor;
        public Color pressedColor;
        public Color selectedColor;
        public Color disabledColor;
        public float colorMultiplier;
        public float fadeDuration;

        public static ColorBlock defaultColorBlock => new ColorBlock
        {
            normalColor = Color.white,
            highlightedColor = new Color(0.96f, 0.96f, 0.96f, 1f),
            pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f),
            selectedColor = new Color(0.96f, 0.96f, 0.96f, 1f),
            disabledColor = new Color(0.78f, 0.78f, 0.78f, 0.5f),
            colorMultiplier = 1f,
            fadeDuration = 0.1f,
        };
    }

    [Serializable]
    public struct SpriteState
    {
        public Sprite highlightedSprite;
        public Sprite pressedSprite;
        public Sprite selectedSprite;
        public Sprite disabledSprite;
    }

    [Serializable]
    public struct Navigation
    {
        public enum Mode
        {
            None = 0,
            Horizontal = 1,
            Vertical = 2,
            Automatic = 3,
            Explicit = 4,
        }

        public Mode mode;

        public static Navigation defaultNavigation => new Navigation { mode = Mode.Automatic };
    }

    public class Selectable : UIBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler,
        IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public enum Transition
        {
            None = 0,
            ColorTint = 1,
            SpriteSwap = 2,
            Animation = 3,
        }

        private bool _interactable = true;

        public bool interactable
        {
            get => _interactable;
            set => _interactable = value;
        }

        public Graphic targetGraphic { get; set; }

        public Image image
        {
            get => targetGraphic as Image;
            set => targetGraphic = value;
        }

        public Transition transition { get; set; } = Transition.ColorTint;

        public ColorBlock colors { get; set; } = ColorBlock.defaultColorBlock;

        public SpriteState spriteState { get; set; }

        public Navigation navigation { get; set; } = Navigation.defaultNavigation;

        public virtual bool IsInteractable() => _interactable;

        public virtual void OnPointerDown(PointerEventData eventData)
        {
        }

        public virtual void OnPointerUp(PointerEventData eventData)
        {
        }

        public virtual void OnPointerEnter(PointerEventData eventData)
        {
        }

        public virtual void OnPointerExit(PointerEventData eventData)
        {
        }

        public virtual void OnSelect(BaseEventData eventData)
        {
        }

        public virtual void OnDeselect(BaseEventData eventData)
        {
        }

        public virtual void Select()
        {
        }
    }

    public class Button : Selectable, IPointerClickHandler, ISubmitHandler
    {
        [Serializable]
        public class ButtonClickedEvent : UnityEvent
        {
        }

        public ButtonClickedEvent onClick { get; set; } = new ButtonClickedEvent();

        public virtual void OnPointerClick(PointerEventData eventData)
        {
            Press();
        }

        public virtual void OnSubmit(BaseEventData eventData)
        {
            Press();
        }

        private void Press()
        {
            if (!IsActive() || !IsInteractable())
            {
                return;
            }

            onClick.Invoke();
        }
    }

    public class Toggle : Selectable, IPointerClickHandler, ISubmitHandler
    {
        [Serializable]
        public class ToggleEvent : UnityEvent<bool>
        {
        }

        private bool _isOn;

        public Graphic graphic { get; set; }

        public ToggleEvent onValueChanged { get; set; } = new ToggleEvent();

        public bool isOn
        {
            get => _isOn;
            set
            {
                if (_isOn == value) return;
                _isOn = value;
                onValueChanged.Invoke(value);
            }
        }

        public void SetIsOnWithoutNotify(bool value) => _isOn = value;

        public virtual void OnPointerClick(PointerEventData eventData)
        {
            if (IsActive() && IsInteractable()) isOn = !isOn;
        }

        public virtual void OnSubmit(BaseEventData eventData) => OnPointerClick(null);
    }

    public class Slider : Selectable
    {
        [Serializable]
        public class SliderEvent : UnityEvent<float>
        {
        }

        private float _value;

        public float minValue { get; set; }

        public float maxValue { get; set; } = 1f;

        public bool wholeNumbers { get; set; }

        public SliderEvent onValueChanged { get; set; } = new SliderEvent();

        public float value
        {
            get => _value;
            set
            {
                float clamped = Mathf.Clamp(value, minValue, maxValue);
                if (Mathf.Approximately(_value, clamped)) return;
                _value = clamped;
                onValueChanged.Invoke(clamped);
            }
        }

        public float normalizedValue
        {
            get => Mathf.InverseLerp(minValue, maxValue, _value);
            set => this.value = Mathf.Lerp(minValue, maxValue, value);
        }

        public void SetValueWithoutNotify(float input) => _value = Mathf.Clamp(input, minValue, maxValue);
    }

    public class Scrollbar : Selectable
    {
        [Serializable]
        public class ScrollEvent : UnityEvent<float>
        {
        }

        public enum Direction
        {
            LeftToRight = 0,
            RightToLeft = 1,
            BottomToTop = 2,
            TopToBottom = 3,
        }

        public RectTransform handleRect { get; set; }

        public Direction direction { get; set; } = Direction.LeftToRight;

        public float value { get; set; }

        public float size { get; set; } = 0.2f;

        public int numberOfSteps { get; set; }

        public ScrollEvent onValueChanged { get; set; } = new ScrollEvent();
    }

    public class ScrollRect : UIBehaviour, ILayoutGroup
    {
        [Serializable]
        public class ScrollRectEvent : UnityEvent<Vector2>
        {
        }

        public enum MovementType
        {
            Unrestricted = 0,
            Elastic = 1,
            Clamped = 2,
        }

        public RectTransform content { get; set; }

        public RectTransform viewport { get; set; }

        public bool horizontal { get; set; } = true;

        public bool vertical { get; set; } = true;

        public MovementType movementType { get; set; } = MovementType.Elastic;

        public float elasticity { get; set; } = 0.1f;

        public bool inertia { get; set; } = true;

        public float decelerationRate { get; set; } = 0.135f;

        public float scrollSensitivity { get; set; } = 1f;

        public Scrollbar horizontalScrollbar { get; set; }

        public Scrollbar verticalScrollbar { get; set; }

        public Vector2 normalizedPosition { get; set; }

        public float horizontalNormalizedPosition { get; set; }

        public float verticalNormalizedPosition { get; set; } = 1f;

        public Vector2 velocity { get; set; }

        public ScrollRectEvent onValueChanged { get; set; } = new ScrollRectEvent();

        public void SetLayoutHorizontal()
        {
        }

        public void SetLayoutVertical()
        {
        }
    }

    public class LayoutElement : UIBehaviour, ILayoutElement, ILayoutIgnorer
    {
        public bool ignoreLayout { get; set; }

        public float minWidth { get; set; } = -1f;

        public float minHeight { get; set; } = -1f;

        public float preferredWidth { get; set; } = -1f;

        public float preferredHeight { get; set; } = -1f;

        public float flexibleWidth { get; set; } = -1f;

        public float flexibleHeight { get; set; } = -1f;

        public int layoutPriority { get; set; } = 1;

        public void CalculateLayoutInputHorizontal()
        {
        }

        public void CalculateLayoutInputVertical()
        {
        }
    }

    public abstract class LayoutGroup : UIBehaviour, ILayoutElement, ILayoutGroup
    {
        private RectOffset _padding = new RectOffset();

        public RectOffset padding
        {
            get => _padding;
            set => _padding = value ?? new RectOffset();
        }

        public TextAnchor childAlignment { get; set; } = TextAnchor.UpperLeft;

        public RectTransform rectTransform => (RectTransform)transform;

        public virtual float minWidth => 0f;

        public virtual float preferredWidth => 0f;

        public virtual float flexibleWidth => -1f;

        public virtual float minHeight => 0f;

        public virtual float preferredHeight => 0f;

        public virtual float flexibleHeight => -1f;

        public virtual int layoutPriority => 0;

        public virtual void CalculateLayoutInputHorizontal()
        {
        }

        public virtual void CalculateLayoutInputVertical()
        {
        }

        public virtual void SetLayoutHorizontal()
        {
        }

        public virtual void SetLayoutVertical()
        {
        }
    }

    public abstract class HorizontalOrVerticalLayoutGroup : LayoutGroup
    {
        public float spacing { get; set; }

        public bool childForceExpandWidth { get; set; } = true;

        public bool childForceExpandHeight { get; set; } = true;

        public bool childControlWidth { get; set; } = true;

        public bool childControlHeight { get; set; } = true;

        public bool childScaleWidth { get; set; }

        public bool childScaleHeight { get; set; }

        public bool reverseArrangement { get; set; }
    }

    public sealed class HorizontalLayoutGroup : HorizontalOrVerticalLayoutGroup
    {
    }

    public sealed class VerticalLayoutGroup : HorizontalOrVerticalLayoutGroup
    {
    }

    public sealed class GridLayoutGroup : LayoutGroup
    {
        public enum Corner
        {
            UpperLeft = 0,
            UpperRight = 1,
            LowerLeft = 2,
            LowerRight = 3,
        }

        public enum Axis
        {
            Horizontal = 0,
            Vertical = 1,
        }

        public enum Constraint
        {
            Flexible = 0,
            FixedColumnCount = 1,
            FixedRowCount = 2,
        }

        public Corner startCorner { get; set; }

        public Axis startAxis { get; set; }

        public Vector2 cellSize { get; set; } = new Vector2(100f, 100f);

        public Vector2 spacing { get; set; }

        public Constraint constraint { get; set; }

        public int constraintCount { get; set; } = 2;
    }

    public sealed class ContentSizeFitter : UIBehaviour, ILayoutSelfController
    {
        public enum FitMode
        {
            Unconstrained = 0,
            MinSize = 1,
            PreferredSize = 2,
        }

        public FitMode horizontalFit { get; set; } = FitMode.Unconstrained;

        public FitMode verticalFit { get; set; } = FitMode.Unconstrained;

        public void SetLayoutHorizontal()
        {
        }

        public void SetLayoutVertical()
        {
        }
    }

    public sealed class AspectRatioFitter : UIBehaviour, ILayoutSelfController
    {
        public enum AspectMode
        {
            None = 0,
            WidthControlsHeight = 1,
            HeightControlsWidth = 2,
            FitInParent = 3,
            EnvelopeParent = 4,
        }

        public AspectMode aspectMode { get; set; }

        public float aspectRatio { get; set; } = 1f;

        public void SetLayoutHorizontal()
        {
        }

        public void SetLayoutVertical()
        {
        }
    }

    /// <summary>
    /// APROXIMAÇÃO: o shim não resolve layout, então reconstruir é um no-op contabilizado.
    /// O harness registra as chamadas para provar que o jogo pede rebuild quando deve.
    /// </summary>
    public static class LayoutRebuilder
    {
        internal static int RebuildRequests;

        public static void MarkLayoutForRebuild(RectTransform rect) => RebuildRequests++;

        public static void ForceRebuildLayoutImmediate(RectTransform rect) => RebuildRequests++;
    }

    public static class LayoutUtility
    {
        public static float GetMinWidth(RectTransform rect) => Query(rect, e => e.minWidth);

        public static float GetPreferredWidth(RectTransform rect) => Query(rect, e => e.preferredWidth);

        public static float GetFlexibleWidth(RectTransform rect) => Query(rect, e => e.flexibleWidth);

        public static float GetMinHeight(RectTransform rect) => Query(rect, e => e.minHeight);

        public static float GetPreferredHeight(RectTransform rect) => Query(rect, e => e.preferredHeight);

        public static float GetFlexibleHeight(RectTransform rect) => Query(rect, e => e.flexibleHeight);

        private static float Query(RectTransform rect, Func<ILayoutElement, float> pick)
        {
            if (rect == null)
            {
                return 0f;
            }

            float best = 0f;
            ILayoutElement[] elements = rect.gameObject.GetComponents<ILayoutElement>();
            for (int i = 0; i < elements.Length; i++)
            {
                float v = pick(elements[i]);
                if (v > best) best = v;
            }

            return best;
        }
    }

    public abstract class BaseMeshEffect : UIBehaviour
    {
        public Graphic graphic => GetComponent<Graphic>();
    }

    public class Shadow : BaseMeshEffect
    {
        public Color effectColor { get; set; } = new Color(0f, 0f, 0f, 0.5f);

        public Vector2 effectDistance { get; set; } = new Vector2(1f, -1f);

        public bool useGraphicAlpha { get; set; } = true;
    }

    public sealed class Outline : Shadow
    {
    }

    public sealed class PositionAsUV1 : BaseMeshEffect
    {
    }

    public sealed class Mask : UIBehaviour
    {
        public bool showMaskGraphic { get; set; } = true;

        public Graphic graphic => GetComponent<Graphic>();

        public RectTransform rectTransform => (RectTransform)transform;

        public bool MaskEnabled() => isActiveAndEnabled && graphic != null;
    }

    public sealed class RectMask2D : UIBehaviour
    {
        public Vector4 padding { get; set; }

        public Vector2Int softness { get; set; }

        public RectTransform rectTransform => (RectTransform)transform;
    }

    public sealed class GraphicRaycaster : UIBehaviour
    {
        public enum BlockingObjects
        {
            None = 0,
            TwoD = 1,
            ThreeD = 2,
            All = 3,
        }

        public bool ignoreReversedGraphics { get; set; } = true;

        public BlockingObjects blockingObjects { get; set; } = BlockingObjects.None;

        public Canvas canvas => GetComponent<Canvas>();

        public void Raycast(PointerEventData eventData, List<object> resultAppendList)
        {
            // Sem renderizador não há raycast: o harness clica no Button diretamente.
        }
    }

    public sealed class CanvasScaler : UIBehaviour
    {
        public enum ScaleMode
        {
            ConstantPixelSize = 0,
            ScaleWithScreenSize = 1,
            ConstantPhysicalSize = 2,
        }

        public enum ScreenMatchMode
        {
            MatchWidthOrHeight = 0,
            Expand = 1,
            Shrink = 2,
        }

        public enum Unit
        {
            Centimeters = 0,
            Millimeters = 1,
            Inches = 2,
            Points = 3,
            Picas = 4,
        }

        public ScaleMode uiScaleMode { get; set; } = ScaleMode.ConstantPixelSize;

        public float referencePixelsPerUnit { get; set; } = 100f;

        public float scaleFactor { get; set; } = 1f;

        public Vector2 referenceResolution { get; set; } = new Vector2(800f, 600f);

        public ScreenMatchMode screenMatchMode { get; set; } = ScreenMatchMode.MatchWidthOrHeight;

        public float matchWidthOrHeight { get; set; }

        public Unit physicalUnit { get; set; } = Unit.Points;

        public float fallbackScreenDPI { get; set; } = 96f;

        public float defaultSpriteDPI { get; set; } = 96f;

        public float dynamicPixelsPerUnit { get; set; } = 1f;
    }
}

namespace UnityEngine
{
    public struct Vector2Int
    {
        public Vector2Int(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public int x { get; set; }

        public int y { get; set; }

        public static Vector2Int zero => new Vector2Int(0, 0);

        public override string ToString() => $"({x}, {y})";
    }
}
