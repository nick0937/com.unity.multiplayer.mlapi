﻿using MLAPI.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Security.Cryptography;
using MLAPI.NetworkingManagerComponents.Cryptography;
using MLAPI.NetworkingManagerComponents.Core;
using UnityEngine.SceneManagement;
using MLAPI.NetworkingManagerComponents.Binary;
using MLAPI.Data.Transports;
using MLAPI.Data.Transports.UNET;
using MLAPI.Data.NetworkProfiler;

namespace MLAPI.MonoBehaviours.Core
{
    /// <summary>
    /// The main component of the library
    /// </summary>
    [AddComponentMenu("MLAPI/NetworkingManager", -100)]
    public class NetworkingManager : MonoBehaviour
    {
        /// <summary>
        /// A syncronized time, represents the time in seconds since the server application started. Is replicated across all clients
        /// </summary>
        public float NetworkTime { get; internal set; }
        /// <summary>
        /// Gets or sets if the NetworkingManager should be marked as DontDestroyOnLoad
        /// </summary>
        [HideInInspector]
        public bool DontDestroy = true;
        /// <summary>
        /// Gets or sets if the application should be set to run in background
        /// </summary>
        [HideInInspector]
        public bool RunInBackground = true;
        /// <summary>
        /// The log level to use
        /// </summary>
        [HideInInspector]
        public LogLevel LogLevel = LogLevel.Normal;
        /// <summary>
        /// The singleton instance of the NetworkingManager
        /// </summary>
        public static NetworkingManager singleton { get; private set; }
        /// <summary>
        /// Gets the networkId of the server
        /// </summary>
        public uint ServerNetId => NetworkConfig.NetworkTransport.ServerNetId;
        /// <summary>
        /// The clientId the server calls the local client by, only valid for clients
        /// </summary>
        public uint LocalClientId 
        {
            get
            {
                if (isHost)
                    return NetworkConfig.NetworkTransport.HostDummyId;
                if (isServer)
                    return NetworkConfig.NetworkTransport.InvalidDummyId;
                
                return localClientId;
            }
            internal set
            {
                localClientId = value;
            }
        }
        private uint localClientId;
        /// <summary>
        /// Gets a dictionary of connected clients and their clientId keys
        /// </summary>
        public readonly Dictionary<uint, NetworkedClient> ConnectedClients = new Dictionary<uint, NetworkedClient>();
        /// <summary>
        /// Gets a list of connected clients
        /// </summary>
        public readonly List<NetworkedClient> ConnectedClientsList = new List<NetworkedClient>();
        internal readonly HashSet<uint> pendingClients = new HashSet<uint>();
        /// <summary>
        /// Gets wheter or not a server is running
        /// </summary>
        public bool isServer { get; internal set; }
        /// <summary>
        /// Gets wheter or not a client is running
        /// </summary>
        public bool isClient { get; internal set; }
        /// <summary>
        /// Gets if we are running as host
        /// </summary>
        public bool isHost => isServer && isClient;
        /// <summary>
        /// Gets wheter or not we are listening for connections
        /// </summary>
        public bool isListening { get; internal set; }
        private byte[] messageBuffer;
        /// <summary>
        /// Gets if we are connected as a client
        /// </summary>
        public bool isConnectedClients { get; internal set; }
        /// <summary>
        /// The callback to invoke once a client connects
        /// </summary>
        public Action<uint> OnClientConnectedCallback = null;
        /// <summary>
        /// The callback to invoke when a client disconnects
        /// </summary>
        public Action<uint> OnClientDisconnectCallback = null;
        /// <summary>
        /// The callback to invoke once the server is ready
        /// </summary>
        public Action OnServerStarted = null;
        /// <summary>
        /// Delegate type called when connection has been approved
        /// </summary>
        /// <param name="clientId">The clientId of the approved client</param>
        /// <param name="prefabId">The prefabId to use for the client</param>
        /// <param name="approved">Wheter or not the client was approved</param>
        /// <param name="position">The position to spawn the client at</param>
        /// <param name="rotation">The rotation to spawn the client with</param>
        public delegate void ConnectionApprovedDelegate(uint clientId, int prefabId, bool approved, Vector3 position, Quaternion rotation);
        /// <summary>
        /// The callback to invoke during connection approval
        /// </summary>
        public Action<byte[], uint, ConnectionApprovedDelegate> ConnectionApprovalCallback = null;
        /// <summary>
        /// The current NetworkingConfiguration
        /// </summary>
        public NetworkConfig NetworkConfig;
        /// <summary>
        /// Delegate used for incomming custom messages
        /// </summary>
        /// <param name="clientId">The clientId that sent the message</param>
        /// <param name="reader">The reader containing the message data</param>
        public delegate void CustomMessageDelegete(uint clientId, BitReader reader);
        /// <summary>
        /// Event invoked when custom messages arrive
        /// </summary>
        public event CustomMessageDelegete OnIncommingCustomMessage;

        internal void InvokeOnIncommingCustomMessage(uint clientId, BitReader reader)
        {
            if (OnIncommingCustomMessage != null) OnIncommingCustomMessage(clientId, reader);
        }

        /// <summary>
        /// Sends custom message to a list of clients
        /// </summary>
        /// <param name="clientIds">The clients to send to, sends to everyone if null</param>
        /// <param name="writer">The message writer containing the data</param>
        /// <param name="channel">The channel to send the data on</param>
        public void SendCustomMessage(List<uint> clientIds, BitWriter writer, string channel = "MLAPI_DEFAULT_MESSAGE")
        {
            if (!isServer)
            {
                if (LogHelper.CurrentLogLevel <= LogLevel.Error) LogHelper.LogWarning("Can not send custom message to multiple users as a client");
                return;
            }
            if (clientIds == null)
            {
                for (int i = 0; i < ConnectedClientsList.Count; i++)
                {
                    InternalMessageHandler.Send(ConnectedClientsList[i].ClientId, "MLAPI_CUSTOM_MESSAGE", channel, writer);
                }
            }
            else
            {
                for (int i = 0; i < clientIds.Count; i++)
                {
                    InternalMessageHandler.Send(clientIds[i], "MLAPI_CUSTOM_MESSAGE", channel, writer);
                }
            }
        }

