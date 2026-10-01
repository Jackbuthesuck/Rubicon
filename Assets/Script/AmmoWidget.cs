using UnityEngine;
using UnityEngine.UI;

public sealed class AmmoWidget : MonoBehaviour
{
    [SerializeField] private Text reserveAmmoText;
    [SerializeField] private Text magazineDisplayText;
    [SerializeField] private Text weaponText;
    [SerializeField, Min(1)] private int magazineDisplayCapacity = 20;
    [SerializeField, Min(0.01f)] private float lowAmmoPulseSpeed = 5f;
    [SerializeField] private Color lowAmmoPulseColor = Color.red;
    private Color magazineDisplayColor = Color.white;

    public void Initialize(Text reserveText, Text magazineDisplay, Text weaponLabel)
    {
        reserveAmmoText = reserveText;
        magazineDisplayText = magazineDisplay;
        weaponText = weaponLabel;
        if (magazineDisplayText != null)
        {
            magazineDisplayColor = magazineDisplayText.color;
        }
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

        if (magazineDisplayText != null)
        {
            int capacity = Mathf.Max(1, magazineDisplayCapacity);
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
    }

    private bool lowAmmoPulseActive;

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
