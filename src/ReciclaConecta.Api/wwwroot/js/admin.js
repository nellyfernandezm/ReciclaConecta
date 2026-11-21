// HU03 usuarios, HU07 puntos de acopio, HU15 indicadores.
(async () => {
  if (!exigirRol('Admin')) return;
  const DIAS_LUN_SAB = [1, 2, 3, 4, 5, 6];
  let materiales = [];

  function barras(cont, filas, clave, valor, sufijo) {
    const max = Math.max(1, ...filas.map(f => f[valor]));
    cont.replaceChildren(...(filas.length ? filas.map(f => el('div', { class: 'fila-barra' },
      el('span', {}, f[clave]),
      el('div', { class: 'barra-fondo' }, Object.assign(el('div', { class: 'barra' }), {})),
      el('strong', {}, f[valor] + sufijo))) : [el('p', { class: 'sub' }, 'Sin datos todavía.')]));
    // Ancho de cada barra (se asigna por CSSOM, compatible con la CSP).
    [...cont.querySelectorAll('.barra')].forEach((b, i) => { b.style.width = (filas[i][valor] / max * 100) + '%'; });
  }

  async function indicadores() {
    const d = await api('/api/admin/indicadores');
    const kpi = (v, t) => el('div', { class: 'kpi' }, el('div', { class: 'valor' }, v), el('div', { class: 'etiqueta' }, t));
    document.getElementById('kpis').replaceChildren(
      kpi(d.totalKg + ' kg', 'Material reciclado'),
      kpi(d.totalSolicitudes, 'Solicitudes de recojo'),
      kpi(d.recolectoresActivos, 'Recolectores verificados'),
      kpi(d.recolectoresPorVerificar, 'Recolectores por verificar'),
      kpi(d.puntosActivos, 'Puntos de acopio activos'),
      kpi(d.calificacionPromedio ? d.calificacionPromedio.toFixed(1) + ' ★' : '—', 'Calificación promedio'));
    barras(document.getElementById('porMaterial'), d.kgPorMaterial, 'material', 'kg', ' kg');
    barras(document.getElementById('porEstado'), d.solicitudesPorEstado, 'estado', 'cantidad', '');
  }

  async function usuarios() {
    const msg = document.getElementById('msgUsuarios');
    const lista = await api('/api/usuarios');
    const accion = async fn => { try { await fn(); await usuarios(); await indicadores(); } catch (e) { mensaje(msg, e.message); } };
    document.getElementById('usuarios').replaceChildren(...lista.map(u => {
      const acc = el('td', { class: 'acciones' });
      if (u.rol === 'Recolector' && !u.verificado)
        acc.append(el('button', { class: 'btn chico', type: 'button', onclick: () => accion(() => api('/api/usuarios/' + u.id + '/verificar', { method: 'PUT' })) }, 'Verificar'));
      if (u.rol !== 'Admin')
        acc.append(el('button', {
          class: 'btn chico ' + (u.activo ? 'peligro' : 'sec'), type: 'button',
          onclick: () => accion(() => api('/api/usuarios/' + u.id + '/activo', { method: 'PUT', body: { activo: !u.activo } }))
        }, u.activo ? 'Desactivar' : 'Activar'));
      const estado = !u.activo ? 'Inactivo' : (u.rol === 'Recolector' ? (u.verificado ? 'Verificado' : 'Por verificar') : 'Activo');
      return el('tr', {}, el('td', {}, u.nombre), el('td', {}, u.email), el('td', {}, u.rol), el('td', {}, u.distrito), el('td', {}, estado), acc);
    }));
  }

  // --- Puntos de acopio ---
  const f = id => document.getElementById(id);
  function limpiar() {
    f('formPunto').reset(); f('puntoId').value = ''; f('btnPunto').textContent = 'Agregar punto';
  }
  function editar(p) {
    f('puntoId').value = p.id; f('pNombre').value = p.nombre; f('pDireccion').value = p.direccion; f('pDistrito').value = p.distrito;
    f('pLat').value = p.lat; f('pLng').value = p.lng;
    if (p.horarios.length) { f('pIni').value = p.horarios[0].horaInicio; f('pFin').value = p.horarios[0].horaFin; }
    document.querySelectorAll('#pMateriales input').forEach(c => { c.checked = p.materiales.includes(c.value); });
    f('btnPunto').textContent = 'Guardar cambios';
    f('formPunto').scrollIntoView({ behavior: 'smooth' });
  }

  async function puntos() {
    const msg = f('msgPuntos');
    const lista = await api('/api/puntos');
    f('puntos').replaceChildren(...lista.map(p => el('tr', {},
      el('td', {}, p.nombre), el('td', {}, p.distrito), el('td', {}, p.materialesNombre.join(', ')), el('td', {}, resumenHorario(p.horarios)),
      el('td', { class: 'acciones' },
        el('button', { class: 'btn chico sec', type: 'button', onclick: () => editar(p) }, 'Editar'),
        el('button', {
          class: 'btn chico peligro', type: 'button',
          onclick: async () => {
            if (!confirm('¿Desactivar el punto "' + p.nombre + '"?')) return;
            try { await api('/api/puntos/' + p.id, { method: 'DELETE' }); await puntos(); await indicadores(); } catch (e) { mensaje(msg, e.message); }
          }
        }, 'Desactivar')))));
  }

  f('formPunto').addEventListener('submit', async ev => {
    ev.preventDefault();
    const msg = f('msgPuntos');
    const body = {
      nombre: f('pNombre').value, direccion: f('pDireccion').value, distrito: f('pDistrito').value,
      lat: parseFloat(f('pLat').value), lng: parseFloat(f('pLng').value), telefono: null,
      materiales: [...document.querySelectorAll('#pMateriales input:checked')].map(c => c.value),
      horarios: DIAS_LUN_SAB.map(d => ({ diaSemana: d, horaInicio: f('pIni').value, horaFin: f('pFin').value }))
    };
    if (isNaN(body.lat) || isNaN(body.lng)) return mensaje(msg, 'Ingresa latitud y longitud.');
    try {
      const id = f('puntoId').value;
      await api(id ? '/api/puntos/' + id : '/api/puntos', { method: id ? 'PUT' : 'POST', body });
      mensaje(msg, id ? 'Punto actualizado.' : 'Punto agregado.', 'ok');
      limpiar();
      await puntos(); await indicadores();
    } catch (e) { mensaje(msg, e.message); }
  });
  f('btnLimpiar').addEventListener('click', limpiar);

  materiales = await api('/api/materiales');
  f('pMateriales').replaceChildren(...materiales.map(m => el('label', { class: 'chip-check' }, el('input', { type: 'checkbox', value: m.codigo }), m.nombre)));

  await Promise.all([indicadores(), usuarios(), puntos()]);
})().catch(e => alert(e.message));
