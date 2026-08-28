using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }

    public event Action OnGameLost;
    public event Action OnGameRestarted;

    public enum State { Playing, Lost }
    public State CurrentState { get; private set; } = State.Playing;

    private HealthComponent playerHealth;
    private bool            subscribed;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()  => LevelGenerator.OnLevelReady += OnLevelReady;
    private void OnDisable()
    {
        LevelGenerator.OnLevelReady -= OnLevelReady;
        Unsubscribe();
    }

    private void Update()
    {
        if (!subscribed) TrySubscribe();
        if (CurrentState == State.Playing && playerHealth != null && playerHealth.IsDead)
            HandleDeath();
    }

    private void OnLevelReady()
    {
        CurrentState    = State.Playing;
        subscribed      = false;
        Time.timeScale  = 1f;
        TrySubscribe();
    }

    private void TrySubscribe()
    {
        Unit unit = FindLocalOwnedUnit();
        if (unit == null) return;
        if (!unit.gameObject.activeInHierarchy) return;

        var hc = unit.GetComponent<HealthComponent>();
        if (hc == null || hc == playerHealth) return;

        Unsubscribe();
        playerHealth          = hc;
        playerHealth.OnDeath += HandleDeath;
        subscribed            = true;
    }

    private static Unit FindLocalOwnedUnit()
    {
        if (!GameManager.IsMultiplayer)
            return FindAnyObjectByType<Unit>();

        foreach (var u in FindObjectsByType<Unit>(FindObjectsSortMode.None))
        {
            var netObj = u.GetComponent<Unity.Netcode.NetworkObject>();
            if (netObj != null && netObj.IsOwner) return u;
        }
        return null;
    }

    private void Unsubscribe()
    {
        if (playerHealth != null) playerHealth.OnDeath -= HandleDeath;
        playerHealth = null;
        subscribed   = false;
    }

    private void HandleDeath()
    {
        if (CurrentState != State.Playing) return;

        // In party mode: only trigger game over when ALL party members are knocked out
        if (PartyManager.Instance != null && PartyManager.Instance.PartyUnits.Count > 0)
        {
            bool allDown = PartyManager.Instance.PartyUnits.All(u =>
            {
                var hc = u.GetComponent<HealthComponent>();
                return hc != null && (hc.IsKnockedOut || hc.IsDead);
            });

            if (!allDown) return; // Some party members are still alive — let knockout handle it
        }

        CurrentState = State.Lost;
        OnGameLost?.Invoke();
        LoseScreen.Show();
    }

    public void NotifyLevelAdvanced()
    {
        // Revive all knocked-out party members when a room is cleared
        PartyManager.Instance?.ReviveAllKnockedOutUnits();

        CurrentState    = State.Playing;
        subscribed      = false;
        Time.timeScale  = 1f;
        OnGameRestarted?.Invoke();
    }

    public void RestartGame(bool resetProgress = true)
    {
        Time.timeScale  = 1f;
        CurrentState    = State.Playing;
        subscribed      = false;
        Unsubscribe();
        OnGameRestarted?.Invoke();

        if (GameManager.IsMultiplayer)
        {
            var bridge = UnityEngine.Object.FindAnyObjectByType<LevelSyncBridge>();
            if (bridge != null && bridge.IsServer)
            {
                if (resetProgress) WaveManager.Instance?.ResetToLevel1();
                Unity.Netcode.NetworkManager.Singleton.SceneManager.LoadScene(
                    SceneManager.GetActiveScene().name,
                    LoadSceneMode.Single);
            }
        }
        else
        {
            if (resetProgress) WaveManager.Instance?.ResetToLevel1();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}