// PilGostergesi - kablosuz kulaklik / mouse pil seviyesini gorev cubugu tepsisinde gosterir.
// Derleme: derle.bat  (Windows'taki .NET Framework csc.exe, C# 5)
// Protokol notlari (yeniden yazildi, kod kopyalanmadi):
//   HyperX Cloud III S Wireless (03F0:02CC / 06BE): 64/64 baytlik koleksiyon, rapor 0C [02 03 01 00 cmd],
//     cevap [5]=cmd [6]=deger (FF gecersiz). 02 -> 2=bagli, 06 -> %, 48 -> 1 sarj / 2 dolu.  (HyperHeadset)
//   ATK / VXE NK mouse NANO dongle (373B:1216): FF05:0001 koleksiyonu, rapor 08 64 bayt
//     [08][checksum][72][02][00][01][cmd][01]..., checksum = sum(2..63)&FF. cmd 07 = %, 0F = sarj.
//     Cevap [08][72][durum][..][..][cmd][01][deger], durum FF = hata. Cevap yok = mouse uykuda. (HaloBattery #350)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

static class Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct HIDD_ATTRIBUTES { public int Size; public ushort VendorID; public ushort ProductID; public ushort VersionNumber; }

    [StructLayout(LayoutKind.Sequential)]
    public struct HIDP_CAPS
    {
        public ushort Usage, UsagePage, InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
        public ushort NumberLinkCollectionNodes, NumberInputButtonCaps, NumberInputValueCaps, NumberInputDataIndices,
            NumberOutputButtonCaps, NumberOutputValueCaps, NumberOutputDataIndices,
            NumberFeatureButtonCaps, NumberFeatureValueCaps, NumberFeatureDataIndices;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SP_DEVICE_INTERFACE_DATA { public int cbSize; public Guid InterfaceClassGuid; public int Flags; public IntPtr Reserved; }

    [DllImport("hid.dll")] public static extern void HidD_GetHidGuid(out Guid g);
    [DllImport("hid.dll")] public static extern bool HidD_GetAttributes(SafeFileHandle h, ref HIDD_ATTRIBUTES a);
    [DllImport("hid.dll")] public static extern bool HidD_GetPreparsedData(SafeFileHandle h, out IntPtr p);
    [DllImport("hid.dll")] public static extern bool HidD_FreePreparsedData(IntPtr p);
    [DllImport("hid.dll")] public static extern int HidP_GetCaps(IntPtr p, ref HIDP_CAPS c);
    [DllImport("hid.dll")] public static extern bool HidD_FlushQueue(SafeFileHandle h);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr SetupDiGetClassDevs(ref Guid g, IntPtr enumerator, IntPtr hwnd, int flags);
    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr info, ref Guid g, int idx, ref SP_DEVICE_INTERFACE_DATA d);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref SP_DEVICE_INTERFACE_DATA d, IntPtr detail, int size, out int req, IntPtr info);
    [DllImport("setupapi.dll")] public static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr sec, uint disp, uint flags, IntPtr tmpl);
    [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr h);

    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }
    [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int W, H; public SIZE(int w, int h) { W = w; H = h; } }
    [StructLayout(LayoutKind.Sequential, Pack = 1)] public struct BLENDFUNCTION { public byte Op, Flags, Alpha, Format; }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string win);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string win);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
    [DllImport("user32.dll")] public static extern bool UpdateLayeredWindow(IntPtr h, IntPtr dcDst, ref POINT dst, ref SIZE size,
        IntPtr dcSrc, ref POINT src, int key, ref BLENDFUNCTION blend, int flags);
    [DllImport("user32.dll")] public static extern IntPtr SetParent(IntPtr child, IntPtr parent);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll", SetLastError = true)] public static extern IntPtr RegisterDeviceNotification(IntPtr recipient, IntPtr filter, int flags);
    [StructLayout(LayoutKind.Sequential)]
    public struct DEV_BROADCAST_DEVICEINTERFACE { public int Size; public int DeviceType; public int Reserved; public Guid ClassGuid; public short Name; }
    [DllImport("user32.dll")] public static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);
    [DllImport("user32.dll")] public static extern IntPtr GetWindowLongPtr(IntPtr h, int i);

    public const int WS_CHILD = unchecked((int)0x40000000), WS_VISIBLE = 0x10000000, WS_POPUP = unchecked((int)0x80000000);
    public const int WS_EX_LAYERED = 0x80000, WS_EX_NOACTIVATE = 0x8000000, WS_EX_TOOLWINDOW = 0x80;
    public const int GWL_STYLE = -16, WM_LBUTTONUP = 0x0202, WM_RBUTTONUP = 0x0205;
}

class HidColl
{
    public string Path;
    public ushort Vid, Pid, UsagePage, Usage, InLen, OutLen;
    public override string ToString()
    {
        return string.Format("{0:X4}:{1:X4} page {2:X4} usage {3:X4} in {4} out {5}  {6}", Vid, Pid, UsagePage, Usage, InLen, OutLen, Path);
    }

