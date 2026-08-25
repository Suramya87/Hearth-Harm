using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public class MainMenuController : MonoBehaviour
{
    // ── Panels ────────────────────────────────────────────────────────────
    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject modePanel;
    [SerializeField] private GameObject multiplayerPanel;
    [SerializeField] private GameObject waitingLobbyPanel;
    [SerializeField] private GameObject characterSelectPanel;
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private GameObject creditsPanel;
    [SerializeField] private GameObject settingsPanel;

    // ── Main menu buttons ─────────────────────────────────────────────────
    [Header("Main Menu Buttons")]
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button multiplayerButton;
    [SerializeField] private Button creditsButton;
    [SerializeField] private Button creditsBackButton;
    [SerializeField] private Button optionsButton;

    // ── Mode panel ────────────────────────────────────────────────────────
    [Header("Mode Panel Buttons")]
    [SerializeField] private Button partyModeButton;

    [Header("Mode Panel")]
    [SerializeField] private Button startSinglePlayerButton;
    [SerializeField] private Button backToMainButton;

    // ── Multiplayer panel ─────────────────────────────────────────────────
    [Header("Multiplayer Panel")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private TextMeshProUGUI ugsStatusText;
    [SerializeField] private TextMeshProUGUI multiplayerErrorText;
    [SerializeField] private Button backToModeButton;

    // ── Player name ───────────────────────────────────────────────────────
    [Header("Player Name")]
    [SerializeField] private TMP_InputField playerNameInput;

    // ── Waiting lobby panel ───────────────────────────────────────────────
    [Header("Waiting Lobby Panel")]
    [SerializeField] private TextMeshProUGUI sessionCodeText;
    [SerializeField] private Button copyCodeButton;
    [SerializeField] private TextMeshProUGUI waitingPlayerCount;
    [SerializeField] private Transform waitingPlayerList;
    [SerializeField] private GameObject playerSlotPrefab;
    [SerializeField] private Button beginCharSelectButton;
    [SerializeField] private Button waitingLeaveButton;

    // ── Character select panel (Party Mode) ───────────────────────────────
    [Header("Party Mode Char Select")]
    [SerializeField] private GameObject partyModeCharSelectPanel;   // drag your editor-built panel here
    [SerializeField] private List<Image> partyModeCharButtonImages;  // Image components on each character button

    // Runtime copies of materials for fade animation — set up in Start()
    private List<Material> _partyModeFadeMaterials = new();

    // ── Single-player character select (legacy, unchanged) ────────────────
    [Header("Character Select Panel")]
    [SerializeField] private Transform charSelectPlayerList;
    [SerializeField] private List<Button> characterButtons;
    [SerializeField] private List<string> characterNames;
    [SerializeField] private List<Image> characterImages;
    [SerializeField] private List<GameObject> characterPrefabs;
    [SerializeField] private TextMeshProUGUI selectedCharacterName;
    [SerializeField] private Button readyButton;
    [SerializeField] private Button startButton;
    [SerializeField] private Button singlePlayerStartButton;
    [SerializeField] private Button partyModeStartButton;
    [SerializeField] private Button charSelectLeaveButton;

    // ── Party Slots (single-player) ───────────────────────────────────────
    [Header("Party Slots")]
    [SerializeField] private Transform[] charSelectSlotContainers;
    [SerializeField] private TextMeshProUGUI slotCountText;

    // ── Scenes ────────────────────────────────────────────────────────────
    [Header("Scenes")]
    [SerializeField] private string singlePlayerSceneName = "SinglePlayerScene";
    [SerializeField] private string multiplayerSceneName  = "MultiplayerScene";
    [SerializeField] private string PartyModeSceneName = "PartyModeScene";


    // ── Runtime state ─────────────────────────────────────────────────────
    private int selectedCharIndex = 0;
    private bool isReady = false;
    private bool inCharSelectPhase = false;
    private bool isSinglePlayer = false;
    private bool isConnecting = false;
    private bool lobbySyncCoroutineActive = false;
    private bool alreadySubscribedToLobbySync = false;
    private string currentJoinCode = "---";
    private bool _isStartingGame = false;
    private bool _charSelectInitialized = false;

    // Party slots: tracks which character indices are active as party members.
    // Each entry = one player slot, max 4 total.
    // This is updated when you click your editor-built character buttons (see OnPartyMemberToggle).
    private readonly List<int> activePartySlots = new();

    public int ActivePartySlotCount => activePartySlots.Count;

    // ─────────────────────────────────────────────────────────────────────
    // Awake — wire up all button listeners
    // ─────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (partyModeButton != null)
            partyModeButton.onClick.RemoveAllListeners();

        newGameButton?.onClick.AddListener(() => ShowPanel(modePanel));
        multiplayerButton?.onClick.AddListener(() => ShowPanel(multiplayerPanel));
        creditsButton?.onClick.AddListener(OpenCredits);
        creditsBackButton?.onClick.AddListener(CloseCredits);
        optionsButton?.onClick.AddListener(OpenSettings);

        if (partyModeButton != null)
            partyModeButton.onClick.AddListener(OnPartyModeClicked);

        startSinglePlayerButton?.onClick.AddListener(GoToSinglePlayerCharSelect);
        backToMainButton?.onClick.AddListener(() => ShowPanel(mainMenuPanel));
        backToModeButton?.onClick.AddListener(OnBackToModeClicked);

        hostButton?.onClick.AddListener(OnHostClicked);
        joinButton?.onClick.AddListener(OnJoinClicked);

        playerNameInput?.onEndEdit.AddListener(OnPlayerNameChanged);

        copyCodeButton?.onClick.AddListener(OnCopyCodeClicked);

        beginCharSelectButton?.onClick.AddListener(OnBeginCharSelectClicked);
        waitingLeaveButton?.onClick.AddListener(OnLeaveClicked);

        for (int i = 0; i < characterButtons.Count; i++)
        {
            int idx = i;
            characterButtons[i]?.onClick.AddListener(() => OnCharacterButtonClicked(idx));
        }

        readyButton?.onClick.AddListener(OnReadyClicked);
        startButton?.onClick.AddListener(OnStartClicked);
        singlePlayerStartButton?.onClick.AddListener(OnSinglePlayerStartClicked);
        partyModeStartButton?.onClick.AddListener(OnPartymodeStartClicked);
        charSelectLeaveButton?.onClick.AddListener(OnLeaveClicked);
    }

    private void OnDestroy()
    {
        UnsubscribeFromNetworkGameManager();
        UnsubscribeFromLobbySync();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Start — initial panel state
    // ─────────────────────────────────────────────────────────────────────

    private void Start()
    {
        NetworkGameManager.ReinitializeForNewGame();

        creditsPanel?.SetActive(false);
        waitingLobbyPanel?.SetActive(false);
        characterSelectPanel?.SetActive(false);
        partyModeCharSelectPanel?.SetActive(false);
        loadingPanel?.SetActive(false);
        modePanel?.SetActive(false);
        multiplayerPanel?.SetActive(false);

        beginCharSelectButton?.gameObject.SetActive(false);

        SetSessionCode("---");
        SetUgsStatus("Signing in…");
        SetMultiplayerError(string.Empty);
        SetMultiplayerButtonsInteractable(false);

        SetUpPartyModeFadeMaterials();

        ShowPanel(mainMenuPanel);

        StartCoroutine(LoadPlayerName());
        StartCoroutine(SubscribeWhenReady());
    }

    // ─────────────────────────────────────────────────────────────────────
    // Coroutines
    // ─────────────────────────────────────────────────────────────────────

    private IEnumerator LoadPlayerName()
    {
        while (NetworkGameManager.Instance == null ||
               string.IsNullOrEmpty(NetworkGameManager.Instance.LocalPlayerId))
            yield return null;

        string key = $"PlayerName_{NetworkGameManager.Instance.LocalPlayerId}";
        string saved = PlayerPrefs.GetString(key, NetworkGameManager.Instance.LocalPlayerName);
        if (playerNameInput != null) playerNameInput.text = saved;
    }

    private IEnumerator SubscribeWhenReady()
    {
        while (NetworkGameManager.Instance == null) yield return null;
        SubscribeToNetworkGameManager();

        while (string.IsNullOrEmpty(NetworkGameManager.Instance.LocalPlayerId))
            yield return null;

        SetUgsStatus(string.Empty);
        SetMultiplayerButtonsInteractable(true);
    }

    private IEnumerator SubscribeToLobbySyncWhenReady()
    {
        lobbySyncCoroutineActive = true;
        Debug.Log("[MainMenuController] Waiting for LobbySync…");

        float elapsed = 0f;
        while (LobbySync.Instance == null)
        {
            elapsed += Time.deltaTime;
            if (elapsed > 30f)
            {
                Debug.LogError("[MainMenuController] Timed out waiting for LobbySync.");
                lobbySyncCoroutineActive = false;
                alreadySubscribedToLobbySync = false;
                yield break;
            }
            yield return null;
        }

        Debug.Log("[MainMenuController] LobbySync ready — subscribing.");
        UnsubscribeFromLobbySync();
        SubscribeToLobbySync();

        bool isHost = NetworkGameManager.Instance?.IsHost ?? false;

        if (waitingLobbyPanel != null && !waitingLobbyPanel.activeSelf &&
            (characterSelectPanel == null || !characterSelectPanel.activeSelf))
        {
            EnterWaitingLobby(isHost);
        }

        if (beginCharSelectButton != null)
        {
            beginCharSelectButton.gameObject.SetActive(isHost);
            beginCharSelectButton.interactable = isHost;
        }

        yield return null;

        if (!inCharSelectPhase && LobbySync.Instance.IsCharSelectPhaseActive)
        {
            Debug.Log("[MainMenuController] Char select already active — catching up immediately.");
            SwitchToCharSelectPhase();
        }
        else if (!inCharSelectPhase)
        {
            float pollElapsed = 0f;
            while (pollElapsed < 3f && !inCharSelectPhase)
            {
                pollElapsed += Time.deltaTime;
                if (LobbySync.Instance != null && LobbySync.Instance.IsCharSelectPhaseActive)
                {
                    Debug.Log("[MainMenuController] Char select detected during poll — catching up.");
                    SwitchToCharSelectPhase();
                    break;
                }
                yield return null;
            }
        }

        lobbySyncCoroutineActive = false;
        alreadySubscribedToLobbySync = false;
    }

    private void Update()
    {
        if (!lobbySyncCoroutineActive &&
            !alreadySubscribedToLobbySync &&
            !inCharSelectPhase &&
            LobbySync.Instance != null &&
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsConnectedClient &&
            !NetworkManager.Singleton.IsHost)
        {
            Debug.Log("[MainMenuController] Fallback: Widget-client detected LobbySync — subscribing.");
            StartCoroutine(SubscribeToLobbySyncWhenReady());
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Event subscriptions
    // ─────────────────────────────────────────────────────────────────────

    private void SubscribeToNetworkGameManager()
    {
        var mgr = NetworkGameManager.Instance;
        if (mgr == null) return;
        mgr.OnSignedIn += OnUgsSignedIn;
        mgr.OnSignInFailed += OnUgsSignInFailed;
        mgr.OnSessionCreated += HandleSessionCreated;
        mgr.OnSessionJoined += HandleSessionJoined;
        mgr.OnSessionLeft += HandleSessionLeft;
        mgr.OnSessionError += HandleSessionError;
        mgr.OnPlayersUpdated += HandlePlayersUpdated;
    }

    private void UnsubscribeFromNetworkGameManager()
    {
        var mgr = NetworkGameManager.Instance;
        if (mgr == null) return;
        mgr.OnSignedIn -= OnUgsSignedIn;
        mgr.OnSignInFailed -= OnUgsSignInFailed;
        mgr.OnSessionCreated -= HandleSessionCreated;
        mgr.OnSessionJoined -= HandleSessionJoined;
        mgr.OnSessionLeft -= HandleSessionLeft;
        mgr.OnSessionError -= HandleSessionError;
        mgr.OnPlayersUpdated -= HandlePlayersUpdated;
    }

    private void SubscribeToLobbySync()
    {
        if (LobbySync.Instance == null) return;
        LobbySync.Instance.OnCharSelectPhaseStarted += SwitchToCharSelectPhase;
        LobbySync.Instance.OnPlayerDataUpdated += HandlePlayerDataUpdated;
        alreadySubscribedToLobbySync = true;
        Debug.Log("[MainMenuController] Subscribed to LobbySync.");
    }

    private void UnsubscribeFromLobbySync()
    {
        if (LobbySync.Instance == null) return;
        LobbySync.Instance.OnCharSelectPhaseStarted -= SwitchToCharSelectPhase;
        LobbySync.Instance.OnPlayerDataUpdated -= HandlePlayerDataUpdated;
    }

    // ─────────────────────────────────────────────────────────────────────
    // UGS callbacks
    // ─────────────────────────────────────────────────────────────────────

    private void OnUgsSignedIn()
    {
        SetUgsStatus(string.Empty);
        SetMultiplayerButtonsInteractable(true);
    }

    private void OnUgsSignInFailed(string error)
    {
        SetUgsStatus("Sign-in failed. Check connection.");
        SetMultiplayerButtonsInteractable(false);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Panel navigation
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>"Play Party Mode" — shows your inspector-assigned panel.</summary>
    private void OnPartyModeClicked()
    {
        Debug.Log("[MainMenuController] >>> Play Party Mode clicked!");
        Debug.Log($"[DEBUG] activePartySlots addr={System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(activePartySlots)}");
        isSinglePlayer = false;
        inCharSelectPhase = true;
        isReady = false;

        if (!_charSelectInitialized)
        {
            activePartySlots.Clear();
            CharacterSelection.ClearSlots();
            _charSelectInitialized = true;
            Debug.Log($"[DEBUG] Cleared slots on first entry");
        }
        else
        {
            Debug.Log($"[DEBUG] NOT clearing — already initialized. Slots before show: [{string.Join(", ", activePartySlots)}]");
        }

        modePanel?.SetActive(false);

        // Always show the inspector panel (no dynamic UI fallback)
        if (partyModeCharSelectPanel != null)
            partyModeCharSelectPanel.SetActive(true);
    }

    private void ShowPanel(GameObject target)
    {
        mainMenuPanel?.SetActive(false);
        modePanel?.SetActive(false);
        multiplayerPanel?.SetActive(false);
        waitingLobbyPanel?.SetActive(false);
        characterSelectPanel?.SetActive(false);
        creditsPanel?.SetActive(false);
        settingsPanel?.SetActive(false);
        if (target != null) target.SetActive(true);
    }

    private void OpenCredits() => ShowPanel(creditsPanel);
    private void CloseCredits() => ShowPanel(mainMenuPanel);
    private void OpenSettings() => ShowPanel(settingsPanel);

    private void GoToSinglePlayerCharSelect()
    {
        isSinglePlayer = true;
        SwitchToCharSelectPhase();
    }

    private void OnBackToModeClicked()
    {
        _isStartingGame = false;
        _charSelectInitialized = false;

        if (NetworkGameManager.Instance?.CurrentSession != null)
            _ = NetworkGameManager.Instance.LeaveSessionAsync();

        isConnecting = false;
        SetMultiplayerError(string.Empty);
        SetMultiplayerButtonsInteractable(true);
        ShowPanel(modePanel);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Host / Join
    // ─────────────────────────────────────────────────────────────────────

    private void OnHostClicked()
    {
        if (isConnecting) return;
        SetMultiplayerError(string.Empty);
        isConnecting = true;
        SetMultiplayerButtonsInteractable(false);
        _ = NetworkGameManager.Instance?.CreateSessionAsync();
    }

    private void OnJoinClicked()
    {
        if (isConnecting) return;

        string code = joinCodeInput != null ? joinCodeInput.text.Trim().ToUpper() : string.Empty;
        if (string.IsNullOrEmpty(code))
        {
            SetMultiplayerError("Please enter a join code.");
            return;
        }

        SetMultiplayerError(string.Empty);
        isConnecting = true;
        SetMultiplayerButtonsInteractable(false);
        _ = NetworkGameManager.Instance?.JoinSessionAsync(code);
    }

    private void OnCopyCodeClicked()
    {
        if (currentJoinCode == "---" || string.IsNullOrEmpty(currentJoinCode)) return;
        GUIUtility.systemCopyBuffer = currentJoinCode;

        var txt = copyCodeButton?.GetComponentInChildren<TextMeshProUGUI>();
        if (txt != null) StartCoroutine(FlashCopyConfirmation(txt));
    }

    private IEnumerator FlashCopyConfirmation(TextMeshProUGUI label)
    {
        string original = label.text;
        label.text = "Copied!";
        yield return new WaitForSeconds(1.5f);
        label.text = original;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Session callbacks
    // ─────────────────────────────────────────────────────────────────────

    private void HandleSessionCreated()
    {
        isConnecting = false;
        GameManager.SetMode(GameMode.Host);

        currentJoinCode = NetworkGameManager.Instance?.GetJoinCode() ?? "---";
        SetSessionCode(currentJoinCode);

        EnterWaitingLobby(isHost: true);

        if (!lobbySyncCoroutineActive)
            StartCoroutine(SubscribeToLobbySyncWhenReady());
    }

    private void HandleSessionJoined()
    {
        isConnecting = false;
        GameManager.SetMode(GameMode.Client);

        currentJoinCode = "---";
        SetSessionCode("---");

        EnterWaitingLobby(isHost: false);

        if (!lobbySyncCoroutineActive)
            StartCoroutine(SubscribeToLobbySyncWhenReady());
    }

    private void HandleSessionLeft()
    {
        activePartySlots.Clear();

        isConnecting = false;
        inCharSelectPhase = false;
        isReady = false;
        isSinglePlayer = false;
        _isStartingGame = false;
        _charSelectInitialized = false;
        lobbySyncCoroutineActive = false;
        alreadySubscribedToLobbySync = false;
        currentJoinCode = "---";

        GameManager.SetMode(GameMode.Offline);

        UnsubscribeFromLobbySync();

        beginCharSelectButton?.gameObject.SetActive(false);
        if (copyCodeButton != null) copyCodeButton.gameObject.SetActive(false);

        SetSessionCode("---");
        SetMultiplayerError(string.Empty);
        SetMultiplayerButtonsInteractable(true);
        ShowPanel(mainMenuPanel);
    }

    private void HandleSessionError(string error)
    {
        isConnecting = false;
        SetMultiplayerButtonsInteractable(true);
        SetMultiplayerError(error);
    }

    private void HandlePlayersUpdated(List<SessionPlayerInfo> players)
    {
        if (inCharSelectPhase) return;
        PopulateWaitingLobbySlots(players);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Waiting lobby
    // ─────────────────────────────────────────────────────────────────────

    private void EnterWaitingLobby(bool isHost)
    {
        inCharSelectPhase = false;
        isReady = false;

        beginCharSelectButton?.gameObject.SetActive(isHost);
        if (beginCharSelectButton != null)
            beginCharSelectButton.interactable = false;

        if (copyCodeButton != null)
            copyCodeButton.gameObject.SetActive(isHost);

        var players = NetworkGameManager.Instance?.GetPlayerList();
        if (players != null) PopulateWaitingLobbySlots(players);

        ShowPanel(waitingLobbyPanel);
    }

    private void PopulateWaitingLobbySlots(List<SessionPlayerInfo> players)
    {
        if (waitingPlayerList == null || playerSlotPrefab == null) return;

        foreach (Transform child in waitingPlayerList) Destroy(child.gameObject);

        foreach (var info in players)
        {
            string charName = (info.CharacterIndex >= 0 && info.CharacterIndex < characterNames.Count)
                ? characterNames[info.CharacterIndex] : "Not selected";

            var go = Instantiate(playerSlotPrefab, waitingPlayerList);
            go.GetComponent<PlayerSlotUI>()?.Setup(info, charName);
        }

        if (waitingPlayerCount != null)
        {
            int max = NetworkGameManager.Instance?.GetMaxPlayers() ?? 4;
            waitingPlayerCount.text = $"{players.Count} / {max} players";
        }
    }

    private void OnBeginCharSelectClicked()
    {
        bool isHost = NetworkGameManager.Instance?.IsHost ?? false;
        if (!isHost) return;

        if (LobbySync.Instance == null)
        {
            Debug.LogWarning("[MainMenuController] LobbySync not ready yet.");
            return;
        }

        LobbySync.Instance.BeginCharSelectPhase();
    }

    // ─────────────────────────────────────────────────────────────────────
    // LobbySync callbacks
    // ─────────────────────────────────────────────────────────────────────

    private void HandlePlayerDataUpdated(ulong[] clientIds)
    {
        Transform list = inCharSelectPhase ? charSelectPlayerList : waitingPlayerList;
        if (list == null || playerSlotPrefab == null || LobbySync.Instance == null) return;

        foreach (Transform child in list) Destroy(child.gameObject);

        foreach (ulong id in clientIds)
        {
            int charIdx = LobbySync.Instance.GetCharacterIndex(id);
            bool ready = LobbySync.Instance.IsReady(id);
            bool isLocal = id == LobbySync.Instance.LocalClientId;
            bool isHost = id == 0;
            string charName = (charIdx >= 0 && charIdx < characterNames.Count)
                ? characterNames[charIdx] : "Selecting…";

            var info = new SessionPlayerInfo($"{id}", $"Player {id}", charIdx, ready, isLocal, isHost);
            var go = Instantiate(playerSlotPrefab, list);
            go.GetComponent<PlayerSlotUI>()?.Setup(info, charName);
        }

        if (waitingPlayerCount != null && !inCharSelectPhase)
            waitingPlayerCount.text = $"{clientIds.Length} / 4 players";

        RefreshStartButton();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Character select (single-player only, unchanged)
    // ─────────────────────────────────────────────────────────────────────

    private void SwitchToCharSelectPhase()
    {
        if (inCharSelectPhase) return;

        inCharSelectPhase = true;
        isReady = false;
        selectedCharIndex = 0;

        if (!_charSelectInitialized)
        {
            activePartySlots.Clear();
            CharacterSelection.ClearSlots();
            _charSelectInitialized = true;
        }
        UpdateSlotCountText();

        CharacterSelection.SetCharacterPrefabs(characterPrefabs);

        bool isHost = !isSinglePlayer && (NetworkGameManager.Instance?.IsHost ?? false);

        singlePlayerStartButton?.gameObject.SetActive(isSinglePlayer && inCharSelectPhase);
        readyButton?.gameObject.SetActive(!isSinglePlayer);

        if (startButton != null)
        {
            startButton.gameObject.SetActive(!isSinglePlayer && isHost);
            startButton.interactable = false;
        }

        modePanel?.SetActive(false);

        if (isSinglePlayer && characterSelectPanel != null)
            characterSelectPanel.SetActive(true);

        Debug.Log($"[MainMenuController] → Char select. SP={isSinglePlayer} Host={isHost} activePartySlots={activePartySlots.Count}");
    }

    /// <summary>Check whether a character index is currently in the party.</summary>
    public bool IsActivePartyMember(int charIndex)
    {
        for (int i = 0; i < activePartySlots.Count; i++)
            if (activePartySlots[i] == charIndex) return true;
        return false;
    }

    /// <summary>Toggle a character on/off in the party. Call from inspector — parameter is the character index (0-3+).</summary>
    public void OnPartyMemberToggle(int charIndex)
    {
        int before = activePartySlots.Count;
        Debug.Log($"[DEBUG] OnPartyMemberToggle({charIndex}) BEFORE: count={before} slots=[{string.Join(", ", activePartySlots)}]");

        // Find existing slot
        int existingSlot = -1;
        for (int i = activePartySlots.Count - 1; i >= 0; i--)
        {
            if (activePartySlots[i] == charIndex)
            {
                existingSlot = i;
                break;
            }
        }

        if (existingSlot >= 0)
        {
            // Remove character from party
            activePartySlots.RemoveAt(existingSlot);

            // Shift remaining selections down
            for (int k = existingSlot; k + 1 < activePartySlots.Count; k++)
                activePartySlots[k] = activePartySlots[k + 1];

            selectedCharIndex = activePartySlots.Count > 0 ? activePartySlots[activePartySlots.Count - 1] : charIndex;
        }
        else
        {
            // Add character to party (max 4)
            if (activePartySlots.Count >= CharacterSelection.MaxSlots)
            {
                Debug.LogWarning($"[DEBUG] Cannot add character {charIndex}: already at max ({CharacterSelection.MaxSlots})");
                return;
            }
            activePartySlots.Add(charIndex);
            selectedCharIndex = charIndex;
        }

        int after = activePartySlots.Count;
        Debug.Log($"[DEBUG] OnPartyMemberToggle({charIndex}) AFTER: count={after} slots=[{string.Join(", ", activePartySlots)}]");
        Debug.Log($"[DEBUG] RefreshActiveSlotUI will run next...");

        RefreshActiveSlotUI();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Ready & Start
    // ─────────────────────────────────────────────────────────────────────

    private void OnReadyClicked()
    {
        isReady = !isReady;
        LobbySync.Instance?.SetMyReady(isReady);
        NetworkGameManager.Instance?.SetLocalReadyState(isReady);
        UpdateReadyVisual();
    }

    private void UpdateReadyVisual()
    {
        var txt = readyButton?.GetComponentInChildren<TextMeshProUGUI>();
        if (txt != null) txt.text = isReady ? "Not Ready" : "Ready!";

        var img = readyButton?.GetComponent<Image>();
        if (img != null) img.color = isReady ? new Color(0.2f, 0.85f, 0.3f, 1f) : Color.white;
    }

    private void RefreshStartButton()
    {
        if (startButton == null || isSinglePlayer) return;

        bool isHost = NetworkGameManager.Instance?.IsHost ?? false;
        if (!isHost) return;

        bool allReady = LobbySync.Instance?.AllPlayersReady()
                     ?? NetworkGameManager.Instance?.AllPlayersReady()
                     ?? false;

        startButton.interactable = allReady;

        var txt = startButton.GetComponentInChildren<TextMeshProUGUI>();
        if (txt != null)
        {
            txt.text = allReady ? "Start Adventure" : "Waiting for players…";
            txt.color = allReady ? Color.white : new Color(1f, 1f, 1f, 0.4f);
        }
    }
    // ─────────────────────────────────────────────────────────────────────
    // Party Mode — editor-panel UI refresh (lightweight)
    // Updates count text and start button visibility on your inspector panel
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>Refresh button highlight colors on the party-mode inspector panel.</summary>
    private void RefreshActiveSlotUI()
    {
        Debug.Log($"[MainMenuController] activePartySlots changed: [{string.Join(", ", activePartySlots)}] ({activePartySlots.Count}/{CharacterSelection.MaxSlots})");

        // Update button visuals via fade materials (0 = faded/colorless, 1 = full color)
        for (int i = 0; i < partyModeCharButtonImages.Count; i++)
        {
            if (_partyModeFadeMaterials == null || _partyModeFadeMaterials.Count <= i) continue;

            Image img = partyModeCharButtonImages[i];
            if (img == null || _partyModeFadeMaterials[i] == null) continue;

            bool inParty = IsActivePartyMember(i);
            StartCoroutine(AnimateVFXFade(img, _partyModeFadeMaterials[i], inParty ? 1f : 0f));
        }
    }

    private IEnumerator AnimateVFXFade(Image img, Material mat, float target)
    {
        if (img == null || mat == null) yield break;

        float startVal = mat.GetFloat("_Amount");
        float t = 0f;
        float duration = 0.35f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / duration;
            mat.SetFloat("_Amount", Mathf.Lerp(startVal, target, t));
            yield return null;
        }
        mat.SetFloat("_Amount", target);
    }

    private void SetUpPartyModeFadeMaterials()
    {
        if (partyModeCharButtonImages == null || partyModeCharButtonImages.Count == 0) return;

        _partyModeFadeMaterials.Clear();
        for (int i = 0; i < partyModeCharButtonImages.Count; i++)
        {
            Image img = partyModeCharButtonImages[i];
            if (img == null) continue;

            Material runtimeMat = new Material(img.material);
            runtimeMat.SetFloat("_Amount", 0f);
            img.material = runtimeMat;
            _partyModeFadeMaterials.Add(runtimeMat);
        }

        Debug.Log($"[MainMenuController] Set up {_partyModeFadeMaterials.Count} fade VFX materials for party mode buttons.");
    }

    // ─────────────────────────────────────────────────────────────────────
    // Start party mode game — validates and launches
    // Call from inspector on your panel's "Start Game" button.
    // ─────────────────────────────────────────────────────────────────────

    public void OnPartymodeStartClicked()
    {
        if (_isStartingGame) return;

        Debug.Log($"[DEBUG] OnPartymodeStartClicked BEFORE check: activePartySlots addr={System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(activePartySlots)} count={activePartySlots.Count} capacity={activePartySlots.Capacity}");
        for (int i = 0; i < activePartySlots.Count; i++)
            Debug.Log($"[DEBUG]   slot[{i}] = {activePartySlots[i]}");

        if (activePartySlots.Count == 0)
        {
            // Try to figure out what cleared it — check if partyModeCharButtonImages got corrupted
            bool anyImagesNull = false;
            for (int i = 0; i < partyModeCharButtonImages.Count; i++)
            {
                if (partyModeCharButtonImages[i] == null) anyImagesNull = true;
            }
            Debug.LogWarning($"[MainMenuController] No characters selected for party mode! activePartySlots addr={System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(activePartySlots)}");
            Debug.LogWarning($"[DEBUG] partyModeCharButtonImages count={partyModeCharButtonImages.Count}, nulls={anyImagesNull}");

            // Check if the list object itself was replaced (common when Unity serializes a List)
            Debug.LogError("[MainMenuController] === activePartySlots might have been replaced by Unity serialization! Check for duplicate MainMenuController components in scene. ===");
            return;
        }

        CharacterSelection.SetCharacterPrefabs(characterPrefabs);
        CharacterSelection.SetSlots(activePartySlots);

        bool valid = false;
        for (int i = 0; i < CharacterSelection.SlotCount && !valid; i++)
        {
            if (CharacterSelection.GetPrefabForSlot(i) != null)
                valid = true;
        }

        if (!valid)
        {
            Debug.LogError("[MainMenuController] No valid prefabs for party mode! characterPrefabs.Count=" + characterPrefabs?.Count);
            return;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        NetworkGameManager.RequestFullShutdown();
        GameManager.SetMode(GameMode.Offline);
        PartyFollowManager.GetOrCreateInstance();

        _isStartingGame = true;
        modePanel?.SetActive(false);
        loadingPanel?.SetActive(true);
        ShowPanel(null);
        Debug.Log("[MainMenuController] Loading " + PartyModeSceneName + " with " + activePartySlots.Count + " party members");
        SceneManager.LoadScene(PartyModeSceneName);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Leave
    // ─────────────────────────────────────────────────────────────────────

    private void OnBackFromCharSelect()
    {
        // Hide inspector party panel if shown
        if (partyModeCharSelectPanel != null)
            partyModeCharSelectPanel.SetActive(false);

        // Show mode panel
        modePanel?.SetActive(true);

        _charSelectInitialized = false;

        Debug.Log("[MainMenuController] Back from char select. Mode panel restored.");
    }

    private void OnLeaveClicked()
    {
        _isStartingGame = false;
        isReady = false;
        inCharSelectPhase = false;

        if (isSinglePlayer)
        {
            activePartySlots.Clear();
            CharacterSelection.ClearSlots();
            _charSelectInitialized = false;
            UpdateSlotCountText();
            isSinglePlayer = false;
            ShowPanel(modePanel);
        }
        else
        {
            isConnecting = false;
            SetMultiplayerButtonsInteractable(true);
            _ = NetworkGameManager.Instance?.LeaveSessionAsync();
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Helpers (unchanged)
    // ─────────────────────────────────────────────────────────────────────

    private void OnPlayerNameChanged(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        NetworkGameManager.Instance?.SetLocalPlayerName(name);
        PlayerPrefs.SetString("PlayerName", name);
    }

    private GameObject GetSelectedPrefab()
    {
        if (characterPrefabs == null || selectedCharIndex >= characterPrefabs.Count) return null;
        return characterPrefabs[selectedCharIndex];
    }

    private void SetMultiplayerButtonsInteractable(bool interactable)
    {
        if (hostButton != null) hostButton.interactable = interactable;
        if (joinButton != null) joinButton.interactable = interactable;
    }

    private void SetMultiplayerError(string msg)
    {
        if (multiplayerErrorText != null) multiplayerErrorText.text = msg;
    }

    private void SetSessionCode(string code)
    {
        currentJoinCode = code;
        if (sessionCodeText != null)
            sessionCodeText.text = code == "---" ? "Waiting…" : $"{code}";
    }

    private void SetUgsStatus(string msg)
    {
        if (ugsStatusText != null) ugsStatusText.text = msg;
    }

    private void UpdateSlotCountText()
    {
        if (slotCountText != null)
            slotCountText.text = $"{activePartySlots.Count} / {CharacterSelection.MaxSlots}";
    }

    private void OnSinglePlayerStartClicked()
    {
        if (!isSinglePlayer) return;
        activePartySlots.Clear();
        CharacterSelection.ClearSlots();
        LaunchGame(isMultiplayer: false);
    }

    private void OnStartClicked()
    {
        bool isHost = NetworkGameManager.Instance?.IsHost ?? false;
        if (!isHost) return;

        bool allReady = LobbySync.Instance?.AllPlayersReady()
                     ?? NetworkGameManager.Instance?.AllPlayersReady()
                     ?? false;
        if (!allReady) return;

        LaunchGame(isMultiplayer: true);
    }

    private void LaunchGame(bool isMultiplayer)
    {
        CharacterSelection.Index = selectedCharIndex;
        CharacterSelection.Prefab = GetSelectedPrefab();

        if (isMultiplayer)
            GameManager.SetMode(NetworkManager.Singleton?.IsHost ?? false
                ? GameMode.Host : GameMode.Client);
        else
            GameManager.SetMode(GameMode.Offline);

        loadingPanel?.SetActive(true);
        ShowPanel(null);

        if (isMultiplayer)
        {
            if (NetworkManager.Singleton?.IsHost ?? false)
            {
                NetworkManager.Singleton.SceneManager.LoadScene(
                    multiplayerSceneName,
                    UnityEngine.SceneManagement.LoadSceneMode.Single);
            }
        }
        else
        {
            SceneManager.LoadScene(singlePlayerSceneName);
        }
    }

    private void OnCharacterButtonClicked(int charIndex)
    {
        int existingSlot = -1;
        for (int i = activePartySlots.Count - 1; i >= 0; i--)
        {
            if (activePartySlots[i] == charIndex)
            {
                existingSlot = i;
                break;
            }
        }

        if (existingSlot >= 0)
        {
            int removedCharIdx = activePartySlots[existingSlot];
            activePartySlots.RemoveAt(existingSlot);
            UpdateSlotCountText();

            if (charSelectSlotContainers != null && removedCharIdx < charSelectSlotContainers.Length)
            {
                for (int i = charSelectSlotContainers[removedCharIdx].childCount - 1; i >= 0; i--)
                    Destroy(charSelectSlotContainers[removedCharIdx].GetChild(i).gameObject);
            }

            RefreshSlotThumbnails();
            selectedCharIndex = activePartySlots.Count > 0 ? activePartySlots[activePartySlots.Count - 1] : 0;
            return;
        }

        selectedCharIndex = charIndex;

        if (activePartySlots.Count >= CharacterSelection.MaxSlots)
        {
            int oldRemovedIdx = activePartySlots[activePartySlots.Count - 1];
            activePartySlots.RemoveAt(activePartySlots.Count - 1);

            if (charSelectSlotContainers != null && oldRemovedIdx < charSelectSlotContainers.Length)
                DestroyAllChildren(charSelectSlotContainers[oldRemovedIdx]);

            RefreshSlotThumbnails();
        }

        activePartySlots.Add(charIndex);
        UpdateSlotCountText();

        if (charSelectSlotContainers != null && charIndex < charSelectSlotContainers.Length)
        {
            StartCoroutine(CreateCharacterThumbnail(charSelectSlotContainers[charIndex], charIndex, activePartySlots.Count - 1));
        }

        RefreshSlotThumbnails();
    }

    private static void DestroyAllChildren(Transform parent)
    {
        if (parent == null) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    private void RefreshSlotThumbnails()
    {
        if (charSelectSlotContainers == null) return;

        for (int i = 0; i < charSelectSlotContainers.Length; i++)
            DestroyAllChildren(charSelectSlotContainers[i]);

        for (int i = 0; i < activePartySlots.Count; i++)
        {
            int charIdx = activePartySlots[i];
            if (charIdx >= 0 && charIdx < charSelectSlotContainers.Length)
                StartCoroutine(CreateCharacterThumbnail(charSelectSlotContainers[charIdx], charIdx, i));
        }
    }

    private IEnumerator CreateCharacterThumbnail(Transform parent, int charIndex, int playerIndex)
    {
        if (parent == null || charIndex >= characterPrefabs.Count) yield break;

        GameObject go = new GameObject($"SlotThumb_{charIndex + 1}");
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();

        Sprite sprite = null;
        GameObject prefab = characterPrefabs[charIndex];
        if (prefab != null)
        {
            var sprites = prefab.GetComponentsInChildren<SpriteRenderer>();
            if (sprites.Length > 0 && sprites[0].sprite != null)
                sprite = sprites[0].sprite;
            if (sprite == null)
            {
                var images = prefab.GetComponentsInChildren<Image>();
                foreach (var uiImg in images)
                {
                    if (uiImg.sprite != null) { sprite = uiImg.sprite; break; }
                }
            }
        }

        img.color = PartySlotUI.SlotColors[playerIndex];
        if (sprite != null) img.sprite = sprite;
        else img.raycastTarget = true;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(40f, 40f);
        rt.localPosition = Vector3.zero;
        rt.localScale = Vector3.zero;

        float t = 0f;
        Vector3 target = new Vector3(0.7f, 0.7f, 1f);
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * 5f;
            float s = Mathf.SmoothStep(0f, 1f, t);
            rt.localScale = target * s;
            yield return null;
        }
        rt.localScale = target;
    }
}
