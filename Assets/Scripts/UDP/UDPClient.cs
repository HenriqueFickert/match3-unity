using System;
using System.Threading;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Text;
using UnityEngine;
using Newtonsoft.Json;
using System.Linq;

public class UDPClient
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
    private Timer timeoutTimer;
    private int timeOutCounter = 0;

    public void StartConnection(string sendIp, int sendPort, int receivePort)
    {
        try { udpClient = new UdpClient(receivePort); }
        catch (Exception e)
        {
            Debug.Log("Failed to listen for UDP at port " + receivePort + ": " + e.Message);
            return;
        }

        Debug.Log(String.Format("Created receiving client at ip {0} and port {1}", sendIp, receivePort));

        senderIp = sendIp;
        senderPort = sendPort;

        Debug.Log("Set sendee at ip " + sendIp + " and port " + sendPort);

        StartReceiveThread();
        StartTimeoutTimer();
    }

    private void StartReceiveThread()
    {
        receiveThread = new(() => ListenForMessages(udpClient));
        receiveThread.IsBackground = true;
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
                ResetTimeoutTimer();
                byte[] receiveBytes = client.Receive(ref remoteIpEndPoint);
                string returnData = Encoding.UTF8.GetString(receiveBytes);
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

            CleanUpSendPackages(packageObject.ack);

            if (packageObject.type == RequestType.RESEND)
            {
                ResendPackages(packageObject.ack);
                return;
            }

            if (packageObject.type == RequestType.TIMEOUT)
            {
                SendLastMessageAgain();
                return;
            }

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
            RequestMissingPackage();
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

    private void CleanUpSendPackages(int ack)
    {
        packagesSent = packagesSent.Where(pkg => pkg.sequence > ack).ToList();
    }

    private void RequestMissingPackage()
    {
        Package requestResend = new (packageSequence, latestAck, null, RequestType.RESEND);
        SendMessage(requestResend, false);
    }

    private void ResendPackages(int ack)
    {
        if (packagesSent.Any())
        {
            packagesSent.Where(x => x.sequence > ack)
                .ToList()
                .ForEach(y => {
                 SendMessage(y, false);
             });
        }
    }

    public Package[] GetMessages()
    {
        Package[] pendingMessages = new Package[0];

        lock (packagesReceived)
        {
            pendingMessages = new Package[packagesReceived.Count];
            int i = 0;
            while (packagesReceived.Count != 0)
            {
                pendingMessages[i] = packagesReceived.Dequeue();
                i++;
            }
        }

        return pendingMessages;
    }

    public void SendMessage(Package message, bool addToPackagesList = true)
    {
        IPEndPoint serverEndpoint = new(IPAddress.Parse(senderIp), senderPort);

        string package = JsonConvert.SerializeObject(message);
        byte[] sendBytes = Encoding.UTF8.GetBytes(package);

        udpClient.Send(sendBytes, sendBytes.Length, serverEndpoint);

        if (!addToPackagesList) return;

        if (AddPackageToSentList(message))
        {
            packageSequence++;
        }
    }

    private bool AddPackageToSentList(Package packageObject)
    {
        if (!packagesSent.Any(p => p.sequence == packageObject.sequence))
        {
            packagesSent.Add(packageObject);
            packagesSent.OrderBy(x => x.sequence);
            return true;
        }

        return false;
    }

    private void StartTimeoutTimer()
    {
        timeoutTimer?.Dispose();
        timeoutTimer = new Timer(HandleTimeout, null, 3000, Timeout.Infinite);
    }

    private void ResetTimeoutTimer()
    {
        StartTimeoutTimer();
        timeOutCounter = 0;
    }

    private void HandleTimeout(System.Object state)
    {
        Console.WriteLine("Send a timeout request.");

        if (timeOutCounter >= 5)
        {
            HandleDisconnect();
            return;
        }

        Package timeoutMessage = new (packageSequence, latestAck, null, RequestType.TIMEOUT);
        SendMessage(timeoutMessage, false);
        timeOutCounter++;
        StartTimeoutTimer();
    }

    void HandleDisconnect()
    {
        //Package timeoutMessage = new (packageSequence, latestAck, null, RequestType.DISCONNECTED);
        //SendMessage(timeoutMessage, false);
    }

    private void SendLastMessageAgain()
    {
        if (packagesSent.Any())
        {
            SendMessage(packagesSent[^1], false);
        }
    }

    public void Stop()
    {
        threadRunning = false;
        receiveThread.Abort();
        udpClient.Close();
    }
}