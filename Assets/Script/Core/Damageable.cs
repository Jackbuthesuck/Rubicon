using UnityEngine;

public class Damageable : MonoBehaviour
{
    [SerializeField, Min(0f)] private float maximumHealth = 100f;
    [SerializeField, Min(0f)] private float startingHealth = 100f;

    [System.NonSerialized] private float baseCurrentHealth;

    public virtual float CurrentHealth => baseCurrentHealth;
    public virtual float MaximumHealth => maximumHealth;
    public virtual bool IsDestroyed => baseCurrentHealth <= 0f;

    protected virtual void Awake()
    {
        baseCurrentHealth = Mathf.Clamp(startingHealth, 0f, maximumHealth);
    }

    public virtual bool ApplyHit(Collider hitCollider, AmmoDefinition ammo)
    {
        if (IsDestroyed || ammo == null)
        {
            return false;
        }

        baseCurrentHealth = Mathf.Max(0f, baseCurrentHealth - Mathf.Max(0f, ammo.damage));
        return true;
    }

    public static Damageable FindInParent(Collider hitCollider)
    {
        return hitCollider != null ? hitCollider.GetComponentInParent<Damageable>() : null;
    }
}
