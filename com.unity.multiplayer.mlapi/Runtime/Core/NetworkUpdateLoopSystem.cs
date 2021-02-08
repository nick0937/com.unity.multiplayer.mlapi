using System;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using System.Linq;
using System.Collections.Generic;


namespace MLAPI
{
    /// <summary>
    ///  NetworkUpdateLoopBehaviour
    ///  Derive from this class if you need to register a NetworkedBehaviour based class
    /// </summary>
    public class NetworkUpdateLoopBehaviour:NetworkedBehaviour, INetworkUpdateLoopSystem
    {
        protected virtual Action InternalRegisterNetworkUpdateStage(NetworkUpdateManager.NetworkUpdateStage stage )
        {
            return null;
        }

        public Action RegisterUpdate(NetworkUpdateManager.NetworkUpdateStage stage )
        {
            return InternalRegisterNetworkUpdateStage(stage);
        }

        protected void RegisterUpdateLoopSystem()
        {
            NetworkUpdateManager.NetworkLoopRegistration(this);
        }

        protected void OnNetworkLoopSystemRemove()
        {
            if(onNetworkLoopSystemDestroyed != null)
            {
                onNetworkLoopSystemDestroyed.Invoke(this);
            }
        }

        private Action<INetworkUpdateLoopSystem> onNetworkLoopSystemDestroyed;

        public void RegisterUpdateLoopSystemDestroyCallback(Action<INetworkUpdateLoopSystem> networkLoopSystemDestroyedCallback)
        {
            onNetworkLoopSystemDestroyed = networkLoopSystemDestroyedCallback;
        }
    }

    /// <summary>
    ///  UpdateLoopBehaviour
    ///  Derive from this class if you only require MonoBehaviour functionality
    /// </summary>
    public class UpdateLoopBehaviour:MonoBehaviour, INetworkUpdateLoopSystem
    {
        protected virtual Action InternalRegisterNetworkUpdateStage(NetworkUpdateManager.NetworkUpdateStage stage )
        {
            return null;
        }

        public Action RegisterUpdate(NetworkUpdateManager.NetworkUpdateStage stage )
        {
            return InternalRegisterNetworkUpdateStage(stage);
        }

        protected void RegisterUpdateLoopSystem()
        {
            NetworkUpdateManager.NetworkLoopRegistration(this);
        }

        protected void OnNetworkLoopSystemRemove()
        {
            if(onNetworkLoopSystemDestroyed != null)
            {
                onNetworkLoopSystemDestroyed.Invoke(this);
            }
        }

        private Action<INetworkUpdateLoopSystem> onNetworkLoopSystemDestroyed;

        public void RegisterUpdateLoopSystemDestroyCallback(Action<INetworkUpdateLoopSystem> networkLoopSystemDestroyedCallback)
        {
            onNetworkLoopSystemDestroyed = networkLoopSystemDestroyedCallback;
        }
    }

    /// <summary>
    /// GenericUpdateLoopSystem
    /// Derive from this class for generic (non-MonoBehaviour) classes
    /// </summary>
    public class GenericUpdateLoopSystem:INetworkUpdateLoopSystem
    {
        protected virtual Action InternalRegisterNetworkUpdateStage(NetworkUpdateManager.NetworkUpdateStage stage )
        {
            return null;
        }

        public Action RegisterUpdate(NetworkUpdateManager.NetworkUpdateStage stage )
        {
            return InternalRegisterNetworkUpdateStage(stage);
        }

        protected void RegisterUpdateLoopSystem()
        {
            NetworkUpdateManager.NetworkLoopRegistration(this);
        }

        protected void OnNetworkLoopSystemRemove()
        {
            if(onNetworkLoopSystemDestroyed != null)
            {
                onNetworkLoopSystemDestroyed.Invoke(this);
            }
        }

        private Action<INetworkUpdateLoopSystem> onNetworkLoopSystemDestroyed;

        public void RegisterUpdateLoopSystemDestroyCallback(Action<INetworkUpdateLoopSystem> networkLoopSystemDestroyedCallback)
        {
            onNetworkLoopSystemDestroyed = networkLoopSystemDestroyedCallback;
        }
    }


    /// <summary>
    /// INetworkUpdateLoopSystem
    /// Use this interface if you need a custom class beyond the scope of GenericUpdateLoopSystem, UpdateLoopBehaviour, and NetworkUpdateLoopBehaviour
    /// </summary>
    public interface INetworkUpdateLoopSystem
    {
        Action RegisterUpdate(NetworkUpdateManager.NetworkUpdateStage stage );

        void RegisterUpdateLoopSystemDestroyCallback(Action<INetworkUpdateLoopSystem>  networkLoopSystemDestroyedCallbsack);
    }

    public interface INetworkUpdateSystem
    {
        void NetworkUpdate();
    }

    public enum NetworkUpdateStage : byte
    {
        Initialization = 1,
        EarlyUpdate = 2,
        FixedUpdate = 3,
        PreUpdate = 4,
        Update = 0,
        PreLateUpdate = 5,
        PostLateUpdate = 6
    }

