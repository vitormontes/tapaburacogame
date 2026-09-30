// Shim de UnityEngine — o "player": ciclo de vida, corrotinas e tempo virtual.
// É este arquivo que faz o jogo RODAR fora do editor: nenhum método de MonoBehaviour é
// chamado por mágica, tudo aqui é reflexão explícita (inclusive métodos privados).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace UnityEngine
{
    public class YieldInstruction
    {
    }

    public sealed class WaitForSeconds : YieldInstruction
    {
        public WaitForSeconds(float seconds)
        {
            Seconds = seconds;
        }

        internal float Seconds { get; }
    }

    public sealed class WaitForSecondsRealtime : YieldInstruction
    {
        public WaitForSecondsRealtime(float seconds)
        {
            Seconds = seconds;
        }

        internal float Seconds { get; }
    }

    public sealed class WaitForEndOfFrame : YieldInstruction
    {
    }

    public sealed class WaitForFixedUpdate : YieldInstruction
    {
    }

    public abstract class CustomYieldInstruction : IEnumerator
    {
        public abstract bool keepWaiting { get; }

        public object Current => null;

        public bool MoveNext() => keepWaiting;

        public void Reset()
        {
        }
    }

    public sealed class WaitUntil : CustomYieldInstruction
    {
        private readonly Func<bool> _predicate;

        public WaitUntil(Func<bool> predicate)
        {
            _predicate = predicate;
        }

        public override bool keepWaiting => !_predicate();
    }

    public sealed class WaitWhile : CustomYieldInstruction
    {
        private readonly Func<bool> _predicate;

        public WaitWhile(Func<bool> predicate)
        {
            _predicate = predicate;
        }

        public override bool keepWaiting => _predicate();
    }

    /// <summary>Alça de corrotina — devolvida por StartCoroutine.</summary>
    public class Coroutine : YieldInstruction
    {
        internal MonoBehaviour Owner;
        internal Stack<IEnumerator> Stack = new Stack<IEnumerator>();
        internal IEnumerator Root;
        internal float ResumeAtTime = float.NegativeInfinity;
        internal int ResumeAtFrame = -1;
        internal Coroutine WaitingFor;
        internal bool Finished;
        internal bool Cancelled;
    }

    /// <summary>
    /// Motor do shim. O harness chama <see cref="Step"/> uma vez por quadro simulado.
    /// Ordem por quadro: Start pendentes → Update → corrotinas → LateUpdate → destruições.
    /// </summary>
    internal static class UnityRuntime
    {
        private const BindingFlags MessageFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static readonly List<GameObject> _gameObjects = new List<GameObject>();
        private static readonly List<Component> _components = new List<Component>();
        private static readonly List<Component> _pendingStart = new List<Component>();
        private static readonly HashSet<Component> _awoken = new HashSet<Component>();
        private static readonly HashSet<Component> _started = new HashSet<Component>();
        private static readonly HashSet<Component> _enabled = new HashSet<Component>();
        private static readonly List<Coroutine> _coroutines = new List<Coroutine>();
        private static readonly List<PendingInvoke> _invokes = new List<PendingInvoke>();
        private static readonly List<PendingDestroy> _destroys = new List<PendingDestroy>();
        private static readonly HashSet<Object> _persistent = new HashSet<Object>();
        private static readonly Dictionary<Type, MethodInfo[]> _messageCache = new Dictionary<Type, MethodInfo[]>();

        /// <summary>Tamanho do canvas em unidades de UI (resolução de referência do jogo).</summary>
        internal static Vector2 CanvasSize = new Vector2(1080f, 1920f);

        private struct PendingInvoke
        {
            public MonoBehaviour Target;
            public string Method;
            public float Time;
            public float Repeat;
        }

        private struct PendingDestroy
        {
            public Object Target;
            public float Time;
        }

        // ------------------------------------------------------------ registro

        internal static void RegisterGameObject(GameObject go) => _gameObjects.Add(go);

        internal static void RegisterComponent(Component component)
        {
            _components.Add(component);
            if (!component.gameObject.activeInHierarchy)
            {
                return;
            }

            Awake(component);
            if (IsEnabled(component))
            {
                Enable(component);
            }

            _pendingStart.Add(component);
        }

        private static bool IsEnabled(Component component) => !(component is Behaviour b) || b.enabled;

        private static void Awake(Component component)
        {
            if (!_awoken.Add(component))
            {
                return;
            }

            Send(component, "Awake");
        }

        private static void Enable(Component component)
        {
            if (!_enabled.Add(component))
            {
                return;
            }

            Send(component, "OnEnable");
        }

        private static void Disable(Component component)
        {
            if (!_enabled.Remove(component))
            {
                return;
            }

            Send(component, "OnDisable");
        }

        internal static void ToggleBehaviour(Behaviour behaviour, bool value)
        {
            if (value)
            {
                Awake(behaviour);
                Enable(behaviour);
                if (!_started.Contains(behaviour) && !_pendingStart.Contains(behaviour))
                {
                    _pendingStart.Add(behaviour);
                }
            }
            else
            {
                Disable(behaviour);
            }
        }

        /// <summary>SetActive/SetParent mudaram o estado efetivo: propaga para os filhos.</summary>
        internal static void PropagateActiveChange(GameObject go, bool active)
        {
            if (go.Destroyed)
            {
                return;
            }

            IReadOnlyList<Component> components = go.Components;
            for (int i = 0; i < components.Count; i++)
            {
                Component c = components[i];
                if (c is Transform)
                {
                    continue;
                }

                if (active)
                {
                    Awake(c);
                    if (IsEnabled(c))
                    {
                        Enable(c);
                        if (!_started.Contains(c) && !_pendingStart.Contains(c))
                        {
                            _pendingStart.Add(c);
                        }
                    }
                }
                else
                {
                    Disable(c);
                }
            }

            Transform t = go.transform;
            for (int i = 0; i < t.childCount; i++)
            {
                GameObject child = t.GetChild(i).gameObject;
                if (child.activeSelf)
                {
                    PropagateActiveChange(child, active);
                }
            }
        }

        // ------------------------------------------------------------ mensagens

        private static MethodInfo FindMessage(Type type, string message)
        {
            Type t = type;
            while (t != null && t != typeof(MonoBehaviour) && t != typeof(Component) && t != typeof(object))
            {
                MethodInfo m = t.GetMethod(message, MessageFlags, null, Type.EmptyTypes, null);
                if (m != null)
                {
                    return m;
                }

                t = t.BaseType;
            }

            return null;
        }

        private static void Send(Component component, string message)
        {
            MethodInfo method = FindMessage(component.GetType(), message);
            if (method == null)
            {
                return;
            }

            try
            {
                method.Invoke(component, null);
            }
            catch (TargetInvocationException e)
            {
                throw new Exception($"{component.GetType().Name}.{message} explodiu: {e.InnerException}", e.InnerException);
            }
        }

        // ------------------------------------------------------------ quadro

        /// <summary>Roda um quadro inteiro do jogo com o passo de tempo dado.</summary>
        internal static void Step(float deltaTime)
        {
            Time.Advance(deltaTime);

            FlushStarts();
            Dispatch("Update");
            TickCoroutines(deltaTime);
            TickInvokes(deltaTime);
            Dispatch("LateUpdate");
            FlushDestroys(deltaTime);
        }

        private static void FlushStarts()
        {
            if (_pendingStart.Count == 0)
            {
                return;
            }

            var batch = new List<Component>(_pendingStart);
            _pendingStart.Clear();
            for (int i = 0; i < batch.Count; i++)
            {
                Component c = batch[i];
                if (c.Destroyed || !c.gameObject.activeInHierarchy || !IsEnabled(c))
                {
                    continue;
                }

                if (_started.Add(c))
                {
                    Send(c, "Start");
                }
            }
        }

        private static void Dispatch(string message)
        {
            // Cópia: o Update pode criar/destruir componentes.
            var snapshot = new List<Component>(_components);
            for (int i = 0; i < snapshot.Count; i++)
            {
                Component c = snapshot[i];
                if (c.Destroyed || !c.gameObject.activeInHierarchy || !IsEnabled(c))
                {
                    continue;
                }

                Send(c, message);
            }
        }

        // ------------------------------------------------------------ corrotinas

        internal static Coroutine StartCoroutine(MonoBehaviour owner, IEnumerator routine)
        {
            if (routine == null)
            {
                throw new ArgumentNullException(nameof(routine));
            }

            var co = new Coroutine { Owner = owner, Root = routine };
            co.Stack.Push(routine);
            _coroutines.Add(co);

            // A Unity roda o corpo até o primeiro yield ainda dentro do StartCoroutine.
            Advance(co);
            return co;
        }

        internal static void StopCoroutine(Coroutine routine)
        {
            if (routine == null)
            {
                return;
            }

            routine.Cancelled = true;
            routine.Finished = true;
        }

        internal static void StopCoroutine(MonoBehaviour owner, IEnumerator routine)
        {
            for (int i = 0; i < _coroutines.Count; i++)
            {
                if (_coroutines[i].Owner == owner && ReferenceEquals(_coroutines[i].Root, routine))
                {
                    StopCoroutine(_coroutines[i]);
                }
            }
        }

        internal static void StopAllCoroutines(MonoBehaviour owner)
        {
            for (int i = 0; i < _coroutines.Count; i++)
            {
                if (_coroutines[i].Owner == owner)
                {
                    StopCoroutine(_coroutines[i]);
                }
            }
        }

        private static void TickCoroutines(float deltaTime)
        {
            var snapshot = new List<Coroutine>(_coroutines);
            for (int i = 0; i < snapshot.Count; i++)
            {
                Coroutine co = snapshot[i];
                if (co.Finished)
                {
                    continue;
                }

                if (co.Owner == null || co.Owner.Destroyed || !co.Owner.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (co.WaitingFor != null)
                {
                    if (!co.WaitingFor.Finished) continue;
                    co.WaitingFor = null;
                }

                if (Time.unscaledTime < co.ResumeAtTime)
                {
                    continue;
                }

                if (Time.frameCount <= co.ResumeAtFrame)
                {
                    continue;
                }

                Advance(co);
            }

            _coroutines.RemoveAll(c => c.Finished);
        }

        private static void Advance(Coroutine co)
        {
            while (true)
            {
                if (co.Cancelled || co.Stack.Count == 0)
                {
                    co.Finished = true;
                    return;
                }

                IEnumerator top = co.Stack.Peek();
                bool moved;
                try
                {
                    moved = top.MoveNext();
                }
                catch (Exception e)
                {
                    throw new Exception($"Corrotina de {co.Owner?.GetType().Name} explodiu: {e}", e);
                }

                if (!moved)
                {
                    co.Stack.Pop();
                    if (co.Stack.Count == 0)
                    {
                        co.Finished = true;
                        return;
                    }

                    // O chamador continua no MESMO quadro só depois do próximo tick,
                    // igual à Unity (o yield return enumerator custa um quadro por nível).
                    co.ResumeAtFrame = Time.frameCount;
                    return;
                }

                object yielded = top.Current;
                switch (yielded)
                {
                    case null:
                        co.ResumeAtFrame = Time.frameCount;
                        return;
                    case WaitForSeconds wait:
                        co.ResumeAtTime = Time.unscaledTime + wait.Seconds;
                        co.ResumeAtFrame = Time.frameCount;
                        return;
                    case WaitForSecondsRealtime waitReal:
                        co.ResumeAtTime = Time.unscaledTime + waitReal.Seconds;
                        co.ResumeAtFrame = Time.frameCount;
                        return;
                    case Coroutine other:
                        co.WaitingFor = other;
                        co.ResumeAtFrame = Time.frameCount;
                        return;
                    case CustomYieldInstruction custom:
                        if (custom.keepWaiting)
                        {
                            co.Stack.Push(custom);
                            co.ResumeAtFrame = Time.frameCount;
                            return;
                        }

                        co.ResumeAtFrame = Time.frameCount;
                        return;
                    case IEnumerator nested:
                        co.Stack.Push(nested);
                        continue;
                    default:
                        co.ResumeAtFrame = Time.frameCount;
                        return;
                }
            }
        }

        internal static int ActiveCoroutines
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _coroutines.Count; i++)
                {
                    if (!_coroutines[i].Finished) n++;
                }

                return n;
            }
        }

        // ------------------------------------------------------------ Invoke

        internal static void Invoke(MonoBehaviour target, string methodName, float time)
            => _invokes.Add(new PendingInvoke { Target = target, Method = methodName, Time = time, Repeat = -1f });

        internal static void InvokeRepeating(MonoBehaviour target, string methodName, float time, float repeatRate)
            => _invokes.Add(new PendingInvoke { Target = target, Method = methodName, Time = time, Repeat = repeatRate });

        internal static void CancelInvoke(MonoBehaviour target) => _invokes.RemoveAll(i => i.Target == target);

        internal static void CancelInvoke(MonoBehaviour target, string methodName)
            => _invokes.RemoveAll(i => i.Target == target && i.Method == methodName);

        internal static bool IsInvoking(MonoBehaviour target) => _invokes.Exists(i => i.Target == target);

        internal static Camera FindFirstCamera()
        {
            for (int i = 0; i < _components.Count; i++)
            {
                if (_components[i] is Camera cam && !cam.Destroyed && cam.gameObject.activeInHierarchy
                    && cam.gameObject.CompareTag("MainCamera"))
                {
                    return cam;
                }
            }

            return null;
        }

        private static void TickInvokes(float deltaTime)
        {
            for (int i = _invokes.Count - 1; i >= 0; i--)
            {
                PendingInvoke pending = _invokes[i];
                pending.Time -= deltaTime;
                if (pending.Time > 0f)
                {
                    _invokes[i] = pending;
                    continue;
                }

                Send(pending.Target, pending.Method);
                if (pending.Repeat > 0f)
                {
                    pending.Time = pending.Repeat;
                    _invokes[i] = pending;
                }
                else
                {
                    _invokes.RemoveAt(i);
                }
            }
        }

        // ------------------------------------------------------------ destruição

        internal static void MarkPersistent(Object target) => _persistent.Add(target);

        internal static void ScheduleDestroy(Object target) => ScheduleDestroy(target, 0f);

        internal static void ScheduleDestroy(Object target, float delay)
        {
            if (target == null)
            {
                return;
            }

            _destroys.Add(new PendingDestroy { Target = target, Time = delay });
        }

        private static void FlushDestroys(float deltaTime)
        {
            for (int i = _destroys.Count - 1; i >= 0; i--)
            {
                PendingDestroy pending = _destroys[i];
                pending.Time -= deltaTime;
                if (pending.Time > 0f)
                {
                    _destroys[i] = pending;
                    continue;
                }

                _destroys.RemoveAt(i);
                DestroyNow(pending.Target);
            }
        }

        internal static void DestroyNow(Object target)
        {
            if (ReferenceEquals(target, null) || target.Destroyed)
            {
                return;
            }

            if (target is GameObject go)
            {
                Transform t = go.transform;
                var children = new List<Transform>(t.Children);
                for (int i = 0; i < children.Count; i++)
                {
                    DestroyNow(children[i].gameObject);
                }

                var components = new List<Component>(go.Components);
                for (int i = 0; i < components.Count; i++)
                {
                    DestroyComponent(components[i]);
                }

                t.DetachFromParent();
                go.Destroyed = true;
                _gameObjects.Remove(go);
                return;
            }

            if (target is Component component)
            {
                DestroyComponent(component);
                component.gameObject?.RemoveComponent(component);
            }
        }

        private static void DestroyComponent(Component component)
        {
            if (component.Destroyed)
            {
                return;
            }

            Disable(component);
            Send(component, "OnDestroy");
            component.Destroyed = true;
            _components.Remove(component);
            _awoken.Remove(component);
            _started.Remove(component);
            _pendingStart.Remove(component);
            if (component is MonoBehaviour mono)
            {
                StopAllCoroutines(mono);
            }
        }

        // ------------------------------------------------------------ buscas

        internal static T FindFirst<T>() where T : Object
        {
            for (int i = 0; i < _components.Count; i++)
            {
                if (!_components[i].Destroyed && _components[i] is T match && _components[i].gameObject.activeInHierarchy)
                {
                    return match;
                }
            }

            for (int i = 0; i < _gameObjects.Count; i++)
            {
                if (!_gameObjects[i].Destroyed && _gameObjects[i] is T match) return match;
            }

            return null;
        }

        internal static T[] FindAll<T>() where T : Object
        {
            var found = new List<T>();
            for (int i = 0; i < _components.Count; i++)
            {
                if (!_components[i].Destroyed && _components[i] is T match) found.Add(match);
            }

            for (int i = 0; i < _gameObjects.Count; i++)
            {
                if (!_gameObjects[i].Destroyed && _gameObjects[i] is T match) found.Add(match);
            }

            return found.ToArray();
        }

        internal static GameObject FindGameObject(string search)
        {
            for (int i = 0; i < _gameObjects.Count; i++)
            {
                if (!_gameObjects[i].Destroyed && _gameObjects[i].name == search && _gameObjects[i].activeInHierarchy)
                {
                    return _gameObjects[i];
                }
            }

            return null;
        }

        internal static GameObject FindGameObjectWithTag(string searchTag)
        {
            for (int i = 0; i < _gameObjects.Count; i++)
            {
                if (!_gameObjects[i].Destroyed && _gameObjects[i].tag == searchTag && _gameObjects[i].activeInHierarchy)
                {
                    return _gameObjects[i];
                }
            }

            return null;
        }

        /// <summary>Todas as raízes da "cena" — usado pelo harness para varrer a hierarquia.</summary>
        internal static IEnumerable<GameObject> Roots()
        {
            for (int i = 0; i < _gameObjects.Count; i++)
            {
                GameObject go = _gameObjects[i];
                if (!go.Destroyed && go.transform.parent == null)
                {
                    yield return go;
                }
            }
        }
    }
}
