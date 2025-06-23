// Khai báo biến global trước
let connection = null;
let messages = [];
let isOpen = false;
let unreadCount = 0;

// Initialize SignalR connection
function initializeConnection() {
    connection = new signalR.HubConnectionBuilder()
        .withUrl("/chatHub")
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Information)
        .build();

    // Connection events
    connection.onreconnecting(() => {
        console.log("SignalR Reconnecting...");
    });

    connection.onreconnected(() => {
        console.log("SignalR Reconnected");
        connection.invoke("GetConversation").catch(err => console.error(err));
    });

    connection.onclose(() => {
        console.log("SignalR Disconnected");
        setTimeout(start, 5000);
    });

    // Receive conversation history
    connection.on("ConversationHistory", (history) => {
        messages = history || [];
        if (messages.length > 0) {
            displayMessages();
        }
    });

    // Receive bot message
    connection.on("BotMessage", (message) => {
        messages.push(message);
        displayMessages();
        hideTyping();

        if (!isOpen) {
            unreadCount++;
            updateUnreadBadge();
        }
    });

    // User message sent confirmation
    connection.on("UserMessageSent", (message) => {
        console.log("Message sent:", message);
    });

    // Bot typing
    connection.on("BotTyping", () => {
        showTyping();
    });

    // Error handling
    connection.on("Error", (error) => {
        console.error("Chat error:", error);
        hideTyping();
    });
}

// Start connection
async function start() {
    try {
        if (!connection) {
            initializeConnection();
        }

        await connection.start();
        console.log("SignalR Connected");

        // Get token if exists
        const token = localStorage.getItem('token');
        if (token) {
            await connection.invoke("Authenticate", token).catch(err => console.error(err));
        } else {
            await connection.invoke("GetConversation").catch(err => console.error(err));
        }
    } catch (err) {
        console.error("SignalR Connection Error: ", err);
        setTimeout(start, 5000);
    }
}

// Toggle chat window
window.toggleChat = function () {
    isOpen = !isOpen;
    const container = document.getElementById('chat-container');
    const launcher = document.getElementById('chat-launcher');

    if (isOpen) {
        container.style.display = 'flex';
        launcher.style.display = 'none';
        unreadCount = 0;
        updateUnreadBadge();

        // Focus input
        setTimeout(() => {
            const input = document.getElementById('chat-input');
            if (input) input.focus();
        }, 100);

        // Load conversation if empty
        if (messages.length === 0 && connection && connection.state === signalR.HubConnectionState.Connected) {
            connection.invoke("GetConversation").catch(err => console.error(err));
        }
    } else {
        container.style.display = 'none';
        launcher.style.display = 'flex';
    }
}

// Send message
window.sendMessage = async function () {
    const input = document.getElementById('chat-input');
    const text = input.value.trim();

    if (!text) return;

    // Check connection before sending
    if (!connection || connection.state !== signalR.HubConnectionState.Connected) {
        console.error("Not connected to chat server");
        return;
    }

    // Add user message to UI immediately
    const userMessage = {
        sender: 'user',
        message: text,
        time: new Date()
    };
    messages.push(userMessage);
    displayMessages();

    // Clear input
    input.value = '';
    input.focus();

    // Send to server
    try {
        await connection.invoke("UserMessage", text);
    } catch (err) {
        console.error("Error sending message:", err);
        hideTyping();
        // Remove the failed message
        messages.pop();
        displayMessages();
        // Show error to user
        alert("Không thể gửi tin nhắn. Vui lòng thử lại!");
    }
}

// Send suggested message
window.sendSuggestedMessage = function (text) {
    const input = document.getElementById('chat-input');
    if (input) {
        input.value = text;
        sendMessage();
    }
}

