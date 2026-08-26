using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Displays the portrait sprite for the currently active character.</summary>
public class CharacterPortraitUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The UI Image to update. Auto-assigned if left empty.")]
    [SerializeField] private Image portraitImage;

    /// <summary>One portrait per character, assigned in Unity inspector.</summary>
    [Header("Portraits")]
    [SerializeField] private List<Sprite> portraits = new List<Sprite>();

    /// <summary>Public accessor so other UI can read portrait sprites by index.</summary>
    public IReadOnlyList<Sprite> Portraits => portraits;

    // ── Lifecycle ──────────────────────────────────────────────────────────

    private void Awake()
    {
        if (portraitImage == null)
            portraitImage = GetComponent<Image>();
    }

    private void Update()
    {
        RefreshPortrait();
    }

    // ── Portrait update ────────────────────────────────────────────────────

    private void RefreshPortrait()
    {
        if (portraitImage == null) return;

        Sprite portrait = GetActivePortraitSprite();
        if (portrait != null && portrait != portraitImage.sprite)
            portraitImage.sprite = portrait;
    }

    /// <summary>Resolve the sprite for the currently active character.</summary>
    private static Sprite GetActivePortraitSprite()
    {
        int index = GetCharacterIndex();
        if (index < 0) return null;

        // Find any CharacterPortraitUI instance that has a portraits list set.
        var portraitUIs = UnityEngine.Object.FindObjectsByType<CharacterPortraitUI>(FindObjectsSortMode.None);
        foreach (var ui in portraitUIs)
        {
            if (ui.portraits != null && ui.portraits.Count > 0 && index < ui.portraits.Count)
                return ui.portraits[index];
        }

        // No portraits list — check MainMenu fallback.
        if (MainMenuController.Instance != null)
        {
            int menuIndex = MainMenuController.Instance.GetSelectedCharIndex();
            if (menuIndex >= 0 && MainMenuController.Instance.CharacterPrefabs != null &&
                menuIndex < MainMenuController.Instance.CharacterPrefabs.Count)
            {
                var go = MainMenuController.Instance.CharacterPrefabs[menuIndex];
                if (go == null) return null;

                var sprites = go.GetComponentsInChildren<SpriteRenderer>();
                if (sprites.Length > 0 && sprites[0].sprite != null) return sprites[0].sprite;

                var images = go.GetComponentsInChildren<Image>();
                foreach (var img in images)
                {
                    if (img.sprite != null) return img.sprite;
                }
            }
        }

        // Final fallback: selected unit's prefab art.
        if (PartyManager.IsValid && PartyManager.Instance.SelectedUnit != null)
        {
            var go = PartyManager.Instance.SelectedUnit.gameObject;
            var sprites = go.GetComponentsInChildren<SpriteRenderer>();
            if (sprites.Length > 0 && sprites[0].sprite != null) return sprites[0].sprite;

            var images = go.GetComponentsInChildren<Image>();
            foreach (var img in images)
            {
                if (img.sprite != null) return img.sprite;
            }
        }

        return null;
    }

    /// <summary>Get the active character index from game state or menu state.</summary>
    private static int GetCharacterIndex()
    {
        if (PartyManager.IsValid && PartyManager.Instance.SelectedUnit != null)
            return PartyManager.Instance.SelectedUnit.CharacterIndex;

        if (MainMenuController.Instance != null)
            return MainMenuController.Instance.GetSelectedCharIndex();

        return CharacterSelection.Index;
    }
}
