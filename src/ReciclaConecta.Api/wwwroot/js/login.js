// HU02: inicio de sesión con JWT.
const DESTINO = { Ciudadano: '/ciudadano.html', Recolector: '/recolector.html', Admin: '/admin.html' };

document.getElementById('form').addEventListener('submit', async ev => {
  ev.preventDefault();
  const msg = document.getElementById('msg');
  try {
    const d = await api('/api/auth/login', {
      method: 'POST',
      body: { email: document.getElementById('email').value, password: document.getElementById('password').value }
    });
    Sesion.guardar(d);
    const volver = new URLSearchParams(location.search).get('volver');
    // Solo se permite volver a rutas internas (evita redirecciones abiertas).
    location.href = volver && /^\/[a-z]+\.html$/.test(volver) ? volver : DESTINO[d.rol];
  } catch (e) {
    mensaje(msg, e.message);
  }
});
