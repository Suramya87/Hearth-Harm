using UnityEngine;


public class PlayerAnimator : UnitAnimator
{
    [Header("Player Parameters")]
    [SerializeField] private string paramStaminaEmpty    = "staminaEmpty";
    [SerializeField] private string paramRoomTransition  = "roomTransition";

    private int hashStaminaEmpty, hashRoomTransition, hashClassAbility;

    private SpriteRenderer   spriteRenderer;
    private PlayerStats      playerStats;

    protected override void Awake()
    {
        base.Awake();
        playerStats = GetComponent<PlayerStats>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        hashStaminaEmpty   = Animator.StringToHash(paramStaminaEmpty);
        hashRoomTransition = Animator.StringToHash(paramRoomTransition);
    }

    /// <summary>Grey out sprite + pause animation when knocked down. Call false to restore.</summary>
    public void OnKnockdownChanged(bool knockedOut)
    {
        anim.enabled = !knockedOut;
        if (spriteRenderer != null)
        {
            var c = spriteRenderer.color;
            float gray = 0.5f * (c.r + c.g + c.b) / 3f;
            spriteRenderer.color = knockedOut ? new Color(gray, gray, gray, c.a) : c;
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