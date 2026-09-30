// Shim de UnityEngine.Events — UnityEvent com AddListener/RemoveListener/Invoke.
using System;
using System.Collections.Generic;

namespace UnityEngine.Events
{
    public delegate void UnityAction();

    public delegate void UnityAction<T0>(T0 arg0);

    public delegate void UnityAction<T0, T1>(T0 arg0, T1 arg1);

    public abstract class UnityEventBase
    {
        public abstract int GetPersistentEventCount();

        public abstract void RemoveAllListeners();
    }

    public class UnityEvent : UnityEventBase
    {
        private readonly List<UnityAction> _listeners = new List<UnityAction>();

        public void AddListener(UnityAction call)
        {
            if (call != null) _listeners.Add(call);
        }

        public void RemoveListener(UnityAction call) => _listeners.Remove(call);

        public override void RemoveAllListeners() => _listeners.Clear();

        public override int GetPersistentEventCount() => _listeners.Count;

        public void Invoke()
        {
            // Cópia: um ouvinte pode trocar de tela e mexer na lista.
            var snapshot = _listeners.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                snapshot[i]();
            }
        }
    }

    public class UnityEvent<T0> : UnityEventBase
    {
        private readonly List<UnityAction<T0>> _listeners = new List<UnityAction<T0>>();

        public void AddListener(UnityAction<T0> call)
        {
            if (call != null) _listeners.Add(call);
        }

        public void RemoveListener(UnityAction<T0> call) => _listeners.Remove(call);

        public override void RemoveAllListeners() => _listeners.Clear();

        public override int GetPersistentEventCount() => _listeners.Count;

        public void Invoke(T0 arg0)
        {
            var snapshot = _listeners.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                snapshot[i](arg0);
            }
        }
    }
}
