using System.Collections;
using System.Collections.Generic;
using MLAPI;
using MLAPI.Configuration;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using MLAPI.Transports.UNET;

public class TransportTest : MonoBehaviour
{

    public NetworkedObject MakeObjectHelper(Vector3 coords)
    {
        GameObject o = new GameObject();
        NetworkedObject no = (NetworkedObject)o.AddComponent(typeof(NetworkedObject));

        no.transform.position = coords;
        return no;
    }

    // A Test behaves as an ordinary method
    [Test]
    public void TestChannels()
    {
        GameObject o = new GameObject();
        NetworkingManager nm = (NetworkingManager)o.AddComponent(typeof(NetworkingManager));
        nm.SetSingleton();
        nm.NetworkConfig = new NetworkConfig();
        UnetTransport ut = (UnetTransport)o.AddComponent(typeof(UnetTransport));
        ut.ServerListenPort = 7777;
        nm.NetworkConfig.NetworkTransport = ut;

        nm.StartServer();
        nm.StopServer();
        nm.Shutdown();

/*
        HashSet<NetworkedObject> objList = new HashSet<NetworkedObject>();

        var p1 = MakeObjectHelper(new Vector3(0.9f, 0.0f, 0.0f));
        var p2 = MakeObjectHelper(new Vector3(1.1f, 0.0f, 0.0f));

        var center = MakeObjectHelper(new Vector3(0.0f, 0.0f, 0.0f));
        Assert.Less(Vector3.Distance(center.transform.position, p1.transform.position), 1.0f, "Point 1 failed test");
        Assert.Greater(Vector3.Distance(center.transform.position, p2.transform.position), 1.0f, "Point 2 failed test");
        */
    }
}

