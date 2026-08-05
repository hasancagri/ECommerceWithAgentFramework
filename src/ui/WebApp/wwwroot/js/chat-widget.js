(function () {
    const root = document.getElementById('chat-widget');
    if (!root) return;

    const toggle = document.getElementById('chat-toggle');
    const panel = document.getElementById('chat-panel');
    const closeBtn = document.getElementById('chat-close');
    const form = document.getElementById('chat-form');
    const input = document.getElementById('chat-input');
    const messages = document.getElementById('chat-messages');

    const PREV_KEY = 'chat.previousResponseId';
    const AUTH_KEY = 'chat.authState';

    // Oturum durumu degistiyse (login/logout) gecmisi resetle.
    const authState = root.getAttribute('data-authenticated');
    if (sessionStorage.getItem(AUTH_KEY) !== authState) {
        sessionStorage.removeItem(PREV_KEY);
        sessionStorage.setItem(AUTH_KEY, authState);
    }

    toggle.addEventListener('click', () => panel.classList.toggle('chat-hidden'));
    closeBtn.addEventListener('click', () => panel.classList.add('chat-hidden'));

    function addBubble(text, who) {
        const el = document.createElement('div');
        el.className = 'chat-msg ' + who;
        el.textContent = text;
        messages.appendChild(el);
        messages.scrollTop = messages.scrollHeight;
        return el;
    }

    function handleData(jsonText, botEl) {
        if (jsonText === '[DONE]') return;
        let obj;
        try { obj = JSON.parse(jsonText); } catch { return; }
        // Sadece metin ciktisini bastir. Responses API'de tool-call argumanlari da 'delta' ile
        // gelir ( or. function_call_arguments.delta) -> bunlari ATLA, yoksa {"name":...} gibi ic
        // akis ekrana sizar.
        const type = typeof obj.type === 'string' ? obj.type : '';
        const isToolOrMeta = type.indexOf('function_call') !== -1 || type.indexOf('reasoning') !== -1;
        if (!isToolOrMeta) {
            if (typeof obj.delta === 'string') { botEl.textContent += obj.delta; }
            else if (obj.output_text && typeof obj.output_text === 'string') { botEl.textContent += obj.output_text; }
        }
        const id = (obj.response && obj.response.id) || obj.id;
        if (id && typeof id === 'string' && id.indexOf('resp') === 0) {
            sessionStorage.setItem(PREV_KEY, id);
        }
        messages.scrollTop = messages.scrollHeight;
    }

    async function send(message) {
        addBubble(message, 'user');
        const botEl = addBubble('', 'bot');
        const previousResponseId = sessionStorage.getItem(PREV_KEY);

        let resp;
        try {
            resp = await fetch('/chat/stream', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ message: message, previousResponseId: previousResponseId })
            });
        } catch {
            botEl.textContent = 'Üzgünüm, şu an yardımcı olamıyorum.';
            return;
        }
        if (!resp.ok || !resp.body) { botEl.textContent = 'Üzgünüm, şu an yardımcı olamıyorum.'; return; }

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
        if (botEl.textContent === '') botEl.textContent = '(boş yanıt)';
    }

    form.addEventListener('submit', function (e) {
        e.preventDefault();
        const message = input.value.trim();
        if (!message) return;
        input.value = '';
        send(message);
    });
})();