    public static List<HidColl> Enumerate()
    {
        var list = new List<HidColl>();
        Guid g; Native.HidD_GetHidGuid(out g);
        IntPtr set = Native.SetupDiGetClassDevs(ref g, IntPtr.Zero, IntPtr.Zero, 0x12); // PRESENT | DEVICEINTERFACE
        if (set == IntPtr.Zero || set == new IntPtr(-1)) return list;
        try
        {
            for (int i = 0; ; i++)
            {
                var d = new Native.SP_DEVICE_INTERFACE_DATA();
                d.cbSize = Marshal.SizeOf(d);
                if (!Native.SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref g, i, ref d)) break;
                int req;
                Native.SetupDiGetDeviceInterfaceDetail(set, ref d, IntPtr.Zero, 0, out req, IntPtr.Zero);
                IntPtr buf = Marshal.AllocHGlobal(req);
                try
                {
                    Marshal.WriteInt32(buf, IntPtr.Size == 8 ? 8 : 6);
                    if (!Native.SetupDiGetDeviceInterfaceDetail(set, ref d, buf, req, out req, IntPtr.Zero)) continue;
                    string path = Marshal.PtrToStringUni(new IntPtr(buf.ToInt64() + 4));
                    var c = Probe(path);
                    if (c != null) list.Add(c);
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
        }
        finally { Native.SetupDiDestroyDeviceInfoList(set); }
        return list;
    }

    static HidColl Probe(string path)
    {
        using (var h = Native.CreateFile(path, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero))
        {
            if (h.IsInvalid) return null;
            var a = new Native.HIDD_ATTRIBUTES(); a.Size = Marshal.SizeOf(a);
            if (!Native.HidD_GetAttributes(h, ref a)) return null;
            IntPtr pp;
            if (!Native.HidD_GetPreparsedData(h, out pp)) return null;
            var caps = new Native.HIDP_CAPS();
            try { Native.HidP_GetCaps(pp, ref caps); }
            finally { Native.HidD_FreePreparsedData(pp); }
            return new HidColl
            {
                Path = path, Vid = a.VendorID, Pid = a.ProductID, UsagePage = caps.UsagePage, Usage = caps.Usage,
                InLen = caps.InputReportByteLength, OutLen = caps.OutputReportByteLength
            };
        }
    }
}

class HidDev : IDisposable
{
    SafeFileHandle h;
    FileStream fs;
    public HidColl C;

    public static HidDev Open(HidColl c)
    {
        var h = Native.CreateFile(c.Path, 0xC0000000, 3, IntPtr.Zero, 3, 0x40000000, IntPtr.Zero); // RW, overlapped
        if (h.IsInvalid) { h.Dispose(); return null; }
        var d = new HidDev();
        d.h = h; d.C = c;
        d.fs = new FileStream(h, FileAccess.ReadWrite, 1, true);
        return d;
    }

    public void Flush() { Native.HidD_FlushQueue(h); }

    public bool Write(byte[] b)
    {
        try
        {
            var t = fs.WriteAsync(b, 0, b.Length);
            return t.Wait(1000) && !t.IsFaulted;
        }
        catch { return false; }
    }

    // Zaman asimi icinde bir input raporu okur; yoksa null.
    public byte[] Read(int ms)
    {
        if (ms <= 0) return null;
        var buf = new byte[C.InLen];
        try
        {
            var t = fs.ReadAsync(buf, 0, buf.Length);
            if (!t.Wait(ms)) return null;
            int n = t.Result;
            if (n <= 0) return null;
            if (n < buf.Length) Array.Resize(ref buf, n);
            return buf;
        }
        catch { return null; }
    }

    public void Dispose()
    {
        try { fs.Dispose(); } catch { }
        try { h.Dispose(); } catch { }
    }
}

class Reading
{
    public bool Connected;   // cihaz cevap verdi mi (uyku/kapali degil)
    public int Percent = -1;
    public bool Charging;
}

abstract class Driver
{
    public string Key, Name, Kind;
    public abstract bool Match(HidColl c);
    public abstract Reading Query(HidDev d);   // null = bu koleksiyon degil
}

class HyperXCloud3S : Driver
{
    public HyperXCloud3S() { Key = "hyperx-cloud3s"; Name = "HyperX Cloud III S"; Kind = "Kulaklık"; }

    public override bool Match(HidColl c)
    {
        return c.Vid == 0x03F0 && (c.Pid == 0x02CC || c.Pid == 0x06BE) && c.InLen == 64 && c.OutLen == 64;
    }

    int Q(HidDev d, byte cmd)
    {
        var req = new byte[d.C.OutLen];
        req[0] = 0x0C; req[1] = 0x02; req[2] = 0x03; req[3] = 0x01; req[4] = 0x00; req[5] = cmd;
        d.Flush();
        long end = Environment.TickCount + 1000;
        if (!d.Write(req)) return -1;
        while (true)
        {
            var r = d.Read((int)(end - Environment.TickCount));
            if (r == null) return -1;
            if (r.Length >= 7 && r[0] == 0x0C && r[5] == cmd && r[6] != 0xFF) return r[6];
        }
    }

    public override Reading Query(HidDev d)
    {
        int link = Q(d, 0x02);
        if (link < 0) return null;
        var rd = new Reading { Connected = link == 2 };
        if (!rd.Connected) return rd;
        int p = Q(d, 0x06);
        if (p >= 0 && p <= 100) rd.Percent = p;
        int ch = Q(d, 0x48);
        rd.Charging = ch == 1 || ch == 2;
        return rd;
    }
}

class AtkNkMouse : Driver
{
    public AtkNkMouse() { Key = "atk-nk"; Name = "ATK / VXE NK mouse"; Kind = "Mouse"; }

