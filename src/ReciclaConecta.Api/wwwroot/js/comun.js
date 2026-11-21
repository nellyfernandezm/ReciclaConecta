// Utilidades compartidas por todas las páginas.
// Seguridad: el contenido que viene del usuario se pinta con textContent (nunca innerHTML) para evitar XSS.

const Sesion = {
  guardar(d) { sessionStorage.setItem('rc_sesion', JSON.stringify(d)); },
  leer() { try { return JSON.parse(sessionStorage.getItem('rc_sesion')); } catch { return null; } },
  cerrar() { sessionStorage.removeItem('rc_sesion'); location.href = '/index.html'; }
};

// Llamada a la API con el token JWT; devuelve JSON o lanza Error con el mensaje del servidor.
async function api(url, opciones = {}) {
  const s = Sesion.leer();
  const headers = { 'Content-Type': 'application/json', ...(opciones.headers || {}) };
  if (s && s.token) headers.Authorization = 'Bearer ' + s.token;
  const r = await fetch(url, { ...opciones, headers, body: opciones.body ? JSON.stringify(opciones.body) : undefined });
  if (r.status === 401 && s) { Sesion.cerrar(); throw new Error('Tu sesión expiró.'); }
  if (r.status === 204) return null;
  const datos = await r.json().catch(() => null);
  if (!r.ok) throw new Error((datos && datos.error) || 'Error ' + r.status);
  return datos;
}

// Crea un elemento: el('div', {class: 'x'}, 'texto', otroElemento)
function el(tag, attrs = {}, ...hijos) {
  const e = document.createElement(tag);
  for (const [k, v] of Object.entries(attrs)) {
    if (k === 'class') e.className = v;
    else if (k.startsWith('on')) e.addEventListener(k.slice(2), v);
    else if (v !== undefined && v !== null && v !== false) e.setAttribute(k, v === true ? '' : v);
  }
  for (const h of hijos.flat()) {
    if (h === null || h === undefined || h === false) continue;
    e.append(h instanceof Node ? h : document.createTextNode(String(h)));
  }
  return e;
}

function mensaje(contenedor, texto, tipo = 'error') {
  contenedor.replaceChildren(texto ? el('div', { class: 'msg ' + tipo }, texto) : '');
}

const DIAS = ['Dom', 'Lun', 'Mar', 'Mié', 'Jue', 'Vie', 'Sáb'];
function resumenHorario(horarios) {
  if (!horarios || !horarios.length) return 'Sin horario registrado';
  const grupos = {};
  for (const h of horarios) {
    const k = h.horaInicio + '–' + h.horaFin;
    (grupos[k] = grupos[k] || []).push(DIAS[h.diaSemana]);
  }
  return Object.entries(grupos).map(([k, d]) => d.join(', ') + ' ' + k).join(' · ');
}

function fecha(iso) {
  if (!iso) return '';
  return new Date(iso.endsWith('Z') ? iso : iso + 'Z').toLocaleString('es-PE', { dateStyle: 'short', timeStyle: 'short' });
}

const LIMA = [-12.0864, -77.0300];
function crearMapa(id, centro = LIMA, zoom = 12) {
  const mapa = L.map(id).setView(centro, zoom);
  L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
    maxZoom: 19, attribution: '&copy; OpenStreetMap'
  }).addTo(mapa);
  return mapa;
}

function ubicacionActual() {
  return new Promise(res => {
    if (!navigator.geolocation) return res(null);
    navigator.geolocation.getCurrentPosition(p => res([p.coords.latitude, p.coords.longitude]), () => res(null), { timeout: 6000 });
  });
}

async function cargarMateriales(select, conTodos = true) {
  const mats = await api('/api/materiales');
  select.replaceChildren(...(conTodos ? [el('option', { value: '' }, 'Todos los materiales')] : []),
    ...mats.map(m => el('option', { value: m.codigo }, m.nombre)));
}

// Cabecera común según el rol.
function pintarCabecera() {
  const s = Sesion.leer();
  const nav = document.getElementById('nav');
  if (!nav) return;
  const links = [el('a', { href: '/index.html' }, 'Mapa de puntos')];
  if (!s) {
    links.push(el('a', { href: '/login.html' }, 'Ingresar'), el('a', { href: '/registro.html' }, 'Crear cuenta'));
  } else {
    if (s.rol === 'Ciudadano') links.push(el('a', { href: '/ciudadano.html' }, 'Mis recojos'));
    if (s.rol === 'Recolector') links.push(el('a', { href: '/recolector.html' }, 'Recojos disponibles'));
    if (s.rol === 'Admin') links.push(el('a', { href: '/admin.html' }, 'Panel admin'));
    links.push(el('span', { class: 'usuario' }, s.nombre + ' (' + s.rol + ')'),
      el('button', { type: 'button', onclick: Sesion.cerrar }, 'Salir'));
  }
  nav.replaceChildren(...links);
}

// Protege una página: si no hay sesión con ese rol, va al login.
function exigirRol(rol) {
  const s = Sesion.leer();
  if (!s || s.rol !== rol) { location.href = '/login.html?volver=' + encodeURIComponent(location.pathname); return null; }
  return s;
}

document.addEventListener('DOMContentLoaded', pintarCabecera);
