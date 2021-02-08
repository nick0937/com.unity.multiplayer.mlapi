using System;
using UnityEngine;
using MLAPI;

namespace MLAPI
{
    public class NetworkTickSystem : NetworkUpdateLoopBehaviour
    {

        [Range(1,120)]
        public  int tickFrequency = 60;         //60 fps network tick default (range from 1 to 120)
        private int m_CurrentTickFrequency;     //Used to determine if tick frequency changed

        private double m_NetworkFrameTick;      //How many network ticks have passed?
        private double m_TimePerTick;           //Calculated from TickFrequency
        private double m_NetworkTime;           //The current network time based on NetworkFrameTick and TickPeriod
        private double m_LastTickUpdate;        //When the last tick update happened
        private double m_AverageTickDelta;      //Average Time between tick updates


        //For testing purposes
        private bool m_IsServerForced;
        private bool m_IsTicking;

        public void StartTicking(bool isTicking, bool bForceServer = false)
        {
            if(isTicking)
            {
                m_IsServerForced = bForceServer;
                CalculateTickTime();
                m_NetworkFrameTick = 0;                                         //Start at frame 0
                m_NetworkTime = 0;                                              //Set the initial time elapsed to zero
                m_LastTickUpdate = Time.unscaledTime;
                if(bForceServer)
                {
                    RegisterUpdateLoopSystem();
                }
            }
            else
            {
                OnNetworkLoopSystemRemove();
            }
            m_IsTicking = isTicking;
        }

        private bool IsServerEnabled()
        {
            return (IsServer || m_IsServerForced);
        }

        /// <summary>
        /// NetworkStart
        /// Called when the network is started
        /// Server Side: Initializes the network tick
        /// </summary>
        public override void NetworkStart()
        {
            if (IsServerEnabled())
            {
                StartTicking(true);
            }

            base.NetworkStart();
        }

        /// <summary>
        /// InternalRegisterNetworkUpdateStage
        /// Registers for the PreUpdate stage (psuedo code, this might end up being placed ahead of all PreUpdate registrations)
        /// </summary>
        /// <param name="stage">stage to register for an update</param>
        /// <returns></returns>
        protected override Action InternalRegisterNetworkUpdateStage(NetworkUpdateManager.NetworkUpdateStage stage)
        {
            if (IsServerEnabled() && stage == NetworkUpdateManager.NetworkUpdateStage.PreUpdate)
            {
                return new Action(UpdateNetworkTick);
            }

            return null;
        }

        /// <summary>
        /// Calculates the tick time if there is a delta between the CurrentTickFrequency and TickFrequency
        /// </summary>
        private void CalculateTickTime()
        {
            if (m_CurrentTickFrequency != tickFrequency)
            {
                m_CurrentTickFrequency = tickFrequency;
                //This calculation rounds down to the nearest millisecond, anything beyond that is outside of typical network communication latencies
                m_TimePerTick =(float)Math.Truncate(1000 * (1.0f / (float)m_CurrentTickFrequency)) * 0.001f;
            }
        }

        /// <summary>
        /// UpdateNetworkTick
        /// Called each network loop update during the PreUpdate stage
        /// </summary>
        private void UpdateNetworkTick()
        {
            if (IsServerEnabled() && m_IsTicking)
            {
                //Always check to see if there was a change?
                CalculateTickTime();
                double unscaledTime = Time.unscaledTimeAsDouble;
                double deltaTime = unscaledTime - m_LastTickUpdate;

                if (m_TimePerTick <= deltaTime)
                {
                    m_LastTickUpdate = unscaledTime;
                    m_NetworkFrameTick++;
                    m_NetworkTime = m_TimePerTick * m_NetworkFrameTick;
                    m_AverageTickDelta += deltaTime;
                    m_AverageTickDelta *= 0.5;
                }
            }
        }

        /// <summary>
        /// NetworkTickUpdate
        /// Invoked on the client side when receiving a network tick
        /// Calculates the network time relative to the tick.
        /// </summary>
        /// <param name="networkTick"></param>
        /// <returns>true = tick was updated | false = tick was not updated</returns>
        public bool NetworkTickUpdate(int networkTick, double )
        {
            if(!IsServer)
            {
                if(networkTick > m_NetworkFrameTick)
                {
                    m_NetworkFrameTick = (double)networkTick;
                    double PreviousNetworkTime = m_NetworkTime;
                    m_NetworkTime = m_TimePerTick * m_NetworkFrameTick;
                    double deltaTime = m_NetworkTime - PreviousNetworkTime;
                    m_AverageTickDelta += deltaTime;
                    m_AverageTickDelta *= 0.5;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// GetTickInterval
        /// </summary>
        /// <returns></returns>
        public double GetTickInterval()
        {
            return m_TimePerTick;
        }

        /// <summary>
        /// GetCurrentTick
        /// Gets the non-fractional current network tick
        /// </summary>
        /// <returns></returns>
        public int GetCurrentTick()
        {
            return (int)m_NetworkFrameTick;
        }

        /// <summary>
        /// GetTickDeltaTimeAverage
        /// Returns the averaged delta time between network ticks
        /// </summary>
        /// <returns>Averaged tick time delta</returns>
        public double GetAverageTickDelta()
        {
            return m_AverageTickDelta;
        }

        /// <summary>
        /// GetNetworkTimeDelta
        /// NetworkTime is calculated from the number of network ticks times m_TimePerTick.
        /// Server: Does this calculation when it updates the tick value
        /// Client: Does this calculation when it receives a new tick value
        /// </summary>
        /// <returns>Network Tick Time</returns>
        public double GetNetworkTime()
        {
            return  m_NetworkTime;
        }
    }
}
