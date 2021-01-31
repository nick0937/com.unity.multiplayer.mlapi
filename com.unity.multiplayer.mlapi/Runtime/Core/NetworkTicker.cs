using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using System.Linq;
using System.Collections.Generic;

namespace MLAPI
{
    public interface INetworkTickable
    {
        void NetworkTick();
    }

    public enum NetworkTickStage
    {
        Initialization = -4,
        EarlyUpdate = -3,
        FixedUpdate = -2,
        PreUpdate = -1,
        Update = 0,
        PreLateUpdate = 1,
        PostLateUpdate = 2
    }

    public static class NetworkTicker
    {
        [RuntimeInitializeOnLoadMethod]
        private static void Initialize()
        {
            var customPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();

            for (int i = 0; i < customPlayerLoop.subSystemList.Length; i++)
            {
                var playerLoopSystem = customPlayerLoop.subSystemList[i];

                if (playerLoopSystem.type == typeof(Initialization))
                {
                    var subsystems = playerLoopSystem.subSystemList.ToList();
                    subsystems.Add(NetworkInitialization.CreateLoopSystem());
                    playerLoopSystem.subSystemList = subsystems.ToArray();
                }
                else if (playerLoopSystem.type == typeof(EarlyUpdate))
                {
                    var subsystems = playerLoopSystem.subSystemList.ToList();
                    subsystems.Insert(0, NetworkEarlyUpdate.CreateLoopSystem());
                    playerLoopSystem.subSystemList = subsystems.ToArray();
                }
                else if (playerLoopSystem.type == typeof(FixedUpdate))
                {
                    var subsystems = playerLoopSystem.subSystemList.ToList();
                    subsystems.Insert(0, NetworkFixedUpdate.CreateLoopSystem());
                    playerLoopSystem.subSystemList = subsystems.ToArray();
                }
                else if (playerLoopSystem.type == typeof(PreUpdate))
                {
                    var subsystems = playerLoopSystem.subSystemList.ToList();
                    subsystems.Insert(0, NetworkPreUpdate.CreateLoopSystem());
                    playerLoopSystem.subSystemList = subsystems.ToArray();
                }
                else if (playerLoopSystem.type == typeof(Update))
                {
                    var subsystems = playerLoopSystem.subSystemList.ToList();
                    subsystems.Insert(0, NetworkUpdate.CreateLoopSystem());
                    playerLoopSystem.subSystemList = subsystems.ToArray();
                }
                else if (playerLoopSystem.type == typeof(PreLateUpdate))
                {
                    var subsystems = playerLoopSystem.subSystemList.ToList();
                    subsystems.Insert(0, NetworkPreLateUpdate.CreateLoopSystem());
                    playerLoopSystem.subSystemList = subsystems.ToArray();
                }
                else if (playerLoopSystem.type == typeof(PostLateUpdate))
                {
                    var subsystems = playerLoopSystem.subSystemList.ToList();
                    subsystems.Add(NetworkPostLateUpdate.CreateLoopSystem());
                    playerLoopSystem.subSystemList = subsystems.ToArray();
                }

                customPlayerLoop.subSystemList[i] = playerLoopSystem;
            }

            PlayerLoop.SetPlayerLoop(customPlayerLoop);
        }

        private struct NetworkInitialization
        {
            public static PlayerLoopSystem CreateLoopSystem()
            {
                return new PlayerLoopSystem
                {
                    type = typeof(NetworkInitialization),
                    updateDelegate = TickNetworkInitialization
                };
            }
        }

        private struct NetworkEarlyUpdate
        {
            public static PlayerLoopSystem CreateLoopSystem()
            {
                return new PlayerLoopSystem
                {
                    type = typeof(NetworkEarlyUpdate),
                    updateDelegate = TickNetworkEarlyUpdate
                };
            }
        }

