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
                        
            //инициализируем настроки времени переключения на PPS c Prop.
            string ticks_str = Utils.GetRegistryKey("PropPeriodTicks");
            if (string.IsNullOrEmpty(ticks_str))
                ticks_str = "180000000000"; // 5 часов
            long ticks;
            long.TryParse(ticks_str, out ticks);
            _periodProp = new TimeSpan(ticks);
            nudHour.Value = _periodProp.Hours;
            nudMinutes.Value = _periodProp.Minutes;
            nudHour.ValueChanged += nudMinutes_ValueChanged;
            nudMinutes.ValueChanged += nudMinutes_ValueChanged;

            ReadWritePhoenixConfig();            
            
            _thread = new Thread(DoCheckingDeepbit);
            _thread.Start();
        }

        private void DoCheckingDeepbit()
        {
            do
            {
                try
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
                }
                catch (Exception ex) { Utils.Log(ex.Message); }

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
                Utils.Log("Основной пул deepbit.net: {0} " + (isPoolDeepbitMainCurrent ? "Да" : "Нет"));
            }
            ReadWritePhoenixConfig();
            try
            {
                this.Invoke(new UpdateFormDelegate(this.ChangeSomeFormControls), new object[] { });
            }
            catch { }
        }

        bool isPoolDeepbitMainCurrent; // текущее значение конфига

        TimeSpan _periodProp = new TimeSpan(6, 0, 0);

        bool? isPoolDeepbitMainNeed // на Deepbit прошло менее 6 часов с последнего блока
        {
            get
            {
                if (_lastBlockDate != DateTime.MinValue)
                    return DateTime.Now - _lastBlockDate <= _periodProp;
                else
                    return null;
            }
        }

        void ReadWritePhoenixConfig(bool isWrite = false)
        {
            try
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
            catch (Exception ex) { Utils.Log(ex.Message); }
        }


        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            _stop.Set();
            Thread.Sleep(200);
        }

        private void nudMinutes_ValueChanged(object sender, EventArgs e)
        {
            _periodProp = new TimeSpan((int)nudHour.Value, (int)nudMinutes.Value, 0);
            Utils.SetRegistryKey("PropPeriodTicks", _periodProp.Ticks.ToString());
        }
    }
}
