﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using YAVSRG.Net.P2P.Protocol.Packets;

namespace YAVSRG.Net.P2P
{
    public class SocketServer
    {
        private Socket sock;
        private SocketAsyncEventArgs accept;
        private ClientWrapper[] clients = new ClientWrapper[16];

        public SocketServer()
        {
            PacketPing.OnReceive += HandlePing;
            PacketAuth.OnReceive += HandleAuth;
            PacketMessage.OnReceive += HandleMessage;
        }

        public bool Start()
        {
            try
            {
                Utilities.Logging.Log("Trying to host server..");
                sock = new Socket(SocketType.Stream, ProtocolType.Tcp);
                sock.Bind(new IPEndPoint(0, 32767));
                sock.Listen(3);
                accept = new SocketAsyncEventArgs();
                accept.Completed += OnAccept;
                sock.AcceptAsync(accept);
                Utilities.Logging.Log("Looks good. Server is awaiting connections.");
                return true;
            }
            catch (Exception e)
            {
                Utilities.Logging.Log("Failed to start server: "+ e.ToString(), Utilities.Logging.LogType.Error);
                sock.Disconnect(false);
                sock.Dispose();
                return false;
            }
        }

        public void ShutDown()
        {
            sock.Close();
            sock.Dispose();

            PacketPing.OnReceive -= HandlePing;
            PacketAuth.OnReceive -= HandleAuth;
        }

        public void Update()
        {
            for (int i = 0; i < clients.Length; i++)
            {
                if (clients[i] != null)
                {
                    if (clients[i].Closed)
                    {
                        Utilities.Logging.Log("Dropped client with id " + i.ToString());
                        clients[i] = null;
                    }
                    else
                    {
                        clients[i].Update(i);
                    }
                }
            }
        }

        public void Broadcast(string message)
        {
            for (int i = 0; i < clients.Length; i++)
            {
                clients[i]?.SendPacket(new PacketMessage() { text = message });
            }
        }

        public void Message(string message, int id)
        {
            clients[id]?.SendPacket(new PacketMessage() { text = message });
        }

        public void Kick(string reason, int id)
        {
            Message("You have been kicked: "+reason, id);
            clients[id]?.Disconnect();
        }

        private void HandleMessage(PacketMessage packet, int id)
        {
            if (id >= 0 && clients[id]?.LoggedIn == true)
            {
                Broadcast(clients[id].Username + ": " + packet.text);
            }
        }

        private void HandleAuth(PacketAuth packet, int id)
        {
            if (clients[id] != null)
            {
                if (clients[id].LoggedIn)
                {
                    Utilities.Logging.Log("Client tried to log in twice!", Utilities.Logging.LogType.Warning);
                }
                else
                {
                    clients[id].Auth(packet);
                }
            }
            else
            {
                Utilities.Logging.Log("Received auth packet for empty slot!", Utilities.Logging.LogType.Warning);
            }
        }

        private void HandlePing(PacketPing packet, int id)
        {
            if (id >= 0)
                clients[id]?.Ping();
        }

        private void OnAccept(object o, SocketAsyncEventArgs e)
        {
            bool freeslot = false;
            for (int i = 0; i < clients.Length; i++)
            {
                if (clients[i] == null)
                {
                    freeslot = true;
                    clients[i] = new ClientWrapper(e.AcceptSocket);
                    Utilities.Logging.Log("Accepted new connection, client id is " + i.ToString());
                    break;
                }
            }
            if (!freeslot)
            {
                Utilities.Logging.Log("There was a new connection, but the server slots are full!", Utilities.Logging.LogType.Warning);
                e.AcceptSocket.Close();
            }

            accept = new SocketAsyncEventArgs();
            accept.Completed += OnAccept;
            sock.AcceptAsync(accept);
        }
    }
}
