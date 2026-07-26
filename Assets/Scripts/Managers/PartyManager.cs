using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PartyManager : MonoBehaviour
{
    public static PartyManager Instance { get; private set; }

    /// <summary>Checks that Instance is non-null AND not a destroyed object.</summary>
    public static bool IsValid => Instance != null && Instance.gameObject != null;

    public event Action<Unit> OnSelectedUnitChanged;
    public event Action OnPartyChanged;

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
        // Ensure only one PartyManager exists — destroy duplicates across scenes.
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

    public void RegisterUnit(Unit unit)
    {
        // Guard against calling on a destroyed PartyManager (e.g. scene transition gap).
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