        private struct NetworkFixedUpdate
        {
            public static PlayerLoopSystem CreateLoopSystem()
            {
                return new PlayerLoopSystem
                {
                    type = typeof(NetworkFixedUpdate),
                    updateDelegate = TickNetworkFixedUpdate
                };
            }
        }

        private struct NetworkPreUpdate
        {
            public static PlayerLoopSystem CreateLoopSystem()
            {
                return new PlayerLoopSystem
                {
                    type = typeof(NetworkPreUpdate),
                    updateDelegate = TickNetworkPreUpdate
                };
            }
        }

        private struct NetworkUpdate
        {
            public static PlayerLoopSystem CreateLoopSystem()
            {
                return new PlayerLoopSystem
                {
                    type = typeof(NetworkUpdate),
                    updateDelegate = TickNetworkUpdate
                };
            }
        }

        private struct NetworkPreLateUpdate
        {
            public static PlayerLoopSystem CreateLoopSystem()
            {
                return new PlayerLoopSystem
                {
                    type = typeof(NetworkPreLateUpdate),
                    updateDelegate = TickNetworkPreLateUpdate
                };
            }
        }

        private struct NetworkPostLateUpdate
        {
            public static PlayerLoopSystem CreateLoopSystem()
            {
                return new PlayerLoopSystem
                {
                    type = typeof(NetworkPostLateUpdate),
                    updateDelegate = TickNetworkPostLateUpdate
                };
            }
        }

        public static void RegisterAllNetworkTicks(this INetworkTickable tickable)
        {
            RegisterNetworkTick(tickable, NetworkTickStage.Initialization);
            RegisterNetworkTick(tickable, NetworkTickStage.EarlyUpdate);
            RegisterNetworkTick(tickable, NetworkTickStage.FixedUpdate);
            RegisterNetworkTick(tickable, NetworkTickStage.PreUpdate);
            RegisterNetworkTick(tickable, NetworkTickStage.Update);
            RegisterNetworkTick(tickable, NetworkTickStage.PreLateUpdate);
            RegisterNetworkTick(tickable, NetworkTickStage.PostLateUpdate);
        }

        public static void RegisterNetworkTick(this INetworkTickable tickable, NetworkTickStage tickStage = NetworkTickStage.Update)
        {
            switch (tickStage)
            {
                case NetworkTickStage.Initialization:
                {
                    if (!m_Initialization_TickableList.Contains(tickable))
                    {
                        m_Initialization_TickableList.Add(tickable);
                        m_Initialization_TickableArray = m_Initialization_TickableList.ToArray();
                    }

                    break;
                }
                case NetworkTickStage.EarlyUpdate:
                {
                    if (!m_EarlyUpdate_TickableList.Contains(tickable))
                    {
                        m_EarlyUpdate_TickableList.Add(tickable);
                        m_EarlyUpdate_TickableArray = m_EarlyUpdate_TickableList.ToArray();
                    }

                    break;
                }
                case NetworkTickStage.FixedUpdate:
                {
                    if (!m_FixedUpdate_TickableList.Contains(tickable))
                    {
                        m_FixedUpdate_TickableList.Add(tickable);
                        m_FixedUpdate_TickableArray = m_FixedUpdate_TickableList.ToArray();
                    }

                    break;
                }
                case NetworkTickStage.PreUpdate:
                {
                    if (!m_PreUpdate_TickableList.Contains(tickable))
                    {
                        m_PreUpdate_TickableList.Add(tickable);
                        m_PreUpdate_TickableArray = m_PreUpdate_TickableList.ToArray();
                    }

                    break;
                }
                case NetworkTickStage.Update:
                {
                    if (!m_Update_TickableList.Contains(tickable))
                    {
                        m_Update_TickableList.Add(tickable);
                        m_Update_TickableArray = m_Update_TickableList.ToArray();
                    }

                    break;
                }
                case NetworkTickStage.PreLateUpdate:
                {
                    if (!m_PreLateUpdate_TickableList.Contains(tickable))
                    {
                        m_PreLateUpdate_TickableList.Add(tickable);
                        m_PreLateUpdate_TickableArray = m_PreLateUpdate_TickableList.ToArray();
                    }

                    break;
                }
                case NetworkTickStage.PostLateUpdate:
                {
                    if (!m_PostLateUpdate_TickableList.Contains(tickable))
                    {
                        m_PostLateUpdate_TickableList.Add(tickable);
                        m_PostLateUpdate_TickableArray = m_PostLateUpdate_TickableList.ToArray();
                    }

                    break;
                }
            }
        }

