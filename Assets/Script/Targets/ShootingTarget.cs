using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ShootingTarget : Damageable
{
    [SerializeField] private string targetId = "shooting_target";
    [SerializeField] private TextAsset fallbackDefinition;
    [SerializeField, Min(0.01f)] private float hitFlashDuration = 0.12f;
    [SerializeField, Min(0f)] private float respawnDelay = 1f;
    [SerializeField] private Vector2 healthBarScreenOffset = new Vector2(18f, 0f);
    [SerializeField] private List<DamageablePartHealthDisplay> partHealth = new List<DamageablePartHealthDisplay>();

    private readonly Dictionary<Collider, TargetPartState> colliderParts = new Dictionary<Collider, TargetPartState>();
    private readonly Dictionary<string, TargetPartState> partsById = new Dictionary<string, TargetPartState>();
    private readonly List<TargetPartState> parts = new List<TargetPartState>();
    private readonly List<Renderer> targetRenderers = new List<Renderer>();
    private readonly List<Collider> targetColliders = new List<Collider>();
    private DamageableDefinition definition;
    [System.NonSerialized] private float totalCurrentHealth;
    private Coroutine respawnRoutine;
    private GameObject healthBarObject;
    private Canvas healthBarCanvas;
    private RectTransform healthBarRoot;
    private Image healthBarFill;
    private CanvasGroup healthBarCanvasGroup;
    private TargetHealthBarView healthBarView;

    public string TargetId => definition != null ? definition.id : targetId;
    public override float CurrentHealth => totalCurrentHealth;
    public override float MaximumHealth => definition != null ? definition.totalHealth : 0f;
    public override bool IsDestroyed => totalCurrentHealth <= 0f;

    public static ShootingTarget FindOrAddForHit(Collider hitCollider)
    {
        if (hitCollider == null)
        {
            return null;
        }

        ShootingTarget existingTarget = hitCollider.GetComponentInParent<ShootingTarget>();
        if (existingTarget != null)
        {
            return existingTarget;
        }

        Transform root = hitCollider.transform.root;
        if (root == null || Normalize(root.name) != Normalize("Shooting Target"))
        {
            return null;
        }

        return root.gameObject.AddComponent<ShootingTarget>();
    }

    private sealed class TargetPartState
    {
        public DamageablePartDefinition Definition;
        public float CurrentHealth;
        public readonly HashSet<Renderer> Renderers = new HashSet<Renderer>();
        public Coroutine FlashRoutine;
    }

    private sealed class FlashRendererState
    {
        public Renderer Renderer;
        public Color BaseColor;
    }

    protected override void Awake()
    {
        definition = LoadDefinition();
        totalCurrentHealth = Mathf.Max(0f, definition.totalHealth);
        BindColliders();
        targetRenderers.AddRange(GetComponentsInChildren<Renderer>(true));
        targetColliders.AddRange(GetComponentsInChildren<Collider>(true));
        CreateHealthBar();
    }

}
