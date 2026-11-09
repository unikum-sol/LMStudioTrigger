// LM Studio Trigger fuer Boray POWer
// Ueberwacht TCP-Verbindungen auf Port 1234 (Status ESTABLISHED).
// Solange Verbindungen bestehen, wird per SendInput eine 0-Pixel-Mausbewegung
// gesendet -> Boray POWer (GetLastInputInfo) erkennt "Aktivitaet" und bleibt auf High.
// Kompilieren (.NET Framework 4, C# 5):
// C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /out:LMStudioTrigger.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll LMStudioTrigger.cs

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

static class Program
{
    [STAThread]
    static void Main()
    {
        bool createdNew;
        using (Mutex m = new Mutex(true, "LMStudioTrigger_SingleInstance", out createdNew))
        {
            if (!createdNew)
            {
                MessageBox.Show("LM Studio Trigger l\u00e4uft bereits (siehe Infobereich).", "LM Studio Trigger");
                return;
            }
            Application.EnableVisualStyles();
            Application.Run(new TriggerContext());
        }
    }
}

class TriggerContext : ApplicationContext
{
    // ===== Einstellungen =====
    const int LmStudioPort = 1234;      // LM-Studio-Serverport
    const int CheckIntervalMs = 2000;   // Pruefintervall Verbindungen
    const int NudgeIntervalSec = 10;    // Abstand der simulierten Eingaben, MUSS kleiner als Boray-Idle-Zeit sein
    // =========================

    [StructLayout(LayoutKind.Sequential)]
    struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    struct INPUT { public uint type; public MOUSEINPUT mi; }
    [DllImport("user32.dll", SetLastError = true)]
    static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
    [DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr hIcon);

    NotifyIcon tray;
    System.Windows.Forms.Timer timer;
    Icon iconActive, iconIdle;

    int connCount = 0;
    List<string> connList = new List<string>();
    DateTime lastActivity = DateTime.MinValue;
    DateTime lastNudge = DateTime.MinValue;
    uint lastNudgeResult = 0;
    string lastError = "";

    public TriggerContext()
    {
        iconActive = MakeIcon(Color.LimeGreen);
        iconIdle = MakeIcon(Color.Gray);

        ContextMenuStrip menu = new ContextMenuStrip();
        menu.Items.Add("Status", null, delegate { ShowStatus(); });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Beenden", null, delegate { ExitApp(); });

        tray = new NotifyIcon();
        tray.Icon = iconIdle;
        tray.Text = "LM Studio Trigger: keine Anfrage";
        tray.ContextMenuStrip = menu;
        tray.Visible = true;
        tray.DoubleClick += delegate { ShowStatus(); };

        timer = new System.Windows.Forms.Timer();
        timer.Interval = CheckIntervalMs;
        timer.Tick += delegate { Check(); };
        timer.Start();
        Check();
    }

    void Check()
    {
        List<string> list = new List<string>();
        try
        {
            TcpConnectionInformation[] conns = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections();
            foreach (TcpConnectionInformation c in conns)
            {
                if (c.LocalEndPoint.Port == LmStudioPort && c.State == TcpState.Established)
                    list.Add(c.RemoteEndPoint.ToString());
            }
            lastError = "";
        }
        catch (Exception ex) { lastError = ex.Message; }

        connList = list;
        connCount = list.Count;

        if (connCount > 0)
        {
            lastActivity = DateTime.Now;
            if ((DateTime.Now - lastNudge).TotalSeconds >= NudgeIntervalSec)
            {
                lastNudgeResult = Nudge();
                lastNudge = DateTime.Now;
            }
            tray.Icon = iconActive;
            tray.Text = "LM Studio Trigger: aktiv (" + connCount + " Verb.)";
        }
        else
        {
            tray.Icon = iconIdle;
            tray.Text = "LM Studio Trigger: keine Anfrage";
        }
    }

    uint Nudge()
    {
        INPUT[] inp = new INPUT[1];
        inp[0].type = 0;               // INPUT_MOUSE
        inp[0].mi.dwFlags = 0x0001;    // MOUSEEVENTF_MOVE, dx=dy=0
        return SendInput(1, inp, Marshal.SizeOf(typeof(INPUT)));
    }

    void ShowStatus()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Port: " + LmStudioPort);
        sb.AppendLine("Status: " + (connCount > 0 ? "AKTIV - LM Studio wird benutzt" : "keine Anfrage"));
        sb.AppendLine("Aktive Verbindungen: " + connCount);
        foreach (string s in connList) sb.AppendLine("   " + s);
        sb.AppendLine("Letzte Aktivit\u00e4t: " + (lastActivity == DateTime.MinValue ? "-" : lastActivity.ToString("dd.MM.yyyy HH:mm:ss")));
        sb.AppendLine("Letzte simulierte Eingabe: " + (lastNudge == DateTime.MinValue ? "-" : lastNudge.ToString("HH:mm:ss") + " (Ergebnis " + lastNudgeResult + ")"));
        if (lastError != "") sb.AppendLine("Fehler: " + lastError);
        MessageBox.Show(sb.ToString(), "LM Studio Trigger - Status");
    }

    void ExitApp()
    {
        timer.Stop();
        tray.Visible = false;
        tray.Dispose();
        ExitThread();
    }

    static Icon MakeIcon(Color c)
    {
        using (Bitmap bmp = new Bitmap(16, 16))
        {
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (SolidBrush b = new SolidBrush(c)) g.FillEllipse(b, 1, 1, 14, 14);
                using (Font f = new Font("Arial", 7f, FontStyle.Bold))
                using (SolidBrush w = new SolidBrush(Color.White))
                    g.DrawString("AI", f, w, 1.5f, 2.5f);
            }
            IntPtr h = bmp.GetHicon();
            Icon tmp = Icon.FromHandle(h);
            Icon ic = (Icon)tmp.Clone();
            DestroyIcon(h);
            return ic;
        }
    }
}
