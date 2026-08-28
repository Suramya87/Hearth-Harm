// ─────────────────────────────────────────────────────────────────────────────
// HealthComponent.cs
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections;
using UnityEngine;

public class HealthComponent : MonoBehaviour
{
    [Header("Damage Numbers")]
    [SerializeField] private DamageNumber damageNumberPrefab;
    [SerializeField] private Vector3 damageNumberOffset = new(0f, 0.6f, 0f);

    [Header("Damage Flash")]
    [SerializeField] private GameObject damageFlashObject;
    [SerializeField] private float flashDuration = 0.15f;
    [SerializeField] private int flashCount = 1;

    [Header("Health Settings")]
    [Min(1)][SerializeField] private int maxHealth = 100;
    [Min(0)][SerializeField] private int startingHealth = 0;

    [Header("Death Behaviour")]
    [SerializeField] private bool destroyOnDeath = false;
    [SerializeField, Min(0f)] private float deathDelay = 0f;

    [Header("Debug")]
    [SerializeField] private int _currentHealth;
    private int _lastKnownHealth;

    public event Action<int, int> OnHealthChanged;
    public event Action OnDeath;
    public event Action OnKnockedOut;
    public event Action OnRevived;

    public int CurrentHealth => _currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDead => _currentHealth <= 0;
    private bool isKnockedOut;
    public bool IsKnockedOut => isKnockedOut;
    public float HealthPercent => maxHealth > 0 ? (float)_currentHealth / maxHealth : 0f;

    private SpriteRenderer flashRenderer;
    private Coroutine flashRoutine;

    private void Awake()
    {
        var provider = GetComponent<IHasHealth>();
        if (provider != null)
        {
            maxHealth = provider.GetMaxHealth();
            _currentHealth = maxHealth;
        }
        else
        {
            _currentHealth = startingHealth > 0
                ? Mathf.Min(startingHealth, maxHealth)
                : maxHealth;
        }
        _lastKnownHealth = _currentHealth;

        if (damageFlashObject)
        {
            flashRenderer = damageFlashObject.GetComponent<SpriteRenderer>();
            damageFlashObject.SetActive(true);
            SetFlashAlpha(0f);
        }
    }

#if UNITY_EDITOR
    private void Update()
    {
        if (_currentHealth != _lastKnownHealth) SetHealth(_currentHealth);
    }
#endif

    public void TakeDamage(int amount)
    {
        if (IsDead || amount <= 0 || !gameObject.activeInHierarchy) return;
        _currentHealth = Mathf.Max(0, _currentHealth - amount);
        _lastKnownHealth = _currentHealth;
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
        SpawnDamageNumber(amount);
        TriggerFlash();
        if (_currentHealth == 0 && !isKnockedOut)
        {
            isKnockedOut = true;
            OnKnockedOut?.Invoke();
        }
        if (_currentHealth == 0) Die();
    }

    public void Heal(int amount)
    {
        if (IsDead || amount <= 0) return;
        _currentHealth = Mathf.Min(maxHealth, _currentHealth + amount);
        _lastKnownHealth = _currentHealth;
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
    }

    public void SetHealth(int value)
    {
        _currentHealth = Mathf.Clamp(value, 0, maxHealth);
        _lastKnownHealth = _currentHealth;
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
        if (_currentHealth == 0 && !isKnockedOut)
        {
            isKnockedOut = true;
            OnKnockedOut?.Invoke();
        }
        if (_currentHealth == 0) Die();
    }

    public void InitializeHealth(int newMax)
    {
        maxHealth = Mathf.Max(1, newMax);
        _currentHealth = maxHealth;
        _lastKnownHealth = _currentHealth;
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
    }

    /// <summary>Revives a knocked-out unit to the given HP.</summary>
    public void Revive(int hp)
    {
        isKnockedOut = false;
        _currentHealth = Mathf.Clamp(hp, 1, maxHealth);
        _lastKnownHealth = _currentHealth;
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);

        // Restore grey flash renderer if it was dimmed
        if (flashRenderer != null)
        {
            var c = flashRenderer.color;
            c.a = 1f;
            flashRenderer.color = c;
        }

        OnRevived?.Invoke();
    }

    private void SpawnDamageNumber(int amount)
    {
        if (!damageNumberPrefab) return;
        var dmg = Instantiate(damageNumberPrefab,
            transform.position + damageNumberOffset, Quaternion.identity);
        dmg.Initialize(amount);
    }

    private void TriggerFlash()
    {
        if (!flashRenderer || !gameObject.activeInHierarchy) return;
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        float half = flashDuration * 0.5f;
        for (int i = 0; i < flashCount; i++)
        {
            yield return Fade(0f, 1f, half);
            yield return Fade(1f, 0f, half);
        }
        flashRoutine = null;
    }

    private IEnumerator Fade(float from, float to, float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            SetFlashAlpha(Mathf.Lerp(from, to, t / dur));
            yield return null;
        }
        SetFlashAlpha(to);
    }

    private void SetFlashAlpha(float a)
    {
        if (flashRenderer == null) return;
        var c = flashRenderer.color; c.a = a; flashRenderer.color = c;
    }

    private void Die()
    {
        OnDeath?.Invoke();
        var enemy = GetComponent<EnemyUnit>();
        if (enemy != null) return; // EnemyUnit.HandleDeath handles destruction

        // Don't auto-destroy when knocked out — let the game/room-clear handle it
        if (!isKnockedOut)
        {
            if (deathDelay > 0f) Invoke(nameof(ExecuteDeath), deathDelay);
            else ExecuteDeath();
        }
    }

    private void ExecuteDeath()
    {
        if (GetComponent<BossUnit>() != null) return;
        if (destroyOnDeath) Destroy(gameObject);
        else gameObject.SetActive(false);
    }
}

public interface IHasHealth { int GetMaxHealth(); }