        /// <summary>
        /// Sends a custom message to a specific client
        /// </summary>
        /// <param name="clientId">The client to send the message to</param>
        /// <param name="writer">The message writer containing the data</param>
        /// <param name="channel">The channel tos end the data on</param>
        public void SendCustomMessage(uint clientId, BitWriter writer, string channel = "MLAPI_DEFAULT_MESSAGE")
        {
            InternalMessageHandler.Send(clientId, "MLAPI_CUSTOM_MESSAGE", channel, writer);
        }


#if !DISABLE_CRYPTOGRAPHY
        internal EllipticDiffieHellman clientDiffieHellman;
        internal readonly Dictionary<uint, byte[]> diffieHellmanPublicKeys = new Dictionary<uint, byte[]>();
        internal byte[] clientAesKey;
#endif

        /// <summary>
        /// An inspector bool that acts as a Trigger for regenerating RSA keys. Should not be used outside Unity editor.
        /// </summary>
        public bool RegenerateRSAKeys = false;

        private void OnValidate()
        {
            if (NetworkConfig == null)
                return; //May occur when the component is added            

            //Sort lists
            if (NetworkConfig.Channels != null)
                NetworkConfig.Channels = NetworkConfig.Channels.OrderBy(x => x.Name).ToList();
            if (NetworkConfig.NetworkedPrefabs != null)
                NetworkConfig.NetworkedPrefabs = NetworkConfig.NetworkedPrefabs.OrderBy(x => x.name).ToList(); 
            if (NetworkConfig.RegisteredScenes != null)
                NetworkConfig.RegisteredScenes.Sort();

            if (NetworkConfig.EnableSceneSwitching && !NetworkConfig.RegisteredScenes.Contains(SceneManager.GetActiveScene().name))
            {
                if (LogHelper.CurrentLogLevel <= LogLevel.Normal) LogHelper.LogWarning("The active scene is not registered as a networked scene. The MLAPI has added it");
                NetworkConfig.RegisteredScenes.Add(SceneManager.GetActiveScene().name);
            }

            if (!NetworkConfig.EnableSceneSwitching && NetworkConfig.HandleObjectSpawning)
            {
                if (LogHelper.CurrentLogLevel <= LogLevel.Normal) LogHelper.LogWarning("Please be aware that Scene objects are NOT supported if SceneManagement is turned off, even if HandleObjectSpawning is turned on");
            }

            if (NetworkConfig.HandleObjectSpawning)
            {
                for (int i = 0; i < NetworkConfig.NetworkedPrefabs.Count; i++)
                {
                    if (string.IsNullOrEmpty(NetworkConfig.NetworkedPrefabs[i].name))
                    {
                        if (LogHelper.CurrentLogLevel <= LogLevel.Normal) LogHelper.LogWarning("The NetworkedPrefab " + NetworkConfig.NetworkedPrefabs[i].prefab.name + " does not have a NetworkedPrefabName.");
                    }
                }
                int playerPrefabCount = NetworkConfig.NetworkedPrefabs.Count(x => x.playerPrefab == true);
                if (playerPrefabCount == 0)
                {
                    if (LogHelper.CurrentLogLevel <= LogLevel.Normal) LogHelper.LogWarning("There is no NetworkedPrefab marked as a PlayerPrefab");
                }
                else if (playerPrefabCount > 1)
                {
                    if (LogHelper.CurrentLogLevel <= LogLevel.Normal) LogHelper.LogWarning("Only one networked prefab can be marked as a player prefab");
                }
                else NetworkConfig.PlayerPrefabName = NetworkConfig.NetworkedPrefabs.Find(x => x.playerPrefab == true).name;

            }

            if (!NetworkConfig.EnableEncryption)
            {
                RegenerateRSAKeys = false;
            }
            else
            {
                if (RegenerateRSAKeys)
                {
#if !DISABLE_CRYPTOGRAPHY
                    using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
                    {
                        rsa.PersistKeyInCsp = false;
                        NetworkConfig.RSAPrivateKey = rsa.ToXmlString(true);
                        NetworkConfig.RSAPublicKey = rsa.ToXmlString(false);
                    }
#endif
                    RegenerateRSAKeys = false;
                }
            }
        }

        private object Init(bool server)
        {
            if (LogHelper.CurrentLogLevel <= LogLevel.Developer) LogHelper.LogInfo("Init()");
            LocalClientId = 0;
            NetworkTime = 0f;
            lastSendTickTime = 0f;
            lastEventTickTime = 0f;
            lastReceiveTickTime = 0f;
            eventOvershootCounter = 0f;
            pendingClients.Clear();
            ConnectedClients.Clear();
            ConnectedClientsList.Clear();
            messageBuffer = new byte[NetworkConfig.MessageBufferSize];
#if !DISABLE_CRYPTOGRAPHY
            diffieHellmanPublicKeys.Clear();
#endif
            MessageManager.channels.Clear();
            MessageManager.messageTypes.Clear();
            MessageManager.messageCallbacks.Clear();
            MessageManager.messageHandlerCounter.Clear();
            MessageManager.releasedMessageHandlerCounters.Clear();
            MessageManager.reverseChannels.Clear();
            MessageManager.reverseMessageTypes.Clear();
            SpawnManager.SpawnedObjects.Clear();
            SpawnManager.SpawnedObjectsList.Clear();
            SpawnManager.releasedNetworkObjectIds.Clear();
            NetworkPoolManager.Pools.Clear();
            NetworkPoolManager.PoolNamesToIndexes.Clear();
            NetworkSceneManager.registeredSceneNames.Clear();
            NetworkSceneManager.sceneIndexToString.Clear();
            NetworkSceneManager.sceneNameToIndex.Clear();
            InternalMessageHandler.FinalMessageBuffer = new byte[NetworkConfig.MessageBufferSize];
            CryptographyHelper.EncryptionBuffer = new byte[NetworkConfig.MessageBufferSize];

