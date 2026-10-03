using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public sealed partial class ShootingTarget
{
    private DamageableDefinition LoadDefinition()
    {
        string jsonPath = Path.Combine(Application.streamingAssetsPath, "Targets", $"{targetId}.json");
        if (File.Exists(jsonPath))
        {
            try
            {
                DamageableDefinition loaded = JsonUtility.FromJson<DamageableDefinition>(File.ReadAllText(jsonPath));
                if (loaded != null && loaded.parts != null && loaded.parts.Length > 0)
                {
                    return loaded;
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"Could not load shooting target definition '{jsonPath}': {exception.Message}", this);
            }
        }

        if (fallbackDefinition != null)
        {
            DamageableDefinition fallback = JsonUtility.FromJson<DamageableDefinition>(fallbackDefinition.text);
            if (fallback != null && fallback.parts != null && fallback.parts.Length > 0)
            {
                return fallback;
            }
        }

        Debug.LogError($"No valid shooting target definition found for '{targetId}'.", this);
        return new DamageableDefinition
        {
            id = targetId,
            displayName = targetId,
            totalHealth = 0f,
            parts = Array.Empty<DamageablePartDefinition>()
        };
    }

    private void BindColliders()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        partsById.Clear();
        foreach (DamageablePartDefinition partDefinition in definition.parts)
        {
            if (partDefinition == null)
            {
                continue;
            }

            TargetPartState part = new TargetPartState
            {
                Definition = partDefinition,
                CurrentHealth = partDefinition.takesDamage ? Mathf.Max(0f, partDefinition.health) : 0f
            };
            parts.Add(part);
            partsById[Normalize(partDefinition.id)] = part;
            foreach (Collider collider in colliders)
            {
                if (collider != null && MatchesPart(collider.name, partDefinition))
                {
                    colliderParts[collider] = part;
                    AddRenderers(collider, part);
                }
            }
        }

        SyncPartHealthDisplay();

        if (colliderParts.Count == 0 && definition.parts.Length > 0)
        {
            Debug.LogError(
                $"No colliders matched any part of {definition.displayName}. Check colliderNames and imported object names.",
                this);
        }
    }

    private void AddRenderers(Collider collider, TargetPartState part)
    {
        foreach (Renderer renderer in collider.GetComponentsInChildren<Renderer>(true))
        {
            part.Renderers.Add(renderer);
        }
    }

    private void SyncPartHealthDisplay()
    {
        partHealth.Clear();
        foreach (TargetPartState part in parts)
        {
            partHealth.Add(new DamageablePartHealthDisplay
            {
                id = part.Definition.id,
                displayName = part.Definition.displayName,
                currentHealth = part.CurrentHealth,
                maximumHealth = Mathf.Max(0f, part.Definition.health),
                broken = part.CurrentHealth <= 0f
            });
        }
    }

    private static bool MatchesPart(string colliderName, DamageablePartDefinition part)
    {
        string normalizedColliderName = Normalize(colliderName);
        if (normalizedColliderName == Normalize(part.id) || normalizedColliderName == Normalize(part.displayName))
        {
            return true;
        }

        foreach (string alias in part.colliderNames)
        {
            string normalizedAlias = Normalize(alias);
            if (!string.IsNullOrEmpty(normalizedAlias)
                && (normalizedColliderName.Contains(normalizedAlias) || normalizedAlias.Contains(normalizedColliderName)))
            {
                return true;
            }
        }

        return false;
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        char[] characters = value.ToLowerInvariant().ToCharArray();
        int writeIndex = 0;
        foreach (char character in characters)
        {
            if (character >= 'a' && character <= 'z' || character >= '0' && character <= '9')
            {
                characters[writeIndex++] = character;
            }
        }

        return new string(characters, 0, writeIndex);
    }
}
