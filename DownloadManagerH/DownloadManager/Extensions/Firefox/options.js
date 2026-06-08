// Options Script for Download Manager Hamed Firefox Extension

// Default settings
const DEFAULT_SETTINGS = {
    showNotifications: true,
    autoDetectDownloads: true,
    enableHoverButton: true,
    batchDownloadEnabled: true,
    maxBatchSize: 50,
    apiBaseUrl: 'http://127.0.0.1:24680'
};

// Load settings on page load
document.addEventListener('DOMContentLoaded', async () => {
    await loadSettings();
    setupEventListeners();
});

// Setup event listeners
function setupEventListeners() {
    document.getElementById('btnSave').addEventListener('click', saveSettings);
    document.getElementById('btnReset').addEventListener('click', resetSettings);
    document.getElementById('btnHelp').addEventListener('click', showHelp);
}

// Load settings from storage
async function loadSettings() {
    try {
        const result = await browser.storage.sync.get('extensionSettings');
        const settings = result.extensionSettings || DEFAULT_SETTINGS;
        
        // Apply settings to UI elements
        document.getElementById('showNotifications').checked = settings.showNotifications;
        document.getElementById('autoDetectDownloads').checked = settings.autoDetectDownloads;
        document.getElementById('enableHoverButton').checked = settings.enableHoverButton;
        document.getElementById('batchDownloadEnabled').checked = settings.batchDownloadEnabled;
        document.getElementById('maxBatchSize').value = settings.maxBatchSize;
        document.getElementById('apiBaseUrl').value = settings.apiBaseUrl;
        
        console.log('تنظیمات بارگذاری شد:', settings);
    } catch (error) {
        console.error('خطا در بارگذاری تنظیمات:', error);
        showNotification('خطا در بارگذاری تنظیمات', 'error');
    }
}

// Save settings to storage
async function saveSettings() {
    try {
        const settings = {
            showNotifications: document.getElementById('showNotifications').checked,
            autoDetectDownloads: document.getElementById('autoDetectDownloads').checked,
            enableHoverButton: document.getElementById('enableHoverButton').checked,
            batchDownloadEnabled: document.getElementById('batchDownloadEnabled').checked,
            maxBatchSize: parseInt(document.getElementById('maxBatchSize').value),
            apiBaseUrl: document.getElementById('apiBaseUrl').value.trim()
        };
        
        // Validate settings
        if (settings.maxBatchSize < 1 || settings.maxBatchSize > 100) {
            showNotification('حداکثر تعداد لینک باید بین 1 تا 100 باشد', 'error');
            return;
        }
        
        // Validate API URL
        try {
            new URL(settings.apiBaseUrl);
        } catch (e) {
            showNotification('آدرس API معتبر نیست', 'error');
            return;
        }
        
        // Save to storage
        await browser.storage.sync.set({ extensionSettings: settings });
        
        // Notify background script about settings change
        await browser.runtime.sendMessage({
            action: 'updateSettings',
            settings: settings
        });
        
        showNotification('تنظیمات با موفقیت ذخیره شد', 'success');
        console.log('تنظیمات ذخیره شد:', settings);
    } catch (error) {
        console.error('خطا در ذخیره تنظیمات:', error);
        showNotification('خطا در ذخیره تنظیمات', 'error');
    }
}

// Reset settings to default
async function resetSettings() {
    try {
        // Confirm reset
        if (!confirm('آیا مطمئن هستید که می‌خواهید تنظیمات را به پیش‌فرض بازگردانید؟')) {
            return;
        }
        
        // Reset to default
        await browser.storage.sync.remove('extensionSettings');
        
        // Reload settings
        await loadSettings();
        
        showNotification('تنظیمات به پیش‌فرض بازگردانده شد', 'success');
    } catch (error) {
        console.error('خطا در بازگردانی تنظیمات:', error);
        showNotification('خطا در بازگردانی تنظیمات', 'error');
    }
}