// Display messages
// Display messages
function displayMessages() {
    const container = document.getElementById('chat-messages');
    if (!container) return;

    const welcomeDiv = container.querySelector('.chat-welcome');
    if (welcomeDiv) {
        welcomeDiv.style.display = messages.length > 0 ? 'none' : 'flex';
    }

    // Xóa tin nhắn cũ
    const existingMessages = container.querySelectorAll('.chat-message:not(.bot:has(.typing-indicator))');
    existingMessages.forEach(msg => {
        if (!msg.querySelector('.typing-indicator')) {
            msg.remove();
        }
    });

    // Tìm và lưu trữ typing indicator
    const typingIndicator = document.getElementById('typing-indicator');

    // Thêm tin nhắn mới
    messages.forEach(msg => {
        const messageDiv = createMessageElement(msg);
        container.appendChild(messageDiv);
    });

    // Di chuyển typing indicator xuống cuối nếu có
    if (typingIndicator && typingIndicator.parentNode) {
        typingIndicator.parentNode.appendChild(typingIndicator);
    }

    // Cuộn xuống cuối
    container.scrollTop = container.scrollHeight;
}

// Show typing indicator
function showTyping() {
    const container = document.getElementById('chat-messages');
    if (!container) return;

    const indicator = document.getElementById('typing-indicator');
    if (indicator) {
        indicator.style.display = 'flex';
        container.scrollTop = container.scrollHeight;
    }
}

// Hide typing indicator
function hideTyping() {
    const indicator = document.getElementById('typing-indicator');
    if (indicator) {
        indicator.style.display = 'none';
    }
}

// Create message element
function createMessageElement(msg) {
    const div = document.createElement('div');
    div.className = `chat-message ${msg.sender}`;

    const avatar = document.createElement('div');
    avatar.className = 'message-avatar';
    avatar.textContent = msg.sender === 'bot' ? '🤖' : '👤';

    const content = document.createElement('div');
    content.className = 'message-content';

    // Process message for links
    content.innerHTML = processMessageLinks(msg.message);

    if (msg.sender === 'user') {
        div.appendChild(content);
        div.appendChild(avatar);
    } else {
        div.appendChild(avatar);
        div.appendChild(content);
    }

    return div;
}

// Process message links
function processMessageLinks(text) {
    if (!text) return '';

    // Escape HTML first
    const div = document.createElement('div');
    div.textContent = text;
    let escapedText = div.innerHTML;

    // Convert các pattern khác nhau của ID
    // Pattern 1: **Tên địa điểm (ID: X):**
    escapedText = escapedText.replace(/\*\*(.+?)\s*\(ID:\s*(\d+)\):\*\*/g,
        '<strong><a href="/Locations/Details/$2" target="_blank" class="location-link">$1</a></strong>:');

    // Pattern 2: Tên địa điểm (ID: X) không có **
    escapedText = escapedText.replace(/([^*]+?)\s*\(ID:\s*(\d+)\)/g,
        '<a href="/Locations/Details/$2" target="_blank" class="location-link">$1</a>');

    // Pattern 3: (Tour ID: X)
    escapedText = escapedText.replace(/\(Tour ID:\s*(\d+)\)/g,
        '<a href="/Tours/Details/$1" target="_blank" class="tour-link">(Xem tour)</a>');

    // Convert ** thành <strong> cho các phần còn lại
    escapedText = escapedText.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>');

    // Convert * thành bullet points
    escapedText = escapedText.replace(/^\*\s+/gm, '• ');

    return escapedText;
}
// Show typing indicator
function showTyping() {
    const indicator = document.getElementById('typing-indicator');
    if (indicator) {
        indicator.style.display = 'flex';
        const container = document.getElementById('chat-messages');
        if (container) {
            container.scrollTop = container.scrollHeight;
        }
    }
}

// Hide typing indicator
function hideTyping() {
    const indicator = document.getElementById('typing-indicator');
    if (indicator) {
        indicator.style.display = 'none';
    }
}

// Update unread badge
function updateUnreadBadge() {
    const badge = document.querySelector('.unread-badge');
    if (badge) {
        if (unreadCount > 0) {
            badge.style.display = 'flex';
            badge.textContent = unreadCount > 9 ? '9+' : unreadCount;
        } else {
            badge.style.display = 'none';
        }
    }
}

