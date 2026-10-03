using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class ShootingTarget
{
    private void FlashPart(TargetPartState part, Color color)
    {
        if (part.FlashRoutine != null)
        {
            StopCoroutine(part.FlashRoutine);
        }

        part.FlashRoutine = StartCoroutine(FlashPartRoutine(part, color));
    }

    private IEnumerator FlashPartRoutine(TargetPartState part, Color color)
    {
        List<FlashRendererState> rendererStates = new List<FlashRendererState>();
        foreach (Renderer renderer in part.Renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            Material material = renderer.sharedMaterial;
            Color baseColor = material != null && material.HasProperty("_BaseColor")
                ? material.GetColor("_BaseColor")
                : material != null && material.HasProperty("_Color")
                ? material.GetColor("_Color")
                : Color.white;
            rendererStates.Add(new FlashRendererState
            {
                Renderer = renderer,
                BaseColor = baseColor
            });
        }

        float elapsed = 0f;
        while (elapsed < hitFlashDuration)
        {
            float fade = Mathf.Clamp01(elapsed / hitFlashDuration);
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            foreach (FlashRendererState rendererState in rendererStates)
            {
                if (rendererState.Renderer == null)
                {
                    continue;
                }

                Color flashColor = Color.Lerp(color, rendererState.BaseColor, fade);
                block.SetColor("_BaseColor", flashColor);
                block.SetColor("_Color", flashColor);
                rendererState.Renderer.SetPropertyBlock(block);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        foreach (FlashRendererState rendererState in rendererStates)
        {
            if (rendererState.Renderer != null)
            {
                rendererState.Renderer.SetPropertyBlock(null);
            }
        }

        part.FlashRoutine = null;
    }

    private void SetTargetVisible(bool visible)
    {
        foreach (Renderer renderer in targetRenderers)
        {
            if (renderer != null)
            {
                renderer.enabled = visible;
            }
        }

        foreach (Collider collider in targetColliders)
        {
            if (collider != null)
            {
                collider.enabled = visible;
            }
        }
    }

    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnDelay);
        totalCurrentHealth = Mathf.Max(0f, definition.totalHealth);
        foreach (TargetPartState part in parts)
        {
            part.CurrentHealth = part.Definition.takesDamage
                ? Mathf.Max(0f, part.Definition.health)
                : 0f;
        }

        SyncPartHealthDisplay();
        SetTargetVisible(true);
        respawnRoutine = null;
        UpdateHealthBarFill();
        UpdateHealthBar();
    }
}
