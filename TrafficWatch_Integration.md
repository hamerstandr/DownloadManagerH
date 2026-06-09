# راهنمای اتصال DownloadManagerH به TrafficWatch

## مقدمه
این سند راهنمای کاملی برای توسعه‌دهندگان DownloadManagerH است تا بتوانند امکان نمایش اطلاعات در داشبورد وب TrafficWatch را به برنامه خود اضافه کنند. این اتصال کاملاً اختیاری است و در صورت نصب بودن هر دو برنامه، کاربر می‌تواند از طریق تنظیمات TrafficWatch این ویژگی را فعال کند.

## معماری ارتباط

```
┌─────────────────────┐         HTTP API          ┌──────────────────────┐
│   TrafficWatch      │ ────────────────────────> │   DownloadManagerH    │
│   (پورت 8080)       │ <──────────────────────── │   (پورت 9090)        │
│                     │       JSON Response       │                      │
└─────────────────────┘                           └──────────────────────┘
```

## مشخصات فنی API

### پیش‌نیازها
- **فریم‌ورک**: .NET Core 9 یا بالاتر
- **پروتکل**: HTTP/1.1
- **فرمت داده**: JSON
- **پورت پیش‌فرض**: 9090 (قابل تغییر در تنظیمات)
- **آدرس**: `http://127.0.0.1:9090`

### نکات مهم
1. API باید فقط روی localhost در دسترس باشد (امنیت)
2. پاسخ‌ها باید سریع باشند (Timeout: 2 ثانیه)
3. در صورت خطا، API نباید کرش کند
4. تمام endpointها باید GET باشند

## Endpointهای مورد نیاز

### 1. دریافت وضعیت کلی (Status)

**Endpoint:** `GET /api/status`

**توضیحات:** اطلاعات کلی دانلود منیجر را برمی‌گرداند

**پاسخ موفق (200 OK):**
```json
{
  "isRunning": true,
  "version": "2.0.0",
  "activeDownloads": 5,
  "queuedDownloads": 3,
  "completedDownloads": 127,
  "totalDownloadSpeed": 1048576,
  "totalUploadedSpeed": 524288,
  "downloadLimit": 0,
  "uploadLimit": 0,
  "schedulerEnabled": true,
  "clipboardMonitorEnabled": true,
  "browserIntegrationEnabled": true,
  "lastError": "",
  "apiEndpoint": "http://127.0.0.1:9090"
}
```

**پاسخ خطا:**
- اگر سرویس در حال اجرا نیست: `{ "isRunning": false }`
- اگر خطایی رخ داده: مقدار `lastError` پر شود

**فیلدها:**
| فیلد | نوع | توضیحات |
|------|-----|---------|
| isRunning | boolean | وضعیت اجرای برنامه |
| version | string | نسخه برنامه |
| activeDownloads | integer | تعداد دانلودهای فعال |
| queuedDownloads | integer | تعداد دانلودهای در صف |
| completedDownloads | integer | تعداد دانلودهای تکمیل شده |
| totalDownloadSpeed | number | سرعت کل دانلود (بایت بر ثانیه) |
| totalUploadedSpeed | number | سرعت کل آپلود (بایت بر ثانیه) |
| downloadLimit | number | محدودیت دانلود (0 = نامحدود) |
| uploadLimit | number | محدودیت آپلود (0 = نامحدود) |
| schedulerEnabled | boolean | وضعیت زمان‌بندی |
| clipboardMonitorEnabled | boolean | وضعیت مانیتورینگ کلیپبورد |
| browserIntegrationEnabled | boolean | وضعیت افزونه مرورگر |
| lastError | string | آخرین خطای رخ داده |
| apiEndpoint | string | آدرس API |

---

### 2. دریافت لیست دانلودهای فعال

**Endpoint:** `GET /api/downloads/active`

**توضیحات:** لیست تمام دانلودهای در حال انجام را برمی‌گرداند

**پاسخ موفق (200 OK):**
```json
[
  {
    "id": "guid-1234-5678",
    "fileName": "ubuntu-22.04.iso",
    "url": "https://example.com/ubuntu.iso",
    "status": "Downloading",
    "progress": 45.5,
    "downloadedSize": 1073741824,
    "totalSize": 2362232012,
    "speed": 2097152,
    "eta": "00:05:30",
    "category": "ISO"
  },
  {
    "id": "guid-abcd-efgh",
    "fileName": "video.mp4",
    "url": "https://example.com/video.mp4",
    "status": "Paused",
    "progress": 78.2,
    "downloadedSize": 823456789,
    "totalSize": 1052345678,
    "speed": 0,
    "eta": "00:00:00",
    "category": "Video"
  }
]
```

**فیلدها:**
| فیلد | نوع | توضیحات |
|------|-----|---------|
| id | string | شناسه یکتا دانلود (GUID پیشنهاد می‌شود) |
| fileName | string | نام فایل در حال دانلود |
| url | string | آدرس منبع دانلود |
| status | string | وضعیت: Downloading, Queued, Paused, Completed, Error |
| progress | number | درصد پیشرفت (0-100) |
| downloadedSize | number | حجم دانلود شده (بایت) |
| totalSize | number | حجم کل فایل (بایت) |
| speed | number | سرعت فعلی (بایت بر ثانیه) |
| eta | string | زمان تخمینی باقی‌مانده (HH:MM:SS) |
| category | string | دسته‌بندی فایل |

