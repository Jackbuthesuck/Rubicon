using System.Collections.Generic;
using UnityEngine;

public sealed partial class ShootingTarget
{
    public override bool ApplyHit(Collider hitCollider, AmmoDefinition ammo)
    {
        if (IsDestroyed || hitCollider == null || ammo == null || !colliderParts.TryGetValue(hitCollider, out TargetPartState part))
        {
            return false;
        }

        float penetrationRatio = part.Definition.armor <= 0f
            ? 1f
            : Mathf.Clamp01((ammo.penetration + 1f) / (part.Definition.armor + 1f));
        bool armorBlocked = part.Definition.armor > 0f && ammo.penetration <= part.Definition.armor;
        if (!part.Definition.takesDamage)
        {
            FlashPart(part, armorBlocked ? Color.white : Color.red);
            return true;
        }

        float effectiveDamageMultiplier = Mathf.Max(0f, part.Definition.damageMultiplier);
        float impactDamage = Mathf.Max(0f, ammo.damage) * effectiveDamageMultiplier * penetrationRatio;
        bool partWasAlreadyDead = part.CurrentHealth <= 0f;
        List<DamageChainHit> chainHits = new List<DamageChainHit>();
        float damage = ApplyDamage(part, impactDamage);
        damage += ApplyCriticalDamage(part, impactDamage, chainHits);
        if (partWasAlreadyDead)
        {
            damage += SpreadBlackedPartDamage(part, impactDamage, new HashSet<TargetPartState>(), chainHits);
        }

        Color dimRed = new Color(0.45f, 0f, 0f, 1f);
        Color flashColor = part.CurrentHealth <= 0f
            ? armorBlocked ? Color.gray : dimRed
            : armorBlocked ? Color.white : Color.red;
        FlashPart(part, flashColor);

        Debug.Log(
            $"{definition.displayName} hit {part.Definition.displayName}: {damage:0.##} damage, part {part.CurrentHealth:0.##}/{part.Definition.health}, target {totalCurrentHealth:0.##}/{definition.totalHealth}.",
            this);
        if (IsDestroyed && respawnRoutine == null)
        {
            totalCurrentHealth = 0f;
            SetTargetVisible(false);
            respawnRoutine = StartCoroutine(RespawnAfterDelay());
        }

        SyncPartHealthDisplay();
        UpdateHealthBarFill();
        UpdateHealthBar();
        ShakeHealthBar(impactDamage);
        healthBarView?.ShowChainDamage(BuildDamageChain(part));
        return true;
    }

    private float ApplyDamage(TargetPartState part, float damage)
    {
        float incomingDamage = Mathf.Max(0f, damage);
        bool partWasAlreadyBroken = part.CurrentHealth <= 0f;
        float appliedPartDamage = Mathf.Min(incomingDamage, part.CurrentHealth);
        part.CurrentHealth -= appliedPartDamage;
        float healthPoolDamage = part.Definition.lethalPart
            ? incomingDamage
                * Mathf.Max(0f, part.Definition.lethalDamageMultiplier)
                * (partWasAlreadyBroken ? Mathf.Max(0f, part.Definition.blackedLethalDamageMultiplier) : 1f)
            : appliedPartDamage;
        totalCurrentHealth = Mathf.Max(0f, totalCurrentHealth - healthPoolDamage);
        return healthPoolDamage;
    }

    private float ApplyCriticalDamage(TargetPartState sourcePart, float impactDamage, List<DamageChainHit> chainHits)
    {
        if (sourcePart.Definition.lethalPart || sourcePart.Definition.criticalDamageMultiplier <= 0f)
        {
            return 0f;
        }

        if (!partsById.TryGetValue(Normalize(sourcePart.Definition.criticalDamageTarget), out TargetPartState criticalPart)
            || criticalPart == sourcePart
            || (!criticalPart.Definition.lethalPart && criticalPart.CurrentHealth <= 0f))
        {
            return 0f;
        }

        return ApplyDamageThroughChain(
            criticalPart,
            Mathf.Max(0f, impactDamage) * sourcePart.Definition.criticalDamageMultiplier,
            new HashSet<TargetPartState>(),
            chainHits);
    }

