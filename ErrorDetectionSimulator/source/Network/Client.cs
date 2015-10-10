using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ErrorDetectionSimulator.source.Network
{
	/// <summary>
	/// An asynchronous implementation of socket.
	/// It will not block the main thread and halt UI interactions.
	/// </summary>
	public class Client
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

		private Socket m_client;
		private IPEndPoint m_remoteEP = null;

		/// <summary>
		/// Main Constructor to initialise out Client
		/// </summary>
		/// <param name="ipAddress">IPAddress object, containing the IP Address</param>
		/// <param name="port">Port number we will making a connection to</param>
		public Client(IPAddress ipAddress, int port)
		{
			// Get the end point (IP and port) we will be connecting to.
			m_remoteEP = new IPEndPoint(ipAddress, port);

			// Create a TCP/IP socket.
			m_client = new Socket(m_remoteEP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

			// Connect to the remote endpoint.
			//m_client.BeginConnect(m_remoteEP, ConnectCallback, m_client);
		}

		~Client()
		{
			// Shutdown and close our socket when we are done.
			if (m_client.Connected)
				m_client.Shutdown(SocketShutdown.Both);

			m_client.Close();
		}


		public void Connect()
		{
			// Start connecting to the remote endpoint without blocking other operations.
			m_client.BeginConnect(m_remoteEP, ConnectCallback, m_client);
		}

		private void ConnectCallback(IAsyncResult ar)
		{
			try
			{
				// Get the result, whether we are connected or not or have hit a rock.
				Socket client = (Socket)ar.AsyncState;
				client.EndConnect(ar);

				if (client.Connected)
				{
					// Notify any listeners that we are connected to a server.
					if (OnSocketConnect != null)
						OnSocketConnect(client);

					// Object to store the state of the connection.
					// It will store the connection (socket) and data received it.
					StateObject state = new StateObject();
					state.workSocket = m_client;
					// Start receiving any data sent by the server we're connected to.
					m_client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, ReceiveCallback, state);

				}
				else
				{
					// If we are not connected then the connection may not have been successful.
					HandleSocketDisconnect();
				}
			}
			catch
			{
				// Something went wrong while connecting to the server.
				HandleSocketDisconnect();
			}
		}

		// Callback to handle data received
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

					// If we have received some data then process it.
					if (bytesRead > 0)
					{
						string content = string.Empty;

						// There might be more data, so store the data received so far.
						state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));

						// Check for end-of-file tag. If it is not there, there must be more data on its way.
						content = state.sb.ToString();
						if (content.IndexOf("<EOF>") != -1)
						{
							// Remove "<EOF>" from the string
							content = content.Remove(content.Length - 5, 5);

							// Let the application know that we have received a message
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
				else
				{
					// We may have disconnected.
					HandleSocketDisconnect();
				}
			}
			catch (SocketException)
			{
				HandleSocketDisconnect();
			}
		}

		public void Send(string message)
		{
			try
			{

				// Translate the passed message into ASCII and store it as a Byte array.
				// And also add end-of-file tag to it so we know when we have received all of it on the other end.
				byte[] byteData = Encoding.ASCII.GetBytes(string.Concat(message, "<EOF>"));

				if (m_client.Connected)
				{
					m_client.BeginSend(byteData, 0, byteData.Length, 0, SendCallback, m_client);
				}
				else
				{
					HandleSocketDisconnect();
				}
			}
			catch (SocketException)
			{
				HandleSocketDisconnect();
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

					// Notify any event handlers
					if (OnDataSent != null)
						OnDataSent();
				}
				else
				{
					HandleSocketDisconnect();
				}

			}
			catch (Exception)
			{
				HandleSocketDisconnect();
			}
		}

		private void HandleSocketDisconnect()
		{
			if (OnSocketDisconnect != null)
				OnSocketDisconnect();
		}
	}
}
