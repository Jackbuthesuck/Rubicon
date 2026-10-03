using System;

public sealed class DamageChainHit
{
    public string DisplayName;
    public float Damage;
    public float MaximumHealth;
    public float RemainingHealth;
}

[Serializable]
public sealed class DamageablePartHealthDisplay
{
    public string id;
    public string displayName;
    public float currentHealth;
    public float maximumHealth;
    public bool broken;
}

[Serializable]
public sealed class DamageablePartDefinition
{
    public string id = "plate";
    public string displayName = "Plate";
    public string[] colliderNames = Array.Empty<string>();
    public bool takesDamage = true;
    public float damageMultiplier = 1f;
    public string criticalDamageTarget = "thorax";
    public float criticalDamageMultiplier;
    public float blackedPartDamageMultiplier = 0.5f;
    public float blackedPartSpreadMultiplier = 1f;
    public string[] blackedPartSpreadTargets = Array.Empty<string>();
    public bool lethalPart;
    public float lethalDamageMultiplier = 1f;
    public float blackedLethalDamageMultiplier = 1f;
    public float health = 100f;
    public float armor;
}

[Serializable]
public sealed class DamageableDefinition
{
    public string id = "damageable";
    public string displayName = "Damageable";
    public float totalHealth = 100f;
    public float stamina = -1f;
    public DamageablePartDefinition[] parts = Array.Empty<DamageablePartDefinition>();
}
