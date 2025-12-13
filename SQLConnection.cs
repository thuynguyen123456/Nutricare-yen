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
    class SQLConnection
    {
        public static string sqlServer;
        public static string sqlDatabase;
        public static string sqlUser;
        public static string sqlPass;
        //SQL
        public static SqlConnection connect;

        static void OpenConnection()
        {
            try
            {
                if (connect == null)
                {
                    connect = new SqlConnection(@"Server=" + sqlServer + ";Database=" + sqlDatabase + ";Trusted_Connection=True;MultipleActiveResultSets=true;");
                }
                if (connect.State != ConnectionState.Open)
                {
                    connect.Open();
                }
            }
            catch
            {
                MessageBox.Show("Can not access to the database!");
            }
        }

        static void CloseConnection()
        {
            if (connect.State == ConnectionState.Open)
            {
                connect.Close();
            }
        }

        static void DoSQLCommad(string strSQL)
        {
            try
            {
                OpenConnection();
                SqlCommand sqlCmd = new SqlCommand(strSQL, connect);
                sqlCmd.ExecuteNonQuery();
                CloseConnection();
            }
            catch
            {

            }
        }


        public static DataTable GetDataTable(string strSQL)
        {
            try
            {
                OpenConnection();
                DataTable dt = new DataTable();
                SqlDataAdapter sqlda = new SqlDataAdapter(strSQL, connect);
                if (sqlda != null)
                {
                    sqlda.Fill(dt);
                    CloseConnection();
                    return dt;
                }
                else
                {
                    CloseConnection();
                    return null;
                }
            }
            catch
            {
                MessageBox.Show("Get data table fault");
                return null;
            }
        }

        private static string GetValue(string strSQL, int col)
        {
            string temp = null;
            OpenConnection();
            SqlCommand sqlcmd = new SqlCommand(strSQL, connect);
            SqlDataReader sqldr = sqlcmd.ExecuteReader();
            while (sqldr.Read())
            {
                temp = sqldr[col].ToString();
            }
            CloseConnection();
            return temp;
        }

        public static string GetPrintedCartonCode()
        {
            return GetValue("select top(1) * from CartonCodes where Printed=0",1);
        }

        public static string GetCurCartonCodeID()
        {
            return GetValue("select top(1) * from CartonCodes where Printed=0", 0);
        }

        public static string GetCartonIDWithCode(string code)
        {
            return GetValue("  select * from Cartons where Code = N'" + code + "'",0);
        }

        public static string GetCurCartonID()
        {
            return GetValue("select top(1) * from Cartons order by CartonID DESC", 0);
        }

        public static void TurnOnPrintedCartonCode(int cartonCodeID,int value, string datetime)
        {
            DoSQLCommad(@"UPDATE CartonCodes SET Printed =N'" + value + "', PrintedDate=N'"+datetime+"'  Where CartonCodeID=N'" + cartonCodeID + "'");
        }

        public static string GetPrintedPackCode()
        {
            return GetValue("select top(1) * from PackCodes where Printed=0", 1);
        }

        public static string GetCurPackCodeID()
        {
            return GetValue("select top(1) * from PackCodes where Printed=0", 0);
        }

        public static void TurnOnPrintedPackCode(int packID, int value,string dateTime)
        {
            DoSQLCommad(@"UPDATE PackCodes SET Printed =N'" + value + "',PrintedDate=N'"+dateTime+"' Where PackCodeID=N'" + packID + "'");
        }


        public static string GetCurBatchIDActive(string fillingLine)
        {
            return GetValue("select * from Batches where FillingLineID='" + fillingLine + "' and InActive=1", 0);
        }

        public static string GetCurCommodiy(string fillingLine)
        {
            return GetValue("select * from Batches where FillingLineID='" + fillingLine + "' and InActive=1", 8);
        }

        public static string GetPackPerCarton(string commodityID)
        {
            return GetValue("select * from Commodities where CommodityID='" + commodityID + "'", 16);
        }

        public static string GetLocationID(string curBatchID)
        {
            return GetValue("select * from Batches where BatchID='"+curBatchID+"'",10);
        }
        public static string CountPackCode(string code)
        {
            return GetValue("select count (*) from Packs where Code like '" + code + "'", 0);
        }

        public static string CountCartonCode(string code)
        {
            return GetValue("select count (*) from Cartons where Code like '" + code + "'", 0);
        }

        public static void InsertPack(string EntryDate, string fillingLine,string batchID,string locationID,string queueID,string commodityID,string code,string lineVolume,string entryStatus)
        {
            DoSQLCommad(@"INSERT INTO Packs (EntryDate, FillingLineID, BatchID, LocationID, QueueID, CommodityID, Code, LineVolume, EntryStatusID)
                        VALUES('" + EntryDate + "','"+fillingLine+ "', '"+batchID+ "', '"+locationID+ "', '"+queueID+ "', '"+commodityID+ "', '"+code+"','"+lineVolume+"','"+entryStatus+"')");
        }

        public static void InsertCarton(string entryDate,string EntryStatusID, string PackCounts, string LineVolume, string Quantity, string Code, string PalletID, string CommodityID, string LocationID, string BatchID, string FillingLineID)
        {
            DoSQLCommad(@"INSERT INTO Cartons(EntryDate, EntryStatusID, PackCounts, LineVolume, Quantity, Code, PalletID, CommodityID, LocationID, BatchID, FillingLineID)
                VALUES('"+entryDate+ "','"+EntryStatusID+ "','"+PackCounts+ "','"+LineVolume+ "','"+Quantity+ "','"+Code+ "','"+PalletID+ "','"+CommodityID+ "','"+LocationID+ "','"+BatchID+ "','"+FillingLineID+"')");;
        }

        public static string GetCartonPerPallet(string commodityID)
        {
            return GetValue("select * from Commodities where CommodityID='" + commodityID + "'", 17);
        }

        public static void UPdateCartonRowOfPackTbl(string packPerCarton,string newCartonID, string curDate, string curBatchID)
        {
            DoSQLCommad(@"UPDATE Packs SET CartonID = N'"+newCartonID+"' WHERE PackID IN(SELECT TOP ("+packPerCarton+ @") PackID FROM Packs where CartonID is NULL and BatchID = '" + curBatchID + "')");
        }

        public static void SetNullPacksAtCartonID(string cartonID)
        {
            DoSQLCommad(@"UPDATE Packs SET CartonID = NULL Where CartonID =N'" + cartonID + "'");
        }

        public static void DeleteRowsViaCartonID(string cartonID)
        {
            DoSQLCommad(@"DELETE Packs Where CartonID =N'" + cartonID + "'");
        }

        public static void DeleteRowsViaPackcode(string code)
        {
            DoSQLCommad(@"DELETE Packs Where Code =N'" + code + "'");
        }

        public static void ChangeCartonToUnused(string code)
        {
            DoSQLCommad(@"UPDATE Cartons SET Code = N'" + "UNUSED"+ "',EntryStatusID=N'" + "9" + "' Where Code =N'" + code + "'");
        }
    }
}
