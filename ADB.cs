using RIFT_Downgrader;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace ADB
{
    public class ADBInteractor
    {
        public List<string> ADBPaths { get; set; } = new List<string>() { "adb.exe", "User\\Android\\platform-tools_r29.0.4-windows\\platform-tools\\adb.exe", "User\\AppData\\Roaming\\SideQuest\\platform-tools\\adb.exe", "C:\\Program Files\\SideQuest\\resources\\app.asar.unpacked\\build\\platform-tools\\adb.exe" };

        public bool adb(String Argument)
        {
            return adbThreadHandler(Argument).Result;
        }

        public async Task<bool> adbThreadHandler(String Argument)
        {
            bool returnValue = false;
            String txtAppend = "N/A";
            Thread t = new Thread(() =>
            {
                switch (adbThread(Argument))
                {
                    case "true":
                        returnValue = true;
                        break;
                    case "adb110":
                        txtAppend = "\n\n\nAn error Occured (Code: ADB110). Check following:\n\n- Your Quest is connected, Developer Mode enabled and USB Debugging enabled.";
                        break;
                    case "adb100":
                        txtAppend = "\n\n\nAn error Occured (Code: ADB100). Check following:\n\n- You have adb installed.";
                        break;
                }
            });
            t.IsBackground = true;
            t.Start();
            while (txtAppend == "N/A" && returnValue == false)
            {
                await DelayCheck();
            }
            if (txtAppend != "N/A")
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(txtAppend);
            }
            return returnValue;
        }

        public string adbThread(String Argument)
        {
            String User = System.Environment.GetEnvironmentVariable("USERPROFILE");
            foreach (String ADB in ADBPaths)
            {

                ProcessStartInfo s = new ProcessStartInfo();
                s.CreateNoWindow = true;
                s.UseShellExecute = false;
                s.FileName = ADB.Replace("User", User);
                s.Arguments = Argument;
                s.RedirectStandardOutput = true;
                try
                {
                    // Start the process with the info we specified.
                    // Call WaitForExit and then the using statement will close.
                    Logger.Log("Starting adb with " + s.ToString());
                    using (Process exeProcess = Process.Start(s))
                    {
                        String IPS = exeProcess.StandardOutput.ReadToEnd();
                        exeProcess.WaitForExit();
                        Logger.Log("Output: " + IPS);
                        Console.WriteLine("Output by ADB: " + IPS);
                        if (IPS.Contains("no devices/emulators found"))
                        {
                            return "adb110";
                        }
                        return "true";
                    }
                }
                catch
                {
                    continue;
                }
            }
            return "adb100";
        }

        public string adbS(String Argument)
        {
            return adbSThreadHandler(Argument).Result;
        }

        public async Task<string> adbSThreadHandler(String Argument)
        {
            string returnValue = "Error";
            String txtAppend = "N/A";
            Thread t = new Thread(() =>
            {
                String MethodReturnValue = adbSThread(Argument);
                Console.WriteLine("adbS finished");
                switch (MethodReturnValue)
                {
                    case "adb110":
                        txtAppend = "\n\n\nAn error Occured (Code: ADB110). Check following:\n\n- Your Quest is connected, Developer Mode enabled and USB Debugging enabled.";
                        break;
                    case "adb100":
                        txtAppend = "\n\n\nAn error Occured (Code: ADB100). Check following:\n\n- You have adb installed.";
                        break;
                    default:
                        returnValue = MethodReturnValue;
                        break;
                }
            });
            t.IsBackground = true;
            t.Start();
            while (txtAppend == "N/A" && returnValue == "Error")
            {
                await DelayCheck();
            }
            if (txtAppend != "N/A")
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(txtAppend);
            }
            return returnValue;
        }

        public async Task DelayCheck()
        {
            var frame = new DispatcherFrame();
            new Thread((ThreadStart)(() =>
            {
                Thread.Sleep(500);
                frame.Continue = false;
            })).Start();
            Dispatcher.PushFrame(frame);
        }

        public string adbSThread(String Argument)
        {
            String User = System.Environment.GetEnvironmentVariable("USERPROFILE");

            foreach (String ADB in ADBPaths)
            {
                ProcessStartInfo s = new ProcessStartInfo();
                s.CreateNoWindow = true;
                s.UseShellExecute = false;
                s.FileName = ADB.Replace("User", User);
                s.WindowStyle = ProcessWindowStyle.Minimized;
                s.Arguments = Argument;
                s.RedirectStandardOutput = true;
                try
                {
                    // Start the process with the info we specified.
                    // Call WaitForExit and then the using statement will close.
                    Logger.Log("Starting adb with " + s.ToString());
                    using (Process exeProcess = Process.Start(s))
                    {
                        String IPS = exeProcess.StandardOutput.ReadToEnd();
                        exeProcess.WaitForExit();
                        Logger.Log("Output: " + IPS);
                        if (IPS.Contains("no devices/emulators found"))
                        {
                            return "adb110";
                        }

                        return IPS;
                    }
                }
                catch
                {
                    continue;
                }
            }
            return "adb100";
        }
    }
}