            if (NetworkConfig.Transport == DefaultTransport.UNET)
                NetworkConfig.NetworkTransport = new UnetTransport();
            else if (NetworkConfig.Transport == DefaultTransport.MLAPI_Relay)
                NetworkConfig.NetworkTransport = new RelayedTransport();
            else if (NetworkConfig.Transport == DefaultTransport.Custom && NetworkConfig.NetworkTransport == null)
                throw new NullReferenceException("The current NetworkTransport is null");

            object settings = NetworkConfig.NetworkTransport.GetSettings(); //Gets a new "settings" object for the transport currently used.

            if (NetworkConfig.HandleObjectSpawning)
            {
                NetworkConfig.NetworkPrefabIds = new Dictionary<string, int>();
                NetworkConfig.NetworkPrefabNames = new Dictionary<int, string>();
                NetworkConfig.NetworkedPrefabs = NetworkConfig.NetworkedPrefabs.OrderBy(x => x.name).ToList();
                HashSet<string> networkedPrefabName = new HashSet<string>();
                for (int i = 0; i < NetworkConfig.NetworkedPrefabs.Count; i++)
                {
                    if (networkedPrefabName.Contains(NetworkConfig.NetworkedPrefabs[i].name))
                    {
                        if (LogHelper.CurrentLogLevel <= LogLevel.Normal) LogHelper.LogWarning("Duplicate NetworkedPrefabName " + NetworkConfig.NetworkedPrefabs[i].name);
                        continue;
                    }
                    NetworkConfig.NetworkPrefabIds.Add(NetworkConfig.NetworkedPrefabs[i].name, i);
                    NetworkConfig.NetworkPrefabNames.Add(i, NetworkConfig.NetworkedPrefabs[i].name);
                    networkedPrefabName.Add(NetworkConfig.NetworkedPrefabs[i].name);
                }
            }

            //MLAPI channels and messageTypes
            List<Channel> internalChannels = new List<Channel>
            {
                new Channel()
                {
                    Name = "MLAPI_INTERNAL",
                    Type = NetworkConfig.NetworkTransport.InternalChannel
                },
                new Channel()
                {
                    Name = "MLAPI_DEFAULT_MESSAGE",
                    Type = ChannelType.Reliable
                },
                new Channel()
                {
                    Name = "MLAPI_POSITION_UPDATE",
                    Type = ChannelType.StateUpdate
                },
                new Channel()
                {
                    Name = "MLAPI_ANIMATION_UPDATE",
                    Type = ChannelType.ReliableSequenced
                },
                new Channel()
                {
                    Name = "MLAPI_NAV_AGENT_STATE",
                    Type = ChannelType.ReliableSequenced
                },
                new Channel()
                {
                    Name = "MLAPI_NAV_AGENT_CORRECTION",
                    Type = ChannelType.StateUpdate
                },
                new Channel()
                {
                    Name = "MLAPI_TIME_SYNC",
                    Type = ChannelType.Unreliable
                }
            };

            HashSet<string> channelNames = new HashSet<string>();
            //Register internal channels
            for (int i = 0; i < internalChannels.Count; i++)
            {
                if (channelNames.Contains(internalChannels[i].Name))
                {
                    if (LogHelper.CurrentLogLevel <= LogLevel.Normal) LogHelper.LogWarning("Duplicate channel name: " + NetworkConfig.Channels[i].Name);
                    continue;
                }
                int channelId = NetworkConfig.NetworkTransport.AddChannel(internalChannels[i].Type, settings);
                MessageManager.channels.Add(internalChannels[i].Name, channelId);
                channelNames.Add(internalChannels[i].Name);
                MessageManager.reverseChannels.Add(channelId, internalChannels[i].Name);
            }

            NetworkConfig.RegisteredScenes.Sort();
            if (NetworkConfig.EnableSceneSwitching)
            {
                for (int i = 0; i < NetworkConfig.RegisteredScenes.Count; i++)
                {
                    NetworkSceneManager.registeredSceneNames.Add(NetworkConfig.RegisteredScenes[i]);
                    NetworkSceneManager.sceneIndexToString.Add((uint)i, NetworkConfig.RegisteredScenes[i]);
                    NetworkSceneManager.sceneNameToIndex.Add(NetworkConfig.RegisteredScenes[i], (uint)i);
                }

                NetworkSceneManager.SetCurrentSceneIndex();
            }

            //Register user channels
            NetworkConfig.Channels = NetworkConfig.Channels.OrderBy(x => x.Name).ToList();
            for (int i = 0; i < NetworkConfig.Channels.Count; i++)
            {
                if(channelNames.Contains(NetworkConfig.Channels[i].Name))
                {
                    if (LogHelper.CurrentLogLevel <= LogLevel.Normal) LogHelper.LogWarning("Duplicate channel name: " + NetworkConfig.Channels[i].Name);
                    continue;
                }
                int channelId = NetworkConfig.NetworkTransport.AddChannel(NetworkConfig.Channels[i].Type, settings);
                MessageManager.channels.Add(NetworkConfig.Channels[i].Name, channelId);
                channelNames.Add(NetworkConfig.Channels[i].Name);
                MessageManager.reverseChannels.Add(channelId, NetworkConfig.Channels[i].Name);
            }

