using System;
using System.Resources;
using System.IO;
using System.Text;
using System.Threading;
using System.IO.Ports;

namespace Sample
{
    public class GSM_Modem
    {
        static Thread LoopThread = null;

        public static SerialPort Port = null;

        public void Init()
        {
            Program.DB_Print("GSM Modem Start");

            LoopThread = new Thread(ThreadLoop);
            LoopThread.Start();
        }

        static public Int32 ModemTxRx(byte[] Cmd, byte[] Rep, byte Ter, String RxSearch, Int32 RxDelay)
        {
            Int32 ReplyLen = 0;

            Program.DB_Print("Sending: " + new String(Encoding.UTF8.GetChars(Cmd)));
           
            if (Ter != 0)
            {
                Cmd[Cmd.Length - 1] = Ter;
            }

            SERIAL_COM.Modem.Write(Cmd, 0, Cmd.Length);

            if (Rep != null)
            {
                do
                {
                    if (RxDelay > 0)
                    {
                        RxDelay--;
                        Thread.Sleep(1000);
                    }

                    ReplyLen = SERIAL_COM.Modem.Read(Rep, 0, Rep.Length);
                    
                    String sRep = new String(Encoding.UTF8.GetChars(Rep), 0, ReplyLen);

                    if (sRep != null)
                    {
                        Program.DB_Print("Rx Wait: " + RxDelay + " Sec :" + sRep);

                        if (RxSearch != null)
                        {
                            Int32 iRep = sRep.IndexOf(RxSearch);

                            if (iRep == -1)
                            {
                                ReplyLen = 0;
                            }
                        }

                        if (ReplyLen > 0)
                        {
                            break;
                        }
                    }
                    else
                    {
                        Program.DB_Print("Rx Wait: " + RxDelay + " sec :");
                    }
                } while (RxDelay > 0);
            }
            return ReplyLen;
        }

        static public Int32 ModemTxRxDecode(String SMSPhoneNum, String SMSText)
        {
            Int32 ReplyLen = 0;
            byte[] Rep = new byte[25];

            try
            {
                Program.DB_Print("Send: " + SMSText + " to " + SMSPhoneNum);

                byte[] Cmd = Encoding.UTF8.GetBytes("AT+CMGF=1 ");

                if (ModemTxRx(Cmd, Rep, 13, "OK", 2) > 1)   //13: Enter; wait for "OK" character else no response
                {
                    Cmd = Encoding.UTF8.GetBytes("AT+CMGS=" + "\"" + SMSPhoneNum + "\" ");

                    if (ModemTxRx(Cmd, Rep, 13, ">", 2) > 1)        //13: Enter; wait for ">" character else no response
                    {
                        Cmd = Encoding.UTF8.GetBytes(SMSText + " ");

                        if (ModemTxRx(Cmd, Rep, 26, "+CMGS", 10) > 1)        //26: Ctl+Z  wait for "+CMGS" else no response
                        {
                            ReplyLen = 1;
                        }
                        else
                        {
                            Program.DB_Print("3: Err/No Resp");
                        }
                    }
                    else
                    {
                        Program.DB_Print("2: Err/No Resp");
                    }
                }
                else
                {
                    Program.DB_Print("1: Err/No Resp");

                    Cmd = Encoding.UTF8.GetBytes(" ");

                    if (ModemTxRx(Cmd, Rep, 26, "+CMGS", 1) > 1)        //26: Ctl+Z wait for "+CMGS" else no response
                    {
                        Program.DB_Print("Modem Init");
                    }
                    else
                    {
                        Program.DB_Print("4: Err/No Resp");
                    }
                }
            }
            catch (Exception ex)
            {
                Program.DB_Print(ex.Message);
            }
            return ReplyLen;
        }

        static public void ThreadLoop()
        {
            while (true)
            {
                try
                {
                    if (SQL_DBaseSet.LogSet.SMSPhoneNum != null)
                    {
                        String SMSPhoneNum = new String(SQL_DBaseSet.LogSet.SMSPhoneNum);

                        if (SMSPhoneNum.Length > 0)
                        {
                            for (UInt32 ChannelNo = 0; ChannelNo < SQL_DBase.Channels; ChannelNo++)
                            {
                                //SQL_DBase.LogCh[ChannelNo].SMS_Msg = "SMS Test";

                                if (SQL_DBase.LogCh[ChannelNo].SMS_Msg != null)
                                {
                                    if (SQL_DBase.LogCh[ChannelNo].SMS_Msg.Length > 0)
                                    {
                                        Int32 ReplyLen = 0;
                                        String[] ArraySMSPhoneNum = SMSPhoneNum.Split(';');
										
                                        String SMS_Msg  = "-----SMS  ALERT-----" + "\n";
											   SMS_Msg += "SL #:" + new String(SQL_DBaseSet.LogSet.SerialNo) + "\n";
											   SMS_Msg += "From:" + new String(SQL_DBaseSet.LogSet.BoxName) + "\n";
											   SMS_Msg += SQL_DBase.LogCh[ChannelNo].SMS_Msg + "\n";
											   SMS_Msg += "-----SMS  ALERT-----";

                                        for (Int32 i = 0; i < ArraySMSPhoneNum.Length; i++)
                                        {
                                            ReplyLen = ModemTxRxDecode(ArraySMSPhoneNum[i], SMS_Msg);
                                        }

                                        if (ReplyLen == 1)
                                        {
                                            Program.DB_Print("SMS Sent: " + SMS_Msg);
                                            SQL_DBase.LogCh[ChannelNo].SMS_Msg = null;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    Thread.Sleep(10 * 1000);
                }
                catch (Exception ex)
                {
                    Program.DB_Print(ex.Message);
                }
            }
        }
   }
}
