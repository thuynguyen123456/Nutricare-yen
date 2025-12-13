using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;
using System.Data.SqlClient;
using System.Globalization;
using System.Threading;

namespace NutricareQRcode
{
    public partial class Form1 : Form
    {
        public static int canNum=0;
        public static int carNum = 0;
        //TIJ
        public static byte ESC = 0x1b;
        public static byte STX = 0x02;
        public static byte ETX = 0x03;

        SerialPort P = new SerialPort(); // Khai báo 1 Object SerialPort mới.
        string InputData = String.Empty; // Khai báo string buff dùng cho hiển thị dữ liệu sau này.
        //Domino
        public AsyncCallback pfnCallBack;
        public Socket client;
        IAsyncResult m_asynResult;
        //Pack camera
        public AsyncCallback pfnCallBackPackCam;
        public Socket clientPackcam;
        IAsyncResult m_asynResultPackCam;
        //Carton camera 
        public AsyncCallback pfnCallBackCartonCam;
        public Socket clientCartoncam;
        IAsyncResult m_asynResultCartonCam;

        //Common
        delegate void SetTextCallback(string text); // Khai bao delegate SetTextCallBack voi tham so string

        private static string printedCartonCode;
        private static string printedcartonCodeID;
        private static string curCartonID;
        private static string printedPackcode;
        private static string printedPackcodeID;
        private static string curFillingLine = "1";
        private static string curBatchID;
        private static string curCommodityID;
        private static string curPackPerCarton;
        private static string readPackCode;
        private static string curCartonPerPallet;
        private static string curLocationID;
        private static string curQueueID;
        private static string lineVolume = "10800";
        private static string entryStatus = "6";

        private static bool duplicatefault;
        private static bool duplicatefaultCarton;

        private string strServer;
        private string strDatabase;
        private string strUsername;
        private string strPassDB;

        private string strDominoAdd;
        private string strDominoPort;

        private string strPackCamIP;
        private string strPackCamPort;

        private string strCartonCamIP;
        private string strCartonCamPort;

        public Form1()
        {
            InitializeComponent();
            ProgramInit();
        }

        private void LoadSettingFile()
        {
            string[] lines = System.IO.File.ReadAllLines(@"Setting.csv");
            if (lines.Length > 0)
            {
                //database
                strServer = lines[0].Split(',')[1];
                strDatabase = lines[1].Split(',')[1];
                strUsername = lines[2].Split(',')[1];
                strPassDB = lines[3].Split(',')[1];
                //Domino
                strDominoAdd = lines[4].Split(',')[1];
                strDominoPort = lines[5].Split(',')[1];
                //TIJ
                P.PortName = lines[6].Split(',')[1];
                P.BaudRate = Convert.ToInt32(lines[7].Split(',')[1]);
                P.DataBits = Convert.ToInt32(lines[8].Split(',')[1]);
                switch (lines[9].Split(',')[1])
                {
                    case "Odd":
                        P.Parity = Parity.Odd;
                        break;
                    case "None":
                        P.Parity = Parity.None;
                        break;
                    case "Even":
                        P.Parity = Parity.Even;
                        break;
                }
                switch (lines[10].Split(',')[1])
                {
                    case "1":
                        P.StopBits = StopBits.One;
                        break;
                    case "1.5":
                        P.StopBits = StopBits.OnePointFive;
                        break;

                    case "2":
                        P.StopBits = StopBits.Two;
                        break;
                }
                //Pack camera
                strPackCamIP = lines[11].Split(',')[1];
                strPackCamPort = lines[12].Split(',')[1];
                //Carton camera
                strCartonCamIP = lines[13].Split(',')[1];
                strCartonCamPort = lines[14].Split(',')[1];
            }
        }

        private void PackCartonInfor()
        {
            curBatchID = SQLConnection.GetCurBatchIDActive(curFillingLine);
            curCommodityID = SQLConnection.GetCurCommodiy(curFillingLine);
            curPackPerCarton = SQLConnection.GetPackPerCarton(curCommodityID);
            curCartonPerPallet = SQLConnection.GetCartonPerPallet(curCommodityID);
            curLocationID = SQLConnection.GetLocationID(curBatchID);
            curCartonID = SQLConnection.GetCurCartonID();
            curQueueID = "0";
            lineVolume = "900";
            entryStatus = "6";
        }

        private void ProgramInit()
        {
            LoadSettingFile();
            SetDateTime();
            SQLConnection.sqlServer = strServer;
            SQLConnection.sqlDatabase = strDatabase;
            SQLConnection.sqlUser = strUsername;
            SQLConnection.sqlPass = strPassDB;

            string[] fillingLine = { "1", "2", "3", "4", "5" };
            comboBoxFillingLine.Items.AddRange(fillingLine);
            Console.WriteLine("Program init: ");
        }

