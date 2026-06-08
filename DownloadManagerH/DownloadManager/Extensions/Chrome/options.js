// Options Script for Download Manager Hamed Chrome Extension

document.addEventListener('DOMContentLoaded', async () => {
    await loadSettings();
    setupEventListeners();
    await checkConnection();
});

// Default settings
const defaultSettings = {
    showNotifications: true,
    autoDetectDownloads: true,
    batchDownloadEnabled: true,
    maxBatchSize: 50,
    hoverDelay: 500,
    showFileInfo: true,
    serverAddress: '127.0.0.1',
    serverPort: 24680,
    retryAttempts: 3,
    retryDelay: 1000
};

// Load settings from storage
async function loadSettings() {
    try {
        const result = await chrome.storage.sync.get('extensionSettings');
        const settings = result.extensionSettings || defaultSettings;
        
        // Update UI elements
        document.getElementById('showNotifications').checked = settings.showNotifications;
        document.getElementById('autoDetectDownloads').checked = settings.autoDetectDownloads;
        document.getElementById('batchDownloadEnabled').checked = settings.batchDownloadEnabled;
        document.getElementById('maxBatchSize').value = settings.maxBatchSize;
        document.getElementById('hoverDelay').value = settings.hoverDelay;
        document.getElementById('showFileInfo').checked = settings.showFileInfo;
        document.getElementById('serverAddress').value = settings.serverAddress;
        document.getElementById('serverPort').value = settings.serverPort;
        document.getElementById('retryAttempts').value = settings.retryAttempts;
        document.getElementById('retryDelay').value = settings.retryDelay;
        
        console.log('تنظیمات بارگذاری شد:', settings);
    } catch (error) {
        console.error('خطا در بارگذاری تنظیمات:', error);
        showStatus('خطا در بارگذاری تنظیمات', 'error');
    }
}

// Save settings to storage
async function saveSettings() {
    try {
        const settings = {
            showNotifications: document.getElementById('showNotifications').checked,
            autoDetectDownloads: document.getElementById('autoDetectDownloads').checked,
            batchDownloadEnabled: document.getElementById('batchDownloadEnabled').checked,
            maxBatchSize: parseInt(document.getElementById('maxBatchSize').value),
            hoverDelay: parseInt(document.getElementById('hoverDelay').value),
            showFileInfo: document.getElementById('showFileInfo').checked,
            serverAddress: document.getElementById('serverAddress').value.trim(),
            serverPort: parseInt(document.getElementById('serverPort').value),
            retryAttempts: parseInt(document.getElementById('retryAttempts').value),
            retryDelay: parseInt(document.getElementById('retryDelay').value)
        };
        
        // Validate settings
        if (!validateSettings(settings)) {
            return;
        }
        
        // Save to storage
        await chrome.storage.sync.set({ extensionSettings: settings });
        
        // Notify background script of settings change
        try {
            await chrome.runtime.sendMessage({
                action: 'updateSettings',
                settings: settings
            });
        } catch (error) {
            console.warn('خطا در اطلاع‌رسانی تغییر تنظیمات:', error);
        }
        
        showStatus('تنظیمات با موفقیت ذخیره شد!', 'success');
        console.log('تنظیمات ذخیره شد:', settings);
    } catch (error) {
        console.error('خطا در ذخیره تنظیمات:', error);
        showStatus('خطا در ذخیره تنظیمات', 'error');
    }
}

// Validate settings
function validateSettings(settings) {
    // Validate server address
    if (!settings.serverAddress || settings.serverAddress.length === 0) {
        showStatus('آدرس سرور نمی‌تواند خالی باشد', 'error');
        return false;
    }
    
    // Validate IP address format (basic check)
    const ipRegex = /^(\d{1,3}\.){3}\d{1,3}$|^localhost$/;
    if (!ipRegex.test(settings.serverAddress) && settings.serverAddress !== 'localhost') {
        showStatus('آدرس سرور معتبر نیست', 'error');
        return false;
    }
    
    // Validate port range
    if (settings.serverPort < 1024 || settings.serverPort > 65535) {
        showStatus('شماره پورت باید بین 1024 تا 65535 باشد', 'error');
        return false;
    }
    
    // Validate max batch size
    if (settings.maxBatchSize < 1 || settings.maxBatchSize > 200) {
        showStatus('حداکثر تعداد لینک در دسته باید بین 1 تا 200 باشد', 'error');
        return false;
    }
    
    // Validate hover delay
    if (settings.hoverDelay < 100 || settings.hoverDelay > 2000) {
        showStatus('تأخیر نمایش دکمه باید بین 100 تا 2000 میلی‌ثانیه باشد', 'error');
        return false;
    }
    
    // Validate retry attempts
    if (settings.retryAttempts < 1 || settings.retryAttempts > 10) {
        showStatus('تعداد تلاش مجدد باید بین 1 تا 10 باشد', 'error');
        return false;
    }
    
    // Validate retry delay
    if (settings.retryDelay < 500 || settings.retryDelay > 5000) {
        showStatus('فاصله زمانی تلاش مجدد باید بین 500 تا 5000 میلی‌ثانیه باشد', 'error');
        return false;
    }
    
    return true;
}

// Reset settings to defaults
async function resetSettings() {
    if (confirm('آیا مطمئن هستید که می‌خواهید همه تنظیمات را به حالت پیش‌فرض بازگردانید؟')) {
        try {
            await chrome.storage.sync.set({ extensionSettings: defaultSettings });
            await loadSettings();
            showStatus('تنظیمات به حالت پیش‌فرض بازگردانده شد', 'success');
        } catch (error) {
            console.error('خطا در بازنشانی تنظیمات:', error);
            showStatus('خطا در بازنشانی تنظیمات', 'error');
        }
    }
}

