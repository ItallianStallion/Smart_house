// Фронтенд «Розумний будинок»: чистий JavaScript без збірки, працює з REST API цього ж сервісу.
// Використовує: кімнати (CRUD + пошук), пристрої (CRUD + пошук + увімкнення), вимірювання (CRUD),
// автоматику клімату (спрацьовує на API після нового вимірювання), health-check і відображення помилок (Problem Details).
(() => {
  'use strict';

  const API_BASE = ((window.APP_CONFIG && window.APP_CONFIG.apiBase) || '').replace(/\/$/, '');

  // ---------- Довідники (значення enum з API -> підписи) ----------
  const ROOM_TYPES = { LivingRoom: 'Вітальня', Bedroom: 'Спальня', Kitchen: 'Кухня', Bathroom: 'Ванна кімната', Hallway: 'Передпокій' };
  const ROOM_ICONS = { LivingRoom: '🛋️', Bedroom: '🛏️', Kitchen: '🍳', Bathroom: '🛁', Hallway: '🚪' };
  const DEVICE_TYPES = { Light: 'Світло', Thermostat: 'Термостат', DoorLock: 'Розумний замок', MotionSensor: 'Датчик руху', SmokeDetector: 'Датчик диму' };
  const DEVICE_ICONS = { Light: '💡', Thermostat: '🌡️', DoorLock: '🔒', MotionSensor: '👁️', SmokeDetector: '🔥' };
  const STATUSES = { Vacant: 'Порожньо', Occupied: 'Зайнято', Warning: 'Увага', Alarm: 'Тривога' };

  const state = {
    rooms: [],
    devices: [],
    roomQuery: '',
    deviceQuery: '',
    selectedRoomId: null,
    log: []
  };

  // ---------- Допоміжні функції ----------
  const $ = (selector, root = document) => root.querySelector(selector);
  const esc = (value) => String(value ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const fmtDate = (iso) => (iso ? new Date(iso).toLocaleString('uk-UA', { dateStyle: 'short', timeStyle: 'medium' }) : '—');
  const fmtNum = (n, digits = 1) => Number(n).toLocaleString('uk-UA', { maximumFractionDigits: digits });
  const debounce = (fn, ms = 300) => { let t; return (...a) => { clearTimeout(t); t = setTimeout(() => fn(...a), ms); }; };

  class ApiError extends Error {
    constructor({ status, title, detail, errors, traceId }) {
      super(detail || title || 'Помилка запиту');
      this.status = status;
      this.title = title;
      this.detail = detail;
      this.errors = errors;
      this.traceId = traceId;
    }
    // Рядки для показу користувачу: помилки валідації по полях або загальний опис.
    lines() {
      if (this.errors) {
        return Object.entries(this.errors).flatMap(([field, msgs]) => [].concat(msgs).map((m) => `${field}: ${m}`));
      }
      return this.detail ? [this.detail] : [];
    }
  }

  // Єдина точка звернення до API: журналює запит і перетворює відповіді з помилкою на ApiError.
  async function api(method, path, body) {
    const started = performance.now();
    const options = { method, headers: {} };
    if (body !== undefined) {
      options.headers['Content-Type'] = 'application/json';
      options.body = JSON.stringify(body);
    }

    let response;
    try {
      response = await fetch(API_BASE + path, options);
    } catch (e) {
      addLog(method, path, 0, performance.now() - started);
      throw new ApiError({ status: 0, title: 'Немає зв’язку з API', detail: 'Сервер недоступний або запит заблоковано (перевірте CORS / мережу).' });
    }

    addLog(method, path, response.status, performance.now() - started);
    if (response.status === 204) return null;

    const text = await response.text();
    let data = null;
    try { data = text ? JSON.parse(text) : null; } catch { /* не JSON */ }

    if (!response.ok) {
      throw new ApiError({
        status: response.status,
        title: (data && data.title) || `HTTP ${response.status}`,
        detail: data && data.detail,
        errors: data && data.errors,
        traceId: data && data.traceId
      });
    }
    return data;
  }

  // ---------- Сповіщення ----------
  function toast(kind, title, lines = []) {
    const el = document.createElement('div');
    el.className = `toast toast--${kind}`;
    el.innerHTML = `<b>${esc(title)}</b>${lines.length ? `<ul>${lines.map((l) => `<li>${esc(l)}</li>`).join('')}</ul>` : ''}`;
    $('#toasts').appendChild(el);
    setTimeout(() => el.remove(), kind === 'error' ? 8000 : 4500);
  }

  function showError(e) {
    if (e instanceof ApiError) {
      toast('error', `${e.status || ''} ${e.title || 'Помилка'}`.trim(), e.lines());
    } else {
      console.error(e);
      toast('error', 'Несподівана помилка', [e.message]);
    }
  }

  // ---------- Журнал запитів ----------
  function addLog(method, path, status, ms) {
    state.log.unshift({ time: new Date().toISOString(), method, path, status, ms: Math.round(ms) });
    state.log.length = Math.min(state.log.length, 200);
    $('#log-count').textContent = state.log.length;
    if (!$('#tab-log').hidden) renderLog();
  }

  function renderLog() {
    const statusClass = (s) => (s === 0 || s >= 500 ? 'code--bad' : s >= 400 ? 'code--warn' : 'code--ok');
    $('#log').innerHTML = state.log.length
      ? `<table><thead><tr><th>Час</th><th>Метод</th><th>Адреса</th><th class="num">Статус</th><th class="num">мс</th></tr></thead><tbody>${
        state.log.map((r) => `<tr><td>${esc(fmtDate(r.time))}</td><td class="code">${esc(r.method)}</td><td class="code">${esc(r.path)}</td><td class="num code ${statusClass(r.status)}">${r.status || 'ERR'}</td><td class="num">${r.ms}</td></tr>`).join('')
      }</tbody></table>`
      : '<div class="empty">Запитів ще не було.</div>';
  }

  // ---------- Health-check ----------
  async function checkHealth() {
    const probe = async (path, pillId, okText, badText) => {
      const pill = $(pillId);
      try {
        const res = await fetch(API_BASE + path, { cache: 'no-store' });
        const data = await res.json().catch(() => ({}));
        const ok = res.ok && data.status === 'Healthy';
        pill.className = `pill ${ok ? 'is-ok' : 'is-bad'}`;
        pill.querySelector('span').textContent = ok ? okText : `${badText} (${res.status})`;
        pill.title = `GET ${path} → ${res.status}${data.duration_ms !== undefined ? `, ${data.duration_ms} мс` : ''}`;
      } catch {
        pill.className = 'pill is-bad';
        pill.querySelector('span').textContent = badText;
      }
    };
    await Promise.all([
      probe('/health', '#pill-live', 'API: працює', 'API: недоступний'),
      probe('/health/ready', '#pill-ready', 'БД: підключено', 'БД: недоступна')
    ]);
  }

  // ---------- Завантаження даних ----------
  async function loadRooms() {
    const path = state.roomQuery.trim()
      ? `/api/rooms/search?query=${encodeURIComponent(state.roomQuery.trim())}`
      : '/api/rooms';
    state.rooms = await api('GET', path);
    renderRooms();
  }

  async function loadDevices() {
    const q = state.deviceQuery.trim();
    state.devices = await api('GET', q ? `/api/devices?name=${encodeURIComponent(q)}` : '/api/devices');
    renderDevices();
  }

  // Зведення рахуємо за повним списком (без пошукових фільтрів).
  async function loadStats() {
    const [rooms, devices] = await Promise.all([api('GET', '/api/rooms'), api('GET', '/api/devices')]);
    const active = devices.filter((d) => d.is_enabled).length;
    const warn = rooms.filter((r) => r.status === 'Warning').length;
    const alarm = rooms.filter((r) => r.status === 'Alarm').length;
    const measurements = rooms.reduce((s, r) => s + r.measurements_count, 0);
    $('#stats').innerHTML = [
      ['', rooms.length, 'кімнат'],
      ['', `${active} / ${devices.length}`, 'пристроїв увімкнено'],
      ['', measurements, 'вимірювань у базі'],
      ['stat--warn', warn, 'кімнат зі статусом «Увага»'],
      ['stat--danger', alarm, 'кімнат у стані «Тривога»']
    ].map(([cls, value, label]) => `<div class="stat ${cls}"><b>${esc(value)}</b><span>${label}</span></div>`).join('');
  }

  async function refreshAll() {
    try {
      await Promise.all([loadRooms(), loadDevices(), loadStats()]);
      if (state.selectedRoomId) await renderDrawer();
    } catch (e) { showError(e); }
  }

  // ---------- Відображення: кімнати ----------
  function renderRooms() {
    const el = $('#rooms');
    if (!state.rooms.length) {
      el.innerHTML = `<div class="empty">${state.roomQuery ? 'За запитом кімнат не знайдено.' : 'Кімнат ще немає — додайте першу.'}</div>`;
      return;
    }
    el.innerHTML = state.rooms.map((r) => `
      <article class="card card--${esc(r.status)}" tabindex="0" role="button" data-room="${esc(r.room_id)}" aria-label="Відкрити кімнату ${esc(r.display_name)}">
        <div class="card__head">
          <div class="card__title">
            <span class="card__icon" aria-hidden="true">${ROOM_ICONS[r.room_type] || '🏠'}</span>
            <div><h3>${esc(r.display_name)}</h3><small>${esc(ROOM_TYPES[r.room_type] || r.room_type)} · поверх ${esc(r.floor)}</small></div>
          </div>
          <span class="status status--${esc(r.status)}">${esc(STATUSES[r.status] || r.status)}</span>
        </div>
        <div class="card__meta">
          <div class="meta"><b>${esc(r.target_temperature)}°</b><span>ціль</span></div>
          <div class="meta"><b>${esc(r.devices_count)}</b><span>пристроїв</span></div>
          <div class="meta"><b>${esc(r.measurements_count)}</b><span>вимірювань</span></div>
        </div>
        <small>Синхронізація: ${esc(fmtDate(r.last_sync))}</small>
      </article>`).join('');
  }

  // ---------- Відображення: пристрої ----------
  function deviceRow(d, { showRoom }) {
    return `<tr>
      <td>${DEVICE_ICONS[d.device_type] || '🔌'} ${esc(d.name)}</td>
      <td>${esc(DEVICE_TYPES[d.device_type] || d.device_type)}</td>
      ${showRoom ? `<td>${esc(d.room_name)}</td>` : ''}
      <td><label class="switch" title="PATCH /api/devices/{id}/status"><input type="checkbox" data-action="device-toggle" data-id="${esc(d.device_id)}" ${d.is_enabled ? 'checked' : ''} aria-label="Увімкнути ${esc(d.name)}"><span></span></label></td>
      ${showRoom ? `<td class="muted">${esc(fmtDate(d.last_updated))}</td>` : ''}
      <td class="actions">
        <button class="btn btn--small" data-action="device-edit" data-id="${esc(d.device_id)}">Змінити</button>
        <button class="btn btn--small btn--danger" data-action="device-delete" data-id="${esc(d.device_id)}">Видалити</button>
      </td>
    </tr>`;
  }

  function renderDevices() {
    const el = $('#devices');
    if (!state.devices.length) {
      el.innerHTML = `<div class="empty">${state.deviceQuery ? 'За запитом пристроїв не знайдено.' : 'Пристроїв ще немає.'}</div>`;
      return;
    }
    el.innerHTML = `<table><thead><tr><th>Назва</th><th>Тип</th><th>Кімната</th><th>Стан</th><th>Оновлено</th><th></th></tr></thead><tbody>${
      state.devices.map((d) => deviceRow(d, { showRoom: true })).join('')
    }</tbody></table>`;
  }

  // ---------- Бокова панель кімнати ----------
  function chartSvg(readings, target) {
    if (readings.length < 2) return '<p class="muted">Для графіка потрібно щонайменше два вимірювання.</p>';
    const W = 520, H = 160, pl = 34, pr = 10, pt = 10, pb = 22;
    const temps = readings.map((r) => r.temperature);
    const lo = Math.floor(Math.min(...temps, target) - 1);
    const hi = Math.ceil(Math.max(...temps, target) + 1);
    const x = (i) => pl + (i * (W - pl - pr)) / (readings.length - 1);
    const y = (t) => pt + ((hi - t) * (H - pt - pb)) / (hi - lo);
    const points = readings.map((r, i) => `${x(i).toFixed(1)},${y(r.temperature).toFixed(1)}`).join(' ');
    const ticks = [lo, Math.round((lo + hi) / 2), hi];
    const label = (r) => new Date(r.timestamp).toLocaleTimeString('uk-UA', { hour: '2-digit', minute: '2-digit' });
    return `<svg class="chart" viewBox="0 0 ${W} ${H}" role="img" aria-label="Динаміка температури">
      ${ticks.map((t) => `<line class="grid-line" x1="${pl}" x2="${W - pr}" y1="${y(t)}" y2="${y(t)}"/><text x="${pl - 6}" y="${y(t) + 4}" text-anchor="end">${t}°</text>`).join('')}
      <line class="target" x1="${pl}" x2="${W - pr}" y1="${y(target)}" y2="${y(target)}"/>
      <text x="${W - pr}" y="${y(target) - 5}" text-anchor="end">ціль ${target}°</text>
      <polyline class="line" points="${points}"/>
      ${readings.map((r, i) => `<circle class="dot" cx="${x(i)}" cy="${y(r.temperature)}" r="3.6"><title>${esc(fmtNum(r.temperature))} °C · ${esc(fmtDate(r.timestamp))}</title></circle>`).join('')}
      <text x="${pl}" y="${H - 5}">${label(readings[0])}</text>
      <text x="${W - pr}" y="${H - 5}" text-anchor="end">${label(readings[readings.length - 1])}</text>
    </svg>`;
  }

  async function renderDrawer() {
    const id = state.selectedRoomId;
    const drawer = $('#drawer');
    let room, devices, telemetry;
    try {
      [room, devices, telemetry] = await Promise.all([
        api('GET', `/api/rooms/${id}`),
        api('GET', '/api/devices'),
        api('GET', `/api/rooms/${id}/telemetry`)
      ]);
    } catch (e) {
      closeDrawer();
      showError(e);
      return;
    }
    devices = devices.filter((d) => d.room_id === id);
    telemetry.sort((a, b) => new Date(a.timestamp) - new Date(b.timestamp));
    const last = telemetry[telemetry.length - 1];

    drawer.innerHTML = `
      <div class="drawer__head">
        <div>
          <h2>${ROOM_ICONS[room.room_type] || '🏠'} ${esc(room.display_name)}</h2>
          <p class="muted">${esc(ROOM_TYPES[room.room_type] || room.room_type)} · поверх ${esc(room.floor)} · <span class="status status--${esc(room.status)}">${esc(STATUSES[room.status] || room.status)}</span></p>
        </div>
        <div class="drawer__actions">
          <button class="btn btn--small" data-action="room-edit">Змінити</button>
          <button class="btn btn--small btn--danger" data-action="room-delete">Видалити</button>
          <button class="btn btn--small" data-action="drawer-close" aria-label="Закрити">✕</button>
        </div>
      </div>
      <div class="drawer__body">
        <div class="panel"><div class="panel__body kv">
          <div class="meta"><b>${esc(room.target_temperature)} °C</b><span>бажана температура</span></div>
          <div class="meta"><b>${last ? esc(fmtNum(last.temperature)) + ' °C' : '—'}</b><span>остання температура</span></div>
          <div class="meta"><b>${last ? esc(fmtNum(last.humidity)) + ' %' : '—'}</b><span>вологість</span></div>
          <div class="meta"><b>${last ? esc(fmtNum(last.energy_consumption, 2)) : '—'}</b><span>кВт·год</span></div>
        </div></div>

        <div class="panel">
          <div class="panel__head"><h3>Пристрої (${devices.length})</h3><button class="btn btn--small btn--primary" data-action="device-add-here">+ Пристрій</button></div>
          ${devices.length
            ? `<div class="table-wrap"><table><thead><tr><th>Назва</th><th>Тип</th><th>Стан</th><th></th></tr></thead><tbody>${devices.map((d) => deviceRow(d, { showRoom: false })).join('')}</tbody></table></div>`
            : '<div class="panel__body muted">У кімнаті немає пристроїв.</div>'}
        </div>

        <div class="panel">
          <div class="panel__head"><h3>Температура</h3></div>
          <div class="panel__body">${chartSvg(telemetry, room.target_temperature)}</div>
        </div>

        <div class="panel">
          <div class="panel__head"><h3>Вимірювання (${telemetry.length})</h3><button class="btn btn--small btn--primary" data-action="telemetry-add">+ Вимірювання</button></div>
          ${telemetry.length
            ? `<div class="table-wrap"><table><thead><tr><th>Час</th><th class="num">°C</th><th class="num">%</th><th class="num">кВт·год</th><th></th></tr></thead><tbody>${
              [...telemetry].reverse().map((t) => `<tr>
                <td>${esc(fmtDate(t.timestamp))}</td>
                <td class="num">${esc(fmtNum(t.temperature))}</td>
                <td class="num">${esc(fmtNum(t.humidity))}</td>
                <td class="num">${esc(fmtNum(t.energy_consumption, 2))}</td>
                <td class="actions">
                  <button class="btn btn--small" data-action="telemetry-edit" data-id="${esc(t.id)}">Змінити</button>
                  <button class="btn btn--small btn--danger" data-action="telemetry-delete" data-id="${esc(t.id)}">✕</button>
                </td></tr>`).join('')
            }</tbody></table></div>`
            : '<div class="panel__body muted">Вимірювань ще немає.</div>'}
          <div class="panel__body muted">Автоматика на API: після нового вимірювання термостат вмикається, коли температура нижча за ціль більш ніж на 1 °C, вимикається за досягнення цілі, а від 50 °C оголошується тривога.</div>
        </div>
      </div>`;

    drawer.dataset.room = JSON.stringify(room);
    drawer.dataset.telemetry = JSON.stringify(telemetry);
    drawer.classList.add('is-open');
    drawer.setAttribute('aria-hidden', 'false');
    $('#overlay').hidden = false;
  }

  function openDrawer(roomId) {
    state.selectedRoomId = roomId;
    renderDrawer();
  }

  function closeDrawer() {
    state.selectedRoomId = null;
    $('#drawer').classList.remove('is-open');
    $('#drawer').setAttribute('aria-hidden', 'true');
    $('#overlay').hidden = true;
  }

  const drawerRoom = () => JSON.parse($('#drawer').dataset.room || 'null');
  const drawerTelemetry = () => JSON.parse($('#drawer').dataset.telemetry || '[]');

  // ---------- Діалог із формою ----------
  // cfg: { title, submitLabel, fields: [{name,label,type,options,value,hint}], onSubmit(values) }
  function openForm(cfg) {
    const dialog = $('#dialog');
    const form = $('#dialog-form');
    form.className = 'form';
    form.innerHTML = `
      <h2>${esc(cfg.title)}</h2>
      ${cfg.fields.map((f) => `
        <div class="field">
          <label for="f-${f.name}">${esc(f.label)}</label>
          ${f.type === 'select'
            ? `<select id="f-${f.name}" name="${f.name}">${f.options.map(([v, t]) => `<option value="${esc(v)}" ${String(v) === String(f.value) ? 'selected' : ''}>${esc(t)}</option>`).join('')}</select>`
            : `<input id="f-${f.name}" name="${f.name}" type="${f.type}" ${f.step ? `step="${f.step}"` : ''} value="${esc(f.value ?? '')}" autocomplete="off">`}
          ${f.hint ? `<small>${esc(f.hint)}</small>` : ''}
        </div>`).join('')}
      <div class="form__error" id="form-error" hidden></div>
      <div class="form__actions">
        <button type="button" class="btn" data-dialog="cancel">Скасувати</button>
        <button type="submit" class="btn btn--primary">${esc(cfg.submitLabel || 'Зберегти')}</button>
      </div>`;

    form.onsubmit = async (event) => {
      event.preventDefault();
      const values = {};
      for (const f of cfg.fields) {
        const raw = form.elements[f.name].value;
        values[f.name] = f.type === 'number' ? (raw === '' ? 0 : Number(raw)) : raw;
      }
      const errorBox = $('#form-error');
      errorBox.hidden = true;
      try {
        await cfg.onSubmit(values);
        dialog.close();
      } catch (e) {
        if (e instanceof ApiError) {
          // Помилки валідації (400), конфлікти (409) тощо показуємо в самій формі — користувач одразу виправляє дані.
          errorBox.innerHTML = `<b>${esc(`${e.status} ${e.title}`)}</b>${e.lines().length ? `<ul>${e.lines().map((l) => `<li>${esc(l)}</li>`).join('')}</ul>` : ''}`;
          errorBox.hidden = false;
        } else {
          dialog.close();
          showError(e);
        }
      }
    };
    dialog.showModal();
    const first = form.querySelector('input, select');
    if (first) first.focus();
  }

  const roomTypeOptions = () => Object.entries(ROOM_TYPES);
  const deviceTypeOptions = () => Object.entries(DEVICE_TYPES);

  // ---------- Дії: кімнати ----------
  function roomForm(room) {
    const editing = Boolean(room);
    openForm({
      title: editing ? 'Змінити кімнату' : 'Нова кімната',
      fields: [
        { name: 'name', label: 'Назва', type: 'text', value: room?.display_name ?? '', hint: '3–100 символів, унікальна в межах будинку' },
        { name: 'type', label: 'Тип', type: 'select', options: roomTypeOptions(), value: room?.room_type ?? 'LivingRoom' },
        { name: 'floor', label: 'Поверх', type: 'number', value: room?.floor ?? 1, hint: '0–10' },
        { name: 'target_temperature', label: 'Бажана температура, °C', type: 'number', value: room?.target_temperature ?? 22, hint: '10–30' }
      ],
      onSubmit: async (v) => {
        if (editing) await api('PUT', `/api/rooms/${room.room_id}`, v);
        else await api('POST', '/api/rooms', v);
        toast('ok', editing ? 'Кімнату оновлено' : 'Кімнату створено', [v.name]);
        await refreshAll();
      }
    });
  }

  async function deleteRoom(room) {
    if (!confirm(`Видалити кімнату «${room.display_name}» разом із пристроями та вимірюваннями?`)) return;
    try {
      await api('DELETE', `/api/rooms/${room.room_id}`);
      toast('ok', 'Кімнату видалено', [room.display_name]);
      closeDrawer();
      await refreshAll();
    } catch (e) { showError(e); }
  }

  // ---------- Дії: пристрої ----------
  function deviceForm(device, fixedRoomId) {
    const editing = Boolean(device);
    const fields = [
      { name: 'name', label: 'Назва', type: 'text', value: device?.name ?? '', hint: '2–100 символів' },
      { name: 'type', label: 'Тип', type: 'select', options: deviceTypeOptions(), value: device?.device_type ?? 'Light' }
    ];
    if (!editing) {
      fields.push({
        name: 'room_id', label: 'Кімната', type: 'select',
        options: state.rooms.map((r) => [r.room_id, r.display_name]),
        value: fixedRoomId ?? state.rooms[0]?.room_id
      });
    }
    openForm({
      title: editing ? 'Змінити пристрій' : 'Новий пристрій',
      fields,
      onSubmit: async (v) => {
        if (editing) await api('PUT', `/api/devices/${device.device_id}`, { name: v.name, type: v.type });
        else await api('POST', '/api/devices', v);
        toast('ok', editing ? 'Пристрій оновлено' : 'Пристрій додано', [v.name]);
        await refreshAll();
      }
    });
  }

  async function toggleDevice(id, enabled) {
    try {
      await api('PATCH', `/api/devices/${id}/status`, { is_active: enabled });
      toast('info', enabled ? 'Пристрій увімкнено' : 'Пристрій вимкнено');
    } catch (e) { showError(e); }
    await refreshAll();
  }

  async function deleteDevice(id) {
    const device = [...state.devices].find((d) => d.device_id === id);
    if (!confirm(`Видалити пристрій «${device ? device.name : id}»?`)) return;
    try {
      await api('DELETE', `/api/devices/${id}`);
      toast('ok', 'Пристрій видалено');
    } catch (e) { showError(e); }
    await refreshAll();
  }

  // ---------- Дії: вимірювання ----------
  function telemetryForm(record) {
    const editing = Boolean(record);
    const room = drawerRoom();
    openForm({
      title: editing ? 'Змінити вимірювання' : `Нове вимірювання — ${room.display_name}`,
      fields: [
        { name: 'temperature', label: 'Температура, °C', type: 'number', step: '0.1', value: record?.temperature ?? room.target_temperature, hint: '-50…60' },
        { name: 'humidity', label: 'Вологість, %', type: 'number', step: '0.1', value: record?.humidity ?? 50, hint: '0…100' },
        { name: 'energy_consumption', label: 'Споживання, кВт·год', type: 'number', step: '0.01', value: record?.energy_consumption ?? 0.5, hint: '0…100' }
      ],
      onSubmit: async (v) => {
        if (editing) {
          await api('PUT', `/api/rooms/${room.room_id}/telemetry/${record.id}`, v);
          toast('ok', 'Вимірювання оновлено');
          await refreshAll();
          return;
        }
        // Автоматика виконується на сервері під час POST: порівнюємо стан до й після, щоб показати її результат.
        const before = await api('GET', '/api/devices');
        await api('POST', `/api/rooms/${room.room_id}/telemetry`, v);
        const [after, roomAfter] = await Promise.all([api('GET', '/api/devices'), api('GET', `/api/rooms/${room.room_id}`)]);

        const changes = [];
        for (const d of after.filter((x) => x.room_id === room.room_id)) {
          const prev = before.find((x) => x.device_id === d.device_id);
          if (prev && prev.is_enabled !== d.is_enabled) {
            changes.push(`«${d.name}» ${d.is_enabled ? 'УВІМКНЕНО' : 'ВИМКНЕНО'}`);
          }
        }
        if (roomAfter.status !== room.status) changes.push(`Статус кімнати: ${STATUSES[room.status]} → ${STATUSES[roomAfter.status]}`);

        toast('ok', 'Вимірювання збережено');
        if (changes.length) toast('info', 'Спрацювала автоматика клімату', changes);
        await refreshAll();
      }
    });
  }

  async function deleteTelemetry(id) {
    const room = drawerRoom();
    if (!confirm('Видалити це вимірювання?')) return;
    try {
      await api('DELETE', `/api/rooms/${room.room_id}/telemetry/${id}`);
      toast('ok', 'Вимірювання видалено');
    } catch (e) { showError(e); }
    await refreshAll();
  }

  // ---------- Події ----------
  document.addEventListener('click', (event) => {
    const tab = event.target.closest('.tab');
    if (tab) {
      document.querySelectorAll('.tab').forEach((t) => t.classList.toggle('is-active', t === tab));
      document.querySelectorAll('.tabpanel').forEach((p) => { p.hidden = p.id !== `tab-${tab.dataset.tab}`; });
      if (tab.dataset.tab === 'log') renderLog();
      return;
    }

    if (event.target.matches('[data-dialog="cancel"]')) { $('#dialog').close(); return; }
    if (event.target.id === 'overlay') { closeDrawer(); return; }

    const actionEl = event.target.closest('[data-action]');
    if (actionEl && actionEl.type !== 'checkbox') {
      const id = actionEl.dataset.id;
      switch (actionEl.dataset.action) {
        case 'room-add': roomForm(null); break;
        case 'room-edit': roomForm(drawerRoom()); break;
        case 'room-delete': deleteRoom(drawerRoom()); break;
        case 'drawer-close': closeDrawer(); break;
        case 'device-add': deviceForm(null); break;
        case 'device-add-here': deviceForm(null, state.selectedRoomId); break;
        case 'device-edit': api('GET', `/api/devices/${id}`).then((d) => deviceForm(d)).catch(showError); break;
        case 'device-delete': deleteDevice(id); break;
        case 'telemetry-add': telemetryForm(null); break;
        case 'telemetry-edit': telemetryForm(drawerTelemetry().find((t) => String(t.id) === id)); break;
        case 'telemetry-delete': deleteTelemetry(id); break;
        case 'log-clear': state.log = []; $('#log-count').textContent = '0'; renderLog(); break;
        default: break;
      }
      return;
    }

    const card = event.target.closest('.card[data-room]');
    if (card) openDrawer(card.dataset.room);
  });

  document.addEventListener('change', (event) => {
    const el = event.target;
    if (el.matches('[data-action="device-toggle"]')) toggleDevice(el.dataset.id, el.checked);
  });

  document.addEventListener('keydown', (event) => {
    if (event.key === 'Escape' && state.selectedRoomId && !$('#dialog').open) closeDrawer();
    const card = event.target.closest && event.target.closest('.card[data-room]');
    if (card && (event.key === 'Enter' || event.key === ' ')) { event.preventDefault(); openDrawer(card.dataset.room); }
  });

  $('#room-search').addEventListener('input', debounce((e) => {
    state.roomQuery = e.target.value;
    loadRooms().catch(showError);
  }));
  $('#device-search').addEventListener('input', debounce((e) => {
    state.deviceQuery = e.target.value;
    loadDevices().catch(showError);
  }));

  // ---------- Старт ----------
  checkHealth();
  setInterval(checkHealth, 30000);
  refreshAll();
})();