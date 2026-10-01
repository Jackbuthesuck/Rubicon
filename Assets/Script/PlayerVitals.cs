using System;
using UnityEngine;

public sealed class PlayerVitals : MonoBehaviour
{
    [Header("Health")]
    [SerializeField, Min(1f)] private float maximumHealth = 100f;
    [SerializeField, Min(0f)] private float startingHealth = 100f;

    [Header("Stamina")]
    [SerializeField, Min(1f)] private float maximumStamina = 100f;
    [SerializeField, Min(0f)] private float startingStamina = 100f;
    [SerializeField, Min(0f)] private float staminaRegeneration = 20f;
    [SerializeField, Min(0f)] private float regenerationDelay = 0.75f;

    public event Action<float, float> HealthChanged;
    public event Action<float, float> StaminaChanged;

    public float MaximumHealth => maximumHealth;
    public float Health { get; private set; }
    public float MaximumStamina => maximumStamina;
    public float Stamina { get; private set; }
    public bool IsDead => Health <= 0f;

    private float lastStaminaUseTime;

    private void Awake()
    {
        Health = Mathf.Clamp(startingHealth, 0f, maximumHealth);
        Stamina = Mathf.Clamp(startingStamina, 0f, maximumStamina);
    }

    private void Update()
    {
        if (Time.time - lastStaminaUseTime < regenerationDelay || Stamina >= maximumStamina)
        {
            return;
        }

        SetStamina(Stamina + staminaRegeneration * Time.deltaTime);
    }

    public void Damage(float amount)
    {
        if (amount <= 0f || IsDead)
        {
            return;
        }

        SetHealth(Health - amount);
    }

    public void Heal(float amount)
    {
        if (amount <= 0f || IsDead)
        {
            return;
        }

        SetHealth(Health + amount);
    }

    public bool TrySpendStamina(float amount)
    {
        if (amount <= 0f)
        {
            return true;
        }

        if (Stamina < amount)
        {
            return false;
        }

        lastStaminaUseTime = Time.time;
        SetStamina(Stamina - amount);
        return true;
    }

    public void RestoreStamina(float amount)
    {
        if (amount > 0f)
        {
            SetStamina(Stamina + amount);
        }
    }

    private void SetHealth(float value)
    {
        float previousHealth = Health;
        Health = Mathf.Clamp(value, 0f, maximumHealth);
        if (!Mathf.Approximately(previousHealth, Health))
        {
            HealthChanged?.Invoke(Health, maximumHealth);
        }
    }

    private void SetStamina(float value)
    {
        float previousStamina = Stamina;
        Stamina = Mathf.Clamp(value, 0f, maximumStamina);
        if (!Mathf.Approximately(previousStamina, Stamina))
        {
            StaminaChanged?.Invoke(Stamina, maximumStamina);
        }
    }
}