// Initialize when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    console.log("Chat widget initializing...");

    // Kiểm tra nếu SignalR đã được tải
    if (typeof signalR === 'undefined') {
        console.error("Thư viện SignalR không được tìm thấy! Chức năng chat sẽ bị vô hiệu hóa.");

        // Ẩn nút launcher và hiển thị thông báo lỗi
        const launcher = document.getElementById('chat-launcher');
        if (launcher) launcher.style.display = 'none';

        return;
    }

    // Phần code còn lại giữ nguyên...
    // Add enter key handler
    const input = document.getElementById('chat-input');
    if (input) {
        input.addEventListener('keypress', (e) => {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                sendMessage();
            }
        });
    }

    // Start SignalR connection
    start();
});

// Make functions available globally for onclick handlers
window.toggleChat = toggleChat;
window.sendMessage = sendMessage;
window.sendSuggestedMessage = sendSuggestedMessage;


//// Chat Widget JavaScript - Enhanced Version
//(function () {
//    'use strict';

//    // Global variables
//    let connection = null;
//    let messages = [];
//    let isOpen = false;
//    let unreadCount = 0;
//    let currentLanguage = 'vi';
//    let isVoiceEnabled = true;
//    let isRecording = false;
//    let recognition = null;
//    let synthesis = window.speechSynthesis;

//    // Language configurations
//    const languages = {
//        'vi': {
//            name: 'Tiếng Việt',
//            flag: '🇻🇳',
//            voiceLang: 'vi-VN',
//            placeholder: 'Nhập câu hỏi của bạn...',
//            voiceNotSupported: 'Trình duyệt không hỗ trợ nhận dạng giọng nói',
//            listening: 'Đang lắng nghe...',
//            sendButton: 'Gửi',
//            suggestions: 'Gợi ý:'
//        },
//        'en': {
//            name: 'English',
//            flag: '🇬🇧',
//            voiceLang: 'en-US',
//            placeholder: 'Type your question...',
//            voiceNotSupported: 'Voice recognition not supported',
//            listening: 'Listening...',
//            sendButton: 'Send',
//            suggestions: 'Suggestions:'
//        },
//        'ko': {
//            name: '한국어',
//            flag: '🇰🇷',
//            voiceLang: 'ko-KR',
//            placeholder: '질문을 입력하세요...',
//            voiceNotSupported: '음성 인식이 지원되지 않습니다',
//            listening: '듣고 있습니다...',
//            sendButton: '보내기',
//            suggestions: '제안:'
//        },
//        'zh': {
//            name: '中文',
//            flag: '🇨🇳',
//            voiceLang: 'zh-CN',
//            placeholder: '请输入您的问题...',
//            voiceNotSupported: '不支持语音识别',
//            listening: '正在聆听...',
//            sendButton: '发送',
//            suggestions: '建议：'
//        }
//    };

//    // Initialize voice recognition
//    function initializeVoiceRecognition() {
//        if ('webkitSpeechRecognition' in window || 'SpeechRecognition' in window) {
//            const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
//            recognition = new SpeechRecognition();
//            recognition.continuous = false;
//            recognition.interimResults = true;
//            recognition.maxAlternatives = 1;

//            recognition.onstart = () => {
//                isRecording = true;
//                updateVoiceButton(true);
//                showListeningIndicator();
//            };

//            recognition.onresult = (event) => {
//                const last = event.results.length - 1;
//                const transcript = event.results[last][0].transcript;

//                // Update input field with interim results
//                const input = document.getElementById('chat-input');
//                if (input) {
//                    input.value = transcript;
//                }

//                // Send message if final result
//                if (event.results[last].isFinal) {
//                    sendMessage(transcript, true);
//                }
//            };

//            recognition.onerror = (event) => {
//                console.error('Speech recognition error:', event.error);
//                isRecording = false;
//                updateVoiceButton(false);
//                hideListeningIndicator();

//                if (event.error === 'no-speech') {
//                    showToast(languages[currentLanguage].voiceNotSupported);
//                }
//            };

//            recognition.onend = () => {
//                isRecording = false;
//                updateVoiceButton(false);
//                hideListeningIndicator();
//            };
//        }
//    }

//    // Check if SignalR is loaded
//    if (typeof signalR === 'undefined') {
//        console.error('SignalR library is not loaded!');
//        return;
//    }

//    // Initialize SignalR connection
//    function initializeConnection() {
//        connection = new signalR.HubConnectionBuilder()
//            .withUrl("/chatHub")
//            .withAutomaticReconnect()
//            .configureLogging(signalR.LogLevel.Information)
//            .build();