    public override bool Match(HidColl c)
    {
        return c.Vid == 0x373B && c.Pid == 0x1216 && c.UsagePage == 0xFF05 && c.Usage == 0x0001 && c.OutLen >= 64 && c.InLen >= 8;
    }

    int Q(HidDev d, byte cmd)
    {
        var req = new byte[d.C.OutLen];
        req[0] = 0x08; req[2] = 0x72; req[3] = 0x02; req[5] = 0x01; req[6] = cmd; req[7] = 0x01;
        int sum = 0;
        for (int i = 2; i < req.Length; i++) sum += req[i];
        req[1] = (byte)(sum & 0xFF);
        d.Flush();
        if (!d.Write(req)) return -1;
        long end = Environment.TickCount + 700;
        while (true)
        {
            var r = d.Read((int)(end - Environment.TickCount));
            if (r == null) return -1;
            if (r.Length < 8 || r[0] != 0x08 || r[1] != 0x72 || r[5] != cmd) continue;
            if (r[2] == 0xFF) return -1;
            return r[7];
        }
    }

    public override Reading Query(HidDev d)
    {
        // Dongle takili ama mouse uykudaysa cevap gelmez: yine de "var ama uykuda" say.
        var rd = new Reading();
        int lvl = Q(d, 0x07);
        if (lvl >= 1 && lvl <= 100)
        {
            rd.Connected = true; rd.Percent = lvl;
            rd.Charging = Q(d, 0x0F) == 1;
        }
        return rd;
    }
}

// Logitech kablosuz mouse (G Pro X Superlight vb.) - HID++ 2.0 protokolu.
// Lightspeed alici (046D:C547 gibi) ya da dogrudan baglanti. Uzun koleksiyon FF00:0002, rapor 0x11 (20 bayt).
// Mesaj: [0x11][cihaz indeksi][feature indeksi][(fn<<4)|swId][parametreler...], swId=0x0D.
// Pil: feature 0x1004 (UnifiedBattery) -> yoksa 0x1000 (BatteryStatus) -> yoksa 0x1001 (voltaj).
// Protokol referansi: Logitech HID++ 2.0 taslagi, Solaar. Kod sifirdan yazildi.
class LogitechMouse : Driver
{
    const byte SwId = 0x0D;
    public LogitechMouse() { Key = "logi-mouse"; Name = "Logitech mouse"; Kind = "Mouse"; }

    public override bool Match(HidColl c)
    {
        return c.Vid == 0x046D && c.UsagePage == 0xFF00 && c.Usage == 0x0002 && c.OutLen >= 20 && c.InLen >= 20;
    }

    // HID++ cagrisi: cevabin parametre kismini (r[4..]) dondurur; hata/zaman asiminda null.
    byte[] Call(HidDev d, byte idx, byte featIdx, byte fn, byte[] par)
    {
        var req = new byte[d.C.OutLen];
        req[0] = 0x11; req[1] = idx; req[2] = featIdx; req[3] = (byte)((fn << 4) | SwId);
        if (par != null) for (int i = 0; i < par.Length && 4 + i < req.Length; i++) req[4 + i] = par[i];
        d.Flush();
        if (!d.Write(req)) return null;
        long end = Environment.TickCount + 500;
        while (true)
        {
            var r = d.Read((int)(end - Environment.TickCount));
            if (r == null) return null;
            if (r.Length < 5) continue;
            // Cihaz uzun (0x11) isteğe kısa (0x10) ya da uzun raporla cevap verebilir: ikisini de kabul et.
            if ((r[0] == 0x11 || r[0] == 0x10) && r[1] == idx && r[2] == featIdx && r[3] == req[3])
            {
                var p = new byte[r.Length - 4];
                Array.Copy(r, 4, p, 0, p.Length);
                return p;
            }
            if ((r[2] == 0xFF || r[2] == 0x8F) && r.Length >= 6 && r[3] == featIdx && r[4] == req[3]) return null; // hata cevabi
        }
    }

    bool Ping(HidDev d, byte idx)
    {
        var p = Call(d, idx, 0x00, 1, new byte[] { 0, 0, 0x5A }); // Root ping
        return p != null && p.Length >= 3 && p[2] == 0x5A && p[0] >= 2;
    }

    byte FeatureIndex(HidDev d, byte idx, ushort feat)
    {
        var p = Call(d, idx, 0x00, 0, new byte[] { (byte)(feat >> 8), (byte)feat }); // Root getFeature
        return (p != null && p.Length >= 1) ? p[0] : (byte)0;
    }

    static readonly int[] LiMv = { 4186, 4067, 3989, 3922, 3859, 3811, 3778, 3751, 3717, 3671, 3646, 3579, 3500 };
    static int LiIon(int mv)
    {
        if (mv >= LiMv[0]) return 100;
        for (int i = 0; i < LiMv.Length - 1; i++)
            if (mv >= LiMv[i + 1]) { int hi = 100 - i * 10, lo = 100 - (i + 1) * 10; return lo + (hi - lo) * (mv - LiMv[i + 1]) / (LiMv[i] - LiMv[i + 1]); }
        return 0;
    }

