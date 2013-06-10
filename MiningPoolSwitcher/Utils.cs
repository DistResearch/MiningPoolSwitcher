using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Win32;
using System.IO;

namespace MiningpoolSwitcher
{
    public class Utils
    {
        public static void SetRegistryKey(string name, string value)
        {
            RegistryKey saveKey = Registry.LocalMachine.CreateSubKey("software\\MiningPoolSwitcher");
            saveKey.SetValue(name, value == null ? string.Empty : value);
            saveKey.Close();
        }

        public static string GetRegistryKey(string name)
        {
            try
            {
                RegistryKey readKey = Registry.LocalMachine.OpenSubKey("software\\MiningPoolSwitcher");
                string loadString = (string)readKey.GetValue(name);
                readKey.Close();
                return loadString;
            }
            catch
            {
                return string.Empty;
            }
        }

        public static void Log(string s, string logfile = "MiningPoolSwitcher.txt")
        {
            //удаляем файл, если он превышает 10 метров
            FileInfo fi = new FileInfo(logfile);
            if (fi.Exists && fi.Length > 10000000)
                fi.Delete();

            File.AppendAllText(logfile, string.Format("{0}: {1}\r\n", DateTime.Now, s));
        }
    }
}