            //Add internal messagetypes directly
            MessageManager.messageTypes.Add("MLAPI_CONNECTION_REQUEST", MLAPIConstants.MLAPI_CONNECTION_REQUEST);
            MessageManager.messageTypes.Add("MLAPI_CONNECTION_APPROVED", MLAPIConstants.MLAPI_CONNECTION_APPROVED);
            MessageManager.messageTypes.Add("MLAPI_ADD_OBJECT", MLAPIConstants.MLAPI_ADD_OBJECT);
            MessageManager.messageTypes.Add("MLAPI_CLIENT_DISCONNECT", MLAPIConstants.MLAPI_CLIENT_DISCONNECT);
            MessageManager.messageTypes.Add("MLAPI_DESTROY_OBJECT", MLAPIConstants.MLAPI_DESTROY_OBJECT);
            MessageManager.messageTypes.Add("MLAPI_SWITCH_SCENE", MLAPIConstants.MLAPI_SWITCH_SCENE);
            MessageManager.messageTypes.Add("MLAPI_SPAWN_POOL_OBJECT", MLAPIConstants.MLAPI_SPAWN_POOL_OBJECT);
            MessageManager.messageTypes.Add("MLAPI_DESTROY_POOL_OBJECT", MLAPIConstants.MLAPI_DESTROY_POOL_OBJECT);
            MessageManager.messageTypes.Add("MLAPI_CHANGE_OWNER", MLAPIConstants.MLAPI_CHANGE_OWNER);
            MessageManager.messageTypes.Add("MLAPI_ADD_OBJECTS", MLAPIConstants.MLAPI_ADD_OBJECTS);
            MessageManager.messageTypes.Add("MLAPI_TIME_SYNC", MLAPIConstants.MLAPI_TIME_SYNC);
            MessageManager.messageTypes.Add("MLAPI_NETWORKED_VAR_DELTA", MLAPIConstants.MLAPI_NETWORKED_VAR_DELTA);
            MessageManager.messageTypes.Add("MLAPI_NETWORKED_VAR_UPDATE", MLAPIConstants.MLAPI_NETWORKED_VAR_UPDATE);
            MessageManager.messageTypes.Add("MLAPI_SERVER_RPC", MLAPIConstants.MLAPI_SERVER_RPC);
            MessageManager.messageTypes.Add("MLAPI_CLIENT_RPC", MLAPIConstants.MLAPI_CLIENT_RPC);
            MessageManager.messageTypes.Add("MLAPI_CUSTOM_MESSAGE", MLAPIConstants.MLAPI_CUSTOM_MESSAGE);

            return settings;
        }

        private void SpawnSceneObjects()
        {
            if (NetworkConfig.EnableSceneSwitching)
            {
                SpawnManager.MarkSceneObjects();
                if (isServer && NetworkConfig.HandleObjectSpawning)
                {
                    NetworkedObject[] networkedObjects = FindObjectsOfType<NetworkedObject>();
                    for (int i = 0; i < networkedObjects.Length; i++)
                    {
                        if (networkedObjects[i].sceneObject == null || networkedObjects[i].sceneObject == true)
                            networkedObjects[i].Spawn();
                    }
                }
            }
        }

        /// <summary>
        /// Starts a server
        /// </summary>
        public void StartServer()
        {
            if (LogHelper.CurrentLogLevel <= LogLevel.Developer) LogHelper.LogInfo("StartServer()");
            if (isServer || isClient)
            {
                if (LogHelper.CurrentLogLevel <= LogLevel.Normal) LogHelper.LogWarning("Cannot start server while an instance is already running");
                return;
            }

            if (NetworkConfig.ConnectionApproval)
            {
                if (ConnectionApprovalCallback == null)
                {
                    if (LogHelper.CurrentLogLevel <= LogLevel.Normal) LogHelper.LogWarning("No ConnectionApproval callback defined. Connection approval will timeout");
                }
            }

            object settings = Init(true);
            NetworkConfig.NetworkTransport.RegisterServerListenSocket(settings);

            isServer = true;
            isClient = false;
            isListening = true;

            SpawnSceneObjects();

            if (OnServerStarted != null)
                OnServerStarted.Invoke();
        }

        /// <summary>
        /// Starts a client
        /// </summary>
        public void StartClient()
        {
            if (LogHelper.CurrentLogLevel <= LogLevel.Developer) LogHelper.LogInfo("StartClient()");
            if (isServer || isClient)
            {
                if (LogHelper.CurrentLogLevel <= LogLevel.Normal) LogHelper.LogWarning("Cannot start client while an instance is already running");
                return;
            }

            object settings = Init(false);
            byte error;
            NetworkConfig.NetworkTransport.Connect(NetworkConfig.ConnectAddress, NetworkConfig.ConnectPort, settings, out error);
            isServer = false;
            isClient = true;
            isListening = true;
        }

        /// <summary>
        /// Stops the running server
        /// </summary>
        public void StopServer()
        {
            if (LogHelper.CurrentLogLevel <= LogLevel.Developer) LogHelper.LogInfo("StopServer()");
            HashSet<uint> disconnectedIds = new HashSet<uint>();
            //Don't know if I have to disconnect the clients. I'm assuming the NetworkTransport does all the cleaning on shtudown. But this way the clients get a disconnect message from server (so long it does't get lost)
            foreach (KeyValuePair<uint, NetworkedClient> pair in ConnectedClients)
            {
                if(!disconnectedIds.Contains(pair.Key))
                {
                    disconnectedIds.Add(pair.Key);
                    if (pair.Key == NetworkConfig.NetworkTransport.HostDummyId ||
                        pair.Key == NetworkConfig.NetworkTransport.InvalidDummyId)
                        continue;

                    NetworkConfig.NetworkTransport.DisconnectClient(pair.Key);
                }
            }
            foreach (uint clientId in pendingClients)
            {
                if (!disconnectedIds.Contains(clientId))
                {
                    disconnectedIds.Add(clientId);
                    if (clientId == NetworkConfig.NetworkTransport.HostDummyId ||
                        clientId == NetworkConfig.NetworkTransport.InvalidDummyId)
                        continue;
                    NetworkConfig.NetworkTransport.DisconnectClient(clientId);
                }
            }
            isServer = false;
            Shutdown();
        }