---

### 3. دریافت آمار تاریخی (اختیاری)

**Endpoint:** `GET /api/stats/history?days=7`

**توضیحات:** آمار دانلودها در روزهای گذشته

**پاسخ موفق (200 OK):**
```json
{
  "dailyStats": [
    {
      "date": "2024-01-15",
      "totalDownloaded": 5368709120,
      "totalUploaded": 1073741824,
      "downloadCount": 12,
      "averageSpeed": 1572864
    }
  ],
  "weeklyTotal": 37580963840,
  "monthlyTotal": 161061273600
}
```

---

### 4. دریافت تنظیمات (اختیاری)

**Endpoint:** `GET /api/settings`

**توضیحات:** تنظیمات فعلی دانلود منیجر

**پاسخ موفق (200 OK):**
```json
{
  "maxConcurrentDownloads": 5,
  "defaultSavePath": "C:\\Downloads",
  "autoStart": true,
  "theme": "Dark",
  "language": "fa",
  "notificationsEnabled": true
}
```

## پیاده‌سازی در DownloadManagerH

### مرحله 1: افزودن کتابخانه‌های مورد نیاز

در فایل `.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0-windows</TargetFramework>
  </PropertyGroup>
  
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Http.Abstractions" Version="2.2.0" />
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
  </ItemGroup>
</Project>
```

### مرحله 2: ایجاد سرویس HTTP Server

سرویس `TrafficWatchIntegrationService` در پوشه `Services` ایجاد شده است.

### مرحله 3: افزودن به App.xaml.cs

سرویس در `App.xaml.cs` مقداردهی اولیه شده است.

### مرحله 4: افزودن تنظیمات به Settings

تنظیمات زیر به کلاس `Settings` اضافه شده‌اند:
- `EnableTrafficWatchIntegration` (bool): فعال/غیرفعال کردن یکپارچگی
- `TrafficWatchPort` (int): پورت API (پیش‌فرض: 9090)

### مرحله 5: افزودن UI برای تنظیمات

در صفحه تنظیمات، بخش "یکپارچگی با TrafficWatch" اضافه شده است که شامل:
- چک‌باکس برای فعال/غیرفعال کردن
- جعبه متن برای وارد کردن پورت

## نکات امنیتی

1. **فقط Localhost**: API باید فقط روی `127.0.0.1` گوش دهد
2. **بدون احراز هویت**: چون فقط localhost است، نیاز به auth نیست
3. **Firewall**: مطمئن شوید پورت در فایروال باز است (اگر نیاز بود)
4. **Error Handling**: تمام خطاها را مدیریت کنید تا برنامه کرش نکند

## تست API

### با curl:
```bash
# تست وضعیت
curl http://127.0.0.1:9090/api/status

# تست دانلودهای فعال
curl http://127.0.0.1:9090/api/downloads/active
```

### با PowerShell:
```powershell
# تست وضعیت
Invoke-RestMethod -Uri http://127.0.0.1:9090/api/status

# تست دانلودهای فعال
Invoke-RestMethod -Uri http://127.0.0.1:9090/api/downloads/active
```

### با Postman:
- Method: GET
- URL: `http://127.0.0.1:9090/api/status`

## عیب‌یابی

### مشکل: TrafficWatch اطلاعات را نشان نمی‌دهد

1. بررسی کنید DownloadManagerH در حال اجرا است
2. بررسی کنید پورت 9090 آزاد است
3. لاگ‌های TrafficWatch را چک کنید
4. با curl یا Postman API را تست کنید

### مشکل: Timeout خطا

1. مطمئن شوید API سریع پاسخ می‌دهد (< 2 ثانیه)
2. از کش کردن اطلاعات استفاده کنید
3. محاسبات سنگین را در thread جداگانه انجام دهید

### مشکل: پورت اشغال است

1. پورت دیگری انتخاب کنید (مثلاً 9091)
2. در تنظیمات هر دو برنامه پورت را تغییر دهید

## سوالات متداول

**سوال:** آیا این اتصال اجباری است؟
**جواب:** خیر، کاملاً اختیاری است.

**سوال:** آیا روی عملکرد دانلود منیجر تأثیر دارد؟
**جواب:** خیر، سرور HTTP بسیار سبک است و تأثیر محسوسی ندارد.

**سوال:** آیا می‌توان پورت را تغییر داد؟
**جواب:** بله، از طریق تنظیمات هر دو برنامه.

**سوال:** آیا نیاز به اینترنت دارد؟
**جواب:** خیر، تمام ارتباطات روی localhost است.

## تماس و پشتیبانی

برای گزارش مشکلات یا提出 پیشنهادات:
- GitHub Issues: https://github.com/hamerstandr/DownloadManagerH/issues 
- Email: support@downloadmenger2.ir

---

**نسخه سند:** 1.0  
**تاریخ انتشار:** 2024  
**تهیه شده برای:** DownloadManagerH Team