    Reading ReadBattery(HidDev d, byte idx)
    {
        byte f;
        // 0x1004 UnifiedBattery
        if ((f = FeatureIndex(d, idx, 0x1004)) != 0)
        {
            var p = Call(d, idx, f, 1, new byte[0]);
            if (p != null && p.Length >= 3)
            {
                var rd = new Reading { Connected = true };
                if (p[0] >= 1 && p[0] <= 100) rd.Percent = p[0];
                else { int fl = p[1]; rd.Percent = fl >= 8 ? 100 : fl >= 4 ? 60 : fl >= 2 ? 20 : 5; }
                rd.Charging = p[2] == 1 || p[2] == 2 || p[2] == 3;
                return rd;
            }
        }
        // 0x1000 BatteryStatus
        if ((f = FeatureIndex(d, idx, 0x1000)) != 0)
        {
            var p = Call(d, idx, f, 0, new byte[0]);
            if (p != null && p.Length >= 3)
            {
                var rd = new Reading { Connected = true };
                if (p[0] <= 100) rd.Percent = p[0];
                int st = p[2]; rd.Charging = st >= 1 && st <= 4;
                if (rd.Percent == 0 && st == 3) rd.Percent = 100;
                return rd;
            }
        }
        // 0x1001 BatteryVoltage
        if ((f = FeatureIndex(d, idx, 0x1001)) != 0)
        {
            var p = Call(d, idx, f, 0, new byte[0]);
            if (p != null && p.Length >= 3)
            {
                int mv = (p[0] << 8) | p[1];
                return new Reading { Connected = true, Percent = LiIon(mv), Charging = (p[2] & 0x80) != 0 };
            }
        }
        return null;
    }

    public override Reading Query(HidDev d)
    {
        // Cihaz indeksi: once dogrudan (0xFF), sonra alici yuvalari 1..6.
        byte[] tryIdx = { 0xFF, 1, 2, 3, 4, 5, 6 };
        foreach (byte idx in tryIdx)
        {
            if (!Ping(d, idx)) continue;
            var rd = ReadBattery(d, idx);
            if (rd != null) return rd;
        }
        return new Reading();  // alici var ama cihaz uykuda/kapali
    }
}

class PollResult
{
    public Driver D;
    public bool Present;     // alici/kablosuz dongle takili
    public Reading R;        // null = okunamadi
}

static class Poller
{
    public static readonly Driver[] Drivers = { new HyperXCloud3S(), new AtkNkMouse(), new LogitechMouse() };

    public static List<PollResult> PollAll(StringBuilder log)
    {
        var colls = HidColl.Enumerate();
        var res = new List<PollResult>();
        foreach (var drv in Drivers)
        {
            var pr = new PollResult { D = drv };
            foreach (var c in colls)
            {
                if (!drv.Match(c)) continue;
                pr.Present = true;
                using (var d = HidDev.Open(c))
                {
                    if (d == null) { if (log != null) log.AppendLine("  acilamadi: " + c); continue; }
                    Reading r = null;
                    try { r = drv.Query(d); } catch (Exception e) { if (log != null) log.AppendLine("  hata: " + e.Message); }
                    if (log != null) log.AppendLine(string.Format("  {0} -> {1}", c, r == null ? "cevap yok" :
                        string.Format("bagli={0} yuzde={1} sarj={2}", r.Connected, r.Percent, r.Charging)));
                    if (r != null) { pr.R = r; if (r.Connected) break; }
                }
            }
            res.Add(pr);
        }
        return res;
    }
}

class WItem { public string Kind; public string Text; public Color Col; }

// Gorev cubugunun uzerine (gizli ^ menusune degil), TrayNotifyWnd'nin soluna yerlesen
// katmanli (seffaf arkaplan) cocuk pencere. Cihaz simgesi + yuzde cizer.
class TaskbarWidget : NativeWindow
{
    IntPtr tray, notify;
    public Action OnClick;
    List<WItem> items = new List<WItem>();

    public bool Create()
    {
        tray = Native.FindWindow("Shell_TrayWnd", null);
        if (tray == IntPtr.Zero) return false;
        notify = Native.FindWindowEx(tray, IntPtr.Zero, "TrayNotifyWnd", null);
        // Once ust-seviye katmanli pencere kur (WinForms capraz-process child'i dogrudan kuramiyor),
        // sonra WS_CHILD yapip SetParent ile gorev cubuguna bagla.
        var cp = new CreateParams
        {
            Style = Native.WS_POPUP,
            ExStyle = Native.WS_EX_LAYERED | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW
        };
        try { CreateHandle(cp); } catch (Exception e) { Log("Create EX: " + e.Message); return false; }
        if (Handle == IntPtr.Zero) return false;
        Native.SetWindowLongPtr(Handle, Native.GWL_STYLE, (IntPtr)(Native.WS_CHILD | Native.WS_VISIBLE));
        if (Native.SetParent(Handle, tray) == IntPtr.Zero) { Log("SetParent basarisiz"); DestroyHandle(); return false; }
        Log("Create ok tray=" + tray + " notify=" + notify + " handle=" + Handle);
        return true;
    }