    private float ApplyDamageThroughChain(
        TargetPartState part,
        float damage,
        HashSet<TargetPartState> visitedParts,
        List<DamageChainHit> chainHits)
    {
        bool partWasAlreadyBroken = part.CurrentHealth <= 0f;
        float appliedDamage = ApplyDamage(part, damage);
        chainHits.Add(new DamageChainHit
        {
            DisplayName = part.Definition.displayName,
            Damage = Mathf.Max(0f, damage),
            MaximumHealth = Mathf.Max(0f, part.Definition.health),
            RemainingHealth = Mathf.Max(0f, part.CurrentHealth)
        });
        if (partWasAlreadyBroken)
        {
            appliedDamage += SpreadBlackedPartDamage(part, damage, visitedParts, chainHits);
        }

        return appliedDamage;
    }

    private float SpreadBlackedPartDamage(
        TargetPartState sourcePart,
        float impactDamage,
        HashSet<TargetPartState> visitedParts,
        List<DamageChainHit> chainHits)
    {
        if (!visitedParts.Add(sourcePart))
        {
            return 0f;
        }

        float spreadDamage = Mathf.Max(0f, impactDamage)
            * Mathf.Max(0f, sourcePart.Definition.blackedPartDamageMultiplier)
            * Mathf.Max(0f, sourcePart.Definition.blackedPartSpreadMultiplier);
        List<TargetPartState> spreadTargets = new List<TargetPartState>();
        foreach (string targetId in sourcePart.Definition.blackedPartSpreadTargets)
        {
            if (partsById.TryGetValue(Normalize(targetId), out TargetPartState targetPart)
                && !visitedParts.Contains(targetPart)
                && !spreadTargets.Contains(targetPart))
            {
                spreadTargets.Add(targetPart);
            }
        }

        if (spreadTargets.Count == 0)
        {
            return 0f;
        }

        float damagePerPart = spreadDamage / spreadTargets.Count;
        float appliedDamage = 0f;
        foreach (TargetPartState targetPart in spreadTargets)
        {
            appliedDamage += ApplyDamageThroughChain(targetPart, damagePerPart, visitedParts, chainHits);
        }

        return appliedDamage;
    }

    private List<DamageChainHit> BuildDamageChain(TargetPartState sourcePart)
    {
        List<DamageChainHit> chain = new List<DamageChainHit>();
        AppendDamageChain(sourcePart, new HashSet<TargetPartState>(), chain);
        return chain;
    }

    private void AppendDamageChain(
        TargetPartState part,
        HashSet<TargetPartState> visitedParts,
        List<DamageChainHit> chain)
    {
        if (part == null || !visitedParts.Add(part))
        {
            return;
        }

        chain.Add(new DamageChainHit
        {
            DisplayName = part.Definition.displayName,
            Damage = 0f,
            MaximumHealth = Mathf.Max(0f, part.Definition.health),
            RemainingHealth = Mathf.Max(0f, part.CurrentHealth)
        });

        if (part.Definition.lethalPart)
        {
            return;
        }

        List<TargetPartState> nextParts = new List<TargetPartState>();
        foreach (string targetId in part.Definition.blackedPartSpreadTargets)
        {
            AddChainTarget(targetId, part, nextParts, visitedParts);
        }

        if (nextParts.Count == 0)
        {
            AddChainTarget(part.Definition.criticalDamageTarget, part, nextParts, visitedParts);
        }

        foreach (TargetPartState nextPart in nextParts)
        {
            AppendDamageChain(nextPart, visitedParts, chain);
        }
    }

    private void AddChainTarget(
        string targetId,
        TargetPartState sourcePart,
        List<TargetPartState> nextParts,
        HashSet<TargetPartState> visitedParts)
    {
        if (string.IsNullOrWhiteSpace(targetId)
            || !partsById.TryGetValue(Normalize(targetId), out TargetPartState targetPart)
            || targetPart == sourcePart
            || targetPart.Definition.takesDamage == false
            || visitedParts.Contains(targetPart)
            || nextParts.Contains(targetPart))
        {
            return;
        }

        nextParts.Add(targetPart);
    }
}