// Test connection to main application
async function testConnection() {
    const serverAddress = document.getElementById('serverAddress').value.trim();
    const serverPort = parseInt(document.getElementById('serverPort').value);
    
    if (!validateConnectionSettings(serverAddress, serverPort)) {
        return;
    }
    
    const testButton = document.getElementById('testConnection');
    const originalText = testButton.textContent;
    testButton.textContent = '🔄 در حال تست...';
    testButton.disabled = true;
    
    try {
        const response = await fetch(`http://${serverAddress}:${serverPort}/status/`, {
            method: 'GET',
            timeout: 5000
        });
        
        if (response.ok) {
            const data = await response.text();
            showStatus('✅ اتصال موفقیت‌آمیز! دانلود منجر حامد در دسترس است.', 'success');
            updateConnectionStatus(true);
        } else {
            throw new Error(`HTTP ${response.status}`);
        }
    } catch (error) {
        console.error('خطا در تست اتصال:', error);
        showStatus('❌ اتصال ناموفق! لطفاً دانلود منجر حامد را اجرا کنید یا تنظیمات را بررسی کنید.', 'error');
        updateConnectionStatus(false);
    } finally {
        testButton.textContent = originalText;
        testButton.disabled = false;
    }
}

// Validate connection settings
function validateConnectionSettings(address, port) {
    if (!address || address.length === 0) {
        showStatus('آدرس سرور نمی‌تواند خالی باشد', 'error');
        return false;
    }
    
    if (port < 1024 || port > 65535) {
        showStatus('شماره پورت معتبر نیست', 'error');
        return false;
    }
    
    return true;
}

// Check connection status on page load
async function checkConnection() {
    const serverAddress = document.getElementById('serverAddress').value.trim();
    const serverPort = parseInt(document.getElementById('serverPort').value);
    
    try {
        const response = await fetch(`http://${serverAddress}:${serverPort}/status/`, {
            method: 'GET',
            timeout: 3000
        });
        
        if (response.ok) {
            updateConnectionStatus(true);
        } else {
            updateConnectionStatus(false);
        }
    } catch (error) {
        updateConnectionStatus(false);
    }
}

// Update connection status indicator
function updateConnectionStatus(isConnected) {
    const dot = document.getElementById('connectionDot');
    const status = document.getElementById('connectionStatus');
    
    if (isConnected) {
        dot.classList.add('connected');
        status.textContent = 'متصل به دانلود منجر حامد - برنامه در حال اجرا است';
    } else {
        dot.classList.remove('connected');
        status.textContent = 'عدم اتصال - لطفاً دانلود منجر حامد را اجرا کنید';
    }
}

// Show status message
function showStatus(message, type) {
    const statusElement = document.getElementById('statusMessage');
    statusElement.textContent = message;
    statusElement.className = `status-message ${type} show`;
    
    setTimeout(() => {
        statusElement.classList.remove('show');
    }, 5000);
}

// Setup event listeners
function setupEventListeners() {
    // Save button
    document.getElementById('saveSettings').addEventListener('click', saveSettings);
    
    // Reset button
    document.getElementById('resetSettings').addEventListener('click', resetSettings);
    
    // Test connection button
    document.getElementById('testConnection').addEventListener('click', testConnection);
    
    // Auto-save on certain changes
    const autoSaveElements = [
        'showNotifications',
        'autoDetectDownloads',
        'batchDownloadEnabled'
    ];
    
    autoSaveElements.forEach(id => {
        document.getElementById(id).addEventListener('change', () => {
            setTimeout(saveSettings, 500); // Auto-save after 500ms
        });
    });
    
    // Validate numeric inputs
    const numericInputs = [
        'maxBatchSize',
        'hoverDelay',
        'serverPort',
        'retryAttempts',
        'retryDelay'
    ];
    
    numericInputs.forEach(id => {
        const element = document.getElementById(id);
        element.addEventListener('input', () => {
            validateNumericInput(element);
        });
    });
    
    // Server address validation
    document.getElementById('serverAddress').addEventListener('input', (e) => {
        const value = e.target.value.trim();
        const ipRegex = /^(\d{1,3}\.){3}\d{1,3}$|^localhost$/;
        
        if (value && !ipRegex.test(value) && value !== 'localhost') {
            e.target.style.borderColor = '#f44336';
        } else {
            e.target.style.borderColor = '#ddd';
        }
    });
    
    // Periodic connection check
    setInterval(checkConnection, 30000); // Check every 30 seconds
}

// Validate numeric input
function validateNumericInput(element) {
    const value = parseInt(element.value);
    const min = parseInt(element.min);
    const max = parseInt(element.max);
    
    if (isNaN(value) || value < min || value > max) {
        element.style.borderColor = '#f44336';
    } else {
        element.style.borderColor = '#ddd';
    }
}

// Handle keyboard shortcuts
document.addEventListener('keydown', (e) => {
    // Ctrl+S to save
    if (e.ctrlKey && e.key === 's') {
        e.preventDefault();
        saveSettings();
    }
    
    // Ctrl+R to reset
    if (e.ctrlKey && e.key === 'r') {
        e.preventDefault();
        resetSettings();
    }
    
    // F5 to test connection
    if (e.key === 'F5') {
        e.preventDefault();
        testConnection();
    }
});

console.log('صفحه تنظیمات آماده است');