// 025: layout seviyesi sepet geri sayimi. Header'daki #basket-countdown[data-expires]'i
// sunucu mutlak bitis anina gore MM:SS olarak isler. Ogenin olmadigi sayfada no-op.
(function () {
    const el = document.getElementById('basket-countdown');
    if (!el) return;

    const expires = Date.parse(el.dataset.expires);
    if (isNaN(expires)) return;

    const wrapper = document.getElementById('basket-countdown-wrapper');
    let timer = null;
    let purged = false;

    function pad(n) { return n < 10 ? '0' + n : '' + n; }

    // Sifir/gecmis: sepeti sunucuda bosalt + sayaci gizle. Sepet/checkout sayfasindaysa
    // bos sepeti yansitmak icin tazele; degilse kullaniciyi bulundugu sayfadan atma (FR-005).
    async function purgeAndHide() {
        if (purged) return;
        purged = true;
        if (timer) clearInterval(timer);
        try { await fetch('/basket/purge-expired', { method: 'POST' }); } catch (e) { /* sonraki yukleme yeniden dener */ }
        if (wrapper) wrapper.style.display = 'none';

        const path = window.location.pathname.toLowerCase();
        if (path.startsWith('/basket') || path.startsWith('/order/create')) {
            window.location.reload();
        }
    }

    function tick() {
        const remaining = Math.floor((expires - Date.now()) / 1000);
        if (remaining <= 0) { purgeAndHide(); return; }
        const m = Math.floor(remaining / 60);
        const s = remaining % 60;
        el.textContent = pad(m) + ':' + pad(s);
    }

    tick();
    timer = setInterval(tick, 1000);
})();