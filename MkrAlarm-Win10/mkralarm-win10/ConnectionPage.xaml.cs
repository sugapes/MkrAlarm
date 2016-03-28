using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Communication;
using Microsoft.Maker.Serial;
using Microsoft.Maker.Firmata;
using Microsoft.Maker.RemoteWiring;
using System.Collections.Generic;
using System.Diagnostics;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Media;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace mkralarm_win10
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ConnectionPage : Page
    {
        DispatcherTimer timeout;
        CancellationTokenSource cancelTokenSource;

        BitmapImage wireBitmap;
        Image wire;

        bool navigated = false;

        public ConnectionPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo( NavigationEventArgs e )
        {
            base.OnNavigatedTo( e );

            navigated = true;
            Reset();
            navigated = false;

            // Load assets for wire icon
            wireBitmap = new BitmapImage(new Uri(BaseUri, @"Assets/wire.png"));
            wire = new Image();
            wire.Stretch = Stretch.Uniform;
            wire.Source = wireBitmap;
            WireStack.Children.Add(wire);

            // Set default values for host address and port on text boxes
            NetworkHostNameTextBox.Text = "192.168.10.99";
            NetworkPortTextBox.Text = "3030";

            RefreshDeviceList();
        }

        private void RefreshDeviceList()
        {
            NetworkHostNameTextBox.IsEnabled = true;
            NetworkPortTextBox.IsEnabled = true;
            ConnectMessage.Text = "Enter a host and port to connect.";
        }

        /****************************************************************
         *                       UI Callbacks                           *
         ****************************************************************/

        /// <summary>
        /// Called if the Cancel button is pressed
        /// </summary>
        /// <param name="sender">The object invoking the event</param>
        /// <param name="e">Arguments relating to the event</param>
        private void CancelButton_Click( object sender, RoutedEventArgs e )
        {
            OnConnectionCancelled();
        }

        /// <summary>
        /// Called if the Connect button is pressed
        /// </summary>
        /// <param name="sender">The object invoking the event</param>
        /// <param name="e">Arguments relating to the event</param>
        private void ConnectButton_Click( object sender, RoutedEventArgs e )
        {
            //disable the buttons and set a timer in case the connection times out
            SetUiEnabled( false );
            
            //create our communication object
            string host = NetworkHostNameTextBox.Text;
            string port = NetworkPortTextBox.Text;
            ushort portnum = 0;

            if (Uri.CheckHostName(host) == UriHostNameType.Unknown)
            {
                ConnectMessage.Text = "You have entered an invalid host or IP.";
                return;
            }

            if (!ushort.TryParse(port, out portnum))
            {
                ConnectMessage.Text = "You have entered an invalid port number.";
                return;
            }

            App.Connection = new NetworkSerial( new Windows.Networking.HostName( host ), portnum );

            App.Firmata = new UwpFirmata();

            App.Arduino = new RemoteDevice( App.Firmata );
            App.Arduino.DeviceReady += OnDeviceReady;
            App.Arduino.DeviceConnectionFailed += OnConnectionFailed;

            //tell the connection to begin, the arguments (baud and serialconfig) don't matter for the NetworkSerial class
            App.Connection.begin( 57600, SerialConfig.SERIAL_8N1 );

            //attach the connection to the UwpFirmata object with begin()
            App.Firmata.begin( App.Connection );

            //start a timer for connection timeout
            timeout = new DispatcherTimer();
            timeout.Interval = new TimeSpan( 0, 0, 30 );
            timeout.Tick += Connection_TimeOut;
            timeout.Start();
        }


        /****************************************************************
         *                  Event callbacks                             *
         ****************************************************************/

        private void OnConnectionFailed( string message )
        {
            var action = Dispatcher.RunAsync( Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler( () =>
            {
                timeout.Stop();
                ConnectMessage.Text = "Connection attempt failed: " + message;

                Reset();
            } ) );
        }

        private void OnDeviceReady()
        {
            var action = Dispatcher.RunAsync( Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler( () =>
            {
                timeout.Stop();
                ConnectMessage.Text = "Successfully connected!";

                this.Frame.Navigate( typeof( MainPage ) );
            } ) );
        }

        private void Connection_TimeOut( object sender, object e )
        {
            var action = Dispatcher.RunAsync( Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler( () =>
            {
                timeout.Stop();
                ConnectMessage.Text = "Connection attempt timed out.";
                
                Reset();
            } ) );
        }

        /// <summary>
        /// This function is invoked if a cancellation is invoked for any reason on the connection task
        /// </summary>
        private void OnConnectionCancelled()
        {
            timeout.Stop();
            ConnectMessage.Text = "Connection attempt cancelled.";

            Reset();
        }

        /****************************************************************
         *                  Helper functions                            *
         ****************************************************************/

        private void SetUiEnabled( bool enabled )
        {
            ConnectButton.IsEnabled = enabled;
            CancelButton.IsEnabled = !enabled;
        }

        private void Reset()
        {
            if( App.Connection != null )
            {
                App.Connection.ConnectionEstablished -= OnDeviceReady;
                App.Connection.ConnectionFailed -= OnConnectionFailed;
                App.Connection.end();
            }

            if( cancelTokenSource != null )
            {
                cancelTokenSource.Dispose();
            }

            App.Connection = null;
            App.Arduino = null;
            cancelTokenSource = null;

            SetUiEnabled( true );
        }

        /****************************************************************
         *                       Menu Bar Callbacks                     *
         ****************************************************************/
        /// <summary>
        /// Called if the pointer hovers over the Digital button.
        /// </summary>
        /// <param name="sender">The object invoking the event</param>
        /// <param name="e">Arguments relating to the event</param>
        private void DigitalButton_Enter(object sender, RoutedEventArgs e)
        {
            DigitalRectangle.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Called if the pointer hovers over the Analog button.
        /// </summary>
        /// <param name="sender">The object invoking the event</param>
        /// <param name="e">Arguments relating to the event</param>
        private void AnalogButton_Enter(object sender, RoutedEventArgs e)
        {
            AnalogRectangle.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Called if the pointer hovers over the PWM button.
        /// </summary>
        /// <param name="sender">The object invoking the event</param>
        /// <param name="e">Arguments relating to the event</param>
        private void PWMButton_Enter(object sender, RoutedEventArgs e)
        {
            PWMRectangle.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Called if the pointer hovers over the About button.
        /// </summary>
        /// <param name="sender">The object invoking the event</param>
        /// <param name="e">Arguments relating to the event</param>
        private void AboutButton_Enter(object sender, RoutedEventArgs e)
        {
            AboutRectangle.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Called if the pointer exits the boundaries of any button.
        /// </summary>
        /// <param name="sender">The object invoking the event</param>
        /// <param name="e">Arguments relating to the event</param>
        private void Button_Exit(object sender, RoutedEventArgs e)
        {
            DigitalRectangle.Visibility = Visibility.Collapsed;
            AnalogRectangle.Visibility = Visibility.Collapsed;
            PWMRectangle.Visibility = Visibility.Collapsed;
            AboutRectangle.Visibility = Visibility.Collapsed;
        }
    }
}
