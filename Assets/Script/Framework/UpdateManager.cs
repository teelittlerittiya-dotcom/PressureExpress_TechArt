using System.Collections.Generic;
using UnityEngine;

namespace PressureExpress.Framework
{
    public interface IUpdateable
    {
        void OnUpdate();
    }

    public interface IFixedUpdateable
    {
        void OnFixedUpdate();
    }

    public interface ILateUpdateable
    {
        void OnLateUpdate();
    }

    public class UpdateManager : MonoBehaviour
    {
        private static UpdateManager instance;
        public static UpdateManager Instance => instance;

        private readonly List<IUpdateable> updateables = new List<IUpdateable>(64);
        private readonly List<IFixedUpdateable> fixedUpdateables = new List<IFixedUpdateable>(64);
        private readonly List<ILateUpdateable> lateUpdateables = new List<ILateUpdateable>(64);

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                ServiceLocator.Register(this);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                ServiceLocator.Unregister<UpdateManager>(this);
                instance = null;
            }
        }

        public void RegisterUpdateable(IUpdateable updateable)
        {
            if (updateable != null && !updateables.Contains(updateable))
            {
                updateables.Add(updateable);
            }
        }

        public void UnregisterUpdateable(IUpdateable updateable)
        {
            if (updateable != null)
            {
                updateables.Remove(updateable);
            }
        }

        public void RegisterFixedUpdateable(IFixedUpdateable fixedUpdateable)
        {
            if (fixedUpdateable != null && !fixedUpdateables.Contains(fixedUpdateable))
            {
                fixedUpdateables.Add(fixedUpdateable);
            }
        }

        public void UnregisterFixedUpdateable(IFixedUpdateable fixedUpdateable)
        {
            if (fixedUpdateable != null)
            {
                fixedUpdateables.Remove(fixedUpdateable);
            }
        }

        public void RegisterLateUpdateable(ILateUpdateable lateUpdateable)
        {
            if (lateUpdateable != null && !lateUpdateables.Contains(lateUpdateable))
            {
                lateUpdateables.Add(lateUpdateable);
            }
        }

        public void UnregisterLateUpdateable(ILateUpdateable lateUpdateable)
        {
            if (lateUpdateable != null)
            {
                lateUpdateables.Remove(lateUpdateable);
            }
        }

        private void Update()
        {
            for (int i = updateables.Count - 1; i >= 0; i--)
            {
                var item = updateables[i];
                if (item != null)
                {
                    item.OnUpdate();
                }
                else
                {
                    updateables.RemoveAt(i);
                }
            }
        }

        private void FixedUpdate()
        {
            for (int i = fixedUpdateables.Count - 1; i >= 0; i--)
            {
                var item = fixedUpdateables[i];
                if (item != null)
                {
                    item.OnFixedUpdate();
                }
                else
                {
                    fixedUpdateables.RemoveAt(i);
                }
            }
        }

        private void LateUpdate()
        {
            for (int i = lateUpdateables.Count - 1; i >= 0; i--)
            {
                var item = lateUpdateables[i];
                if (item != null)
                {
                    item.OnLateUpdate();
                }
                else
                {
                    lateUpdateables.RemoveAt(i);
                }
            }
        }
    }
}
