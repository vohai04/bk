/**
 * chatbot.js - Chatbot Widget
 * BookInfoFinder
 */

'use strict';

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
    const msgDiv = document.createElement('div');
    msgDiv.className = `chat-message ${sender}`;
    msgDiv.innerHTML = `<div class="chat-bubble">${text}</div>`;
    messages.appendChild(msgDiv);
    messages.scrollTop = messages.scrollHeight;
}

function sendChatMessage() {
    const $input = $('#chatInput');
    const message = $input.val().trim();
    if (!message) return;

    addMessage('user', message);
    $input.val('');

    fetch('/Index?handler=Chat', {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: new URLSearchParams({ message })
    })
    .then(function (res) { return res.json(); })
    .then(function (data) { addMessage('bot', data.reply); })
    .catch(function () { addMessage('bot', 'Xin lỗi, có lỗi xảy ra. Vui lòng thử lại.'); });
}

// Use delegated handlers so they work after any dynamic render
$(document).on('click', '#sendChat', sendChatMessage);

$(document).on('keypress', '#chatInput', function (e) {
    if (e.which === 13) sendChatMessage();
});
