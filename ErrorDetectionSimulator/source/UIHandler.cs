using System;
using System.Net;
using System.Windows;
using System.Net.Sockets;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;
using System.Collections.Generic;
using ErrorDetectionSimulator.source.Network;

namespace ErrorDetectionSimulator.source
{
	public enum eAppMode
	{
		None,
		Server,
		Client,
	};

	public struct ChatMessage
	{
		public string data;
		public string sender;

		public ChatMessage(string a_message, string a_sender)
		{
			this.data = a_message;
			this.sender = a_sender;
		}
	};

	/// <summary>
	/// Handles UI interactions
	/// </summary>
	public sealed class UIHandler
	{
		// There should only be one instance of this class handling button clicks etc.
		private static readonly UIHandler m_instance = new UIHandler();

		private MainWindow m_mainWindow = null;

		private Server m_server = null;
		private Client m_client = null;

		// Variables for UI elements.
		private eAppMode m_appMode;
		private Grid m_appModeSelectionGrid = null;
		private Grid m_appModeBtnsGrid = null;
		private Grid m_appModeInputControlsGrid = null;
		private Label m_appModeSelectionLbl = null;
		private Button m_serverModeBtn = null;
		private Button m_clientModeBtn = null;
		private Button m_findIPBtn = null;
		private Button m_connectBtn = null;
		private Button m_sendMsgBtn = null;

		private TextBox m_ipAddressTxt = null;
		private TextBox m_portTxt = null;
		private TextBox m_chatBox = null;
		private TextBox m_msgTxt = null;

		private string m_clientName = "Client";

		// List to store all sent and received messages.
		private List<ChatMessage> m_Messages;

		private UIHandler()
		{
			// Get current application window context.
			m_mainWindow = Application.Current.MainWindow as MainWindow;

			// Get reference to UI elements.
			m_appMode = eAppMode.None;
			m_appModeSelectionGrid = m_mainWindow.modeSelectionGrid;
			m_appModeBtnsGrid = m_mainWindow.appModeBtns;
			m_appModeInputControlsGrid = m_mainWindow.appModeInputControls;
			m_appModeSelectionLbl = m_mainWindow.modeSelectionLbl;
			m_serverModeBtn = m_mainWindow.startServerBtn;
			m_clientModeBtn = m_mainWindow.startClientBtn;
			m_findIPBtn = m_mainWindow.refreshIPBtn;
			m_connectBtn = m_mainWindow.connectBtn;
			m_sendMsgBtn = m_mainWindow.sendBtn;
			m_ipAddressTxt = m_mainWindow.ipAddressTxt;
			m_portTxt = m_mainWindow.portTxt;
			m_chatBox = m_mainWindow.chatBox;
			m_msgTxt = m_mainWindow.messageTxtBox;

			// Subscribe to UI events, button clicks, etc.
			m_serverModeBtn.Click += OnServerBtnClick;
			m_clientModeBtn.Click += OnClientBtnClick;
			m_connectBtn.Click += OnConnectBtnClick;
			m_sendMsgBtn.Click += OnSendBtnClick;
			m_msgTxt.KeyUp += OnMsgTxtBoxKeyUp;

			// Store this machine's IPV4 address.
			m_ipAddressTxt.Text = ResolveIPAddress(Dns.GetHostName()).ToString();
			m_portTxt.Text = "11000";

			m_Messages = new List<ChatMessage>();

		}

		private void OnServerBtnClick(object sender, RoutedEventArgs e)
		{
			// Set the application mode selected.
			m_appMode = eAppMode.Server;

			// Display appropriate input controls for this mode.
			DisplayAppModeBtns(false);
			DisplayAppModeInput(true);
			m_appModeSelectionLbl.Content = "Server Mode";
			m_connectBtn.Content = "Start";

			m_mainWindow.clientNameLbl.Visibility = Visibility.Hidden;
			m_mainWindow.clientNameTxt.Visibility = Visibility.Hidden;
			m_msgTxt.Visibility = Visibility.Hidden;
			m_sendMsgBtn.Visibility = Visibility.Hidden;

			m_mainWindow.chatBoxScroll.Margin = new Thickness(10);
			m_mainWindow.connectionsLbl.Content = "Connections: 0";
		}

