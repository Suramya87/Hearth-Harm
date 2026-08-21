using UnityEngine;

public class PlayerRingColor : MonoBehaviour
{
    [Header("Ring Appearance")]
    [SerializeField] private float radiusMultiplier = 1.4f;

    private GameObject _ringGO;
    private SpriteRenderer _ringRenderer;
    private Color _ringColor = Color.white;

    public static void ApplySlotColor(GameObject player, int slotIndex)
    {
        Color color = PartySlotUI.GetPlayerRingColor(slotIndex);

        var ring = player.GetComponent<PlayerRingColor>();
        if (ring == null) ring = player.AddComponent<PlayerRingColor>();
        ring._ringColor = color;
        ring.EnsureRing();
    }

    private void EnsureRing()
    {
        if (_ringRenderer != null)
        {
            _ringRenderer.color = _ringColor;
            return;
        }

        SpriteRenderer baseRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (baseRenderer == null) return;

        _ringGO = new GameObject("PlayerRing");
        _ringGO.transform.SetParent(baseRenderer.transform, false);
        _ringGO.transform.localPosition = Vector3.zero;
        _ringGO.layer = baseRenderer.gameObject.layer;
        _ringGO.hideFlags = HideFlags.HideInHierarchy;

        _ringRenderer = _ringGO.AddComponent<SpriteRenderer>();
        _ringRenderer.sprite = CreateRingSprite();
        _ringRenderer.color = _ringColor;
        _ringRenderer.sortingOrder = baseRenderer.sortingOrder - 1;

        Vector2 spriteSize = baseRenderer.sprite.bounds.size;
        float ringRadius = Mathf.Max(spriteSize.x, spriteSize.y) / 2f * radiusMultiplier;
        _ringGO.transform.localScale = new Vector3(ringRadius * 2f / baseRenderer.sprite.pixelsPerUnit, ringRadius * 2f / baseRenderer.sprite.pixelsPerUnit, 1f);
        _ringGO.transform.localPosition = new Vector3(0f, -spriteSize.y / 2f + 0.05f, 0f);
    }

    private Sprite CreateRingSprite()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size);
        float center = size / 2f;
        float innerRadius = (size / 2f) - 6f;
        float outerRadius = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                bool insideRing = dist >= innerRadius && dist <= outerRadius;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, insideRing ? 1f : 0f));
            }
        }
        tex.Apply();

        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        sprite.name = "RingSprite";
        return sprite;
    }

    private void OnDestroy()
    {
        if (_ringGO != null) Destroy(_ringGO);
    }
}