//        // Connection events
//        connection.onreconnecting(() => {
//            console.log("SignalR Reconnecting...");
//        });

//        connection.onreconnected(() => {
//            console.log("SignalR Reconnected");
//            connection.invoke("GetConversation").catch(err => console.error(err));
//        });

//        connection.onclose(() => {
//            console.log("SignalR Disconnected");
//            setTimeout(start, 5000);
//        });

//        setupEventHandlers();
//    }

//    // Setup SignalR event handlers
//    function setupEventHandlers() {
//        // Receive conversation history
//        connection.on("ConversationHistory", (history) => {
//            messages = history || [];
//            if (messages.length > 0) {
//                displayMessages();
//            }
//        });

//        // Receive bot message
//        connection.on("BotMessage", (message) => {
//            messages.push(message);
//            displayMessages();
//            hideTyping();

//            // Text to speech for bot messages
//            if (message.enableVoice && isVoiceEnabled) {
//                speakMessage(message.message);
//            }

//            // Show smart suggestions
//            if (message.suggestions && message.suggestions.length > 0) {
//                showSmartSuggestions(message.suggestions);
//            }

//            if (!isOpen) {
//                unreadCount++;
//                updateUnreadBadge();
//            }
//        });

//        // User message sent confirmation
//        connection.on("UserMessageSent", (message) => {
//            console.log("Message sent:", message);
//        });

//        // Bot typing
//        connection.on("BotTyping", () => {
//            showTyping();
//        });

//        // Language changed
//        connection.on("LanguageChanged", (language) => {
//            currentLanguage = language;
//            updateUILanguage();
//        });

//        // Error handling
//        connection.on("Error", (error) => {
//            console.error("Chat error:", error);
//            hideTyping();
//        });
//    }

//    // Start connection
//    async function start() {
//        try {
//            if (!connection) {
//                initializeConnection();
//            }

//            await connection.start();
//            console.log("SignalR Connected");

//            // Get saved language preference
//            const savedLang = localStorage.getItem('chatLanguage') || 'vi';
//            if (savedLang !== 'vi') {
//                await connection.invoke("SetLanguage", savedLang);
//            }

//            // Get token if exists
//            const token = localStorage.getItem('token');
//            if (token) {
//                await connection.invoke("Authenticate", token).catch(err => console.error(err));
//            } else {
//                await connection.invoke("GetConversation").catch(err => console.error(err));
//            }
//        } catch (err) {
//            console.error("SignalR Connection Error: ", err);
//            setTimeout(start, 5000);
//        }
//    }

//    // Toggle chat window
//    window.toggleChat = function () {
//        isOpen = !isOpen;
//        const container = document.getElementById('chat-container');
//        const launcher = document.getElementById('chat-launcher');

//        if (isOpen) {
//            container.style.display = 'flex';
//            launcher.style.display = 'none';
//            unreadCount = 0;
//            updateUnreadBadge();

//            // Focus input
//            setTimeout(() => {
//                const input = document.getElementById('chat-input');
//                if (input) input.focus();
//            }, 100);

//            // Load conversation if empty
//            if (messages.length === 0 && connection && connection.state === signalR.HubConnectionState.Connected) {
//                connection.invoke("GetConversation").catch(err => console.error(err));
//            }
//        } else {
//            container.style.display = 'none';
//            launcher.style.display = 'flex';
//            // Stop any ongoing speech
//            synthesis.cancel();
//        }
//    }

//    // Send message
//    window.sendMessage = async function (text, isVoiceInput = false) {
//        if (!text || !text.trim()) {
//            const input = document.getElementById('chat-input');
//            text = input ? input.value.trim() : '';
//        }

//        if (!text) return;

//        // Check connection before sending
//        if (!connection || connection.state !== signalR.HubConnectionState.Connected) {
//            console.error("Not connected to chat server");
//            return;
//        }

//        // Add user message to UI immediately
//        const userMessage = {
//            sender: 'user',
//            message: text,
//            time: new Date(),
//            isVoiceInput: isVoiceInput
//        };
//        messages.push(userMessage);
//        displayMessages();

