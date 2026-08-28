using UnityEngine;


public class PlayerAnimator : UnitAnimator
{
    [Header("Player Parameters")]
    [SerializeField] private string paramStaminaEmpty    = "staminaEmpty";
    [SerializeField] private string paramRoomTransition  = "roomTransition";

    private int hashStaminaEmpty, hashRoomTransition, hashClassAbility;

    private SpriteRenderer   spriteRenderer;
    private PlayerStats      playerStats;
    private Color            originalSpriteColor;
    private Animator         cachedAnim;

    protected override void Awake()
    {
        base.Awake();
        playerStats = GetComponent<PlayerStats>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
            originalSpriteColor = spriteRenderer.color;

        hashStaminaEmpty   = Animator.StringToHash(paramStaminaEmpty);
        hashRoomTransition = Animator.StringToHash(paramRoomTransition);
    }

    /// <summary>Grey out sprite + pause animation when knocked down. Call false to restore.</summary>
    public void OnKnockdownChanged(bool knockedOut)
    {
        if (knockedOut)
        {
            // Disable animator first to freeze state, then grey out color
            anim.enabled = false;
            if (spriteRenderer != null)
            {
                var c = spriteRenderer.color;
                float gray = 0.5f * (c.r + c.g + c.b) / 3f;
                spriteRenderer.color = new Color(gray, gray, gray, c.a);
            }
        }
        else
        {
            // Clear death-state flag BEFORE re-enabling so Animator evaluates idle instead of stuck on death frame
            anim.SetBool(hashIsDead, false);

            if (spriteRenderer != null)
                spriteRenderer.color = originalSpriteColor;

            // Re-enable animator — it now sees isDead=false and returns to idle
            anim.enabled = true;
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        RoomManager.OnAnyRoomChanged += OnRoomChanged;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        RoomManager.OnAnyRoomChanged -= OnRoomChanged;
    }

    // Call this every time stamina changes (e.g. after MoveAction or CombatAction)
    public void RefreshStaminaState()
    {
        bool empty = playerStats != null && playerStats.currentStamina <= 0;
        anim.SetBool(hashStaminaEmpty, empty);
    }


    private void OnRoomChanged(LevelGenerator.PlacedRoom _) =>
        anim.SetTrigger(hashRoomTransition);
}