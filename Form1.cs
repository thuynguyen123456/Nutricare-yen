using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NutricareQRcode
{
    public partial class Form1 : Form
    {
        public static int canNum = 0;
        public static int carNum = 0;

        private bool onlyCarton;

        // --- MÁY IN SERIAL BUFFER MỚI ---
        public SerialPort packPrinterPort;
        public SerialPort cartonPrinterPort;

        private string strPackPrinterPortName;
        private string strPackPrinterBaud;
        private string strCartonPrinterPortName;
        private string strCartonPrinterBaud;

        public const byte ESC = 0x1B;
        public const byte PRINTER_COMMAND_START = 0x53; // 'S'
        public const byte PRINTER_COMMAND_FUNCTION = 0x31; // '1'
        public const byte PRINTER_RESPONSE_OK_START = 0x4F; // 'O'
        public const byte PRINTER_RESPONSE_OK_END = 0x35; // '5'

        // --- CAMERA SOCKETS (GIỮ NGUYÊN) ---
        public AsyncCallback pfnCallBackPackCam;
        public Socket clientPackcam;
        IAsyncResult m_asynResultPackCam;
        public AsyncCallback pfnCallBackCartonCam;
        public Socket clientCartoncam;
        IAsyncResult m_asynResultCartonCam;

        private bool packCamEnabled = false;
        private bool cartonCamEnabled = false;

        // --- CÁC BIẾN DATA VÀ CONFIG CẦN THIẾT ---
        delegate void SetTextCallback(string text);

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
        private static string lineVolume = "900";
        private static string entryStatus = "6";

        private static bool duplicatefault;
        private static bool duplicatefaultCarton;

        private string strServer;
        private string strDatabase;
        private string strUsername;
        private string strPassDB;

        private string strPackCamIP;
        private string strPackCamPort;

        private string strCartonCamIP;
        private string strCartonCamPort;

        private int packCodeCount;
        private int cartonCodeCount;

        // ĐÃ XÓA: SerialPort P, InputData, client, pfnCallBack, strDominoAdd, strDominoPort, m_asynResult

        public Form1()
        {
            InitializeComponent();


            packPrinterPort = new SerialPort();

            cartonPrinterPort = new SerialPort();

            LoadSettingFile();
            ProgramInit();
        }

        private void LoadSettingFile()
        {
            string[] lines = System.IO.File.ReadAllLines(@"Setting.csv");
            if (lines.Length > 0)
            {
                // 0-3: Database
                strServer = lines[0].Split(',')[1];
                strDatabase = lines[1].Split(',')[1];
                strUsername = lines[2].Split(',')[1];
                strPassDB = lines[3].Split(',')[1];

                // 4 & 5: PACK PRINTER NEW SETTINGS
                strPackPrinterPortName = lines[4].Split(',')[1];
                strPackPrinterBaud = lines[5].Split(',')[1];

                // 6 & 7: CARTON PRINTER NEW SETTINGS
                strCartonPrinterPortName = lines[6].Split(',')[1];
                strCartonPrinterBaud = lines[7].Split(',')[1];

                // 8, 9, 10: DataBits, Parity, StopBits
                cartonPrinterPort.PortName = strCartonPrinterPortName;
                cartonPrinterPort.BaudRate = Convert.ToInt32(strCartonPrinterBaud);

                cartonPrinterPort.DataBits = Convert.ToInt32(lines[8].Split(',')[1]);
                switch (lines[9].Split(',')[1])
                {
                    case "Odd": cartonPrinterPort.Parity = Parity.Odd; break;
                    case "None": cartonPrinterPort.Parity = Parity.None; break;
                    case "Even": cartonPrinterPort.Parity = Parity.Even; break;
                }
                switch (lines[10].Split(',')[1])
                {
                    case "1": cartonPrinterPort.StopBits = StopBits.One; break;
                    case "1.5": cartonPrinterPort.StopBits = StopBits.OnePointFive; break;
                    case "2": cartonPrinterPort.StopBits = StopBits.Two; break;
                }

                packPrinterPort.PortName = strPackPrinterPortName;
                packPrinterPort.BaudRate = Convert.ToInt32(strPackPrinterBaud);

                packPrinterPort.DataBits = cartonPrinterPort.DataBits;
                packPrinterPort.Parity = cartonPrinterPort.Parity;
                packPrinterPort.StopBits = cartonPrinterPort.StopBits;

                // 11-14: Cấu hình Camera và Enable Flag
                string[] packIpLine = lines[11].Split(',');
                strPackCamIP = packIpLine[1];
                strPackCamPort = lines[12].Split(',')[1];
                if (packIpLine.Length > 2)
                {
                    packCamEnabled = (packIpLine[2].Trim() == "1");
                }
                else
                {
                    packCamEnabled = true;
                }

                string[] cartonCamIpLine = lines[13].Split(',');
                strCartonCamIP = cartonCamIpLine[1];
                strCartonCamPort = lines[14].Split(',')[1];
                if (cartonCamIpLine.Length > 2)
                {
                    cartonCamEnabled = (cartonCamIpLine[2].Trim() == "1");
                }
                else
                {
                    cartonCamEnabled = true;
                }

                string[] onlyCartonModeStr = lines[15].Split(',');
                if (onlyCartonModeStr[1] == "1")
                {
                    onlyCarton = true;
                }
                else
                {
                    onlyCarton = false;
                }
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

            SetDateTime();
            SQLConnection.sqlServer = strServer;
            SQLConnection.sqlDatabase = strDatabase;
            SQLConnection.sqlUser = strUsername;
            SQLConnection.sqlPass = strPassDB;
            packCodeCount = 0;
            cartonCodeCount = 0;

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
                printedCartonCode = "http://nits.vn/" + SQLConnection.GetPrintedCartonCode();
                printedcartonCodeID = SQLConnection.GetCurCartonCodeID();
                printedPackcode = "http://nits.vn/" + SQLConnection.GetPrintedPackCode();
                printedPackcodeID = SQLConnection.GetCurPackCodeID();
                Console.WriteLine("Get first printed data");
            }
            catch
            {
                MessageBox.Show("Hết mã code!");
            }
        }

        // --- LOGIC MÁY IN SERIAL BUFFER MỚI ---

        // Thêm vào phần khai báo biến toàn cục (Global Variables)
        private List<byte> packReceivedBuffer = new List<byte>();
        private List<byte> cartonReceivedBuffer = new List<byte>();


        private void NewProtocol_ClearCache(SerialPort printer)
        {
            if (printer == null || !printer.IsOpen) return;
            try
            {
                // Dọn dẹp List buffer tương ứng
                if (printer == packPrinterPort) packReceivedBuffer.Clear();
                if (printer == cartonPrinterPort) cartonReceivedBuffer.Clear();

                byte[] clearCacheCommand = new byte[] { ESC, PRINTER_COMMAND_START, 0x37, 0x37, 0xDC, 0x0D, 0x0A };
                printer.Write(clearCacheCommand, 0, clearCacheCommand.Length);
            }
            catch { }
        }
        private void NewProtocol_PrintSingleField(SerialPort printer, string content)
        {
            if (printer == null || !printer.IsOpen) return;

            byte[] contentBytes = Encoding.ASCII.GetBytes(content);
            byte[] message = new byte[contentBytes.Length + 7];

            message[0] = ESC;
            message[1] = PRINTER_COMMAND_START;
            message[2] = PRINTER_COMMAND_FUNCTION;
            message[3] = PRINTER_COMMAND_FUNCTION;

            Array.Copy(contentBytes, 0, message, 4, contentBytes.Length);

            int checksum = 0;
            for (int i = 0; i < contentBytes.Length + 4; i++)
            {
                checksum += message[i];
            }
            checksum %= 256;
            message[contentBytes.Length + 4] = (byte)checksum;

            message[contentBytes.Length + 5] = 0x0D;
            message[contentBytes.Length + 6] = 0x0A;

            printer.Write(message, 0, message.Length);
        }

        private void TransferDataToPrinter(SerialPort printer, string code, string nsx, string hsd, string lotnum)
        {
            if (printer == null || !printer.IsOpen)
            {
                MessageBox.Show($"Máy in {printer.PortName} chưa kết nối!", "Lỗi Kết nối", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                NewProtocol_PrintSingleField(printer, code);
                Thread.Sleep(50);
                NewProtocol_PrintSingleField(printer, nsx);
                Thread.Sleep(50);
                NewProtocol_PrintSingleField(printer, hsd);
                Thread.Sleep(50);
                NewProtocol_PrintSingleField(printer, lotnum);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi gửi dữ liệu đến máy in {printer.PortName}: {ex.Message}");
            }
        }

        private void InitNewPrinter(SerialPort printer, string portName, string baudRateStr, SerialDataReceivedEventHandler handler)
        {
            try
            {
                printer.PortName = portName;
                printer.BaudRate = Convert.ToInt32(baudRateStr);
                printer.ReadTimeout = 1000;

                printer.DataReceived += handler;

                if (!printer.IsOpen)
                {
                    printer.Open();
                    NewProtocol_ClearCache(printer);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi kết nối hoặc khởi tạo máy in {portName}: {ex.Message}", "Lỗi Serial Port", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void PackPrinterInit()
        {
            InitNewPrinter(packPrinterPort, strPackPrinterPortName, strPackPrinterBaud, NewPrinterDataReceivedHandler);
        }

        private void CartonPrinterInit()
        {
            InitNewPrinter(cartonPrinterPort, strCartonPrinterPortName, strCartonPrinterBaud, NewPrinterDataReceivedHandler);
        }

        private void NewPrinterDataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort sp = (SerialPort)sender;
            int bytesToRead = sp.BytesToRead;
            byte[] buffer = new byte[bytesToRead];
            sp.Read(buffer, 0, bytesToRead);

            if (sp == packPrinterPort)
            {
                packReceivedBuffer.AddRange(buffer);
                ProcessPackBuffer();
            }
            else if (sp == cartonPrinterPort)
            {
                cartonReceivedBuffer.AddRange(buffer);
                ProcessCartonBuffer();
            }
        }

        private void ProcessPackBuffer()
        {
            // Kiểm tra nếu có ít nhất 4 byte (độ dài của ESC O K 5)
            while (packReceivedBuffer.Count >= 4)
            {
                bool found = false;
                for (int i = 0; i <= packReceivedBuffer.Count - 4; i++)
                {
                    if (packReceivedBuffer[i] == ESC &&
                        packReceivedBuffer[i + 1] == PRINTER_RESPONSE_OK_START &&
                        packReceivedBuffer[i + 2] == 0x4B && // 'K'
                        packReceivedBuffer[i + 3] == PRINTER_RESPONSE_OK_END)
                    {
                        // Tìm thấy Trigger OK
                        this.Invoke(new Action(() =>
                        {
                            HandlePackPrintSuccess();
                        }));

                        // Xóa phần dữ liệu đã xử lý bao gồm cả mã Trigger
                        packReceivedBuffer.RemoveRange(0, i + 4);
                        found = true;
                        break;
                    }
                }
                if (!found) break; // Nếu không tìm thấy khung hình hợp lệ nào thì thoát vòng lặp chờ dữ liệu tiếp
            }
        }

        private void ProcessCartonBuffer()
        {
            while (cartonReceivedBuffer.Count >= 4)
            {
                bool found = false;
                for (int i = 0; i <= cartonReceivedBuffer.Count - 4; i++)
                {
                    if (cartonReceivedBuffer[i] == ESC &&
                        cartonReceivedBuffer[i + 1] == PRINTER_RESPONSE_OK_START &&
                        cartonReceivedBuffer[i + 2] == 0x4B &&
                        cartonReceivedBuffer[i + 3] == PRINTER_RESPONSE_OK_END)
                    {
                        this.Invoke(new Action(() =>
                        {
                            HandleCartonPrintSuccess();
                        }));

                        cartonReceivedBuffer.RemoveRange(0, i + 4);
                        found = true;
                        break;
                    }
                }
                if (!found) break;
            }
        }

        // --- LOGIC XỬ LÝ TRIGGER VÀ SIMULATION ---

        private void HandlePackPrintSuccess()
        {
            if (duplicatefault) return;
            packCodeCount++;
            // 1. Aggregation (Chỉ xảy ra khi camera TẮT -> Simulation Mode)
            if (!packCamEnabled)
            {
                //if (checkBox1Code.Checked == false || (checkBox1Code.Checked == true && packCodeCount == 0))
                //{
                if (int.Parse(SQLConnection.CountPackCode(printedPackcode)) <= 0)
                {
                    SimulatePackCamRead(printedPackcode);
                    //}
                }
            }

            try
            {
                // 2. Đánh dấu mã cũ là đã in và lấy mã mới
                if (checkBox1Code.Checked == false)
                {
                    SQLConnection.TurnOnPrintedPackCode(int.Parse(printedPackcodeID), 1, GetrDateTimeNow());
                }

                printedPackcode = "http://nits.vn/" + SQLConnection.GetPrintedPackCode();
                printedPackcodeID = SQLConnection.GetCurPackCodeID();

                // 3. Đẩy mã mới vào Buffer
                TransferDataToPrinter(
                    packPrinterPort,
                    printedPackcode,
                    textBoxNSX.Text,
                    textBoxHSD.Text,
                    textBoxLotNum.Text
                );
            }
            catch
            {
                MessageBox.Show("Kiểm tra phần mềm tạo code in (Pack)! Hết mã code.");
            }
        }

        private void HandleCartonPrintSuccess()
        {
            if (duplicatefaultCarton) return;
            cartonCodeCount++;
            // 1. Aggregation (Chỉ xảy ra khi camera TẮT -> Simulation Mode)
            if (!cartonCamEnabled)
            {
                //if (checkBox1Code.Checked == false || (checkBox1Code.Checked == true && cartonCodeCount == 0))
                //{
                if (int.Parse(SQLConnection.CountCartonCode(printedCartonCode)) <= 0)
                {
                    SimulateCartonCamRead(printedCartonCode);
                }
                //}
            }

            try
            {
                // 2. Đánh dấu mã cũ là đã in và lấy mã mới
                if (checkBox1Code.Checked == false)
                {
                    SQLConnection.TurnOnPrintedCartonCode(int.Parse(printedcartonCodeID), 1, GetrDateTimeNow());
                }

                printedCartonCode = "http://nits.vn/" + SQLConnection.GetPrintedCartonCode();
                printedcartonCodeID = SQLConnection.GetCurCartonCodeID();

                // 3. Đẩy mã mới vào Buffer
                TransferDataToPrinter(
                    cartonPrinterPort,
                    printedCartonCode,
                    textBoxNSX.Text,
                    textBoxHSD.Text,
                    textBoxLotNum.Text
                );
                // textBoxQRCodeTIJ.Text = printedCartonCode;
            }
            catch
            {
                MessageBox.Show("Hết mã code thùng!");
            }
        }

        // --- HÀM GIẢ LẬP (SIMULATION) ---

        private void SimulatePackCamRead(string packCode)
        {
            this.Invoke(new Action(() =>
            {
                PackCartonInfor();
                SQLConnection.InsertPack(GetrDateTimeNow(), curFillingLine, curBatchID, curLocationID, curQueueID, curCommodityID, packCode, lineVolume, entryStatus);
                UpDateCheckedPackDisplay(1);
                canNum++;
            }));
        }

        private void SimulateCartonCamRead(string cartonCode)
        {
            this.Invoke(new Action(() =>
            {
                carNum++;
                PackCartonInfor();
                SQLConnection.InsertCarton(GetrDateTimeNow(), entryStatus, curPackPerCarton, lineVolume, "1", cartonCode, "10350", curCommodityID, curLocationID, curBatchID, curFillingLine);
                SQLConnection.UPdateCartonRowOfPackTbl(curPackPerCarton, SQLConnection.GetCartonIDWithCode(cartonCode), GetShortDateNow(), curBatchID);
                UpDateCheckedPackDisplay(2);
                // textBoxCarton.Text = cartonCode;
            }));
        }

        // --- LOGIC CAMERA SOCKET (GIỮ NGUYÊN) ---

        private void PackCamInit()
        {
            if (!packCamEnabled) return;
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
                        buttonReset.BackColor = Color.Red;
                        MessageBox.Show("Lỗi đọc code lon trùng nhau!");
                        return;
                    }

                    this.Invoke(new Action(() =>
                    {
                        PackCartonInfor();
                        SQLConnection.InsertPack(GetrDateTimeNow(), curFillingLine, curBatchID, curLocationID, curQueueID, curCommodityID, fullCode, lineVolume, entryStatus);
                        UpDateCheckedPackDisplay(1);
                        // textBoxPack.Text = fullCode;
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

        private void CartonCamInit()
        {
            if (!cartonCamEnabled) return; // ĐÃ SỬA LỖI LOGIC TẠI ĐÂY
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
                        // XÓA: StopPrintTIJ();
                        buttonReset.BackColor = Color.Red;
                        MessageBox.Show("Lỗi đọc code carton trùng nhau!");
                        return;
                    }

                    this.Invoke(new Action(() =>
                    {
                        carNum++;
                        // labelNumCarton.Text = (carNum).ToString();
                        PackCartonInfor();
                        SQLConnection.InsertCarton(GetrDateTimeNow(), entryStatus, curPackPerCarton, lineVolume, "1", fullCode, "10350", curCommodityID, curLocationID, curBatchID, curFillingLine);
                        SQLConnection.UPdateCartonRowOfPackTbl(curPackPerCarton, SQLConnection.GetCartonIDWithCode(fullCode), GetShortDateNow(), curBatchID);
                        UpDateCheckedPackDisplay(2);
                        // textBoxCarton.Text = szData;
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
                        // textBoxCarton.Text = szData;
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

        private string GetrDateTimeNow()
        {
            return DateTime.Now.ToString("yyyy-M-dd HH:mm:ss");
        }

        private string GetShortDateNow()
        {
            return DateTime.Now.ToString("yyyy-M-dd HH:mm:ss").Substring(0, 10);
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

        // ĐÃ XÓA/SỬA CÁC HÀM CŨ: StopPrintTIJ, MakeStopForm, StartPrintTIJ, MakeStartForm, timerCheckProNum_Tick, labelProNum_TextChanged, ChangeThePrinterCode.

        // Hàm này không còn liên quan đến luồng chính.
        private void button3_Click(object sender, EventArgs e)
        {
            // Logic cũ đã bị xóa.
        }

        private void buttonDisconnectRs232_Click(object sender, EventArgs e)
        {
            // Logic cũ đã bị xóa.
        }

        // --- CÁC HÀM XỬ LÝ SỰ KIỆN GIAO DIỆN CHÍNH ---

        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (textBoxLotNum.Text.Length < 10)
            {
                MessageBox.Show("Kiểm tra lại số LOT!");
                return;
            }

            PackCartonInfor();

            // KHỞI TẠO MÁY IN SERIAL BUFFER MỚI (FIXED: Đã thêm gọi hàm INIT)


            if (onlyCarton == false)
            {
                PackPrinterInit();
                CartonPrinterInit();
            }
            else
            {
                CartonPrinterInit();
            }

            GetFirstPrintedData();

            // ĐẨY DỮ LIỆU BAN ĐẦU VÀO BUFFER (FIXED: Đã thêm logic đẩy dữ liệu)
            if (onlyCarton == false)
            {
                TransferDataToPrinter(packPrinterPort, printedPackcode, textBoxNSX.Text, textBoxHSD.Text, textBoxLotNum.Text);
            }
            TransferDataToPrinter(cartonPrinterPort, printedCartonCode, textBoxNSX.Text, textBoxHSD.Text, textBoxLotNum.Text);

            UpDateCheckedPackDisplay(2);

            if (packCamEnabled)
            {
                PackCamInit();
            }
            if (cartonCamEnabled)
            {
                CartonCamInit();
            }

            // KIỂM TRA TRẠNG THÁI KẾT NỐI MỚI (Đã loại bỏ kiểm tra P và client cũ)
            bool printersConnected = packPrinterPort.IsOpen && cartonPrinterPort.IsOpen;
            bool camerasReady = (clientPackcam != null || !packCamEnabled) && (clientCartoncam != null || !cartonCamEnabled);

            if (printersConnected && camerasReady)
            {
                btnConnect.Enabled = false;
            }
        }

        private void button2_Click_2(object sender, EventArgs e) // Nút Disconnect (FIXED: Đã đóng cổng COM mới)
        {
            cartonCodeCount = 0;
            packCodeCount = 0;

            SQLConnection.TurnOnPrintedPackCode(int.Parse(printedPackcodeID), 1, GetrDateTimeNow());
            SQLConnection.TurnOnPrintedCartonCode(int.Parse(printedcartonCodeID), 1, GetrDateTimeNow());

            if (packPrinterPort != null && packPrinterPort.IsOpen) packPrinterPort.Close();
            if (cartonPrinterPort != null && cartonPrinterPort.IsOpen) cartonPrinterPort.Close();

            StopClientCartonCam();
            StopClientPackCam();

            // SỬA LOGIC KIỂM TRA TRẠNG THÁI CUỐI CÙNG
            if (!packPrinterPort.IsOpen && !cartonPrinterPort.IsOpen && clientPackcam == null && clientCartoncam == null)
            {
                btnConnect.Enabled = true;
                buttonDisCon.Enabled = false;
            }
        }

        private void buttonReset_Click(object sender, EventArgs e)
        {
            if (duplicatefaultCarton)
            {
                // Logic dừng in cũ đã bị loại bỏ, chỉ xóa cờ lỗi
                duplicatefaultCarton = false;
            }
            else if (duplicatefault)
            {
                // Logic dừng in cũ đã bị loại bỏ, chỉ xóa cờ lỗi
                duplicatefault = false;
            }

            // Gửi lại dữ liệu in ban đầu sau reset
            GetFirstPrintedData(); // Lấy mã mới sau khi reset lỗi
            TransferDataToPrinter(packPrinterPort, printedPackcode, textBoxNSX.Text, textBoxHSD.Text, textBoxLotNum.Text);
            TransferDataToPrinter(cartonPrinterPort, printedCartonCode, textBoxNSX.Text, textBoxHSD.Text, textBoxLotNum.Text);

            buttonReset.BackColor = Color.Green;
            waitForDataPackCam();
            waitForDataCartonCam();
        }



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

            labelNumFailCarton.Text = (dataGridViewFailCarton.Rows.Count - 1).ToString();
            labelNumFailCarton2.Text = (dataGridViewFailCarton.Rows.Count - 1).ToString();
            labelNumCarton.Text = (dataGridViewCheckedCarton.Rows.Count - 1).ToString();
            labelNumCarton2.Text = (dataGridViewCheckedCarton.Rows.Count - 1).ToString();

            labelPalletNum.Text = ((dataGridViewCheckedCarton.Rows.Count - 1) / int.Parse(curCartonPerPallet)).ToString();

            for (int i = 0; i < dataGridViewPackWaitPack.Rows.Count - 1; i++)
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


        private void timerCheckDominostate_Tick(object sender, EventArgs e)
        {

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

        private void buttonDeleteAllCan_Click(object sender, EventArgs e)
        {

        }

        private void Form1_Load(object sender, EventArgs e)
        {


        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            cartonCodeCount = 0;
            packCodeCount = 0;

            try
            {
                if (!string.IsNullOrEmpty(printedPackcodeID))
                {
                    SQLConnection.TurnOnPrintedPackCode(
                        int.Parse(printedPackcodeID), 1, GetrDateTimeNow());
                }

                if (!string.IsNullOrEmpty(printedcartonCodeID))
                {
                    SQLConnection.TurnOnPrintedCartonCode(
                        int.Parse(printedcartonCodeID), 1, GetrDateTimeNow());
                }
            }
            catch
            {
                // Khi đóng app: NUỐT LỖI, KHÔNG SHOW MESSAGEBOX
            }
        }
    }
}