    public static void Log(string s)
    {
        try { File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "widget.log"), DateTime.Now.ToString("HH:mm:ss ") + s + "\r\n"); } catch { }
    }

    public bool Alive { get { return Handle != IntPtr.Zero && Native.IsWindow(Handle) && Native.IsWindow(tray); } }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Native.WM_LBUTTONUP || m.Msg == Native.WM_RBUTTONUP)
        {
            if (OnClick != null) OnClick();
            return;
        }
        base.WndProc(ref m);
    }

    public void Render(List<WItem> list)
    {
        items = list;
        if (!Alive) return;
        if (notify == IntPtr.Zero || !Native.IsWindow(notify))
            notify = Native.FindWindowEx(tray, IntPtr.Zero, "TrayNotifyWnd", null);

        Native.RECT tr, nr;
        if (!Native.GetWindowRect(tray, out tr)) return;
        int taskH = tr.B - tr.T;
        bool light = TrayApp.LightTaskbar();
        Color fg = light ? Color.FromArgb(32, 32, 32) : Color.White;

        // Olcum
        int gap = 5, pad = 6, glyphW = (int)(taskH * 0.30f), h = taskH;
        var font = new Font("Segoe UI", taskH * 0.245f, FontStyle.Regular, GraphicsUnit.Pixel);
        int total = pad;
        using (var mb = new Bitmap(1, 1))
        using (var mg = Graphics.FromImage(mb))
        {
            foreach (var it in items)
            {
                int tw = (int)Math.Ceiling(mg.MeasureString(it.Text, font).Width);
                total += glyphW + 4 + tw + gap;
            }
        }
        total += pad - gap;
        if (items.Count == 0) total = 0;
        int w = Math.Max(total, 1);

        var bmp = new Bitmap(w, h);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.FromArgb(1, 0, 0, 0)); // gorunmez ama tiklanabilir arkaplan (alfa=0 pikseller tiklamayi gecirir)
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            int x = pad;
            foreach (var it in items)
            {
                var gr = new RectangleF(x, (h - glyphW) / 2f, glyphW, glyphW);
                DrawGlyph(g, it.Kind, gr, it.Col);
                x += glyphW + 4;
                var sf = new StringFormat { LineAlignment = StringAlignment.Center };
                int tw = (int)Math.Ceiling(g.MeasureString(it.Text, font).Width);
                using (var b = new SolidBrush(it.Col))
                    g.DrawString(it.Text, font, b, new RectangleF(x, 0, tw + 2, h), sf);
                x += tw + gap;
            }
        }
        font.Dispose();

        // Konum: TrayNotifyWnd'nin soluna, dikeyde ortala (tray client koordinati)
        int px, py = (taskH - h) / 2;
        if (notify != IntPtr.Zero && Native.GetWindowRect(notify, out nr))
            px = (nr.L - tr.L) - w - 4;
        else
            px = (tr.R - tr.L) - w - 160;
        if (px < 0) px = 0;

        Push(bmp, px, py, w, h);
        bmp.Dispose();
    }

    void Push(Bitmap bmp, int x, int y, int w, int h)
    {
        IntPtr screenDc = Native.GetDC(IntPtr.Zero);
        IntPtr memDc = Native.CreateCompatibleDC(screenDc);
        IntPtr hBmp = IntPtr.Zero, old = IntPtr.Zero;
        try
        {
            hBmp = bmp.GetHbitmap(Color.FromArgb(0));
            old = Native.SelectObject(memDc, hBmp);
            var dst = new Native.POINT(x, y);
            var size = new Native.SIZE(w, h);
            var src = new Native.POINT(0, 0);
            var blend = new Native.BLENDFUNCTION { Op = 0, Flags = 0, Alpha = 255, Format = 1 }; // AC_SRC_OVER / AC_SRC_ALPHA
            Native.UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, 2); // ULW_ALPHA
        }
        finally
        {
            Native.SelectObject(memDc, old);
            if (hBmp != IntPtr.Zero) Native.DeleteObject(hBmp);
            Native.DeleteDC(memDc);
            Native.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    static void DrawGlyph(Graphics g, string kind, RectangleF r, Color col)
    {
        float pw = Math.Max(1.4f, r.Height * 0.09f);
        using (var pen = new Pen(col, pw))
        using (var br = new SolidBrush(col))
        {
            pen.StartCap = pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
            if (kind == "Mouse")
            {
                float bw = r.Width * 0.60f, bx = r.X + (r.Width - bw) / 2;
                var body = new RectangleF(bx, r.Y, bw, r.Height);
                using (var p = Round(body, bw * 0.5f)) g.DrawPath(pen, p);
                g.DrawLine(pen, body.X + bw / 2, r.Y + r.Height * 0.14f, body.X + bw / 2, r.Y + r.Height * 0.40f);
            }
            else // Kulaklik
            {
                float d = r.Width;
                var band = new RectangleF(r.X + d * 0.08f, r.Y + d * 0.10f, d * 0.84f, d * 0.9f);
                g.DrawArc(pen, band, 180, 180);
                float cw = d * 0.20f, ch = d * 0.34f, cy = r.Y + d * 0.52f;
                using (var l = Round(new RectangleF(r.X + d * 0.06f, cy, cw, ch), cw * 0.4f)) g.FillPath(br, l);
                using (var rr = Round(new RectangleF(r.Right - d * 0.06f - cw, cy, cw, ch), cw * 0.4f)) g.FillPath(br, rr);
            }
        }
    }

    static System.Drawing.Drawing2D.GraphicsPath Round(RectangleF r, float rad)
    {
        float d = rad * 2;
        var p = new System.Drawing.Drawing2D.GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public void Destroy() { if (Handle != IntPtr.Zero) DestroyHandle(); }
}

// Kendi gizli üst-seviye penceresiyle cihaz takılma/çıkarma (WM_DEVICECHANGE) dinler.
// Not: şarj PRİZE (duvara) takılınca PC'de cihaz olayı olmaz; o durumu yakalayan tek şey sık yoklamadır.
class DevWatch : NativeWindow
{
    public Action OnChange;
    public void Start()
    {
        CreateHandle(new CreateParams { Caption = "PGDevWatch", X = -6000, Y = -6000, Width = 0, Height = 0, Style = unchecked((int)0x80000000) });
        var di = new Native.DEV_BROADCAST_DEVICEINTERFACE();
        di.Size = Marshal.SizeOf(di); di.DeviceType = 5; // DBT_DEVTYP_DEVICEINTERFACE
        IntPtr buf = Marshal.AllocHGlobal(di.Size);
        try { Marshal.StructureToPtr(di, buf, false); Native.RegisterDeviceNotification(Handle, buf, 0x04); } // ALL_INTERFACE_CLASSES
        finally { Marshal.FreeHGlobal(buf); }
    }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0219 && OnChange != null) OnChange(); // WM_DEVICECHANGE
        base.WndProc(ref m);
    }
}

