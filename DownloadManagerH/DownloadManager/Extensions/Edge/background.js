// Enhanced Edge Extension for Download Manager Hamed
// Native Messaging Implementation - Manifest V3 Service Worker

// Configuration
const CONFIG = {
    NATIVE_HOST_NAME: 'com.hameddownloadmanager.host',
    RETRY_ATTEMPTS: 3,
    RETRY_DELAY: 1000,
    CONNECTION_TIMEOUT: 10000,
    BROWSER_PRIORITY: 'critical', // Edge has highest priority
    MESSAGE_TIMEOUT: 30000
};

// Native messaging connection
let nativePort = null;
let connectionAttempts = 0;
let isConnecting = false;

// Extension state management
let extensionSettings = {
    autoDetectDownloads: true,
    showNotifications: true,
    batchDownloadEnabled: true,
    maxBatchSize: 50,
    enableDownloadInterception: true
};

// Message queue for when native host is not connected
let messageQueue = [];
let pendingMessages = new Map(); // For tracking message responses

// Initialize extension
chrome.runtime.onInstalled.addListener(async (details) => {
    console.log('دانلود منجر حامد - افزونه Edge نصب شد');
    
    // Load settings from storage
    await loadSettings();
    
    // Setup context menus
    await setupContextMenus();
    
    // Connect to native host
    await connectToNativeHost();
    
    // Show welcome notification for new installs
    if (details.reason === 'install') {
        showNotification(
            'دانلود منجر حامد',
            'افزونه Edge با موفقیت نصب شد! اکنون می‌توانید لینک‌ها را به راحتی ارسال کنید.',
            'success'
        );
    }
});

// Connect to native messaging host
async function connectToNativeHost() {
    if (isConnecting || (nativePort && nativePort.name)) {
        return; // Already connecting or connected
    }
    
    isConnecting = true;
    connectionAttempts++;
    
    try {
        console.log(`تلاش اتصال به Native Host (تلاش ${connectionAttempts})...`);
        
        nativePort = chrome.runtime.connectNative(CONFIG.NATIVE_HOST_NAME);
        
        if (!nativePort) {
            throw new Error('Failed to create native port');
        }
        
        // Handle incoming messages
        nativePort.onMessage.addListener(handleNativeMessage);
        
        // Handle disconnection
        nativePort.onDisconnect.addListener(handleNativeDisconnect);
        
        // Send initial connection message
        const connectionMessage = {
            type: 'getStatus',
            id: generateMessageId(),
            timestamp: Date.now(),
            browser: 'edge',
            priority: CONFIG.BROWSER_PRIORITY,
            version: '1.0'
        };
        
        await sendNativeMessage(connectionMessage);
        
        console.log('✅ اتصال به Native Host برقرار شد');
        connectionAttempts = 0; // Reset on successful connection
        
        // Process queued messages
        await processMessageQueue();
        
    } catch (error) {
        console.error('❌ خطا در اتصال به Native Host:', error);
        handleConnectionError(error);
    } finally {
        isConnecting = false;
    }
}

// Handle incoming messages from native host
function handleNativeMessage(message) {
    console.log('پیام دریافت شده از Native Host:', message);
    
    try {
        // Handle response messages
        if (message.id && pendingMessages.has(message.id)) {
            const { resolve, reject } = pendingMessages.get(message.id);
            pendingMessages.delete(message.id);
            
            if (message.data && message.data.success) {
                resolve(message);
            } else {
                reject(new Error(message.data?.message || 'Unknown error'));
            }
            return;
        }
        
        // Handle different message types
        switch (message.type) {
            case 'statusResponse':
                handleStatusResponse(message);
                break;
                
            case 'settingsResponse':
                handleSettingsResponse(message);
                break;
                
            case 'response':
                handleGenericResponse(message);
                break;
                
            default:
                console.log('نوع پیام ناشناخته:', message.type);
        }
        
    } catch (error) {
        console.error('خطا در پردازش پیام Native Host:', error);
    }
}

