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

    // ── Character select panel ────────────────────────────────────────────
    [Header("Character Select Panel")]
    [SerializeField] private GameObject partyModeCharSelectPanel;   // inspector-assigned panel for party mode char select
    [SerializeField] private Transform charSelectPlayerList;
    [SerializeField] private List<Button> characterButtons;
    [SerializeField] private List<string> characterNames;
    [SerializeField] private List<Image> characterImages; // for vfx
    [SerializeField] private List<GameObject> characterPrefabs;
    [SerializeField] private TextMeshProUGUI selectedCharacterName;
    [SerializeField] private Button readyButton;
    [SerializeField] private Button startButton;
    [SerializeField] private Button singlePlayerStartButton;
    [SerializeField] private Button partyModeStartButton;
    [SerializeField] private Button charSelectLeaveButton;

    // ── Party Slots (single-player party mode) ───────────────────────────
    [Header("Party Slots")]
    [SerializeField] private Transform[] charSelectSlotContainers; // one per character button, parent for dots
    [SerializeField] private TextMeshProUGUI slotCountText;        // shows "X / 4"

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
    private bool _showCharSelectForPartyMode = false;
    private bool _isStartingGame = false;       // prevents double-load if multiple listeners fire simultaneously
    private bool _charSelectInitialized = false; // tracks whether first char-select entry has occurred

    // Party slots: tracks which character indices are active as party members.
    // Each entry = one player slot, max 4 total.
    private readonly List<int> activePartySlots = new();

    // ── Dynamic char select UI fields (built at runtime) ──────────────────
    private GameObject _charScreenRoot;                  // root panel for dynamic char select screen
    private CanvasGroup _charScreenCanvasGroup;           // canvas group for fade-in effect
    private Transform _charPreviewContainer;              // parent for preview row thumbnails
    private GameObject _charCountTextGO;                 // GameObject holding the "X / 4" count text
    private GameObject[] _charButtonGo;                  // character button GameObjects
    private Image[] _charButtonImages;                   // background images for char buttons
    private TextMeshProUGUI[] _charButtonTexts;          // name labels on char buttons
    private GameObject _charStartButtonGo;               // start game button GameObject
    private Transform _charSelectUIRoot;                 // fallback: inspector-assigned char select root
    private GameObject[] _charPreviewThumbs;             // preview thumbnails row

    // ─────────────────────────────────────────────────────────────────────
    // Awake — wire up all button listeners
    // ─────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Clear any inspector wiring on partyModeButton so we have full control.
        // Without this, both inspector-registered listeners AND our AddListener fire
        // in the same frame, fighting over shared state and corrupting activePartySlots.
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

        SetUpCharacterSelectMaterials();
        RefreshCharacterButtons();
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

    /// <summary>"Play Party Mode" button — builds dynamic char select screen, hides mode panel.</summary>
    private void OnPartyModeClicked()
    {
        Debug.Log("[MainMenuController] >>> Play Party Mode clicked!");
        isSinglePlayer = false;
        inCharSelectPhase = true;
        isReady = false;

        // Ensure dynamic UI exists (built once, reused on re-entry)
        EnsurePartyCharacterSelectUI();

        // Reset state for fresh party if starting from scratch
        if (!_charSelectInitialized)
        {
            activePartySlots.Clear();
            CharacterSelection.ClearSlots();
            _charSelectInitialized = true;
        }

        // Hide mode panel, show dynamic char select screen
        modePanel?.SetActive(false);
        if (_charScreenRoot != null)
        {
            _charScreenRoot.SetActive(true);
            // Always fade in — on first entry alpha is 0f, on re-entry it's also 0f from OnBackFromCharSelect
            if (_charScreenCanvasGroup != null && _charScreenCanvasGroup.alpha < 1f)
                _charScreenCanvasGroup.alpha = 0f;
            if (_charScreenCanvasGroup != null && _charScreenCanvasGroup.alpha == 0f)
                StartCoroutine(FadeInCharScreen());
        }

        RefreshCharacterSelectUI();
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
    private void OpenSettings()
    {
        ShowPanel(settingsPanel);
    }
    private void GoToSinglePlayerCharSelect()
    {
        isSinglePlayer = true;
        SwitchToCharSelectPhase();
    }

    private void OnBackToModeClicked()
    {
        _isStartingGame = false;
        _showCharSelectForPartyMode = false;  // reset party-mode flag
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

    // ─────────────────────────────────────────────────────────────────────
    // Copy code button — copies the 6-char code to the system clipboard
    // ─────────────────────────────────────────────────────────────────────

    private void OnCopyCodeClicked()
    {
        if (currentJoinCode == "---" || string.IsNullOrEmpty(currentJoinCode)) return;
        GUIUtility.systemCopyBuffer = currentJoinCode;

        // Brief visual confirmation
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
        activePartySlots.Clear();   // prevent leftover slots from corrupting next session

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

        // Host sees "Begin Char Select" — disabled until LobbySync is ready
        beginCharSelectButton?.gameObject.SetActive(isHost);
        if (beginCharSelectButton != null)
            beginCharSelectButton.interactable = false;

        // Copy button is only useful for the host
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
    // Character select
    // ─────────────────────────────────────────────────────────────────────

    private void SwitchToCharSelectPhase()
    {
        if (inCharSelectPhase) return;

        inCharSelectPhase = true;
        isReady = false;
        selectedCharIndex = 0;

        // Only clear on FIRST entry — don't wipe selections when returning via back button
        if (!_charSelectInitialized)
        {
            activePartySlots.Clear();
            CharacterSelection.ClearSlots();
            _charSelectInitialized = true;
        }
        UpdateSlotCountText();

        // Register prefabs so CharacterSelection can resolve them later
        CharacterSelection.SetCharacterPrefabs(characterPrefabs);

        RefreshCharacterButtons();
        UpdateReadyVisual();

        bool isHost = !isSinglePlayer && (NetworkGameManager.Instance?.IsHost ?? false);

        // Always show the party mode start button during char select
        singlePlayerStartButton?.gameObject.SetActive(isSinglePlayer && inCharSelectPhase);
        readyButton?.gameObject.SetActive(!isSinglePlayer);

        if (startButton != null)
        {
            startButton.gameObject.SetActive(!isSinglePlayer && isHost);
            startButton.interactable = false;
        }

        modePanel?.SetActive(false);

        // Only single-player uses the inspector-assigned character select panel.
        // Party mode builds its own dynamic UI from OnPartyModeClicked().
        if (isSinglePlayer && characterSelectPanel != null)
            characterSelectPanel.SetActive(true);

        Debug.Log($"[MainMenuController] → Char select. SP={isSinglePlayer} Host={isHost} activePartySlots={activePartySlots.Count}");
    }

    private void SetUpCharacterSelectMaterials()
    {
        for (int i = 0; i < characterImages.Count; ++i)
        {
            Material runtimeMaterial = new Material(characterImages[i].material);
            characterImages[i].material = runtimeMaterial;
        }
    }

    private void SelectCharacter(int index)
    {
        selectedCharIndex = index;

        if (!isSinglePlayer)
        {
            LobbySync.Instance?.SetMyCharacter(index);
            NetworkGameManager.Instance?.SetLocalCharacterSelection(index);
        }

        RefreshCharacterButtons();
    }

    private void RefreshCharacterButtons()
    {
        for (int i = 0; i < characterButtons.Count; i++)
        {
            if (characterButtons[i] == null) continue;
            bool sel = (i == selectedCharIndex);
            bool inParty = IsActivePartyMember(i);

            // Preview highlight — scale + VFX material
            StartCoroutine(SelectCharVFX(characterImages[i].material, sel));
            characterButtons[i].transform.localScale = sel ? new Vector3(1.1f, 1.1f, 1f) : Vector3.one;

            // Party membership: use existing VFX material to "pop in" the character image
            if (inParty && !sel)
                StartCoroutine(PopInCharImage(i));
            else if (!inParty)
                ClearPartyMemberHighlight(i);
        }

        if (selectedCharacterName != null && selectedCharIndex < characterNames.Count)
            selectedCharacterName.text = characterNames[selectedCharIndex];
    }

    /// <summary>Make a character image appear using the existing VFX material (same as preview highlight).</summary>
    private IEnumerator PopInCharImage(int charIndex)
    {
        if (charIndex >= characterImages.Count || characterImages[charIndex] == null) yield break;
        var mat = characterImages[charIndex].material;
        if (mat == null || !mat.HasProperty("_Amount")) yield break;

        float t = 0f;
        float duration = 0.3f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * (1f / duration);
            mat.SetFloat("_Amount", Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        mat.SetFloat("_Amount", 1f);
    }

    private bool IsActivePartyMember(int charIndex)
    {
        for (int i = 0; i < activePartySlots.Count; i++)
            if (activePartySlots[i] == charIndex) return true;
        return false;
    }

    private void ClearPartyMemberHighlight(int charIndex)
    {
        // Reset the VFX material so the character image disappears (same as preview unselected state)
        if (charIndex < characterImages.Count && characterImages[charIndex] != null)
        {
            var mat = characterImages[charIndex].material;
            if (mat != null && mat.HasProperty("_Amount"))
                mat.SetFloat("_Amount", 0f);
        }
    }

    IEnumerator SelectCharVFX(Material material, bool selected = true)
    {
        float duration = 2f;
        float elapsed = 0f;
        float target = selected ? 1f : 0f;
        float start = 1f - target;
        bool done = material.GetFloat("_Amount") == target;

        if (!done)
        {
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / duration);
                float amount = Mathf.Lerp(start, target, t);

                material.SetFloat("_Amount", amount);

                yield return null;
            }
        }

        material.SetFloat("_Amount", target);
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
    // Party slot management (single-player)
    // ─────────────────────────────────────────────────────────────────────

    private void OnCharacterButtonClicked(int charIndex)
    {
        // Check if this character is already in the party — toggle it off if so
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
            // Remove this character from the party
            int removedCharIdx = activePartySlots[existingSlot];
            activePartySlots.RemoveAt(existingSlot);
            UpdateSlotCountText();

            // Destroy the thumbnail at this slot position
            if (charSelectSlotContainers != null && removedCharIdx < charSelectSlotContainers.Length)
                DestroyAllChildren(charSelectSlotContainers[removedCharIdx]);

            RefreshSlotThumbnails();

            // Preview: stay on a valid party member if possible, or default to first available
            selectedCharIndex = activePartySlots.Count > 0 ? activePartySlots[activePartySlots.Count - 1] : 0;
            RefreshCharacterButtons();
            return;
        }

        // Not in party — add it (toggle on)
        // Always advance preview to the clicked character so its highlight is visible
        selectedCharIndex = charIndex;

        if (activePartySlots.Count >= CharacterSelection.MaxSlots)
        {
            // Party is full — remove the last slot and replace with new character
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
            CreateCharacterThumbnail(charSelectSlotContainers[charIndex], charIndex, activePartySlots.Count - 1);
        }

        RefreshCharacterButtons();
    }


    /// <summary>Destroy all child GameObjects of a Transform.</summary>
    private static void DestroyAllChildren(Transform parent)
    {
        if (parent == null) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    private void RefreshSlotThumbnails()
    {
        if (charSelectSlotContainers == null) return;

        // Clear all thumbnails first
        for (int i = 0; i < charSelectSlotContainers.Length; i++)
            DestroyAllChildren(charSelectSlotContainers[i]);

        // Recreate them in order with correct character images
        for (int i = 0; i < activePartySlots.Count; i++)
        {
            int charIdx = activePartySlots[i];
            if (charIdx >= 0 && charIdx < charSelectSlotContainers.Length)
                CreateCharacterThumbnail(charSelectSlotContainers[charIdx], charIdx, i);
        }
    }

    /// <summary>Create a small character image thumbnail in the slot container.</summary>
    private void CreateCharacterThumbnail(Transform parent, int charIndex, int playerIndex)
    {
        if (parent == null || charIndex >= characterPrefabs.Count) return;

        GameObject go = new GameObject($"SlotThumb_{charIndex + 1}");
        go.transform.SetParent(parent, false);

        Image img = go.AddComponent<Image>();

        // Try to grab a sprite from the character prefab's graphics
        Sprite sprite = null;
        GameObject prefab = characterPrefabs[charIndex];
        if (prefab != null)
        {
            var sprites = prefab.GetComponentsInChildren<SpriteRenderer>();
            if (sprites.Length > 0 && sprites[0].sprite != null)
                sprite = sprites[0].sprite;

            // Fallback: try the first Image child
            if (sprite == null)
            {
                var images = prefab.GetComponentsInChildren<Image>();
                foreach (var uiImg in images)
                {
                    if (uiImg.sprite != null)
                    {
                        sprite = uiImg.sprite;
                        break;
                    }
                }
            }

            // Last fallback: RawImage has texture not sprite, but Image covers most cases
        }

        // If no sprite, tint to the player slot color as a placeholder
        img.color = PartySlotUI.SlotColors[playerIndex];

        if (sprite != null)
            img.sprite = sprite;
        else
            img.raycastTarget = true;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(40f, 40f); // slightly larger than dots to show character art
        rt.localPosition = Vector3.zero;

        // Scale-in animation (same as existing dot animation)
        rt.localScale = Vector3.zero;
        StartCoroutine(ScaleInThumbnail(rt));
    }

    private static IEnumerator ScaleInThumbnail(RectTransform rt)
    {
        float t = 0f;
        Vector3 target = new Vector3(0.7f, 0.7f, 1f); // thumbnail-sized
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * 5f;
            float s = Mathf.SmoothStep(0f, 1f, t);
            rt.localScale = target * s;
            yield return null;
        }
        rt.localScale = target;
    }


    private void UpdateSlotCountText()
    {
        if (slotCountText != null)
            slotCountText.text = $"{activePartySlots.Count} / {CharacterSelection.MaxSlots}";
    }

    private void OnSinglePlayerStartClicked()
    {
        if (!isSinglePlayer) return;

        // Clear any leftover party mode data — single-player only loads the one chosen character
        activePartySlots.Clear();
        CharacterSelection.ClearSlots();

        LaunchGame(isMultiplayer: false);
    }

    public void OnPartymodeStartClicked()
    {
        if (_isStartingGame) return;

        // Validate selections and launch
        if (activePartySlots.Count == 0)
        {
            Debug.LogWarning("[MainMenuController] No characters selected for party mode!");
            return;
        }

        // Pass party slots to the persistent selection state
        CharacterSelection.SetCharacterPrefabs(characterPrefabs);
        CharacterSelection.SetSlots(activePartySlots);

        // Validate at least one prefab resolved
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

        // Shutdown, set mode, load
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }
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

    // ─────────────────────────────────────────────────────────────────────
    // Leave
    // ─────────────────────────────────────────────────────────────────────

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
    // Helpers
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

    // ═══════════════════════════════════════════════════════════════════════
    // Party Mode — Dynamic Character Select UI
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Build the entire character select screen at runtime. Zero inspector deps.</summary>
    private void EnsurePartyCharacterSelectUI()
    {
        if (_charScreenRoot != null) return;  // already built

        // ── Root panel (own Canvas so it renders regardless of modePanel wiring) ──
        _charScreenRoot = new GameObject("PartyCharSelectPanel");
        var rt = _charScreenRoot.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // MUST have a Canvas for UI rendering — overlay so it's always on top
        var mainCanvas = _charScreenRoot.AddComponent<Canvas>();
        mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        mainCanvas.pixelPerfect = false;
        mainCanvas.sortingOrder = 10;  // render above the main menu canvas

        // CRITICAL: Without GraphicRaycaster, Unity's EventSystem can't route clicks to Buttons on this Canvas
        _charScreenRoot.AddComponent<GraphicRaycaster>();

        _charScreenCanvasGroup = _charScreenRoot.AddComponent<CanvasGroup>();
        _charScreenCanvasGroup.alpha = 0f;
        _charScreenCanvasGroup.interactable = true;
        _charScreenCanvasGroup.blocksRaycasts = true;

        // Parent under mainMenuPanel so it shares the same canvas hierarchy
        _charScreenRoot.transform.SetParent(mainMenuPanel?.transform, false);

        var bgImage = _charScreenRoot.AddComponent<Image>();
        bgImage.color = new Color(0f, 0f, 0f, 0.75f);
        bgImage.raycastTarget = false;  // don't block clicks from reaching child Buttons

        // ── Title label ──────────────────────────────────────────────
        GameObject titleGO = new GameObject("TitleText");
        titleGO.transform.SetParent(_charScreenRoot.transform, false);
        TextMeshProUGUI titleTxt = titleGO.AddComponent<TextMeshProUGUI>();
        titleTxt.text = "Choose Your Party";
        titleTxt.fontSize = 20f;
        titleTxt.alignment = TextAlignmentOptions.Center;
        titleTxt.color = Color.white;
        if (titleGO.TryGetComponent<RectTransform>(out var titleRt))
        {
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0, -40f);
        }

        // ── Party preview container (row for selected characters) ────
        GameObject previewContainerGO = new GameObject("PreviewRow");
        _charPreviewContainer = previewContainerGO.transform;
        _charPreviewContainer.SetParent(_charScreenRoot.transform, false);
        if (previewContainerGO.TryGetComponent<RectTransform>(out var previewRt))
        {
            previewRt.anchorMin = new Vector2(0.5f, 1f);
            previewRt.anchorMax = new Vector2(0.5f, 1f);
            previewRt.pivot = new Vector2(0.5f, 0.5f);
            previewRt.sizeDelta = new Vector2(340f, 60f);
            previewRt.anchoredPosition = new Vector2(0, -90f);

            var containerBg = previewContainerGO.AddComponent<Image>();
            containerBg.color = new Color(0.15f, 0.15f, 0.3f, 0.8f);
        }

        // ── Count label ─────────────────────────────────────────────
        GameObject countGO = new GameObject("CountText");
        _charCountTextGO = countGO;
        countGO.transform.SetParent(_charScreenRoot.transform, false);
        TextMeshProUGUI countTxt = countGO.AddComponent<TextMeshProUGUI>();
        countTxt.text = "0 / 4 characters";
        countTxt.fontSize = 14f;
        countTxt.alignment = TextAlignmentOptions.Center;
        countTxt.color = new Color(0.8f, 0.8f, 1f);
        if (countGO.TryGetComponent<RectTransform>(out var countRt))
        {
            countRt.anchorMin = new Vector2(0.5f, 1f);
            countRt.anchorMax = new Vector2(0.5f, 1f);
            countRt.pivot = new Vector2(0.5f, 1f);
            countRt.anchoredPosition = new Vector2(0, -175f);
        }

        // ── Character buttons (from characterPrefabs) ───────────────
        bool hasPrefabs = characterPrefabs != null && characterPrefabs.Count > 0;
        if (!hasPrefabs)
            Debug.LogWarning("[MainMenuController] characterPrefabs is empty! Assign characters to the MainMenuController → Character Prefabs field in the Unity inspector.");

        int charCount = hasPrefabs ? characterPrefabs.Count : 4; // 4 placeholders if no prefabs
        _charButtonGo = new GameObject[charCount];
        _charButtonImages = new Image[charCount];
        _charButtonTexts = new TextMeshProUGUI[charCount];

        float btnWidth = 140f;
        float btnHeight = 180f;
        float gap = 15f;
        float gridStartX = -(charCount * (btnWidth + gap)) / 2f + gap / 2f;
        float gridY = -60f;

        for (int i = 0; i < charCount; i++)
        {
            GameObject btnGO = new GameObject($"CharButton_{i}");
            btnGO.transform.SetParent(_charScreenRoot.transform, false);
            var btnComp = btnGO.AddComponent<Button>();
            _charButtonGo[i] = btnGO;

            // Background image for the button
            Image btnImg = btnGO.AddComponent<Image>();
            _charButtonImages[i] = btnImg;
            btnImg.raycastTarget = true;
            btnImg.color = new Color(0.25f, 0.25f, 0.35f, 1f);

            // Get sprite from prefab (try SpriteRenderer first, then Image)
            Sprite sprite = null;
            GameObject prefab = hasPrefabs ? characterPrefabs[i] : null;
            if (prefab != null)
            {
                var sprites = prefab.GetComponentsInChildren<SpriteRenderer>();
                if (sprites.Length > 0 && sprites[0].sprite != null)
                    sprite = sprites[0].sprite;
                if (sprite == null)
                {
                    var imgs = prefab.GetComponentsInChildren<Image>();
                    foreach (var uiImg in imgs)
                    {
                        if (uiImg.sprite != null) { sprite = uiImg.sprite; break; }
                    }
                }
            }

            if (sprite != null)
            {
                btnImg.sprite = sprite;
                btnImg.type = Image.Type.Sliced;
            }
            else
            {
                // Placeholder color per character index
                btnImg.color = PartySlotUI.SlotColors[i % PartySlotUI.SlotColors.Length];
            }

            // Name label below the image
            GameObject nameGO = new GameObject("CharName");
            nameGO.transform.SetParent(btnGO.transform, false);
            TextMeshProUGUI nameTxt = nameGO.AddComponent<TextMeshProUGUI>();
            _charButtonTexts[i] = nameTxt;
            nameTxt.text = hasPrefabs && i < characterPrefabs.Count && characterPrefabs[i] != null ? characterPrefabs[i].name : $"Char {i + 1}";
            nameTxt.fontSize = 13f;
            nameTxt.alignment = TextAlignmentOptions.Center;
            nameTxt.color = Color.white;

            // Layout: image on top, name below (use vertical layout)
            RectTransform btnRt = btnGO.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.5f, 0.5f);
            btnRt.anchorMax = new Vector2(0.5f, 0.5f);
            btnRt.pivot = new Vector2(0.5f, 1f);
            btnRt.sizeDelta = new Vector2(btnWidth, btnHeight);

            float xPos = gridStartX + i * (btnWidth + gap);
            btnRt.anchoredPosition = new Vector2(xPos, gridY);

            // Wire click handler
            int idx = i;
            btnComp.onClick.AddListener(() => OnPartyMemberToggle(idx));
        }

        // ── Back button ─────────────────────────────────────────────
        GameObject backGO = new GameObject("BackButton");
        backGO.transform.SetParent(_charScreenRoot.transform, false);
        var backBtn = backGO.AddComponent<Button>();
        var backImg = backGO.AddComponent<Image>();
        backImg.color = new Color(0.4f, 0.2f, 0.15f, 0.9f);
        if (backGO.TryGetComponent<RectTransform>(out var backRt))
        {
            backRt.anchorMin = new Vector2(0f, 0f);
            backRt.anchorMax = new Vector2(0f, 0f);
            backRt.pivot = new Vector2(0f, 0f);
            backRt.anchoredPosition = new Vector2(20f, 20f);
            backRt.sizeDelta = new Vector2(80f, 35f);
        }

        GameObject backTextGO = new GameObject("BackLabel");
        backTextGO.transform.SetParent(backGO.transform, false);
        var backTxt = backTextGO.AddComponent<TextMeshProUGUI>();
        backTxt.text = "Back";
        backTxt.alignment = TextAlignmentOptions.Center;
        backTxt.fontSize = 13f;
        backTxt.color = Color.white;

        backBtn.onClick.AddListener(OnBackFromCharSelect);

        // ── Start button (hidden initially) ─────────────────────────
        GameObject startGO = new GameObject("StartButton");
        _charStartButtonGo = startGO;
        startGO.transform.SetParent(_charScreenRoot.transform, false);
        var startBtnComp = startGO.AddComponent<Button>();
        var startImgComp = startGO.AddComponent<Image>();
        startImgComp.color = new Color(0.15f, 0.6f, 0.2f, 0.9f);
        if (startGO.TryGetComponent<RectTransform>(out var startRt))
        {
            startRt.anchorMin = new Vector2(0.5f, 0f);
            startRt.anchorMax = new Vector2(0.5f, 0f);
            startRt.pivot = new Vector2(0.5f, 0f);
            startRt.anchoredPosition = new Vector2(0, -18f);
            startRt.sizeDelta = new Vector2(200f, 45f);
        }

        GameObject startTextGO = new GameObject("StartLabel");
        startTextGO.transform.SetParent(startGO.transform, false);
        var startTxtComp = startTextGO.AddComponent<TextMeshProUGUI>();
        startTxtComp.text = "Start Game";
        startTxtComp.alignment = TextAlignmentOptions.Center;
        startTxtComp.fontSize = 15f;
        startTxtComp.color = Color.white;

        // Wire the start button click
        startBtnComp.onClick.AddListener(OnPartymodeStartClicked);

        // ── Initialize preview thumbs array ─────────────────────────
        _charPreviewThumbs = new GameObject[CharacterSelection.MaxSlots];
        for (int i = 0; i < CharacterSelection.MaxSlots; i++)
            _charPreviewThumbs[i] = null;

        Debug.Log($"[MainMenuController] Dynamic char select created with {charCount} characters.");
    }

    /// <summary>Toggle a character on/off in the party. Same button adds AND removes.</summary>
    private void OnPartyMemberToggle(int charIndex)
    {
        // Check if already in party
        int existingSlot = -1;
        for (int i = activePartySlots.Count - 1; i >= 0; i--)
        {
            if (activePartySlots[i] == charIndex)
            {
                existingSlot = i;
                break;
            }
        }

        if (existingSlot >= 0) {
            // ── Toggle OFF: remove this character from the party ────────
            activePartySlots.RemoveAt(existingSlot);

            // Remove and shift preview thumbnails left
            for (int k = existingSlot; k + 1 < _charPreviewThumbs.Length; k++)
            {
                _charPreviewThumbs[k] = _charPreviewThumbs[k + 1];
                if (_charPreviewThumbs[k] != null)
                    _charPreviewThumbs[k].name = $"Preview_{k + 1}";
            }
            _charPreviewThumbs[_charPreviewThumbs.Length - 1] = null;

            selectedCharIndex = activePartySlots.Count > 0 ? activePartySlots[activePartySlots.Count - 1] : charIndex;
        } else {
            // ── Toggle ON: add this character to the party ────────────
            if (activePartySlots.Count >= CharacterSelection.MaxSlots) return;

            int newSlot = activePartySlots.Count;
            activePartySlots.Add(charIndex);

            // Add preview thumbnail at end of row
            GameObject thumb = CreatePreviewThumbnail(charIndex, newSlot);
            _charPreviewThumbs[newSlot] = thumb;

            selectedCharIndex = charIndex;
        }

        // Refresh visuals after toggle
        RefreshCharacterSelectUI();
    }

    /// <summary>Refresh all character select visuals after a toggle.</summary>
    private void RefreshCharacterSelectUI()
    {
        if (_charButtonImages == null) return;

        // Update button highlights: green = in party, neutral = not selected
        for (int i = 0; i < _charButtonImages.Length; i++)
        {
            if (_charButtonImages[i] == null) continue;
            bool inParty = IsActivePartyMember(i);

            if (inParty)
                _charButtonImages[i].color = new Color(0.2f, 0.9f, 0.35f, 0.85f); // green highlight
            else
                _charButtonImages[i].color = new Color(0.25f, 0.25f, 0.35f, 1f);   // neutral gray
        }

        // Update count text
        if (_charCountTextGO != null)
        {
            var txt = _charCountTextGO.GetComponent<TextMeshProUGUI>();
            if (txt != null)
                txt.text = $"{activePartySlots.Count} / 4 characters";
        }

        // Show/hide start button based on selection count
        if (_charStartButtonGo != null)
            _charStartButtonGo.SetActive(activePartySlots.Count > 0);

        // Fade in screen on first open
        if (_charScreenCanvasGroup && _charScreenCanvasGroup.alpha < 1f)
        {
            StartCoroutine(FadeInCharScreen());
        }
    }

    /// <summary>Create a preview thumbnail for the selected party row.</summary>
    private GameObject CreatePreviewThumbnail(int charIndex, int slotIndex)
    {
        if (charIndex >= characterPrefabs.Count || _charPreviewContainer == null) return null;

        GameObject go = new GameObject($"CharPreview_{slotIndex}");
        go.transform.SetParent(_charPreviewContainer, false);

        // Get the character's sprite
        Sprite sprite = null;
        GameObject prefab = characterPrefabs[charIndex];
        if (prefab != null)
        {
            var sprites = prefab.GetComponentsInChildren<SpriteRenderer>();
            if (sprites.Length > 0 && sprites[0].sprite != null)
                sprite = sprites[0].sprite;
            if (sprite == null)
            {
                var imgs = prefab.GetComponentsInChildren<Image>();
                foreach (var uiImg in imgs)
                {
                    if (uiImg.sprite != null) { sprite = uiImg.sprite; break; }
                }
            }
        }

        Image img = go.AddComponent<Image>();
        img.raycastTarget = false;  // thumbnails aren't clickable
        if (sprite != null)
            img.sprite = sprite;
        else
            img.color = PartySlotUI.SlotColors[slotIndex];

        RectTransform rt = go.GetComponent<RectTransform>();
        float thumbSize = 50f;
        float rowWidth = CharacterSelection.MaxSlots * (thumbSize + 8);
        float startX = -(rowWidth / 2) + slotIndex * (thumbSize + 8);
        rt.sizeDelta = new Vector2(thumbSize, thumbSize);
        rt.anchoredPosition = new Vector2(startX + thumbSize / 2 - 4, 0f);

        // Scale-in animation
        rt.localScale = Vector3.zero;
        StartCoroutine(PreviewScaleIn(rt));

        return go;
    }

    private static IEnumerator PreviewScaleIn(RectTransform rt)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * 6f;
            rt.localScale = Vector3.one * Mathf.SmoothStep(0f, 1f, t);
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    private IEnumerator FadeInCharScreen()
    {
        if (_charScreenCanvasGroup == null) yield break;
        float t = 0f;
        float start = _charScreenCanvasGroup.alpha;
        while (t < 1f && _charScreenCanvasGroup != null)
        {
            t += Time.unscaledDeltaTime * 4f;
            _charScreenCanvasGroup.alpha = Mathf.Lerp(start, 1f, t);
            yield return null;
        }
        if (_charScreenCanvasGroup != null)
            _charScreenCanvasGroup.alpha = 1f;
    }

    /// <summary>"Back" button — hides char select and returns to mode panel. Preserves selections.</summary>
    private void OnBackFromCharSelect()
    {
        // Hide character select screen
        if (_charScreenRoot != null)
            _charScreenRoot.SetActive(false);

        // Show mode panel
        modePanel?.SetActive(true);

        // Reset initialization flag so the next mode (party or single-player) starts fresh.
        // Party mode preserves selections across re-entries while on the char select screen,
        // but switching to a different mode must clear state.
        _charSelectInitialized = false;

        if (_charScreenCanvasGroup != null)
            _charScreenCanvasGroup.alpha = 0f;

        Debug.Log("[MainMenuController] Back from char select. Mode panel restored.");
    }
}

