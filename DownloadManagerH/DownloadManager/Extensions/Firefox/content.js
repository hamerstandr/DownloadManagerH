// Content Script for Download Manager Hamed Firefox Extension
// Provides enhanced page integration and link detection

(function() {
    'use strict';
    
    // Configuration
    const CONFIG = {
        HOVER_DELAY: 500,
        BUTTON_FADE_DELAY: 3000,
        DOWNLOADABLE_EXTENSIONS: [
            'zip', 'rar', '7z', 'tar', 'gz', 'bz2',
            'exe', 'msi', 'dmg', 'pkg', 'deb', 'rpm',
            'pdf', 'doc', 'docx', 'xls', 'xlsx', 'ppt', 'pptx',
            'mp3', 'mp4', 'avi', 'mkv', 'mov', 'wmv', 'flv',
            'jpg', 'jpeg', 'png', 'gif', 'bmp', 'svg', 'webp',
            'iso', 'img', 'bin', 'apk', 'ipa'
        ]
    };
    
    let extensionSettings = {};
    let hoverTimeout = null;
    let downloadButton = null;
    
    // Initialize content script
    init();
    
    async function init() {
        try {
            // Get extension settings
            const response = await browser.runtime.sendMessage({ action: 'getSettings' });
            if (response && response.settings) {
                extensionSettings = response.settings;
            }
            
            // Setup page enhancements if enabled
            if (extensionSettings.autoDetectDownloads) {
                setupLinkEnhancements();
                setupKeyboardShortcuts();
            }
            
            console.log('دانلود منجر حامد - Content Script آماده است');
        } catch (error) {
            console.error('خطا در مقداردهی Content Script:', error);
        }
    }
    
    // Setup link hover enhancements
    function setupLinkEnhancements() {
        // Add hover effects to downloadable links
        document.addEventListener('mouseover', handleLinkHover);
        document.addEventListener('mouseout', handleLinkMouseOut);
        
        // Add click interceptor for downloadable links
        document.addEventListener('click', handleLinkClick, true);
    }
    
    // Handle link hover
    function handleLinkHover(event) {
        const link = event.target.closest('a[href]');
        if (!link || !isDownloadableLink(link.href)) return;
        
        // Clear existing timeout
        if (hoverTimeout) {
            clearTimeout(hoverTimeout);
        }
        
        // Show download button after delay
        hoverTimeout = setTimeout(() => {
            showDownloadButton(link);
        }, CONFIG.HOVER_DELAY);
    }
    
    // Handle link mouse out
    function handleLinkMouseOut(event) {
        const link = event.target.closest('a[href]');
        if (!link) return;
        
        // Clear hover timeout
        if (hoverTimeout) {
            clearTimeout(hoverTimeout);
            hoverTimeout = null;
        }
        
        // Hide download button after delay
        setTimeout(() => {
            hideDownloadButton();
        }, CONFIG.BUTTON_FADE_DELAY);
    }
    
    // Handle link clicks
    function handleLinkClick(event) {
        const link = event.target.closest('a[href]');
        if (!link || !isDownloadableLink(link.href)) return;
        
        // Check if Ctrl/Cmd key is pressed for direct download
        if (event.ctrlKey || event.metaKey) {
            event.preventDefault();
            event.stopPropagation();
            
            sendLinkToDownloadManager(link.href);
            
            // Show visual feedback
            showLinkSentFeedback(link);
        }
    }
    
    // Check if link is downloadable
    function isDownloadableLink(url) {
        try {
            const urlObj = new URL(url);
            const pathname = urlObj.pathname.toLowerCase();
            const extension = pathname.split('.').pop();
            
            return CONFIG.DOWNLOADABLE_EXTENSIONS.includes(extension);
        } catch (error) {
            return false;
        }
    }
    
    // Show download button near link
    function showDownloadButton(link) {
        // Remove existing button
        hideDownloadButton();
        
        // Create download button
        downloadButton = document.createElement('div');
        downloadButton.className = 'dmh-download-button';
        downloadButton.innerHTML = '🚀';
        downloadButton.title = 'دانلود با دانلود منجر حامد (کلیک کنید)';
        
        // Style the button
        Object.assign(downloadButton.style, {
            position: 'absolute',
            zIndex: '10000',
            backgroundColor: '#4CAF50',
            color: 'white',
            border: 'none',
            borderRadius: '50%',
            width: '30px',
            height: '30px',
            fontSize: '16px',
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            boxShadow: '0 2px 8px rgba(0,0,0,0.3)',
            transition: 'all 0.3s ease',
            opacity: '0',
            transform: 'scale(0.8)'
        });
        
        // Position button near link
        const rect = link.getBoundingClientRect();
        downloadButton.style.left = (rect.right + window.scrollX + 5) + 'px';
        downloadButton.style.top = (rect.top + window.scrollY) + 'px';
        
        // Add click handler
        downloadButton.addEventListener('click', (e) => {
            e.preventDefault();
            e.stopPropagation();
            sendLinkToDownloadManager(link.href);
            showLinkSentFeedback(link);
            hideDownloadButton();
        });
        
        // Add to page
        document.body.appendChild(downloadButton);
        
        // Animate in
        requestAnimationFrame(() => {
            downloadButton.style.opacity = '1';
            downloadButton.style.transform = 'scale(1)';
        });
    }
    
    // Hide download button
    function hideDownloadButton() {
        if (downloadButton) {
            downloadButton.style.opacity = '0';
            downloadButton.style.transform = 'scale(0.8)';
            
            setTimeout(() => {
                if (downloadButton && downloadButton.parentNode) {
                    downloadButton.parentNode.removeChild(downloadButton);
                }
                downloadButton = null;
            }, 300);
        }
    }
    
    // Send link to download manager
    async function sendLinkToDownloadManager(url) {
        try {
            await browser.runtime.sendMessage({
                action: 'sendLinks',
                url: url
            });
        } catch (error) {
            console.error('خطا در ارسال لینک:', error);
        }
    }
    
    // Show visual feedback when link is sent
    function showLinkSentFeedback(link) {
        // Create feedback element
        const feedback = document.createElement('div');
        feedback.className = 'dmh-link-sent-feedback';
        feedback.innerHTML = '✅ ارسال شد';
        
        // Style feedback
        Object.assign(feedback.style, {
            position: 'absolute',
            zIndex: '10001',
            backgroundColor: '#4CAF50',
            color: 'white',
            padding: '5px 10px',
            borderRadius: '15px',
            fontSize: '12px',
            fontFamily: 'Arial, sans-serif',
            boxShadow: '0 2px 8px rgba(0,0,0,0.3)',
            opacity: '0',
            transform: 'translateY(-10px)',
            transition: 'all 0.3s ease',
            pointerEvents: 'none'
        });
        
        // Position near link
        const rect = link.getBoundingClientRect();
        feedback.style.left = (rect.left + window.scrollX) + 'px';
        feedback.style.top = (rect.top + window.scrollY - 35) + 'px';
        
        // Add to page
        document.body.appendChild(feedback);
        
        // Animate in
        requestAnimationFrame(() => {
            feedback.style.opacity = '1';
            feedback.style.transform = 'translateY(0)';
        });
        
        // Remove after delay
        setTimeout(() => {
            feedback.style.opacity = '0';
            feedback.style.transform = 'translateY(-10px)';
            
            setTimeout(() => {
                if (feedback.parentNode) {
                    feedback.parentNode.removeChild(feedback);
                }
            }, 300);
        }, 2000);
    }
    
    // Setup keyboard shortcuts
    function setupKeyboardShortcuts() {
        document.addEventListener('keydown', (event) => {
            // Ctrl+Shift+D: Detect all downloadable links on page
            if (event.ctrlKey && event.shiftKey && event.key === 'D') {
                event.preventDefault();
                detectAndHighlightDownloadableLinks();
            }
            
            // Escape: Hide download button
            if (event.key === 'Escape') {
                hideDownloadButton();
            }
        });
    }
    
    // Detect and highlight all downloadable links on page
    function detectAndHighlightDownloadableLinks() {
        const links = document.querySelectorAll('a[href]');
        const downloadableLinks = [];
        
        links.forEach(link => {
            if (isDownloadableLink(link.href)) {
                downloadableLinks.push(link);
                
                // Add highlight effect
                link.style.outline = '2px solid #4CAF50';
                link.style.outlineOffset = '2px';
                link.title = (link.title || '') + ' [قابل دانلود با دانلود منجر حامد]';
                
                // Remove highlight after delay
                setTimeout(() => {
                    link.style.outline = '';
                    link.style.outlineOffset = '';
                }, 5000);
            }
        });
        
        // Show summary notification
        if (downloadableLinks.length > 0) {
            showPageNotification(`${downloadableLinks.length} لینک قابل دانلود یافت شد`);
        } else {
            showPageNotification('هیچ لینک قابل دانلودی یافت نشد');
        }
    }
    
    // Show page notification
    function showPageNotification(message) {
        const notification = document.createElement('div');
        notification.className = 'dmh-page-notification';
        notification.textContent = message;
        
        Object.assign(notification.style, {
            position: 'fixed',
            top: '20px',
            right: '20px',
            zIndex: '10002',
            backgroundColor: '#333',
            color: 'white',
            padding: '10px 15px',
            borderRadius: '5px',
            fontSize: '14px',
            fontFamily: 'Arial, sans-serif',
            boxShadow: '0 4px 12px rgba(0,0,0,0.3)',
            opacity: '0',
            transform: 'translateX(100%)',
            transition: 'all 0.3s ease'
        });
        
        document.body.appendChild(notification);
        
        // Animate in
        requestAnimationFrame(() => {
            notification.style.opacity = '1';
            notification.style.transform = 'translateX(0)';
        });
        
        // Remove after delay
        setTimeout(() => {
            notification.style.opacity = '0';
            notification.style.transform = 'translateX(100%)';
            
            setTimeout(() => {
                if (notification.parentNode) {
                    notification.parentNode.removeChild(notification);
                }
            }, 300);
        }, 3000);
    }
    
    // Clean up on page unload
    window.addEventListener('beforeunload', () => {
        hideDownloadButton();
        if (hoverTimeout) {
            clearTimeout(hoverTimeout);
        }
    });
    
})();