// Handle native host disconnection
function handleNativeDisconnect() {
    const error = chrome.runtime.lastError;
    console.log('اتصال Native Host قطع شد:', error?.message || 'Unknown reason');
    
    nativePort = null;
    
    // Reject all pending messages
    for (const [id, { reject }] of pendingMessages) {
        reject(new Error('Native host disconnected'));
    }
    pendingMessages.clear();
    
    // Attempt to reconnect after delay
    if (connectionAttempts < CONFIG.RETRY_ATTEMPTS) {
        setTimeout(() => {
            connectToNativeHost();
        }, CONFIG.RETRY_DELAY * connectionAttempts);
    } else {
        console.error('حداکثر تلاش برای اتصال مجدد انجام شد');
        showNotification(
            'خطا در ارتباط',
            'ارتباط با دانلود منجر حامد برقرار نشد. لطفاً برنامه را اجرا کنید.',
            'error'
        );
    }
}

// Handle connection errors
function handleConnectionError(error) {
    console.error('خطای اتصال Native Host:', error);
    
    if (connectionAttempts < CONFIG.RETRY_ATTEMPTS) {
        setTimeout(() => {
            connectToNativeHost();
        }, CONFIG.RETRY_DELAY * connectionAttempts);
    }
}

// Send message to native host
async function sendNativeMessage(message, expectResponse = true) {
    return new Promise((resolve, reject) => {
        if (!nativePort) {
            // Queue message if not connected
            messageQueue.push({ message, resolve, reject, expectResponse });
            
            // Try to connect
            connectToNativeHost();
            return;
        }
        
        try {
            // Add message to pending if expecting response
            if (expectResponse) {
                pendingMessages.set(message.id, { resolve, reject });
                
                // Set timeout for response
                setTimeout(() => {
                    if (pendingMessages.has(message.id)) {
                        pendingMessages.delete(message.id);
                        reject(new Error('Message timeout'));
                    }
                }, CONFIG.MESSAGE_TIMEOUT);
            }
            
            nativePort.postMessage(message);
            console.log('پیام ارسال شده به Native Host:', message);
            
            if (!expectResponse) {
                resolve({ success: true });
            }
            
        } catch (error) {
            if (expectResponse && pendingMessages.has(message.id)) {
                pendingMessages.delete(message.id);
            }
            reject(error);
        }
    });
}

// Process queued messages
async function processMessageQueue() {
    while (messageQueue.length > 0 && nativePort) {
        const { message, resolve, reject, expectResponse } = messageQueue.shift();
        
        try {
            const result = await sendNativeMessage(message, expectResponse);
            resolve(result);
        } catch (error) {
            reject(error);
        }
    }
}

