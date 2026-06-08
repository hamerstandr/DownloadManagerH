using System;
using System.Windows.Threading;

namespace DownloadManagerH.Models
{
    public class ClipboardMonitor
    {
        private readonly DispatcherTimer timer;
        private string lastClipboardUrl = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public event Action<string>? NewUrlFound;
        public ClipboardMonitor()
        {
            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += Timer_Tick;
        }
        public void Start()
        {
            if (!timer.IsEnabled)
                timer.Start();
        }
        public void Stop()
        {
            if (timer.IsEnabled)
                timer.Stop();
        }
        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (!IsEnabled) return;
            try
            {
                var text = System.Windows.Clipboard.GetText();
                if (!string.IsNullOrWhiteSpace(text) && (text.StartsWith("http://") || text.StartsWith("https://")))
                {
                    if (text != lastClipboardUrl)
                    {
                        lastClipboardUrl = text;
                        NewUrlFound?.Invoke(text);
                    }
                }
            }
            catch { }
        }
    }
} 