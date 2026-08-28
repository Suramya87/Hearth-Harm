using System;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(HealthComponent))]
public class NetworkedHealthBridge : NetworkBehaviour
{
    private HealthComponent health;
    public  event Action OnKnockedOut;

    private void Awake()
    {
        health = GetComponent<HealthComponent>();
    }

    // ── Static helpers ─────────────────────────────────────────────────────

    public static void TakeDamage(GameObject target, int amount)
    {
        if (!GameManager.IsMultiplayer)
        {
            target.GetComponent<HealthComponent>()?.TakeDamage(amount);
            return;
        }

        var bridge = target.GetComponent<NetworkedHealthBridge>();
        if (bridge != null)
            bridge.TakeDamageInstance(amount);
        else
            target.GetComponent<HealthComponent>()?.TakeDamage(amount);
    }

    public static void Heal(GameObject target, int amount)
    {
        if (!GameManager.IsMultiplayer)
        {
            target.GetComponent<HealthComponent>()?.Heal(amount);
            return;
        }

        var bridge = target.GetComponent<NetworkedHealthBridge>();
        if (bridge != null)
            bridge.Heal(amount);
        else
            target.GetComponent<HealthComponent>()?.Heal(amount);
    }

    // ── Instance API ───────────────────────────────────────────────────────

    public void TakeDamageInstance(int amount)
    {
        if (!GameManager.IsMultiplayer)
        {
            health.TakeDamage(amount);
            return;
        }

        if (!IsSpawned)
        {
            health.TakeDamage(amount);
            return;
        }

        bool wasKnockedOut = health.IsKnockedOut;
        RequestTakeDamageServerRpc(amount);

        // Fire OnKnockedOut locally only when this instance crossed into knockdown state
        if (!wasKnockedOut && health.IsKnockedOut)
            OnKnockedOut?.Invoke();
    }

    public void Heal(int amount)
    {
        if (!GameManager.IsMultiplayer)
        {
            health.Heal(amount);
            return;
        }
        RequestHealServerRpc(amount);
    }

    // ── Server RPCs ────────────────────────────────────────────────────────

    [ServerRpc(RequireOwnership = false)]
    private void RequestTakeDamageServerRpc(int amount, ServerRpcParams rpcParams = default)
    {
        if (health.IsDead) return;
        health.TakeDamage(amount);
        bool knockedOutThisTick = !health.IsKnockedOut && health.CurrentHealth <= 0;
        SyncHealthClientRpc(health.CurrentHealth, health.MaxHealth, amount, health.IsDead, knockedOutThisTick);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestHealServerRpc(int amount)
    {
        if (health.IsDead) return;
        health.Heal(amount);
        SyncHealthClientRpc(health.CurrentHealth, health.MaxHealth, 0, false, false);
    }

    // ── Client RPCs ────────────────────────────────────────────────────────

    [ClientRpc]
    private void SyncHealthClientRpc(int currentHp, int maxHp, int damageDealt, bool isDead, bool wasKnockedOutOnServer)
    {
        if (IsServer) return;

        // Fire OnKnockedOut on clients where the server says knocked out but we haven't synced yet
        if (wasKnockedOutOnServer && !health.IsKnockedOut)
            OnKnockedOut?.Invoke();

        // Sync health exactly — avoid redundant TakeDamage that would re-fire OnDeath/Knockout
        if (health.CurrentHealth != currentHp)
            health.SetHealth(currentHp);
    }
}