//        // Clear input
//        const input = document.getElementById('chat-input');
//        if (input) {
//            input.value = '';
//            input.focus();
//        }

//        // Hide suggestions
//        hideSmartSuggestions();

//        // Send to server with voice flag
//        try {
//            await connection.invoke("UserMessage", text, isVoiceInput);
//        } catch (err) {
//            console.error("Error sending message:", err);
//            hideTyping();
//            messages.pop();
//            displayMessages();
//            alert("Không thể gửi tin nhắn. Vui lòng thử lại!");
//        }
//    }

//    // Voice input handling
//    function toggleVoiceInput() {
//        if (!recognition) {
//            showToast(languages[currentLanguage].voiceNotSupported);
//            return;
//        }

//        if (isRecording) {
//            recognition.stop();
//        } else {
//            recognition.lang = languages[currentLanguage].voiceLang;
//            try {
//                recognition.start();
//            } catch (err) {
//                console.error('Voice recognition error:', err);
//                showToast(languages[currentLanguage].voiceNotSupported);
//            }
//        }
//    }

//    // Text to speech
//    function speakMessage(text) {
//        if (!synthesis || !isVoiceEnabled) return;

//        // Cancel any ongoing speech
//        synthesis.cancel();

//        // Remove markdown and links from text
//        const cleanText = text
//            .replace(/\*\*(.*?)\*\*/g, '$1')
//            .replace(/\(ID:\s*\d+\)/g, '')
//            .replace(/\(Tour ID:\s*\d+\)/g, '');

//        const utterance = new SpeechSynthesisUtterance(cleanText);
//        utterance.lang = languages[currentLanguage].voiceLang;
//        utterance.rate = 0.9;
//        utterance.pitch = 1;
//        utterance.volume = 1;

//        // Find best voice for the language
//        const voices = synthesis.getVoices();
//        const langVoice = voices.find(voice =>
//            voice.lang.startsWith(languages[currentLanguage].voiceLang.split('-')[0])
//        );
//        if (langVoice) {
//            utterance.voice = langVoice;
//        }

//        synthesis.speak(utterance);
//    }

//    // Toggle voice output
//    function toggleVoiceOutput() {
//        isVoiceEnabled = !isVoiceEnabled;
//        localStorage.setItem('voiceEnabled', isVoiceEnabled);
//        updateVoiceOutputButton();

//        if (!isVoiceEnabled) {
//            synthesis.cancel();
//        }
//    }

//    // Language switching
//    async function switchLanguage(lang) {
//        if (lang === currentLanguage) return;

//        currentLanguage = lang;
//        localStorage.setItem('chatLanguage', lang);

//        // Update UI
//        updateUILanguage();

//        // Notify server
//        if (connection && connection.state === signalR.HubConnectionState.Connected) {
//            await connection.invoke("SetLanguage", lang);
//        }
//    }

//    // Update UI language
//    function updateUILanguage() {
//        const input = document.getElementById('chat-input');
//        if (input) {
//            input.placeholder = languages[currentLanguage].placeholder;
//        }

//        // Update language selector
//        const langButton = document.querySelector('.language-current');
//        if (langButton) {
//            langButton.textContent = languages[currentLanguage].flag;
//        }

//        // Update suggestions label
//        const suggestionsLabel = document.querySelector('.suggestions-label');
//        if (suggestionsLabel) {
//            suggestionsLabel.textContent = languages[currentLanguage].suggestions;
//        }
//    }

//    // Show smart suggestions
//    function showSmartSuggestions(suggestions) {
//        const container = document.getElementById('chat-messages');
//        if (!container) return;

//        // Remove existing suggestions
//        hideSmartSuggestions();

//        // Create suggestions container
//        const suggestionsDiv = document.createElement('div');
//        suggestionsDiv.id = 'smart-suggestions';
//        suggestionsDiv.className = 'smart-suggestions';
//        suggestionsDiv.innerHTML = `
//            <div class="suggestions-label">${languages[currentLanguage].suggestions}</div>
//            <div class="suggestions-list">
//                ${suggestions.map(s => `
//                    <button class="suggestion-btn" onclick="sendSuggestedMessage('${s.replace(/'/g, "\\'")}')">${s}</button>
//                `).join('')}
//            </div>
//        `;

