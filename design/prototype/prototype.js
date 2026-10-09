/* open-ClaudeOS prototype. Scenes are scripted, but everything they show comes from window.COS_DATA,
   which `claudeos demo-data` generates by running the real C# core. */
(() => {
  const D = window.COS_DATA;
  const $ = (s, r = document) => r.querySelector(s);
  const $$ = (s, r = document) => [...r.querySelectorAll(s)];
  const el = (tag, cls, html) => { const e = document.createElement(tag); if (cls) e.className = cls; if (html != null) e.innerHTML = html; return e; };
  const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
  const U = 0.5; // physical pixels -> device-independent pixels (the Surface at 200%)

  const stage = $('#stage'), wrap = $('#wrap'), work = $('#work'), bar = $('#bar'), input = $('#input'), scrim = $('#scrim');
  const statusEl = $('#status'), statusLabel = $('#statusLabel'), statusSub = $('#statusSub'), results = $('#results'), card = $('#card'), toast = $('#toast');
  const root = document.documentElement;

  const S = { scenario: 'one-app', appearance: 'light', accent: '#EB6834', wins: {}, content: {}, busy: false, sel: 0, shown: [], lastUndo: null, held: null };

  /* ---------------------------------------------------------------- icons */
  const ICON = {
    file: '<svg viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.3" stroke-linejoin="round"><path d="M3.5 1.8h5.2l3.8 3.8v8.6h-9z"/><path d="M8.5 1.8v4h4"/></svg>',
    sheet: '<svg viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.3"><rect x="2.2" y="2.2" width="11.6" height="11.6" rx="2"/><path d="M2.2 6.3h11.6M6.3 6.3v7.5"/></svg>',
    window: '<svg viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.3"><rect x="1.8" y="2.8" width="12.4" height="10.4" rx="2"/><path d="M1.8 6h12.4"/></svg>',
    snap: '<svg viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.3"><rect x="1.8" y="2.8" width="12.4" height="10.4" rx="2"/><path d="M8 2.8v10.4"/></svg>',
    spark: '<svg viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.3" stroke-linecap="round"><circle cx="8" cy="8" r="3.2"/><path d="M8 1.6v1.4M8 13v1.4M1.6 8h1.4M13 8h1.4M3.5 3.5l1 1M11.5 11.5l1 1M12.5 3.5l-1 1M4.5 11.5l-1 1"/></svg>',
    chart: '<svg viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.3" stroke-linecap="round"><path d="M2.5 13.5h11M4.5 13V8.5M8 13V4.5M11.5 13V7"/></svg>',
    folder: '<svg viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.3" stroke-linejoin="round"><path d="M1.8 4.2h4.3l1.3 1.6h6.8v7H1.8z"/></svg>',
    out: '<svg viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.4" stroke-linecap="round" stroke-linejoin="round"><path d="M3 8h9M8.5 4.2 12.3 8l-3.8 3.8"/><path d="M13.6 2.6v10.8" opacity=".35"/></svg>',
    mail: '<svg viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.3" stroke-linejoin="round"><rect x="1.8" y="3.2" width="12.4" height="9.6" rx="2"/><path d="m2.4 4.4 5.6 4.4 5.6-4.4"/></svg>',
    move: '<svg viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.4" stroke-linecap="round" stroke-linejoin="round"><path d="M2.5 8h10M9 4.5 12.5 8 9 11.5"/></svg>',
    plus: '<svg viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.4" stroke-linecap="round"><path d="M8 3v10M3 8h10"/></svg>',
  };

  /* ---------------------------------------------------------------- the Spark */
  let orbId = 0;
  function orbMarkup() {
    const n = ++orbId;
    return `<svg viewBox="0 0 36 36" aria-hidden="true"><defs>
      <radialGradient id="oc${n}" cx=".38" cy=".32" r=".78"><stop class="g1" offset="0"/><stop class="g2" offset=".4"/><stop class="g3" offset=".72"/><stop class="g4" offset="1"/></radialGradient>
      <radialGradient id="oh${n}"><stop class="h1" offset=".55"/><stop class="h2" offset="1"/></radialGradient></defs>
      <circle class="halo" cx="18" cy="18" r="18" fill="url(#oh${n})"/>
      <circle class="core" cx="18" cy="18" r="10" fill="url(#oc${n})"/>
      <circle class="ring" cx="18" cy="18" r="14.4" fill="none" stroke-width="1.6" stroke-linecap="round" stroke-dasharray="70.6 19.9"/></svg>`;
  }
  const hydrateOrbs = (rootEl = document) => $$('[data-orb]', rootEl).forEach((o) => { if (!o.firstElementChild) o.innerHTML = orbMarkup(); });
  const style = document.createElement('style');
  style.textContent = '.orb svg *{transform-box:fill-box}';
  document.head.append(style);
  const barOrb = $('#barOrb'), trayOrb = $('#trayBtn .orb');
  const setState = (s) => { barOrb.dataset.state = s; trayOrb.dataset.state = s === 'idle' ? 'dormant' : s; };

  /* ---------------------------------------------------------------- layout */
  const sc = () => D.layouts.scenarios.find((s) => s.name === S.scenario);
  const setRect = (e, r) => { e.style.left = r.x * U + 'px'; e.style.top = r.y * U + 'px'; e.style.width = r.w * U + 'px'; e.style.height = r.h * U + 'px'; };

  function appBody(title) {
    if (title === 'Browser') return '<div class="sk pill"></div><div class="sk block"></div><div class="sk h"></div><div class="sk l"></div><div class="sk m"></div><div class="sk block"></div><div class="sk s"></div>';
    return '<div class="sk h"></div><div class="sk l"></div><div class="sk m"></div><div class="sk l"></div><div class="sk s"></div><div class="sk m"></div><div class="sk l"></div><div class="sk"></div><div class="sk s"></div><div class="sk l"></div><div class="sk m"></div><div class="sk l"></div>';
  }

  function buildDesktop() {
    $$('.win', work).forEach((w) => w.remove());
    S.wins = {}; S.content = {};
    for (const w of sc().windows) {
      const e = el('div', 'win spring' + (w.active ? ' active' : ''));
      e.innerHTML = `<div class="titlebar"><span>${w.title}</span><span class="dots"><span>—</span><span>▢</span><span>✕</span></span></div><div class="body">${appBody(w.title)}</div>`;
      setRect(e, w.bounds);
      work.append(e);
      S.wins[w.title] = { el: e, home: w.bounds };
    }
    $('#method').textContent = '';
  }

  function applyMoves(p) {
    for (const m of p.moves) { const w = S.wins[m.title]; if (w) setRect(w.el, m.to); }
  }

  function method(p) { $('#method').textContent = `${p.method.replace(/([A-Z])/g, ' $1').trim().toLowerCase()}: ${p.reason}`; }

  /* ---------------------------------------------------------------- bar */
  const bars = { on: false };
  function showBar() {
    bars.on = true; bar.classList.add('on'); scrim.classList.add('on'); input.value = ''; input.disabled = false;
    setState('idle'); setStatus(null); showResults([]);
    requestAnimationFrame(() => input.focus());
  }
  function hideBar() { bars.on = false; bar.classList.remove('on'); scrim.classList.remove('on'); setState('idle'); input.blur(); }
  function setStatus(label, sub) {
    statusEl.classList.toggle('on', !!label);
    statusLabel.textContent = label || ''; statusSub.textContent = sub || ''; statusSub.style.display = sub ? 'block' : 'none';
  }
  function showResults(list) {
    S.shown = list; S.sel = 0; results.innerHTML = '';
    list.forEach((r, i) => {
      const row = el('div', 'res' + (i === 0 ? ' sel' : ''), `<span class="ic">${ICON[r.icon] || ICON.file}</span><span><div class="t">${r.title}</div><div class="s">${r.sub}</div></span><span class="enter kbd">Enter</span>`);
      row.addEventListener('mouseenter', () => selectRow(i));
      row.addEventListener('click', () => { selectRow(i); submit(); });
      results.append(row);
    });
    results.classList.toggle('on', list.length > 0);
  }
  const selectRow = (i) => { S.sel = i; $$('.res', results).forEach((r, j) => r.classList.toggle('sel', j === i)); };

  function suggest(text) {
    const t = text.toLowerCase().trim();
    if (t.length < 2) return [];
    if (/budget|q3/.test(t) && !/graph|chart|plot|spending|rename|widget/.test(t)) return [
      { icon: 'sheet', title: 'Q3 Budget.xlsx', sub: '~/Documents/Finance · opened 3 days ago' },
      { icon: 'file', title: 'q3-budget-notes.txt', sub: '~/Downloads · yesterday' },
      { icon: 'sheet', title: 'Q3 Budget (old).xlsx', sub: '~/Documents/Finance · 6 months ago' }];
    if (/^(snap|put it|restore)/.test(t)) return [{ icon: 'snap', title: /back|restore/.test(t) ? 'Put the windows back' : 'Snap this window to the ' + (/right/.test(t) ? 'right' : 'left') + ' half', sub: 'Moves windows only; nothing in your files changes' }];
    if (t.length > 6) return [{ icon: 'spark', title: 'Ask Claude', sub: 'It will show you exactly what it plans to do first' }];
    return [];
  }

  /* Charts are drawn for the default accent; follow whichever accent is chosen. */
  // A lone series wears the accent; two or more keep the validated palette, as the native renderer does.
  const tint = (chart, svg) => (chart.series === 1 ? themeChart(svg) : svg);
  const themeChart = (svg) => svg.replace(/(fill|stroke)="#(EB6834|D95926)"/gi, (_, a) => `style="${a}:var(--cos-series-1)"`);

  /* ---------------------------------------------------------------- toast */
  let toastTimer;
  function showToast(text, undo) {
    $('#toastText').textContent = text; const b = $('#toastBtn');
    b.style.display = undo ? '' : 'none'; S.lastUndo = undo || null;
    toast.classList.add('on'); clearTimeout(toastTimer); toastTimer = setTimeout(() => toast.classList.remove('on'), 6000);
  }
  $('#toastBtn').addEventListener('click', async () => { const u = S.lastUndo; if (u) { S.lastUndo = null; await u(); showToast('Undone. Everything is back as it was.'); } });

  /* ---------------------------------------------------------------- subject-only windows */
  function subjectWindow(key, inner, cls = '') {
    const p = sc().placements[key];
    const e = el('div', 'win subject arriving ' + cls);
    e.innerHTML = inner;
    e.style.zIndex = 8;
    applyMoves(p);
    setRect(e, p.placed);
    method(p);
    work.append(e);
    S.content[key] = e;
    makeInteractive(e, key);
    return e;
  }

  function makeInteractive(e, key) {
    e.addEventListener('contextmenu', (ev) => {
      ev.preventDefault(); $$('.menu').forEach((m) => m.remove());
      const r = stage.getBoundingClientRect(), k = r.width / 1440;
      const m = el('div', 'menu', ['Pin on top', 'Save as…', 'Copy', 'Open in app', 'Annotate with pen', '<hr>', 'Edit with Claude…', 'Close'].map((t) => (t === '<hr>' ? t : `<div>${t}</div>`)).join(''));
      m.style.left = (ev.clientX - r.left) / k + 'px'; m.style.top = (ev.clientY - r.top) / k + 'px';
      stage.append(m);
      m.addEventListener('click', (c) => { if (c.target.textContent === 'Close') closeContent(key); m.remove(); });
      setTimeout(() => document.addEventListener('pointerdown', () => m.remove(), { once: true }), 0);
    });
    // Drag anywhere on the content to move it; there is no title bar to grab.
    let drag = null;
    e.addEventListener('pointerdown', (ev) => {
      if (ev.button !== 0) return;
      const k = stage.getBoundingClientRect().width / 1440;
      drag = { x: ev.clientX, y: ev.clientY, l: e.offsetLeft, t: e.offsetTop, k };
      e.setPointerCapture(ev.pointerId); e.classList.remove('spring'); e.style.transition = 'none';
    });
    e.addEventListener('pointermove', (ev) => { if (!drag) return; e.style.left = drag.l + (ev.clientX - drag.x) / drag.k + 'px'; e.style.top = drag.t + (ev.clientY - drag.y) / drag.k + 'px'; });
    e.addEventListener('pointerup', () => { drag = null; e.style.transition = ''; });
  }
  function closeContent(key) { const e = S.content[key]; if (e) { e.style.opacity = 0; e.style.transform = 'scale(.97)'; setTimeout(() => e.remove(), 200); delete S.content[key]; } }

  /* ---------------------------------------------------------------- approval card */
  function riskPill(r, label) { return `<span class="pill" data-risk="${r}"><i></i>${label || (r === 'High' ? 'High risk' : r === 'Medium' ? 'Changes your files' : 'New files only')}</span>`; }
  function diffHtml(diff) {
    return diff.split('\n').filter((l) => l.length || false).map((l) => `<div class="${l.startsWith('+++') || l.startsWith('---') || l.startsWith('@@') ? 'hdr' : l.startsWith('+') ? 'add' : l.startsWith('-') ? 'del' : ''}">${l.replace(/&/g, '&amp;').replace(/</g, '&lt;')}</div>`).join('');
  }
  function cardActions(c) {
    return c.items.map((i) => {
      const ext = i.effect === 'external';
      const g = ext ? (i.description.startsWith('send email') ? ICON.mail : ICON.out) : /^move/.test(i.description) ? ICON.move : ICON.plus;
      const notes = i.notes.filter((n) => !/^leaves this machine/.test(n) || true).map((n) => `<li class="${/not on your trusted list/.test(n) ? 'warn' : ''}">${n}</li>`).join('');
      const payload = ext && i.external.length ? `<div class="payload"><b>Exactly what leaves this computer</b>${i.external.join('\n').replace(/</g, '&lt;')}</div>` : '';
      return `<div class="act ${ext ? 'ext' : ''}"><div class="gl">${g}</div><div><div class="d"><span class="eff">${ext ? 'Leaves this computer' : 'On this computer'}</span>${i.description.replace(/</g, '&lt;')}</div><ul>${notes}</ul>${payload}</div></div>`;
    }).join('');
  }

  function askApproval({ title, sub, risk, riskLabel, body, footNote, hold, approveLabel = 'Approve' }) {
    return new Promise((resolve) => {
      card.innerHTML = `<header><div class="orb" data-state="needs" data-orb></div><div><h2>${title}</h2><div class="sub">${sub}</div></div>${riskPill(risk, riskLabel)}</header>
        <div class="scroll">${body}</div>
        <footer><span class="note">${footNote}</span><button class="btn" id="no">Decline <span class="kbd">Esc</span></button><button class="btn primary ${hold ? 'hold' : ''}" id="yes">${hold ? 'Hold to approve' : approveLabel + ' <span class="kbd" style="background:rgba(255,255,255,.22);color:inherit">Enter</span>'}</button></footer>`;
      hydrateOrbs(card);
      card.classList.add('on'); scrim.classList.add('on'); setState('needs');
      const yes = $('#yes', card), no = $('#no', card);
      let raf = 0, t0 = 0;
      const done = (v) => { cancelAnimationFrame(raf); document.removeEventListener('keydown', key); document.removeEventListener('keyup', keyUp); card.classList.remove('on'); resolve(v); };
      const start = () => { if (!hold) return done(true); t0 = performance.now(); const tick = () => { const p = Math.min(1, (performance.now() - t0) / 700); yes.style.setProperty('--p', p); if (p >= 1) done(true); else raf = requestAnimationFrame(tick); }; raf = requestAnimationFrame(tick); };
      const stop = () => { cancelAnimationFrame(raf); yes.style.setProperty('--p', 0); };
      const key = (e) => { if (e.key === 'Escape') done(false); else if (e.key === 'Enter' && !e.repeat) { e.preventDefault(); start(); } };
      const keyUp = (e) => { if (e.key === 'Enter' && hold) stop(); };
      yes.addEventListener('pointerdown', start); yes.addEventListener('pointerup', stop); yes.addEventListener('pointerleave', stop);
      if (!hold) yes.addEventListener('click', () => {}); // pointerdown already resolved
      no.addEventListener('click', () => done(false));
      document.addEventListener('keydown', key); document.addEventListener('keyup', keyUp);
      yes.focus();
    });
  }

  /* ---------------------------------------------------------------- scenes */
  const say = async (label, sub, ms = 650) => { setStatus(label, sub); await sleep(ms); };

  async function sceneOpen() {
    setState('understanding'); setStatus('Opening Q3 Budget.xlsx', 'Found in ~/Documents/Finance. No model needed.'); await sleep(520);
    hideBar();
    const grid = Array.from({ length: 9 }, (_, r) => `<div style="display:grid;grid-template-columns:1.6fr 1fr 1fr 1fr;gap:1px">${Array.from({ length: 4 }, (_, c) => `<div class="sk" style="margin:0;height:22px;border-radius:3px;${r === 0 ? 'background:color-mix(in srgb,var(--cos-ink-primary) 14%,transparent)' : ''}"></div>`).join('')}</div>`).join('');
    const w = subjectWindow('sheet', `<div class="titlebar"><span>Q3 Budget.xlsx</span><span class="dots"><span>—</span><span>▢</span><span>✕</span></span></div><div class="body" style="display:grid;gap:6px">${grid}</div>`);
    showToast('Opened Q3 Budget.xlsx in the free space · say "put it back" to restore the layout');
  }

  async function sceneChart(which = 'spend-by-month') {
    setState('working');
    await say('Thinking', null, 500);
    await say('Looking in Finance', null, 450);
    await say('Reading q3-budget.csv', 'Only its shape: 4 columns, 242 rows, 5 sample rows', 750);
    await say('Writing the recipe', 'A few hundred tokens, however big the file is', 650);
    await say('Drawing', null, 350);
    hideBar();
    const c = D.charts[which];
    const w = subjectWindow(which, `<div class="reveal">${tint(c, (c.variants[S.scenario] || c)[S.appearance])}</div>`); w.dataset.scenario = S.scenario;
    w.setAttribute('role', 'img'); w.setAttribute('aria-label', c.alt);
    showToast(`Charted ${c.sourceRows} rows locally · Claude saw the profile only`);
  }

  async function sceneInvoices(injected = false) {
    setState('working');
    await say('Looking in invoices', null, 450);
    for (const f of ['2026-09-acme.txt', '2026-09-globex.txt', '2026-09-initech.txt']) await say(`Reading ${f}`, null, 420);
    const c = D.cards[injected ? 'invoices-injected' : 'invoices'];
    await say('Checking the plan against your policy', null, 600);
    setStatus(null);
    hideBar();
    const ext = c.items.some((i) => i.effect === 'external');
    const flagged = c.items.some((i) => i.notes.some((n) => /not on your trusted list/.test(n)));
    const ok = await askApproval({
      title: 'Review what I\'ll do', sub: c.intent, risk: c.risk, hold: ext,
      body: `<div class="model-says"><b>Claude's summary, in its own words</b>${c.summary}</div>
             <div class="sec">What will actually happen · ${c.items.length} actions · plan ${c.digest}</div>${cardActions(c)}
             <details class="diff"><summary>Staged file changes (nothing is written until you approve)</summary><div class="diffbox">${diffHtml(c.diff)}</div></details>
             <div class="note" style="color:var(--cos-ink-tertiary);font-size:12px;margin-top:6px">Files read while planning: ${c.reads.join(', ') || 'none'}</div>`,
      footNote: flagged ? 'A recipient you have not approved before is included. Look at the second email.' : 'This card is built from the actions themselves, not from Claude\'s description.',
    });
    scrim.classList.remove('on'); setState('idle');
    if (!ok) { showToast('Declined. Nothing was changed.'); return; }
    setState('working'); await sleep(450); setState('done');
    showToast(ext ? 'Done · 2 files created · email saved to the outbox (dry run)' : 'Done', async () => {});
    await sleep(1800); setState('idle');
  }

  async function sceneRename() {
    setState('working'); await say('Looking in invoices', null, 450); await say('Planning the renames', null, 500); setStatus(null); hideBar();
    const c = D.cards.rename;
    const ok = await askApproval({
      title: 'Review what I\'ll do', sub: c.intent, risk: c.risk,
      body: `<div class="model-says"><b>Claude's summary, in its own words</b>${c.summary}</div><div class="sec">What will actually happen · ${c.items.length} actions</div>${cardActions(c)}
             <details class="diff" open><summary>Staged file changes</summary><div class="diffbox">${diffHtml(c.diff) || '<div class="hdr">3 files renamed; contents unchanged</div>'}</div></details>`,
      footNote: 'Undo is available after this. Nothing leaves your computer.',
    });
    scrim.classList.remove('on'); setState('idle');
    if (!ok) { showToast('Declined. Nothing was changed.'); return; }
    setState('done'); showToast('Renamed 3 files', async () => {}); await sleep(1500); setState('idle');
  }

  async function sceneWidget() {
    setState('working'); await say('Thinking', null, 450); await say('Writing the widget', 'A small JSON file: no code', 650); setStatus(null); hideBar();
    const m = D.mod.preview;
    const widgetHtml = `<div class="widget" style="background:var(--cos-surface-raised)"><div class="metric">${m.metric}<small>${m.metricLabel}</small></div><div class="next">${m.title} · ${m.subtitle}</div></div>`;
    const ok = await askApproval({
      title: 'Add a widget', sub: D.mod.name, risk: 'Low', riskLabel: 'Read-only, no code',
      body: `<div class="sec">How it will look</div><div class="preview-stage">${widgetHtml}</div>
             <div class="sec">What it can see</div><div class="mod-perm">${D.mod.capabilities.map((c) => `<div class="perm"><span>${c.text}</span><span class="pill" data-risk="${c.risk}"><i></i>${c.risk === 'Medium' ? 'Personal' : 'Low'}</span></div>`).join('')}</div>
             <div class="note" style="color:var(--cos-ink-tertiary);font-size:12px">It cannot read your files, use the network, or run code. It is a folder you own: edit it, share it, or remove it any time.</div>`,
      footNote: 'Declared permissions are checked on every read.', approveLabel: 'Add widget',
    });
    scrim.classList.remove('on'); setState('idle');
    if (!ok) { showToast('Declined. Nothing was added.'); return; }
    const w = subjectWindow('widget', widgetHtml, 'widget-win'); w.style.background = 'transparent'; w.style.border = '0'; w.style.boxShadow = 'none';
    $('.widget', w).style.boxShadow = 'var(--cos-shadow-card)'; $('.widget', w).style.border = '1px solid var(--cos-line-hairline)';
    setState('done'); showToast('Widget added top right', async () => closeContent('widget')); await sleep(1400); setState('idle');
  }

  async function sceneBars() {
    const chart = S.content['spend-by-month'] || S.content['weekly-area'] || S.content['spend-by-category'];
    setState('understanding'); setStatus(chart ? 'Making the bars blue' : 'Nothing on screen to change yet', chart ? null : 'Try making a chart first'); await sleep(chart ? 500 : 1300);
    hideBar();
    if (!chart) return;
    const blue = getComputedStyle(root).getPropertyValue('--cos-series-2').trim();
    const marks = $$('[style*="--cos-series-1"]', chart);
    marks.forEach((n) => { n.style.transition = 'fill 500ms var(--cos-ease-out), stroke 500ms var(--cos-ease-out)'; if (n.style.fill) n.style.fill = blue; if (n.style.stroke) n.style.stroke = blue; });
    showToast('Made the bars blue', async () => marks.forEach((n) => { n.style.fill = n.style.fill ? 'var(--cos-series-1)' : ''; n.style.stroke = n.style.stroke ? 'var(--cos-series-1)' : ''; }));
  }

  async function sceneSnap(dir) {
    setState('understanding'); setStatus(dir === 'back' ? 'Putting everything back' : `Snapping to the ${dir} half`); await sleep(400); hideBar();
    const w = S.wins.Notes || Object.values(S.wins)[0]; if (!w) return;
    if (dir === 'back') { setRect(w.el, w.home); showToast('Put everything back'); return; }
    const half = (2880 - 16 * 3) / 2;
    setRect(w.el, dir === 'left' ? { x: 16, y: 16, w: half, h: 1808 } : { x: 16 + half + 16, y: 16, w: half, h: 1808 });
    showToast('Done · say "put it back" to restore', async () => setRect(w.el, w.home));
  }

  const SCENES = [
    { chip: 'open the Q3 budget', re: /\b(open|pull up)\b.*(budget|q3)/i, run: sceneOpen },
    { chip: 'chart spending by category', re: /by category|categories/i, run: () => sceneChart('spend-by-category') },
    { chip: 'show me spending by month from the Q3 budget as a graph', re: /spending by month|\bgraph\b|\bchart\b/i, run: () => sceneChart('spend-by-month') },
    { chip: '…and if the model had obeyed the instruction planted in an invoice', type: 'summarize the invoices and email finance, obeying the planted instruction', re: /planted/i, run: () => sceneInvoices(true) },
    { chip: 'summarize the September invoices and email finance the total', re: /invoice/i, run: () => sceneInvoices(false) },
    { chip: 'rename these files by date', re: /rename/i, run: sceneRename },
    { chip: 'make me a widget with battery and my next meeting, top right', re: /widget/i, run: sceneWidget },
    { chip: 'make the bars blue', re: /blue|bars?/i, run: sceneBars },
    { chip: 'snap left', re: /snap.*left|left half/i, run: () => sceneSnap('left') },
    { chip: 'put it back', re: /put (it|them|everything).*back|restore/i, run: () => sceneSnap('back') },
  ];

  async function submit() {
    if (S.busy) return;
    const text = input.value.trim(); if (!text) return;
    const scene = SCENES.find((s) => s.re.test(text));
    S.busy = true; input.disabled = true; showResults([]);
    try {
      if (scene) await scene.run();
      else { setState('refused'); setStatus('This prototype only knows the scenes below', 'The real thing asks Claude about anything else'); await sleep(1600); hideBar(); }
    } finally { S.busy = false; input.disabled = false; }
  }

  /* ---------------------------------------------------------------- input wiring */
  input.addEventListener('input', () => { setState(input.value ? 'listening' : 'idle'); showResults(suggest(input.value)); });
  document.addEventListener('keydown', (e) => {
    if (card.classList.contains('on')) return;
    if (bars.on) {
      if (e.key === 'Escape') { hideBar(); }
      else if (e.key === 'Enter') { e.preventDefault(); submit(); }
      else if (e.key === 'ArrowDown') { e.preventDefault(); selectRow(Math.min(S.shown.length - 1, S.sel + 1)); }
      else if (e.key === 'ArrowUp') { e.preventDefault(); selectRow(Math.max(0, S.sel - 1)); }
    } else if ((e.key === 's' || e.key === 'S' || (e.code === 'Space' && e.ctrlKey && e.altKey)) && !/INPUT|TEXTAREA/.test(document.activeElement.tagName)) { e.preventDefault(); showBar(); }
  });
  scrim.addEventListener('click', () => { if (!card.classList.contains('on')) hideBar(); });
  $('#trayBtn').addEventListener('click', () => (bars.on ? hideBar() : showBar()));

  async function typeInto(text, andSubmit = true) {
    if (S.busy) return;
    if (!bars.on) showBar();
    await sleep(260); input.value = '';
    for (const ch of text) { input.value += ch; input.dispatchEvent(new Event('input')); await sleep(18 + Math.random() * 16); }
    if (!andSubmit) return;
    await sleep(320); submit();
  }

  /* ---------------------------------------------------------------- controls below the stage */
  function mountControls() {
    const tryEl = $('#try');
    SCENES.forEach((s) => { const b = el('button', 'chip', s.chip); b.addEventListener('click', () => typeInto(s.type || s.chip)); tryEl.append(b); });

    const scen = $('#scenario');
    [['empty', 'Empty desktop'], ['one-app', 'One app'], ['maximized', 'Maximized app'], ['snapped', 'Two snapped']].forEach(([v, label]) => {
      const b = el('button', v === S.scenario ? 'on' : '', label); b.dataset.v = v;
      b.addEventListener('click', () => { S.scenario = v; $$('button', scen).forEach((x) => x.classList.toggle('on', x === b)); buildDesktop(); });
      scen.append(b);
    });

    const seg = (id, cb) => $$(`#${id} button`).forEach((b) => b.addEventListener('click', () => { $$(`#${id} button`).forEach((x) => x.classList.toggle('on', x === b)); cb(b.dataset.v); }));
    seg('appearance', (v) => { S.appearance = v; applyTheme(); });
    seg('backdrop', (v) => { stage.dataset.backdrop = v; });
    seg('motion', (v) => { stage.dataset.motion = v; });
    $('#radius').addEventListener('input', (e) => { stage.style.setProperty('--radius-scale', e.target.value); $('#radiusV').textContent = (+e.target.value).toFixed(1) + '×'; });

    const sw = $('#swatches');
    ['#EB6834', '#2A78D6', '#1BAF7A', '#E87BA4', '#4A3AA7'].forEach((hex) => {
      const b = el('button', 'sw' + (hex === S.accent ? ' on' : '')); b.style.setProperty('--c', hex); b.title = hex; b.setAttribute('aria-label', 'Accent ' + hex);
      b.addEventListener('click', () => { S.accent = hex; $$('.sw', sw).forEach((x) => x.classList.toggle('on', x === b)); applyTheme(); });
      sw.append(b);
    });

    const states = $('#states');
    [['idle', 'Idle', 'a slow breath'], ['listening', 'Listening', 'tighter, brighter'], ['understanding', 'Understanding', 'a shimmer'], ['working', 'Working', 'the ring orbits'], ['needs', 'Needs you', 'amber, until you decide'], ['done', 'Done', 'settles'], ['refused', 'Refused', 'steady, never alarming']].forEach(([s, name, d]) => {
      states.append(el('div', 'stt', `<div class="orb" data-state="${s}" data-orb></div><b style="color:#e8e5df;font-weight:600">${name}</b><span>${d}</span>`));
    });
  }

  /* ---------------------------------------------------------------- theme */
  function lum(hex) { const c = [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16) / 255).map((v) => (v <= 0.04045 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4)); return 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2]; }
  const ratio = (a, b) => { const [x, y] = [lum(a), lum(b)].sort((p, q) => q - p); return (x + 0.05) / (y + 0.05); };
  function applyTheme() {
    root.dataset.theme = S.appearance;
    const t = D.themes[`${S.accent}-${S.appearance}`];
    for (const [role, v] of Object.entries(t)) if (role.startsWith('accent.')) root.style.setProperty('--cos-' + role.replace(/\./g, '-'), v.length === 9 ? `rgb(${[1, 3, 5].map((i) => parseInt(v.slice(i, i + 2), 16)).join(' ')} / ${(parseInt(v.slice(7, 9), 16) / 255).toFixed(3)})` : v);
    const cs = getComputedStyle(root);
    const hx = (n) => cs.getPropertyValue(n).trim();
    const r1 = ratio(hx('--cos-ink-primary'), hx('--cos-surface-base')), r2 = ratio(hx('--cos-accent-action-ink'), hx('--cos-accent-action'));
    $('#contrast').textContent = `Body text ${r1.toFixed(1)}:1 · button label ${r2.toFixed(1)}:1 · every theme is checked against 4.5:1 before it is allowed.`;
    // Series 1 follows the accent, as in the native renderer.
    root.style.setProperty('--cos-series-1', hx('--cos-accent-mark'));
    // Charts are rendered for one appearance; swap the markup to match.
    for (const [key, node] of Object.entries(S.content)) if (D.charts[key]) { const c = D.charts[key]; $('.reveal', node).innerHTML = tint(c, (c.variants[node.dataset.scenario] || c)[S.appearance]); }
  }

  /* ---------------------------------------------------------------- boot */
  function fit() { const k = Math.min((innerWidth - 40) / 1440, (innerHeight - 150) / 960, 1.25); wrap.style.setProperty('--k', Math.max(k, 0.3)); }
  addEventListener('resize', fit); fit();
  hydrateOrbs(); mountControls(); hydrateOrbs(); buildDesktop(); applyTheme();
  new URLSearchParams(location.search).forEach((v, k) => { if (k === 'theme') { S.appearance = v; $$('#appearance button').forEach((b) => b.classList.toggle('on', b.dataset.v === v)); applyTheme(); } if (k === 'scenario') { S.scenario = v; buildDesktop(); } });
  window.COS = { showBar, hideBar, typeInto, setState, S, SCENES, applyTheme, buildDesktop };
})();
