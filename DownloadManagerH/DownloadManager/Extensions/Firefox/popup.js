// Popup Script for Download Manager Hamed Firefox Extension

document.addEventListener('DOMContentLoaded', async () => {
    // Initialize popup
    await initializePopup();

    // Setup event listeners
    setupEventListeners();

    // Check connection status
    await checkConnectionStatus();

    // Load statistics
    await loadStatistics();
});

// Initialize popup elements
async function initializePopup() {
    try {
        // Get extension settings
        const response = await browser.runtime.sendMessage({ action: 'getSettings' });
        if (response && response.settings) {
            console.log('تنظیمات بارگذاری شد:', response.settings);
        }
    } catch (error) {
        console.error('خطا در مقداردهی popup:', error);
    }
}

// Setup event listeners
function setupEventListeners() {
    // Open main app button
    document.getElementById('btnOpenApp').addEventListener('click', openMainApp);
    
    // Add link button
    document.getElementById('btnAddLink').addEventListener('click', addNewLink);
    
    // Detect links button
    document.getElementById('btnDetectLinks').addEventListener('click', detectPageLinks);
    
    // Quick action buttons
    document.querySelectorAll('.action-btn').forEach(btn => {
        btn.addEventListener('click', (e) => {
            const action = e.currentTarget.dataset.action;
            handleQuickAction(action);
        });
    });
}

// Check connection status with main application
async function checkConnectionStatus() {
    const statusIndicator = document.getElementById('statusIndicator');
    const statusText = document.getElementById('statusText');

    try {
        const response = await fetch('http://127.0.0.1:24680/status/', {
            method: 'GET',
            timeout: 5000
        });

        if (response.ok) {
            statusIndicator.classList.add('connected');
            statusIndicator.classList.remove('disconnected');
            statusText.textContent = 'متصل به دانلود منجر حامد';
        } else {
            throw new Error('Response not OK');
        }
    } catch (error) {
        statusIndicator.classList.add('disconnected');
        statusIndicator.classList.remove('connected');
        statusText.textContent = 'عدم اتصال به برنامه';
        showNotification('لطفاً دانلود منجر حامد را اجرا کنید', 'warning');
    }
}

// Load statistics from download manager
async function loadStatistics() {
    try {
        const response = await fetch('http://127.0.0.1:24680/stats/', {
            method: 'GET',
            timeout: 3000
        });

        if (response.ok) {
            const data = await response.json();
            
            document.getElementById('activeDownloads').textContent = data.active || 0;
            document.getElementById('totalSpeed').textContent = formatSpeed(data.speed || 0);
            document.getElementById('completedDownloads').textContent = data.completed || 0;
            
            document.getElementById('statsBox').style.display = 'block';
        }
    } catch (error) {
        console.log('عدم توانایی در بارگیری آمار:', error.message);
    }
}

// Open main application
function openMainApp() {
    browser.runtime.sendMessage({ action: 'openMainApp' })
        .then(() => {
            showNotification('برنامه اصلی باز شد', 'success');
        })
        .catch((error) => {
            showNotification('خطا در باز کردن برنامه', 'error');
        });
}

// Add new link
async function addNewLink() {
    const tabs = await browser.tabs.query({ active: true, currentWindow: true });
    const currentTab = tabs[0];
    
    if (currentTab && currentTab.url) {
        await browser.runtime.sendMessage({
            action: 'sendLinks',
            url: currentTab.url
        });
        showNotification('لینک صفحه فعلی ارسال شد', 'success');
    }
}

// Detect page links
async function detectPageLinks() {
    try {
        const tabs = await browser.tabs.query({ active: true, currentWindow: true });
        const currentTab = tabs[0];
        
        if (!currentTab || !currentTab.id) {
            showNotification('تب فعال یافت نشد', 'error');
            return;
        }
        
        // Send message to background script to detect links
        const response = await browser.runtime.sendMessage({
            action: 'detectLinks',
            tabId: currentTab.id
        });
        
        if (response && response.count > 0) {
            showNotification(`${response.count} لینک قابل دانلود یافت شد`, 'success');
        } else {
            showNotification('هیچ لینک قابل دانلودی یافت نشد', 'warning');
        }
    } catch (error) {
        console.error('خطا در تشخیص لینک‌ها:', error);
        showNotification('خطا در تشخیص لینک‌ها', 'error');
    }
}

// Handle quick actions
async function handleQuickAction(action) {
    switch (action) {
        case 'clipboard':
            await handleClipboard();
            break;
        case 'history':
            await handleHistory();
            break;
        case 'settings':
            await handleSettings();
            break;
        case 'help':
            await handleHelp();
            break;
    }
}

// Handle clipboard action
async function handleClipboard() {
    try {
        // Request clipboard permission and read text
        const text = await navigator.clipboard.readText();
        
        if (isValidUrl(text)) {
            await browser.runtime.sendMessage({
                action: 'sendLinks',
                url: text
            });
            showNotification('لینک از کلیپ‌بورد ارسال شد', 'success');
        } else {
            showNotification('متن کلیپ‌بورد لینک معتبر نیست', 'warning');
        }
    } catch (error) {
        showNotification('عدم دسترسی به کلیپ‌بورد', 'error');
    }
}

// Handle history action
async function handleHistory() {
    // Open history in a new tab or show in popup
    browser.tabs.create({
        url: 'about:addons'
    });
}

// Handle settings action
async function handleSettings() {
    browser.runtime.openOptionsPage();
}

// Handle help action
async function handleHelp() {
    browser.tabs.create({
        url: browser.runtime.getURL('help.html')
    });
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

// Validate URL
function isValidUrl(string) {
    try {
        new URL(string);
        return true;
    } catch (_) {
        return false;
    }
}

// Format speed
function formatSpeed(bytesPerSecond) {
    if (bytesPerSecond < 1024) {
        return `${bytesPerSecond} B/s`;
    } else if (bytesPerSecond < 1024 * 1024) {
        return `${(bytesPerSecond / 1024).toFixed(1)} KB/s`;
    } else {
        return `${(bytesPerSecond / (1024 * 1024)).toFixed(1)} MB/s`;
    }
}

// Helper function to get current tab
async function getCurrentTab() {
    const tabs = await browser.tabs.query({ active: true, currentWindow: true });
    return tabs[0];
}