// Generate unique message ID
function generateMessageId() {
    return `edge_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
}

// Handle status response
function handleStatusResponse(message) {
    if (message.data) {
        console.log('وضعیت دانلود منجر:', message.data);
        // Update extension badge or UI based on status
    }
}

// Handle settings response
function handleSettingsResponse(message) {
    if (message.data) {
        console.log('تنظیمات دانلود منجر:', message.data);
        // Update extension settings based on main app settings
    }
}

// Handle generic response
function handleGenericResponse(message) {
    if (message.data) {
        if (message.data.success) {
            console.log('عملیات موفق:', message.data.message);
            if (message.data.addedCount > 0) {
                showNotification(
                    'دانلود منجر حامد',
                    `${message.data.addedCount} لینک با موفقیت اضافه شد`,
                    'success'
                );
            }
        } else {
            console.error('خطا در عملیات:', message.data.message);
            showNotification('خطا', message.data.message, 'error');
        }
    }
}

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
        
        console.log('منوهای زمینه‌ای پیشرفته Edge با موفقیت ایجاد شدند');
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

// Send data to download manager via Native Messaging
async function sendToDownloadManager(linkData, requestType) {
    try {
        // Prepare download links data
        const links = linkData.map(link => ({
            url: link.url,
            filename: link.filename || null,
            referrer: link.referrer || null,
            headers: link.headers || null,
            cookies: link.cookies || null,
            intercepted: link.intercepted || false,
            totalBytes: link.totalBytes || null,
            mimeType: link.mimeType || null
        }));
        
        // Create add download message
        const message = {
            type: 'addDownload',
            id: generateMessageId(),
            timestamp: Date.now(),
            browser: 'edge',
            priority: CONFIG.BROWSER_PRIORITY,
            version: '1.0',
            data: {
                links: links,
                requestType: requestType || 'single',
                group: null, // Can be set based on user preference
                savePath: null // Use default save path
            }
        };
        
        // Send message and wait for response
        const response = await sendNativeMessage(message, true);
        
        if (response && response.data && response.data.success) {
            if (extensionSettings.showNotifications) {
                const addedCount = response.data.addedCount || linkData.length;
                const message = addedCount === 1 
                    ? 'لینک با موفقیت ارسال شد!'
                    : `${addedCount} لینک با موفقیت ارسال شد!`;
                
                showNotification('دانلود منجر حامد', message, 'success');
            }
            
            console.log(`${linkData.length} لینک با موفقیت ارسال شد`);
        } else {
            throw new Error(response?.data?.message || 'Unknown error');
        }
        
    } catch (error) {
        console.error('خطا در ارسال لینک‌ها:', error);
        
        // Show appropriate error message
        let errorMessage = 'خطا در ارسال لینک‌ها';
        if (error.message.includes('Native host disconnected') || error.message.includes('timeout')) {
            errorMessage = 'ارتباط با دانلود منجر حامد برقرار نشد. لطفاً برنامه را اجرا کنید.';
        } else if (error.message.includes('Invalid URL')) {
            errorMessage = 'آدرس لینک معتبر نیست';
        }
        
        showNotification('خطا در ارتباط', errorMessage, 'error');
        
        // Try to reconnect if connection was lost
        if (!nativePort) {
            connectToNativeHost();
        }
    }
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
chrome.action.onClicked.addListener(async () => {
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
            
        case 'checkNativeConnection':
            sendResponse({ connected: nativePort !== null });
            break;
            
        case 'testNativeConnection':
            (async () => {
                try {
                    if (!nativePort) {
                        await connectToNativeHost();
                    }
                    
                    const testMessage = {
                        type: 'getStatus',
                        id: generateMessageId(),
                        timestamp: Date.now(),
                        browser: 'edge',
                        priority: CONFIG.BROWSER_PRIORITY,
                        version: '1.0'
                    };
                    
                    await sendNativeMessage(testMessage, true);
                    sendResponse({ success: true });
                } catch (error) {
                    sendResponse({ success: false, error: error.message });
                }
            })();
            return true; // Keep message channel open for async response
            
        case 'sendLinks':
            (async () => {
                try {
                    await handleSingleLink(request.url, request.tab || sender.tab);
                    sendResponse({ success: true });
                } catch (error) {
                    sendResponse({ success: false, error: error.message });
                }
            })();
            return true; // Keep message channel open for async response
            
        case 'focusMainApp':
            (async () => {
                try {
                    await openMainApplication();
                    sendResponse({ success: true });
                } catch (error) {
                    sendResponse({ success: false, error: error.message });
                }
            })();
            return true; // Keep message channel open for async response
            
        case 'sendDetectedLinks':
            (async () => {
                try {
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
                        
                        await sendToDownloadManager(linkData, 'detected');
                    }
                    sendResponse({ success: true });
                } catch (error) {
                    sendResponse({ success: false, error: error.message });
                }
            })();
            return true; // Keep message channel open for async response
            
        case 'batchDownload':
            (async () => {
                try {
                    if (request.urls && Array.isArray(request.urls)) {
                        const linkData = request.urls.map(url => ({
                            url: url,
                            referrer: sender.tab ? sender.tab.url : '',
                            title: sender.tab ? sender.tab.title : '',
                            userAgent: navigator.userAgent,
                            timestamp: Date.now()
                        }));
                        
                        await sendToDownloadManager(linkData, 'batch');
                    }
                    sendResponse({ success: true });
                } catch (error) {
                    sendResponse({ success: false, error: error.message });
                }
            })();
            return true; // Keep message channel open for async response
            
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
        const message = {
            type: 'focus',
            id: generateMessageId(),
            timestamp: Date.now(),
            browser: 'edge',
            priority: CONFIG.BROWSER_PRIORITY,
            version: '1.0'
        };
        
        await sendNativeMessage(message, false);
        showNotification('دانلود منجر حامد', 'برنامه اصلی فعال شد', 'success');
        
    } catch (error) {
        console.error('خطا در فعال‌سازی برنامه اصلی:', error);
        showNotification('خطا', 'خطا در باز کردن برنامه اصلی', 'error');
    }
}

// Show extension help
async function showExtensionHelp() {
    const helpContent = `
🚀 راهنمای استفاده از افزونه دانلود منجر حامد (Edge)

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
• پورت 9090 باید آزاد باشد
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
                <title>راهنمای افزونه دانلود منجر حامد (Edge)</title>
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
                    <h1>🚀 راهنمای افزونه دانلود منجر حامد (Edge)</h1>
                    <pre>${helpContent}</pre>
                </div>
            </body>
            </html>
        `)}`
    });
}

