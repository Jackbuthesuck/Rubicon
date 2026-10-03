using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class TargetHealthBarView : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float chainDamageDisplayDuration = 0.75f;
    [SerializeField, Min(0f)] private float chainDamageFadeInDuration = 0.08f;
    [SerializeField, Min(0f)] private float chainDamageFadeOutDuration = 0.2f;
    [SerializeField, Min(0f)] private float chainDamageBarSpacing = 3f;

    public Image FillImage { get; private set; }
    public RectTransform BarRoot { get; private set; }
    private RectTransform fillRect;
    private Vector3 baseFillScale = Vector3.one;
    private Text targetName;
    private GameObject staminaFill;
    private RectTransform chainDamageRoot;
    private GameObject chainDamageTemplate;
    private CanvasGroup chainDamageCanvasGroup;
    private Coroutine chainDamageRoutine;

    private void Awake()
    {
        Transform barTransform = transform.Find("Health Bar");
        Transform fillTransform = barTransform != null
            ? barTransform.Find("Fill") ?? barTransform.Find("HealthFill")
            : null;
        if (barTransform == null)
        {
            GameObject barObject = new GameObject("Health Bar", typeof(RectTransform));
            barObject.transform.SetParent(transform, false);
            barTransform = barObject.transform;

            RectTransform barRect = barTransform as RectTransform;
            barRect.anchorMin = new Vector2(0.5f, 0.5f);
            barRect.anchorMax = new Vector2(0.5f, 0.5f);
            barRect.pivot = new Vector2(0.5f, 0.5f);
            barRect.sizeDelta = new Vector2(14f, 180f);
            barRect.anchoredPosition = Vector2.zero;
        }

        if (fillTransform == null)
        {
            fillTransform = transform.Find("HealthFill");
            if (fillTransform != null)
            {
                fillTransform.SetParent(barTransform, false);
            }
        }

        BarRoot = barTransform as RectTransform;
        if (BarRoot == null || fillTransform == null)
        {
            return;
        }

        Image background = barTransform.GetComponent<Image>();
        fillRect = fillTransform as RectTransform;
        FillImage = fillTransform.GetComponent<Image>();
        if (background == null || FillImage == null)
        {
            Debug.LogWarning("TargetStatusOverlay needs Image components on Health Bar and HealthFill.", this);
            return;
        }

        baseFillScale = fillRect != null ? fillRect.localScale : Vector3.one;

        chainDamageTemplate = BarRoot.Find("Chain Damage Template")?.gameObject
            ?? transform.Find("Chain Damage Template")?.gameObject;
        if (chainDamageTemplate != null)
        {
            chainDamageTemplate.SetActive(false);
        }

        CreateChainDamageRoot();

        Transform nameTransform = transform.Find("Target Name");
        if (nameTransform == null && BarRoot != null)
        {
            nameTransform = BarRoot.Find("Target Name");
        }

        staminaFill = transform.Find("StaminaFill")?.gameObject;
        if (nameTransform != null)
        {
            targetName = nameTransform.GetComponent<Text>();
            if (targetName == null)
            {
                targetName = nameTransform.gameObject.AddComponent<Text>();
            }

        }
    }

    public void SetTargetName(string displayName)
    {
        if (targetName != null)
        {
            targetName.text = displayName;
        }
    }

    public void SetHealth(float currentHealth, float maximumHealth)
    {
        float healthRatio = maximumHealth > 0f
            ? Mathf.Clamp01(currentHealth / maximumHealth)
            : 0f;
        if (fillRect != null)
        {
            fillRect.localScale = new Vector3(
                baseFillScale.x,
                baseFillScale.y * healthRatio,
                baseFillScale.z);
        }

        if (FillImage != null)
        {
            FillImage.fillAmount = 1f;
        }
    }

    public void SetStaminaVisible(bool visible)
    {
        if (staminaFill != null)
        {
            staminaFill.SetActive(visible);
        }
    }

    public void ShowChainDamage(IList<DamageChainHit> chainHits)
    {
        if (chainDamageRoot == null || chainDamageTemplate == null || chainHits == null || chainHits.Count == 0)
        {
            return;
        }

        if (chainDamageRoutine != null)
        {
            StopCoroutine(chainDamageRoutine);
        }

        ClearChainDamageEntries();
        chainDamageRoot.gameObject.SetActive(true);
        chainDamageRoot.SetAsLastSibling();
        chainDamageCanvasGroup.alpha = 0f;
        RectTransform templateRect = chainDamageTemplate.GetComponent<RectTransform>();
        float totalMaximumHealth = 0f;
        foreach (DamageChainHit chainHit in chainHits)
        {
            totalMaximumHealth += Mathf.Max(0f, chainHit.MaximumHealth);
        }

        float totalSpacing = Mathf.Max(0, chainHits.Count - 1) * chainDamageBarSpacing;
        float availableLength = Mathf.Max(1f, BarRoot.rect.height);
        float chainScale = totalMaximumHealth + totalSpacing > availableLength
            ? availableLength / (totalMaximumHealth + totalSpacing)
            : 1f;
        float totalDisplayLength = Mathf.Min(
            availableLength,
            totalMaximumHealth * chainScale + totalSpacing);
        chainDamageRoot.sizeDelta = new Vector2(
            chainDamageRoot.sizeDelta.x,
            totalDisplayLength);
        float remainingDisplayLength = totalDisplayLength;
        for (int index = 0; index < chainHits.Count; index++)
        {
            float segmentLength = Mathf.Max(0f, chainHits[index].MaximumHealth) * chainScale;
            remainingDisplayLength -= segmentLength;
            CreateChainDamageEntry(chainDamageRoot, chainHits[index], remainingDisplayLength, segmentLength);
            remainingDisplayLength -= chainDamageBarSpacing;
        }

        chainDamageRoutine = StartCoroutine(AnimateChainDamage());
    }

    private void CreateChainDamageRoot()
    {
        if (BarRoot == null || chainDamageRoot != null || chainDamageTemplate == null)
        {
            return;
        }

        GameObject rootObject = new GameObject("Chain Damage", typeof(RectTransform));
        RectTransform templateRect = chainDamageTemplate.GetComponent<RectTransform>();
        RectTransform rootRect = rootObject.GetComponent<RectTransform>();
        rootObject.transform.SetParent(templateRect.parent, false);
        rootRect.anchorMin = templateRect.anchorMin;
        rootRect.anchorMax = templateRect.anchorMax;
        rootRect.pivot = templateRect.pivot;
        rootRect.anchoredPosition = templateRect.anchoredPosition;
        rootRect.sizeDelta = templateRect.sizeDelta;
        rootRect.localRotation = templateRect.localRotation;
        rootRect.localScale = templateRect.localScale;
        chainDamageRoot = rootObject.GetComponent<RectTransform>();
        chainDamageCanvasGroup = rootObject.AddComponent<CanvasGroup>();
        chainDamageRoot.gameObject.SetActive(false);
    }

    private void CreateChainDamageEntry(
        RectTransform parent,
        DamageChainHit chainHit,
        float verticalPosition,
        float segmentLength)
    {
        GameObject entryObject = Instantiate(chainDamageTemplate, parent);
        entryObject.name = $"Chain Damage {chainHit.DisplayName}";
        entryObject.SetActive(true);
        RectTransform entryRect = entryObject.GetComponent<RectTransform>();
        entryRect.anchoredPosition = Vector2.up * verticalPosition;
        entryRect.sizeDelta = new Vector2(entryRect.sizeDelta.x, segmentLength);
        Image fill = entryObject.transform.Find("Damage")?.GetComponent<Image>();
        Text label = entryObject.transform.Find("Label")?.GetComponent<Text>();
        if (fill == null)
        {
            Destroy(entryObject);
            return;
        }

        float healthRatio = chainHit.MaximumHealth > 0f
            ? Mathf.Clamp01(chainHit.RemainingHealth / chainHit.MaximumHealth)
            : 0f;
        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(1f, 0f);
        fillRect.pivot = new Vector2(0.5f, 0f);
        fillRect.anchoredPosition = new Vector2(0f, 2f);
        fillRect.sizeDelta = new Vector2(-4f, Mathf.Max(0f, segmentLength - 4f));
        fillRect.localScale = new Vector3(1f, healthRatio, 1f);
        fill.fillAmount = 1f;
        if (label != null)
        {
            label.text = chainHit.DisplayName;
        }
    }

    private IEnumerator AnimateChainDamage()
    {
        float elapsed = 0f;
        while (elapsed < chainDamageFadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            chainDamageCanvasGroup.alpha = chainDamageFadeInDuration > 0f
                ? Mathf.Clamp01(elapsed / chainDamageFadeInDuration)
                : 1f;
            yield return null;
        }

        chainDamageCanvasGroup.alpha = 1f;
        yield return new WaitForSecondsRealtime(chainDamageDisplayDuration);

        elapsed = 0f;
        while (elapsed < chainDamageFadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            chainDamageCanvasGroup.alpha = chainDamageFadeOutDuration > 0f
                ? 1f - Mathf.Clamp01(elapsed / chainDamageFadeOutDuration)
                : 0f;
            yield return null;
        }

        ClearChainDamageEntries();
        chainDamageRoot.gameObject.SetActive(false);
        chainDamageCanvasGroup.alpha = 0f;
        chainDamageRoutine = null;
    }

    private void ClearChainDamageEntries()
    {
        for (int index = chainDamageRoot.childCount - 1; index >= 0; index--)
        {
            Destroy(chainDamageRoot.GetChild(index).gameObject);
        }
    }
}