        public static void UnregisterAllNetworkTicks(this INetworkTickable tickable)
        {
            UnregisterNetworkTick(tickable, NetworkTickStage.Initialization);
            UnregisterNetworkTick(tickable, NetworkTickStage.EarlyUpdate);
            UnregisterNetworkTick(tickable, NetworkTickStage.FixedUpdate);
            UnregisterNetworkTick(tickable, NetworkTickStage.PreUpdate);
            UnregisterNetworkTick(tickable, NetworkTickStage.Update);
            UnregisterNetworkTick(tickable, NetworkTickStage.PreLateUpdate);
            UnregisterNetworkTick(tickable, NetworkTickStage.PostLateUpdate);
        }

        public static void UnregisterNetworkTick(this INetworkTickable tickable, NetworkTickStage tickStage = NetworkTickStage.Update)
        {
            switch (tickStage)
            {
                case NetworkTickStage.Initialization:
                {
                    if (m_Initialization_TickableList.Contains(tickable))
                    {
                        m_Initialization_TickableList.Remove(tickable);
                        m_Initialization_TickableArray = m_Initialization_TickableList.ToArray();
                    }

                    break;
                }
                case NetworkTickStage.EarlyUpdate:
                {
                    if (m_EarlyUpdate_TickableList.Contains(tickable))
                    {
                        m_EarlyUpdate_TickableList.Remove(tickable);
                        m_EarlyUpdate_TickableArray = m_EarlyUpdate_TickableList.ToArray();
                    }

                    break;
                }
                case NetworkTickStage.FixedUpdate:
                {
                    if (m_FixedUpdate_TickableList.Contains(tickable))
                    {
                        m_FixedUpdate_TickableList.Remove(tickable);
                        m_FixedUpdate_TickableArray = m_FixedUpdate_TickableList.ToArray();
                    }

                    break;
                }
                case NetworkTickStage.PreUpdate:
                {
                    if (m_PreUpdate_TickableList.Contains(tickable))
                    {
                        m_PreUpdate_TickableList.Remove(tickable);
                        m_PreUpdate_TickableArray = m_PreUpdate_TickableList.ToArray();
                    }

                    break;
                }
                case NetworkTickStage.Update:
                {
                    if (m_Update_TickableList.Contains(tickable))
                    {
                        m_Update_TickableList.Remove(tickable);
                        m_Update_TickableArray = m_Update_TickableList.ToArray();
                    }

                    break;
                }
                case NetworkTickStage.PreLateUpdate:
                {
                    if (m_PreLateUpdate_TickableList.Contains(tickable))
                    {
                        m_PreLateUpdate_TickableList.Remove(tickable);
                        m_PreLateUpdate_TickableArray = m_PreLateUpdate_TickableList.ToArray();
                    }

                    break;
                }
                case NetworkTickStage.PostLateUpdate:
                {
                    if (m_PostLateUpdate_TickableList.Contains(tickable))
                    {
                        m_PostLateUpdate_TickableList.Remove(tickable);
                        m_PostLateUpdate_TickableArray = m_PostLateUpdate_TickableList.ToArray();
                    }

                    break;
                }
            }
        }

        public static uint TickCount = 0;
        public static NetworkTickStage TickStage;

        private static void AdvanceTick()
        {
            ++TickCount;
        }