        /// <summary>
        /// Stops the running host
        /// </summary>
        public void StopHost()
        {
            if (LogHelper.CurrentLogLevel <= LogLevel.Developer) LogHelper.LogInfo("StopHost()");
            isServer = false;
            isClient = false;
            StopServer();
            //We don't stop client since we dont actually have a transport connection to our own host. We just handle host messages directly in the MLAPI
        }

        /// <summary>
        /// Stops the running client
        /// </summary>
        public void StopClient()
        {
            if (LogHelper.CurrentLogLevel <= LogLevel.Developer) LogHelper.LogInfo("StopClient()");
            isClient = false;
            NetworkConfig.NetworkTransport.DisconnectFromServer();
            isConnectedClients = false;
            Shutdown();
        }

        /// <summary>
        /// Starts a Host
        /// </summary>
        public void StartHost(Vector3? pos = null, Quaternion? rot = null, int prefabId = -1)
        {
            if (LogHelper.CurrentLogLevel <= LogLevel.Developer) LogHelper.LogInfo("StartHost()");
            if (isServer || isClient)
            {
                if (LogHelper.CurrentLogLevel <= LogLevel.Normal) LogHelper.LogWarning("Cannot start host while an instance is already running");
                return;
            }

            if (NetworkConfig.ConnectionApproval)
            {
                if (ConnectionApprovalCallback == null)
                {
                    if (LogHelper.CurrentLogLevel <= LogLevel.Normal) LogHelper.LogWarning("No ConnectionApproval callback defined. Connection approval will timeout");
                }
            }
            object settings = Init(true);
            NetworkConfig.NetworkTransport.RegisterServerListenSocket(settings);

            isServer = true;
            isClient = true;
            isListening = true;

            uint hostClientId = NetworkConfig.NetworkTransport.HostDummyId;
            ConnectedClients.Add(hostClientId, new NetworkedClient()
            {
                ClientId = hostClientId
            });
            ConnectedClientsList.Add(ConnectedClients[hostClientId]);

            if (NetworkConfig.HandleObjectSpawning)
            {
                prefabId = prefabId == -1 ? NetworkConfig.NetworkPrefabIds[NetworkConfig.PlayerPrefabName] : prefabId;
                SpawnManager.CreateSpawnedObject(prefabId, 0, hostClientId, true, pos.GetValueOrDefault(), rot.GetValueOrDefault(), null, false, false);
            }

            SpawnSceneObjects();

            if (OnServerStarted != null)
                OnServerStarted.Invoke();
        }

        private void OnEnable()
        {
            if (singleton != null && singleton != this)
            {
                Destroy(this.gameObject);
            }
            else
            {
                singleton = this;
                if (DontDestroy)
                    DontDestroyOnLoad(gameObject);
                if (RunInBackground)
                    Application.runInBackground = true;
            }
        }
        
        private void OnDestroy()
        {
            if (singleton != null && singleton == this)
            {
                singleton = null;
                Shutdown();  
            }
        }

        private void Shutdown()
        {
            if (LogHelper.CurrentLogLevel <= LogLevel.Developer) LogHelper.LogInfo("Shutdown()");
            NetworkProfiler.Stop();
            isListening = false;
            isServer = false;
            isClient = false;
            SpawnManager.DestroyNonSceneObjects();

            if (NetworkConfig != null && NetworkConfig.NetworkTransport != null) //The Transport is set during Init time, thus it is possible for the Transport to be null
                NetworkConfig.NetworkTransport.Shutdown();
        }

