using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PartyManager : MonoBehaviour
{
    public static PartyManager Instance { get; private set; }

    /// <summary>Checks that Instance is non-null AND not a destroyed object.</summary>
    public static bool IsValid => Instance != null && Instance.gameObject != null;

    public event Action<Unit> OnSelectedUnitChanged;
    public event Action OnPartyChanged;
    public event Action OnKnockedOutUnitsRevived;

    [Header("Debug")]
    [SerializeField] private bool useDebugStartingUnit = true;
    [SerializeField] private int debugStartingUnitIndex = 0;

    private readonly List<Unit> partyUnits = new();

    private Unit selectedUnit;
    private Coroutine debugStartCoroutine;

    public Unit SelectedUnit => selectedUnit;
    public IReadOnlyList<Unit> PartyUnits => partyUnits;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SelectDebugIndex(0);
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SelectDebugIndex(1);
        }
    }

    private void CycleSelectedUnit()
    {
        if (partyUnits.Count <= 1)
            return;

        int currentIndex = partyUnits.IndexOf(selectedUnit);

        if (currentIndex < 0)
        {
            SelectUnit(partyUnits[0]);
            return;
        }

        int nextIndex = (currentIndex + 1) % partyUnits.Count;

        SelectUnit(partyUnits[nextIndex]);
    }

    // ── Knockdown handling ───────────────────────────────────────────────────

    private void OnPartyMemberKnockedOut()
    {
        // Grey out all party members' sprites and pause animations for those knocked out
        foreach (var unit in partyUnits)
        {
            var hc = unit.GetComponent<HealthComponent>();
            if (hc != null && hc.IsKnockedOut)
            {
                var animator = unit.GetComponent<PlayerAnimator>();
                animator?.OnKnockdownChanged(true);
            }
        }

        // Auto-switch selection if the currently selected unit was knocked out
        if (selectedUnit != null && PartyManager.IsValid)
        {
            var selHc = selectedUnit.GetComponent<HealthComponent>();
            if (selHc != null && selHc.IsKnockedOut)
            {
                // Find first alive party member, fallback to any unit
                selectedUnit = partyUnits.FirstOrDefault(u =>
                {
                    var uh = u.GetComponent<HealthComponent>();
                    return uh != null && !uh.IsKnockedOut;
                }) ?? partyUnits[0];

                SelectUnit(selectedUnit);
            }
        }
    }

    public void ReviveAllKnockedOutUnits()
    {
        foreach (var unit in partyUnits.ToList())
        {
            var hc = unit.GetComponent<HealthComponent>();
            if (hc != null && hc.IsKnockedOut)
            {
                hc.Revive(1);

                // Restore visuals: re-enable animation + restore original color
                var animator = unit.GetComponent<PlayerAnimator>();
                animator?.OnKnockdownChanged(false);
            }
        }
        OnKnockedOutUnitsRevived?.Invoke();
    }

    public void RegisterUnit(Unit unit)
    {
        if (this == null || gameObject == null)
        {
            Debug.LogWarning("[PartyManager] RegisterUnit called on destroyed instance — skipping.");
            return;
        }

        if (unit == null)
            return;

        if (!partyUnits.Contains(unit))
        {
            partyUnits.Add(unit);
            OnPartyChanged?.Invoke();
        }

        // Subscribe to knockdown events so we can switch selection and apply visuals
        var hc = unit.GetComponent<HealthComponent>();
        if (hc != null) hc.OnKnockedOut += OnPartyMemberKnockedOut;

        Debug.Log($"[PartyManager] Registered {unit.name}. Party count = {partyUnits.Count}");

        if (selectedUnit == null)
            SelectUnit(unit);

        if (useDebugStartingUnit)
        {
            if (debugStartCoroutine != null)
                StopCoroutine(debugStartCoroutine);

            debugStartCoroutine = StartCoroutine(ApplyDebugStartingUnitNextFrame());
        }
    }

    private IEnumerator ApplyDebugStartingUnitNextFrame()
    {
        yield return null;
        yield return null;

        if (!useDebugStartingUnit)
            yield break;

        if (partyUnits.Count == 0)
            yield break;

        int index = Mathf.Clamp(debugStartingUnitIndex, 0, partyUnits.Count - 1);

        SelectUnit(partyUnits[index]);

        Debug.Log($"[PartyManager] Debug starting unit selected index {index}: {partyUnits[index].name}");

        debugStartCoroutine = null;
    }

    private void SelectDebugIndex(int index)
    {
        if (partyUnits.Count == 0)
            return;

        index = Mathf.Clamp(index, 0, partyUnits.Count - 1);

        SelectUnit(partyUnits[index]);

        Debug.Log($"[PartyManager] Debug hotkey selected index {index}: {partyUnits[index].name}");
    }

    public void UnregisterUnit(Unit unit)
    {
        if (unit == null)
            return;

        // Unsubscribe from knockdown events
        var hc = unit.GetComponent<HealthComponent>();
        if (hc != null) hc.OnKnockedOut -= OnPartyMemberKnockedOut;

        if (partyUnits.Remove(unit))
            OnPartyChanged?.Invoke();

        if (selectedUnit == unit)
        {
            selectedUnit = partyUnits.Count > 0 ? partyUnits[0] : null;
            OnSelectedUnitChanged?.Invoke(selectedUnit);
        }
    }

    public void SelectUnit(Unit unit)
    {
        if (unit == null)
            return;

        if (!partyUnits.Contains(unit))
            return;

        selectedUnit = unit;

        UnitActionSystem.Instance?.SetSelectedUnit(unit);

        OnSelectedUnitChanged?.Invoke(selectedUnit);

        CameraController2D.Instance?.SoftFocusOn(unit.transform);

        Debug.Log(
            $"[PartyManager] Selected {unit.name} | " +
            $"MoveAction={unit.GetMoveAction() != null} | " +
            $"PlayerStats={unit.GetComponent<PlayerStats>() != null} | " +
            $"Health={unit.GetComponent<HealthComponent>() != null} | " +
            $"Room={unit.GetCurrentRoomGrid()?.name} | " +
            $"Grid={unit.GetGridPosition()}"
        );
    }
}