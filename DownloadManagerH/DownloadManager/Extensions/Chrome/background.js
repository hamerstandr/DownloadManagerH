// Enhanced Chrome Extension for Download Manager Hamed
// Manifest V3 Service Worker Implementation

// Configuration
const CONFIG = {
    API_BASE_URL: 'http://127.0.0.1:24680',
    API_ENDPOINTS: {
        ADD: '/add/',
        STATUS: '/status/',
        SETTINGS: '/settings/'
    },
    RETRY_ATTEMPTS: 3,
    RETRY_DELAY: 1000
};

// Extension state management
let extensionSettings = {
    autoDetectDownloads: true,
    showNotifications: true,
    batchDownloadEnabled: true,
    maxBatchSize: 50
};

// Initialize extension
chrome.runtime.onInstalled.addListener(async (details) => {
    console.log('دانلود منجر حامد - افزونه نصب شد');
    
    // Load settings from storage
    await loadSettings();
    
    // Setup context menus
    await setupContextMenus();
    
    // Show welcome notification for new installs
    if (details.reason === 'install') {
        showNotification(
            'دانلود منجر حامد',
            'افزونه با موفقیت نصب شد! اکنون می‌توانید لینک‌ها را به راحتی ارسال کنید.',
            'success'
        );
    }
});

// Load settings from storage
async function loadSettings() {
    try {
        const result = await chrome.storage.sync.get('extensionSettings');
        if (result.extensionSettings) {
            extensionSettings = { ...extensionSettings, ...result.extensionSettings };
        }
    } catch (error) {
        console.error('خطا در بارگذاری تنظیمات:', error);
    }
}

// Save settings to storage
async function saveSettings() {
    try {
        await chrome.storage.sync.set({ extensionSettings });
    } catch (error) {
        console.error('خطا در ذخیره تنظیمات:', error);
    }
}

// Setup context menus
async function setupContextMenus() {
    try {
        // Remove all existing menus
        await chrome.contextMenus.removeAll();
        
        // Main parent menu
        chrome.contextMenus.create({
            id: "dmh-main",
            title: "دانلود منجر حامد 🚀",
            contexts: ["link", "selection", "page", "image", "video", "audio"],
            documentUrlPatterns: ["http://*/*", "https://*/*"]
        });
        
        // Single link download
        chrome.contextMenus.create({
            id: "send-single-link",
            parentId: "dmh-main",
            title: "دانلود این لینک 📥",
            contexts: ["link"],
            documentUrlPatterns: ["http://*/*", "https://*/*"]
        });
        
        // Image download
        chrome.contextMenus.create({
            id: "send-image",
            parentId: "dmh-main",
            title: "دانلود این تصویر 🖼️",
            contexts: ["image"],
            documentUrlPatterns: ["http://*/*", "https://*/*"]
        });
        
        // Video download
        chrome.contextMenus.create({
            id: "send-video",
            parentId: "dmh-main",
            title: "دانلود این ویدیو 🎬",
            contexts: ["video"],
            documentUrlPatterns: ["http://*/*", "https://*/*"]
        });
        
        // Audio download
        chrome.contextMenus.create({
            id: "send-audio",
            parentId: "dmh-main",
            title: "دانلود این صوت 🎵",
            contexts: ["audio"],
            documentUrlPatterns: ["http://*/*", "https://*/*"]
        });
        
        // Separator
        chrome.contextMenus.create({
            id: "separator1",
            parentId: "dmh-main",
            type: "separator",
            contexts: ["selection", "page"]
        });
        
        // Selected text with links
        chrome.contextMenus.create({
            id: "send-selected-links",
            parentId: "dmh-main",
            title: "دانلود لینک‌های انتخاب شده 📦",
            contexts: ["selection"],
            documentUrlPatterns: ["http://*/*", "https://*/*"]
        });
        
        // Page link detection
        chrome.contextMenus.create({
            id: "detect-page-links",
            parentId: "dmh-main",
            title: "تشخیص همه لینک‌های قابل دانلود 🔍",
            contexts: ["page"],
            documentUrlPatterns: ["http://*/*", "https://*/*"]
        });
        
        // Download all media on page
        chrome.contextMenus.create({
            id: "download-all-media",
            parentId: "dmh-main",
            title: "دانلود همه رسانه‌های صفحه 📺",
            contexts: ["page"],
            documentUrlPatterns: ["http://*/*", "https://*/*"]
        });
        
        // Separator
        chrome.contextMenus.create({
            id: "separator2",
            parentId: "dmh-main",
            type: "separator",
            contexts: ["page"]
        });
        
        // Quick actions submenu
        chrome.contextMenus.create({
            id: "quick-actions",
            parentId: "dmh-main",
            title: "اقدامات سریع ⚡",
            contexts: ["page"]
        });
        
        // Open main app
        chrome.contextMenus.create({
            id: "open-main-app",
            parentId: "quick-actions",
            title: "باز کردن برنامه اصلی 📱",
            contexts: ["page"]
        });
        
        // Settings
        chrome.contextMenus.create({
            id: "open-settings",
            parentId: "quick-actions",
            title: "تنظیمات افزونه ⚙️",
            contexts: ["page"]
        });
        
        // Help
        chrome.contextMenus.create({
            id: "show-help",
            parentId: "quick-actions",
            title: "راهنما و پشتیبانی ❓",
            contexts: ["page"]
        });
        
        console.log('منوهای زمینه‌ای پیشرفته با موفقیت ایجاد شدند');
    } catch (error) {
        console.error('خطا در ایجاد منوهای زمینه‌ای:', error);
    }
}

