using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ErrorDetectionSimulator.source.Network
{
	// State object for receiving data from remote device.
	public class StateObject
	{
		// Client socket.
		public Socket workSocket = null;
		// Size of receive buffer.
		public const int BufferSize = 256;
		// Receive buffer.
		public byte[] buffer = new byte[BufferSize];
		// Received data string.
		public StringBuilder sb = new StringBuilder();
	}

	/// <summary>
	///  An asynchronous socket server implementation, handles connections, receives and sends data.
	/// </summary>
	public class Server : IDisposable
	{
		// Delegate functions that will be used for event notification
		public delegate void OnSocketConnectHandler(Socket socket);
		public delegate void SocketDisconnectHandler();
		public delegate void OnDataSentHandler();
		public delegate void OnDataRecieveHandler(string message);

		// These events will notify any subscribers, when triggered
		// When a client connects
		public event OnSocketConnectHandler OnSocketConnect;
		// When a client disconnects
		public event SocketDisconnectHandler OnSocketDisconnect;
		// When data is sent to a client
		public event OnDataSentHandler OnDataSent;
		// When data is received from a client
		public event OnDataRecieveHandler OnDataRecieve;

		// Socket listener
		private Socket m_listener = null;
		// Our address and port number
		private IPEndPoint m_localEP;
		// List to store all connected clients.
		private List<Socket> m_clients;

		public Server(IPAddress ipAddress, int port, int backlog = 100)
		{
			m_clients = new List<Socket>();

			// Establish the local endpoint for the socket.
			m_localEP = new IPEndPoint(ipAddress, port);

			// Create a TCP/IP socket.
			m_listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			// Bind the socket to the local endpoint
			m_listener.Bind(m_localEP);
			// And listen for incoming connections.
			m_listener.Listen(backlog);
		}

		// Begin accepting connections from clients.
		public void StartListening()
		{
			m_listener.BeginAccept(AcceptCallback, m_listener);
		}

		// When a client connects
		private void AcceptCallback(IAsyncResult ar)
		{
			// Get the socket that handles the client request.
			Socket listener = (Socket)ar.AsyncState;
			Socket socket = listener.EndAccept(ar);

			try
			{
				// If the client did connect successfully
				if (socket.Connected)
				{
					// Add the client connected to our list of connections.
					m_clients.Add(socket);

					// Notify any subscribers that a client has just connected
					if (OnSocketConnect != null)
						OnSocketConnect(socket);

					// Object to store the state of the connection.
					// It will store state of this connection (socket) and data received from it.
					StateObject state = new StateObject();
					state.workSocket = socket;

					// And finally start receiving data from this client.
					socket.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, ReceiveCallback, state);
				}
				else
				{
					// The client may not have connected successfully.
					HandleClientDisconnect(socket);
				}
			}
			catch (SocketException)
			{
				// Something went wrong during the connection
				HandleClientDisconnect(socket);
			}
			finally
			{
				// Listen for more incoming client connections.
				m_listener.BeginAccept(AcceptCallback, m_listener);
			}
		}

		// Will handle any data sent by the clients
		private void ReceiveCallback(IAsyncResult ar)
		{
			// Retrieve the state object and the handler socket
			// from the asynchronous state object.
			StateObject state = (StateObject)ar.AsyncState;
			Socket socket = state.workSocket;

			try
			{
				if (socket.Connected)
				{
					// Read data from the client socket. 
					int bytesRead = socket.EndReceive(ar);

					// If we have received some data
					if (bytesRead > 0)
					{
						string content = string.Empty;

						// There might be more data, so store the data received so far.
						state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));

						// Check for end-of-file tag. If it is not there, there must be more data on its way.
						content = state.sb.ToString();
						if (content.IndexOf("<EOF>") != -1)
						{
							// Broadcast this message to all clients connected.
							BroadcastMessage(content, socket);

							// Remove "<EOF>" from the string
							content = content.Remove(content.Length - 5, 5);

							// Notify any listeners that we've received a message					
							if (OnDataRecieve != null)
								OnDataRecieve(content);

							// Clear the string now that we have processed the message
							state.sb.Clear();

							// And receive more data if and when its sent
							socket.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, ReceiveCallback, state);
						}
						else
						{
							// Not all data received. Get more.
							socket.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, ReceiveCallback, state);
						}
					}
				}
			}
			catch (SocketException)
			{
				HandleClientDisconnect(socket);
			}
		}

		public void BroadcastMessage(string message, Socket sender)
		{
			try
			{
				// Translate the passed message into ASCII and store it as a Byte array.
				byte[] byteData = Encoding.ASCII.GetBytes(message);

				// Loop through the list of clients and send the broadcast to all except the sender
				foreach (Socket client in m_clients)
				{
					try
					{
						if (client.Connected)
						{
							// If its not the client sent this message
							if (client != sender)
								client.BeginSend(byteData, 0, byteData.Length, 0, SendCallback, client);
						}
						else
						{
							HandleClientDisconnect(client);
						}
					}
					catch (Exception e)
					{
						Console.WriteLine(e.ToString());
					}
				}
			}
			catch (Exception e)
			{
				// Serialization error
				Console.WriteLine(e.ToString());
			}
		}

		private void SendCallback(IAsyncResult ar)
		{
			try
			{
				Socket socket = (Socket)ar.AsyncState;

				if (socket.Connected)
				{
					// Complete sending the data to the remote device.
					int bytesSent = socket.EndSend(ar);

					// Notify any listeners
					if (OnDataSent != null)
						OnDataSent();
				}
				else
				{
					HandleClientDisconnect(socket);
				}

			}
			catch (Exception e)
			{
				Console.WriteLine(e.ToString());
			}
		}

		private void HandleClientDisconnect(Socket client)
		{
			// Client socket may have shutdown unexpectedly
			if (client.Connected)
				client.Shutdown(SocketShutdown.Both);

			// Close the socket and remove it from the list
			client.Close();
			m_clients.Remove(client);

			// Notify any event handlers
			if (OnSocketDisconnect != null)
				OnSocketDisconnect();
		}

		// Close all client connections when we're done.
		public void Dispose()
		{
			foreach (Socket client in m_clients)
			{
				if (client.Connected)
					client.Shutdown(SocketShutdown.Receive);

				client.Close();
			}
		}

		// Properties
		public IPEndPoint localEndPoint { get { return m_localEP; } }
		public int Connections { get { return m_clients.Count; } }
	}
}