        private float lastReceiveTickTime;
        private float lastSendTickTime;
        private float lastEventTickTime;
        private float eventOvershootCounter;
        private float lastTimeSyncTime;
        private void Update()
        {
            if(isListening)
            {
                if((NetworkTime - lastSendTickTime >= (1f / NetworkConfig.SendTickrate)) || NetworkConfig.SendTickrate <= 0)
                {
                    NetworkedObject.NetworkedVarPrepareSend();
                    foreach (KeyValuePair<uint, NetworkedClient> pair in ConnectedClients)
                    {
                        byte error;
                        NetworkConfig.NetworkTransport.SendQueue(pair.Key, out error);
                        if (LogHelper.CurrentLogLevel <= LogLevel.Developer) LogHelper.LogInfo("Send Pending Queue: " + pair.Key);
                    }
                    lastSendTickTime = NetworkTime;
                }
                if((NetworkTime - lastReceiveTickTime >= (1f / NetworkConfig.ReceiveTickrate)) || NetworkConfig.ReceiveTickrate <= 0)
                {
                    NetworkProfiler.StartTick(TickType.Receive);
                    NetEventType eventType;
                    int processedEvents = 0;
                    do
                    {
                        processedEvents++;
                        uint clientId;
                        int channelId;
                        int receivedSize;
                        byte error;
                        eventType = NetworkConfig.NetworkTransport.PollReceive(out clientId, out channelId, ref messageBuffer, messageBuffer.Length, out receivedSize, out error);

                        switch (eventType)
                        {
                            case NetEventType.Connect:
                                NetworkProfiler.StartEvent(TickType.Receive, (uint)receivedSize, MessageManager.reverseChannels[channelId], "TRANSPORT_CONNECT");
                                if (isServer)
                                {
                                    if (LogHelper.CurrentLogLevel <= LogLevel.Developer) LogHelper.LogInfo("Client Connected");
                                    pendingClients.Add(clientId);
                                    StartCoroutine(ApprovalTimeout(clientId));
                                }
                                else
                                {
                                    if (LogHelper.CurrentLogLevel <= LogLevel.Developer) LogHelper.LogInfo("Connected");
#if !DISABLE_CRYPTOGRAPHY
                                    byte[] diffiePublic = new byte[0];
                                    if(NetworkConfig.EnableEncryption)
                                    {
                                        clientDiffieHellman = new EllipticDiffieHellman(EllipticDiffieHellman.DEFAULT_CURVE, EllipticDiffieHellman.DEFAULT_GENERATOR, EllipticDiffieHellman.DEFAULT_ORDER);
                                        diffiePublic = clientDiffieHellman.GetPublicKey();
                                    }
#endif

                                    using (BitWriter writer = BitWriter.Get())
                                    {
                                        writer.WriteULong(NetworkConfig.GetConfig());
#if !DISABLE_CRYPTOGRAPHY
                                        if (NetworkConfig.EnableEncryption)      
                                            writer.WriteByteArray(diffiePublic);
#endif

                                        if (NetworkConfig.ConnectionApproval)
                                            writer.WriteByteArray(NetworkConfig.ConnectionData);

                                        InternalMessageHandler.Send(clientId, "MLAPI_CONNECTION_REQUEST", "MLAPI_INTERNAL", writer, true);
                                    }
                                }
                                NetworkProfiler.EndEvent();
                                break;
                            case NetEventType.Data:
                                if (LogHelper.CurrentLogLevel <= LogLevel.Developer) LogHelper.LogInfo("Incomming Data From " + clientId + " : " + receivedSize + " bytes");

                                HandleIncomingData(clientId, messageBuffer, channelId, (uint)receivedSize);
                                break;
                            case NetEventType.Disconnect:
                                NetworkProfiler.StartEvent(TickType.Receive, 0, "NONE", "TRANSPORT_DISCONNECT");
                                if (LogHelper.CurrentLogLevel <= LogLevel.Developer) LogHelper.LogInfo("Disconnect Event From " + clientId);

                                if (isServer)
                                    OnClientDisconnectFromServer(clientId);
                                else
                                {
                                    isConnectedClients = false;
                                    StopClient();
                                }

                                if (OnClientDisconnectCallback != null)
                                    OnClientDisconnectCallback.Invoke(clientId);
                                NetworkProfiler.EndEvent();
                                break;
                        }
                        // Only do another iteration if: there are no more messages AND (there is no limit to max events or we have processed less than the maximum)
                    } while (isListening && (eventType != NetEventType.Nothing && (NetworkConfig.MaxReceiveEventsPerTickRate <= 0 || processedEvents < NetworkConfig.MaxReceiveEventsPerTickRate)));
                    lastReceiveTickTime = NetworkTime;
                    NetworkProfiler.EndTick();
                }

                if (isServer && ((NetworkTime - lastEventTickTime >= (1f / NetworkConfig.EventTickrate))))
                {
                    NetworkProfiler.StartTick(TickType.Event);
                    eventOvershootCounter += ((NetworkTime - lastEventTickTime) - (1f / NetworkConfig.EventTickrate));
                    LagCompensationManager.AddFrames();
                    lastEventTickTime = NetworkTime;
                    NetworkProfiler.EndTick();
                }
                else if (isServer && eventOvershootCounter >= ((1f / NetworkConfig.EventTickrate)))
                {
                    NetworkProfiler.StartTick(TickType.Event);
                    //We run this one to compensate for previous update overshoots.
                    eventOvershootCounter -= (1f / NetworkConfig.EventTickrate);
                    LagCompensationManager.AddFrames();
                    NetworkProfiler.EndTick();
                }

                if (isServer && NetworkConfig.EnableTimeResync && NetworkTime - lastTimeSyncTime >= 30)
                {
                    NetworkProfiler.StartTick(TickType.Event);
                    SyncTime();
                    lastTimeSyncTime = NetworkTime;
                    NetworkProfiler.EndTick();
                }

                NetworkTime += Time.unscaledDeltaTime;
            }
        }

        private IEnumerator ApprovalTimeout(uint clientId)
        {
            float timeStarted = NetworkTime;
            //We yield every frame incase a pending client disconnects and someone else gets its connection id
            while (NetworkTime - timeStarted < NetworkConfig.ClientConnectionBufferTimeout && pendingClients.Contains(clientId))
            {
                yield return null;
            }
            if(pendingClients.Contains(clientId) && !ConnectedClients.ContainsKey(clientId))
            {
                //Timeout
                if (LogHelper.CurrentLogLevel <= LogLevel.Developer) LogHelper.LogInfo("Client " + clientId + " Handshake Timed Out");
                DisconnectClient(clientId);
            }
        }

