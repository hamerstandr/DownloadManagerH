// Popup Script for Download Manager Hamed Chrome Extension

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
        const response = await chrome.runtime.sendMessage({ action: 'getSettings' });
        if (response && response.settings) {
            console.log('تنظیمات بارگذاری شد:', response.settings);
        }
    } catch (error) {
        console.error('خطا در مقداردهی popup:', error);
    }
}

// Setup event listeners
function setupEventListeners() {
    // URL input and send button
    const urlInput = document.getElementById('urlInput');
    const sendUrlBtn = document.getElementById('sendUrlBtn');
    
    urlInput.addEventListener('keypress', (e) => {
        if (e.key === 'Enter') {
            sendUrl();
        }
    });
    
    urlInput.addEventListener('input', () => {
        const isValid = isValidUrl(urlInput.value.trim());
        sendUrlBtn.disabled = !isValid;
    });
    
    sendUrlBtn.addEventListener('click', sendUrl);
    
    // Quick action buttons
    document.getElementById('detectLinksBtn').addEventListener('click', detectPageLinks);
    document.getElementById('openAppBtn').addEventListener('click', openMainApp);
    document.getElementById('settingsBtn').addEventListener('click', openSettings);
    document.getElementById('helpBtn').addEventListener('click', showHelp);
    
    // Footer links
    document.getElementById('aboutLink').addEventListener('click', showAbout);
    document.getElementById('supportLink').addEventListener('click', showSupport);
    document.getElementById('rateLink').addEventListener('click', showRating);
}

// Check connection status with main application via Native Messaging
async function checkConnectionStatus() {
    const statusDot = document.getElementById('statusDot');
    const statusText = document.getElementById('statusText');
    const connectionInfo = document.getElementById('connectionInfo');
    
    try {
        // Send status request to background script
        const response = await chrome.runtime.sendMessage({ 
            action: 'checkNativeConnection' 
        });
        
        if (response && response.connected) {
            statusDot.classList.add('connected');
            statusText.textContent = 'متصل به دانلود منجر حامد';
            connectionInfo.textContent = 'برنامه در حال اجرا است و آماده دریافت لینک‌ها';
        } else {
            throw new Error('Not connected');
        }
    } catch (error) {
        statusDot.classList.remove('connected');
        statusText.textContent = 'عدم اتصال به برنامه';
        connectionInfo.textContent = 'لطفاً دانلود منجر حامد را اجرا کنید';
    }
}

// Send URL to download manager via Native Messaging
async function sendUrl() {
    const urlInput = document.getElementById('urlInput');
    const sendBtnText = document.getElementById('sendBtnText');
    const sendBtnLoading = document.getElementById('sendBtnLoading');
    const url = urlInput.value.trim();
    
    if (!isValidUrl(url)) {
        showNotification('لینک وارد شده معتبر نیست', 'error');
        return;
    }
    
    // Show loading state
    sendBtnText.style.display = 'none';
    sendBtnLoading.style.display = 'inline-block';
    
    try {
        // Get current tab info
        const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
        
        // Send to background script for Native Messaging
        const response = await chrome.runtime.sendMessage({
            action: 'sendLinks',
            url: url,
            tab: tab
        });
        
        if (response && response.success) {
            showNotification('لینک با موفقیت ارسال شد!', 'success');
            urlInput.value = '';
            
            // Update statistics
            await updateStatistics();
            
            // Close popup after short delay
            setTimeout(() => {
                window.close();
            }, 1500);
        } else {
            throw new Error(response?.error || 'خطا در ارسال لینک');
        }
    } catch (error) {
        console.error('خطا در ارسال لینک:', error);
        showNotification('خطا در ارسال لینک: ' + error.message, 'error');
    } finally {
        // Hide loading state
        sendBtnText.style.display = 'inline';
        sendBtnLoading.style.display = 'none';
    }
}

// Detect downloadable links on current page
async function detectPageLinks() {
    try {
        const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
        
        if (!tab) {
            showNotification('خطا در دسترسی به صفحه فعال', 'error');
            return;
        }
        
        // Execute content script to detect links
        const results = await chrome.scripting.executeScript({
            target: { tabId: tab.id },
            function: detectDownloadableLinksInPage
        });
        
        if (results && results[0] && results[0].result) {
            const links = results[0].result;
            
            if (links.length === 0) {
                showNotification('هیچ لینک قابل دانلودی یافت نشد', 'info');
            } else {
                showNotification(`${links.length} لینک قابل دانلود یافت شد`, 'success');
                
                // Send detected links to background script
                chrome.runtime.sendMessage({
                    action: 'sendDetectedLinks',
                    links: links,
                    tab: tab
                });
                
                // Close popup
                setTimeout(() => {
                    window.close();
                }, 1000);
            }
        }
    } catch (error) {
        console.error('خطا در تشخیص لینک‌ها:', error);
        showNotification('خطا در تشخیص لینک‌ها', 'error');
    }
}

// Function to be injected for link detection
function detectDownloadableLinksInPage() {
    const downloadableExtensions = [
        'zip', 'rar', '7z', 'tar', 'gz', 'bz2',
        'exe', 'msi', 'dmg', 'pkg', 'deb', 'rpm',
        'pdf', 'doc', 'docx', 'xls', 'xlsx', 'ppt', 'pptx',
        'mp3', 'mp4', 'avi', 'mkv', 'mov', 'wmv', 'flv',
        'jpg', 'jpeg', 'png', 'gif', 'bmp', 'svg', 'webp',
        'iso', 'img', 'bin', 'apk', 'ipa'
    ];
    
    const links = [];
    const anchors = document.querySelectorAll('a[href]');
    
    anchors.forEach(anchor => {
        const href = anchor.href;
        if (href && href.startsWith('http')) {
            try {
                const url = new URL(href);
                const pathname = url.pathname.toLowerCase();
                const extension = pathname.split('.').pop();
                
                if (downloadableExtensions.includes(extension)) {
                    links.push({
                        url: href,
                        filename: pathname.split('/').pop() || anchor.textContent.trim(),
                        filetype: extension,
                        linkText: anchor.textContent.trim()
                    });
                }
            } catch (e) {
                // Invalid URL, skip
            }
        }
    });
    
    return links.slice(0, 50); // Limit to 50 links
}

