// HU01: registro de ciudadanos y recolectores.
const rol = document.getElementById('rol');
rol.addEventListener('change', () => { document.getElementById('notaRecolector').hidden = rol.value !== 'Recolector'; });

document.getElementById('form').addEventListener('submit', async ev => {
  ev.preventDefault();
  const msg = document.getElementById('msg');
  const v = id => document.getElementById(id).value;
  try {
    await api('/api/auth/registro', {
      method: 'POST',
      body: { nombre: v('nombre'), email: v('email'), password: v('password'), rol: v('rol'), distrito: v('distrito') }
    });
    const d = await api('/api/auth/login', { method: 'POST', body: { email: v('email'), password: v('password') } });
    Sesion.guardar(d);
    location.href = d.rol === 'Recolector' ? '/recolector.html' : '/ciudadano.html';
  } catch (e) {
    mensaje(msg, e.message);
  }
});