        private void HandleIncomingData(uint clientId, byte[] data, int channelId, uint totalSize)
        {
            if (LogHelper.CurrentLogLevel <= LogLevel.Developer) LogHelper.LogInfo("Unwrapping Data Header");
            using (BitReader reader = BitReader.Get(data))
            {
                ushort messageType = reader.ReadUShort();

                long headerBitSize = BitWriter.GetBitCount(messageType);
                uint headerByteSize = (uint) Math.Ceiling(headerBitSize / 8d);
                NetworkProfiler.StartEvent(TickType.Receive, totalSize - headerByteSize, channelId, messageType);

                if (LogHelper.CurrentLogLevel <= LogLevel.Developer)
                    LogHelper.LogInfo("Data Header: messageType=" + messageType);

                //Client tried to send a network message that was not the connection request before he was accepted.
                if (isServer && pendingClients.Contains(clientId) && messageType != 0)
                {
                    if (LogHelper.CurrentLogLevel <= LogLevel.Normal) LogHelper.LogWarning("Message recieved from clientId " + clientId + " before it has been accepted");
                    return;
                }

                reader.SkipPadded();

                if (messageType >= 32)
                {
                    
                }
                else
                {
                    #region INTERNAL MESSAGE

                    switch (messageType)
                    {
                        case MLAPIConstants.MLAPI_CONNECTION_REQUEST:
                            if (isServer) InternalMessageHandler.HandleConnectionRequest(clientId, reader, channelId);
                            break;
                        case MLAPIConstants.MLAPI_CONNECTION_APPROVED:
                            if (isClient) InternalMessageHandler.HandleConnectionApproved(clientId, reader, channelId);
                            break;
                        case MLAPIConstants.MLAPI_ADD_OBJECT:
                            if (isClient) InternalMessageHandler.HandleAddObject(clientId, reader, channelId);
                            break;
                        case MLAPIConstants.MLAPI_CLIENT_DISCONNECT:
                            if (isClient) InternalMessageHandler.HandleClientDisconnect(clientId, reader, channelId);
                            break;
                        case MLAPIConstants.MLAPI_DESTROY_OBJECT:
                            if (isClient) InternalMessageHandler.HandleDestroyObject(clientId, reader, channelId);
                            break;
                        case MLAPIConstants.MLAPI_SWITCH_SCENE:
                            if (isClient) InternalMessageHandler.HandleSwitchScene(clientId, reader, channelId);
                            break;
                        case MLAPIConstants.MLAPI_SPAWN_POOL_OBJECT:
                            if (isClient) InternalMessageHandler.HandleSpawnPoolObject(clientId, reader, channelId);
                            break;
                        case MLAPIConstants.MLAPI_DESTROY_POOL_OBJECT:
                            if (isClient) InternalMessageHandler.HandleDestroyPoolObject(clientId, reader, channelId);
                            break;
                        case MLAPIConstants.MLAPI_CHANGE_OWNER:
                            if (isClient) InternalMessageHandler.HandleChangeOwner(clientId, reader, channelId);
                            break;
                        case MLAPIConstants.MLAPI_ADD_OBJECTS:
                            if (isClient) InternalMessageHandler.HandleAddObjects(clientId, reader, channelId);
                            break;
                        case MLAPIConstants.MLAPI_TIME_SYNC:
                            if (isClient) InternalMessageHandler.HandleTimeSync(clientId, reader, channelId);
                            break;
                        case MLAPIConstants.MLAPI_NETWORKED_VAR_DELTA:
                            InternalMessageHandler.HandleNetworkedVarDelta(clientId, reader, channelId);
                            break;
                        case MLAPIConstants.MLAPI_NETWORKED_VAR_UPDATE:
                            InternalMessageHandler.HandleNetworkedVarUpdate(clientId, reader, channelId);
                            break;
                        case MLAPIConstants.MLAPI_SERVER_RPC:
                            if (isServer) InternalMessageHandler.HandleServerRPC(clientId, reader, channelId);
                            break;
                        case MLAPIConstants.MLAPI_CLIENT_RPC:
                            if (isClient) InternalMessageHandler.HandleClientRPC(clientId, reader, channelId);
                            break;
                        case MLAPIConstants.MLAPI_CUSTOM_MESSAGE:
                            InternalMessageHandler.HandleCustomMessage(clientId, reader, channelId);
                            break;
                    }

                    #endregion
                }

                NetworkProfiler.EndEvent();
            }
        }

        internal void DisconnectClient(uint clientId)
        {
            if (!isServer)
                return;

            if (pendingClients.Contains(clientId))
                pendingClients.Remove(clientId);

            if (ConnectedClients.ContainsKey(clientId))
                ConnectedClients.Remove(clientId);
      
            ConnectedClientsList.RemoveAll(x => x.ClientId == clientId); // :(

#if !DISABLE_CRYPTOGRAPHY
            if (diffieHellmanPublicKeys.ContainsKey(clientId))
                diffieHellmanPublicKeys.Remove(clientId);
#endif

            NetworkConfig.NetworkTransport.DisconnectClient(clientId);
        }

        internal void OnClientDisconnectFromServer(uint clientId)
        {
            if (pendingClients.Contains(clientId))
                pendingClients.Remove(clientId);
            if (ConnectedClients.ContainsKey(clientId))
            {
                if(NetworkConfig.HandleObjectSpawning)
                {
                    if (ConnectedClients[clientId].PlayerObject != null)
                        Destroy(ConnectedClients[clientId].PlayerObject.gameObject);
                    for (int i = 0; i < ConnectedClients[clientId].OwnedObjects.Count; i++)
                    {
                        if (ConnectedClients[clientId].OwnedObjects[i] != null)
                            Destroy(ConnectedClients[clientId].OwnedObjects[i].gameObject);
                    }
                }
                ConnectedClientsList.RemoveAll(x => x.ClientId == clientId);
                ConnectedClients.Remove(clientId);
            }

            if (isServer)
            {
                using (BitWriter writer = BitWriter.Get())
                {
                    writer.WriteUInt(clientId);
                    InternalMessageHandler.Send("MLAPI_CLIENT_DISCONNECT", "MLAPI_INTERNAL", clientId, writer);
                }
            }
        }

        private void SyncTime()
        {
            if (LogHelper.CurrentLogLevel <= LogLevel.Developer) LogHelper.LogInfo("Syncing Time To Clients");
            using (BitWriter writer = BitWriter.Get())
            {
                writer.WriteFloat(NetworkTime);
                int timestamp = NetworkConfig.NetworkTransport.GetNetworkTimestamp();
                writer.WriteInt(timestamp);
                InternalMessageHandler.Send("MLAPI_TIME_SYNC", "MLAPI_TIME_SYNC", writer);
            }
        }

