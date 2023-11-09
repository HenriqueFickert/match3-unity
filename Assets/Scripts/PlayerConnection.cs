using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerConnection : MonoBehaviour
{
    private UdpConnection connection;

    void Start()
    {
        string sendIp = "127.0.0.1";
        int sendPort = 3000;
        int receivePort = 11000;

        connection = new UdpConnection();
        connection.StartConnection(sendIp, sendPort, receivePort);

        connection.Send("ENTER");
    }

    void Update()
    {
        foreach (string message in connection.getMessages()) Debug.Log(message);
    }

    void OnDestroy()
    {
        connection.Stop();
    }
}
