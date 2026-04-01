/**
 * signalr-hub.js - SignalR Notification Hub Connection
 * BookInfoFinder
 *
 * Reads window.currentUserId (emitted by _Layout.cshtml)
 * and connects to the notification hub.
 */

'use strict';

(function () {
    var currentUserId = (window.currentUserId || '').toString().trim();

    function initializeWithUser(userId, unreadCount) {
        if (!userId) return;

        // Update badge count
        var countEl = document.getElementById('notificationCount');
        if (countEl && typeof unreadCount === 'number') {
            if (unreadCount > 0) {
                countEl.innerText = unreadCount;
                countEl.style.display = '';
            } else {
                countEl.innerText = '';
                countEl.style.display = 'none';
            }
        }

        // Connect to SignalR hub
        try {
            var connection = new signalR.HubConnectionBuilder()
                .withUrl('/notificationHub')
                .withAutomaticReconnect()
                .build();

            connection.on('ReceiveNotification', function (payload) {
                try {
                    var message = (payload && (payload.message || payload.Message || payload.title || payload.Title)) || 'Bạn có thông báo mới';
                    if (typeof showToast === 'function') showToast(message, 'info');

                    var el = document.getElementById('notificationCount');
                    if (el) {
                        var n = (parseInt(el.innerText) || 0) + 1;
                        el.innerText = n;
                        el.style.display = '';
                    }
                } catch (e) { console.error(e); }
            });

            connection.start()
                .then(function () {
                    console.log('SignalR connected');
                    connection.invoke('Register', parseInt(userId))
                              .catch(function (err) { console.error(err.toString()); });
                })
                .catch(function (err) { console.error(err.toString()); });

        } catch (ex) {
            console.error('SignalR init error', ex);
        }
    }

    // If userId was server-rendered into the page, use it directly
    if (currentUserId.length > 0) {
        initializeWithUser(currentUserId, null);
    } else {
        // Fallback: fetch via AJAX (sends session cookies)
        $.getJSON('/Notifications?handler=UnreadCount')
            .done(function (data) {
                if (data && data.userId) {
                    initializeWithUser(data.userId, data.unread || 0);
                }
            })
            .fail(function (jqxhr, status, err) {
                console.warn('Could not fetch unread count/user id', status, err);
            });
    }
})();
