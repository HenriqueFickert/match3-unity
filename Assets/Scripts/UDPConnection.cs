using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class UdpConnection
{
    // Cliente UDP para enviar e receber dados.
    private UdpClient udpClient;

    // Fila para armazenar as mensagens recebidas.
    private readonly Queue<string> incomingQueue = new Queue<string>();

    // Thread para o recebimento de mensagens, para n�o bloquear a thread principal.
    Thread receiveThread;

    // Flag para controle do estado da thread de recebimento.
    private bool threadRunning = false;

    // Endere�o IP do remetente.
    private string senderIp;

    // Porta de envio do remetente.
    private int senderPort;

    // M�todo para iniciar a conex�o, configurar o cliente UDP e iniciar a thread de recebimento.
    public void StartConnection(string sendIp, int sendPort, int receivePort)
    {
        // Tenta criar um cliente UDP para ouvir na porta especificada.
        try { udpClient = new UdpClient(receivePort); }
        catch (Exception e)
        {
            Debug.Log("Failed to listen for UDP at port " + receivePort + ": " + e.Message);
            return;
        }

        Debug.Log(String.Format("Created receiving client at ip {0} and port {1}", sendIp, receivePort));

        this.senderIp = sendIp;
        this.senderPort = sendPort;

        // Informa que o endere�o de envio foi definido.
        Debug.Log("Set sendee at ip " + sendIp + " and port " + sendPort);

        // Inicia a thread de recebimento.
        StartReceiveThread();
    }

    // M�todo privado para iniciar a thread de recebimento.
    private void StartReceiveThread()
    {
        // Cria e configura a thread.
        receiveThread = new Thread(() => ListenForMessages(udpClient));
        receiveThread.IsBackground = true;
        threadRunning = true;
        // Inicia a thread.
        receiveThread.Start();
    }

    // M�todo que ser� executado na thread de recebimento.
    private void ListenForMessages(UdpClient client)
    {
        // Define um ponto final para qualquer IP e porta.
        IPEndPoint remoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);

        // Loop de execu��o enquanto a thread estiver ativa.
        while (threadRunning)
        {
            try
            {
                // Bloqueia at� receber uma mensagem e a decodifica.
                Byte[] receiveBytes = client.Receive(ref remoteIpEndPoint);
                string returnData = Encoding.UTF8.GetString(receiveBytes);

                // Coloca a mensagem decodificada na fila.
                lock (incomingQueue)
                {
                    incomingQueue.Enqueue(returnData);
                }
            }

            // Trata exce��es de socket e outras exce��es.
            catch (SocketException e)
            {
                // Exce��o espec�fica quando o socket � fechado.
                if (e.ErrorCode != 10004) Debug.Log("Socket exception while receiving data from udp client: " + e.Message);
            }
            catch (Exception e)
            {
                Debug.Log("Error receiving data from udp client: " + e.Message);
            }

            // Pausa curta para evitar uso excessivo da CPU.
            Thread.Sleep(1);
        }
    }

    // M�todo para recuperar mensagens da fila.
    public string[] getMessages()
    {
        string[] pendingMessages = new string[0];

        // Garante o acesso exclusivo � fila para remover as mensagens.
        lock (incomingQueue)
        {
            pendingMessages = new string[incomingQueue.Count];
            int i = 0;
            while (incomingQueue.Count != 0)
            {
                pendingMessages[i] = incomingQueue.Dequeue();
                i++;
            }
        }

        // Retorna as mensagens pendentes.
        return pendingMessages;
    }

    // M�todo para enviar uma mensagem usando o cliente UDP.
    public void Send(string message)
    {
        // Registra a mensagem a ser enviada.
        Debug.Log(String.Format("Send to ip {0} at port {1} the message: {2}", senderIp, senderPort, message));

        // Cria um ponto final com o IP e a porta do remetente.
        IPEndPoint serverEndpoint = new IPEndPoint(IPAddress.Parse(senderIp), senderPort);

        // Codifica a mensagem em bytes e a envia.
        Byte[] sendBytes = Encoding.UTF8.GetBytes(message);
        udpClient.Send(sendBytes, sendBytes.Length, serverEndpoint);
    }

    // M�todo para parar a thread de recebimento e fechar o cliente UDP.
    public void Stop()
    {
        // Sinaliza para a thread de recebimento parar e for�a sua finaliza��o.
        threadRunning = false;
        receiveThread.Abort();

        // Fecha o cliente UDP.
        udpClient.Close();
    }
}