class TrayApp : ApplicationContext
{
    const string RegKey = @"Software\PilGostergesi";
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    readonly Control ui = new Control();
    readonly ContextMenuStrip menu = new ContextMenuStrip();
    readonly Dictionary<string, NotifyIcon> icons = new Dictionary<string, NotifyIcon>();
    readonly Dictionary<string, IntPtr> iconHandles = new Dictionary<string, IntPtr>();
    readonly Dictionary<string, int> lastPercent = new Dictionary<string, int>();
    readonly HashSet<string> warned = new HashSet<string>();
    readonly NotifyIcon mainIcon = new NotifyIcon();
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    readonly System.Windows.Forms.Timer repos = new System.Windows.Forms.Timer();
    List<PollResult> last = new List<PollResult>();
    List<WItem> lastItems = new List<WItem>();
    TaskbarWidget widget;
    HashSet<string> hidden;
    bool polling;
    int menuClosedAt;
    Form menuOwner;
    DevWatch devWatch;
    readonly System.Windows.Forms.Timer devTimer = new System.Windows.Forms.Timer();

    public TrayApp()
    {
        ui.CreateControl();
        var h = ui.Handle; // UI thread'e baglanmak icin
        hidden = LoadHidden();
        menu.Opening += (s, e) => BuildMenu();
        menu.Closed += (s, e) => menuClosedAt = Environment.TickCount;

        // Ekran disinda, gorunmez 1x1 sahip pencere: menunun foreground alip disari tiklayinca
        // kapanabilmesi icin (tepsi menulerinin kullandigi yontem).
        menuOwner = new Form
        {
            FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual, Location = new Point(-4000, -4000), Size = new Size(1, 1)
        };
        menuOwner.Show();

        // Cihaz takılma/çıkarma: gelen olaylar çok olur, 1.5 sn bekleyip tek sefer yenile (pil durumu otursun).
        devTimer.Interval = 1500;
        devTimer.Tick += (s, e) => { devTimer.Stop(); Poll(); };
        devWatch = new DevWatch { OnChange = () => { devTimer.Stop(); devTimer.Start(); } };
        devWatch.Start();

        mainIcon.Icon = SystemIcons.Information;
        mainIcon.Text = "Pil Göstergesi";
        mainIcon.ContextMenuStrip = menu;
        mainIcon.MouseClick += IconClick;
        mainIcon.Visible = false;

        widget = new TaskbarWidget();
        if (widget.Create())
            widget.OnClick = () => ui.BeginInvoke(new Action(ShowMenuAtCursor));
        else
            widget = null;

        timer.Interval = 10000;   // 10 sn: priz/kablo şarjı dongle bildirince hızlı yakalansın
        timer.Tick += (s, e) => Poll();
        timer.Start();

        // Gorev cubugu yeniden ciziminden sonra (Explorer yeniden baslama, cozunurluk) konumu koru
        repos.Interval = 3000;
        repos.Tick += (s, e) =>
        {
            // Explorer yeniden baslayinca eski widget (eski gorev cubugunun cocugu) olur; yenisini kur.
            if (widget == null || !widget.Alive)
            {
                var w = new TaskbarWidget();
                if (w.Create()) { w.OnClick = () => ui.BeginInvoke(new Action(ShowMenuAtCursor)); widget = w; }
            }
            if (widget != null && widget.Alive) widget.Render(lastItems);
        };
        repos.Start();
        SystemEvents.PowerModeChanged += (s, e) =>
        {
            if (e.Mode == PowerModes.Resume) ui.BeginInvoke(new Action(() => { Thread.Sleep(3000); Poll(); }));
        };
        Poll();
    }

    void IconClick(object s, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        var mi = typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
        if (mi != null) mi.Invoke(s, null);
    }

    void ShowMenuAtCursor()
    {
        // Menu aciksa ya da yeni kapandiysa, tiklama onu kapatmistir: tekrar acma.
        if (menu.Visible) { menu.Close(); return; }
        if (Environment.TickCount - menuClosedAt < 300) return;
        BuildMenu();
        Native.SetForegroundWindow(menuOwner.Handle);   // uygulamayi one al: disari tiklayinca menu kapansin
        menu.Show(Cursor.Position);
        Native.PostMessage(menuOwner.Handle, 0, IntPtr.Zero, IntPtr.Zero); // WM_NULL: ilk disari tiklamanin islenmesi icin
    }

