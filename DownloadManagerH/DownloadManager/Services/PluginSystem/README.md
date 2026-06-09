# راهنمای اتصال افزونه‌ها به TrafficWatch از طریق Named Pipe

## معرفی
این سند نحوه استریم داده‌های زنده (مانند اطلاعات موسیقی در حال پخش) از برنامه‌های شخص ثالث به داشبورد TrafficWatch را توضیح می‌دهد.

## معماری ارتباطی

```
┌─────────────────┐     Named Pipe      ┌──────────────────┐
│  برنامه افزونه  │ ◄─────────────────► │   TrafficWatch   │
│  (Music Player) │    TrafficWatch     │   (Server)       │
│                 │    PluginPipe       │                  │
└─────────────────┘                     └──────────────────┘
       │                                        │
       │ 1. اتصال و ثبت نام                    │ 1. گوش دادن به پایپ
       │ 2. ارسال استریم داده                  │ 2. پردازش داده‌ها
       │ 3. Heartbeat                           │ 3. بروزرسانی داشبورد
```

## سناریوی اجرا

### مرحله 1: اجرای سرویس اصلی
به محض اجرای برنامه DownloadManager، سرور Named Pipe به صورت خودکار فعال می‌شود و روی پایپ `TrafficWatchPluginPipe` گوش می‌دهد.

### مرحله 2: اجرای برنامه افزونه (مثلاً پخش کننده موسیقی)
برنامه افزونه با استفاده از `PluginClient` به سرور متصل می‌شود:

```csharp
// در برنامه پخش کننده موسیقی
var plugin = new MusicStreamerPlugin();
await plugin.StartAsync();
```

### مرحله 3: ثبت نام خودکار
افزونه به صورت خودکار ثبت شده و در داشبورد TrafficWatch نمایش داده می‌شود.

### مرحله 4: استریم داده‌های موسیقی
اطلاعات آهنگ در حال پخش به صورت بلادرنگ به داشبورد ارسال می‌شود.

## پروتکل ارتباطی

### فرمت پیام‌ها

#### 1. درخواست ثبت نام (Register)
```json
{
  "action": "register",
  "name": "Music Player Plugin",
  "version": "2.0.0",
  "icon": "🎵"
}
```

**پاسخ:**
```json
{
  "action": "registered",
  "id": "a1b2c3d4",
  "status": "success"
}
```

#### 2. ارسال داده استریم (Stream Data)
```json
{
  "action": "stream_data",
  "timestamp": "2024-01-15T10:30:00.000Z",
  "payload": {
    "type": "now_playing",
    "title": "Bohemian Rhapsody",
    "artist": "Queen",
    "album": "A Night at the Opera",
    "duration": "5:55",
    "progress": 45,
    "isPlaying": true
  }
}
```

#### 3. Heartbeat
```json
{
  "action": "heartbeat"
}
```

## نمونه کد کامل افزونه موسیقی

