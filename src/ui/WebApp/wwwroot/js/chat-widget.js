(function () {
    const root = document.getElementById('chat-widget');
    if (!root) return;

    const toggle = document.getElementById('chat-toggle');
    const panel = document.getElementById('chat-panel');
    const closeBtn = document.getElementById('chat-close');
    const newBtn = document.getElementById('chat-new');
    const historyToggle = document.getElementById('chat-history-toggle'); // yalniz login'de var
    const historyPanel = document.getElementById('chat-history');
    const form = document.getElementById('chat-form');
    const input = document.getElementById('chat-input');
    const messages = document.getElementById('chat-messages');

    // 009: gecmis sunucuda yasar; tarayici yalnizca conversation id tasir (sessionStorage —
    // anonim "ayni oturumda sureklilik" semantigi de bununla birebir).
    const CONV_KEY = 'chat.conversationId';
    const AUTH_KEY = 'chat.authState';

    // Oturum durumu degistiyse (login/logout) aktif konusmayi birak — agent'i da degisir.
    const authState = root.getAttribute('data-authenticated');
    if (sessionStorage.getItem(AUTH_KEY) !== authState) {
        sessionStorage.removeItem(CONV_KEY);
        sessionStorage.setItem(AUTH_KEY, authState);
    }

    toggle.addEventListener('click', () => panel.classList.toggle('chat-hidden'));
    closeBtn.addEventListener('click', () => panel.classList.add('chat-hidden'));

    newBtn.addEventListener('click', function () {
        sessionStorage.removeItem(CONV_KEY);
        messages.innerHTML = '';
        if (historyPanel) historyPanel.classList.add('chat-hidden');
        input.focus();
    });

    if (historyToggle) {
        historyToggle.addEventListener('click', async function () {
            if (!historyPanel.classList.contains('chat-hidden')) {
                historyPanel.classList.add('chat-hidden');
                return;
            }
            await loadHistoryList();
            historyPanel.classList.remove('chat-hidden');
        });
    }

    async function loadHistoryList() {
        historyPanel.innerHTML = '<div class="chat-history-empty">Yükleniyor…</div>';
        let data;
        try {
            const resp = await fetch('/chat/conversations');
            if (!resp.ok) throw new Error();
            data = await resp.json();
        } catch {
            historyPanel.innerHTML = '<div class="chat-history-empty">Geçmiş yüklenemedi.</div>';
            return;
        }
        if (!data.items || data.items.length === 0) {
            historyPanel.innerHTML = '<div class="chat-history-empty">Henüz konuşma yok.</div>';
            return;
        }
        historyPanel.innerHTML = '';
        for (const item of data.items) {
            const el = document.createElement('button');
            el.type = 'button';
            el.className = 'chat-history-item';
            el.textContent = item.title;
            el.title = new Date(item.lastActivityTime).toLocaleString();
            el.addEventListener('click', function () { openConversation(item.conversationId); });
            historyPanel.appendChild(el);
        }
    }

    // Gecmis konusma acilir: TUM mesajlar gelir (goruntulemede kirpma yok); devam edilebilir.
    async function openConversation(conversationId) {
        let data;
        try {
            const resp = await fetch('/chat/conversations/' + encodeURIComponent(conversationId));
            if (!resp.ok) throw new Error();
            data = await resp.json();
        } catch {
            return;
        }
        sessionStorage.setItem(CONV_KEY, conversationId);
        messages.innerHTML = '';
        for (const item of data.items) {
            if (item.kind !== 'message' || !item.text) continue; // tool adimlari baloncuk olmaz
            addBubble(item.text, item.role === 'user' ? 'user' : 'bot');
        }
        historyPanel.classList.add('chat-hidden');
        input.focus();
    }

    function addBubble(text, who) {
        const el = document.createElement('div');
        el.className = 'chat-msg ' + who;
        el.textContent = text;
        messages.appendChild(el);
        messages.scrollTop = messages.scrollHeight;
        return el;
    }

    // 009 SSE sozlesmesi: {delta} metin parcasi, {done, conversationId} kapanis.
    function handleData(jsonText, botEl) {
        let obj;
        try { obj = JSON.parse(jsonText); } catch { return; }
        if (typeof obj.delta === 'string') botEl.textContent += obj.delta;
        if (obj.done && typeof obj.conversationId === 'string') {
            sessionStorage.setItem(CONV_KEY, obj.conversationId);
        }
        messages.scrollTop = messages.scrollHeight;
    }

    async function send(message) {
        addBubble(message, 'user');
        const botEl = addBubble('', 'bot');
        const conversationId = sessionStorage.getItem(CONV_KEY);

        let resp;
        try {
            resp = await fetch('/chat/stream', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ message: message, conversationId: conversationId })
            });
        } catch {
            botEl.textContent = 'Sorry, I cannot help right now.';
            return;
        }
        if (!resp.ok || !resp.body) { botEl.textContent = 'Sorry, I cannot help right now.'; return; }

        // Bayat id'de BFF yeni konusma acar ve id'yi header'la bildirir (FR-010).
        const headerConvId = resp.headers.get('X-Conversation-Id');
        if (headerConvId) sessionStorage.setItem(CONV_KEY, headerConvId);

        const reader = resp.body.getReader();
        const decoder = new TextDecoder();
        let buffer = '';
        while (true) {
            const { value, done } = await reader.read();
            if (done) break;
            buffer += decoder.decode(value, { stream: true });
            const parts = buffer.split('\n\n');      // SSE event ayraci
            buffer = parts.pop();                     // tamamlanmamis son parca
            for (const part of parts) {
                for (const line of part.split('\n')) {
                    const trimmed = line.trim();
                    if (trimmed.indexOf('data:') === 0) {
                        handleData(trimmed.slice(5).trim(), botEl);
                    }
                }
            }
        }
        // Ajan uygulama linklerini GORELI verir (urun: /Products/Detail/{guid}, sepet: /Basket);
        // host'u istemci origin'inden ekle -> config yok, hangi ortamdaysak dogru host cikar.
        botEl.textContent = botEl.textContent.replace(
            /\/(?:Products\/Detail\/[0-9a-fA-F-]{36}|Basket)\b/g,
            function (m) { return window.location.origin + m; }
        );
        if (botEl.textContent === '') botEl.textContent = '(empty response)';
    }

    form.addEventListener('submit', function (e) {
        e.preventDefault();
        const message = input.value.trim();
        if (!message) return;
        input.value = '';
        send(message);
    });
})();