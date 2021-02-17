using MLAPI;
using UnityEngine;

public class NetLoopComp : MonoBehaviour, INetworkUpdateSystem
{
    public void RegisterUpdates(int id)
    {
        switch (id % 5)
        {
            case 0:
                this.RegisterAllNetworkUpdates();
                break;
            case 1:
                this.RegisterNetworkUpdate(NetworkUpdateStage.FixedUpdate);
                this.RegisterNetworkUpdate(NetworkUpdateStage.Update);
                this.RegisterNetworkUpdate(NetworkUpdateStage.PreLateUpdate);
                break;
            case 2:
                this.RegisterNetworkUpdate(NetworkUpdateStage.EarlyUpdate);
                this.RegisterNetworkUpdate(NetworkUpdateStage.PreUpdate);
                this.RegisterNetworkUpdate(NetworkUpdateStage.PostLateUpdate);
                break;
            case 3:
                this.RegisterNetworkUpdate(NetworkUpdateStage.Initialization);
                this.RegisterNetworkUpdate(NetworkUpdateStage.FixedUpdate);
                break;
            case 4:
                this.RegisterNetworkUpdate();
                break;
        }
    }

    public void UnregisterUpdates()
    {
        this.UnregisterAllNetworkUpdates();
    }

    private void OnDestroy()
    {
        UnregisterUpdates();
    }

    private readonly int[] m_NetUpdates = new int[7];

    public void NetworkUpdate(NetworkUpdateStage updateStage)
    {
        switch (updateStage)
        {
            case NetworkUpdateStage.Initialization:
                m_NetUpdates[0]++;
                break;
            case NetworkUpdateStage.EarlyUpdate:
                m_NetUpdates[1]++;
                break;
            case NetworkUpdateStage.FixedUpdate:
                m_NetUpdates[2]++;
                break;
            case NetworkUpdateStage.PreUpdate:
                m_NetUpdates[3]++;
                break;
            case NetworkUpdateStage.Update:
                m_NetUpdates[4]++;
                break;
            case NetworkUpdateStage.PreLateUpdate:
                m_NetUpdates[5]++;
                break;
            case NetworkUpdateStage.PostLateUpdate:
                m_NetUpdates[6]++;
                break;
        }
    }
}