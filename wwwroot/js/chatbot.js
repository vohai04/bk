/** Persistent conversation UI; message contents are loaded only from the current server identity. */
'use strict';
let chatPending = false;
let chatPosting = false;
let chatLoading = false;
let chatPollTimer = null;

function rememberChatOpen(open) {
    try { sessionStorage.setItem('bookinfo.chat.open', open ? '1' : '0'); } catch (_) { /* Storage may be disabled. */ }
}

function updateChatControls() {
    $('#sendChat').prop('disabled', chatPending || chatLoading).text(chatLoading ? 'Đang tải…' : chatPending ? 'Đang trả lời…' : 'Gửi');
    document.getElementById('chatMessages')?.setAttribute('aria-busy', String(chatPending || chatLoading));
}

function toggleChatbotModal() {
    const windowEl = document.getElementById('chatbotWindow');
    if (!windowEl) return;
    const open = windowEl.style.display !== 'flex';
    windowEl.style.display = open ? 'flex' : 'none';
    $('.floating-chatbot').toggle(!open);
    rememberChatOpen(open);
    clearTimeout(chatPollTimer);
    if (open) {
        $('#chatInput').trigger('focus');
        if (!chatPosting) loadChatHistory();
    }
}

function addMessage(sender, text, createdAt) {
    const messages = document.getElementById('chatMessages');
    if (!messages) return;
    const isUser = sender === 'user';
    const parsed = createdAt ? new Date(createdAt) : new Date();
    const sentAt = Number.isNaN(parsed.getTime()) ? new Date() : parsed;
    const msgDiv = document.createElement('div');
    msgDiv.className = `chat-message chat-message--${isUser ? 'user' : 'bot'}`;
    const meta = document.createElement('div');
    meta.className = 'chat-message-meta';
    const author = document.createElement('span');
    author.className = 'chat-message-author';
    author.textContent = isUser ? 'Bạn' : 'Trợ lý sách';
    const time = document.createElement('time');
    time.dateTime = sentAt.toISOString();
    time.textContent = new Intl.DateTimeFormat('vi-VN', {
        hour: '2-digit', minute: '2-digit', hourCycle: 'h23', timeZone: 'Asia/Ho_Chi_Minh'
    }).format(sentAt);
    time.title = new Intl.DateTimeFormat('vi-VN', {
        dateStyle: 'medium', timeStyle: 'short', timeZone: 'Asia/Ho_Chi_Minh'
    }).format(sentAt);
    meta.append(author, time);
    const bubble = document.createElement('div');
    bubble.className = 'chat-bubble';
    // Only relative book-detail URLs are linked; all other text remains escaped.
    for (const part of String(text).split(/(\/BookDetail\/\d+)/g)) {
        if (/^\/BookDetail\/\d+$/.test(part)) {
            const link = document.createElement('a');
            link.href = part;
            link.textContent = 'Xem sách';
            bubble.appendChild(link);
        } else bubble.appendChild(document.createTextNode(part));
    }
    msgDiv.append(meta, bubble);
    messages.appendChild(msgDiv);
    messages.scrollTop = messages.scrollHeight;
    return msgDiv;
}

async function loadChatHistory() {
    if (chatLoading || chatPosting) return;
    chatLoading = true;
    updateChatControls();
    clearTimeout(chatPollTimer);
    try {
        const response = await fetch('/Index?handler=ChatHistory', { cache: 'no-store' });
        if (!response.ok) throw new Error('History request failed');
        const data = await response.json();
        if (!Array.isArray(data.messages)) throw new Error('Invalid history');
        const messages = document.getElementById('chatMessages');
        messages.replaceChildren();
        data.messages.forEach(message => addMessage(message.role === 'user' ? 'user' : 'bot', message.text, message.createdAt));
        if (!data.messages.length) addMessage('bot', 'Xin chào! Tôi có thể giúp bạn tìm sách, xem đánh giá và gợi ý sách được yêu thích trong thư viện.');
        const last = data.messages[data.messages.length - 1];
        chatPending = Boolean(last && last.role === 'user' && Date.now() - Date.parse(last.createdAt) < 60000);
        if (chatPending) {
            addMessage('bot', 'Đang tìm câu trả lời cho bạn…').classList.add('chat-message--pending');
            chatPollTimer = setTimeout(loadChatHistory, 2000);
        } else if (last && last.role === 'user') {
            addMessage('bot', 'Câu hỏi trước chưa có phản hồi. Bạn có thể gửi lại yêu cầu nhé.');
        }
    } catch (_) {
        chatPending = false;
        if (!document.getElementById('chatMessages').children.length)
            addMessage('bot', 'Chưa tải được hội thoại. Bạn đóng rồi mở lại trợ lý để thử lại nhé.');
    } finally {
        chatLoading = false;
        updateChatControls();
    }
}

async function sendChatMessage() {
    const $input = $('#chatInput');
    const message = $input.val().trim();
    if (!message || chatPending || chatLoading) return;
    if (message.length > 2000) { addMessage('bot', 'Bạn nhập câu hỏi tối đa 2000 ký tự nhé.'); return; }
    chatPosting = true;
    chatPending = true;
    updateChatControls();
    addMessage('user', message);
    $input.val('');
    const pending = addMessage('bot', 'Đang tìm câu trả lời cho bạn…');
    pending.classList.add('chat-message--pending');
    let completed = false;
    try {
        const response = await fetch('/Index?handler=Chat', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || '' },
            body: new URLSearchParams({ message })
        });
        if (!response.ok) throw new Error('Chat request failed');
        const data = await response.json();
        if (typeof data.reply !== 'string') throw new Error('Invalid reply');
        addMessage('bot', data.reply);
        completed = true;
    } catch (_) {
        addMessage('bot', 'Chưa nhận được phản hồi. Bạn mở lại trợ lý để kiểm tra câu hỏi đã được xử lý chưa nhé.');
    } finally {
        pending.remove();
        chatPosting = false;
        chatPending = false;
        updateChatControls();
    }
    if (completed) await loadChatHistory();
}

$(document).on('click', '#sendChat', sendChatMessage);
$(document).on('keydown', '#chatInput', function (event) {
    if (event.key === 'Enter' && !event.isComposing) { event.preventDefault(); sendChatMessage(); }
});
$(function () {
    try {
        if (sessionStorage.getItem('bookinfo.chat.open') === '1') toggleChatbotModal();
    } catch (_) { /* The widget works even when sessionStorage is unavailable. */ }
});
