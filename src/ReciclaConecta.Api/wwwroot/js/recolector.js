// HU09-HU11: el recolector ve solicitudes cercanas, las acepta y registra el recojo.
(async () => {
  const s = exigirRol('Recolector');
  if (!s) return;
  const msg = document.getElementById('msg');
  if (!s.verificado)
    document.getElementById('aviso').textContent = 'Tu cuenta aún no está verificada: puedes ver las solicitudes, pero solo podrás aceptarlas cuando el administrador te verifique.';

  const mapa = crearMapa('mapa', LIMA, 12);
  const capa = L.layerGroup().addTo(mapa);
  const selMaterial = document.getElementById('material');
  const selRadio = document.getElementById('radio');
  let miUbicacion = null;

  await cargarMateriales(selMaterial);

  async function accion(fn, ok) {
    try { await fn(); mensaje(msg, ok, 'ok'); await cargar(); } catch (e) { mensaje(msg, e.message); }
  }

  async function cargar() {
    const p = new URLSearchParams();
    if (selMaterial.value) p.set('material', selMaterial.value);
    if (miUbicacion) { p.set('lat', miUbicacion[0]); p.set('lng', miUbicacion[1]); p.set('radioKm', selRadio.value); }
    const [disp, mios] = await Promise.all([api('/api/solicitudes/disponibles?' + p), api('/api/solicitudes/mias')]);

    capa.clearLayers();
    const cont = document.getElementById('disponibles');
    cont.replaceChildren(...(disp.length ? [] : [el('p', { class: 'sub' }, 'No hay solicitudes pendientes con esos filtros.')]));
    for (const d of disp) {
      L.circleMarker([d.lat, d.lng], { radius: 9, color: '#b26b00', fillColor: '#f0a020', fillOpacity: .9 })
        .bindPopup(el('div', {}, el('strong', {}, d.materialNombre + ' · ' + d.cantidadEstimadaKg + ' kg'), el('br'), d.direccion + ', ' + d.distrito))
        .addTo(capa);
      cont.append(el('div', { class: 'tarjeta' },
        el('h3', {}, d.materialNombre + ' · ' + d.cantidadEstimadaKg + ' kg'),
        el('div', { class: 'meta' }, d.direccion + ', ' + d.distrito + (d.distanciaKm != null ? ' · a ' + d.distanciaKm + ' km' : '')),
        d.referencia ? el('div', { class: 'meta' }, 'Ref.: ' + d.referencia) : null,
        el('div', { class: 'meta' }, 'Publicada: ' + fecha(d.fechaCreacion) + ' por ' + d.ciudadano),
        el('div', { class: 'acciones' }, el('button', {
          class: 'btn chico', type: 'button',
          onclick: () => accion(() => api('/api/solicitudes/' + d.id + '/aceptar', { method: 'POST' }), 'Recojo aceptado. ¡Pasa a recogerlo!')
        }, 'Aceptar recojo'))));
    }

    const contMios = document.getElementById('mios');
    contMios.replaceChildren(...(mios.length ? [] : [el('p', { class: 'sub' }, 'Todavía no aceptaste recojos.')]));
    for (const m of mios) {
      const acciones = el('div', { class: 'acciones' });
      if (m.estado === 'Aceptada') {
        const kg = el('input', { type: 'number', min: '0.1', max: '1000', step: '0.1', value: m.cantidadEstimadaKg, 'aria-label': 'Kg reales' });
        acciones.append(kg, el('button', {
          class: 'btn chico', type: 'button',
          onclick: () => accion(() => api('/api/solicitudes/' + m.id + '/recoger', { method: 'POST', body: { kgReales: parseFloat(kg.value) } }), 'Recojo registrado.')
        }, 'Marcar recogido'));
      }
      contMios.append(el('div', { class: 'tarjeta' },
        el('h3', {}, m.materialNombre + ' ', el('span', { class: 'estado ' + m.estado }, m.estado)),
        el('div', { class: 'meta' }, m.direccion + ', ' + m.distrito),
        el('div', { class: 'meta' }, 'Ciudadano: ' + m.ciudadano + (m.kgReales ? ' · ' + m.kgReales + ' kg recogidos' : ' · ' + m.cantidadEstimadaKg + ' kg estimados')),
        m.calificacion ? el('div', { class: 'meta' }, 'Calificación: ' + '★'.repeat(m.calificacion)) : null,
        acciones));
    }
  }

  selMaterial.addEventListener('change', cargar);
  selRadio.addEventListener('change', cargar);
  document.getElementById('cerca').addEventListener('click', async () => {
    miUbicacion = (await ubicacionActual()) || LIMA;
    L.circleMarker(miUbicacion, { radius: 7, color: '#1d5fa8', fillColor: '#1d5fa8', fillOpacity: 1 }).bindPopup('Tú').addTo(mapa);
    mapa.setView(miUbicacion, 13);
    await cargar();
  });
  await cargar();
})().catch(e => alert(e.message));