// Handle context menu clicks
chrome.contextMenus.onClicked.addListener(async (info, tab) => {
    try {
        switch (info.menuItemId) {
            case "send-single-link":
                await handleSingleLink(info.linkUrl, tab);
                break;
                
            case "send-image":
                await handleMediaDownload(info.srcUrl, 'image', tab);
                break;
                
            case "send-video":
                await handleMediaDownload(info.srcUrl, 'video', tab);
                break;
                
            case "send-audio":
                await handleMediaDownload(info.srcUrl, 'audio', tab);
                break;
                
            case "send-selected-links":
                await handleSelectedLinks(info.selectionText, tab);
                break;
                
            case "detect-page-links":
                await handlePageLinkDetection(tab);
                break;
                
            case "download-all-media":
                await handleAllMediaDownload(tab);
                break;
                
            case "open-main-app":
                await openMainApplication();
                break;
                
            case "open-settings":
                chrome.runtime.openOptionsPage();
                break;
                
            case "show-help":
                await showExtensionHelp();
                break;
        }
    } catch (error) {
        console.error('خطا در پردازش منوی زمینه‌ای:', error);
        showNotification('خطا', 'خطا در پردازش درخواست', 'error');
    }
});

// Handle single link download
async function handleSingleLink(url, tab) {
    if (!url) {
        showNotification('خطا', 'لینک معتبر نیست', 'error');
        return;
    }
    
    const linkData = {
        url: url,
        referrer: tab.url,
        title: tab.title,
        userAgent: navigator.userAgent,
        timestamp: Date.now()
    };
    
    await sendToDownloadManager([linkData], 'single');
}

// Handle selected text with links
async function handleSelectedLinks(selectionText, tab) {
    if (!selectionText) {
        showNotification('خطا', 'متن انتخاب شده‌ای وجود ندارد', 'error');
        return;
    }
    
    // Extract URLs from selected text
    const urlRegex = /https?:\/\/[^\s'"<>]+/g;
    const urls = Array.from(selectionText.matchAll(urlRegex)).map(match => match[0]);
    
    if (urls.length === 0) {
        showNotification('هیچ لینکی یافت نشد', 'در متن انتخاب شده هیچ لینک معتبری یافت نشد', 'warning');
        return;
    }
    
    if (urls.length > extensionSettings.maxBatchSize) {
        showNotification(
            'تعداد لینک‌ها زیاد است',
            `تنها ${extensionSettings.maxBatchSize} لینک اول ارسال می‌شود`,
            'warning'
        );
        urls.splice(extensionSettings.maxBatchSize);
    }
    
    const linkData = urls.map(url => ({
        url: url,
        referrer: tab.url,
        title: tab.title,
        userAgent: navigator.userAgent,
        timestamp: Date.now()
    }));
    
    await sendToDownloadManager(linkData, 'batch');
}

// Handle page link detection
async function handlePageLinkDetection(tab) {
    try {
        // Inject content script to detect downloadable links
        const results = await chrome.scripting.executeScript({
            target: { tabId: tab.id },
            function: detectDownloadableLinks
        });
        
        if (results && results[0] && results[0].result) {
            const links = results[0].result;
            
            if (links.length === 0) {
                showNotification('هیچ لینکی یافت نشد', 'در این صفحه لینک قابل دانلودی یافت نشد', 'info');
                return;
            }
            
            const linkData = links.map(link => ({
                url: link.url,
                filename: link.filename,
                filesize: link.filesize,
                filetype: link.filetype,
                referrer: tab.url,
                title: tab.title,
                userAgent: navigator.userAgent,
                timestamp: Date.now()
            }));
            
            await sendToDownloadManager(linkData, 'detected');
        }
    } catch (error) {
        console.error('خطا در تشخیص لینک‌های صفحه:', error);
        showNotification('خطا', 'خطا در تشخیص لینک‌های صفحه', 'error');
    }
}

// Function to be injected into page for link detection
function detectDownloadableLinks() {
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
        const text = anchor.textContent.trim();
        
        if (href && href.startsWith('http')) {
            try {
                const url = new URL(href);
                const pathname = url.pathname.toLowerCase();
                const extension = pathname.split('.').pop();
                
                if (downloadableExtensions.includes(extension)) {
                    // Try to get file size from text or attributes
                    let filesize = null;
                    const sizeMatch = text.match(/\(([0-9.]+\s*(KB|MB|GB))\)/i);
                    if (sizeMatch) {
                        filesize = sizeMatch[1];
                    }
                    
                    links.push({
                        url: href,
                        filename: pathname.split('/').pop() || text,
                        filesize: filesize,
                        filetype: extension,
                        linkText: text
                    });
                }
            } catch (e) {
                // Invalid URL, skip
            }
        }
    });
    
    return links.slice(0, 100); // Limit to 100 links
}

