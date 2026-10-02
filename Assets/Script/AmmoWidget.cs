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
    private Color chamberBoxColor = Color.white;
    private RawImage reloadBarBackground;
    private RawImage reloadBarFill;
    private RawImage chamberBarFill;
    private RawImage chamberBoxImage;
    private Transform chamberBox;
    private Transform chamberBar;

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
            string magazineSection = $"{loadedSlots}{emptySlots}";
            magazineDisplayText.text = $"{magazineSection} >";
            magazineDisplayText.color = magazineDisplayColor;
            magazineDisplayText.gameObject.SetActive(true);
            lowAmmoPulseActive = magazineAmmo < lowAmmoThreshold;
        }

        SetProgressBars(reloadProgress, chamberProgress, chamberAmmo > 0);
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
        barRect.offsetMin = new Vector2(0f, -4f);
        barRect.offsetMax = new Vector2(0f, 4f);

        reloadBarBackground = CreateBarImage(barObject.transform, "Background", new Color(0f, 0f, 0f, 0.7f));
        chamberBarFill = CreateBarImage(barObject.transform, "ChamberFill", new Color(0.25f, 0.8f, 1f, 1f));
        reloadBarFill = CreateBarImage(barObject.transform, "Fill", new Color(0.95f, 0.72f, 0.12f, 1f));
        barObject.transform.SetAsFirstSibling();
        SetProgressBars(0f, 0f, false);
    }

    private void EnsureReloadBar()
    {
        if (magazineDisplayText == null)
        {
            return;
        }

        Transform ammoGroup = magazineDisplayText.transform.parent;
        Transform existingBar = FindNamedChild(ammoGroup, "ReloadBar");
        chamberBox = FindNamedChild(ammoGroup, "ChamberBox");
        chamberBar = FindNamedChild(ammoGroup, "ChamberBar");
        if (chamberBox != null)
        {
            chamberBoxImage = chamberBox.GetComponent<RawImage>();
            if (chamberBoxImage != null)
            {
                chamberBoxColor = chamberBoxImage.color;
            }
        }

        if (existingBar != null)
        {
            reloadBarBackground = FindNamedImage(existingBar, "Background");
            reloadBarFill = FindNamedImage(existingBar, "ReloadFill");
            reloadBarFill = reloadBarFill != null ? reloadBarFill : FindNamedImage(existingBar, "Fill");
            chamberBarFill = FindNamedImage(chamberBar, "Fill");
            chamberBarFill = chamberBarFill != null ? chamberBarFill : FindNamedImage(chamberBar, "ChamberFill");
            chamberBarFill = chamberBarFill != null ? chamberBarFill : FindNamedImage(existingBar, "ChamberFill");
            if (chamberBarFill == null && chamberBar == null && chamberBox == null)
            {
                chamberBarFill = CreateBarImage(existingBar, "ChamberFill", new Color(0.25f, 0.8f, 1f, 1f));
            }
            return;
        }

        CreateReloadBar();
    }

    private static Transform FindNamedChild(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        Transform directChild = root.Find(objectName);
        if (directChild != null)
        {
            return directChild;
        }

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == objectName)
            {
                return child;
            }
        }

        return null;
    }

    private static RawImage FindNamedImage(Transform root, string objectName)
    {
        return FindNamedChild(root, objectName)?.GetComponent<RawImage>();
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

    private void SetProgressBars(float reloadProgress, float chamberProgress, bool chambered)
    {
        if (reloadBarBackground == null || reloadBarFill == null)
        {
            return;
        }

        bool reloadVisible = !Application.isPlaying || (reloadProgress > 0f && reloadProgress < 1f);
        bool chamberVisible = Application.isPlaying && chamberProgress > 0f && chamberProgress < 1f;
        reloadBarBackground.gameObject.SetActive(!Application.isPlaying || reloadVisible);
        reloadBarFill.gameObject.SetActive(reloadVisible);
        reloadBarFill.rectTransform.anchorMin = Vector2.zero;
        reloadBarFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(reloadProgress), 1f);

        if (chamberBoxImage != null)
        {
            chamberBoxImage.gameObject.SetActive(!Application.isPlaying || chambered);
        }

        if (chamberBarFill != null)
        {
            chamberBarFill.gameObject.SetActive(chamberVisible);
            float chamberFill = Mathf.Clamp01(chamberProgress);
            chamberBarFill.rectTransform.anchorMin = new Vector2(1f - chamberFill, 0f);
            chamberBarFill.rectTransform.anchorMax = Vector2.one;
        }
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
            if (chamberBoxImage != null)
            {
                chamberBoxImage.color = chamberBoxColor;
            }

            return;
        }

        float pulse = (Mathf.Sin(Time.unscaledTime * lowAmmoPulseSpeed) + 1f) * 0.5f;
        Color pulseColor = Color.Lerp(magazineDisplayColor, lowAmmoPulseColor, pulse);
        magazineDisplayText.color = pulseColor;
        if (chamberBoxImage != null)
        {
            chamberBoxImage.color = Color.Lerp(chamberBoxColor, lowAmmoPulseColor, pulse);
        }
    }
}