// Periodic connection check and status update (every 2 minutes)
setInterval(async () => {
    if (nativePort) {
        try {
            const statusMessage = {
                type: 'getStatus',
                id: generateMessageId(),
                timestamp: Date.now(),
                browser: 'edge',
                priority: CONFIG.BROWSER_PRIORITY,
                version: '1.0'
            };
            
            await sendNativeMessage(statusMessage, true);
            console.log('بررسی وضعیت دانلود منجر حامد انجام شد');
            
        } catch (error) {
            console.warn('خطا در بررسی وضعیت:', error.message);
        }
    } else {
        // Try to reconnect if not connected
        console.log('تلاش برای اتصال مجدد...');
        connectToNativeHost();
    }
}, 2 * 60 * 1000);

// Download interception for Edge
chrome.downloads.onCreated.addListener(async (downloadItem) => {
    if (!extensionSettings.enableDownloadInterception) {
        return;
    }
    
    try {
        // Check if this download should be intercepted
        if (shouldInterceptDownload(downloadItem)) {
            console.log('رهگیری دانلود:', downloadItem.url);
            
            // Cancel the browser download
            await chrome.downloads.cancel(downloadItem.id);
            
            // Send to download manager
            const interceptMessage = {
                type: 'interceptDownload',
                id: generateMessageId(),
                timestamp: Date.now(),
                browser: 'edge',
                priority: CONFIG.BROWSER_PRIORITY,
                version: '1.0',
                data: {
                    downloadId: downloadItem.id.toString(),
                    url: downloadItem.url,
                    filename: downloadItem.filename,
                    totalBytes: downloadItem.totalBytes,
                    mimeType: downloadItem.mime,
                    referrer: downloadItem.referrer
                }
            };
            
            await sendNativeMessage(interceptMessage, true);
            
            showNotification(
                'دانلود رهگیری شد',
                `دانلود "${downloadItem.filename}" به دانلود منجر حامد ارسال شد`,
                'success'
            );
        }
    } catch (error) {
        console.error('خطا در رهگیری دانلود:', error);
    }
});

// Determine if download should be intercepted
function shouldInterceptDownload(downloadItem) {
    // Don't intercept extension files or very small files
    if (downloadItem.totalBytes < 1024 * 100) { // Less than 100KB
        return false;
    }
    
    // Don't intercept if URL is from extension or local
    if (downloadItem.url.startsWith('chrome-extension://') || 
        downloadItem.url.startsWith('moz-extension://') ||
        downloadItem.url.startsWith('file://')) {
        return false;
    }
    
    // Check file extension for common downloadable types
    const filename = downloadItem.filename || '';
    const extension = filename.split('.').pop()?.toLowerCase();
    
    const interceptableExtensions = [
        'zip', 'rar', '7z', 'tar', 'gz', 'bz2',
        'exe', 'msi', 'dmg', 'pkg', 'deb', 'rpm',
        'pdf', 'doc', 'docx', 'xls', 'xlsx', 'ppt', 'pptx',
        'mp3', 'mp4', 'avi', 'mkv', 'mov', 'wmv', 'flv',
        'jpg', 'jpeg', 'png', 'gif', 'bmp', 'svg', 'webp',
        'iso', 'img', 'bin', 'apk', 'ipa'
    ];
    
    return extension && interceptableExtensions.includes(extension);
}

// Handle startup connection
chrome.runtime.onStartup.addListener(() => {
    console.log('Edge Service Worker شروع شد');
    connectToNativeHost();
});

console.log('دانلود منجر حامد - Edge Service Worker آماده است');