using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PartyFollowManager : MonoBehaviour
{
    // ── Singleton ────────────────────────────────────────────────────────

    private static PartyFollowManager instance;

    public static PartyFollowManager Instance
    {
        get => instance;
        set => instance = value;
    }

    public static PartyFollowManager GetOrCreateInstance()
    {
        if (instance != null) return instance;
        var go = new GameObject("PartyFollowManager");
        instance = go.AddComponent<PartyFollowManager>();
        DontDestroyOnLoad(go);
        Debug.Log("[PartyFollowManager] Created fallback instance via GetOrCreateInstance.");
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    [SerializeField] private float followerMoveSpeed = 10f;

    private readonly HashSet<Unit> subscribedUnits = new();

    private static bool diagnosticPrinted;

    /// <summary>Fired when follower grid positions change, so MoveAction can refresh its BFS cache.</summary>
    public static event System.Action OnFollowerPositionsChanged;

    private void FireFollowerPositionChanged() => OnFollowerPositionsChanged?.Invoke();

    private void OnEnable()
    {
        Debug.Log($"[PartyFollowManager] Enabled — GameMode={GameManager.Mode} IsMultiplayer={GameManager.IsMultiplayer}");
        StartCoroutine(SubscribeWhenReady());
    }

    private IEnumerator SubscribeWhenReady()
    {
        while (!PartyManager.IsValid)
            yield return null;

        PartyManager.Instance.OnPartyChanged += HookLateJoiner;
        PartyManager.Instance.OnSelectedUnitChanged += HandleSelectedUnitChanged;

        Debug.Log($"[PartyFollowManager] SubscribeWhenReady started — waiting for ≥2 party units (current: {PartyManager.Instance.PartyUnits.Count})");

        while (PartyManager.Instance.PartyUnits.Count < 2)
            yield return null;

        Debug.Log($"[PartyFollowManager] Found ≥2 party units. Now subscribing all {PartyManager.Instance.PartyUnits.Count}");
        foreach (Unit unit in PartyManager.Instance.PartyUnits)
            SubscribeUnit(unit);

        Debug.Log($"[PartyFollowManager] Ready! Subscribed {subscribedUnits.Count}/{PartyManager.Instance.PartyUnits.Count} units.");

        if (!diagnosticPrinted)
        {
            diagnosticPrinted = true;
            PrintStartupDiagnostic();
        }

    }

    private void OnDisable()
    {
        if (PartyManager.IsValid)
            PartyManager.Instance.OnPartyChanged -= HookLateJoiner;
        PartyManager.Instance.OnSelectedUnitChanged -= HandleSelectedUnitChanged;

        subscribedUnits.Clear();
    }

    private void HookLateJoiner()
    {
        if (!PartyManager.IsValid) return;

        Debug.Log($"[PartyFollowManager] HookLateJoiner called — PartyUnits={PartyManager.Instance.PartyUnits.Count}, subscribed={subscribedUnits.Count}");

        PurgeStaleReferences();

        foreach (Unit unit in PartyManager.Instance.PartyUnits)
            SubscribeUnit(unit);
    }

    private Unit previousSelectedLeader;

    private void HandleSelectedUnitChanged(Unit unit)
    {
        var leaderHasMove = unit != null && unit.GetMoveAction() != null;
        if (leaderHasMove && unit != previousSelectedLeader)
        {
            ClearFollowerQueue();
            previousSelectedLeader = unit;
        }

        SubscribeUnit(PartyManager.Instance.SelectedUnit);
    }

    private void PurgeStaleReferences()
    {
        var snapshot = new List<Unit>(subscribedUnits);
        foreach (Unit stale in snapshot)
        {
            if (!stale || !stale.gameObject.activeInHierarchy)
                subscribedUnits.Remove(stale);
        }
    }

    private void SubscribeUnit(Unit unit)
    {
        if (!PartyManager.IsValid || unit == null) return;

        if (subscribedUnits.Contains(unit)) return;

        MoveAction move = unit.GetMoveAction();
        if (move != null)
        {
            move.OnWorldStepCompleted += HandleWorldStepCompleted;
            subscribedUnits.Add(unit);
            Debug.Log($"[PartyFollowManager] Subscribed {unit.name} — GameManager.Mode={GameManager.Mode}, moveAction={move != null}");
        }
        else
        {
            Debug.LogWarning($"[PartyFollowManager] Failed to subscribe {unit.name}: MoveAction is null!");
        }
    }


    private IEnumerator MoveFollowerToWorldCell(Unit follower, Vector3 cellWorld)
    {
        Vector2 visualOffset = follower.GetVisualOffset();

        Vector3 target = new Vector3(
            cellWorld.x + visualOffset.x,
            cellWorld.y + visualOffset.y,
            follower.transform.position.z
        );

        var unitAnimator = follower.GetComponent<UnitAnimator>();

        // Determine facing direction from current position to target.
        float dx = target.x - follower.transform.position.x;
        float dy = target.y - follower.transform.position.y;
        Vector2Int facingDir = new Vector2Int(
            Mathf.Abs(dx) > 0.01f ? (int)Mathf.Sign(dx) : 0,
            Mathf.Abs(dy) > 0.01f ? (int)Mathf.Sign(dy) : 0);

        // Mirror what MoveAction does for the leader: set moving + facing at start.
        if (unitAnimator != null)
        {
            unitAnimator.SetMoving(true);
            unitAnimator.SetFacing(facingDir);
        }

        while (Vector2.Distance(follower.transform.position, target) > 0.01f)
        {
            follower.transform.position = Vector3.MoveTowards(
                follower.transform.position,
                target,
                followerMoveSpeed * Time.deltaTime
            );

            yield return null;
        }

        follower.transform.position = target;

        SyncFollowerGridToWorld(follower, cellWorld);

        // Mirror what MoveAction does for the leader: stop moving + keep facing at end.
        if (unitAnimator != null)
        {
            unitAnimator.SetMoving(false);
            unitAnimator.SetFacing(facingDir);
        }
    }

    private void SyncFollowerGridToWorld(Unit follower, Vector3 cellWorld)
    {
        RoomGrid grid = UnifiedWorldGrid.Instance != null
            ? UnifiedWorldGrid.Instance.GetOwnerAt(cellWorld)
            : follower.GetCurrentRoomGrid();

        if (grid == null)
            return;

        GridPosition gp = grid.GetGridPosition(cellWorld);
        follower.PlaceInRoomNoMove(grid, gp);
    }

    private bool IsInCombatRoom(Unit unit)
    {
        RoomGrid room = unit.GetCurrentRoomGrid();

        if (room == null || EnemyManager.Instance == null)
            return false;

        return EnemyManager.Instance.GetEnemiesInRoom(room).Count > 0;
    }

    public void SnapPartyNearLeader()
    {
        if (!PartyManager.IsValid)
            return;

        Unit leader = PartyManager.Instance.SelectedUnit;
        if (leader == null)
            return;

        RoomGrid room = leader.GetCurrentRoomGrid();
        if (room == null)
            return;

        GridPosition leaderPos = leader.GetGridPosition();

        int placed = 0;

        foreach (Unit unit in PartyManager.Instance.PartyUnits)
        {
            if (unit == null || unit == leader)
                continue;

            GridPosition spot = FindNearbyOpenTile(room, leaderPos, placed + 1);

            unit.PlaceInRoom(room, spot);
            placed++;
        }
    }

    private GridPosition FindNearbyOpenTile(RoomGrid room, GridPosition center, int preferredDistance)
    {
        for (int radius = 1; radius <= 4; radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (Mathf.Abs(dx) + Mathf.Abs(dy) != radius)
                        continue;

                    GridPosition test = new GridPosition(center.x + dx, center.y + dy);

                    if (!room.IsValidGridPosition(test))
                        continue;

                    if (!room.IsWalkableIgnoreOccupancy(test))
                        continue;

                    if (room.HasAnyUnitOnGridPosition(test))
                        continue;

                    return test;
                }
        }

        return center;
    }

    private readonly Queue<Vector3> followerStepQueue = new();
    private bool followerIsMoving;
    private Coroutine followerMoveRoutine;
    private void HandleWorldStepCompleted(Unit leader, Vector3 leaderStepWorld)
    {
        Debug.Log($"[PartyFollowManager] OnWorldStepCompleted called — " +
            $"Mode={GameManager.RawModeString} IsOffline={GameManager.Mode == GameMode.Offline} " +
            $"leaderNull={leader == null} leaderSelected={(leader != null && PartyManager.IsValid && leader == PartyManager.Instance.SelectedUnit)} " +
            $"subscribed={subscribedUnits.Contains(leader)}");

        if (GameManager.Mode != GameMode.Offline)
        {
            Debug.Log($"[PartyFollowManager] REJECTED — GameManager.Mode={GameManager.Mode}");
            return;
        }

        if (leader == null)
        {
            Debug.LogWarning("[PartyFollowManager] REJECTED — leader is null");
            return;
        }

        if (!PartyManager.IsValid)
        {
            Debug.LogWarning("[PartyFollowManager] REJECTED — PartyManager not valid");
            return;
        }

        if (leader != PartyManager.Instance.SelectedUnit)
        {
            Debug.Log($"[PartyFollowManager] Ignored step from non-selected unit: {leader.name} (selected={PartyManager.Instance.SelectedUnit?.name})");
            return;
        }

        if (IsInCombatRoom(leader) && IsWholePartyInLeaderRoom(leader))
        {
            Debug.Log($"[PartyFollowManager] Skipping — in combat with whole party present");
            return;
        }

        int before = followerStepQueue.Count;
        followerStepQueue.Enqueue(leaderStepWorld);
        Debug.Log($"[PartyFollowManager] Step accepted → queue {before}→{followerStepQueue.Count}, leader={leader.name}, stepWorld={leaderStepWorld}, subscribedUnits={subscribedUnits.Count}");

        if (!followerIsMoving)
            followerMoveRoutine = StartCoroutine(ProcessFollowerStepQueue(leader));
    }

    private IEnumerator ProcessFollowerStepQueue(Unit leader)
    {
        followerIsMoving = true;

        while (followerStepQueue.Count > 1)
        {
            Vector3 nextCell = followerStepQueue.Dequeue();

            foreach (Unit follower in PartyManager.Instance.PartyUnits)
            {
                if (follower == null || follower == leader)
                    continue;

                yield return MoveFollowerToWorldCell(follower, nextCell);
            }
        }

        followerIsMoving = false;
    }

    public void SnapFollowersToEntrance(RoomGrid room, LevelGenerator.Direction entranceDir)
    {
        ClearFollowerQueue();

        if (!PartyManager.IsValid)
            return;

        Unit leader = PartyManager.Instance.SelectedUnit;

        if (leader == null || room == null)
            return;

        GridPosition leaderPos = room.GetGridPosition(leader.transform.position);
        int placed = 0;

        foreach (Unit follower in PartyManager.Instance.PartyUnits)
        {
            if (follower == null || follower == leader)
                continue;

            GridPosition spot = FindEntranceSideTile(room, leaderPos, entranceDir, placed);

            follower.PlaceInRoom(room, spot);
            placed++;
        }
    }

    private GridPosition FindEntranceSideTile(
        RoomGrid room,
        GridPosition leaderPos,
        LevelGenerator.Direction entranceDir,
        int followerIndex)
    {
        List<GridPosition> candidates = new();

        switch (entranceDir)
        {
            case LevelGenerator.Direction.North:
            case LevelGenerator.Direction.South:
                // Door is vertical movement, so line followers horizontally beside leader.
                candidates.Add(new GridPosition(leaderPos.x - 1 - followerIndex, leaderPos.y));
                candidates.Add(new GridPosition(leaderPos.x + 1 + followerIndex, leaderPos.y));
                candidates.Add(new GridPosition(leaderPos.x, leaderPos.y - 1));
                candidates.Add(new GridPosition(leaderPos.x, leaderPos.y + 1));
                break;

            case LevelGenerator.Direction.East:
            case LevelGenerator.Direction.West:
                // Door is horizontal movement, so line followers vertically beside leader.
                candidates.Add(new GridPosition(leaderPos.x, leaderPos.y - 1 - followerIndex));
                candidates.Add(new GridPosition(leaderPos.x, leaderPos.y + 1 + followerIndex));
                candidates.Add(new GridPosition(leaderPos.x - 1, leaderPos.y));
                candidates.Add(new GridPosition(leaderPos.x + 1, leaderPos.y));
                break;
        }

        foreach (GridPosition test in candidates)
        {
            if (!room.IsValidGridPosition(test))
                continue;

            if (!room.IsWalkableIgnoreOccupancy(test))
                continue;

            if (room.HasAnyUnitOnGridPosition(test))
                continue;

            return test;
        }

        return FindNearbyOpenTile(room, leaderPos, followerIndex + 1);
    }

    private void ClearFollowerQueue()
    {
        followerStepQueue.Clear();
        followerIsMoving = false;

        if (followerMoveRoutine != null)
        {
            StopCoroutine(followerMoveRoutine);
            followerMoveRoutine = null;
        }
    }

    public void StopFollowingNow()
    {
        ClearFollowerQueue();
    }

    /// <summary>Prints a one-shot diagnostic of party mode state.</summary>
    private void PrintStartupDiagnostic()
    {
        var sb = new System.Text.StringBuilder();

        var gmInstanceStr = GameManager.Instance != null ? "exists" : "NULL";
        var partyMgrStr = PartyManager.IsValid ? "valid" : "invalid/missing";
        var partyCountStr = PartyManager.IsValid ? PartyManager.Instance.PartyUnits.Count.ToString() : "N/A";

        sb.Append("[PartyFollowManager DIAGNOSTIC] PartyMode state:\n");
        sb.Append($"  GameManager.Instance = {gmInstanceStr}\n");
        sb.Append($"  GameManager.Mode     = {GameManager.RawModeString}\n");
        sb.Append($"  IsMultiplayer        = {GameManager.IsMultiplayer}\n");
        sb.Append($"  PartyManager         = {partyMgrStr}\n");
        sb.Append($"  PartyUnits.Count     = {partyCountStr}\n");
        sb.Append($"  SubscribedCount      = {subscribedUnits.Count}\n");

        if (PartyManager.IsValid)
        {
            for (int i = 0; i < PartyManager.Instance.PartyUnits.Count; i++)
            {
                var u = PartyManager.Instance.PartyUnits[i];
                if (u == null) { sb.Append($"    Unit[{i}] = NULL\n"); continue; }
                var ma = u.GetMoveAction();
                var moveStr = ma != null ? "yes" : "NULL";
                var psStr = u.GetComponent<PlayerStats>() != null ? "yes" : "NULL";
                var gridStr = u.IsInitialized() ? u.GetGridPosition().ToString() : "not init";
                var roomStr = u.GetCurrentRoomGrid() != null ? u.GetCurrentRoomGrid().gameObject.name : "NULL";
                sb.Append($"    Unit[{i}] = {u.name} " +
                    $"MoveAction={moveStr} " +
                    $"PlayerStats={psStr} " +
                    $"grid={gridStr} " +
                    $"room={roomStr}\n");
            }
        }

        var turnStr = TurnSystem.Instance != null ? "exists" : "NULL";
        var roomMgrStr = RoomManager.Instance != null ? "exists" : "NULL";
        var uasStr = UnitActionSystem.Instance != null ? "exists" : "NULL";
        var enemyStr = EnemyManager.Instance != null ? "exists" : "NULL";

        sb.Append($"  TurnSystem           = {turnStr}\n");
        sb.Append($"  RoomManager          = {roomMgrStr}\n");
        sb.Append($"  UnitActionSystem     = {uasStr}\n");
        sb.Append($"  EnemyManager         = {enemyStr}\n");

        Debug.Log(sb.ToString());
    }

    private bool IsWholePartyInLeaderRoom(Unit leader)
    {
        if (!PartyManager.IsValid || leader == null)
            return true;

        RoomGrid leaderRoom = leader.GetCurrentRoomGrid();
        if (leaderRoom == null)
            return false;

        foreach (Unit unit in PartyManager.Instance.PartyUnits)
        {
            if (unit == null)
                continue;

            if (unit.GetCurrentRoomGrid() != leaderRoom)
                return false;
        }

        return true;
    }

}