//        container.appendChild(suggestionsDiv);
//        container.scrollTop = container.scrollHeight;
//    }

//    // Hide smart suggestions
//    function hideSmartSuggestions() {
//        const suggestions = document.getElementById('smart-suggestions');
//        if (suggestions) {
//            suggestions.remove();
//        }
//    }

//    // Send suggested message
//    window.sendSuggestedMessage = function (text) {
//        const input = document.getElementById('chat-input');
//        if (input) {
//            input.value = text;
//            sendMessage(text);
//        }
//    }

//    // Display messages
//    function displayMessages() {
//        const container = document.getElementById('chat-messages');
//        if (!container) return;

//        const welcomeDiv = container.querySelector('.chat-welcome');
//        if (welcomeDiv) {
//            welcomeDiv.style.display = messages.length > 0 ? 'none' : 'flex';
//        }

//        // Remove existing messages
//        container.querySelectorAll('.chat-message:not(#typing-indicator)').forEach(el => el.remove());

//        // Remove smart suggestions
//        hideSmartSuggestions();

//        // Add messages
//        messages.forEach(msg => {
//            const messageEl = createMessageElement(msg);
//            const typingIndicator = document.getElementById('typing-indicator');
//            if (typingIndicator) {
//                container.insertBefore(messageEl, typingIndicator);
//            } else {
//                container.appendChild(messageEl);
//            }
//        });

//        // Scroll to bottom
//        container.scrollTop = container.scrollHeight;
//    }

//    // Create message element
//    function createMessageElement(msg) {
//        const div = document.createElement('div');
//        div.className = `chat-message ${msg.sender}`;

//        const avatar = document.createElement('div');
//        avatar.className = 'message-avatar';
//        avatar.textContent = msg.sender === 'bot' ? '🤖' : '👤';

//        const content = document.createElement('div');
//        content.className = 'message-content';

//        // Add voice indicator for voice messages
//        if (msg.isVoiceInput && msg.sender === 'user') {
//            content.innerHTML = `<span class="voice-indicator">🎤</span> ${processMessageLinks(msg.message)}`;
//        } else {
//            content.innerHTML = processMessageLinks(msg.message);
//        }

//        if (msg.sender === 'user') {
//            div.appendChild(content);
//            div.appendChild(avatar);
//        } else {
//            div.appendChild(avatar);
//            div.appendChild(content);
//        }

//        return div;
//    }

//    // Process message links
//    function processMessageLinks(text) {
//        if (!text) return '';

//        const div = document.createElement('div');
//        div.textContent = text;
//        let escapedText = div.innerHTML;

//        escapedText = escapedText.replace(/\*\*(.+?)\s*\(ID:\s*(\d+)\):\*\*/g,
//            '<strong><a href="/Locations/Details/$2" target="_blank" class="location-link">$1</a></strong>:');

//        escapedText = escapedText.replace(/([^*]+?)\s*\(ID:\s*(\d+)\)/g,
//            '<a href="/Locations/Details/$2" target="_blank" class="location-link">$1</a>');

//        escapedText = escapedText.replace(/\(Tour ID:\s*(\d+)\)/g,
//            '<a href="/Tours/Details/$1" target="_blank" class="tour-link">(Xem tour)</a>');

//        escapedText = escapedText.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>');
//        escapedText = escapedText.replace(/^\*\s+/gm, '• ');

//        return escapedText;
//    }

//    // Show typing indicator
//    function showTyping() {
//        const container = document.getElementById('chat-messages');
//        if (!container) return;

//        hideTyping();

//        const typingDiv = document.createElement('div');
//        typingDiv.id = 'typing-indicator';
//        typingDiv.className = 'chat-message bot';
//        typingDiv.innerHTML = `
//            <div class="message-avatar">🤖</div>
//            <div class="message-content">
//                <div class="typing-indicator">
//                    <span></span>
//                    <span></span>
//                    <span></span>
//                </div>
//            </div>
//        `;

//        container.appendChild(typingDiv);
//        typingDiv.style.display = 'flex';
//        container.scrollTop = container.scrollHeight;
//    }

