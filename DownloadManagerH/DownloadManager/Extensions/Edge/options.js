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
    enableDownloadInterception: true,
    interceptFileTypes: ['zip', 'rar', '7z', 'exe', 'msi', 'pdf', 'mp4', 'mp3'],
    retryAttempts: 3,
    retryDelay: 1000,
    connectionTimeout: 30000
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
        document.getElementById('enableDownloadInterception').checked = settings.enableDownloadInterception;
        document.getElementById('retryAttempts').value = settings.retryAttempts;
        document.getElementById('retryDelay').value = settings.retryDelay;
        document.getElementById('connectionTimeout').value = settings.connectionTimeout;
        
        // Load intercept file types
        if (settings.interceptFileTypes) {
            settings.interceptFileTypes.forEach(type => {
                const checkbox = document.getElementById(`filetype_${type}`);
                if (checkbox) {
                    checkbox.checked = true;
                }
            });
        }
        
        console.log('تنظیمات بارگذاری شد:', settings);
    } catch (error) {
        console.error('خطا در بارگذاری تنظیمات:', error);
        showStatus('خطا در بارگذاری تنظیمات', 'error');
    }
}

// Save settings to storage
async function saveSettings() {
    try {
        // Collect intercept file types
        const interceptFileTypes = [];
        const fileTypeCheckboxes = document.querySelectorAll('input[id^="filetype_"]:checked');
        fileTypeCheckboxes.forEach(checkbox => {
            const type = checkbox.id.replace('filetype_', '');
            interceptFileTypes.push(type);
        });
        
        const settings = {
            showNotifications: document.getElementById('showNotifications').checked,
            autoDetectDownloads: document.getElementById('autoDetectDownloads').checked,
            batchDownloadEnabled: document.getElementById('batchDownloadEnabled').checked,
            maxBatchSize: parseInt(document.getElementById('maxBatchSize').value),
            enableDownloadInterception: document.getElementById('enableDownloadInterception').checked,
            interceptFileTypes: interceptFileTypes,
            retryAttempts: parseInt(document.getElementById('retryAttempts').value),
            retryDelay: parseInt(document.getElementById('retryDelay').value),
            connectionTimeout: parseInt(document.getElementById('connectionTimeout').value)
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
    // Validate max batch size
    if (settings.maxBatchSize < 1 || settings.maxBatchSize > 200) {
        showStatus('حداکثر تعداد لینک در دسته باید بین 1 تا 200 باشد', 'error');
        return false;
    }
    
    // Validate retry attempts
    if (settings.retryAttempts < 1 || settings.retryAttempts > 10) {
        showStatus('تعداد تلاش مجدد باید بین 1 تا 10 باشد', 'error');
        return false;
    }
    
    // Validate retry delay
    if (settings.retryDelay < 500 || settings.retryDelay > 10000) {
        showStatus('فاصله زمانی تلاش مجدد باید بین 500 تا 10000 میلی‌ثانیه باشد', 'error');
        return false;
    }
    
    // Validate connection timeout
    if (settings.connectionTimeout < 5000 || settings.connectionTimeout > 60000) {
        showStatus('زمان انتظار اتصال باید بین 5000 تا 60000 میلی‌ثانیه باشد', 'error');
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

// Test Native Messaging connection
async function testConnection() {
    const testButton = document.getElementById('testConnection');
    const originalText = testButton.textContent;
    testButton.textContent = '🔄 در حال تست...';
    testButton.disabled = true;
    
    try {
        // Send test message to background script
        const response = await chrome.runtime.sendMessage({
            action: 'testNativeConnection'
        });
        
        if (response && response.success) {
            showStatus('✅ اتصال Native Messaging موفقیت‌آمیز! دانلود منجر حامد در دسترس است.', 'success');
            updateConnectionStatus(true);
        } else {
            throw new Error(response?.error || 'Connection failed');
        }
    } catch (error) {
        console.error('خطا در تست اتصال:', error);
        showStatus('❌ اتصال ناموفق! لطفاً دانلود منجر حامد را اجرا کنید.', 'error');
        updateConnectionStatus(false);
    } finally {
        testButton.textContent = originalText;
        testButton.disabled = false;
    }
}



// Check Native Messaging connection status on page load
async function checkConnection() {
    try {
        const response = await chrome.runtime.sendMessage({
            action: 'checkNativeConnection'
        });
        
        if (response && response.connected) {
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
        status.textContent = 'متصل به دانلود منجر حامد از طریق Native Messaging - برنامه در حال اجرا است';
    } else {
        dot.classList.remove('connected');
        status.textContent = 'عدم اتصال Native Messaging - لطفاً دانلود منجر حامد را اجرا کنید';
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
        'batchDownloadEnabled',
        'enableDownloadInterception'
    ];
    
    autoSaveElements.forEach(id => {
        document.getElementById(id).addEventListener('change', () => {
            setTimeout(saveSettings, 500); // Auto-save after 500ms
        });
    });
    
    // Validate numeric inputs
    const numericInputs = [
        'maxBatchSize',
        'retryAttempts',
        'retryDelay',
        'connectionTimeout'
    ];
    
    numericInputs.forEach(id => {
        const element = document.getElementById(id);
        element.addEventListener('input', () => {
            validateNumericInput(element);
        });
    });
    
    // File type checkboxes
    const fileTypeCheckboxes = document.querySelectorAll('input[id^="filetype_"]');
    fileTypeCheckboxes.forEach(checkbox => {
        checkbox.addEventListener('change', () => {
            setTimeout(saveSettings, 500); // Auto-save after 500ms
        });
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