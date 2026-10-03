using System.Collections;
using UnityEngine;

public sealed partial class ShootingTarget
{
    [SerializeField, Min(0f)] private float healthBarShakeBaseDistance = 2f;
    [SerializeField, Min(0f)] private float healthBarShakeDamageMultiplier = 0.08f;
    [SerializeField, Min(0f)] private float healthBarShakeMaximumDistance = 10f;
    private float lastHealthBarCurrent = float.NaN;
    private float lastHealthBarMaximum = float.NaN;
    private Vector2 healthBarShakeOffset;
    private Coroutine healthBarShakeRoutine;
    private float currentHealthBarShakeDistance;

    private void LateUpdate()
    {
        UpdateHealthBarFill();
        UpdateHealthBar();
    }

    private void CreateHealthBar()
    {
        GameObject healthBarPrefab = Resources.Load<GameObject>("UI/TargetStatusOverlay");
        if (healthBarPrefab == null)
        {
            Debug.LogWarning("Could not load Resources/UI/TargetStatusOverlay.prefab.", this);
            return;
        }

        healthBarObject = Instantiate(healthBarPrefab);
        healthBarCanvas = healthBarObject.GetComponent<Canvas>();
        if (healthBarCanvas == null)
        {
            healthBarCanvas = healthBarObject.AddComponent<Canvas>();
        }

        healthBarCanvasGroup = healthBarObject.GetComponent<CanvasGroup>();
        if (healthBarCanvasGroup == null)
        {
            healthBarCanvasGroup = healthBarObject.AddComponent<CanvasGroup>();
        }

        healthBarView = healthBarObject.GetComponent<TargetHealthBarView>();
        healthBarRoot = healthBarView?.BarRoot;
        healthBarFill = healthBarView?.FillImage;
        healthBarView?.SetTargetName(definition.displayName);
        healthBarView?.SetStaminaVisible(definition.stamina >= 0f);
        if (healthBarCanvas == null || healthBarCanvasGroup == null || healthBarRoot == null || healthBarFill == null)
        {
            Debug.LogWarning("TargetStatusOverlay.prefab is missing its target health view or fill object.", this);
            Destroy(healthBarObject);
            healthBarObject = null;
            return;
        }

        healthBarCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        healthBarCanvas.pixelPerfect = true;
        healthBarCanvas.sortingOrder = 90;
        UpdateHealthBarFill();
    }

    private void UpdateHealthBar()
    {
        if (healthBarRoot == null || healthBarCanvasGroup == null)
        {
            return;
        }

        Camera targetCamera = Camera.main;
        if (targetCamera == null || targetRenderers.Count == 0 || IsDestroyed || respawnRoutine != null)
        {
            healthBarCanvasGroup.alpha = 0f;
            return;
        }

        Bounds bounds = targetRenderers[0].bounds;
        for (int index = 1; index < targetRenderers.Count; index++)
        {
            if (targetRenderers[index] != null)
            {
                bounds.Encapsulate(targetRenderers[index].bounds);
            }
        }

        Vector3 screenBoundsMin = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3 screenBoundsMax = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        for (int x = 0; x <= 1; x++)
        {
            for (int y = 0; y <= 1; y++)
            {
                for (int z = 0; z <= 1; z++)
                {
                    Vector3 worldCorner = new Vector3(
                        x == 0 ? bounds.min.x : bounds.max.x,
                        y == 0 ? bounds.min.y : bounds.max.y,
                        z == 0 ? bounds.min.z : bounds.max.z);
                    Vector3 screenCorner = targetCamera.WorldToScreenPoint(worldCorner);
                    if (screenCorner.z <= 0f)
                    {
                        healthBarCanvasGroup.alpha = 0f;
                        return;
                    }

                    screenBoundsMin = Vector3.Min(screenBoundsMin, screenCorner);
                    screenBoundsMax = Vector3.Max(screenBoundsMax, screenCorner);
                }
            }
        }

        if (screenBoundsMax.x <= 0f || screenBoundsMin.x >= Screen.width
            || screenBoundsMax.y <= 0f || screenBoundsMin.y >= Screen.height)
        {
            healthBarCanvasGroup.alpha = 0f;
            return;
        }

        float barWidth = healthBarRoot.rect.width * healthBarRoot.lossyScale.x;
        float barHeight = healthBarRoot.rect.height * healthBarRoot.lossyScale.y;
        healthBarRoot.position = new Vector3(
            screenBoundsMax.x + healthBarScreenOffset.x + barWidth * healthBarRoot.pivot.x,
            screenBoundsMin.y + healthBarScreenOffset.y + barHeight * healthBarRoot.pivot.y,
            0f);
        healthBarRoot.position += (Vector3)healthBarShakeOffset;
        healthBarCanvasGroup.alpha = 1f;
    }

    private void ShakeHealthBar(float impactDamage)
    {
        if (healthBarShakeRoutine != null)
        {
            StopCoroutine(healthBarShakeRoutine);
        }

        currentHealthBarShakeDistance = Mathf.Clamp(
            healthBarShakeBaseDistance + Mathf.Max(0f, impactDamage) * healthBarShakeDamageMultiplier,
            0f,
            healthBarShakeMaximumDistance);
        healthBarShakeRoutine = StartCoroutine(ShakeHealthBarRoutine());
    }

    private IEnumerator ShakeHealthBarRoutine()
    {
        const float duration = 0.18f;
        const float frequency = 28f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float envelope = 1f - progress;
            float phase = elapsed * frequency * Mathf.PI * 2f;
            healthBarShakeOffset = new Vector2(
                Mathf.Sin(phase) * currentHealthBarShakeDistance * envelope,
                Mathf.Cos(phase * 1.37f) * currentHealthBarShakeDistance * 0.35f * envelope);
            yield return null;
        }

        healthBarShakeOffset = Vector2.zero;
        healthBarShakeRoutine = null;
    }

    private void UpdateHealthBarFill()
    {
        float currentHealth = CurrentHealth;
        float maximumHealth = MaximumHealth;
        if (healthBarView != null
            && (!Mathf.Approximately(lastHealthBarCurrent, currentHealth)
                || !Mathf.Approximately(lastHealthBarMaximum, maximumHealth)))
        {
            healthBarView.SetHealth(currentHealth, maximumHealth);
            lastHealthBarCurrent = currentHealth;
            lastHealthBarMaximum = maximumHealth;
        }
    }

    private void OnDestroy()
    {
        if (healthBarObject != null)
        {
            Destroy(healthBarObject);
        }
    }
}