        private static readonly List<INetworkTickable> m_Initialization_TickableList = new List<INetworkTickable>();
        private static INetworkTickable[] m_Initialization_TickableArray = new INetworkTickable[0];

        private static void TickNetworkInitialization()
        {
            AdvanceTick();

            TickStage = NetworkTickStage.Initialization;
            int tickableArrayLength = m_Initialization_TickableArray.Length;
            for (int i = 0; i < tickableArrayLength; i++)
            {
                m_Initialization_TickableArray[i].NetworkTick();
            }
        }

        private static readonly List<INetworkTickable> m_EarlyUpdate_TickableList = new List<INetworkTickable>();
        private static INetworkTickable[] m_EarlyUpdate_TickableArray = new INetworkTickable[0];

        private static void TickNetworkEarlyUpdate()
        {
            TickStage = NetworkTickStage.EarlyUpdate;
            int tickableArrayLength = m_EarlyUpdate_TickableArray.Length;
            for (int i = 0; i < tickableArrayLength; i++)
            {
                m_EarlyUpdate_TickableArray[i].NetworkTick();
            }
        }

        private static readonly List<INetworkTickable> m_FixedUpdate_TickableList = new List<INetworkTickable>();
        private static INetworkTickable[] m_FixedUpdate_TickableArray = new INetworkTickable[0];

        private static void TickNetworkFixedUpdate()
        {
            TickStage = NetworkTickStage.FixedUpdate;
            int tickableArrayLength = m_FixedUpdate_TickableArray.Length;
            for (int i = 0; i < tickableArrayLength; i++)
            {
                m_FixedUpdate_TickableArray[i].NetworkTick();
            }
        }

        private static readonly List<INetworkTickable> m_PreUpdate_TickableList = new List<INetworkTickable>();
        private static INetworkTickable[] m_PreUpdate_TickableArray = new INetworkTickable[0];

        private static void TickNetworkPreUpdate()
        {
            TickStage = NetworkTickStage.PreUpdate;
            int tickableArrayLength = m_PreUpdate_TickableArray.Length;
            for (int i = 0; i < tickableArrayLength; i++)
            {
                m_PreUpdate_TickableArray[i].NetworkTick();
            }
        }

        private static readonly List<INetworkTickable> m_Update_TickableList = new List<INetworkTickable>();
        private static INetworkTickable[] m_Update_TickableArray = new INetworkTickable[0];

        private static void TickNetworkUpdate()
        {
            TickStage = NetworkTickStage.Update;
            int tickableArrayLength = m_Update_TickableArray.Length;
            for (int i = 0; i < tickableArrayLength; i++)
            {
                m_Update_TickableArray[i].NetworkTick();
            }
        }

        private static readonly List<INetworkTickable> m_PreLateUpdate_TickableList = new List<INetworkTickable>();
        private static INetworkTickable[] m_PreLateUpdate_TickableArray = new INetworkTickable[0];

        private static void TickNetworkPreLateUpdate()
        {
            TickStage = NetworkTickStage.PreLateUpdate;
            int tickableArrayLength = m_PreLateUpdate_TickableArray.Length;
            for (int i = 0; i < tickableArrayLength; i++)
            {
                m_PreLateUpdate_TickableArray[i].NetworkTick();
            }
        }

        private static readonly List<INetworkTickable> m_PostLateUpdate_TickableList = new List<INetworkTickable>();
        private static INetworkTickable[] m_PostLateUpdate_TickableArray = new INetworkTickable[0];

        private static void TickNetworkPostLateUpdate()
        {
            TickStage = NetworkTickStage.PostLateUpdate;
            int tickableArrayLength = m_PostLateUpdate_TickableArray.Length;
            for (int i = 0; i < tickableArrayLength; i++)
            {
                m_PostLateUpdate_TickableArray[i].NetworkTick();
            }
        }
    }
}