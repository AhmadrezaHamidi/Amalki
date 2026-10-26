// Progressive enhancement only: every page works without this file.
(() => {
  const $ = (s, r = document) => r.querySelector(s);
  const $$ = (s, r = document) => [...r.querySelectorAll(s)];
  const fa = (s) => String(s).replace(/\d/g, (d) => '۰۱۲۳۴۵۶۷۸۹'[d]);
  const latin = (s) => String(s).replace(/[۰-۹]/g, (d) => '۰۱۲۳۴۵۶۷۸۹'.indexOf(d)).replace(/[٠-٩]/g, (d) => '٠١٢٣٤٥٦٧٨٩'.indexOf(d));

  // ---- toggles: mobile nav, mobile filters ----
  const toggle = (btn, target, cls, host = target) => {
    if (!btn || !target) return;
    btn.addEventListener('click', () => {
      const open = host.classList.toggle(cls);
      btn.setAttribute('aria-expanded', String(open));
    });
  };
  toggle($('[data-nav-toggle]'), $('#main-nav'), 'open', $('[data-head]'));
  toggle($('[data-filter-toggle]'), $('#filters'), 'open');
  toggle($('[data-admin-toggle]'), $('#admin-nav'), 'open', $('.admin-side'));

  // ---- sort select submits itself ----
  $$('form[data-autosubmit] select').forEach((s) => s.addEventListener('change', () => s.form.submit()));

  // ---- home search: budget options follow the deal tab ----
  const budget = $('[data-budget]');
  if (budget) {
    const sync = () => {
      const rent = $('[data-deal]:checked')?.value === 'Rent';
      $$('option[data-sale]', budget).forEach((o) => { o.hidden = rent; o.disabled = rent; });
      $$('option[data-rent]', budget).forEach((o) => { o.hidden = !rent; o.disabled = !rent; });
      if (budget.selectedOptions[0]?.disabled) budget.value = '';
      $('[data-budget-label]').textContent = rent ? 'سقف رهن' : 'سقف قیمت';
    };
    $$('[data-deal]').forEach((r) => r.addEventListener('change', sync));
    sync();
  }

  // ---- money inputs: show "۱۲٫۵ میلیارد تومان" under the raw number ----
  const money = (v) => {
    if (!v) return '';
    if (v >= 1e9) return fa(+(v / 1e9).toFixed(2)).replace('.', '٫') + ' میلیارد تومان';
    if (v >= 1e6) return fa(+(v / 1e6).toFixed(2)).replace('.', '٫') + ' میلیون تومان';
    return fa(v.toLocaleString('en-US')) + ' تومان';
  };
  $$('input[data-money]').forEach((input) => {
    const hint = input.parentElement.querySelector('[data-money-hint]') || input.closest('form')?.querySelector('[data-money-hint]');
    const show = () => { if (hint) hint.textContent = money(+latin(input.value).replace(/\D/g, '')); };
    input.addEventListener('input', show);
    show();
  });
  // Persian digits typed into numeric admin fields would fail model binding.
  $$('form').forEach((f) => f.addEventListener('submit', () => {
    $$('input[inputmode=numeric]', f).forEach((i) => { i.value = latin(i.value).replace(/[,\s٬]/g, ''); });
  }));

  // ---- admin edit: show only the price fields for the chosen deal ----
  const dealSelect = $('[data-deal-select]');
  if (dealSelect) {
    const sync = () => {
      const rent = dealSelect.value === 'Rent';
      $$('[data-sale-only]').forEach((e) => { e.hidden = rent; });
      $$('[data-rent-only]').forEach((e) => { e.hidden = !rent; });
    };
    dealSelect.addEventListener('change', sync);
    sync();
  }

  // ---- favorites (PUB-08): ids kept in this browser ----
  const KEY = 'amlak:favs';
  const read = () => { try { return JSON.parse(localStorage.getItem(KEY) || '[]').filter(Number.isInteger); } catch { return []; } };
  const write = (ids) => { try { localStorage.setItem(KEY, JSON.stringify(ids)); } catch { /* private mode */ } };
  const paint = () => {
    const ids = read();
    $$('[data-fav]').forEach((b) => {
      const on = ids.includes(+b.dataset.fav);
      b.setAttribute('aria-pressed', String(on));
      const label = b.hasAttribute('data-fav-label') ? $('span', b) : null;
      if (label) label.textContent = on ? 'نشان شد' : 'نشان کردن';
    });
    const count = $('[data-fav-count]');
    if (count) { count.hidden = ids.length === 0; count.textContent = fa(ids.length); }
  };
  document.addEventListener('click', (e) => {
    const b = e.target.closest('[data-fav]');
    if (!b) return;
    e.preventDefault();
    const id = +b.dataset.fav;
    const ids = read();
    write(ids.includes(id) ? ids.filter((x) => x !== id) : [id, ...ids]);
    paint();
    const saved = $('[data-saved-page]');
    if (saved && !read().includes(id)) b.closest('.card')?.remove();
  });
  paint();
  const savedPage = $('[data-saved-page]');
  if (savedPage && savedPage.dataset.hasIds === '0' && read().length) {
    location.replace(location.pathname + '?ids=' + read().join(','));
  }

  // ---- share ----
  $$('[data-share]').forEach((b) => b.addEventListener('click', async () => {
    const data = { title: b.dataset.title, url: b.dataset.url };
    try {
      if (navigator.share) await navigator.share(data);
      else { await navigator.clipboard.writeText(data.url); $('span', b).textContent = 'لینک کپی شد'; }
    } catch { /* cancelled */ }
  }));

  // ---- gallery lightbox ----
  const box = $('[data-lightbox]');
  if (box && box.showModal) {
    const photos = JSON.parse($('[data-lb-photos]', box).textContent);
    const img = $('[data-lb-img]', box);
    let i = 0;
    const show = (n) => {
      i = (n + photos.length) % photos.length;
      img.src = photos[i].src; img.alt = photos[i].alt;
      $('[data-lb-count]', box).textContent = fa(i + 1) + ' از ' + fa(photos.length);
    };
    $$('[data-gallery] button').forEach((b) => b.addEventListener('click', () => { show(+b.dataset.index); box.showModal(); }));
    $('[data-lb-close]', box).addEventListener('click', () => box.close());
    // RTL: "previous" sits on the right.
    $('[data-lb-prev]', box).addEventListener('click', () => show(i - 1));
    $('[data-lb-next]', box).addEventListener('click', () => show(i + 1));
    box.addEventListener('keydown', (e) => {
      if (e.key === 'ArrowRight') show(i - 1);
      if (e.key === 'ArrowLeft') show(i + 1);
    });
    box.addEventListener('click', (e) => { if (e.target === box || e.target.classList.contains('lb-stage')) box.close(); });
    let x0 = null;
    box.addEventListener('touchstart', (e) => { x0 = e.touches[0].clientX; }, { passive: true });
    box.addEventListener('touchend', (e) => {
      if (x0 === null) return;
      const dx = e.changedTouches[0].clientX - x0;
      if (Math.abs(dx) > 40) show(dx > 0 ? i + 1 : i - 1);
      x0 = null;
    });
    if (photos.length < 2) $$('.lb-stage .lb-btn', box).forEach((b) => { b.hidden = true; });
  }
})();