        private void SetDateTime()
        {
            textBoxNSX.Text = DateTime.Now.ToString("dd/MM/yyyy");
            int curYear = int.Parse(textBoxNSX.Text.Substring(6, 4));
            string nextYear = (curYear + 2).ToString();
            textBoxHSD.Text = textBoxNSX.Text.Replace(textBoxNSX.Text.Substring(6, 4), nextYear);
        }

        private void GetFirstPrintedData()
        {
            try
            {
                //Get printed carton code
                printedCartonCode = "http://nits.vn/" + SQLConnection.GetPrintedCartonCode();
                //get printed carton code ID
                printedcartonCodeID = SQLConnection.GetCurCartonCodeID();

                //Get the first data for Domino
                printedPackcode = "http://nits.vn/" + SQLConnection.GetPrintedPackCode();
                printedPackcodeID = SQLConnection.GetCurPackCodeID();
                //TransferDataToDonino(printedPackcode);
                Console.WriteLine("Get first printed data");
            }
            catch
            {
                MessageBox.Show("Hết mã code!");
            }
        }

        private void PackCamInit()
        {
            StartPackCamClient();
            waitForDataPackCam();
        }
        private void StartPackCamClient()
        {
            try
            {
                clientPackcam = new Socket(System.Net.Sockets.AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IPAddress ip = IPAddress.Parse(strPackCamIP);
                int port = int.Parse(strPackCamPort);
                int iPortNo = System.Convert.ToInt16(port);
                IPEndPoint ipEnd = new IPEndPoint(ip, iPortNo);
                clientPackcam.Connect(ipEnd);
            }
            catch (SocketException se)
            {
                MessageBox.Show(se.Message);
            }
        }

        public class cSocketPacketPackCam
        {
            public System.Net.Sockets.Socket thisSocket;
            public byte[] dataBuffer = new byte[96];
        }

        private string GetrDateTimeNow()
        {
            return DateTime.Now.ToString("yyyy-M-dd HH:mm:ss");
        }

        private string GetShortDateNow()
        {
            return DateTime.Now.ToString("yyyy-M-dd HH:mm:ss").Substring(0, 10);
        }

        public void onDataReceivedPackCam(IAsyncResult asyn)
        {
            try
            {
                cSocketPacketPackCam theSockID = (cSocketPacketPackCam)asyn.AsyncState;
                int iRx = 0;
                iRx = theSockID.thisSocket.EndReceive(asyn);
                char[] chars = new char[iRx + 1];
                System.Text.Decoder d = System.Text.Encoding.UTF8.GetDecoder();
                int charLen = d.GetChars(theSockID.dataBuffer, 0, iRx, chars, 0);
                System.String szData = new System.String(chars);

                if (szData != string.Empty && szData.Contains("http") && szData.Length > 5)
                {
                    string fullCode = szData.Substring(0, szData.Length - 1);

                    if (int.Parse(SQLConnection.CountPackCode(fullCode)) > 0)
                    {
                        duplicatefault = true;
                        send((char)0x1B + "Q1N" + (char)0x04);
                        buttonReset.BackColor = Color.Red;
                        MessageBox.Show("Lỗi đọc code lon trùng nhau!");
                        return;
                    }

                    this.Invoke(new Action(() =>
                    {
                        PackCartonInfor();
                        SQLConnection.InsertPack(GetrDateTimeNow(), curFillingLine, curBatchID, curLocationID, curQueueID, curCommodityID, fullCode, lineVolume, entryStatus);
                        UpDateCheckedPackDisplay(1);
                        textBoxPack.Text = fullCode;
                        canNum++;

                    }));
                }

                if (clientPackcam != null)
                {
                    waitForDataPackCam();
                }
            }
            catch (ObjectDisposedException)
            {
                MessageBox.Show("OnDataReceived: Socket has been closed");
            }
        }

        public void waitForDataPackCam()
        {
            if (clientPackcam != null)
            {
                try
                {
                    if (pfnCallBackPackCam == null)
                    {
                        pfnCallBackPackCam = new AsyncCallback(onDataReceivedPackCam);
                    }
                    cSocketPacketPackCam theSocPkt = new cSocketPacketPackCam();
                    theSocPkt.thisSocket = clientPackcam;
                    m_asynResultPackCam = clientPackcam.BeginReceive(theSocPkt.dataBuffer, 0, theSocPkt.dataBuffer.Length, SocketFlags.None, pfnCallBackPackCam, theSocPkt);
                }
                catch (SocketException se)
                {
                    MessageBox.Show(se.Message);
                }
            }
        }
       
        private void StopClientPackCam()
        {
            if (clientPackcam != null)
            {
                clientPackcam.Close();
                clientPackcam = null;
            }
        }
        /// <summary>
        /// /////////////////////////////////////////////////////////////Carton camera///////////////////////////////////////////////////////////////////////////
        /// </summary>
        private void CartonCamInit()
        {
            StartCartonCamClient();
            waitForDataCartonCam();
        }
        private void StartCartonCamClient()
        {
            try
            {
                clientCartoncam = new Socket(System.Net.Sockets.AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IPAddress ip = IPAddress.Parse(strCartonCamIP);
                int port = int.Parse(strCartonCamPort);
                int iPortNo = System.Convert.ToInt16(port);
                IPEndPoint ipEnd = new IPEndPoint(ip, iPortNo);
                clientCartoncam.Connect(ipEnd);
            }
            catch (SocketException se)
            {
                MessageBox.Show(se.Message);
            }
        }

        public class cSocketPacketCartonCam
        {
            public System.Net.Sockets.Socket thisSocket;
            public byte[] dataBuffer = new byte[96];
        }

        public void onDataReceivedCartonCam(IAsyncResult asyn)
        {
            try
            {
                cSocketPacketCartonCam theSockID = (cSocketPacketCartonCam)asyn.AsyncState;
                int iRx = 0;
                iRx = theSockID.thisSocket.EndReceive(asyn);
                char[] chars = new char[iRx + 1];
                System.Text.Decoder d = System.Text.Encoding.UTF8.GetDecoder();
                int charLen = d.GetChars(theSockID.dataBuffer, 0, iRx, chars, 0);
                System.String szData = new System.String(chars);
                if (szData.Length > 5 && !szData.Contains("Noread"))
                {

                    string fullCode = szData.Substring(0, szData.Length - 1);

                    if (int.Parse(SQLConnection.CountCartonCode(fullCode)) > 0)
                    {
                        duplicatefaultCarton = true;
                        StopPrintTIJ();
                        buttonReset.BackColor = Color.Red;
                        MessageBox.Show("Lỗi đọc code carton trùng nhau!");
                        return;
                    }

                    this.Invoke(new Action(() =>
                    {
                        carNum++;
                        labelNumCarton.Text = (carNum).ToString();
                        PackCartonInfor();
                        SQLConnection.InsertCarton(GetrDateTimeNow(), entryStatus, curPackPerCarton, lineVolume, "1", fullCode, "10350", curCommodityID, curLocationID, curBatchID, curFillingLine);
                        SQLConnection.UPdateCartonRowOfPackTbl(curPackPerCarton, SQLConnection.GetCartonIDWithCode(fullCode), GetShortDateNow(),curBatchID);
                        UpDateCheckedPackDisplay(2);
                        textBoxCarton.Text = szData;
                    }));
                }
                else if (szData.Length > 5 && szData.Contains("Noread"))
                {
                    this.Invoke(new Action(() =>
                    {
                        string temp = GetrDateTimeNow() + "XXXXXX";
                        PackCartonInfor();
                        SQLConnection.InsertCarton(GetrDateTimeNow(), "9", curPackPerCarton, lineVolume, "1", temp, "10350", curCommodityID, curLocationID, curBatchID, curFillingLine);
                        SQLConnection.UPdateCartonRowOfPackTbl(curPackPerCarton, SQLConnection.GetCartonIDWithCode(temp), GetShortDateNow(), curBatchID);
                        UpDateCheckedPackDisplay(2);
                        textBoxCarton.Text = szData;
                    }));
                }

                if (clientCartoncam != null)
                {
                    waitForDataCartonCam();
                }
            }
            catch (ObjectDisposedException)
            {
                MessageBox.Show("OnDataReceived: Socket has been closed");
            }
        }

        private void StopPrintTIJ()
        {
            try
            {
                byte[] data = MakeStopForm();
                P.Write(data, 0, data.Length);
            }
            catch
            {
                MessageBox.Show("can not stop");
            }
        }

        private byte[] MakeStopForm()
        {
            byte[] data = new byte[7];
            data[0] = 0x1b;
            data[1] = 0x02;
            data[2] = 0x00;
            data[3] = 0x12;
            data[4] = 0x1b;
            data[5] = 0x03;
            data[6] = 0xb3;
            return data;
        }

        private void StartPrintTIJ()
        {
            try
            {
                byte[] data = MakeStartForm();
                P.Write(data, 0, data.Length);
            }
            catch
            {
                MessageBox.Show("can not stop");
            }
        }

        private byte[] MakeStartForm()
        {
            byte[] data = new byte[7];
            data[0] = 0x1b;
            data[1] = 0x02;
            data[2] = 0x00;
            data[3] = 0x11;
            data[4] = 0x1b;
            data[5] = 0x03;
            data[6] = 0xb4;
            return data;
        }

        public void waitForDataCartonCam()
        {
            if (clientCartoncam != null)
            {
                try
                {
                    if (pfnCallBackCartonCam == null)
                    {
                        pfnCallBackCartonCam = new AsyncCallback(onDataReceivedCartonCam);
                    }
                    cSocketPacketCartonCam theSocPkt = new cSocketPacketCartonCam();
                    theSocPkt.thisSocket = clientCartoncam;
                    m_asynResultCartonCam = clientCartoncam.BeginReceive(theSocPkt.dataBuffer, 0, theSocPkt.dataBuffer.Length, SocketFlags.None, pfnCallBackCartonCam, theSocPkt);
                }
                catch (SocketException se)
                {
                    MessageBox.Show(se.Message);
                }
            }
        }
       
        private void StopClientCartonCam()
        {
            if (clientCartoncam != null)
            {
                clientCartoncam.Close();
                clientCartoncam = null;
            }
        }
        /// <summary>
        /// /////////////////////////////////////////////////////////////TIJ///////////////////////////////////////////////////////////////////////////
        /// </summary>
        private void TIJInit()
        {
            string[] ports = SerialPort.GetPortNames();

            // Thêm toàn bộ các COM đã tìm được vào combox cbCom
            comboBoxPort.Items.AddRange(ports); // Sử dụng AddRange thay vì dùng foreach
            P.ReadTimeout = 1000;

            P.DataReceived += new SerialDataReceivedEventHandler(DataReceive);

            // Cài đặt cho BaudRate
            string[] BaudRate = { "1200", "2400", "4800", "9600", "19200", "38400", "57600", "115200" };
            comboBoxBaud.Items.AddRange(BaudRate);

            // Cài đặt cho DataBits
            string[] Databits = { "6", "7", "8" };
            comboBoxDataSize.Items.AddRange(Databits);

            //Cho Parity
            string[] Parity = { "None", "Odd", "Even" };
            comboBoxParity.Items.AddRange(Parity);

            //Cho Stop bit
            string[] stopbit = { "1", "1.5", "2" };
            comboBoxStopbit.Items.AddRange(stopbit);
            if (!P.IsOpen)
            {
                P.Open();
            }
        }

        private void comboBoxPort_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (P.IsOpen)
            {
                P.Close(); // Nếu đang mở Port thì phải đóng lại
            }
            P.PortName = comboBoxPort.SelectedItem.ToString(); // Gán PortName bằng COM đã chọn 
        }

        private void comboBoxBaud_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (P.IsOpen)
            {
                P.Close();
            }
            P.BaudRate = Convert.ToInt32(comboBoxBaud.Text);
        }

