// Uncovering Greatness CRM — front-end behaviour.
// Strict CSP (script-src 'self'): no inline scripts or handlers anywhere; everything is wired up here via data-* attributes.
document.addEventListener('DOMContentLoaded', function () {
  var csrf = (document.querySelector('meta[name="csrf-token"]') || {}).content || '';

  function post(url, body) {
    var headers = { 'RequestVerificationToken': csrf, 'X-Requested-With': 'XMLHttpRequest' };
    var opts = { method: 'POST', headers: headers, credentials: 'same-origin' };
    if (body !== undefined) { headers['Content-Type'] = 'application/json'; opts.body = JSON.stringify(body); }
    return fetch(url, opts).then(function (r) {
      return r.json().catch(function () { return {}; }).then(function (j) { j.status = r.status; j.httpOk = r.ok; return j; });
    });
  }
  function toast(msg, kind) {
    var host = document.querySelector('.content'); if (!host) return;
    var el = document.createElement('div');
    el.className = 'flash ' + (kind || 'error'); el.setAttribute('role', (kind || 'error') === 'error' ? 'alert' : 'status');
    el.innerHTML = '<i class="bi bi-info-circle"></i>';
    el.appendChild(document.createTextNode(' ' + msg));
    host.insertBefore(el, host.firstChild);
    setTimeout(function () { el.remove(); }, 5000);
  }

  document.querySelectorAll('i.bi').forEach(function (i) { i.setAttribute('aria-hidden', 'true'); });

  // Mobile sidebar
  var toggle = document.querySelector('.menu-toggle'), sidebar = document.querySelector('.sidebar'), scrim = document.querySelector('.scrim');
  function setSidebar(open) {
    if (!sidebar) return;
    sidebar.classList.toggle('open', open); scrim && scrim.classList.toggle('show', open);
    toggle && toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
  }
  function closeSidebar() { setSidebar(false); }
  if (toggle && sidebar) toggle.addEventListener('click', function () { setSidebar(!sidebar.classList.contains('open')); });
  scrim && scrim.addEventListener('click', closeSidebar);

  // Tabs (client-side panels) with ARIA roles and arrow-key navigation
  document.querySelectorAll('.tabs[data-tabs]').forEach(function (group, g) {
    var panels = document.querySelectorAll('#' + group.getAttribute('data-tabs') + ' > .tab-panel');
    var tabs = Array.prototype.slice.call(group.querySelectorAll('.tab'));
    group.setAttribute('role', 'tablist');
    function select(i, focus) {
      tabs.forEach(function (t, k) {
        var on = k === i;
        t.classList.toggle('active', on); t.setAttribute('aria-selected', on ? 'true' : 'false'); t.setAttribute('tabindex', on ? '0' : '-1');
      });
      panels.forEach(function (p, k) { p.classList.toggle('active', k === i); });
      if (focus) tabs[i].focus();
    }
    tabs.forEach(function (tab, i) {
      var tid = 'tab-' + g + '-' + i;
      tab.id = tab.id || tid; tab.setAttribute('role', 'tab');
      if (panels[i]) { panels[i].id = panels[i].id || 'tabpanel-' + g + '-' + i; panels[i].setAttribute('role', 'tabpanel'); panels[i].setAttribute('aria-labelledby', tab.id); tab.setAttribute('aria-controls', panels[i].id); }
      tab.addEventListener('click', function () { select(i, false); });
      tab.addEventListener('keydown', function (e) {
        var n = null;
        if (e.key === 'ArrowRight') n = (i + 1) % tabs.length;
        else if (e.key === 'ArrowLeft') n = (i - 1 + tabs.length) % tabs.length;
        else if (e.key === 'Home') n = 0; else if (e.key === 'End') n = tabs.length - 1;
        if (n !== null) { e.preventDefault(); select(n, true); }
      });
    });
    var start = tabs.findIndex(function (t) { return t.classList.contains('active'); });
    select(start < 0 ? 0 : start, false);
  });
  if (location.hash === '#notes') { var nt = document.querySelector('.tab[data-tab-name="notes"]'); nt && nt.click(); }

  // Dropdowns (click, Enter/Space, Escape) with aria-expanded
  function closeDropdowns(except) {
    document.querySelectorAll('.dropdown-panel.show').forEach(function (m) {
      if (m === except) return;
      m.classList.remove('show');
      var b = document.querySelector('[data-dropdown="' + m.id + '"]'); b && b.setAttribute('aria-expanded', 'false');
    });
  }
  document.querySelectorAll('[data-dropdown]').forEach(function (btn) {
    var menu = document.getElementById(btn.getAttribute('data-dropdown')); if (!menu) return;
    function toggleMenu(e) {
      e.stopPropagation(); closeDropdowns(menu);
      var open = menu.classList.toggle('show'); btn.setAttribute('aria-expanded', open ? 'true' : 'false');
      if (open) { var first = menu.querySelector('a,button'); first && first.focus(); }
    }
    btn.addEventListener('click', toggleMenu);
    btn.addEventListener('keydown', function (e) { if (btn.tagName !== 'BUTTON' && (e.key === 'Enter' || e.key === ' ')) { e.preventDefault(); toggleMenu(e); } });
  });
  document.addEventListener('click', function (e) { if (!e.target.closest('.dropdown-panel')) closeDropdowns(); });
  document.addEventListener('keydown', function (e) {
    if (e.key !== 'Escape') return;
    var open = document.querySelector('.dropdown-panel.show');
    if (open) { var b = document.querySelector('[data-dropdown="' + open.id + '"]'); closeDropdowns(); b && b.focus(); }
    closeSidebar();
  });

  // Clickable table rows (replaces inline onclick, which the CSP forbids)
  document.querySelectorAll('tr[data-href]').forEach(function (row) {
    row.addEventListener('click', function (e) { if (!e.target.closest('a,button,input,select,form,label')) location.href = row.getAttribute('data-href'); });
  });

  document.querySelectorAll('tr[data-href]').forEach(function (row) {
    if (row.querySelector('a[href]')) return; // a real link already gives keyboard access
    row.setAttribute('tabindex', '0'); row.setAttribute('role', 'link');
    row.addEventListener('keydown', function (e) { if (e.key === 'Enter' && e.target === row) location.href = row.getAttribute('data-href'); });
  });

  // Auto-submit filters
  document.querySelectorAll('select[data-autosubmit]').forEach(function (s) { s.addEventListener('change', function () { s.form.submit(); }); });

  // Confirm dialogs on destructive forms
  document.querySelectorAll('form[data-confirm]').forEach(function (f) {
    f.addEventListener('submit', function (e) { if (!window.confirm(f.getAttribute('data-confirm'))) e.preventDefault(); });
  });

  // Prevent double submits
  document.querySelectorAll('form').forEach(function (f) {
    f.addEventListener('submit', function (e) {
      if (e.defaultPrevented) return;
      f.setAttribute('aria-busy', 'true');
      f.querySelectorAll('button[type=submit]:not([data-keep])').forEach(function (b) { setTimeout(function () { b.disabled = true; b.classList.add('is-loading'); b.setAttribute('aria-label', (b.textContent || 'Saving').trim() + ' in progress'); }, 0); });
    });
  });

  // Star toggle
  document.querySelectorAll('[data-star]').forEach(function (el) { el.setAttribute('aria-pressed', el.classList.contains('is-on') ? 'true' : 'false'); });
  document.querySelectorAll('[data-star]').forEach(function (el) {
    el.addEventListener('click', function (e) {
      e.stopPropagation(); e.preventDefault();
      post('/Leads/ToggleStar/' + el.getAttribute('data-star')).then(function (r) {
        if (r.ok) { var on = !!(r.data && r.data.starred); el.classList.toggle('is-on', on); el.setAttribute('aria-pressed', on ? 'true' : 'false'); } else toast(r.error || 'Could not update star.');
      });
    });
  });

  // Task completion checkbox
  document.querySelectorAll('input[data-task]').forEach(function (cb) {
    cb.addEventListener('change', function () {
      var wanted = cb.checked;
      post('/Tasks/Toggle/' + cb.getAttribute('data-task') + '?completed=' + wanted).then(function (r) {
        if (r.ok) { var row = cb.closest('[data-task-row]'); row && row.classList.toggle('is-done', wanted); setTimeout(function () { location.reload(); }, 350); }
        else { cb.checked = !wanted; toast(r.error || 'Could not update task.'); }
      });
    });
  });

  // Notifications: mark as read
  document.querySelectorAll('[data-notif]').forEach(function (el) {
    el.addEventListener('click', function () { navigator.sendBeacon && post('/Notifications/MarkRead/' + el.getAttribute('data-notif')); });
  });
  document.querySelectorAll('[data-mark-all]').forEach(function (a) {
    a.addEventListener('click', function (e) {
      e.preventDefault();
      post('/Notifications/MarkAllRead').then(function () {
        document.querySelectorAll('.notif-dot,.dropdown-item.unread,.notif-row.unread').forEach(function (n) { n.classList.remove('unread'); if (n.classList.contains('notif-dot')) n.remove(); });
      });
    });
  });

  // Pipeline drag & drop -> moves stage via AJAX
  var dragged = null;
  document.querySelectorAll('.deal-card[draggable=true]').forEach(function (card) {
    card.addEventListener('dragstart', function (e) { dragged = card; card.classList.add('dragging'); e.dataTransfer.effectAllowed = 'move'; try { e.dataTransfer.setData('text/plain', card.getAttribute('data-lead')); } catch (x) {} });
    card.addEventListener('dragend', function () { card.classList.remove('dragging'); document.querySelectorAll('.drop-target').forEach(function (d) { d.classList.remove('drop-target'); }); });
  });
  document.querySelectorAll('.pipeline-cards[data-stage]').forEach(function (col) {
    col.addEventListener('dragover', function (e) { if (dragged) { e.preventDefault(); col.classList.add('drop-target'); } });
    col.addEventListener('dragleave', function () { col.classList.remove('drop-target'); });
    col.addEventListener('drop', function (e) {
      e.preventDefault(); col.classList.remove('drop-target');
      if (!dragged) return;
      var card = dragged, from = card.parentElement; dragged = null;
      if (from === col) return;
      col.insertBefore(card, col.firstChild);
      post('/Leads/MoveStage/' + card.getAttribute('data-lead'), { stage: parseInt(col.getAttribute('data-stage'), 10) }).then(function (r) {
        if (r.ok) location.reload(); else { from.insertBefore(card, from.firstChild); toast(r.error || 'Could not move lead.'); }
      });
    });
  });

  // Calendar: visibility picker on create/edit
  function syncVisibility() {
    var checked = document.querySelector('input[name="Visibility"]:checked');
    var shareList = document.getElementById('shareWithList');
    document.querySelectorAll('.visibility-option').forEach(function (o) { o.classList.remove('is-selected'); });
    if (checked) { var o = checked.closest('.visibility-option'); o && o.classList.add('is-selected'); }
    if (shareList) shareList.classList.toggle('show', !!checked && checked.value === 'Shared');
  }
  document.querySelectorAll('input[name="Visibility"]').forEach(function (r) { r.addEventListener('change', syncVisibility); });
  syncVisibility();

  // Calendar: linked-record picker (type -> id list)
  var linkedType = document.getElementById('LinkedType');
  if (linkedType) {
    var sync = function () {
      document.querySelectorAll('[data-linked-for]').forEach(function (el) {
        var on = el.getAttribute('data-linked-for') === linkedType.value;
        el.classList.toggle('is-hidden', !on);
        el.querySelectorAll('select').forEach(function (s) { s.disabled = !on; });
      });
    };
    linkedType.addEventListener('change', sync); sync();
  }

  // Campaign form: audience selector + live recipient count + preview
  var target = document.getElementById('Target');
  if (target) {
    var countEl = document.getElementById('audienceCount');
    var refresh = function () {
      document.querySelectorAll('[data-target-for]').forEach(function (el) {
        var on = el.getAttribute('data-target-for') === target.value;
        el.classList.toggle('is-hidden', !on);
        el.querySelectorAll('select').forEach(function (s) { s.disabled = !on; });
      });
      var year = document.getElementById('TargetYear'), ev = document.getElementById('TargetEventId');
      var qs = 'target=' + encodeURIComponent(target.value) + (year && !year.disabled && year.value ? '&year=' + year.value : '') + (ev && !ev.disabled && ev.value ? '&eventId=' + ev.value : '');
      fetch('/Campaigns/Audience?' + qs, { credentials: 'same-origin' }).then(function (r) { return r.json(); })
        .then(function (j) { if (countEl) countEl.textContent = j.count; }).catch(function () {});
    };
    target.addEventListener('change', refresh);
    ['TargetYear', 'TargetEventId'].forEach(function (id) { var e = document.getElementById(id); e && e.addEventListener('change', refresh); });
    refresh();
  }

  // Password show/hide
  document.querySelectorAll('[data-toggle-password]').forEach(function (btn) {
    btn.addEventListener('click', function () {
      var input = document.getElementById(btn.getAttribute('data-toggle-password')); if (!input) return;
      input.type = input.type === 'password' ? 'text' : 'password';
    });
  });

  // Copy-to-clipboard helper
  document.querySelectorAll('[data-copy]').forEach(function (btn) {
    btn.addEventListener('click', function () {
      var input = document.getElementById(btn.getAttribute('data-copy')); if (!input) return;
      var label = btn.textContent; navigator.clipboard && navigator.clipboard.writeText(input.value).then(function () { btn.textContent = 'Copied'; setTimeout(function () { btn.textContent = label; }, 1800); });
    });
  });
});

// Restore buttons when the browser brings a page back from its back/forward cache
window.addEventListener('pageshow', function (e) {
  if (!e.persisted) return;
  document.querySelectorAll('form[aria-busy]').forEach(function (f) { f.removeAttribute('aria-busy'); });
  document.querySelectorAll('.btn.is-loading').forEach(function (b) { b.disabled = false; b.classList.remove('is-loading'); b.removeAttribute('aria-label'); });
});
