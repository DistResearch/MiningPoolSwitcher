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

namespace MiningpoolSwitcher
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string htmlCode;
            using (WebClient client = new WebClient())
            {
                //client.DownloadFile("http://yoursite.com/page.html", @"C:\localfile.html");

                htmlCode = client.DownloadString("https://deepbit.net/stats");
            }

            Regex r = new Regex("/\\w{64}?'>(.*?)</tr");
            MatchCollection mc = r.Matches(htmlCode);
        }
    }
}