// Open main application via Native Messaging
async function openMainApp() {
    try {
        // Send focus request to background script
        const response = await chrome.runtime.sendMessage({
            action: 'focusMainApp'
        });
        
        if (response && response.success) {
            showNotification('برنامه اصلی فعال شد', 'success');
            window.close();
        } else {
            showNotification('برنامه در حال اجرا نیست', 'error');
        }
    } catch (error) {
        showNotification('خطا در باز کردن برنامه', 'error');
    }
}

// Open extension settings
function openSettings() {
    chrome.runtime.openOptionsPage();
    window.close();
}

// Show help information
function showHelp() {
    const helpText = `
راهنمای استفاده از افزونه:

🔹 کلیک راست روی لینک‌ها برای ارسال به دانلود منجر
🔹 انتخاب متن حاوی لینک و کلیک راست برای دانلود دسته‌ای
🔹 Ctrl+Shift+D برای تشخیص لینک‌های قابل دانلود در صفحه
🔹 Ctrl+کلیک روی لینک‌ها برای ارسال مستقیم

برای عملکرد بهتر، دانلود منجر حامد باید در حال اجرا باشد.
    `;
    
    alert(helpText);
}

// Show about information
function showAbout() {
    const aboutText = `
دانلود منجر حامد - افزونه مرورگر
نسخه 2.0.0

این افزونه برای یکپارچگی بهتر با برنامه دانلود منجر حامد طراحی شده است.

ویژگی‌ها:
✅ ارسال لینک‌های تکی
✅ دانلود دسته‌ای
✅ تشخیص خودکار لینک‌های قابل دانلود
✅ پشتیبانی از کلیدهای میانبر
✅ رابط کاربری فارسی

توسعه‌دهنده: تیم دانلود منجر حامد
    `;
    
    alert(aboutText);
}

// Show support information
function showSupport() {
    const supportText = `
پشتیبانی و راهنمایی:

🔧 مشکلات رایج:
- اطمینان حاصل کنید برنامه اصلی در حال اجرا است
- فایروال یا آنتی‌ویروس ممکن است ارتباط را مسدود کند
- پورت 24680 باید آزاد باشد

📧 تماس با پشتیبانی:
در صورت بروز مشکل، لطفاً با تیم پشتیبانی تماس بگیرید.

🔄 به‌روزرسانی:
همیشه از آخرین نسخه برنامه و افزونه استفاده کنید.
    `;
    
    alert(supportText);
}

// Show rating prompt
function showRating() {
    const ratingText = `
آیا از افزونه راضی هستید؟

اگر افزونه برای شما مفید بوده، لطفاً با دادن امتیاز و نظر، ما را در بهبود آن یاری کنید.

امتیاز شما برای ما بسیار ارزشمند است! ⭐⭐⭐⭐⭐
    `;
    
    if (confirm(ratingText)) {
        // Open Chrome Web Store page for rating
        chrome.tabs.create({
            url: 'https://chrome.google.com/webstore/detail/download-manager-hamed'
        });
    }
}

// Load and display statistics
async function loadStatistics() {
    try {
        // Get statistics from storage
        const result = await chrome.storage.local.get(['downloadStats']);
        const stats = result.downloadStats || { today: 0, total: 0, lastDate: null };
        
        // Check if it's a new day
        const today = new Date().toDateString();
        if (stats.lastDate !== today) {
            stats.today = 0;
            stats.lastDate = today;
            await chrome.storage.local.set({ downloadStats: stats });
        }
        
        // Update display
        document.getElementById('todayDownloads').textContent = stats.today;
        document.getElementById('totalDownloads').textContent = stats.total;
    } catch (error) {
        console.error('خطا در بارگذاری آمار:', error);
    }
}

// Update statistics after successful download
async function updateStatistics() {
    try {
        const result = await chrome.storage.local.get(['downloadStats']);
        const stats = result.downloadStats || { today: 0, total: 0, lastDate: null };
        
        const today = new Date().toDateString();
        if (stats.lastDate !== today) {
            stats.today = 1;
            stats.lastDate = today;
        } else {
            stats.today++;
        }
        stats.total++;
        
        await chrome.storage.local.set({ downloadStats: stats });
        
        // Update display
        document.getElementById('todayDownloads').textContent = stats.today;
        document.getElementById('totalDownloads').textContent = stats.total;
    } catch (error) {
        console.error('خطا در به‌روزرسانی آمار:', error);
    }
}

// Validate URL
function isValidUrl(string) {
    try {
        const url = new URL(string);
        return url.protocol === 'http:' || url.protocol === 'https:';
    } catch (_) {
        return false;
    }
}

// Show notification
function showNotification(message, type = 'info') {
    const notification = document.getElementById('notification');
    notification.textContent = message;
    notification.className = `notification ${type}`;
    notification.classList.add('show');
    
    setTimeout(() => {
        notification.classList.remove('show');
    }, 3000);
}

// Auto-focus URL input
document.getElementById('urlInput').focus();