        private void comboBoxDataSize_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (P.IsOpen)
            {
                P.Close();
            }
            P.DataBits = Convert.ToInt32(comboBoxDataSize.Text);
        }

        private void comboBoxParity_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (P.IsOpen)
            {
                P.Close();
            }
            // Với thằng Parity hơn lằng nhằng. Nhưng cũng OK thôi. ^_^
            switch (comboBoxParity.SelectedItem.ToString())
            {
                case "Odd":
                    P.Parity = Parity.Odd;
                    break;
                case "None":
                    P.Parity = Parity.None;
                    break;
                case "Even":
                    P.Parity = Parity.Even;
                    break;
            }
        }

        private void comboBoxStopbit_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (P.IsOpen)
            {
                P.Close();
            }
            switch (comboBoxStopbit.SelectedItem.ToString())
            {
                case "1":
                    P.StopBits = StopBits.One;
                    break;
                case "1.5":
                    P.StopBits = StopBits.OnePointFive;
                    break;

                case "2":
                    P.StopBits = StopBits.Two;
                    break;
            }
        }

        // Hàm này được sự kiện nhận dử liệu gọi đến. Mục đích để hiển thị thôi
        private void DataReceive(object obj, SerialDataReceivedEventArgs e)
        {
            InputData = P.ReadExisting();
            if (InputData != string.Empty)
            {
                //InputData = InputData.Substring(4, 1);

                Console.WriteLine(InputData);
                if (InputData.Contains("?") && !duplicatefault)
                {
                    try
                    {
                        this.Invoke(new Action(() =>
                        {
                        //Update the printed status 
                        SQLConnection.TurnOnPrintedCartonCode(int.Parse(printedcartonCodeID), 1, GetrDateTimeNow());

                        //Get printed carton code
                        printedCartonCode = "http://nits.vn/" + SQLConnection.GetPrintedCartonCode();
                        //get printed carton code ID
                        printedcartonCodeID = SQLConnection.GetCurCartonCodeID();
                        //Transfer data to TIJ
                        TransferDataToTIJ(printedCartonCode);
                            textBoxQRCodeTIJ.Text = printedCartonCode;
                        }));
                    }
                    catch
                    {
                        MessageBox.Show("Hết mã code thùng!");
                    }
                }
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            try
            {
                P.Open();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không kết nối được.", "Thử lại", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void buttonDisconnectRs232_Click(object sender, EventArgs e)
        {
            P.Close();
        }

        public byte[] insert_qr_code(byte id, string chuoi, string chuoi2, string chuoi3)
        {
            //mã lệnh, num feild, feild id, num off mes, data
            byte[] _string = Encoding.UTF8.GetBytes(chuoi);
            byte[] _string2 = Encoding.UTF8.GetBytes(chuoi2);
            byte[] _string3 = Encoding.UTF8.GetBytes(chuoi3);
            byte[] data = new byte[chuoi.Length + 4 + chuoi2.Length + 2 + chuoi3.Length + 2];


            data[0] = 0x1d; data[1] = 0x03; data[2] = 0x01; data[3] = (byte)chuoi.Length;
            for (int i = 0; i < chuoi.Length; i++)
            {
                data[i + 4] = _string[i];

            }

            data[chuoi.Length - 1 + 4 + 1] = 0x02;
            data[chuoi.Length + 5] = (byte)chuoi2.Length;
            for (int i = chuoi.Length + 6; i < chuoi.Length + 4 + chuoi2.Length + 2; i++)
            {
                data[i] = _string2[i - chuoi.Length - 6];
            }

            data[chuoi.Length + 4 + chuoi2.Length + 2] = 0x03;
            Console.WriteLine(data[chuoi.Length + 4 + chuoi2.Length + 2]);
            data[chuoi.Length + 4 + chuoi2.Length + 2 + 1] = (byte)chuoi3.Length;
            Console.WriteLine(data[chuoi.Length + 4 + chuoi2.Length + 2 + 1]);
            for (int i = chuoi.Length + 4 + chuoi2.Length + 2 + 1 + 1; i < chuoi.Length + 4 + chuoi2.Length + 2 + 1 + 1 + chuoi3.Length; i++)
            {
                //Console.WriteLine("test: " + (i - (chuoi.Length + 4 + chuoi2.Length + 2 + 1 + 1)).ToString());
                data[i] = _string3[i - (chuoi.Length + 4 + chuoi2.Length + 2 + 1 + 1)];
                Console.WriteLine(data[i]);
            }
            byte[] new_data = data_cmd(id, data);
            return new_data;
        }



        private byte[] data_cmd(int id, byte[] cmd)
        {
            byte[] data = new byte[6 + cmd.Length];
            data[0] = ESC; data[1] = STX; data[2] = toID(id);
            for (int i = 0; i < cmd.Length; i++)
            {
                data[i + 3] = cmd[i];
            }
            data[cmd.Length + 3] = ESC; data[cmd.Length + 4] = ETX; data[cmd.Length + 5] = getsum(data);
            Console.WriteLine(data[cmd.Length + 3] + "," + data[cmd.Length + 4] + "," + data[cmd.Length + 5]);
            return data;
        }

        private byte toID(int id)
        {
            return (byte)id;
        }

        byte getsum(byte[] data)
        {
            int sum = 0;
            for (int i = 0; i < data.Length; i++)
            {
                sum = sum + data[i];
            }

            if (sum > 256)
            {
                int var = sum / 256;
                int out_data = sum - (var * 256);
                return (byte)(256 - out_data);
            }
            return (byte)(256 - sum);
        }

        private void TransferDataToTIJ(string strTransfer)
        {
            if (textBoxNSX.Text.Length == 10)
            {
                byte[] data = insert_qr_code(0, strTransfer, strTransfer.Substring(15, 12), textBoxNSX.Text + "-" + textBoxLotNum.Text.Substring(0, 9));
                for (int i = 0; i < data.Length; i++)
                {
                    Console.Write(data[i] + ",");
                }
                P.Write(data, 0, data.Length);
            }
        }
        ///
        ////////////////////////////////////////////////////////Domino/////////////////////////////////////////////////////////
        ///
        private void DominoInit()
        {
            StartClient();
            waitForData();
        }
        private void StartClient()
        {
            try
            {
                client = new Socket(System.Net.Sockets.AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IPAddress ip = IPAddress.Parse(strDominoAdd);
                int port = int.Parse(strDominoPort);
                int iPortNo = System.Convert.ToInt16(port);
                IPEndPoint ipEnd = new IPEndPoint(ip, iPortNo);
                client.Connect(ipEnd);
            }
            catch (SocketException se)
            {
                MessageBox.Show(se.Message);
            }
        }

        public class cSocketPacket
        {
            public System.Net.Sockets.Socket thisSocket;
            public byte[] dataBuffer = new byte[96];
        }

        public void onDataReceived(IAsyncResult asyn)
        {
            try
            {
                cSocketPacket theSockID = (cSocketPacket)asyn.AsyncState;
                int iRx = 0;
                iRx = theSockID.thisSocket.EndReceive(asyn);
                char[] chars = new char[iRx + 1];
                System.Text.Decoder d = System.Text.Encoding.UTF8.GetDecoder();
                int charLen = d.GetChars(theSockID.dataBuffer, 0, iRx, chars, 0);
                System.String szData = new System.String(chars);

                if (szData.Contains("T1"))
                {
                    this.Invoke(new Action(() =>
                    {
                        if (labelProNum.Text != szData.Substring(3, 10))
                            labelProNum.Text = szData.Substring(3, 10);
                    }));
                }
                if (client != null)
                {
                    waitForData();
                }
            }
            catch (ObjectDisposedException)
            {
                MessageBox.Show("OnDataReceived: Socket has been closed");
            }
        }

        public void waitForData()
        {
            if (client != null)
            {
                try
                {
                    if (pfnCallBack == null)
                    {
                        pfnCallBack = new AsyncCallback(onDataReceived);
                    }
                    cSocketPacket theSocPkt = new cSocketPacket();
                    theSocPkt.thisSocket = client;
                    m_asynResult = client.BeginReceive(theSocPkt.dataBuffer, 0, theSocPkt.dataBuffer.Length, SocketFlags.None, pfnCallBack, theSocPkt);
                }
                catch (SocketException se)
                {
                    MessageBox.Show(se.Message);
                }
            }
        }
        private void send(string data)
        {
            if (client != null)
            {
                try
                {
                    Object objDta = data;

                    byte[] byData = System.Text.Encoding.ASCII.GetBytes(objDta.ToString());

                    client.Send(byData);

                }
                catch (SocketException)
                {
                    MessageBox.Show("Loi Socket");
                }
            }
        }
        private void stopClient()
        {
            if (client != null)
            {
                client.Close();
                client = null;
            }
        }

        private void buttonConnect_Click(object sender, EventArgs e)
        {
            StartClient();
        }

        private void buttonDisconnect_Click(object sender, EventArgs e)
        {
            stopClient();
        }

        private void TransferDataToDonino(string dominoCode)
        {
            string dataQRCode = dominoCode;
            string dataLotNum = textBoxLotNum.Text;
            string NSX = textBoxNSX.Text;
            string HSD = textBoxHSD.Text;

            if (dataLotNum.Length == 14)
            {
                dataLotNum += "   ";
            }
            else if (dataLotNum.Length == 15)
            {
                dataLotNum += "  ";
            }
            else if (dataLotNum.Length == 16)
            {
                dataLotNum += " ";
            }

            //string data = dataQRCode + NSX + HSD + dataLotNum +dataQRCode.Substring(14,13);
            string data = dataQRCode + NSX + HSD + dataLotNum;
            int len = data.Length;
            Console.WriteLine("QR: " + dataQRCode);
            Console.WriteLine("NSX: " + NSX);
            Console.WriteLine("Lot: " + dataLotNum);
            Console.WriteLine("dong 4: " + dataQRCode.Substring(14, 13));
            //
            send((char)0x1B + "OE" + data.Length.ToString("0000") + data + (char)0x04);
        }

        private void buttonStart_Click(object sender, EventArgs e)
        {
            if (curFillingLine == "0")
                return;
            TIJInit();
            GetFirstPrintedData();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            send((char)0x1B + "T1?" + (char)0x04);
        }

        private void timerCheckProNum_Tick(object sender, EventArgs e)
        {
            if (client != null)
            {
                send((char)0x1B + "T1?" + (char)0x04);
            }
        }

        private void labelProNum_TextChanged(object sender, EventArgs e)
        {
            if (duplicatefault)
            {
                return;
            }
            ChangeThePrinterCode();
            ChangeThePrinterCode();
        }

        private void ChangeThePrinterCode()
        {
            try
            {
                SQLConnection.TurnOnPrintedPackCode(int.Parse(printedPackcodeID), 1, GetrDateTimeNow());

                printedPackcode = "http://nits.vn/" + SQLConnection.GetPrintedPackCode();
                printedPackcodeID = SQLConnection.GetCurPackCodeID();

                SQLConnection.TurnOnPrintedPackCode(int.Parse(printedPackcodeID), 1, GetrDateTimeNow());
                TransferDataToDonino(printedPackcode);
                textBoxQRCodeDomino.Text = printedPackcode;
            }
            catch
            {
                MessageBox.Show("Kiem tra phan mem tao code in!");
            }
        }

        private void buttonClearBuff_Click(object sender, EventArgs e)
        {
            //send((char)0x1B+"}J"+ "1"+ (char)0x04);
            send((char)0x1B + "OE00002" + (char)0x04);
        }

        private void comboBoxFillingLine_SelectedIndexChanged(object sender, EventArgs e)
        {
            curFillingLine = comboBoxFillingLine.SelectedItem.ToString();
        }
        //Simulation

        private void UpDateCheckedPackDisplay(int id)
        {
            PackCartonInfor();

            dataGridViewPackWaitPack.DataSource = SQLConnection.GetDataTable("select Code from Packs where CartonID is NULL and BatchID = '" + curBatchID + "' and Code not in (select Top(" + curPackPerCarton + ") Code from Packs where CartonID is null  and BatchID = '" + curBatchID + "' )");
            dataGridViewCheckedPack.DataSource = SQLConnection.GetDataTable("select Top(" + curPackPerCarton + ") Code from Packs where CartonID is NULL and BatchID = '" + curBatchID + "'");
            dataGridViewCheckedCarton.DataSource = SQLConnection.GetDataTable("select Code from Cartons where BatchID = '" + curBatchID + "' and Code not like '%XXXXXX%' and Code not like '%UNUSED%'");
            dataGridViewFailCarton.DataSource = SQLConnection.GetDataTable("select Code from Cartons where BatchID = '" + curBatchID + "' and Code like '%XXXXXX%' ");

            labelNumCan.Text = (dataGridViewCheckedPack.Rows.Count - 1).ToString();
            labelNumCan2.Text = (dataGridViewCheckedPack.Rows.Count - 1).ToString();
            labelNumWaitingCan.Text = (dataGridViewPackWaitPack.Rows.Count - 1).ToString();
            labelNumWaitingCan2.Text = (dataGridViewPackWaitPack.Rows.Count - 1).ToString();

            labelNumFailCarton.Text = (dataGridViewFailCarton.Rows.Count-1).ToString();
            labelNumFailCarton2.Text = (dataGridViewFailCarton.Rows.Count - 1).ToString();
            labelNumCarton.Text = (dataGridViewCheckedCarton.Rows.Count-1).ToString();
            labelNumCarton2.Text = (dataGridViewCheckedCarton.Rows.Count-1).ToString();

            labelPalletNum.Text = ((dataGridViewCheckedCarton.Rows.Count - 1) / int.Parse(curCartonPerPallet)).ToString();

            for (int i=0;i< dataGridViewPackWaitPack.Rows.Count-1;i++)
            {
                dataGridViewPackWaitPack.Rows[i].Cells[0].Value = i + 1;
            }
            for (int i = 0; i < dataGridViewCheckedPack.Rows.Count - 1; i++)
            {
                dataGridViewCheckedPack.Rows[i].Cells[0].Value = i + 1;
            }
            for (int i = 0; i < dataGridViewCheckedCarton.Rows.Count - 1; i++)
            {
                dataGridViewCheckedCarton.Rows[i].Cells[0].Value = i + 1;
            }
            for (int i = 0; i < dataGridViewFailCarton.Rows.Count - 1; i++)
            {
                dataGridViewFailCarton.Rows[i].Cells[0].Value = i + 1;
            }
        }

        private void button2_Click_1(object sender, EventArgs e)
        {
            UpDateCheckedPackDisplay(1);
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (textBoxLotNum.Text.Length < 10)
            {
                MessageBox.Show("Kiểm tra lại số LOT!");
                return;
            }

            PackCartonInfor();
            TIJInit();
            DominoInit();
            GetFirstPrintedData();
            UpDateCheckedPackDisplay(2);
            PackCamInit();
            CartonCamInit();
            timerCheckDominostate.Enabled = true;

            if (P.IsOpen && client != null && clientCartoncam != null && clientPackcam != null)
            {
                btnConnect.Enabled = false;
                buttonDisCon.Enabled = true;
            }
        }

        private void textBoxNSX_TextChanged_1(object sender, EventArgs e)
        {
            if (textBoxNSX.Text.Length == 10)
            {
                try
                {
                    int curYear = int.Parse(textBoxNSX.Text.Substring(6, 4));
                    string nextYear = (curYear + 2).ToString();
                    textBoxHSD.Text = textBoxNSX.Text.Replace(textBoxNSX.Text.Substring(6, 4), nextYear);
                }
                catch
                {

                }
            }
        }

        private void buttonFailCarton_Click(object sender, EventArgs e)
        {
            SQLConnection.SetNullPacksAtCartonID(SQLConnection.GetCartonIDWithCode(labelCellInfor.Text));
            SQLConnection.ChangeCartonToUnused(labelCellInfor.Text);
            UpDateCheckedPackDisplay(2);
        }

        private void buttonDeleteFailCartonAndCan_Click(object sender, EventArgs e)
        {
            SQLConnection.DeleteRowsViaCartonID(SQLConnection.GetCartonIDWithCode(labelCellInfor.Text));
            SQLConnection.ChangeCartonToUnused(labelCellInfor.Text);
            UpDateCheckedPackDisplay(1);
        }

        private void button2_Click_2(object sender, EventArgs e)
        {
            P.Close();
            stopClient();
            StopClientCartonCam();
            StopClientPackCam();

            if (!P.IsOpen && client == null && clientCartoncam == null && clientPackcam == null)
            {
                btnConnect.Enabled = true;
                buttonDisCon.Enabled = false;
            }
        }

        private void buttonDeleteCarton_Click_1(object sender, EventArgs e)
        {
            SQLConnection.SetNullPacksAtCartonID(SQLConnection.GetCartonIDWithCode(labelCellInfor.Text));
            SQLConnection.ChangeCartonToUnused(labelCellInfor.Text);
            UpDateCheckedPackDisplay(1);
        }

        private void buttonDeleteWaitingCan_Click(object sender, EventArgs e)
        {
            SQLConnection.DeleteRowsViaPackcode(labelCellInfor.Text);
            UpDateCheckedPackDisplay(2);
        }

        private void buttonDeletecan_Click(object sender, EventArgs e)
        {
            SQLConnection.DeleteRowsViaPackcode(labelCellInfor.Text);
            UpDateCheckedPackDisplay(2);
        }

        private void button2_Click_3(object sender, EventArgs e)
        {
            ChangeThePrinterCode();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            send((char)0x1B + "N1" + (char)0x04);
        }

        private void timerCheckDominostate_Tick(object sender, EventArgs e)
        {
            if(labelProNum.Text=="label")
            {
                MessageBox.Show("Reset connection cho máy in, rồi kết nối lại phần mềm!");
            }
            timerCheckDominostate.Enabled = false;
        }

        private void buttonReset_Click(object sender, EventArgs e)
        {
            if (duplicatefaultCarton)
            {
                StartPrintTIJ();
                duplicatefaultCarton = false;
            }
            else if (duplicatefault)
            {
                send((char)0x1B + "Q1Y" + (char)0x04);
                duplicatefault = false;
            }
            
            buttonReset.BackColor = Color.Green;
            waitForDataPackCam();
            waitForDataCartonCam();
            GetFirstPrintedData();
        }

        private void dataGridViewFailCarton_CellClick_1(object sender, DataGridViewCellEventArgs e)
        {
            int numrow;
            numrow = e.RowIndex;

            if (dataGridViewFailCarton.Rows[numrow].Cells[1].Value == null)
            {
                return;
            }
            labelCellInfor.Text = dataGridViewFailCarton.Rows[numrow].Cells[1].Value.ToString();
        }

        private void dataGridViewCheckedCarton_CellClick_1(object sender, DataGridViewCellEventArgs e)
        {
            int numrow;
            numrow = e.RowIndex;

            if (dataGridViewCheckedCarton.Rows[numrow].Cells[1].Value == null)
            {
                return;
            }
            labelCellInfor.Text = dataGridViewCheckedCarton.Rows[numrow].Cells[1].Value.ToString();
        }

        private void dataGridViewCheckedPack_CellClick_1(object sender, DataGridViewCellEventArgs e)
        {
            int numrow;
            numrow = e.RowIndex;

            if (dataGridViewCheckedPack.Rows[numrow].Cells[1].Value == null)
            {
                return;
            }
            labelCellInfor.Text = dataGridViewCheckedPack.Rows[numrow].Cells[1].Value.ToString();
        }
    }
}
