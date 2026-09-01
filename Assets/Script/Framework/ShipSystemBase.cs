using Unity.Netcode;
using UnityEngine;

namespace PressureExpress.Framework
{
    public abstract class ShipSystemBase : NetworkBehaviour
    {
        [Header("Subsystem Configuration")]
        [SerializeField] protected float maxResourceValue = 100f;
        [SerializeField] protected NetworkVariable<float> currentResourceValue = new NetworkVariable<float>(100f);

        public event System.Action OnResourceChanged;

        public virtual float CurrentValue => currentResourceValue.Value;
        public virtual float MaxValue => maxResourceValue;
        public float NormalizedValue => maxResourceValue > 0f ? Mathf.Clamp01(currentResourceValue.Value / maxResourceValue) : 0f;

        protected virtual void Awake()
        {
            InitializeService();
        }

        protected virtual void InitializeService()
        {

        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            currentResourceValue.OnValueChanged += HandleValueNetChanged;
            if (IsServer)
            {
                OnServerInitialize();
            }
        }

        public override void OnNetworkDespawn()
        {
            currentResourceValue.OnValueChanged -= HandleValueNetChanged;
            base.OnNetworkDespawn();
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
        }

        protected virtual void OnServerInitialize()
        {
            currentResourceValue.Value = maxResourceValue;
        }

        private void HandleValueNetChanged(float previousValue, float newValue)
        {
            OnResourceChanged?.Invoke();
            OnValueChanged(previousValue, newValue);
        }

        protected virtual void OnValueChanged(float previousValue, float newValue) { }

        public virtual void Consume(float amount)
        {
            if (!IsServer || amount <= 0f) return;
            currentResourceValue.Value = Mathf.Clamp(currentResourceValue.Value - amount, 0f, maxResourceValue);
        }

        public virtual void Add(float amount)
        {
            if (!IsServer || amount <= 0f) return;
            currentResourceValue.Value = Mathf.Clamp(currentResourceValue.Value + amount, 0f, maxResourceValue);
        }

        public virtual bool TryConsume(float amount)
        {
            if (!IsServer) return false;
            if (currentResourceValue.Value < amount) return false;
            Consume(amount);
            return true;
        }
    }
}
