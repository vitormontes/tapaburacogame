// Shim de UnityEngine.EventSystems.
// O harness NÃO faz raycast: ele encontra o Button na hierarquia e dispara onClick direto
// (respeitando interactable e objeto ativo). É a única honestidade possível sem renderizador.
using System.Collections.Generic;

namespace UnityEngine.EventSystems
{
    /// <summary>Base de todos os componentes de UI — expõe as mensagens como métodos virtuais.</summary>
    public abstract class UIBehaviour : MonoBehaviour
    {
        protected virtual void Awake()
        {
        }

        protected virtual void OnEnable()
        {
        }

        protected virtual void Start()
        {
        }

        protected virtual void OnDisable()
        {
        }

        protected virtual void OnDestroy()
        {
        }

        public virtual bool IsActive() => isActiveAndEnabled;

        public virtual bool IsDestroyed() => Destroyed;

        protected virtual void OnRectTransformDimensionsChange()
        {
        }

        protected virtual void OnBeforeTransformParentChanged()
        {
        }

        protected virtual void OnTransformParentChanged()
        {
        }

        protected virtual void OnDidApplyAnimationProperties()
        {
        }

        protected virtual void OnCanvasGroupChanged()
        {
        }

        protected virtual void OnCanvasHierarchyChanged()
        {
        }

        protected virtual void OnValidate()
        {
        }

        protected virtual void Reset()
        {
        }
    }

    public class BaseEventData
    {
        public BaseEventData(EventSystem eventSystem)
        {
            currentInputModule = null;
            selectedObject = null;
        }

        public BaseInputModule currentInputModule { get; set; }

        public GameObject selectedObject { get; set; }

        public bool used { get; private set; }

        public void Use() => used = true;

        public void Reset() => used = false;
    }

    public class AxisEventData : BaseEventData
    {
        public AxisEventData(EventSystem eventSystem) : base(eventSystem)
        {
        }

        public Vector2 moveVector { get; set; }
    }

    public class PointerEventData : BaseEventData
    {
        public PointerEventData(EventSystem eventSystem) : base(eventSystem)
        {
        }

        public enum InputButton
        {
            Left = 0,
            Right = 1,
            Middle = 2,
        }

        public enum FramePressState
        {
            Pressed = 0,
            Released = 1,
            PressedAndReleased = 2,
            NotChanged = 3,
        }

        public GameObject pointerEnter { get; set; }

        public GameObject pointerPress { get; set; }

        public GameObject lastPress { get; set; }

        public GameObject pointerDrag { get; set; }

        public GameObject pointerClick { get; set; }

        public Vector2 position { get; set; }

        public Vector2 delta { get; set; }

        public Vector2 pressPosition { get; set; }

        public float clickTime { get; set; }

        public int clickCount { get; set; }

        public Vector2 scrollDelta { get; set; }

        public bool useDragThreshold { get; set; } = true;

        public bool dragging { get; set; }

        public int pointerId { get; set; } = -1;

        public InputButton button { get; set; } = InputButton.Left;

        public bool eligibleForClick { get; set; }
    }

    public interface IEventSystemHandler
    {
    }

    public interface IPointerEnterHandler : IEventSystemHandler
    {
        void OnPointerEnter(PointerEventData eventData);
    }

    public interface IPointerExitHandler : IEventSystemHandler
    {
        void OnPointerExit(PointerEventData eventData);
    }

    public interface IPointerDownHandler : IEventSystemHandler
    {
        void OnPointerDown(PointerEventData eventData);
    }

    public interface IPointerUpHandler : IEventSystemHandler
    {
        void OnPointerUp(PointerEventData eventData);
    }

    public interface IPointerClickHandler : IEventSystemHandler
    {
        void OnPointerClick(PointerEventData eventData);
    }

    public interface IBeginDragHandler : IEventSystemHandler
    {
        void OnBeginDrag(PointerEventData eventData);
    }

    public interface IDragHandler : IEventSystemHandler
    {
        void OnDrag(PointerEventData eventData);
    }

    public interface IEndDragHandler : IEventSystemHandler
    {
        void OnEndDrag(PointerEventData eventData);
    }

