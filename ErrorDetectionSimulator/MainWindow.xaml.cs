using System;
using System.Windows;
using System.Windows.Input;
using ErrorDetectionSimulator.source;

namespace ErrorDetectionSimulator
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
        }

		private void Window_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Keyboard.ClearFocus();
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			// Initalise our UI Handler singleton
			UIHandler uiHandler = UIHandler.Instance;
		}
	}
}