// Send data to download manager with retry logic
async function sendToDownloadManager(linkData, type) {
    const payload = {
        links: linkData,
        type: type,
        timestamp: Date.now(),
        extensionVersion: chrome.runtime.getManifest().version
    };
    
    let lastError = null;
    
    for (let attempt = 1; attempt <= CONFIG.RETRY_ATTEMPTS; attempt++) {
        try {
            const response = await fetch(`${CONFIG.API_BASE_URL}${CONFIG.API_ENDPOINTS.ADD}`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'User-Agent': 'DownloadManagerHamed-Extension/2.0'
                },
                body: JSON.stringify(payload)
            });
            
            if (response.ok) {
                const result = await response.text();
                
                if (extensionSettings.showNotifications) {
                    const message = linkData.length === 1 
                        ? 'لینک با موفقیت ارسال شد!'
                        : `${linkData.length} لینک با موفقیت ارسال شد!`;
                    
                    showNotification('دانلود منجر حامد', message, 'success');
                }
                
                // Log successful transmission
                console.log(`${linkData.length} لینک با موفقیت ارسال شد (تلاش ${attempt})`);
                return;
            } else {
                const errorText = await response.text();
                lastError = new Error(`HTTP ${response.status}: ${errorText}`);
            }
        } catch (error) {
            lastError = error;
            console.warn(`تلاش ${attempt} ناموفق:`, error.message);
            
            // Wait before retry (except for last attempt)
            if (attempt < CONFIG.RETRY_ATTEMPTS) {
                await new Promise(resolve => setTimeout(resolve, CONFIG.RETRY_DELAY * attempt));
            }
        }
    }
    
    // All attempts failed
    console.error('همه تلاش‌ها ناموفق:', lastError);
    showNotification(
        'خطا در ارتباط',
        'ارتباط با دانلود منجر حامد برقرار نشد. لطفاً برنامه را اجرا کنید.',
        'error'
    );
}

// Show notification with different types
function showNotification(title, message, type = 'info') {
    if (!extensionSettings.showNotifications) return;
    
    const iconMap = {
        success: 'icon128.png',
        error: 'icon128.png',
        warning: 'icon128.png',
        info: 'icon128.png'
    };
    
    chrome.notifications.create({
        type: 'basic',
        iconUrl: iconMap[type],
        title: title,
        message: message,
        priority: type === 'error' ? 2 : 1
    });
}

// Handle extension icon click
chrome.action.onClicked.addListener(async (tab) => {
    // This will open the popup, but we can also add fallback logic here
    console.log('Extension icon clicked');
});