		private void OnClientBtnClick(object sender, RoutedEventArgs e)
		{
			// Set the application mode selected.
			m_appMode = eAppMode.Client;

			// Display appropriate input controls for this mode.
			DisplayAppModeBtns(false);
			DisplayAppModeInput(true);
			m_appModeSelectionLbl.Content = "Client Mode";
			m_connectBtn.Content = "Connect";
		}

		private void OnConnectBtnClick(object sender, RoutedEventArgs e)
		{
			string remoteIPAddress = m_ipAddressTxt.Text;
			string portString = m_portTxt.Text;
			int remotePort = 0;

			IPAddress ipAddress = null;

			// Try parsing the IP address in string into IPAddress object
			if (IPAddress.TryParse(remoteIPAddress, out ipAddress))
			{
				// Get the port number as an integer from the string
				if (int.TryParse(portString, out remotePort))
				{
					// Check if the address is not empty and the port number is over 1024 to avoid binding to a reserved port.
					if (!string.IsNullOrEmpty(remoteIPAddress) && remotePort > 1024)
					{
						// Disable the Connect/Start button to avoid button spam.
						m_connectBtn.IsEnabled = false;

						try
						{
							if (m_appMode == eAppMode.Server)
							{
								// There should only be one instance of server or client per App unless there is a bug!
								if (m_server == null)
								{
									m_connectBtn.Content = "Starting...";
									// Create the server and send the IP and port we want to listen on.
									m_server = new Server(ResolveIPAddress(Dns.GetHostName()), remotePort);
									// Subscribe to server events; client connect/disconnect etc.
									SubscribeToEvents();
									// Start listening for connections
									m_server.StartListening();

									m_mainWindow.selfIPLbl.Content = m_server.localEndPoint.ToString();
									HideAppModeSelection();
								}
								else
								{
									m_server.StartListening();
									Console.WriteLine("Error: server already running!");
								}

							}
							else if (m_appMode == eAppMode.Client)
							{
								m_connectBtn.Content = "Connecting...";

								if (m_client == null)
								{
									// Store the client's name if there is one
									if (!string.IsNullOrEmpty(m_mainWindow.clientNameTxt.Text))
										m_clientName = m_mainWindow.clientNameTxt.Text;

									// Create the client and send the IP and Port we want to connect to.
									m_client = new Client(IPAddress.Parse(remoteIPAddress), remotePort);
									// Subscribe to client events; on connect, message received...
									SubscribeToEvents();
									// Finally connect
									m_client.Connect();
								}
								else
								{
									// If client already exists, then retry.
									m_client.Connect();
								}
							}
						}
						catch (Exception exp)
						{
							Console.WriteLine(exp.ToString());
							// Trying to bind multiple servers to same port
							// If not successful, enable retry button.
							Reconnect();
						}
					}
				}
				else
				{
					m_mainWindow.portLbl.Content = "Port number must be between 1024-65536";
				}
			}
			else
			{
				m_mainWindow.ipAddressLbl.Content = "Invalid IP address.";
			}
		}

		// Server - called when a client connects.
		// Client - called when client connects to a server.
		private void OnSocketConnect(Socket socket)
		{
			// For asynchronous socket we have to get the main thread so we can update the UI.
			// Maybe not the best way to handle events.
			m_mainWindow.Dispatcher.BeginInvoke(new Action(() =>
			{
				if (m_appMode == eAppMode.Server)
				{
					m_mainWindow.connectionsLbl.Content = "Connections: " + m_server.Connections.ToString();
					PushMessage(new ChatMessage("Connected", socket.RemoteEndPoint.ToString()), false);
				}
				else
				{
					m_mainWindow.selfIPLbl.Content = socket.RemoteEndPoint;
					HideAppModeSelection();
				}
			}));
		}

		// Server - called when a client disconnects.
		// Client - called when client disconnects from a server.
		private void OnSocketDisconnect()
		{
			// For asynchronous socket we have to get the main thread so we can update the UI.
			m_mainWindow.Dispatcher.BeginInvoke(new Action(() =>
			{
				if (m_appMode == eAppMode.Server)
				{

				}
				else
				{
					Reconnect();
				}
			}));
		}

