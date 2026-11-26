using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.Win32;
using System.Web.Script.Serialization;

internal static class TCSRemoteAgent
{
    private const int Port = 8765;
    private static readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "TCS", "ChocoAI");
    private static readonly string WebRoot = Path.Combine(Root, "control-center");
    private static readonly string ConfigRoot = Path.Combine(Root, "config");
    private static readonly string LogRoot = Path.Combine(Root, "logs");
    private static readonly string TokenPath = Path.Combine(ConfigRoot, "agent.token");
    private static readonly string LogPath = Path.Combine(LogRoot, "remote-agent.log");
    private static readonly object LogLock = new object();
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 1024 * 1024 };
    private static string _token;

    private sealed class HttpRequest
    {
        public string Method;
        public string Path;
        public Dictionary<string, string> Headers;
        public byte[] Body;
    }

    private sealed class WindowInfo
    {
        public string handle { get; set; }
        public string title { get; set; }
        public string process { get; set; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public uint Low;
        public uint High;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MEMORYSTATUSEX
    {
        public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX buffer);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int maxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hwnd, int command);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    private static void Main()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(ConfigRoot);
        Directory.CreateDirectory(LogRoot);
        Directory.CreateDirectory(WebRoot);
        _token = LoadOrCreateToken();
        Log("TCS Remote Agent starting.");

        TcpListener listener = new TcpListener(IPAddress.Loopback, Port);
        try
        {
            listener.Start(16);
            Log("Listening on loopback port " + Port + ".");
        }
        catch (Exception ex)
        {
            Log("Listener start failed: " + ex.Message);
            Environment.Exit(2);
            return;
        }

        while (true)
        {
            try
            {
                TcpClient client = listener.AcceptTcpClient();
                ThreadPool.QueueUserWorkItem(delegate(object state)
                {
                    HandleClient((TcpClient)state);
                }, client);
            }
            catch (Exception ex)
            {
                Log("Accept failed: " + ex.Message);
                Thread.Sleep(1000);
            }
        }
    }

    private static string LoadOrCreateToken()
    {
        if (File.Exists(TokenPath))
        {
            string existing = File.ReadAllText(TokenPath, Encoding.UTF8).Trim();
            if (existing.Length >= 48) return existing;
        }

        byte[] bytes = new byte[32];
        using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
        {
            rng.GetBytes(bytes);
        }
        StringBuilder value = new StringBuilder(64);
        foreach (byte b in bytes) value.Append(b.ToString("x2"));
        string token = value.ToString();
        File.WriteAllText(TokenPath, token, new UTF8Encoding(false));
        try
        {
            File.SetAttributes(TokenPath, FileAttributes.Hidden);
        }
        catch { }
        Log("A new local access token was generated.");
        return token;
    }

    private static void HandleClient(TcpClient client)
    {
        using (client)
        {
            client.ReceiveTimeout = 5000;
            client.SendTimeout = 5000;
            try
            {
                NetworkStream stream = client.GetStream();
                HttpRequest request = ReadRequest(stream);
                if (request == null)
                {
                    WriteJson(stream, 400, new { ok = false, error = "Bad request." });
                    return;
                }

                string path = request.Path.Split('?')[0];
                if (request.Method == "GET" && path == "/health")
                {
                    WriteJson(stream, 200, new { ok = true, service = "TCS Remote Agent", authRequired = true });
                    return;
                }

                if (request.Method == "GET" && IsStaticPath(path))
                {
                    ServeStatic(stream, path);
                    return;
                }

                if (!Authorized(request))
                {
                    WriteJson(stream, 401, new { ok = false, error = "Unauthorized. Masukkan token agent." });
                    return;
                }

                if (request.Method == "POST" && path == "/api/auth/validate")
                {
                    WriteJson(stream, 200, new { ok = true, authenticated = true });
                    return;
                }
                if (request.Method == "GET" && path == "/api/status")
                {
                    WriteJson(stream, 200, GetStatus());
                    return;
                }
                if ((request.Method == "GET" && path == "/api/windows") ||
                    (request.Method == "POST" && path == "/api/actions/windows/list"))
                {
                    WriteJson(stream, 200, new { ok = true, items = GetWindows() });
                    return;
                }
                if (request.Method == "POST")
                {
                    HandleAction(stream, path);
                    return;
                }

                WriteJson(stream, 404, new { ok = false, error = "Endpoint tidak ditemukan." });
            }
            catch (Exception ex)
            {
                Log("Request error: " + ex.Message);
                try { WriteJson(client.GetStream(), 500, new { ok = false, error = "Internal agent error." }); } catch { }
            }
        }
    }

    private static HttpRequest ReadRequest(NetworkStream stream)
    {
        List<byte> header = new List<byte>();
        int matched = 0;
        byte[] end = new byte[] { 13, 10, 13, 10 };
        while (header.Count < 16384)
        {
            int next = stream.ReadByte();
            if (next < 0) return null;
            byte b = (byte)next;
            header.Add(b);
            if (b == end[matched]) matched++;
            else matched = b == end[0] ? 1 : 0;
            if (matched == 4) break;
        }
        if (matched != 4) return null;

        string raw = Encoding.ASCII.GetString(header.ToArray());
        string[] lines = raw.Split(new string[] { "\r\n" }, StringSplitOptions.None);
        if (lines.Length == 0) return null;
        string[] first = lines[0].Split(' ');
        if (first.Length < 2) return null;

        Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < lines.Length; i++)
        {
            int colon = lines[i].IndexOf(':');
            if (colon > 0)
                headers[lines[i].Substring(0, colon).Trim()] = lines[i].Substring(colon + 1).Trim();
        }

        int contentLength = 0;
        if (headers.ContainsKey("Content-Length"))
            Int32.TryParse(headers["Content-Length"], out contentLength);
        if (contentLength < 0 || contentLength > 8192) throw new InvalidDataException("Request body too large.");

        byte[] body = new byte[contentLength];
        int offset = 0;
        while (offset < contentLength)
        {
            int count = stream.Read(body, offset, contentLength - offset);
            if (count <= 0) throw new EndOfStreamException();
            offset += count;
        }

        return new HttpRequest { Method = first[0].ToUpperInvariant(), Path = first[1], Headers = headers, Body = body };
    }

    private static bool Authorized(HttpRequest request)
    {
        string value;
        if (!request.Headers.TryGetValue("Authorization", out value)) return false;
        const string prefix = "Bearer ";
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        return ConstantTimeEquals(value.Substring(prefix.Length).Trim(), _token);
    }

    private static bool ConstantTimeEquals(string a, string b)
    {
        if (a == null || b == null || a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }

    private static bool IsStaticPath(string path)
    {
        return path == "/" || path == "/index.html" || path == "/styles.css" || path == "/app.js";
    }

    private static void ServeStatic(NetworkStream stream, string path)
    {
        string file = path == "/" || path == "/index.html" ? "index.html" : path.Substring(1);
        string fullPath = Path.Combine(WebRoot, file);
        if (!File.Exists(fullPath))
        {
            WriteText(stream, 404, "text/plain; charset=utf-8", "Dashboard asset tidak ditemukan. Jalankan installer agent lagi.");
            return;
        }
        byte[] bytes = File.ReadAllBytes(fullPath);
        string type = file.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ? "text/css; charset=utf-8" :
                      file.EndsWith(".js", StringComparison.OrdinalIgnoreCase) ? "application/javascript; charset=utf-8" :
                      "text/html; charset=utf-8";
        WriteBytes(stream, 200, type, bytes);
    }

    private static void HandleAction(NetworkStream stream, string path)
    {
        if (path == "/api/actions/screen/off")
        {
            SendMessage(new IntPtr(0xffff), 0x0112, new IntPtr(0xf170), new IntPtr(2));
            Log("Display off action requested.");
            WriteJson(stream, 200, new { ok = true, message = "Perintah mematikan layar dikirim ke sesi desktop ASUS." });
            return;
        }
        if (path == "/api/actions/screen/on")
        {
            SendMessage(new IntPtr(0xffff), 0x0112, new IntPtr(0xf170), new IntPtr(-1));
            Log("Display on action requested.");
            WriteJson(stream, 200, new { ok = true, message = "Perintah menyalakan layar dikirim ke sesi desktop ASUS." });
            return;
        }
        if (path == "/api/actions/windows/minimize-active")
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero || !IsWindow(hwnd))
            {
                WriteJson(stream, 409, new { ok = false, error = "Tidak ada jendela aktif yang dapat diminimalkan." });
                return;
            }
            ShowWindow(hwnd, 6);
            WriteJson(stream, 200, new { ok = true, message = "Jendela aktif diminta untuk diminimalkan." });
            return;
        }
        if (path == "/api/actions/windows/maximize-active")
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero || !IsWindow(hwnd))
            {
                WriteJson(stream, 409, new { ok = false, error = "Tidak ada jendela aktif yang dapat dimaksimalkan." });
                return;
            }
            ShowWindow(hwnd, 3);
            WriteJson(stream, 200, new { ok = true, message = "Jendela aktif diminta untuk dimaksimalkan." });
            return;
        }
        if (path == "/api/actions/windows/close-active")
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero || !IsWindow(hwnd))
            {
                WriteJson(stream, 409, new { ok = false, error = "Tidak ada jendela aktif yang dapat ditutup." });
                return;
            }
            PostMessage(hwnd, 0x0010, IntPtr.Zero, IntPtr.Zero);
            Log("WM_CLOSE sent to active window.");
            WriteJson(stream, 200, new { ok = true, message = "Permintaan penutupan dikirim ke jendela aktif. Aplikasi dapat meminta konfirmasi penyimpanan." });
            return;
        }
        if (path == "/api/actions/system/refresh")
        {
            WriteJson(stream, 200, GetStatus());
            return;
        }
        WriteJson(stream, 404, new { ok = false, error = "Aksi tidak tersedia." });
    }

    private static object GetStatus()
    {
        MEMORYSTATUSEX memory = new MEMORYSTATUSEX();
        GlobalMemoryStatusEx(memory);
        double cpu = GetCpuLoad();
        string cpuName = "Tidak diketahui";
        try
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
            {
                if (key != null)
                {
                    object value = key.GetValue("ProcessorNameString");
                    if (value != null) cpuName = value.ToString().Trim();
                }
            }
        }
        catch { }

        bool aiRunning = Process.GetProcessesByName("llama-server").Length > 0;
        long totalMb = (long)(memory.ullTotalPhys / (1024 * 1024));
        long availableMb = (long)(memory.ullAvailPhys / (1024 * 1024));
        double used = totalMb > 0 ? Math.Round((double)(totalMb - availableMb) * 100.0 / totalMb, 1) : 0;

        return new
        {
            ok = true,
            host = new { hostname = Environment.MachineName, os = Environment.OSVersion.VersionString },
            hardware = new
            {
                cpu = new { name = cpuName, loadPercent = cpu },
                memory = new { totalMB = totalMb, availableMB = availableMb, usedPercent = used }
            },
            services = new { agent = "running", aiServer = aiRunning ? "running" : "stopped" },
            timestamp = DateTime.UtcNow.ToString("o")
        };
    }

    private static double GetCpuLoad()
    {
        try
        {
            FILETIME idle1, kernel1, user1, idle2, kernel2, user2;
            if (!GetSystemTimes(out idle1, out kernel1, out user1)) return 0;
            Thread.Sleep(250);
            if (!GetSystemTimes(out idle2, out kernel2, out user2)) return 0;
            ulong idle = ToUInt64(idle2) - ToUInt64(idle1);
            ulong kernel = ToUInt64(kernel2) - ToUInt64(kernel1);
            ulong user = ToUInt64(user2) - ToUInt64(user1);
            ulong total = kernel + user;
            if (total == 0) return 0;
            double value = (double)(total - idle) * 100.0 / total;
            return Math.Round(Math.Max(0, Math.Min(100, value)), 1);
        }
        catch { return 0; }
    }

    private static ulong ToUInt64(FILETIME value)
    {
        return ((ulong)value.High << 32) | value.Low;
    }

    private static List<WindowInfo> GetWindows()
    {
        List<WindowInfo> result = new List<WindowInfo>();
        EnumWindows(delegate(IntPtr hwnd, IntPtr lParam)
        {
            if (!IsWindowVisible(hwnd)) return true;
            int length = GetWindowTextLength(hwnd);
            if (length <= 0 || length > 1024) return true;
            StringBuilder title = new StringBuilder(length + 1);
            GetWindowText(hwnd, title, title.Capacity);
            string text = title.ToString().Trim();
            if (text.Length == 0) return true;

            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);
            string processName = "unknown";
            try
            {
                using (Process process = Process.GetProcessById((int)pid))
                    processName = process.ProcessName;
            }
            catch { }

            result.Add(new WindowInfo
            {
                handle = "0x" + hwnd.ToInt64().ToString("X"),
                title = text,
                process = processName
            });
            return result.Count < 100;
        }, IntPtr.Zero);
        return result;
    }

    private static void WriteJson(NetworkStream stream, int status, object value)
    {
        WriteText(stream, status, "application/json; charset=utf-8", Json.Serialize(value));
    }

    private static void WriteText(NetworkStream stream, int status, string contentType, string text)
    {
        WriteBytes(stream, status, contentType, Encoding.UTF8.GetBytes(text));
    }

    private static void WriteBytes(NetworkStream stream, int status, string contentType, byte[] body)
    {
        string reason = status == 200 ? "OK" : status == 401 ? "Unauthorized" :
                        status == 404 ? "Not Found" : status == 409 ? "Conflict" :
                        status == 413 ? "Payload Too Large" : status == 500 ? "Internal Server Error" : "Bad Request";
        string headers = "HTTP/1.1 " + status + " " + reason + "\r\n" +
                         "Content-Type: " + contentType + "\r\n" +
                         "Content-Length: " + body.Length + "\r\n" +
                         "Connection: close\r\n" +
                         "Cache-Control: no-store\r\n" +
                         "X-Content-Type-Options: nosniff\r\n" +
                         "Content-Security-Policy: default-src 'self'; style-src 'self'; script-src 'self'; connect-src 'self'; img-src 'self' data:\r\n\r\n";
        byte[] head = Encoding.ASCII.GetBytes(headers);
        stream.Write(head, 0, head.Length);
        if (body.Length > 0) stream.Write(body, 0, body.Length);
        stream.Flush();
    }

    private static void Log(string message)
    {
        try
        {
            lock (LogLock)
                File.AppendAllText(LogPath, DateTime.UtcNow.ToString("o") + " " + message + Environment.NewLine, Encoding.UTF8);
        }
        catch { }
    }
}