//    // Hide typing indicator
//    function hideTyping() {
//        const indicator = document.getElementById('typing-indicator');
//        if (indicator) {
//            indicator.remove();
//        }
//    }

//    // Update voice button state
//    function updateVoiceButton(recording) {
//        const voiceBtn = document.querySelector('.voice-input-btn');
//        if (voiceBtn) {
//            if (recording) {
//                voiceBtn.classList.add('recording');
//                voiceBtn.innerHTML = '🔴';
//            } else {
//                voiceBtn.classList.remove('recording');
//                voiceBtn.innerHTML = '🎤';
//            }
//        }
//    }

//    // Update voice output button
//    function updateVoiceOutputButton() {
//        const btn = document.querySelector('.voice-output-btn');
//        if (btn) {
//            btn.innerHTML = isVoiceEnabled ? '🔊' : '🔇';
//            btn.title = isVoiceEnabled ? 'Tắt giọng nói' : 'Bật giọng nói';
//        }
//    }

//    // Show listening indicator
//    function showListeningIndicator() {
//        const input = document.getElementById('chat-input');
//        if (input) {
//            input.placeholder = languages[currentLanguage].listening;
//            input.classList.add('listening');
//        }
//    }

//    // Hide listening indicator
//    function hideListeningIndicator() {
//        const input = document.getElementById('chat-input');
//        if (input) {
//            input.placeholder = languages[currentLanguage].placeholder;
//            input.classList.remove('listening');
//        }
//    }

//    // Show toast notification
//    function showToast(message) {
//        const toast = document.createElement('div');
//        toast.className = 'chat-toast';
//        toast.textContent = message;
//        document.body.appendChild(toast);

//        setTimeout(() => {
//            toast.classList.add('show');
//        }, 100);

//        setTimeout(() => {
//            toast.classList.remove('show');
//            setTimeout(() => toast.remove(), 300);
//        }, 3000);
//    }

//    // Update unread badge
//    function updateUnreadBadge() {
//        const badge = document.querySelector('.unread-badge');
//        if (badge) {
//            if (unreadCount > 0) {
//                badge.style.display = 'flex';
//                badge.textContent = unreadCount > 9 ? '9+' : unreadCount;
//            } else {
//                badge.style.display = 'none';
//            }
//        }
//    }

//    // Setup UI event listeners
//    function setupUIEventListeners() {
//        // Voice input button
//        const voiceBtn = document.querySelector('.voice-input-btn');
//        if (voiceBtn) {
//            voiceBtn.addEventListener('click', toggleVoiceInput);
//        }

//        // Voice output button
//        const voiceOutputBtn = document.querySelector('.voice-output-btn');
//        if (voiceOutputBtn) {
//            voiceOutputBtn.addEventListener('click', toggleVoiceOutput);
//        }

//        // Language selector
//        document.querySelectorAll('.language-option').forEach(option => {
//            option.addEventListener('click', (e) => {
//                const lang = e.target.getAttribute('data-lang');
//                if (lang) switchLanguage(lang);
//            });
//        });

//        // Enter key handler
//        const input = document.getElementById('chat-input');
//        if (input) {
//            input.addEventListener('keypress', (e) => {
//                if (e.key === 'Enter' && !e.shiftKey) {
//                    e.preventDefault();
//                    sendMessage();
//                }
//            });
//        }
//    }

//    // Initialize when DOM is ready
//    document.addEventListener('DOMContentLoaded', () => {
//        console.log("Chat widget initializing...");

//        // Get saved preferences
//        currentLanguage = localStorage.getItem('chatLanguage') || 'vi';
//        isVoiceEnabled = localStorage.getItem('voiceEnabled') !== 'false';

//        // Initialize voice recognition
//        initializeVoiceRecognition();

//        // Setup UI
//        updateUILanguage();
//        updateVoiceOutputButton();
//        setupUIEventListeners();

//        // Start SignalR connection
//        start();
//    });

//    // Make functions available globally
//    window.toggleChat = toggleChat;
//    window.sendMessage = sendMessage;
//    window.sendSuggestedMessage = sendSuggestedMessage;

//})();