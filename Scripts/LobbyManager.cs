using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using System.Threading.Tasks;
using UnityEngine.UI;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }

    [SerializeField] private RelayManager relayManager;

    [Header("Lobby creation")]
    [SerializeField] private GameObject lobbyCreationParent;
    [SerializeField] private TMP_InputField createLobbyNameField;
    [SerializeField] private TMP_Dropdown createLobbyGameModeDropdown;
    [SerializeField] private TMP_InputField createLobbyMaxPlayersField;
    [SerializeField] private TMP_InputField createLobbyPasswordField;
    [SerializeField] private Toggle createLobbyPrivateToggle;

    [Space(10)]
    [Header("Lobby list")]
    [SerializeField] private GameObject lobbyListParent;
    [SerializeField] private Transform lobbyContentParent;
    [SerializeField] private Transform lobbyItemPrefab;
    [SerializeField] private TMP_InputField searchLobbyNameInputField;

    [Space(10)]
    [Header("Profile setup")]
    [SerializeField] private GameObject profileSetupParent;
    [SerializeField] private TMP_InputField profileNameField;

    [Space(10)]
    [Header("Joined lobby")]
    [SerializeField] private GameObject joinedLobbyParent;
    [SerializeField] private Transform playerItemPrefab;
    [SerializeField] private Transform playerListParent;
    [SerializeField] private TextMeshProUGUI joinedLobbyNameText;
    [SerializeField] private TextMeshProUGUI joinedLobbyGamemodeText;
    [SerializeField] private GameObject joinedLobbyStartButton;

    [Space(10)]
    [Header("Password protection")]
    [SerializeField] private Button inputPasswordButton;
    [SerializeField] private TMP_InputField inputPasswordField;
    [SerializeField] private GameObject inputPasswordParent;


    private string playerName;
    private Player playerData;
    private string joinedLobbyId;

    private async void Start()
    {
        Instance = this;

        createLobbyPrivateToggle.onValueChanged.AddListener(OnCreateLobbyPrivateToggle);

        await UnityServices.InitializeAsync();
        await AuthenticationService.Instance.SignInAnonymouslyAsync();

        profileSetupParent.SetActive(true);
        lobbyListParent.SetActive(false);
        joinedLobbyParent.SetActive(false);
        lobbyCreationParent.SetActive(false);
        inputPasswordParent.SetActive(false);
    }

    public void OnCreateLobbyPrivateToggle(bool value)
    {
        createLobbyPasswordField.gameObject.SetActive(value);
    }

    public void CreateProfile()
    {
        playerName = profileNameField.text;
        profileSetupParent.SetActive(false);
        lobbyListParent.SetActive(true);
        ShowLobbies();

        PlayerDataObject playerDataObjectName = new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, playerName);
        PlayerDataObject playerDataObjectTeam = new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, "A");

        playerData = new Player(id: AuthenticationService.Instance.PlayerId, data:
        new Dictionary<string, PlayerDataObject> { { "Name", playerDataObjectName }, { "Team", playerDataObjectTeam } });
    }


    public async void JoinLobby(string lobbyID, bool needPassword)
    {
        if (needPassword)
        {
            try
            {
                await LobbyService.Instance.JoinLobbyByIdAsync(lobbyID, new JoinLobbyByIdOptions
                { Password = await InputPassword(), Player = playerData });

                joinedLobbyId = lobbyID;
                lobbyListParent.SetActive(false);
                joinedLobbyParent.SetActive(true);
                UpdateLobbyInfo();
            }
            catch (LobbyServiceException e)
            {
                Debug.Log(e);
            }
        }
        else
        {
            try
            {
                await LobbyService.Instance.JoinLobbyByIdAsync(lobbyID, new JoinLobbyByIdOptions { Player = playerData });
                lobbyListParent.SetActive(false);
                joinedLobbyParent.SetActive(true);

                joinedLobbyId = lobbyID;
                UpdateLobbyInfo();
            }
            catch (LobbyServiceException e)
            {
                Debug.Log(e);
            }
        }
    }

    private async Task<string> InputPassword()
    {
        bool waiting = true;
        inputPasswordParent.SetActive(true);

        while (waiting)
        {
            inputPasswordButton.onClick.AddListener(() => waiting = false);
            await Task.Yield();
        }

        inputPasswordParent.SetActive(false);
        return inputPasswordField.text;
    }

    private async void ShowLobbies()
    {
        while (Application.isPlaying && lobbyListParent.activeInHierarchy)
        {
            QueryLobbiesOptions queryLobbiesOptions = new QueryLobbiesOptions();
            queryLobbiesOptions.Filters = new List<QueryFilter>();

            if (searchLobbyNameInputField.text != string.Empty)
            {
                queryLobbiesOptions.Filters.Add(new QueryFilter(QueryFilter.FieldOptions.Name, searchLobbyNameInputField.text, QueryFilter.OpOptions.CONTAINS));
            }

            QueryResponse queryResponse = await Lobbies.Instance.QueryLobbiesAsync(queryLobbiesOptions);

            foreach (Transform t in lobbyContentParent)
            {
                Destroy(t.gameObject);
            }

            foreach (Lobby lobby in queryResponse.Results)
            {
                Transform newLobbyItem = Instantiate(lobbyItemPrefab, lobbyContentParent);
                newLobbyItem.GetComponent<JoinLobbyButton>().lobbyId = lobby.Id;
                newLobbyItem.GetComponent<JoinLobbyButton>().needPassword = lobby.HasPassword;
                newLobbyItem.GetChild(0).GetComponent<TextMeshProUGUI>().text = lobby.Name;
                newLobbyItem.GetChild(1).GetComponent<TextMeshProUGUI>().text = lobby.Data["GameMode"].Value;
                newLobbyItem.GetChild(2).GetComponent<TextMeshProUGUI>().text = lobby.Players.Count + "/" + lobby.MaxPlayers;
            }

            await Task.Delay(1000);
        }
    }

    public async void LobbyStart()
    {
        Lobby lobby = await Lobbies.Instance.GetLobbyAsync(joinedLobbyId);
        string JoinCode = await relayManager.StartHostWithRelay(lobby.MaxPlayers);
        isJoined = true;
        await Lobbies.Instance.UpdateLobbyAsync(joinedLobbyId, new UpdateLobbyOptions
        { Data = new Dictionary<string, DataObject> { { "JoinCode", new DataObject(DataObject.VisibilityOptions.Public, JoinCode) } } });

        lobbyListParent.SetActive(false);
        joinedLobbyParent.SetActive(false);
    }


    private bool isJoined = false;
    private async void UpdateLobbyInfo()
    {
        while (Application.isPlaying)
        {
            if (string.IsNullOrEmpty(joinedLobbyId))
            {
                return;
            }

            Lobby lobby = await Lobbies.Instance.GetLobbyAsync(joinedLobbyId);

            if (!isJoined && lobby.Data["JoinCode"].Value != string.Empty)
            {
                await relayManager.StartClientWithRelay(lobby.Data["JoinCode"].Value);
                isJoined = true;
                joinedLobbyParent.SetActive(false);
                return;
            }

            if (AuthenticationService.Instance.PlayerId == lobby.HostId)
            {
                joinedLobbyStartButton.SetActive(true);
            }
            else
            {
                joinedLobbyStartButton.SetActive(false);
            }

            joinedLobbyNameText.text = lobby.Name;
            joinedLobbyGamemodeText.text = lobby.Data["GameMode"].Value;

            foreach (Transform t in playerListParent)
            {
                Destroy(t.gameObject);
            }

            foreach (Player player in lobby.Players)
            {
                Transform newPlayerItem = Instantiate(playerItemPrefab, playerListParent);
                newPlayerItem.GetChild(0).GetComponent<TextMeshProUGUI>().text = player.Data["Name"].Value;
                newPlayerItem.GetChild(1).GetComponent<TextMeshProUGUI>().text = player.Data["Team"].Value;
                newPlayerItem.GetChild(2).GetComponent<TextMeshProUGUI>().text = (lobby.HostId == player.Id) ? "Owner" : "User";
            }

            await Task.Delay(1000);
        }
    }


    public void ExitLobbyCreationButton()
    {
        lobbyCreationParent.SetActive(false);
        lobbyListParent.SetActive(true);
        ShowLobbies();
    }

    public void CreateNewLobbyButton()
    {
        lobbyCreationParent.SetActive(true);
        lobbyListParent.SetActive(false);
    }


    public async void CreateLobby()
    {
        if (!int.TryParse(createLobbyMaxPlayersField.text, out int maxPlayers))
        {
            Debug.LogWarning("Incorrect player count");
            return;
        }

        Lobby createdLobby = null;

        CreateLobbyOptions options = new CreateLobbyOptions();
        options.IsPrivate = false;
        options.Player = playerData;

        if (createLobbyPrivateToggle.isOn)
        {
            options.Password = createLobbyPasswordField.text;
        }

        DataObject dataObjectGameMode = new DataObject(DataObject.VisibilityOptions.Public,
            createLobbyGameModeDropdown.options[createLobbyGameModeDropdown.value].text);

        DataObject dataObjectJoinCode = new DataObject(DataObject.VisibilityOptions.Public, string.Empty);

        options.Data = new Dictionary<string, DataObject> { { "GameMode", dataObjectGameMode }, { "JoinCode", dataObjectJoinCode } };

        try
        {
            createdLobby = await LobbyService.Instance.CreateLobbyAsync(createLobbyNameField.text, maxPlayers, options);
            lobbyCreationParent.SetActive(false);
            joinedLobbyParent.SetActive(true);
            joinedLobbyId = createdLobby.Id;
            UpdateLobbyInfo();
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }

        LobbyHeartbeat(createdLobby);
    }

    private async void LobbyHeartbeat(Lobby lobby)
    {
        while (true)
        {
            if (lobby == null)
            {
                return;
            }

            await LobbyService.Instance.SendHeartbeatPingAsync(lobby.Id);

            await Task.Delay(15 * 1000);
        }
    }

    public async void SwitchPlayerTeamButton()
    {
        string newTeam;
        if (playerData.Data["Team"].Value == "A")
        {
            newTeam = "B";
        }
        else
        {
            newTeam = "A";
        }

        await Lobbies.Instance.UpdatePlayerAsync(joinedLobbyId, AuthenticationService.Instance.PlayerId, new UpdatePlayerOptions
        { Data = new Dictionary<string, PlayerDataObject> { { "Team", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, newTeam) } } });
        playerData.Data["Team"].Value = newTeam;
    }

}