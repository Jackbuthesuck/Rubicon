using UnityEngine;

public sealed class WeaponAttachmentSocket : MonoBehaviour
{
    [SerializeField] private string originName = "Origin";
    [SerializeField] private string fullStockName = "OBStockShotgunFull";
    [SerializeField] private string alternateGripName = "OBStockShotgunGrip";
    [SerializeField] private string singleActionName = "SingleAction";
    [SerializeField] private string doubleActionName = "DoubleAction";
    private bool alternateGripActive;
    private bool doubleActionActive;

    public string GripVariantName => alternateGripActive ? "Grip stock" : "Full stock";
    public string ActionModeName => doubleActionActive ? "Double Action" : "Single Action";
    public bool IsDoubleAction => doubleActionActive;

    private void Awake()
    {
        if (FindAttachmentPart(fullStockName) != null && FindAttachmentPart(alternateGripName) != null)
        {
            SetGripVariant(false);
        }
        else
        {
            SetActionVariant(false);
        }
    }

    public GameObject Attach(GameObject attachmentPrefab, string socketName)
    {
        if (attachmentPrefab == null)
        {
            return null;
        }

        Transform socket = FindDescendant(transform, socketName);
        if (socket == null)
        {
            return null;
        }

        GameObject attachment = Instantiate(attachmentPrefab);
        Transform origin = FindDescendant(attachment.transform, originName);
        if (origin == null)
        {
            Destroy(attachment);
            return null;
        }

        attachment.transform.SetParent(socket, true);
        attachment.transform.rotation = socket.rotation * Quaternion.Inverse(origin.localRotation);
        attachment.transform.position = socket.position - attachment.transform.rotation * origin.localPosition;
        return attachment;
    }

    public bool ToggleGripVariant()
    {
        Transform fullStock = FindAttachmentPart(fullStockName);
        Transform alternateGrip = FindAttachmentPart(alternateGripName);
        if (fullStock == null || alternateGrip == null)
        {
            LogMissingGripVariants("toggle");
            return false;
        }

        bool changed = SetGripVariant(!alternateGripActive);
        Debug.Log($"KS-23 grip toggle on {name}: {GripVariantName} (full='{fullStock.name}', grip='{alternateGrip.name}', changed={changed}).", this);
        return changed;
    }

    public bool ToggleActionVariant()
    {
        return SetActionVariant(!doubleActionActive);
    }

    private bool SetGripVariant(bool useAlternateGrip)
    {
        Transform fullStock = FindAttachmentPart(fullStockName);
        Transform alternateGrip = FindAttachmentPart(alternateGripName);
        if (fullStock == null || alternateGrip == null)
        {
            LogMissingGripVariants("initialization");
            return false;
        }

        alternateGripActive = useAlternateGrip;
        fullStock.gameObject.SetActive(!alternateGripActive);
        alternateGrip.gameObject.SetActive(alternateGripActive);
        return true;
    }

    private bool SetActionVariant(bool useDoubleAction)
    {
        Transform singleAction = FindVariantPart(singleActionName, "single");
        Transform doubleAction = FindVariantPart(doubleActionName, "double");
        doubleActionActive = useDoubleAction;
        if (singleAction != null && doubleAction != null)
        {
            singleAction.gameObject.SetActive(!doubleActionActive);
            doubleAction.gameObject.SetActive(doubleActionActive);
        }

        return true;
    }

    private void LogMissingGripVariants(string phase)
    {
        string childNames = string.Empty;
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name.IndexOf("stock", System.StringComparison.OrdinalIgnoreCase) >= 0
                || child.name.IndexOf("grip", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                childNames += $" '{child.name}'";
            }
        }

        Debug.LogWarning($"KS-23 grip {phase} failed under {name}. Expected full='{fullStockName}' grip='{alternateGripName}'. Found:{childNames}", this);
    }

    private Transform FindAttachmentPart(string expectedName)
    {
        Transform exactMatch = FindDescendant(transform, expectedName);
        if (exactMatch != null)
        {
            return exactMatch;
        }

        string normalizedName = expectedName.Replace("OB", string.Empty).ToLowerInvariant();
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            string normalizedChildName = child.name.Replace("OB", string.Empty).ToLowerInvariant();
            if (normalizedChildName.Contains(normalizedName))
            {
                return child;
            }
        }

        return null;
    }

    private Transform FindVariantPart(string expectedName, string keyword)
    {
        Transform exactMatch = FindDescendant(transform, expectedName);
        if (exactMatch != null)
        {
            return exactMatch;
        }

        string normalizedKeyword = keyword.ToLowerInvariant();
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            string normalizedName = child.name.Replace(" ", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
            if (normalizedName.Contains(normalizedKeyword)
                && (normalizedName.Contains("action") || normalizedName == normalizedKeyword))
            {
                return child;
            }
        }

        return null;
    }

    public static Transform FindDescendant(Transform root, string objectName)
    {
        if (root == null || string.IsNullOrEmpty(objectName))
        {
            return null;
        }

        if (root.name == objectName)
        {
            return root;
        }

        for (int childIndex = 0; childIndex < root.childCount; childIndex++)
        {
            Transform result = FindDescendant(root.GetChild(childIndex), objectName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}