    public interface ISelectHandler : IEventSystemHandler
    {
        void OnSelect(BaseEventData eventData);
    }

    public interface IDeselectHandler : IEventSystemHandler
    {
        void OnDeselect(BaseEventData eventData);
    }

    public interface ISubmitHandler : IEventSystemHandler
    {
        void OnSubmit(BaseEventData eventData);
    }

    public interface ICancelHandler : IEventSystemHandler
    {
        void OnCancel(BaseEventData eventData);
    }

    public interface IScrollHandler : IEventSystemHandler
    {
        void OnScroll(PointerEventData eventData);
    }

    /// <summary>Entrega manual de eventos — sem raycast, alvo explícito.</summary>
    public static class ExecuteEvents
    {
        public delegate void EventFunction<T1>(T1 handler, BaseEventData eventData);

        public static EventFunction<IPointerDownHandler> pointerDownHandler
            => (handler, data) => handler.OnPointerDown((PointerEventData)data);

        public static EventFunction<IPointerUpHandler> pointerUpHandler
            => (handler, data) => handler.OnPointerUp((PointerEventData)data);

        public static EventFunction<IPointerClickHandler> pointerClickHandler
            => (handler, data) => handler.OnPointerClick((PointerEventData)data);

        public static EventFunction<IPointerEnterHandler> pointerEnterHandler
            => (handler, data) => handler.OnPointerEnter((PointerEventData)data);

        public static EventFunction<IPointerExitHandler> pointerExitHandler
            => (handler, data) => handler.OnPointerExit((PointerEventData)data);

        public static EventFunction<ISubmitHandler> submitHandler
            => (handler, data) => handler.OnSubmit(data);

        public static bool Execute<T>(GameObject target, BaseEventData eventData, EventFunction<T> functor)
            where T : class, IEventSystemHandler
        {
            if (target == null || !target.activeInHierarchy)
            {
                return false;
            }

            bool any = false;
            T[] handlers = target.GetComponents<T>();
            for (int i = 0; i < handlers.Length; i++)
            {
                functor(handlers[i], eventData);
                any = true;
            }

            return any;
        }

        public static GameObject ExecuteHierarchy<T>(GameObject root, BaseEventData eventData, EventFunction<T> functor)
            where T : class, IEventSystemHandler
        {
            Transform t = root != null ? root.transform : null;
            while (t != null)
            {
                if (Execute(t.gameObject, eventData, functor)) return t.gameObject;
                t = t.parent;
            }

            return null;
        }
    }

    public abstract class BaseInputModule : UIBehaviour
    {
        public virtual void Process()
        {
        }
    }

    public class PointerInputModule : BaseInputModule
    {
    }

    public sealed class StandaloneInputModule : PointerInputModule
    {
        public string horizontalAxis { get; set; } = "Horizontal";

        public string verticalAxis { get; set; } = "Vertical";

        public string submitButton { get; set; } = "Submit";

        public string cancelButton { get; set; } = "Cancel";

        public float inputActionsPerSecond { get; set; } = 10f;
    }

    public sealed class EventSystem : UIBehaviour
    {
        private static readonly List<EventSystem> _all = new List<EventSystem>();

        private GameObject _selected;

        public static EventSystem current
        {
            get
            {
                for (int i = 0; i < _all.Count; i++)
                {
                    if (!_all[i].Destroyed) return _all[i];
                }

                return null;
            }
            set
            {
            }
        }

        public GameObject currentSelectedGameObject => _selected;

        public GameObject firstSelectedGameObject { get; set; }

        public bool sendNavigationEvents { get; set; } = true;

        public int pixelDragThreshold { get; set; } = 10;

        public bool alreadySelecting => false;

        protected override void Awake()
        {
            base.Awake();
            _all.Add(this);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _all.Remove(this);
        }

        public void SetSelectedGameObject(GameObject selected) => _selected = selected;

        public void SetSelectedGameObject(GameObject selected, BaseEventData pointer) => _selected = selected;

        public bool IsPointerOverGameObject() => false;

        public bool IsPointerOverGameObject(int pointerId) => false;
    }
}
