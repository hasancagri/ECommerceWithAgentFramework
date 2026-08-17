// 034: müşteri hizmetleri tam-sayfa chat. SSE akış/parse mantığı eski chat-widget.js'ten
// taşındı (widget paneli kaldırıldı, tek tüketici bu sayfa).
(function () {
    const root = document.getElementById('chat-page');
    if (!root) return;

    const form = document.getElementById('chat-page-form');
    const input = document.getElementById('chat-page-input');
    const sendBtn = document.getElementById('chat-page-send');
    const messages = document.getElementById('chat-page-messages');

    // Cok turlu gecmis STATELESS: transkript sayfa-ici degiskende tutulur, her istekle gider.
    // (Sunucu previous_response_id/conversation cozmuyor — MAF hosting sinirlamasi.)
    // Sayfa yuklemesi = temiz oturum; gorunen transkript ile tasinan gecmis hep ayni.
    const HIST_MAX = 40; // son N mesaj (token siniri)
    let chatHistory = [];

    function setBusy(busy) {
        input.disabled = busy;
        sendBtn.disabled = busy;
        if (!busy) input.focus();
    }

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
        messages.scrollTop = messages.scrollHeight;
    }

    async function send(message) {
        addBubble(message, 'user');
        const botEl = addBubble('', 'bot');
        setBusy(true);

        let resp;
        try {
            resp = await fetch('/chat/stream', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ message: message, history: chatHistory })
            });
        } catch {
            botEl.textContent = 'Üzgünüm, şu an yardımcı olamıyorum.';
            setBusy(false);
            return;
        }
        if (!resp.ok || !resp.body) {
            botEl.textContent = 'Üzgünüm, şu an yardımcı olamıyorum.';
            setBusy(false);
            return;
        }

        try {
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
        } catch {
            // Akis ortada koptu (ag hatasi): o ana kadar geleni birak, kullaniciyi bilgilendir.
            if (botEl.textContent === '') botEl.textContent = 'Üzgünüm, şu an yardımcı olamıyorum.';
            else botEl.textContent += '\n(bağlantı kesildi)';
            setBusy(false);
            return;
        }

        // Ajan uygulama linklerini GORELI verir (urun: /Products/Detail/{guid}, sepet: /Basket);
        // host'u istemci origin'inden ekle -> config yok, hangi ortamdaysak dogru host cikar.
        botEl.textContent = botEl.textContent.replace(
            /\/(?:Products\/Detail\/[0-9a-fA-F-]{36}|Basket)\b/g,
            function (m) { return window.location.origin + m; }
        );
        if (botEl.textContent === '') botEl.textContent = '(boş yanıt)';

        // Turu transkripte isle — sonraki istek gecmisi tasir.
        chatHistory.push({ role: 'user', content: message });
        chatHistory.push({ role: 'assistant', content: botEl.textContent });
        chatHistory = chatHistory.slice(-HIST_MAX);
        setBusy(false);
    }

    form.addEventListener('submit', function (e) {
        e.preventDefault();
        const message = input.value.trim();
        if (!message) return;
        input.value = '';
        send(message);
    });

    input.focus();
})();