    void Poll()
    {
        if (polling) return;
        polling = true;
        Task.Factory.StartNew(() => Poller.PollAll(null)).ContinueWith(t =>
        {
            var r = t.IsFaulted ? new List<PollResult>() : t.Result;
            ui.BeginInvoke(new Action(() => { polling = false; Apply(r); }));
        });
    }

    public static bool LightTaskbar()
    {
        try
        {
            using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
            {
                object v = k == null ? null : k.GetValue("SystemUsesLightTheme");
                return v is int && (int)v == 1;
            }
        }
        catch { return false; }
    }

    // pr -> (pct, awake, renk) ortak hesabi
    bool Eval(PollResult pr, out int pct, out bool awake, out Color col)
    {
        string key = pr.D.Key;
        awake = pr.R != null && pr.R.Connected && pr.R.Percent >= 0;
        pct = -1;
        if (awake) { pct = pr.R.Percent; lastPercent[key] = pct; }
        else if (lastPercent.ContainsKey(key)) pct = lastPercent[key];
        if (!awake) col = Color.FromArgb(150, 150, 150);
        else if (pr.R.Charging) col = Color.FromArgb(74, 222, 128);
        else if (pct <= 15) col = Color.FromArgb(248, 113, 113);
        else col = LightTaskbar() ? Color.FromArgb(32, 32, 32) : Color.White;
        return awake;
    }

    void Apply(List<PollResult> results)
    {
        last = results;

        if (widget != null && widget.Alive)
        {
            var items = new List<WItem>();
            foreach (var pr in results)
            {
                if (!pr.Present || hidden.Contains(pr.D.Key)) continue;
                int pct; bool awake; Color col;
                Eval(pr, out pct, out awake, out col);
                items.Add(new WItem { Kind = pr.D.Kind, Col = col, Text = pct >= 0 ? pct + "%" : "–" });
            }
            lastItems = items;
            widget.Render(items);
            foreach (var pr in results) LowWarn(pr);
            return;
        }

        // Yedek: katmanli pencere kurulamadiysa cihaz basi tepsi simgesi
        var keep = new HashSet<string>();
        foreach (var pr in results)
        {
            if (!pr.Present || hidden.Contains(pr.D.Key)) continue;
            keep.Add(pr.D.Key);
            string key = pr.D.Key;
            int pct; bool awake; Color col;
            Eval(pr, out pct, out awake, out col);

            string tip = pr.D.Name + ": " + (pct >= 0 ? "%" + pct : "?");
            if (!awake) tip += " (uykuda / kapalı)";
            else if (pr.R.Charging) tip += " (şarj oluyor)";
            if (tip.Length > 63) tip = tip.Substring(0, 63);

            NotifyIcon ni;
            if (!icons.TryGetValue(key, out ni))
            {
                ni = new NotifyIcon { ContextMenuStrip = menu };
                ni.MouseClick += IconClick;
                icons[key] = ni;
            }
            SetIcon(key, ni, pct >= 0 ? pct.ToString() : "–", col);
            ni.Text = tip;
            ni.Visible = true;
            LowWarn(pr);
        }
        foreach (var key in new List<string>(icons.Keys))
        {
            if (keep.Contains(key)) continue;
            icons[key].Visible = false;
            icons[key].Dispose();
            icons.Remove(key);
            FreeIcon(key);
        }
        mainIcon.Visible = icons.Count == 0;
    }

    void LowWarn(PollResult pr)
    {
        if (!pr.Present || hidden.Contains(pr.D.Key)) return;
        string key = pr.D.Key;
        bool awake = pr.R != null && pr.R.Connected && pr.R.Percent >= 0;
        if (awake && !pr.R.Charging && pr.R.Percent <= 15 && !warned.Contains(key))
        {
            warned.Add(key);
            mainIcon.Visible = true;
            mainIcon.ShowBalloonTip(5000, "Pil azaldı", pr.D.Name + " %" + pr.R.Percent, ToolTipIcon.Warning);
        }
        if (awake && (pr.R.Charging || pr.R.Percent > 20)) warned.Remove(key);
    }

    void FreeIcon(string key)
    {
        IntPtr old;
        if (iconHandles.TryGetValue(key, out old)) { Native.DestroyIcon(old); iconHandles.Remove(key); }
    }

    void SetIcon(string key, NotifyIcon ni, string text, Color col)
    {
        int s = SystemInformation.SmallIconSize.Width;
        using (var bmp = new Bitmap(s, s))
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            var sf = new StringFormat(StringFormat.GenericTypographic) { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            float size = s * (text.Length >= 3 ? 0.62f : 0.82f);
            Font f = new Font("Segoe UI", size, FontStyle.Bold, GraphicsUnit.Pixel);
            while (g.MeasureString(text, f, 1000, sf).Width > s + 1 && size > 6)
            {
                f.Dispose(); size -= 0.5f;
                f = new Font("Segoe UI", size, FontStyle.Bold, GraphicsUnit.Pixel);
            }
            using (var b = new SolidBrush(col)) g.DrawString(text, f, b, new RectangleF(0, 0, s, s + 1), sf);
            f.Dispose();
            IntPtr h = bmp.GetHicon();
            var oldIcon = ni.Icon;
            ni.Icon = Icon.FromHandle(h);
            FreeIcon(key);
            iconHandles[key] = h;
        }
    }

