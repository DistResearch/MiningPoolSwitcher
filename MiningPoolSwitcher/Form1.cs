using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.IO;
using System.Diagnostics;

namespace MiningpoolSwitcher
{
    public partial class Form1 : Form
    {
        Thread _thread;
        ManualResetEvent _stop = new ManualResetEvent(false);
        public delegate void UpdateFormDelegate();

        DateTime _lastBlockDate = DateTime.MinValue;
        string _duration;

        public Form1()
        {
            InitializeComponent();
            
            ReadWritePhoenixConfig();
            
            _thread = new Thread(CheckPhoenixByRPC);
            _thread.Start();
        }

        private void CheckPhoenixByRPC()
        {
            do
            {
                string htmlCode;
                using (WebClient client = new WebClient())
                {
                    htmlCode = client.DownloadString("https://deepbit.net/stats");
                }

                Regex r = new Regex("/\\w{64}?'>(.*?)</tr");
                foreach (Match m in r.Matches(htmlCode))
                {
                    string strDate = (new Regex("(.*?)</a")).Match(m.Groups[1].Value).Groups[1].Value.Replace("&nbsp;", " ");
                    DateTime utcDate = DateTime.ParseExact(strDate, "dd.MM H:mm:ss", null);
                    _lastBlockDate = utcDate.ToLocalTime();
                    _duration = (new Regex("<td>(.*?)</td>")).Match(m.Groups[1].Value).Groups[1].Value;
                    break;
                }

                try
                {
                    this.Invoke(new UpdateFormDelegate(this.ChangeSomeFormControls), new object[] { });
                }
                catch { }

                SwitchPoolIfNeed();
            }
            while (!_stop.WaitOne(60000));
        }

        public void ChangeSomeFormControls()
        {
            TimeSpan period = DateTime.Now - _lastBlockDate;
            label1.Text = string.Format("Последний найденый блок на deepbit.net {0} за {1},\r\n"
                + "c последенго блока прошло {2}h {3}m\r\n\r\nОсновной пул deepbit.net: {4}", 
                _lastBlockDate, _duration, period.Hours, period.Minutes, isPoolDeepbitMainCurrent ? "Да" : "Нет");
        }


        private void button1_Click(object sender, EventArgs e)
        {
            SwitchPoolIfNeed();
        }

        void SwitchPoolIfNeed()
        {
            ReadWritePhoenixConfig();
            if (isPoolDeepbitMainNeed != null && isPoolDeepbitMainCurrent != isPoolDeepbitMainNeed)
            {
                ReadWritePhoenixConfig(true);
                try { Process.Start("rerun.bat"); }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
                Log("Основной пул deepbit.net: {0} " + (isPoolDeepbitMainCurrent ? "Да" : "Нет"));
            }
            ReadWritePhoenixConfig();
            this.Invoke(new UpdateFormDelegate(this.ChangeSomeFormControls), new object[] { });
        }


        public static void Log(string s, string logfile = "MiningPoolSwitcher.txt")
        {
            //удаляем файл, если он превышает 10 метров
            FileInfo fi = new FileInfo(logfile);
            if (fi.Exists && fi.Length > 10000000)
                fi.Delete();

            File.AppendAllText(logfile, string.Format("{0}: {1}\r\n", DateTime.Now, s));
        }

        bool isPoolDeepbitMainCurrent; // текущее значение конфига

        bool? isPoolDeepbitMainNeed // на Deepbit прошло менее 6 часов с последнего блока
        {
            get
            {
                if (_lastBlockDate != DateTime.MinValue)
                    return DateTime.Now - _lastBlockDate <= new TimeSpan(6, 0, 0);
                else
                    return null;
            }
        }


        void ReadWritePhoenixConfig(bool isWrite = false)
        {
            string[] ss = File.ReadAllLines("phoenix.cfg");
            string[] ss_new = new string[ss.Count()];
            string acc_deepbit = string.Empty;
            string acc_50btc = string.Empty;

            //находим строки подключения к пулам
            foreach (string s in ss)
            {
                if (s.Trim().IndexOf("50btc.com") > -1)
                    acc_50btc = s.Substring(s.IndexOf("=") + 1).Trim();
                if (s.Trim().IndexOf("deepbit.net") > -1)
                {
                    acc_deepbit = s.Substring(s.IndexOf("=") + 1).Trim();

                    //isPoolDeepbitMainCurrent = s.Trim()!= s.Trim().IndexOf("backend") == 0;

                    if (s.Trim().IndexOf("backend") == 0)
                        isPoolDeepbitMainCurrent = true;
                    else if (s.Trim().IndexOf("backups") == 0)
                        isPoolDeepbitMainCurrent = false;
                }

            }

            for (int i = 0; i < ss.Count(); i++)
            {
                if (ss[i].Trim().IndexOf("backend") == 0)
                    ss_new[i] = "backend = " + (isPoolDeepbitMainNeed == true ? acc_deepbit : acc_50btc);
                else if (ss[i].Trim().IndexOf("backups") == 0)
                    ss_new[i] = "backups = " + (!isPoolDeepbitMainNeed == true ? acc_deepbit : acc_50btc);
                else
                    ss_new[i] = ss[i];
            }

            if (isWrite)
                File.WriteAllLines("phoenix.cfg", ss_new);
        }


        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            _stop.Set();
            Thread.Sleep(200);
        }
    }
}
