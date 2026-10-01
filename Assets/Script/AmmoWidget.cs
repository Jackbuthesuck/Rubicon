using UnityEngine;
using UnityEngine.UI;

public sealed class AmmoWidget : MonoBehaviour
{
    [SerializeField] private Text reserveAmmoText;
    [SerializeField] private Text chamberAmmoText;
    [SerializeField] private Text weaponText;
    [SerializeField] private Text[] magazineIcons;
    [SerializeField, Min(1)] private int triangleThreshold = 20;

    public void Initialize(Text reserveText, Text chamberText, Text weaponLabel, Text[] icons)
    {
        reserveAmmoText = reserveText;
        chamberAmmoText = chamberText;
        weaponText = weaponLabel;
        magazineIcons = icons;
    }

    public void SetDisplay(string weaponName, int reserveAmmo, int magazineAmmo, int chamberAmmo)
    {
        if (weaponText != null)
        {
            weaponText.text = weaponName;
        }

        if (reserveAmmoText != null)
        {
            reserveAmmoText.text = Mathf.Max(0, reserveAmmo).ToString();
        }

        if (chamberAmmoText != null)
        {
            chamberAmmoText.text = $"|+{Mathf.Max(0, chamberAmmo)}";
        }

        if (magazineIcons == null)
        {
            return;
        }

        bool useTriangle = magazineAmmo > triangleThreshold;
        int visibleIconCount = useTriangle ? 1 : Mathf.Clamp(magazineAmmo, 0, magazineIcons.Length);
        for (int iconIndex = 0; iconIndex < magazineIcons.Length; iconIndex++)
        {
            Text icon = magazineIcons[iconIndex];
            if (icon == null)
            {
                continue;
            }

            icon.gameObject.SetActive(iconIndex < visibleIconCount);
            if (iconIndex < visibleIconCount)
            {
                icon.text = useTriangle ? ">" : "|";
            }
        }
    }
}
