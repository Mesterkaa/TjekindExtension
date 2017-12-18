using PCSC;
using PCSC.Iso7816;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace TjekindExtension
{
    public partial class Form1 : Form
    {
 
        string[] readerNames; 
        bool Running = true;
        public Form1()
        {
            InitializeComponent();           
        }
        void StartNFC()
        {
            var contextFactory = ContextFactory.Instance;
            using (var context = contextFactory.Establish(SCardScope.System))
            {   
                
                //Gets the connected SmartCard Readers and selects the first [0]. It runs two checks to see if a reader is plug in.
                try
                {
                    readerNames = context.GetReaders();
                }
                catch (PCSC.NoServiceException e)
                {
                    LogText(e.Message);
                    return;
                }               
                if (NoReaderFound(readerNames))
                {
                    this.LogText("You need at least one reader in order to run this program.");
                    return;
                }
                var readerName = readerNames[0];
                if (readerName == null)
                {
                    return;
                }


                // 'using' statement to make sure the reader will be disposed (disconnected) on exit
                using (var rfidReader = new SCardReader(context))
                {

                    //Connects to the SmartCard and chech if it is a succes
                    var sc = rfidReader.Connect(readerName, SCardShareMode.Shared, SCardProtocol.Any);
                    if (sc != SCardError.Success)
                    {
                        Thread.Sleep(1);

                        this.LogText("No card detected");
                        return;
                    }

                    //Some data used for transaction
                    var apdu = new CommandApdu(IsoCase.Case2Short, rfidReader.ActiveProtocol)
                    {
                        CLA = 0xFF,
                        Instruction = InstructionCode.GetData,
                        P1 = 0x00,
                        P2 = 0x00,
                        Le = 0 // We don't know the ID tag size
                    };
                    
                    //Begins Transaction to smartcard and check if it is succes
                    sc = rfidReader.BeginTransaction();
                    if (sc != SCardError.Success)
                    {
                        this.LogText("Could not begin transaction.");
                        return;
                    }

                    //
                    var receivePci = new SCardPCI(); // IO returned protocol control information.
                    var sendPci = SCardPCI.GetPci(rfidReader.ActiveProtocol);

                    var receiveBuffer = new byte[256];
                    var command = apdu.ToArray();

                    sc = rfidReader.Transmit(
                        sendPci, // Protocol Control Information (T0, T1 or Raw)
                        command, // command APDU
                        receivePci, // returning Protocol Control Information
                        ref receiveBuffer); // data buffer

                    if (sc != SCardError.Success)
                    {
                        this.LogText("Error: " + SCardHelper.StringifyError(sc));
                    }
                    var responseApdu = new ResponseApdu(receiveBuffer, IsoCase.Case2Short, rfidReader.ActiveProtocol);

                    this.LogText(responseApdu.HasData ? "Card detected (" + BitConverter.ToString(responseApdu.GetData()) + ")" : "No uid received");
                    if (responseApdu.HasData)
                    {                       
                        SendKeys.SendWait(BitConverter.ToString(responseApdu.GetData()) + "{ENTER}");
                    }
                    else
                    {
                        return;
                    }               
                    rfidReader.EndTransaction(SCardReaderDisposition.Leave);
                    rfidReader.Disconnect(SCardReaderDisposition.Reset);
                    Thread.Sleep(2000);
                }

            }
        }
        
        //Returns null if no readers is found
        private static bool NoReaderFound(ICollection<string> readerNames)
        {
            return readerNames == null || readerNames.Count < 1;
        }

        //When Form1 loads. It starts a thread, make it background so it closes with the program, and Minimizes the window
        private void Form1_Load(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
            Thread NFCThread = new Thread(NFCloop);
            NFCThread.IsBackground = true;
            NFCThread.Start();
        }

        //The loop that runs the NFC reader
        void NFCloop()
        {
            while (true)
            {
                while (Running)
                {
                    StartNFC();
                }
            }
        }

        //The button that turns on and off the Reader
        //If the program turns it self off, it because the reader outputs enter and hits enter on this button
        private void StartStop_Click(object sender, EventArgs e)
        {
            if (Running)
            {
                LogText("NFC reader turned off\r\n");
                Running = false;
            }
            else
            {
                LogText("NFC reader turned on\r\n");
                Running = true;
            }
        }

        //Checks if an error is not a repeat and sents it to SetText() . If it isn't check a error will repeat forever and ever
        string LastError;
        private void LogText(string text)
        {
            if (LastError == "NFC reader turned off")
            {
                if (text == "NFC reader turned on" || text == "NFC reader turned off")
                {
                    SetText(text);
                }
            }
            else if (text != LastError)
            {
                LastError = text;
                SetText(text);
            }
        }


        delegate void StringArgReturningVoidDelegate(string text);
        private void SetText(string text)
        {             
            if (this.LogBox.InvokeRequired)
            {
                StringArgReturningVoidDelegate d = new StringArgReturningVoidDelegate(SetText);
                this.Invoke(d, new object[] { text + "\r\n" });
            }
            else
            {
                this.LogBox.Text += text;
            }
        }
    }
}
