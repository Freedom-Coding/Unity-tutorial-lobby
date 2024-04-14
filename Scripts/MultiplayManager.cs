using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.Services.Core;
using Unity.Services.Multiplay;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Threading.Tasks;

public class MultiplayManager : MonoBehaviour
{
    [SerializeField] private TMP_InputField ipAddressInputField;
    [SerializeField] private TMP_InputField portInputField;

    private IServerQueryHandler serverQueryHandler;

    private async void Start()
    {
        if (Application.platform == RuntimePlatform.LinuxServer)
        {
            Application.targetFrameRate = 60;

            await UnityServices.InitializeAsync();

            ServerConfig serverConfig = MultiplayService.Instance.ServerConfig;

            serverQueryHandler = await MultiplayService.Instance.StartServerQueryHandlerAsync(10, "MyServer", "MyGameType", "0", "TestMap");

            if (serverConfig.AllocationId != string.Empty)
            {
                NetworkManager.Singleton.GetComponent<UnityTransport>().SetConnectionData("0.0.0.0", serverConfig.Port, "0.0.0.0");

                NetworkManager.Singleton.StartServer();

                await MultiplayService.Instance.ReadyServerForPlayersAsync();
            }
        }
    }

    private async void Update()
    {
        if (Application.platform == RuntimePlatform.LinuxServer)
        {
            if (serverQueryHandler != null)
            {
                serverQueryHandler.CurrentPlayers = (ushort)NetworkManager.Singleton.ConnectedClientsIds.Count;
                serverQueryHandler.UpdateServerCheck();
                await Task.Delay(100);
            }
        }
    }

    public void JoinToServer()
    {
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        transport.SetConnectionData(ipAddressInputField.text, ushort.Parse(portInputField.text));

        NetworkManager.Singleton.StartClient();
    }
}