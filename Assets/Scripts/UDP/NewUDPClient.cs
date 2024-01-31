using Newtonsoft.Json;
using System;
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

    private Timer timeoutTimer;
    private int timeOutCounter = 0;

    private string messageBuffered = "";

    private readonly object packageSequenceLock = new();
    private readonly object latestAckLock = new();

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
        StartTimeoutTimer();
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
                ResetTimeoutTimer();
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
            Package packageObject = null;

            try
            {
                packageObject = JsonConvert.DeserializeObject<Package>(messagePart);
            }
            catch (Exception e)
            {
                Debug.Log("Erro na deserializa��o do pacote: " + e.Message);
            }

            if (packageObject.protocolId != "MRQST")
                return;

            CleanUpSendPackages(packageObject.ack);

            if (packageObject.type == RequestType.RESEND)
            {
                ResendMissingPackages(packageObject.ack);
                return;
            }

            if (packageObject.type == RequestType.TIMEOUT)
            {
                ResendLastPackage();
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
        if (packageObject.sequence <= GetAckNumber())
            return;

        if (packageObject.sequence == GetAckNumber() + 1)
        {
            ChangeAckNumber(packageObject.sequence);
            AddToReceivedPackages(packageObject);
        }
        else
        {
            RequestMissingPackages();
        }
    }

    private void AddToReceivedPackages(Package package)
    {
        lock (packagesReceived)
        {
            if (packagesReceived.Any(item => item.sequence == package.sequence))
                return;

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

    private void CreateAndSendNewPackage(GameCommand gameCommand)
    {
        lock (packagesSent)
        {
            Package package = new(GetPackageSequence(), GetAckNumber(), gameCommand, RequestType.RES);
            SendMessage(package);
            AddPackageSequence();

            if (!packagesSent.Any(p => p.sequence == package.sequence))
            {
                packagesSent.Add(package);
                packagesSent.OrderBy(x => x.sequence);
            }
        }
    }

    private void RequestMissingPackages()
    {
        lock (packagesSent)
        {
            Package package = new(GetPackageSequence(), GetAckNumber(), null, RequestType.RESEND);
            SendMessage(package);
        }
    }

    private void SendTimeoutPackage()
    {
        lock (packagesSent)
        {
            Package package = new(GetPackageSequence(), GetAckNumber(), null, RequestType.TIMEOUT);
            SendMessage(package);
            timeOutCounter++;
        }
    }

    private void ResendMissingPackages(int ack)
    {
        lock (packagesSent)
        {
            if (packagesSent.Any())
            {
                packagesSent.Where(x => x.sequence > ack)
                    .ToList()
                    .ForEach(y =>
                    {
                        SendMessage(y);
                    });
            }
        }
    }

    private void ResendLastPackage()
    {
        lock (packagesSent)
        {
            if (packagesSent.Any())
            {
                SendMessage(packagesSent[^1]);
            }
        }
    }

    private void CleanUpSendPackages(int ack)
    {
        lock (packagesSent)
        {
            packagesSent = packagesSent.Where(pkg => pkg.sequence > ack).ToList();
        }
    }

    private void SendMessage(Package package)
    {
        IPEndPoint serverEndpoint = new(IPAddress.Parse(senderIp), senderPort);

        string message = JsonConvert.SerializeObject(package);
        byte[] sendBytes = Encoding.UTF8.GetBytes(message + "|");

        udpClient.Send(sendBytes, sendBytes.Length, serverEndpoint);
    }

    private void StartTimeoutTimer()
    {
        if (timeoutTimer == null)
            timeoutTimer = new Timer(HandleTimeout, null, 3000, Timeout.Infinite);
        else
            ResetTimeoutTimer();
    }

    private void ResetTimeoutTimer()
    {
        if (timeoutTimer != null)
        {
            timeoutTimer.Change(3000, Timeout.Infinite);
            timeOutCounter = 0;
        }
    }

    private void HandleTimeout(System.Object state)
    {
        Console.WriteLine("Send a timeout request.");

        if (timeOutCounter >= 5)
        {
            HandleDisconnect();
            return;
        }

        SendTimeoutPackage();
        ResetTimeoutTimer();
    }

    private void HandleDisconnect()
    {
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

    private void ChangeAckNumber(int value)
    {
        lock (latestAckLock)
        {
            latestAck = value;
        }
    }

    private int GetAckNumber()
    {
        lock (latestAckLock)
        {
            return latestAck;
        }
    }

    public void Stop()
    {
        threadRunning = false;
        //receiveThread.Abort();
        udpClient.Close();
    }
}