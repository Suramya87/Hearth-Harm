using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class PartySlotUI : MonoBehaviour, IPointerClickHandler
{
    public int PlayerIndex { get; set; }
    public Color SlotColor => _dotImage != null ? _dotImage.color : Color.white;

    // Distinct colors for each player slot (red, blue, green, yellow)
    public static readonly Color[] SlotColors =
    {
        new Color(0.95f, 0.25f, 0.25f),  // Player 1 — red
        new Color(0.25f, 0.45f, 0.95f),  // Player 2 — blue
        new Color(0.25f, 0.85f, 0.35f),  // Player 3 — green
        new Color(0.95f, 0.75f, 0.15f),  // Player 4 — yellow
    };

    private Image _dotImage;

    private void Awake()
    {
        if (_dotImage == null)
            _dotImage = GetComponent<Image>();
    }

    public static PartySlotUI Create(Transform parent, int playerIndex)
    {
        GameObject go = new GameObject($"PartySlot_{playerIndex + 1}");
        go.transform.SetParent(parent, false);

        Image img = go.AddComponent<Image>();
        img.color = SlotColors[playerIndex];
        img.raycastTarget = true;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(36f, 36f);

        PartySlotUI slot = go.AddComponent<PartySlotUI>();
        slot.PlayerIndex = playerIndex;

        // Brief scale-up animation on spawn
        Vector3 originalScale = rt.localScale;
        rt.localScale = Vector3.zero;
        slot.StartCoroutine(ScaleIn(rt, originalScale));

        return slot;
    }

    private static IEnumerator ScaleIn(RectTransform rt, Vector3 target)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * 5f;
            float s = Mathf.SmoothStep(0f, 1f, t);
            rt.localScale = target * s;
            yield return null;
        }
        rt.localScale = target;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        RectTransform rt = GetComponent<RectTransform>();
        StartCoroutine(RemovePulse(rt));
    }

    private IEnumerator RemovePulse(RectTransform rt)
    {
        float t = 0f;
        Vector3 original = rt.localScale;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * 8f;
            float s = 1f - Mathf.SmoothStep(0f, 1f, t) * 0.3f; 
            rt.localScale = original * s;
            yield return null;
        }
        Destroy(gameObject);
    }

    public static Color GetPlayerRingColor(int playerIndex)
    {
        if (playerIndex >= 0 && playerIndex < SlotColors.Length)
            return SlotColors[playerIndex];
        return Color.white;
    }
}