		private void OnMessageSent()
		{
			// For asynchronous socket we have to get the main thread so we can update the UI.
			m_mainWindow.Dispatcher.BeginInvoke(new Action(() =>
			{
				if (m_appMode == eAppMode.Server)
				{

				}
				else
				{

				}
			}));
		}

		// Raised when a message is received.
		private void OnMessageReceive(string message)
		{
			m_mainWindow.Dispatcher.BeginInvoke(new Action(() =>
			{
				// Push it to our screen.
				PushMessage(new ChatMessage(message, ""), true);
			}));
		}

		private void OnMsgTxtBoxKeyUp(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter)
			{
				SendMessage();
			}
		}

		private void OnSendBtnClick(object sender, RoutedEventArgs e)
		{
			SendMessage();
		}

		private void SendMessage()
		{
			string message = m_msgTxt.Text;
			if (!string.IsNullOrEmpty(message))
			{
				// Only a client can input and send a message
				if (m_appMode == eAppMode.Client)
				{
					// Push the message to our screen 
					PushMessage(new ChatMessage(message, "Me"), false);
					// And also send it out the server
					m_client.Send(string.Concat((m_clientName + ":"), message));
				}

				// Clear the textbox
				m_msgTxt.Text = "";
			}
		}

		private void PushMessage(ChatMessage chatMessage, bool validateHammingCode)
		{
			// Although, we are not using the list for anything but add it anyway.
			m_Messages.Add(chatMessage);

			// Retrieve the message and its sender so we can do more processing.
			string sender = chatMessage.sender;
			string message = chatMessage.data;

			// Get the name of the person sent the message
			int index = message.IndexOf(":");
			if (index > 0)
			{
				sender = message.Substring(0, index);
				message = message.Remove(0, index + 1);
			}

			// Append it the chat textbox
			m_chatBox.AppendText(sender + ": " + message + "\n");
			if (validateHammingCode)
				m_chatBox.AppendText(ValidateHammingCode(message));

			// Scroll down to the bottom
			ScrollViewer sv = (ScrollViewer)m_chatBox.Parent;
			sv.ScrollToEnd();
		}

		// Miscellaneous functions below

		private string ValidateHammingCode(string hammingCode)
		{
			return HammingCodeHelper.CheckHammingCode(hammingCode);
		}

		private void DisplayAppModeBtns(bool display)
		{
			if (display) m_appModeBtnsGrid.Visibility = Visibility.Visible;
			else m_appModeBtnsGrid.Visibility = Visibility.Hidden;
		}

		private void DisplayAppModeInput(bool display)
		{
			if (display) m_appModeInputControlsGrid.Visibility = Visibility.Visible;
			else m_appModeInputControlsGrid.Visibility = Visibility.Hidden;
		}

		private void Reconnect()
		{
			m_connectBtn.IsEnabled = true;
			m_connectBtn.Content = "Retry";
			m_connectBtn.Background = Brushes.Red;
		}

		private void HideAppModeSelection()
		{
			m_appModeSelectionGrid.Visibility = Visibility.Hidden;
			m_mainWindow.appModeLbl.Content = m_appMode.ToString();
		}

		private IPAddress ResolveIPAddress(string hostAddress)
		{
			IPHostEntry ipHostEntry = Dns.GetHostEntry(hostAddress);

			// Find our IPV4 address
			foreach (IPAddress ip in ipHostEntry.AddressList)
			{
				if (ip.AddressFamily == AddressFamily.InterNetwork)
					return ip;
			}

			// Fallback to the the first available IP address if we didnt find the IPV4 address
			return ipHostEntry.AddressList[0];
		}

		// Subscribe and listen to these events
		private void SubscribeToEvents()
		{
			if (m_appMode == eAppMode.Server)
			{
				m_server.OnSocketConnect += OnSocketConnect;
				m_server.OnSocketDisconnect += OnSocketDisconnect;
				m_server.OnDataSent += OnMessageSent;
				m_server.OnDataRecieve += OnMessageReceive;
			}
			else
			{
				m_client.OnSocketConnect += OnSocketConnect;
				m_client.OnSocketDisconnect += OnSocketDisconnect;
				m_client.OnDataSent += OnMessageSent;
				m_client.OnDataRecieve += OnMessageReceive;
			}
		}

		public static UIHandler Instance { get { return m_instance; } }
	}
}