```csharp
using System;
using System.Threading.Tasks;
using DownloadManagerH.Services.PluginSystem;

namespace MusicPlayerAddon
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("🎵 Music Player Addon Starting...");

            var plugin = new MusicStreamerPlugin();
            await plugin.StartAsync();

            Console.WriteLine("Press any key to stop...");
            Console.ReadKey();

            plugin.Stop();
        }
    }

    public class MusicStreamerPlugin
    {
        private PluginClient? _client;
        private System.Timers.Timer? _heartbeatTimer;
        private bool _isRunning;

        public async Task StartAsync()
        {
            // ایجاد کلاینت و اتصال به سرور
            _client = new PluginClient();
            _client.Connected += OnConnected;
            _client.Disconnected += OnDisconnected;
            _client.ResponseReceived += OnResponseReceived;

            await _client.ConnectAsync();

            // ثبت نام افزونه
            await _client.RegisterAsync("Music Player", "1.0.0", "🎵");

            // شروع حلقه Heartbeat
            _ = _client.StartHeartbeatLoopAsync();

            _isRunning = true;

            // شبیه‌سازی ارسال داده موسیقی
            await SimulateMusicStreaming();
        }

        private void OnConnected(object? sender, EventArgs e)
        {
            Console.WriteLine("✅ Connected to TrafficWatch");
        }

        private void OnDisconnected(object? sender, EventArgs e)
        {
            Console.WriteLine("❌ Disconnected from TrafficWatch");
        }

        private void OnResponseReceived(object? sender, ServerResponseEventArgs e)
        {
            if (e.Response.TryGetProperty("action", out var action))
            {
                Console.WriteLine($"Response: {action.GetString()}");
            }
        }

        private async Task SimulateMusicStreaming()
        {
            while (_isRunning)
            {
                // شبیه‌سازی تغییر آهنگ هر 10 ثانیه
                await Task.Delay(10000);

                var nowPlaying = new
                {
                    type = "now_playing",
                    title = GetRandomSong(),
                    artist = GetRandomArtist(),
                    album = GetRandomAlbum(),
                    duration = "4:30",
                    progress = 0,
                    isPlaying = true
                };

                await _client!.SendStreamDataAsync(nowPlaying);
                Console.WriteLine($"🎵 Now Playing: {nowPlaying.title} - {nowPlaying.artist}");
            }
        }

        private string GetRandomSong() => 
            new[] { "Bohemian Rhapsody", "Hotel California", "Imagine", "Hey Jude" }
            .OrderBy(_ => Guid.NewGuid()).First();

        private string GetRandomArtist() => 
            new[] { "Queen", "Eagles", "John Lennon", "The Beatles" }
            .OrderBy(_ => Guid.NewGuid()).First();

        private string GetRandomAlbum() => 
            new[] { "A Night at the Opera", "Hotel California", "Imagine", "Hey Jude" }
            .OrderBy(_ => Guid.NewGuid()).First();

        public void Stop()
        {
            _isRunning = false;
            _heartbeatTimer?.Stop();
            _client?.Disconnect();
            _client?.Dispose();
        }
    }
}
```

## رویدادهای DashboardAddonService

برای دریافت داده‌های استریم در داشبورد، از رویداد `DataReceived` در `NamedPipePluginServer` استفاده کنید:

```csharp
// در App.xaml.cs یا ViewModel داشبورد
_pluginServer.DataReceived += (sender, e) =>
{
    // e.AddonId: شناسه افزونه
    // e.Data: داده‌های JSON استریم شده

    var data = JsonDocument.Parse(e.Data);

    if (data.RootElement.TryGetProperty("type", out var typeElement))
    {
        var type = typeElement.GetString();
        
        if (type == "now_playing")
        {
            // بروزرسانی UI با اطلاعات موسیقی
            string title = data.RootElement.GetProperty("title").GetString() ?? "";
            string artist = data.RootElement.GetProperty("artist").GetString() ?? "";

            // آپدیت تب موسیقی در داشبورد
            UpdateMusicTab(title, artist);
        }
    }
};
```

## فایل‌های مرتبط

| فایل | توضیحات |
|------|---------|
| `NamedPipePluginServer.cs` | سرور Named Pipe در TrafficWatch |
| `PluginClient.cs` | کلاینت نمونه برای افزونه‌ها |
| `App.xaml.cs` | مقداردهی اولیه سرور Named Pipe |

## نکات مهم

1. **امنیت**: Named Pipe فقط به کاربران محلی اجازه اتصال می‌دهد
2. **Performance**: داده‌های استریم باید سبک باشند (JSON کوچک)
3. **Heartbeat**: هر 30 ثانیه برای حفظ اتصال ارسال شود
4. **قطع اتصال**: در صورت قطع اتصال، افزونه از لیست حذف می‌شود

## عیب‌یابی

| مشکل | راه حل |
|------|--------|
| اتصال برقرار نمی‌شود | مطمئن شوید DownloadManager در حال اجرا است |
| داده‌ها نمایش داده نمی‌شوند | رویداد `DataReceived` را در Dashboard subscribe کنید |
| افزونه ثبت نمی‌شود | فرمت JSON درخواست register را بررسی کنید |

## گسترش آینده

- پشتیبانی از چندین Named Pipe برای افزونه‌های مختلف
- افزودن احراز هویت برای افزونه‌ها
- امکان ارسال فرمان از سرور به افزونه (Play, Pause, Stop)

## نکته مهم

اگر برنامه TrafficWatch نصب باشد، سرویس مربوطه را وصل می‌کند. در غیر این صورت برنامه به کار خود ادامه می‌دهد.
