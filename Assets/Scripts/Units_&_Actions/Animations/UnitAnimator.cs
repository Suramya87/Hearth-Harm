using UnityEngine;

[RequireComponent(typeof(Animator))]
public class UnitAnimator : MonoBehaviour
{
    [Header("Animator Parameters")]
    [SerializeField] private string paramIsMoving    = "isMoving";
    [SerializeField] private string paramFacingNorth = "facingNorth";
    [SerializeField] private string paramFacingSouth = "facingSouth";
    [SerializeField] private string paramFacingEast  = "facingEast";
    [SerializeField] private string paramFacingWest  = "facingWest";
    [SerializeField] private string paramAttack      = "attack";
    [SerializeField] private string paramHurt        = "hurt";
    [SerializeField] private string paramIsDead      = "isDead";

    protected Animator      anim;
    private   HealthComponent health;

    protected int hashIsMoving,
                  hashFacingNorth, hashFacingSouth, hashFacingEast, hashFacingWest,
                  hashAttack, hashHurt, hashIsDead;

    protected virtual void Awake()
    {
        anim   = GetComponent<Animator>();
        health = GetComponent<HealthComponent>();

        hashIsMoving    = Animator.StringToHash(paramIsMoving);
        hashFacingNorth = Animator.StringToHash(paramFacingNorth);
        hashFacingSouth = Animator.StringToHash(paramFacingSouth);
        hashFacingEast  = Animator.StringToHash(paramFacingEast);
        hashFacingWest  = Animator.StringToHash(paramFacingWest);
        hashAttack      = Animator.StringToHash(paramAttack);
        hashHurt        = Animator.StringToHash(paramHurt);
        hashIsDead      = Animator.StringToHash(paramIsDead);
    }

    protected virtual void OnEnable()
    {
        if (health == null) return;
        health.OnDeath         += OnDeath;
        health.OnHealthChanged += OnHealthChanged;
    }

    protected virtual void OnDisable()
    {
        if (health == null) return;
        health.OnDeath         -= OnDeath;
        health.OnHealthChanged -= OnHealthChanged;
    }

    // ── Public API ─────────────────────────────────────────────────────────

    public void SetMoving(bool moving)
    {
        anim.SetBool(hashIsMoving, moving);
    }

    public void SetFacing(Vector2Int dir)
    {
        if (dir == Vector2Int.zero) return;

        // Determine the dominant axis. When |x| == |y| (exact diagonal),
        // prefer horizontal to avoid north/south flicker on equal components.
        bool east   = dir.x > 0 && Mathf.Abs(dir.x) >= Mathf.Abs(dir.y);
        bool west   = dir.x < 0 && Mathf.Abs(dir.x) >= Mathf.Abs(dir.y);
        bool north  = dir.y > 0 && Mathf.Abs(dir.y) > Mathf.Abs(dir.x);
        bool south  = dir.y < 0 && Mathf.Abs(dir.y) > Mathf.Abs(dir.x);

        anim.SetBool(hashFacingNorth, north);
        anim.SetBool(hashFacingSouth, south);
        anim.SetBool(hashFacingEast,  east);
        anim.SetBool(hashFacingWest,  west);

        anim.Update(0);
    }

    public void TriggerAttack() =>
        anim.SetTrigger(hashAttack);


    private void OnDeath() =>
        anim.SetBool(hashIsDead, true);

    public void ClearDeathState() =>
        anim.SetBool(hashIsDead, false);

    private void OnHealthChanged(int current, int max)
    {
        if (current < max && current > 0)
            anim.SetTrigger(hashHurt);
    }
}