    void BuildMenu()
    {
        menu.Items.Clear();
        bool any = false;
        foreach (var pr in last)
        {
            if (!pr.Present) continue;
            any = true;
            string val = "bulunamadı";
            if (pr.R != null && pr.R.Connected && pr.R.Percent >= 0)
                val = "%" + pr.R.Percent + (pr.R.Charging ? " ⚡" : "");
            else val = "uykuda";
            var it = new ToolStripMenuItem(pr.D.Kind + " – " + pr.D.Name + "   " + val);
            it.Checked = !hidden.Contains(pr.D.Key);
            string key = pr.D.Key;
            it.Click += (s, e) =>
            {
                if (hidden.Contains(key)) hidden.Remove(key); else hidden.Add(key);
                SaveHidden();
                Apply(last);
            };
            menu.Items.Add(it);
        }
        if (!any) menu.Items.Add(new ToolStripMenuItem("Desteklenen cihaz bulunamadı") { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Şimdi yenile", null, (s, e) => Poll());
        var auto = new ToolStripMenuItem("Windows ile başlat") { Checked = AutoStart() };
        auto.Click += (s, e) => SetAutoStart(!AutoStart());
        menu.Items.Add(auto);
        menu.Items.Add("Çıkış", null, (s, e) => Quit());
    }

    static HashSet<string> LoadHidden()
    {
        var set = new HashSet<string>();
        try
        {
            using (var k = Registry.CurrentUser.OpenSubKey(RegKey))
            {
                var v = k == null ? null : k.GetValue("Gizli") as string;
                if (v != null) foreach (var p in v.Split(',')) if (p.Length > 0) set.Add(p);
            }
        }
        catch { }
        return set;
    }

    void SaveHidden()
    {
        using (var k = Registry.CurrentUser.CreateSubKey(RegKey)) k.SetValue("Gizli", string.Join(",", hidden));
    }

    static bool AutoStart()
    {
        using (var k = Registry.CurrentUser.OpenSubKey(RunKey)) return k != null && k.GetValue("PilGostergesi") != null;
    }

    static void SetAutoStart(bool on)
    {
        using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
        {
            if (on) k.SetValue("PilGostergesi", "\"" + Application.ExecutablePath + "\"");
            else k.DeleteValue("PilGostergesi", false);
        }
    }

    void Quit()
    {
        timer.Stop(); repos.Stop(); devTimer.Stop();
        if (devWatch != null && devWatch.Handle != IntPtr.Zero) devWatch.DestroyHandle();
        if (widget != null) widget.Destroy();
        if (menuOwner != null) menuOwner.Dispose();
        foreach (var ni in icons.Values) { ni.Visible = false; ni.Dispose(); }
        foreach (var h in iconHandles.Values) Native.DestroyIcon(h);
        mainIcon.Visible = false; mainIcon.Dispose();
        ExitThread();
    }
}

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--test")
        {
            var sb = new StringBuilder();
            try
            {
                sb.AppendLine("Ilgili HID koleksiyonlari:");
                foreach (var c in HidColl.Enumerate())
                    if (c.Vid == 0x03F0 || c.Vid == 0x373B || c.Vid == 0x046D) sb.AppendLine("  " + c);
                sb.AppendLine("Okumalar:");
                Poller.PollAll(sb);

                // Logitech ham teshis: her FF00:0002 koleksiyonunda indekslere Root ping gonder, ham cevabi yaz.
                sb.AppendLine("Logitech ham ping (046D):");
                foreach (var c in HidColl.Enumerate())
                {
                    if (!(c.Vid == 0x046D && c.UsagePage == 0xFF00 && c.Usage == 0x0002 && c.OutLen >= 20)) continue;
                    sb.AppendLine("  koleksiyon: " + c);
                    using (var d = HidDev.Open(c))
                    {
                        if (d == null) { sb.AppendLine("    acilamadi"); continue; }
                        foreach (byte idx in new byte[] { 0xFF, 1, 2, 3 })
                        {
                            var req = new byte[c.OutLen];
                            req[0] = 0x11; req[1] = idx; req[2] = 0x00; req[3] = (1 << 4) | 0x0D; req[6] = 0x5A; // Root ping fn1
                            d.Flush(); d.Write(req);
                            long end = Environment.TickCount + 400; string got = "(cevap yok)";
                            while (Environment.TickCount < end)
                            {
                                var r = d.Read((int)(end - Environment.TickCount));
                                if (r == null) break;
                                var hex = new StringBuilder();
                                for (int i = 0; i < Math.Min(r.Length, 8); i++) hex.Append(r[i].ToString("X2") + " ");
                                got = hex.ToString().Trim(); break;
                            }
                            sb.AppendLine("    idx " + idx.ToString("X2") + " -> " + got);
                        }
                    }
                }
            }
            catch (Exception ex) { sb.AppendLine("HATA: " + ex); }

            // test.txt'yi hem masaustune hem exe yanina yaz; kullaniciya bildir.
            string path1 = null;
            try { path1 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "test.txt"); File.WriteAllText(path1, sb.ToString(), Encoding.UTF8); } catch { }
            try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test.txt"), sb.ToString(), Encoding.UTF8); } catch { }
            try { MessageBox.Show("Test bitti.\ntest.txt masaustune yazildi:\n" + (path1 ?? "(masaustu yazilamadi)") + "\n\nBu dosyayi gonder.", "PilGostergesi --test"); } catch { }
            return;
        }

        bool created;
        using (var m = new Mutex(true, "PilGostergesi_tek", out created))
        {
            if (!created) return;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayApp());
        }
    }
}
