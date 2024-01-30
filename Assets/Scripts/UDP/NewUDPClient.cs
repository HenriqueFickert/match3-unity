using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class NewUDPClient
{
    private string senderIp;
    private int senderPort;
    private UdpClient udpClient;

    private Thread receiveThread;
    private bool threadRunning = false;

    private readonly Queue<Package> packagesReceived = new();
    private List<Package> packagesSent = new();

    private int packageSequence = 1;
    private int latestAck = 0;

    private string messageBuffered = "";

    private readonly object packageSequenceLock = new();

    public void StartConnection(string sendIp, int sendPort, int receivePort)
    {
        try { udpClient = new UdpClient(receivePort); }
        catch (Exception e)
        {
            Debug.Log("Failed to listen for UDP at port " + receivePort + ": " + e.Message);
            return;
        }

        Debug.Log(string.Format("Created receiving client at ip {0} and port {1}", sendIp, receivePort));

        senderIp = sendIp;
        senderPort = sendPort;

        Debug.Log("Set sendee at ip " + sendIp + " and port " + sendPort);

        StartReceiveThread();
    }

    private void StartReceiveThread()
    {
        receiveThread = new(() => ListenForMessages(udpClient))
        {
            IsBackground = true
        };

        threadRunning = true;
        receiveThread.Start();
    }

    private void ListenForMessages(UdpClient client)
    {
        IPEndPoint remoteIpEndPoint = new(IPAddress.Any, 0);

        while (threadRunning)
        {
            try
            {
                byte[] receiveBytes = client.Receive(ref remoteIpEndPoint);
                string returnData = Encoding.UTF8.GetString(receiveBytes);
                Debug.Log(returnData);
                BuffedMessage(returnData);
            }
            catch (SocketException e)
            {
                if (e.ErrorCode != 10004) Debug.Log("Socket exception while receiving data from udp client: " + e.Message);
            }
            catch (Exception e)
            {
                Debug.Log("Error receiving data from udp client: " + e.Message);
            }

            Thread.Sleep(1);
        }
    }

    private void BuffedMessage(string message)
    {
        messageBuffered += message;

        if (messageBuffered.Contains('|'))
        {
            string[] messageParts = messageBuffered.Split('|');
            messageBuffered = messageParts.Last();
            foreach (string part in messageParts.Take(messageParts.Length - 1))
            {
                HandleMessageParts(part);
            }
        }
    }

    private void HandleMessageParts(string messagePart)
    {
        try
        {
            Package packageObject = JsonConvert.DeserializeObject<Package>(messagePart);

            if (packageObject.protocolId != "MRQST")
                return;

            //CleanUpSendPackages(packageObject.ack);

            //if (packageObject.type == RequestType.RESEND)
            //{
            //    ResendPackages(packageObject.ack);
            //    return;
            //}

            //if (packageObject.type == RequestType.TIMEOUT)
            //{
            //    SendLastMessageAgain();
            //    return;
            //}

            HandlePackage(packageObject);
        }
        catch
        {
            return;
        }
    }

    private void HandlePackage(Package packageObject)
    {
        if (packageObject.sequence <= latestAck)
            return;

        if (packageObject.sequence == latestAck + 1)
        {
            latestAck = packageObject.sequence;
            AddToReceivedPackages(packageObject);
        }
        else
        {
            //RequestMissingPackage();
        }
    }

    private void AddToReceivedPackages(Package package)
    {
        if (packagesReceived.Any(item => item.sequence == package.sequence))
            return;

        lock (packagesReceived)
        {
            packagesReceived.Enqueue(package);
            packagesReceived.OrderBy(x => x.sequence);
        }
    }

    public Package[] GetMessages()
    {
        Package[] pendingMessages;

        lock (packagesReceived)
        {
            pendingMessages = packagesReceived.ToArray();
            packagesReceived.Clear();
        }

        return pendingMessages;
    }

    private void AddPackageToSentList(Package packageObject)
    {
        if (!packagesSent.Any(p => p.sequence == packageObject.sequence))
        {
            packagesSent.Add(packageObject);
            packagesSent.OrderBy(x => x.sequence);
        }
    }

    private void AddPackageSequence()
    {
        lock (packageSequenceLock)
        {
            packageSequence++;
        }
    }

    private int GetPackageSequence()
    {
        lock (packageSequenceLock)
        {
            return packageSequence;
        }
    }

    public void Stop()
    {
        threadRunning = false;
        receiveThread.Abort();
        udpClient.Close();
    }
}