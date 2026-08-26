using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// Basic token binder for turn order UI.
public class TurnOrderTokenUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Optional UI")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image iconImage;

    [Header("Visual State")]
    [SerializeField] private Image background;
    [SerializeField] private Color normalColor = new Color(0.15f, 0.15f, 0.15f, 1f);
    [SerializeField] private Color hoverColor = new Color(0.35f, 0.35f, 0.35f, 1f);
    [SerializeField] private Color selectedColor = Color.white;
    [SerializeField] private Color clickFlashColor = Color.yellow;
    [SerializeField] private float clickFlashTime = 0.08f;

    private EnemyUnit boundEnemy;
    private Unit boundPlayer;

    private bool isHovering;
    private Coroutine flashRoutine;

    private void Awake()
    {
        if (background != null)
            normalColor = background.color;
    }

    private void OnEnable()
    {
        if (PartyManager.IsValid)
            PartyManager.Instance.OnSelectedUnitChanged += HandleSelectedUnitChanged;

        RefreshVisualState();
    }

    private void OnDisable()
    {
        if (PartyManager.IsValid)
            PartyManager.Instance.OnSelectedUnitChanged -= HandleSelectedUnitChanged;

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }
    }

    public void BindEnemy(EnemyUnit enemy)
    {
        boundEnemy = enemy;
        boundPlayer = null;

        if (nameText != null)
        {
            string displayName = enemy != null && enemy.Stats != null
                ? enemy.Stats.enemyName
                : enemy != null
                    ? enemy.name.Replace("(Clone)", "").Trim()
                    : "Enemy";

            nameText.text = displayName;
        }

        if (iconImage != null) iconImage.sprite = null;

        RefreshVisualState();
    }

    public void BindPlayer(Unit player)
    {
        boundPlayer = player;
        boundEnemy = null;

        if (nameText != null)
        {
            if (player != null)
                nameText.text = player.DisplayName;
            else
                nameText.text = "Player";
        }

        // Pull the portrait from CharacterPortraitUI's portraits list using this unit's character index.
        if (player != null)
            LoadPortraitForCharacter(GetPortraitIndexForUnit(player));

        RefreshVisualState();
    }

    /// <summary>Load a portrait sprite for a given character index onto this token's iconImage.</summary>
    private void LoadPortraitForCharacter(int index)
    {
        if (iconImage == null) return;

        if (index >= 0 && TryGetPortraitFromAnyUI(index, out Sprite portrait))
        {
            iconImage.sprite = portrait;
        }
        else
        {
            // Fallback: try the Unit's own prefab art.
            if (boundPlayer != null)
            {
                var sprites = boundPlayer.gameObject.GetComponentsInChildren<SpriteRenderer>();
                if (sprites.Length > 0 && sprites[0].sprite != null) iconImage.sprite = sprites[0].sprite;

                if (iconImage.sprite == null)
                {
                    var images = boundPlayer.gameObject.GetComponentsInChildren<Image>();
                    foreach (var img in images)
                    {
                        if (img.sprite != null) { iconImage.sprite = img.sprite; break; }
                    }
                }
            }

            // Final fallback: blank.
            if (iconImage.sprite == null) iconImage.sprite = null;
        }
    }

    /// <summary>Find any CharacterPortraitUI instance that has a portraits list set, and return index-th sprite.</summary>
    private static bool TryGetPortraitFromAnyUI(int index, out Sprite result)
    {
        var portraitUIs = UnityEngine.Object.FindObjectsByType<CharacterPortraitUI>(FindObjectsSortMode.None);
        foreach (var ui in portraitUIs)
        {
            if (ui.Portraits != null && ui.Portraits.Count > 0 && index < ui.Portraits.Count)
            {
                result = ui.Portraits[index];
                return true;
            }
        }
        result = null;
        return false;
    }

    /// <summary>Get the character index for a Unit, used to resolve portrait position in the portraits list.</summary>
    private static int GetPortraitIndexForUnit(Unit unit)
    {
        if (unit == null) return -1;
        if (PartyManager.IsValid && PartyManager.Instance.SelectedUnit == unit)
            return unit.CharacterIndex;

        // If this unit hasn't been assigned a CharacterIndex yet, try to infer from party position.
        if (PartyManager.IsValid && PartyManager.Instance.PartyUnits != null)
        {
            int idx = FindIndexOfUnit(PartyManager.Instance.PartyUnits, unit);
            if (idx >= 0 && unit.CharacterIndex == 0) // fallback: default CharacterIndex is 0
                return idx;
        }

        return unit.CharacterIndex;
    }

    /// <summary>Find the index of a Unit in an IReadOnlyList (since it doesn't expose IndexOf).</summary>
    private static int FindIndexOfUnit(IReadOnlyList<Unit> list, Unit target)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i] == target) return i;
        return -1;
    }

    public EnemyUnit GetBoundEnemy() => boundEnemy;
    public Unit GetBoundPlayer() => boundPlayer;

    public void SetHighlighted(bool value)
    {
        if (background == null)
            return;

        background.color = value ? selectedColor : normalColor;
    }

    private void HandleSelectedUnitChanged(Unit unit)
    {
        RefreshVisualState();
    }

    private void RefreshVisualState()
    {
        if (background == null)
            return;

        if (boundPlayer != null &&
            PartyManager.IsValid &&
            PartyManager.Instance.SelectedUnit == boundPlayer)
        {
            background.color = selectedColor;
            return;
        }

        background.color = isHovering ? hoverColor : normalColor;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (boundPlayer != null)
        {
            if (TurnSystem.Instance != null && !TurnSystem.Instance.IsPlayerTurn)
                return;

            if (PartyManager.IsValid)
                PartyManager.Instance.SelectUnit(boundPlayer);

            FlashClick();

            Debug.Log($"[TurnOrderTokenUI] Selected player token: {boundPlayer.DisplayName}");
            return;
        }

        if (boundEnemy == null)
            return;

        if (TurnSystem.Instance != null && !TurnSystem.Instance.IsPlayerTurn)
            return;

        HealthComponent health = boundEnemy.GetComponent<HealthComponent>();
        if (health != null)
            EnemyHealthUI.Instance?.SetTarget(health);

        CameraController2D.Instance?.SoftFocusOn(boundEnemy.transform);
        TilemapHighlighter.Instance?.ShowEnemyMoveRange(boundEnemy);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;

        if (boundPlayer != null)
        {
            RefreshVisualState();
            return;
        }

        if (boundEnemy == null)
            return;

        TilemapHighlighter.Instance?.ShowEnemyMoveRange(boundEnemy);

        HealthComponent health = boundEnemy.GetComponent<HealthComponent>();
        if (health != null)
            EnemyHealthUI.Instance?.SetTarget(health);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;

        if (boundPlayer != null)
        {
            RefreshVisualState();
            return;
        }

        TilemapHighlighter.Instance?.ClearEnemyPreview();

        if (boundEnemy == null)
            return;

        EnemyHealthUI.Instance?.ClearTarget();
    }

    private void FlashClick()
    {
        if (background == null)
            return;

        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        background.color = clickFlashColor;

        yield return new WaitForSecondsRealtime(clickFlashTime);

        flashRoutine = null;
        RefreshVisualState();
    }
}