        internal void HandleApproval(uint clientId, int prefabId, bool approved, Vector3 position, Quaternion rotation)
        {
            if(approved)
            {
                //Inform new client it got approved
                if (pendingClients.Contains(clientId))
                    pendingClients.Remove(clientId);

#if !DISABLE_CRYPTOGRAPHY
                byte[] aesKey = new byte[0];
                byte[] publicKey = new byte[0];
                byte[] publicKeySignature = new byte[0];
                if (NetworkConfig.EnableEncryption)
                {
                    EllipticDiffieHellman diffieHellman = new EllipticDiffieHellman(EllipticDiffieHellman.DEFAULT_CURVE, EllipticDiffieHellman.DEFAULT_GENERATOR, EllipticDiffieHellman.DEFAULT_ORDER);
                    aesKey = diffieHellman.GetSharedSecret(diffieHellmanPublicKeys[clientId]);
                    publicKey = diffieHellman.GetPublicKey();

                    if (diffieHellmanPublicKeys.ContainsKey(clientId))
                        diffieHellmanPublicKeys.Remove(clientId);

                    if (NetworkConfig.SignKeyExchange)
                    {
                        using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
                        {
                            rsa.PersistKeyInCsp = false;
                            rsa.FromXmlString(NetworkConfig.RSAPrivateKey);
                            publicKeySignature = rsa.SignData(publicKey, new SHA512CryptoServiceProvider());
                        }
                    }
                }
#endif

                NetworkedClient client = new NetworkedClient()
                {
                    ClientId = clientId,
#if !DISABLE_CRYPTOGRAPHY
                    AesKey = aesKey
#endif
                };
                ConnectedClients.Add(clientId, client);
                ConnectedClientsList.Add(client);

                NetworkedObject netObject = null;
                if(NetworkConfig.HandleObjectSpawning)
                {
                    prefabId = prefabId == -1 ? NetworkConfig.NetworkPrefabIds[NetworkConfig.PlayerPrefabName] : prefabId;
                    netObject = SpawnManager.CreateSpawnedObject(prefabId, 0, clientId, true, position, rotation, null, false, false);
                    ConnectedClients[clientId].PlayerObject = netObject;
                }

                int amountOfObjectsToSend = SpawnManager.SpawnedObjects.Values.Count;

                using (BitWriter writer = BitWriter.Get())
                {
                    writer.WriteUInt(clientId);
                    if (NetworkConfig.EnableSceneSwitching)
                        writer.WriteUInt(NetworkSceneManager.CurrentSceneIndex);

#if !DISABLE_CRYPTOGRAPHY
                    if (NetworkConfig.EnableEncryption)
                    {
                        writer.WriteByteArray(publicKey);
                        if (NetworkConfig.SignKeyExchange)
                            writer.WriteByteArray(publicKeySignature);
                    }
#endif

                    writer.WriteFloat(NetworkTime);
                    writer.WriteInt(NetworkConfig.NetworkTransport.GetNetworkTimestamp());

                    writer.WriteInt(ConnectedClients.Count - 1);

                    foreach (KeyValuePair<uint, NetworkedClient> item in ConnectedClients)
                    {
                        //Our own ID. Already added as the first one above
                        if (item.Key == clientId)
                            continue;
                        writer.WriteUInt(item.Key); //ClientId
                    }
                    if (NetworkConfig.HandleObjectSpawning)
                    {
                        writer.WriteInt(amountOfObjectsToSend);

                        foreach (KeyValuePair<uint, NetworkedObject> pair in SpawnManager.SpawnedObjects)
                        {
                            writer.WriteBool(pair.Value.isPlayerObject);
                            writer.WriteUInt(pair.Value.NetworkId);
                            writer.WriteUInt(pair.Value.OwnerClientId);
                            writer.WriteInt(NetworkConfig.NetworkPrefabIds[pair.Value.NetworkedPrefabName]);
                            writer.WriteBool(pair.Value.gameObject.activeInHierarchy);
                            writer.WriteBool(pair.Value.sceneObject == null ? true : pair.Value.sceneObject.Value);

                            writer.WriteFloat(pair.Value.transform.position.x);
                            writer.WriteFloat(pair.Value.transform.position.y);
                            writer.WriteFloat(pair.Value.transform.position.z);

                            writer.WriteFloat(pair.Value.transform.rotation.eulerAngles.x);
                            writer.WriteFloat(pair.Value.transform.rotation.eulerAngles.y);
                            writer.WriteFloat(pair.Value.transform.rotation.eulerAngles.z);

                            pair.Value.WriteNetworkedVarData(writer, clientId);
                        }
                    }
                    InternalMessageHandler.Send(clientId, "MLAPI_CONNECTION_APPROVED", "MLAPI_INTERNAL", writer, true);

                    if (OnClientConnectedCallback != null)
                        OnClientConnectedCallback.Invoke(clientId);
                }

                //Inform old clients of the new player

                foreach (var clientPair in ConnectedClients)
                {
                    if (clientPair.Key == clientId)
                        continue; //The new client.

                    using (BitWriter writer = BitWriter.Get())
                    {
                        if (NetworkConfig.HandleObjectSpawning)
                        {
                            writer.WriteBool(true);
                            writer.WriteUInt(ConnectedClients[clientId].PlayerObject.GetComponent<NetworkedObject>().NetworkId);
                            writer.WriteUInt(clientId);
                            writer.WriteInt(prefabId);
                            writer.WriteBool(false);

                            writer.WriteFloat(ConnectedClients[clientId].PlayerObject.transform.position.x);
                            writer.WriteFloat(ConnectedClients[clientId].PlayerObject.transform.position.y);
                            writer.WriteFloat(ConnectedClients[clientId].PlayerObject.transform.position.z);

                            writer.WriteFloat(ConnectedClients[clientId].PlayerObject.transform.rotation.eulerAngles.x);
                            writer.WriteFloat(ConnectedClients[clientId].PlayerObject.transform.rotation.eulerAngles.y);
                            writer.WriteFloat(ConnectedClients[clientId].PlayerObject.transform.rotation.eulerAngles.z);

                            writer.WriteBool(false); //No payload data

                            ConnectedClients[clientId].PlayerObject.GetComponent<NetworkedObject>().WriteNetworkedVarData(writer, clientPair.Key);
                        }
                        else
                        {
                            writer.WriteUInt(clientId);
                        }
                        InternalMessageHandler.Send(clientPair.Key, "MLAPI_ADD_OBJECT", "MLAPI_INTERNAL", writer);
                    }
                }
            }
            else
            {
                if (pendingClients.Contains(clientId))
                    pendingClients.Remove(clientId);

#if !DISABLE_CRYPTOGRAPHY
                if (diffieHellmanPublicKeys.ContainsKey(clientId))
                    diffieHellmanPublicKeys.Remove(clientId);
#endif

                NetworkConfig.NetworkTransport.DisconnectClient(clientId);
            }
        }
    }
}