// Show help
function showHelp() {
    const helpContent = `
📖 راهنمای تنظیمات افزونه دانلود منجر حامد

🔔 اعلان‌ها و اطلاع‌رسانی:
• نمایش اعلان‌ها: فعال/غیرفعال کردن پیام‌های اطلاع‌رسانی
• هنگام ارسال لینک به دانلود منجر، پیام تأیید نمایش داده می‌شود

🔍 تشخیص خودکار:
• تشخیص خودکار دانلودها: شناسایی لینک‌های قابل دانلود در صفحات وب
• دکمه شناور دانلود: نمایش دکمه 🚀 هنگام قرار دادن موس روی لینک‌ها

📦 دانلود دسته‌ای:
• فعال‌سازی دانلود دسته‌ای: امکان ارسال چندین لینک همزمان
• حداکثر تعداد لینک: محدودیت تعداد لینک‌ها در هر بار ارسال (1-100)

🌐 اتصال به برنامه:
• آدرس API: آدرس سرور محلی دانلود منجر حامد
• پیش‌فرض: http://127.0.0.1:24680
• در صورت تغییر پورت در برنامه اصلی، این مقدار را تغییر دهید

⌨️ میانبرهای صفحه کلید:
• Ctrl+Shift+D: تشخیص همه لینک‌های قابل دانلود در صفحه
• Ctrl+کلیک روی لینک: ارسال مستقیم به دانلود منجر
• Escape: بستن دکمه‌های شناور

🔧 عیب‌یابی:
1. اطمینان حاصل کنید دانلود منجر حامد در حال اجرا است
2. پورت 24680 باید آزاد باشد
3. فایروال یا آنتی‌ویروس ممکن است ارتباط را مسدود کند
4. در صورت بروز مشکل، تنظیمات را به پیش‌فرض بازگردانید

📞 پشتیبانی:
در صورت بروز مشکل، با تیم پشتیبانی تماس بگیرید.
    `;
    
    alert(helpContent);
}

// Show notification
function showNotification(message, type = 'info') {
    // Remove existing notifications
    const existing = document.querySelector('.notification');
    if (existing) {
        existing.remove();
    }
    
    const notification = document.createElement('div');
    notification.className = `notification ${type}`;
    notification.textContent = message;
    
    document.body.appendChild(notification);
    
    setTimeout(() => {
        notification.remove();
    }, 3000);
}

// Helper function to validate URL
function isValidUrl(string) {
    try {
        new URL(string);
        return true;
    } catch (_) {
        return false;
    }
}

// Auto-save settings on change (optional)
function setupAutoSave() {
    const inputs = document.querySelectorAll('input[type="checkbox"], input[type="number"], input[type="text"]');
    
    inputs.forEach(input => {
        input.addEventListener('change', async () => {
            // Debounce auto-save
            clearTimeout(window.autoSaveTimeout);
            window.autoSaveTimeout = setTimeout(async () => {
                await saveSettings();
            }, 1000);
        });
    });
}

// Export settings (for backup)
async function exportSettings() {
    try {
        const result = await browser.storage.sync.get('extensionSettings');
        const settings = result.extensionSettings || DEFAULT_SETTINGS;
        
        const dataStr = JSON.stringify(settings, null, 2);
        const dataBlob = new Blob([dataStr], { type: 'application/json' });
        const url = URL.createObjectURL(dataBlob);
        
        const a = document.createElement('a');
        a.href = url;
        a.download = 'download-manager-hamed-settings.json';
        a.click();
        
        URL.revokeObjectURL(url);
        
        showNotification('تنظیمات صادر شد', 'success');
    } catch (error) {
        console.error('خطا در صدور تنظیمات:', error);
        showNotification('خطا در صدور تنظیمات', 'error');
    }
}

// Import settings (from backup)
async function importSettings(fileInput) {
    try {
        const file = fileInput.files[0];
        if (!file) {
            showNotification('فایلی انتخاب نشده است', 'error');
            return;
        }
        
        const text = await file.text();
        const settings = JSON.parse(text);
        
        // Validate settings
        if (typeof settings !== 'object') {
            throw new Error('Invalid settings format');
        }
        
        // Save to storage
        await browser.storage.sync.set({ extensionSettings: settings });
        
        // Reload UI
        await loadSettings();
        
        showNotification('تنظیمات وارد شد', 'success');
    } catch (error) {
        console.error('خطا در ورود تنظیمات:', error);
        showNotification('خطا در ورود تنظیمات', 'error');
    }
}

console.log('دانلود منجر حامد - Options Script آماده است');
