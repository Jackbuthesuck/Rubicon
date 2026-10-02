using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public sealed class AmmoWidget : MonoBehaviour
{
    [SerializeField] private Text reserveAmmoText;
    [SerializeField] private Text magazineDisplayText;
    [SerializeField] private Text weaponText;
    [SerializeField, Min(1)] private int magazineDisplayCapacity = 20;
    [SerializeField, Min(0.01f)] private float lowAmmoPulseSpeed = 5f;
    [SerializeField] private Color lowAmmoPulseColor = Color.red;
    private Color magazineDisplayColor = Color.white;
    private RawImage reloadBarBackground;
    private RawImage reloadBarFill;
    private RawImage chamberBarFill;

    private void OnEnable()
    {
        EnsureReloadBar();
    }

    public void Initialize(Text reserveText, Text magazineDisplay, Text weaponLabel)
    {
        reserveAmmoText = reserveText;
        magazineDisplayText = magazineDisplay;
        weaponText = weaponLabel;
        if (magazineDisplayText != null)
        {
            magazineDisplayColor = magazineDisplayText.color;
            EnsureReloadBar();
        }
    }

    public void SetDisplay(string weaponName, int reserveAmmo, int magazineAmmo, int chamberAmmo, int magazineCapacity, float reloadProgress, float chamberProgress)
    {
        if (weaponText != null)
        {
            weaponText.text = weaponName;
        }

        if (reserveAmmoText != null)
        {
            reserveAmmoText.text = Mathf.Max(0, reserveAmmo).ToString();
        }

        if (magazineDisplayText != null)
        {
            int capacity = magazineCapacity > 0 && magazineCapacity < magazineDisplayCapacity
                ? magazineCapacity
                : Mathf.Max(1, magazineDisplayCapacity);
            int loadedAmmo = Mathf.Clamp(magazineAmmo, 0, capacity);
            int emptyAmmo = capacity - loadedAmmo;
            int lowAmmoThreshold = capacity > 20
                ? 10
                : Mathf.Max(0, Mathf.FloorToInt(capacity * 0.5f) - 1);

            string loadedSlots = new string('|', loadedAmmo);
            string emptySlots = $"<color=#000000>{new string('|', emptyAmmo)}</color>";
            magazineDisplayText.text = $"{loadedSlots}{emptySlots}|+{Mathf.Max(0, chamberAmmo)}";
            magazineDisplayText.color = magazineDisplayColor;
            magazineDisplayText.gameObject.SetActive(true);
            lowAmmoPulseActive = magazineAmmo < lowAmmoThreshold;
        }

        SetProgressBars(reloadProgress, chamberProgress);
    }

    private bool lowAmmoPulseActive;

    private void CreateReloadBar()
    {
        Transform parent = magazineDisplayText.transform;
        GameObject barObject = new GameObject("ReloadBar", typeof(RectTransform));
        barObject.transform.SetParent(parent, false);
        RectTransform barRect = barObject.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 0.5f);
        barRect.anchorMax = new Vector2(1f, 0.5f);
        barRect.offsetMin = new Vector2(0f, -2f);
        barRect.offsetMax = new Vector2(0f, 2f);

        reloadBarBackground = CreateBarImage(barObject.transform, "Background", new Color(0f, 0f, 0f, 0.7f));
        reloadBarFill = CreateBarImage(barObject.transform, "Fill", new Color(0.95f, 0.72f, 0.12f, 1f));
        chamberBarFill = CreateBarImage(barObject.transform, "ChamberFill", new Color(0.25f, 0.8f, 1f, 1f));
        barObject.transform.SetAsFirstSibling();
        SetProgressBars(0f, 0f);
    }

    private void EnsureReloadBar()
    {
        if (magazineDisplayText == null)
        {
            return;
        }

        Transform existingBar = magazineDisplayText.transform.Find("ReloadBar");
        if (existingBar != null)
        {
            reloadBarBackground = existingBar.Find("Background")?.GetComponent<RawImage>();
            reloadBarFill = existingBar.Find("Fill")?.GetComponent<RawImage>();
            chamberBarFill = existingBar.Find("ChamberFill")?.GetComponent<RawImage>();
            if (chamberBarFill == null)
            {
                chamberBarFill = CreateBarImage(existingBar, "ChamberFill", new Color(0.25f, 0.8f, 1f, 1f));
            }
            return;
        }

        CreateReloadBar();
    }

    private static RawImage CreateBarImage(Transform parent, string objectName, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(RawImage));
        imageObject.transform.SetParent(parent, false);
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;
        RawImage image = imageObject.GetComponent<RawImage>();
        image.texture = Texture2D.whiteTexture;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private void SetProgressBars(float reloadProgress, float chamberProgress)
    {
        if (reloadBarBackground == null || reloadBarFill == null || chamberBarFill == null)
        {
            return;
        }

        bool reloadVisible = !Application.isPlaying || (reloadProgress > 0f && reloadProgress < 1f);
        bool chamberVisible = Application.isPlaying && chamberProgress > 0f && chamberProgress < 1f;
        reloadBarBackground.gameObject.SetActive(!Application.isPlaying || reloadVisible || chamberVisible);
        reloadBarFill.gameObject.SetActive(reloadVisible);
        chamberBarFill.gameObject.SetActive(chamberVisible);
        reloadBarFill.rectTransform.anchorMin = Vector2.zero;
        reloadBarFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(reloadProgress), 1f);
        chamberBarFill.rectTransform.anchorMin = new Vector2(1f - Mathf.Clamp01(chamberProgress), 0f);
        chamberBarFill.rectTransform.anchorMax = Vector2.one;
    }

    private void Update()
    {
        if (magazineDisplayText == null)
        {
            return;
        }

        if (!lowAmmoPulseActive)
        {
            magazineDisplayText.color = magazineDisplayColor;
            return;
        }

        float pulse = (Mathf.Sin(Time.unscaledTime * lowAmmoPulseSpeed) + 1f) * 0.5f;
        magazineDisplayText.color = Color.Lerp(magazineDisplayColor, lowAmmoPulseColor, pulse);
    }
}
