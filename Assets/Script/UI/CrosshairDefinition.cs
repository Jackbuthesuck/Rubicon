using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

[System.Serializable]
public sealed class CrosshairPieceDefinition
{
    public Vector2 position;
    public Vector2 size = new Vector2(8f, 8f);
    public float rotation;
}

[System.Serializable]
public sealed class CrosshairDefinition
{
    public bool enabled = true;
    public WeaponCategory category;
    public Image[] pieces = new Image[0];
    public CrosshairPieceDefinition[] layoutPieces = new CrosshairPieceDefinition[0];
    public CrosshairPieceMode[] pieceModes = new CrosshairPieceMode[0];
    public bool[] runningVisible = new bool[0];
    public bool[] fadeWhenMoving = new bool[0];
    public bool[] fadeWhenFiring = new bool[0];
    [Min(0f)] public float minimumGap = 18f;
    [Min(0f)] public float maximumGap = 44f;
    [Min(0f)] public float pieceLength = 16f;
    [Min(0f)] public float pieceThickness = 4f;
    public bool overridePieceSize = true;
    [FormerlySerializedAs("idleColor")] public Color defaultColor = Color.white;
    [FormerlySerializedAs("aimingColor")] public Color aimAssistColor = new Color(1f, 0.8f, 0.2f, 1f);
    public Color unavailableColor = new Color(0.55f, 0.55f, 0.55f, 1f);
}

public enum CrosshairPieceMode
{
    Expand,
    Contract,
    Stay
}
