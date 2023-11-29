using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEditor.MemoryProfiler;
using UnityEngine;

public class Teste : MonoBehaviour
{
    private UDPClient connection;

    private void Awake()
    {
        string sendIp = "127.0.0.1";
        int sendPort = 3000;
        int receivePort = Random.Range(11000, 11500);

        connection = new UDPClient();
        connection.StartConnection(sendIp, sendPort, receivePort);
    }

    void Start()
    {
        connection.CreateAndSendMessage(null, RequestType.RES);
    }

    void Update()
    {
        foreach (Package message in connection.GetMessages())
        {
            Debug.Log(JsonConvert.SerializeObject(message));
        }

        if (Input.GetKey(KeyCode.Z))
        {
            connection.CreateAndSendMessage(null, RequestType.RES);
        }
    }

    private void OnDisable()
    {
        connection.Stop();
    }
}
