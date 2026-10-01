using UnityEngine;
using UnityEngine.EventSystems;

public sealed class MenuController : MonoBehaviour
{
    private GameObject activeChild;
    private GameObject parentSelection;
    private GameObject childSelection;

    public bool HasOpenChild => activeChild != null && activeChild.activeInHierarchy;

    public bool OpenChild(GameObject childRoot, GameObject firstSelectable, GameObject restoreSelection = null)
    {
        if (childRoot == null)
        {
            return false;
        }

        if (HasOpenChild)
        {
            CloseChild();
        }

        parentSelection = restoreSelection != null
            ? restoreSelection
            : EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        activeChild = childRoot;
        childSelection = firstSelectable;
        activeChild.SetActive(true);
        Select(childSelection);
        return true;
    }

    public bool CloseChild()
    {
        if (!HasOpenChild)
        {
            return false;
        }

        activeChild.SetActive(false);
        activeChild = null;
        Select(parentSelection);
        parentSelection = null;
        childSelection = null;
        return true;
    }

    public void CloseAllChildren()
    {
        if (activeChild != null)
        {
            activeChild.SetActive(false);
        }

        activeChild = null;
        parentSelection = null;
        childSelection = null;
    }

    private static void Select(GameObject target)
    {
        if (EventSystem.current != null && target != null)
        {
            EventSystem.current.SetSelectedGameObject(target);
        }
    }
}