    public static class NetworkUpdateLoop
    {

        #region INTERNAL REGISTRATION
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
                    updateDelegate = RunNetworkInitialization
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
                    updateDelegate = RunNetworkEarlyUpdate
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
                    updateDelegate = RunNetworkFixedUpdate
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
                    updateDelegate = RunNetworkPreUpdate
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
                    updateDelegate = RunNetworkUpdate
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
                    updateDelegate = RunNetworkPreLateUpdate
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
                    updateDelegate = RunNetworkPostLateUpdate
                };
            }
        }

        public static void RegisterAllNetworkUpdates(this INetworkUpdateSystem updateSystem)
        {
            RegisterNetworkUpdate(updateSystem, NetworkUpdateStage.Initialization);
            RegisterNetworkUpdate(updateSystem, NetworkUpdateStage.EarlyUpdate);
            RegisterNetworkUpdate(updateSystem, NetworkUpdateStage.FixedUpdate);
            RegisterNetworkUpdate(updateSystem, NetworkUpdateStage.PreUpdate);
            RegisterNetworkUpdate(updateSystem, NetworkUpdateStage.Update);
            RegisterNetworkUpdate(updateSystem, NetworkUpdateStage.PreLateUpdate);
            RegisterNetworkUpdate(updateSystem, NetworkUpdateStage.PostLateUpdate);
        }
        #endregion


        private static Dictionary<NetworkUpdateStage,List<INetworkUpdateSystem>> m_RegisteredNetworkUpdates = new Dictionary<NetworkUpdateStage, List<INetworkUpdateSystem>>();

        public static void RegisterNetworkUpdate(this INetworkUpdateSystem updateSystem, NetworkUpdateStage updateStage = NetworkUpdateStage.Update)
        {
            if(!m_RegisteredNetworkUpdates.ContainsKey(updateStage))
            {
                m_RegisteredNetworkUpdates.Add(updateStage, new List<INetworkUpdateSystem>());
            }

            if(!m_RegisteredNetworkUpdates[updateStage].Contains(updateSystem))
            {
                m_RegisteredNetworkUpdates[updateStage].Add(updateSystem);
            }
            #region OLDWAY
            //switch (updateStage)
            //{
            //    case NetworkUpdateStage.Initialization:
            //    {
            //        if (!m_Initialization_List.Contains(updateSystem))
            //        {
            //            m_Initialization_List.Add(updateSystem);
            //            m_Initialization_Array = m_Initialization_List.ToArray();
            //        }
            //        break;
            //    }
            //    case NetworkUpdateStage.EarlyUpdate:
            //    {
            //        if (!m_EarlyUpdate_List.Contains(updateSystem))
            //        {
            //            m_EarlyUpdate_List.Add(updateSystem);
            //            m_EarlyUpdate_Array = m_EarlyUpdate_List.ToArray();
            //        }

            //        break;
            //    }
            //    case NetworkUpdateStage.FixedUpdate:
            //    {
            //        if (!m_FixedUpdate_List.Contains(updateSystem))
            //        {
            //            m_FixedUpdate_List.Add(updateSystem);
            //            m_FixedUpdate_Array = m_FixedUpdate_List.ToArray();
            //        }

            //        break;
            //    }
            //    case NetworkUpdateStage.PreUpdate:
            //    {
            //        if (!m_PreUpdate_List.Contains(updateSystem))
            //        {
            //            m_PreUpdate_List.Add(updateSystem);
            //            m_PreUpdate_Array = m_PreUpdate_List.ToArray();
            //        }

            //        break;
            //    }
            //    case NetworkUpdateStage.Update:
            //    {
            //        if (!m_Update_List.Contains(updateSystem))
            //        {
            //            m_Update_List.Add(updateSystem);
            //            m_Update_Array = m_Update_List.ToArray();
            //        }

            //        break;
            //    }
            //    case NetworkUpdateStage.PreLateUpdate:
            //    {
            //        if (!m_PreLateUpdate_List.Contains(updateSystem))
            //        {
            //            m_PreLateUpdate_List.Add(updateSystem);
            //            m_PreLateUpdate_Array = m_PreLateUpdate_List.ToArray();
            //        }

            //        break;
            //    }
            //    case NetworkUpdateStage.PostLateUpdate:
            //    {
            //        if (!m_PostLateUpdate_List.Contains(updateSystem))
            //        {
            //            m_PostLateUpdate_List.Add(updateSystem);
            //            m_PostLateUpdate_Array = m_PostLateUpdate_List.ToArray();
            //        }

            //        break;
            //    }
            //}
            #endregion
        }

        public static void UnregisterAllNetworkUpdates(this INetworkUpdateSystem updateSystem)
        {
            foreach(KeyValuePair<NetworkUpdateStage,List<INetworkUpdateSystem>> pair in m_RegisteredNetworkUpdates)
            {
                pair.Value.Clear();
            }
            //UnregisterNetworkUpdate(updateSystem, NetworkUpdateStage.Initialization);
            //UnregisterNetworkUpdate(updateSystem, NetworkUpdateStage.EarlyUpdate);
            //UnregisterNetworkUpdate(updateSystem, NetworkUpdateStage.FixedUpdate);
            //UnregisterNetworkUpdate(updateSystem, NetworkUpdateStage.PreUpdate);
            //UnregisterNetworkUpdate(updateSystem, NetworkUpdateStage.Update);
            //UnregisterNetworkUpdate(updateSystem, NetworkUpdateStage.PreLateUpdate);
            //UnregisterNetworkUpdate(updateSystem, NetworkUpdateStage.PostLateUpdate);
        }


        public static void UnregisterNetworkUpdate(this INetworkUpdateSystem updateSystem, NetworkUpdateStage updateStage = NetworkUpdateStage.Update)
        {
            if(m_RegisteredNetworkUpdates.ContainsKey(updateStage))
            {
                if(m_RegisteredNetworkUpdates[updateStage].Contains(updateSystem))
                {
                    m_RegisteredNetworkUpdates[updateStage].Remove(updateSystem);
                }
            }

            #region OLDWAY
            //switch (updateStage)
            //{
            //    case NetworkUpdateStage.Initialization:
            //    {
            //        if ()
            //        {
            //            m_Initialization_List.Remove(updateSystem);
            //            m_Initialization_Array = m_Initialization_List.ToArray();
            //        }

            //        break;
            //    }
            //    case NetworkUpdateStage.EarlyUpdate:
            //    {
            //        if (m_EarlyUpdate_List.Contains(updateSystem))
            //        {
            //            m_EarlyUpdate_List.Remove(updateSystem);
            //            m_EarlyUpdate_Array = m_EarlyUpdate_List.ToArray();
            //        }

            //        break;
            //    }
            //    case NetworkUpdateStage.FixedUpdate:
            //    {
            //        if (m_FixedUpdate_List.Contains(updateSystem))
            //        {
            //            m_FixedUpdate_List.Remove(updateSystem);
            //            m_FixedUpdate_Array = m_FixedUpdate_List.ToArray();
            //        }

            //        break;
            //    }
            //    case NetworkUpdateStage.PreUpdate:
            //    {
            //        if (m_PreUpdate_List.Contains(updateSystem))
            //        {
            //            m_PreUpdate_List.Remove(updateSystem);
            //            m_PreUpdate_Array = m_PreUpdate_List.ToArray();
            //        }

            //        break;
            //    }
            //    case NetworkUpdateStage.Update:
            //    {
            //        if (m_Update_List.Contains(updateSystem))
            //        {
            //            m_Update_List.Remove(updateSystem);
            //            m_Update_Array = m_Update_List.ToArray();
            //        }

            //        break;
            //    }
            //    case NetworkUpdateStage.PreLateUpdate:
            //    {
            //        if (m_PreLateUpdate_List.Contains(updateSystem))
            //        {
            //            m_PreLateUpdate_List.Remove(updateSystem);
            //            m_PreLateUpdate_Array = m_PreLateUpdate_List.ToArray();
            //        }

            //        break;
            //    }
            //    case NetworkUpdateStage.PostLateUpdate:
            //    {
            //        if (m_PostLateUpdate_List.Contains(updateSystem))
            //        {
            //            m_PostLateUpdate_List.Remove(updateSystem);
            //            m_PostLateUpdate_Array = m_PostLateUpdate_List.ToArray();
            //        }

            //        break;
            //    }
            //}
            #endregion
        }

        public static uint FrameCount = 0;
        public static NetworkUpdateStage UpdateStage;

        private static void AdvanceFrame()
        {
            ++FrameCount;
        }

        private static void RunRegisteredUpdates()
        {
            for (int i = 0; i < m_RegisteredNetworkUpdates[UpdateStage].Count; i++)
            {
                m_RegisteredNetworkUpdates[UpdateStage][i].NetworkUpdate();
            }
        }

        private static void RunNetworkInitialization()
        {

            UpdateStage = NetworkUpdateStage.Initialization;
            RunRegisteredUpdates();
        }

        private static void RunNetworkEarlyUpdate()
        {
            AdvanceFrame();
            UpdateStage = NetworkUpdateStage.EarlyUpdate;
            RunRegisteredUpdates();
        }

        private static void RunNetworkFixedUpdate()
        {
            UpdateStage = NetworkUpdateStage.FixedUpdate;
            RunRegisteredUpdates();
        }

        private static void RunNetworkPreUpdate()
        {
            UpdateStage = NetworkUpdateStage.PreUpdate;
            RunRegisteredUpdates();
        }

        private static void RunNetworkUpdate()
        {
            UpdateStage = NetworkUpdateStage.Update;
            RunRegisteredUpdates();
        }

        private static void RunNetworkPreLateUpdate()
        {
            UpdateStage = NetworkUpdateStage.PreLateUpdate;
            RunRegisteredUpdates();
        }

        private static void RunNetworkPostLateUpdate()
        {
            UpdateStage = NetworkUpdateStage.PostLateUpdate;
            RunRegisteredUpdates();
        }
    }
}
