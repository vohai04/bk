/**
 * chatbot.js - Chatbot Widget
 * BookInfoFinder
 */

'use strict';
let chatPending = false;

function toggleChatbotModal() {
    const windowEl = document.getElementById('chatbotWindow');
    const isHidden = windowEl.style.display === 'none' || windowEl.style.display === '';

    if (isHidden) {
        windowEl.style.display = 'flex';
        $('.floating-chatbot').hide();
        setTimeout(function () {
            $('#chatInput').focus();
            if ($('#chatMessages').children().length === 0) {
                addMessage('bot', 'Xin chào! Tôi có thể giúp bạn tìm sách. Hãy nhập từ khóa như tên sách, tác giả, hoặc thể loại.');
            }
        }, 100);
    } else {
        windowEl.style.display = 'none';
        $('.floating-chatbot').show();
    }
}

function addMessage(sender, text) {
    const messages = document.getElementById('chatMessages');
    if (!messages) return;
    const isUser = sender === 'user';
    const sentAt = new Date();
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
    bubble.textContent = text;
    msgDiv.append(meta, bubble);
    messages.appendChild(msgDiv);
    messages.scrollTop = messages.scrollHeight;
    return msgDiv;
}

function sendChatMessage() {
    const $input = $('#chatInput');
    const message = $input.val().trim();
    if (!message || chatPending) return;

    chatPending = true;
    $('#sendChat').prop('disabled', true).text('Đang trả lời…');

    addMessage('user', message);
    $input.val('');
    const pending = addMessage('bot', 'Đang tìm câu trả lời cho bạn…');
    pending.classList.add('chat-message--pending');
    document.getElementById('chatMessages').setAttribute('aria-busy', 'true');

    fetch('/Index?handler=Chat', {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || '' },
        body: new URLSearchParams({ message })
    })
    .then(function (res) {
        if (!res.ok) throw new Error('Chat request failed');
        return res.json();
    })
    .then(function (data) { addMessage('bot', data.reply); })
    .catch(function () { addMessage('bot', 'Chưa gửi được tin nhắn. Bạn tải lại trang rồi thử lại nhé.'); })
    .finally(function () {
        pending.remove();
        document.getElementById('chatMessages').setAttribute('aria-busy', 'false');
        chatPending = false;
        $('#sendChat').prop('disabled', false).text('Gửi');
    });
}

// Use delegated handlers so they work after any dynamic render
$(document).on('click', '#sendChat', sendChatMessage);

$(document).on('keypress', '#chatInput', function (e) {
    if (e.which === 13) sendChatMessage();
});