// Handle messages from content script or popup
chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
    switch (request.action) {
        case 'getSettings':
            sendResponse({ settings: extensionSettings });
            break;
            
        case 'updateSettings':
            extensionSettings = { ...extensionSettings, ...request.settings };
            saveSettings();
            sendResponse({ success: true });
            break;
            
        case 'sendLinks':
            handleSingleLink(request.url, sender.tab);
            sendResponse({ success: true });
            break;
            
        case 'sendDetectedLinks':
            if (request.links && request.links.length > 0) {
                const linkData = request.links.map(link => ({
                    url: typeof link === 'string' ? link : link.url,
                    filename: typeof link === 'object' ? link.filename : null,
                    filesize: typeof link === 'object' ? link.filesize : null,
                    filetype: typeof link === 'object' ? link.filetype : null,
                    referrer: request.tab ? request.tab.url : '',
                    title: request.tab ? request.tab.title : '',
                    userAgent: navigator.userAgent,
                    timestamp: Date.now()
                }));
                
                sendToDownloadManager(linkData, 'detected');
            }
            sendResponse({ success: true });
            break;
            
        case 'batchDownload':
            if (request.urls && Array.isArray(request.urls)) {
                const linkData = request.urls.map(url => ({
                    url: url,
                    referrer: sender.tab ? sender.tab.url : '',
                    title: sender.tab ? sender.tab.title : '',
                    userAgent: navigator.userAgent,
                    timestamp: Date.now()
                }));
                
                sendToDownloadManager(linkData, 'batch');
            }
            sendResponse({ success: true });
            break;
            
        default:
            sendResponse({ error: 'Unknown action' });
    }
    
    return true; // Keep message channel open for async response
});

// Handle media download (images, videos, audio)
async function handleMediaDownload(srcUrl, mediaType, tab) {
    if (!srcUrl) {
        showNotification('خطا', `آدرس ${mediaType} معتبر نیست`, 'error');
        return;
    }
    
    const linkData = {
        url: srcUrl,
        referrer: tab.url,
        title: tab.title,
        userAgent: navigator.userAgent,
        timestamp: Date.now(),
        mediaType: mediaType
    };
    
    await sendToDownloadManager([linkData], 'media');
}

// Handle downloading all media on page
async function handleAllMediaDownload(tab) {
    try {
        const results = await chrome.scripting.executeScript({
            target: { tabId: tab.id },
            function: detectAllMediaOnPage
        });
        
        if (results && results[0] && results[0].result) {
            const mediaItems = results[0].result;
            
            if (mediaItems.length === 0) {
                showNotification('هیچ رسانه‌ای یافت نشد', 'در این صفحه رسانه قابل دانلودی یافت نشد', 'info');
                return;
            }
            
            const linkData = mediaItems.map(item => ({
                url: item.url,
                filename: item.filename,
                mediaType: item.type,
                referrer: tab.url,
                title: tab.title,
                userAgent: navigator.userAgent,
                timestamp: Date.now()
            }));
            
            await sendToDownloadManager(linkData, 'media-batch');
            
            showNotification(
                'رسانه‌های صفحه',
                `${mediaItems.length} رسانه برای دانلود ارسال شد`,
                'success'
            );
        }
    } catch (error) {
        console.error('خطا در تشخیص رسانه‌های صفحه:', error);
        showNotification('خطا', 'خطا در تشخیص رسانه‌های صفحه', 'error');
    }
}

// Function to detect all media on page
function detectAllMediaOnPage() {
    const mediaItems = [];
    
    // Images
    const images = document.querySelectorAll('img[src]');
    images.forEach(img => {
        if (img.src && img.src.startsWith('http')) {
            // Skip small images (likely icons)
            if (img.naturalWidth > 100 && img.naturalHeight > 100) {
                mediaItems.push({
                    url: img.src,
                    type: 'image',
                    filename: img.src.split('/').pop() || 'image',
                    alt: img.alt || ''
                });
            }
        }
    });
    
    // Videos
    const videos = document.querySelectorAll('video[src], video source[src]');
    videos.forEach(video => {
        const src = video.src || video.querySelector('source')?.src;
        if (src && src.startsWith('http')) {
            mediaItems.push({
                url: src,
                type: 'video',
                filename: src.split('/').pop() || 'video'
            });
        }
    });
    
    // Audio
    const audios = document.querySelectorAll('audio[src], audio source[src]');
    audios.forEach(audio => {
        const src = audio.src || audio.querySelector('source')?.src;
        if (src && src.startsWith('http')) {
            mediaItems.push({
                url: src,
                type: 'audio',
                filename: src.split('/').pop() || 'audio'
            });
        }
    });
    
    // Background images from CSS
    const elementsWithBg = document.querySelectorAll('*');
    elementsWithBg.forEach(el => {
        const bgImage = window.getComputedStyle(el).backgroundImage;
        if (bgImage && bgImage !== 'none') {
            const match = bgImage.match(/url\(['"]?([^'"]+)['"]?\)/);
            if (match && match[1] && match[1].startsWith('http')) {
                mediaItems.push({
                    url: match[1],
                    type: 'image',
                    filename: match[1].split('/').pop() || 'background-image'
                });
            }
        }
    });
    
    // Remove duplicates
    const uniqueItems = mediaItems.filter((item, index, self) =>
        index === self.findIndex(t => t.url === item.url)
    );
    
    return uniqueItems.slice(0, 100); // Limit to 100 items
}

