// Shim de UnityEngine — modelo de objetos (Object/GameObject/Component/Transform).
// A hierarquia é REAL: pais, filhos, ordem de irmãos e SetActive propagando de verdade.
// Quem chama Awake/OnEnable/Start/Update/OnDisable/OnDestroy é o UnityRuntime (Shim/UnityRuntime.cs).
using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityEngine
{
    [Flags]
    public enum HideFlags
    {
        None = 0,
        HideInHierarchy = 1,
        HideInInspector = 2,
        DontSaveInEditor = 4,
        NotEditable = 8,
        DontSaveInBuild = 16,
        DontUnloadUnusedAsset = 32,
        DontSave = 52,
        HideAndDontSave = 61,
    }

    public class Object
    {
        private static int _nextId = 1;

        private readonly int _id;

        protected Object()
        {
            _id = _nextId++;
        }

        public string name { get; set; } = string.Empty;

        public HideFlags hideFlags { get; set; } = HideFlags.None;

        /// <summary>true depois de Destroy — reproduz o "null falso" da Unity.</summary>
        internal bool Destroyed { get; set; }

        public int GetInstanceID() => _id;

        public override string ToString() => $"{name} ({GetType().Name})";

        public static void Destroy(Object obj) => UnityRuntime.ScheduleDestroy(obj);

        public static void Destroy(Object obj, float delay) => UnityRuntime.ScheduleDestroy(obj, delay);

        public static void DestroyImmediate(Object obj) => UnityRuntime.DestroyNow(obj);

        public static void DestroyImmediate(Object obj, bool allowDestroyingAssets) => UnityRuntime.DestroyNow(obj);

        public static void DontDestroyOnLoad(Object target) => UnityRuntime.MarkPersistent(target);

        public static T Instantiate<T>(T original) where T : Object
            => throw new NotSupportedException("O shim não clona objetos: o jogo monta tudo em código.");

        public static T FindFirstObjectByType<T>() where T : Object => UnityRuntime.FindFirst<T>();

        public static T FindAnyObjectByType<T>() where T : Object => UnityRuntime.FindFirst<T>();

        public static T FindObjectOfType<T>() where T : Object => UnityRuntime.FindFirst<T>();

        public static T[] FindObjectsByType<T>(FindObjectsSortMode sortMode) where T : Object
            => UnityRuntime.FindAll<T>();

        public static T[] FindObjectsOfType<T>() where T : Object => UnityRuntime.FindAll<T>();

        public static bool operator ==(Object a, Object b)
        {
            bool aNull = ReferenceEquals(a, null) || a.Destroyed;
            bool bNull = ReferenceEquals(b, null) || b.Destroyed;
            if (aNull && bNull) return true;
            if (aNull || bNull) return false;
            return ReferenceEquals(a, b);
        }

        public static bool operator !=(Object a, Object b) => !(a == b);

        public static implicit operator bool(Object obj) => !ReferenceEquals(obj, null) && !obj.Destroyed;

        public override bool Equals(object other) => ReferenceEquals(this, other);

        public override int GetHashCode() => _id;
    }

    public enum FindObjectsSortMode
    {
        None = 0,
        InstanceID = 1,
    }

    public sealed class GameObject : Object
    {
        private readonly List<Component> _components = new List<Component>();

        private bool _active = true;

        public GameObject() : this("GameObject")
        {
        }

        public GameObject(string goName)
        {
            name = goName;
            transform = new RectTransform();
            transform.AttachTo(this);
            _components.Add(transform);
            UnityRuntime.RegisterGameObject(this);
        }

        public GameObject(string goName, params Type[] components) : this(goName)
        {
            if (components == null)
            {
                return;
            }

            for (int i = 0; i < components.Length; i++)
            {
                AddComponent(components[i]);
            }
        }

        public RectTransform transform { get; }

        public string tag { get; set; } = "Untagged";

        public int layer { get; set; }

        /// <summary>Estado do próprio objeto (o que SetActive liga/desliga).</summary>
        public bool activeSelf => _active;

        /// <summary>Ativo de verdade: precisa que todos os pais também estejam ativos.</summary>
        public bool activeInHierarchy
        {
            get
            {
                if (!_active || Destroyed)
                {
                    return false;
                }

                Transform p = transform.parent;
                while (p != null)
                {
                    if (!p.gameObject._active) return false;
                    p = p.parent;
                }

                return true;
            }
        }

        internal IReadOnlyList<Component> Components => _components;

        public void SetActive(bool value)
        {
            if (_active == value)
            {
                return;
            }

            bool wasActive = activeInHierarchy;
            _active = value;
            bool nowActive = activeInHierarchy;
            if (wasActive != nowActive)
            {
                UnityRuntime.PropagateActiveChange(this, nowActive);
            }
        }

        public bool CompareTag(string other) => tag == other;

        public T AddComponent<T>() where T : Component, new() => (T)AddComponent(typeof(T));

        public Component AddComponent(Type type)
        {
            if (type == typeof(Transform) || type == typeof(RectTransform))
            {
                // A Unity nunca deixa um GameObject com dois Transform; o shim também não.
                return transform;
            }

            var component = (Component)Activator.CreateInstance(type, nonPublic: true);
            component.AttachTo(this);
            _components.Add(component);
            UnityRuntime.RegisterComponent(component);
            return component;
        }

        public T GetComponent<T>() where T : class
        {
            for (int i = 0; i < _components.Count; i++)
            {
                if (_components[i] is T match) return match;
            }

            return null;
        }

        public Component GetComponent(Type type)
        {
            for (int i = 0; i < _components.Count; i++)
            {
                if (type.IsInstanceOfType(_components[i])) return _components[i];
            }

            return null;
        }

        public bool TryGetComponent<T>(out T component) where T : class
        {
            component = GetComponent<T>();
            return component != null;
        }

        public T[] GetComponents<T>() where T : class
        {
            var found = new List<T>();
            for (int i = 0; i < _components.Count; i++)
            {
                if (_components[i] is T match) found.Add(match);
            }

            return found.ToArray();
        }

        public T GetComponentInChildren<T>() where T : class => GetComponentInChildren<T>(false);

        public T GetComponentInChildren<T>(bool includeInactive) where T : class
        {
            if (!includeInactive && !activeInHierarchy)
            {
                return null;
            }

            T own = GetComponent<T>();
            if (own != null) return own;
            for (int i = 0; i < transform.childCount; i++)
            {
                T child = transform.GetChild(i).gameObject.GetComponentInChildren<T>(includeInactive);
                if (child != null) return child;
            }

            return null;
        }

        public T[] GetComponentsInChildren<T>() where T : class => GetComponentsInChildren<T>(false);

        public T[] GetComponentsInChildren<T>(bool includeInactive) where T : class
        {
            var found = new List<T>();
            Collect(this, includeInactive, found);
            return found.ToArray();
        }

        public T GetComponentInParent<T>() where T : class
        {
            Transform t = transform;
            while (t != null)
            {
                T match = t.gameObject.GetComponent<T>();
                if (match != null) return match;
                t = t.parent;
            }

            return null;
        }

        private static void Collect<T>(GameObject go, bool includeInactive, List<T> into) where T : class
        {
            if (!includeInactive && !go.activeInHierarchy)
            {
                return;
            }

            for (int i = 0; i < go._components.Count; i++)
            {
                if (go._components[i] is T match) into.Add(match);
            }

            for (int i = 0; i < go.transform.childCount; i++)
            {
                Collect(go.transform.GetChild(i).gameObject, includeInactive, into);
            }
        }

        internal void RemoveComponent(Component component) => _components.Remove(component);

        public static GameObject Find(string search) => UnityRuntime.FindGameObject(search);

        public static GameObject FindWithTag(string searchTag) => UnityRuntime.FindGameObjectWithTag(searchTag);
    }

    public abstract class Component : Object
    {
        public GameObject gameObject { get; private set; }

        public Transform transform => gameObject.transform;

        public new string name
        {
            get => gameObject != null ? gameObject.name : base.name;
            set
            {
                if (gameObject != null) gameObject.name = value;
                else base.name = value;
            }
        }

        public string tag
        {
            get => gameObject.tag;
            set => gameObject.tag = value;
        }

        internal void AttachTo(GameObject owner)
        {
            gameObject = owner;
        }

        public T GetComponent<T>() where T : class => gameObject.GetComponent<T>();

        public Component GetComponent(Type type) => gameObject.GetComponent(type);

        public bool TryGetComponent<T>(out T component) where T : class => gameObject.TryGetComponent(out component);

        public T[] GetComponents<T>() where T : class => gameObject.GetComponents<T>();

        public T GetComponentInChildren<T>() where T : class => gameObject.GetComponentInChildren<T>();

        public T GetComponentInChildren<T>(bool includeInactive) where T : class
            => gameObject.GetComponentInChildren<T>(includeInactive);

        public T[] GetComponentsInChildren<T>() where T : class => gameObject.GetComponentsInChildren<T>();

        public T[] GetComponentsInChildren<T>(bool includeInactive) where T : class
            => gameObject.GetComponentsInChildren<T>(includeInactive);

        public T GetComponentInParent<T>() where T : class => gameObject.GetComponentInParent<T>();

        public bool CompareTag(string other) => gameObject.CompareTag(other);
    }

    public abstract class Behaviour : Component
    {
        private bool _enabled = true;

        public bool enabled
        {
            get => _enabled;
            set
            {
                if (_enabled == value)
                {
                    return;
                }

                _enabled = value;
                if (gameObject != null && gameObject.activeInHierarchy)
                {
                    UnityRuntime.ToggleBehaviour(this, value);
                }
            }
        }

        public bool isActiveAndEnabled => _enabled && gameObject != null && gameObject.activeInHierarchy;
    }

    public class MonoBehaviour : Behaviour
    {
        public Coroutine StartCoroutine(IEnumerator routine) => UnityRuntime.StartCoroutine(this, routine);

        public Coroutine StartCoroutine(string methodName) => throw new NotSupportedException(
            "O shim não inicia corrotina por nome; o jogo usa sempre a sobrecarga com IEnumerator.");

        public void StopCoroutine(Coroutine routine) => UnityRuntime.StopCoroutine(routine);

        public void StopCoroutine(IEnumerator routine) => UnityRuntime.StopCoroutine(this, routine);

        public void StopAllCoroutines() => UnityRuntime.StopAllCoroutines(this);

        public void Invoke(string methodName, float time) => UnityRuntime.Invoke(this, methodName, time);

        public void InvokeRepeating(string methodName, float time, float repeatRate)
            => UnityRuntime.InvokeRepeating(this, methodName, time, repeatRate);

        public void CancelInvoke() => UnityRuntime.CancelInvoke(this);

        public void CancelInvoke(string methodName) => UnityRuntime.CancelInvoke(this, methodName);

        public bool IsInvoking() => UnityRuntime.IsInvoking(this);

        public static void print(object message) => Debug.Log(message);
    }

    public class Transform : Component, IEnumerable
    {
        private readonly List<Transform> _children = new List<Transform>();

        public Vector3 localPosition { get; set; } = Vector3.zero;

        public Quaternion localRotation { get; set; } = Quaternion.identity;

        public Vector3 localScale { get; set; } = Vector3.one;

        public Transform parent { get; private set; }

        public Transform root
        {
            get
            {
                Transform t = this;
                while (t.parent != null) t = t.parent;
                return t;
            }
        }

        public int childCount => _children.Count;

        public Vector3 position
        {
            get
            {
                Vector3 p = localPosition;
                Transform t = parent;
                while (t != null)
                {
                    p = new Vector3(p.x * t.localScale.x, p.y * t.localScale.y, p.z * t.localScale.z) + t.localPosition;
                    t = t.parent;
                }

                return p;
            }
            set => localPosition = parent == null ? value : value - parent.position;
        }

        public Quaternion rotation
        {
            get => localRotation;
            set => localRotation = value;
        }

        public Vector3 localEulerAngles
        {
            get => localRotation.eulerAngles;
            set => localRotation = Quaternion.Euler(value);
        }

        public Vector3 eulerAngles
        {
            get => rotation.eulerAngles;
            set => rotation = Quaternion.Euler(value);
        }

        public Vector3 lossyScale
        {
            get
            {
                Vector3 s = localScale;
                Transform t = parent;
                while (t != null)
                {
                    s = new Vector3(s.x * t.localScale.x, s.y * t.localScale.y, s.z * t.localScale.z);
                    t = t.parent;
                }

                return s;
            }
        }

        public void SetParent(Transform newParent) => SetParent(newParent, true);

        public void SetParent(Transform newParent, bool worldPositionStays)
        {
            if (parent == newParent)
            {
                return;
            }

            bool wasActive = gameObject.activeInHierarchy;
            parent?._children.Remove(this);
            parent = newParent;
            newParent?._children.Add(this);

            if (!worldPositionStays)
            {
                localPosition = Vector3.zero;
                localRotation = Quaternion.identity;
                localScale = Vector3.one;
            }

            bool nowActive = gameObject.activeInHierarchy;
            if (wasActive != nowActive)
            {
                UnityRuntime.PropagateActiveChange(gameObject, nowActive);
            }
        }

        public Transform GetChild(int index) => _children[index];

        public int GetSiblingIndex() => parent == null ? 0 : parent._children.IndexOf(this);

        public void SetSiblingIndex(int index)
        {
            if (parent == null)
            {
                return;
            }

            parent._children.Remove(this);
            index = Mathf.Clamp(index, 0, parent._children.Count);
            parent._children.Insert(index, this);
        }

        public void SetAsFirstSibling() => SetSiblingIndex(0);

        public void SetAsLastSibling()
        {
            if (parent == null)
            {
                return;
            }

            parent._children.Remove(this);
            parent._children.Add(this);
        }

        public Transform Find(string path)
        {
            int slash = path.IndexOf('/');
            string head = slash < 0 ? path : path.Substring(0, slash);
            for (int i = 0; i < _children.Count; i++)
            {
                if (_children[i].name != head) continue;
                return slash < 0 ? _children[i] : _children[i].Find(path.Substring(slash + 1));
            }

            return null;
        }

        public void DetachChildren()
        {
            var copy = new List<Transform>(_children);
            for (int i = 0; i < copy.Count; i++) copy[i].SetParent(null, false);
        }

        public bool IsChildOf(Transform other)
        {
            Transform t = this;
            while (t != null)
            {
                if (t == other) return true;
                t = t.parent;
            }

            return false;
        }

        public void Translate(Vector3 translation) => localPosition += translation;

        public void Rotate(Vector3 euler) => localRotation = Quaternion.Euler(euler) * localRotation;

        internal IReadOnlyList<Transform> Children => _children;

        internal void DetachFromParent() => parent?._children.Remove(this);

        public IEnumerator GetEnumerator() => _children.GetEnumerator();
    }

    /// <summary>
    /// RectTransform do shim. anchor/pivot/sizeDelta/offset seguem exatamente a álgebra da Unity;
    /// <see cref="rect"/> é resolvido a partir do pai, com a raiz do canvas medindo
    /// <see cref="UnityRuntime.CanvasSize"/> (1080×1920).
    /// </summary>
    public sealed class RectTransform : Transform
    {
        public Vector2 anchorMin { get; set; } = new Vector2(0.5f, 0.5f);

        public Vector2 anchorMax { get; set; } = new Vector2(0.5f, 0.5f);

        public Vector2 pivot { get; set; } = new Vector2(0.5f, 0.5f);

        public Vector2 sizeDelta { get; set; } = new Vector2(100f, 100f);

        public Vector2 anchoredPosition { get; set; } = Vector2.zero;

        public Vector3 anchoredPosition3D
        {
            get => new Vector3(anchoredPosition.x, anchoredPosition.y, localPosition.z);
            set
            {
                anchoredPosition = new Vector2(value.x, value.y);
                localPosition = new Vector3(localPosition.x, localPosition.y, value.z);
            }
        }

        public Vector2 offsetMin
        {
            get => anchoredPosition - Vector2.Scale(sizeDelta, pivot);
            set
            {
                Vector2 offset = value - (anchoredPosition - Vector2.Scale(sizeDelta, pivot));
                sizeDelta -= offset;
                anchoredPosition += Vector2.Scale(offset, Vector2.one - pivot);
            }
        }

        public Vector2 offsetMax
        {
            get => anchoredPosition + Vector2.Scale(sizeDelta, Vector2.one - pivot);
            set
            {
                Vector2 offset = value - (anchoredPosition + Vector2.Scale(sizeDelta, Vector2.one - pivot));
                sizeDelta += offset;
                anchoredPosition += Vector2.Scale(offset, pivot);
            }
        }

        /// <summary>Tamanho resolvido do retângulo, em coordenadas locais (origem no pivot).</summary>
        public Rect rect
        {
            get
            {
                Vector2 size = ResolvedSize();
                return new Rect(-pivot.x * size.x, -pivot.y * size.y, size.x, size.y);
            }
        }

        public void SetSizeWithCurrentAnchors(Axis axis, float size)
        {
            Vector2 parentSize = ParentSize();
            Vector2 delta = sizeDelta;
            if (axis == Axis.Horizontal)
            {
                delta.x = size - parentSize.x * (anchorMax.x - anchorMin.x);
            }
            else
            {
                delta.y = size - parentSize.y * (anchorMax.y - anchorMin.y);
            }

            sizeDelta = delta;
        }

        public void SetInsetAndSizeFromParentEdge(Edge edge, float inset, float size)
        {
            bool horizontal = edge == Edge.Left || edge == Edge.Right;
            float anchor = edge == Edge.Left || edge == Edge.Top ? 0f : 1f;
            if (edge == Edge.Top) anchor = 1f;
            if (edge == Edge.Bottom) anchor = 0f;

            Vector2 min = anchorMin;
            Vector2 max = anchorMax;
            if (horizontal)
            {
                min.x = max.x = edge == Edge.Left ? 0f : 1f;
            }
            else
            {
                min.y = max.y = anchor;
            }

            anchorMin = min;
            anchorMax = max;
            SetSizeWithCurrentAnchors(horizontal ? Axis.Horizontal : Axis.Vertical, size);

            Vector2 pos = anchoredPosition;
            float sign = edge == Edge.Left || edge == Edge.Bottom ? 1f : -1f;
            float value = sign * (inset + size * (edge == Edge.Left || edge == Edge.Bottom ? pivot.x : 1f - pivot.x));
            if (horizontal) pos.x = value;
            else pos.y = sign * (inset + size * (edge == Edge.Bottom ? pivot.y : 1f - pivot.y));
            anchoredPosition = pos;
        }

        public void GetWorldCorners(Vector3[] fourCorners)
        {
            Rect r = rect;
            Vector3 p = position;
            fourCorners[0] = new Vector3(p.x + r.xMin, p.y + r.yMin, 0f);
            fourCorners[1] = new Vector3(p.x + r.xMin, p.y + r.yMax, 0f);
            fourCorners[2] = new Vector3(p.x + r.xMax, p.y + r.yMax, 0f);
            fourCorners[3] = new Vector3(p.x + r.xMax, p.y + r.yMin, 0f);
        }

        public void ForceUpdateRectTransforms()
        {
        }

        public enum Axis
        {
            Horizontal = 0,
            Vertical = 1,
        }

        public enum Edge
        {
            Left = 0,
            Right = 1,
            Top = 2,
            Bottom = 3,
        }

        internal Vector2 ResolvedSize()
        {
            if (IsCanvasRoot())
            {
                return UnityRuntime.CanvasSize;
            }

            Vector2 parentSize = ParentSize();
            return new Vector2(
                parentSize.x * (anchorMax.x - anchorMin.x) + sizeDelta.x,
                parentSize.y * (anchorMax.y - anchorMin.y) + sizeDelta.y);
        }

        private bool IsCanvasRoot()
        {
            // O RectTransform de um Canvas raiz é dimensionado pela TELA, não pelo pai — mesmo
            // quando o objeto está pendurado num GameObject qualquer (o caso do GameApp).
            if (gameObject == null || gameObject.GetComponent<Canvas>() == null)
            {
                return false;
            }

            Transform t = parent;
            while (t != null)
            {
                if (t.gameObject.GetComponent<Canvas>() != null) return false;
                t = t.parent;
            }

            return true;
        }

        private Vector2 ParentSize()
        {
            return parent is RectTransform p ? p.ResolvedSize() : UnityRuntime.CanvasSize;
        }
    }

    // ---------------------------------------------------------------- atributos

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SerializeFieldAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class SerializeReferenceAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class RequireComponent : Attribute
    {
        public RequireComponent(Type requiredComponent)
        {
        }

        public RequireComponent(Type requiredComponent, Type requiredComponent2)
        {
        }

        public RequireComponent(Type requiredComponent, Type requiredComponent2, Type requiredComponent3)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class DisallowMultipleComponent : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class AddComponentMenu : Attribute
    {
        public AddComponentMenu(string menuName)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ExecuteAlways : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class RangeAttribute : Attribute
    {
        public RangeAttribute(float min, float max)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class HeaderAttribute : Attribute
    {
        public HeaderAttribute(string header)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TooltipAttribute : Attribute
    {
        public TooltipAttribute(string tooltip)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SpaceAttribute : Attribute
    {
        public SpaceAttribute()
        {
        }

        public SpaceAttribute(float height)
        {
        }
    }

    public enum RuntimeInitializeLoadType
    {
        AfterSceneLoad = 0,
        BeforeSceneLoad = 1,
        BeforeSplashScreen = 2,
        AfterAssembliesLoaded = 3,
        SubsystemRegistration = 4,
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute()
        {
            loadType = RuntimeInitializeLoadType.AfterSceneLoad;
        }

        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType loadType)
        {
            this.loadType = loadType;
        }

        public RuntimeInitializeLoadType loadType { get; }
    }
}
