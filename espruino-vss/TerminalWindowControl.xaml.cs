namespace espruino_vss
{
    using EnvDTE;
    using EnvDTE80;
    using System.Diagnostics.CodeAnalysis;
    using System.IO.Ports;
    using System.Text.RegularExpressions;
    using System.Windows;
    using System.Windows.Controls;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Interaction logic for TerminalWindowControl.
    /// </summary>
    public partial class TerminalWindowControl : UserControl
    {
        private Dictionary<string, string> modulesCache = new Dictionary<string, string>();

        private EnvDTE.Window currentWindow;
        SerialPort serialPort = new SerialPort() { Handshake = Handshake.None, DataBits = 8, Parity = Parity.None, StopBits = StopBits.One };
        /// <summary>
        /// Initializes a new instance of the <see cref="TerminalWindowControl"/> class.
        /// </summary>
        public TerminalWindowControl()
        {
            this.InitializeComponent();

            serialPort.DataReceived += SerialPort_DataReceived;

            espruinovss.Instance.Dte.Events.WindowEvents.WindowActivated += WindowEvents_WindowActivated;

            string[] ports = SerialPort.GetPortNames();
            cmbPorts.ItemsSource = ports;
            if (ports != null && ports.Length > 0)
                cmbPorts.SelectedValue = ports[0];

        }

        private void WindowEvents_WindowActivated(EnvDTE.Window GotFocus, EnvDTE.Window LostFocus)
        {
            if (GotFocus?.Document != null)
                currentWindow = GotFocus;
        }


        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {

            byte[] buffer = new byte[serialPort.BytesToRead];
            serialPort.Read(buffer, 0, buffer.Length);

            var data = System.Text.Encoding.UTF8.GetString(buffer);
            //if (data.StartsWith("=") && data.EndsWith(">\n"))
            {
                Dispatcher.BeginInvoke((Action)(() =>
                {
                    txtTerminal.AppendText(data);

                    txtTerminal.ScrollToEnd();

                }));
            }

            
        }

        private void ports_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            string[] ports = SerialPort.GetPortNames();
            cmbPorts.ItemsSource = ports;
        }

        private void Reconnect_Clicked(object sender, RoutedEventArgs e)
        {
            try
            {
                var port = cmbPorts.SelectedValue.ToString();
                var rate = int.Parse(cmbBaudRate.SelectedValue.ToString());


                serialPort.Close();

                serialPort.PortName = port;
                serialPort.BaudRate = rate;

                serialPort.Open();

                SendEnd("process.env");
            }
            catch (System.Exception ex)
            {

                AppentResult(ex.ToString());
            }
        }

        private void txtTerminal_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {

        }

        private void Send(string data)
        {
            AppentResult(data);

            serialPort.Write(data);
        }

        private void SendEnd(string data = null)
        {
            if (data != null) Send(data);
            Send("\n");
        }

        private void AppentResult(string data)
        {
            txtTerminal.AppendText(data + "\r\n");
            txtTerminal.ScrollToEnd();
        }

        private void SendCode(bool reset = false)
        {

            try
            {
                if (currentWindow?.Document?.Language == "TypeScript")
                {
                    if (reset)
                    {
                        SendEnd("reset()");

                        System.Threading.Thread.Sleep(100);
                    }

                    string code = null;
                    TextDocument document = (TextDocument)currentWindow?.Document.Object(String.Empty);
                    if (document == null) return;
                    if (document.Selection != null && !document.Selection.IsEmpty)
                    {
                        code = document.Selection.Text;
                    }
                    else
                    {
                        var objEditPt = document.StartPoint.CreateEditPoint();
                        code = objEditPt.GetText(document.EndPoint);
                    }

                    var requires = Regex.Matches(code, "require\\(\"(.*)\"\\)", RegexOptions.Compiled);
                    foreach (Match item in requires)
                    {
                        LoadModule(item.Groups[1].Value);
                    }

                    SendEnd(code);

                    AppentResult("Code loaded");
                }
            }
            catch (Exception ex)
            {

                AppentResult(ex.Message);
            }
        }

        private void LoadModule(string moduleName)
        {

            string code;

            if (!modulesCache.TryGetValue(moduleName, out code))
            {
                var url = $"http://www.espruino.com/modules/{moduleName}.min.js";
                code = new System.Net.WebClient().DownloadString(url);
                modulesCache.Add(moduleName, code);
            }

            Send("echo(0);");
            Send($"Modules.addCached(\"{moduleName}\", \"{code}\");");
            SendEnd("echo(1);");

            AppentResult($"Module {moduleName} loaded");
        }

        private void sendToBoard_Clicked(object sender, RoutedEventArgs e)
        {
            SendCode();
        }

        private void Disconnect_Clicked(object sender, RoutedEventArgs e)
        {
            serialPort.Close();
        }

        private void sendToBoardReset_Clicked(object sender, RoutedEventArgs e)
        {
            SendCode(true);
        }

        private void sendReset_Clicked(object sender, RoutedEventArgs e)
        {
            SendEnd("reset()");
        }
    }
}