// Open main application
async function openMainApplication() {
    try {
        const response = await fetch(`${CONFIG.API_BASE_URL}/focus/`, {
            method: 'POST'
        });
        
        if (response.ok) {
            showNotification('دانلود منجر حامد', 'برنامه اصلی فعال شد', 'success');
        } else {
            showNotification('خطا', 'برنامه در حال اجرا نیست', 'error');
        }
    } catch (error) {
        showNotification('خطا', 'خطا در باز کردن برنامه اصلی', 'error');
    }
}

// Show extension help
async function showExtensionHelp() {
    const helpContent = `
🚀 راهنمای استفاده از افزونه دانلود منجر حامد

📋 ویژگی‌های اصلی:
• کلیک راست روی لینک‌ها برای دانلود تکی
• کلیک راست روی تصاویر، ویدیوها و صوت‌ها
• انتخاب متن حاوی لینک و کلیک راست برای دانلود دسته‌ای
• تشخیص خودکار همه لینک‌های قابل دانلود در صفحه
• دانلود همه رسانه‌های موجود در صفحه

⌨️ کلیدهای میانبر:
• Ctrl+Shift+D: تشخیص لینک‌های قابل دانلود
• Ctrl+کلیک روی لینک: ارسال مستقیم به دانلود منجر
• Escape: بستن دکمه‌های شناور

⚙️ تنظیمات:
• برای تغییر تنظیمات، از منوی "تنظیمات افزونه" استفاده کنید
• می‌توانید اعلان‌ها، تشخیص خودکار و سایر گزینه‌ها را تنظیم کنید

🔧 عیب‌یابی:
• اطمینان حاصل کنید دانلود منجر حامد در حال اجرا است
• پورت 24680 باید آزاد باشد
• فایروال یا آنتی‌ویروس ممکن است ارتباط را مسدود کند

📞 پشتیبانی:
در صورت بروز مشکل، با تیم پشتیبانی تماس بگیرید.
    `;
    
    // Create a new tab with help content
    chrome.tabs.create({
        url: `data:text/html;charset=utf-8,${encodeURIComponent(`
            <!DOCTYPE html>
            <html dir="rtl" lang="fa">
            <head>
                <meta charset="UTF-8">
                <title>راهنمای افزونه دانلود منجر حامد</title>
                <style>
                    body { 
                        font-family: 'Segoe UI', Tahoma, Arial, sans-serif; 
                        line-height: 1.6; 
                        max-width: 800px; 
                        margin: 0 auto; 
                        padding: 20px;
                        background: #f5f5f5;
                    }
                    .container {
                        background: white;
                        padding: 30px;
                        border-radius: 10px;
                        box-shadow: 0 4px 20px rgba(0,0,0,0.1);
                    }
                    h1 { color: #333; text-align: center; }
                    pre { 
                        background: #f8f9fa; 
                        padding: 20px; 
                        border-radius: 8px; 
                        white-space: pre-wrap;
                        font-family: inherit;
                    }
                </style>
            </head>
            <body>
                <div class="container">
                    <h1>🚀 راهنمای افزونه دانلود منجر حامد</h1>
                    <pre>${helpContent}</pre>
                </div>
            </body>
            </html>
        `)}`
    });
}

// Enhanced batch download with user selection
async function handleBatchDownloadWithSelection(links, tab) {
    if (!links || links.length === 0) {
        showNotification('خطا', 'هیچ لینکی برای دانلود یافت نشد', 'error');
        return;
    }
    
    // For now, send all links. In the future, we could show a selection dialog
    const linkData = links.map(link => ({
        url: typeof link === 'string' ? link : link.url,
        filename: typeof link === 'object' ? link.filename : null,
        referrer: tab.url,
        title: tab.title,
        userAgent: navigator.userAgent,
        timestamp: Date.now()
    }));
    
    await sendToDownloadManager(linkData, 'batch-selected');
}

// Periodic connection check (every 5 minutes)
setInterval(async () => {
    try {
        const response = await fetch(`${CONFIG.API_BASE_URL}${CONFIG.API_ENDPOINTS.STATUS}`, {
            method: 'GET',
            timeout: 5000
        });
        
        if (response.ok) {
            console.log('ارتباط با دانلود منجر حامد برقرار است');
        }
    } catch (error) {
        console.warn('دانلود منجر حامد در دسترس نیست:', error.message);
    }
}, 5 * 60 * 1000);

console.log('دانلود منجر حامد - Service Worker آماده است'); 