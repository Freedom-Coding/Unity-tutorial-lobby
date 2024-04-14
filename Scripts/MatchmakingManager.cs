using System.Collections;
using System.Collections.Generic;
using Unity.Services.Authentication;
using Unity.Services.Matchmaker;
using Unity.Services.Matchmaker.Models;
using UnityEngine;
using TMPro;
using System.Threading.Tasks;
using Unity.Netcode.Transports.UTP;
using Unity.Netcode;
using Unity.Services.Core;
using Unity.Services.Multiplay;

public class MatchmakingManager : NetworkBehaviour
{
    [SerializeField] private TMP_Dropdown gameModeDropdown;

    private PayloadAllocation payloadAllocation;
    private IMatchmakerService matchmakerService;
    private string backfillTicketId;

    private NetworkManager networkManager;
    private string currentTicket;

    private async void Start()
    {
        networkManager = NetworkManager.Singleton;

        if (Application.platform != RuntimePlatform.LinuxServer)
        {
            await UnityServices.InitializeAsync();
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        else
        {
            while (UnityServices.State == ServicesInitializationState.Uninitialized || UnityServices.State == ServicesInitializationState.Initializing)
            {
                await Task.Yield();
            }

            matchmakerService = MatchmakerService.Instance;
            payloadAllocation = await MultiplayService.Instance.GetPayloadAllocationFromJsonAs<PayloadAllocation>();
            backfillTicketId = payloadAllocation.BackfillTicketId;
        }
    }

    bool isDeallocating = false;
    bool deallocatingCancellationToken = false;

    private async void Update()
    {
        if (Application.platform == RuntimePlatform.LinuxServer)
        {
            if (NetworkManager.Singleton.ConnectedClientsList.Count == 0 && !isDeallocating)
            {
                isDeallocating = true;
                deallocatingCancellationToken = false;
                Deallocate();
            }

            if (NetworkManager.Singleton.ConnectedClientsList.Count != 0)
            {
                isDeallocating = false;
                deallocatingCancellationToken = true;
            }

            if (backfillTicketId != null && NetworkManager.Singleton.ConnectedClientsList.Count < 4)
            {
                BackfillTicket backfillTicket = await MatchmakerService.Instance.ApproveBackfillTicketAsync(backfillTicketId);
                backfillTicketId = backfillTicket.Id;
            }

            await Task.Delay(1000);
        }
    }

    private void OnPlayerConnected()
    {
        if (Application.platform == RuntimePlatform.LinuxServer)
        {
            UpdateBackfillTicket();
        }
    }
    private void OnPlayerDisconnected()
    {
        if (Application.platform == RuntimePlatform.LinuxServer)
        {
            UpdateBackfillTicket();
        }
    }

    private async void UpdateBackfillTicket()
    {
        List<Player> players = new List<Player>();

        foreach (ulong playerId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            players.Add(new Player(playerId.ToString()));
        }

        MatchProperties matchProperties = new MatchProperties(null, players, null, backfillTicketId);

        await MatchmakerService.Instance.UpdateBackfillTicketAsync(payloadAllocation.BackfillTicketId,
            new BackfillTicket(backfillTicketId, properties: new BackfillTicketProperties(matchProperties)));
    }


    private async void Deallocate()
    {
        await Task.Delay(60 * 1000);

        if (!deallocatingCancellationToken)
        {
            Application.Quit();
        }
    }

    private void OnApplicationQuit()
    {
        if (Application.platform != RuntimePlatform.LinuxServer)
        {
            if (networkManager.IsConnectedClient)
            {
                networkManager.Shutdown(true);
                networkManager.DisconnectClient(OwnerClientId);
            }
        }
    }


    public async void ClientJoin()
    {
        CreateTicketOptions createTicketOptions = new CreateTicketOptions("MyQueue",
            new Dictionary<string, object> { { "GameMode", gameModeDropdown.options[gameModeDropdown.value].text } });

        List<Player> players = new List<Player> { new Player(AuthenticationService.Instance.PlayerId) };

        CreateTicketResponse createTicketResponse = await MatchmakerService.Instance.CreateTicketAsync(players, createTicketOptions);
        currentTicket = createTicketResponse.Id;
        Debug.Log("Ticket created");

        while (true)
        {
            TicketStatusResponse ticketStatusResponse = await MatchmakerService.Instance.GetTicketAsync(createTicketResponse.Id);

            if (ticketStatusResponse.Type == typeof(MultiplayAssignment))
            {
                MultiplayAssignment multiplayAssignment = (MultiplayAssignment)ticketStatusResponse.Value;

                if (multiplayAssignment.Status == MultiplayAssignment.StatusOptions.Found)
                {
                    UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                    transport.SetConnectionData(multiplayAssignment.Ip, ushort.Parse(multiplayAssignment.Port.ToString()));
                    NetworkManager.Singleton.StartClient();

                    Debug.Log("Match found");
                    return;
                }
                else if (multiplayAssignment.Status == MultiplayAssignment.StatusOptions.Timeout)
                {
                    Debug.Log("Match timeout");
                    return;
                }
                else if (multiplayAssignment.Status == MultiplayAssignment.StatusOptions.Failed)
                {
                    Debug.Log("Match failed" + multiplayAssignment.Status + "  " + multiplayAssignment.Message);
                    return;
                }
                else if (multiplayAssignment.Status == MultiplayAssignment.StatusOptions.InProgress)
                {
                    Debug.Log("Match is in progress");
                }

            }

            await Task.Delay(1000);
        }

    }

    [System.Serializable]
    public class PayloadAllocation
    {
        public MatchProperties MatchProperties;
        public string GeneratorName;
        public string QueueName;
        public string PoolName;
        public string EnvironmentId;
        public string BackfillTicketId;
        public string MatchId;
        public string PoolId;
    }
}
