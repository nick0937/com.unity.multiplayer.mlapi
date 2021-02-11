using System;
using UnityEngine;


namespace MLAPI
{
    public class NetworkTickSystem : GenericUpdateLoopSystem, IDisposable
    {
        private const int k_DefaultTickFrequency = 20;

        private bool m_IsTicking;               //Used to determine if the tick system is ticking

        private int tickFrequency;              //current network tick frequency in network frames per second
        private int m_CurrentTickFrequency;     //Used to determine if tick frequency changed

        private double m_NetworkFrameTick;      //How many network ticks have passed?
        private double m_TimePerTick;           //Calculated from TickFrequency
        private double m_NetworkTime;           //The current network time based on m_NetworkFrameTick and m_TimePerTick
        private double m_LastTickUpdate;        //When the last tick update happened

        /// <summary>
        /// IsTicking
        /// Returns whether the tick system is updating network ticks
        /// </summary>
        /// <returns></returns>
        public bool IsTicking()
        {
            return m_IsTicking;
        }

        /// <summary>
        /// GetTickInterval
        /// Returns the tick interval period in seconds
        /// </summary>
        /// <returns></returns>
        public double GetTickInterval()
        {
            return m_TimePerTick;
        }

        /// <summary>
        /// GetTick
        /// Gets the non-fractional current network tick
        /// </summary>
        /// <returns></returns>
        public int GetTick()
        {
            return (int)m_NetworkFrameTick;
        }

        /// <summary>
        /// GetNetworkTime
        /// NetworkTime is a calculation based on delta time since the last network tick
        /// </summary>
        /// <returns>Network Tick Time</returns>
        public double GetNetworkTime()
        {
            return  m_NetworkTime;
        }

        /// <summary>
        /// Start
        /// Stats the network tick system
        /// </summary>
        /// <param name="resetsystem"></param>
        public void Start(bool resetsystem = false)
        {
            if(!m_IsTicking)
            {
                InitializeTickSystem(resetsystem);
            }
        }

        /// <summary>
        /// Stop
        /// Stops the network tick system
        /// </summary>
        public void Stop()
        {
            m_IsTicking = false;
            OnNetworkLoopSystemRemove();
        }

        /// <summary>
        /// Dispose
        /// Called when this class is destroyed
        /// </summary>
        public void Dispose()
        {
            if(m_IsTicking)
            {
                Stop();
            }
        }

        /// <summary>
        /// CalculateTickTime
        /// Calculates the tick time if there is a delta between the CurrentTickFrequency and TickFrequency
        /// </summary>
        private void CalculateTickTime()
        {
            m_CurrentTickFrequency = tickFrequency;

            //This calculation rounds down to the nearest millisecond, anything beyond that is outside of typical network communication latencies
            m_TimePerTick = Math.Truncate(1000 * (1.0 / (double)m_CurrentTickFrequency)) * 0.001;
        }

        /// <summary>
        /// InitializeTickSystem
        /// Initializes the Network Tick System
        /// </summary>
        /// <param name="resetsystem"></param>
        private void InitializeTickSystem(bool resetsystem = true)
        {
            if (resetsystem)
            {
                m_NetworkFrameTick = 0;
                m_NetworkTime = 0;
                m_LastTickUpdate = Time.unscaledTimeAsDouble;
            }

            RegisterUpdateLoopSystem();

            m_IsTicking = true;
        }

        /// <summary>
        /// InternalRegisterNetworkUpdateStage
        /// Registers for the PreUpdate stage (psuedo code, this will be replaced with new network update loop registration)
        /// </summary>
        /// <param name="stage">stage to register for an update</param>
        /// <returns></returns>
        protected override Action InternalRegisterNetworkUpdateStage(NetworkUpdateManager.NetworkUpdateStage stage)
        {
            if (stage == NetworkUpdateManager.NetworkUpdateStage.PreUpdate)
            {
                return new Action(UpdateNetworkTick);
            }
            return null;
        }

        /// <summary>
        /// UpdateNetworkTick
        /// Called each network loop update during the PreUpdate stage
        /// </summary>
        private void UpdateNetworkTick()
        {
            if (m_IsTicking)
            {
                double unscaledTime = Time.unscaledTimeAsDouble;
                double deltaTime = unscaledTime - m_LastTickUpdate;

                if (m_TimePerTick <= deltaTime)
                {
                    double tickDelta = deltaTime/m_TimePerTick;
                    m_NetworkFrameTick += (int)tickDelta;
                    m_NetworkTime = m_TimePerTick * (double)m_NetworkFrameTick;
                    m_LastTickUpdate = unscaledTime;
                }
            }
        }

        /// <summary>
        /// Constructor
        /// Defaults to k_DefaultTickFrequency NFPS (network frames per second) if no tick frequency is specified
        /// </summary>
        /// <param name="tickFreq"></param>
        public NetworkTickSystem(int tickFreq = k_DefaultTickFrequency)
        {
            //Assure we don't specify a value less than or equal to zero for tick frequency
            tickFrequency = tickFreq <= 0 ? k_DefaultTickFrequency:tickFreq;
            CalculateTickTime();
        }
    }
}
