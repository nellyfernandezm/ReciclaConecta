// HU08, HU12, HU13, HU14: el ciudadano publica, sigue, cancela y califica sus recojos.
(async () => {
  if (!exigirRol('Ciudadano')) return;
  const msg = document.getElementById('msg');
  const lista = document.getElementById('lista');
  const mapa = crearMapa('mapa', LIMA, 12);
  let ubic = null, marcador = null;

  function fijar(latlng) {
    ubic = [latlng[0], latlng[1]];
    if (marcador) marcador.remove();
    marcador = L.circleMarker(ubic, { radius: 9, color: '#13603a', fillColor: '#1b7f4b', fillOpacity: .9 }).addTo(mapa);
    document.getElementById('coords').textContent = 'Ubicación: ' + ubic[0].toFixed(5) + ', ' + ubic[1].toFixed(5);
  }
  mapa.on('click', e => fijar([e.latlng.lat, e.latlng.lng]));
  ubicacionActual().then(u => { if (u && !ubic) { fijar(u); mapa.setView(u, 15); } });

  await cargarMateriales(document.getElementById('material'), false);

  document.getElementById('form').addEventListener('submit', async ev => {
    ev.preventDefault();
    if (!ubic) return mensaje(msg, 'Marca la ubicación en el mapa.');
    const v = id => document.getElementById(id).value;
    try {
      await api('/api/solicitudes', {
        method: 'POST',
        body: {
          material: v('material'), cantidadEstimadaKg: parseFloat(v('kg')), direccion: v('direccion'),
          distrito: v('distrito'), referencia: v('referencia'), lat: ubic[0], lng: ubic[1]
        }
      });
      mensaje(msg, 'Solicitud publicada. Te avisaremos cuando un recolector la acepte.', 'ok');
      ev.target.reset();
      await cargar();
    } catch (e) { mensaje(msg, e.message); }
  });

  async function accion(fn) {
    try { await fn(); await cargar(); } catch (e) { mensaje(msg, e.message); }
  }

  function estrellas(s) {
    return el('div', {}, el('span', { class: 'meta' }, 'Califica al recolector: '),
      el('span', { class: 'estrellas' }, [1, 2, 3, 4, 5].map(n => el('button', {
        type: 'button', title: n + ' estrellas',
        onclick: () => accion(() => api('/api/solicitudes/' + s.id + '/calificar', { method: 'POST', body: { calificacion: n } }))
      }, '★'))));
  }

  async function cargar() {
    const [sols, imp] = await Promise.all([api('/api/solicitudes/mias'), api('/api/impacto/mio')]);
    document.getElementById('impacto').replaceChildren(
      el('div', { class: 'kpi' }, el('div', { class: 'valor' }, imp.totalKg + ' kg'), el('div', { class: 'etiqueta' }, 'Reciclados gracias a ti')),
      el('div', { class: 'kpi' }, el('div', { class: 'valor' }, imp.solicitudesRecogidas), el('div', { class: 'etiqueta' }, 'Recojos completados')),
      el('div', { class: 'kpi' }, el('div', { class: 'valor' }, imp.porMaterial.length ? imp.porMaterial[0].material : '—'), el('div', { class: 'etiqueta' }, 'Material que más reciclas')));

    lista.replaceChildren(...(sols.length ? [] : [el('p', { class: 'sub' }, 'Aún no tienes solicitudes.')]));
    for (const s of sols) {
      const acciones = el('div', { class: 'acciones' });
      if (s.estado === 'Pendiente')
        acciones.append(el('button', {
          class: 'btn chico peligro', type: 'button',
          onclick: () => confirm('¿Cancelar esta solicitud?') && accion(() => api('/api/solicitudes/' + s.id + '/cancelar', { method: 'POST' }))
        }, 'Cancelar'));
      if (s.estado === 'Recogida' && s.calificacion == null) acciones.append(estrellas(s));
      lista.append(el('div', { class: 'tarjeta' },
        el('h3', {}, s.materialNombre + ' · ' + (s.kgReales ?? s.cantidadEstimadaKg) + ' kg ', el('span', { class: 'estado ' + s.estado }, s.estado)),
        el('div', { class: 'meta' }, s.direccion + ', ' + s.distrito),
        el('div', { class: 'meta' }, 'Creada: ' + fecha(s.fechaCreacion) + (s.recolector ? ' · Recolector: ' + s.recolector : '')),
        s.calificacion ? el('div', { class: 'meta' }, 'Tu calificación: ' + '★'.repeat(s.calificacion)) : null,
        acciones));
    }
  }
  await cargar();
})().catch(e => alert(e.message));
