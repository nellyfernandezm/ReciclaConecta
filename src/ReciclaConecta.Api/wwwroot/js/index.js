// HU04-HU06: mapa público de puntos de acopio con filtros y cercanía.
(async () => {
  const mapa = crearMapa('mapa');
  const capa = L.layerGroup().addTo(mapa);
  const selMaterial = document.getElementById('material');
  const selAbierto = document.getElementById('abierto');
  const lista = document.getElementById('lista');
  const estadoUbic = document.getElementById('estadoUbicacion');
  let miUbicacion = null, marcadorYo = null;
  const marcadores = {};

  await cargarMateriales(selMaterial);

  async function cargar() {
    const p = new URLSearchParams();
    if (selMaterial.value) p.set('material', selMaterial.value);
    if (selAbierto.value) p.set('abiertoAhora', 'true');
    if (miUbicacion) { p.set('lat', miUbicacion[0]); p.set('lng', miUbicacion[1]); }
    const puntos = await api('/api/puntos?' + p.toString());

    capa.clearLayers();
    lista.replaceChildren();
    document.getElementById('titLista').textContent = puntos.length + ' puntos de acopio';
    if (!puntos.length) lista.append(el('p', { class: 'sub' }, 'No hay puntos con esos filtros.'));

    for (const pt of puntos) {
      const popup = el('div', {},
        el('strong', {}, pt.nombre), el('br'),
        pt.direccion + ' — ' + pt.distrito, el('br'),
        el('span', { class: 'estado ' + (pt.abiertoAhora ? 'abierto' : 'cerrado') }, pt.abiertoAhora ? 'Abierto ahora' : 'Cerrado'), el('br'),
        'Recibe: ' + pt.materialesNombre.join(', '), el('br'),
        resumenHorario(pt.horarios));
      const m = L.circleMarker([pt.lat, pt.lng], {
        radius: 9, weight: 2, color: '#13603a', fillColor: pt.abiertoAhora ? '#1b7f4b' : '#9aa5a0', fillOpacity: .9
      }).bindPopup(popup).addTo(capa);
      marcadores[pt.id] = m;

      const t = el('div', { class: 'tarjeta' },
        el('h3', {}, pt.nombre),
        el('div', { class: 'meta' }, pt.direccion + ' · ' + pt.distrito + (pt.distanciaKm != null ? ' · ' + pt.distanciaKm + ' km' : '')),
        el('div', { class: 'chips' }, pt.materialesNombre.map(n => el('span', { class: 'chip' }, n))),
        el('div', { class: 'meta' }, el('span', { class: 'estado ' + (pt.abiertoAhora ? 'abierto' : 'cerrado') }, pt.abiertoAhora ? 'Abierto ahora' : 'Cerrado'), ' ', resumenHorario(pt.horarios)),
        el('div', { class: 'acciones' }, el('button', {
          class: 'btn chico sec', type: 'button',
          onclick: () => { mapa.setView([pt.lat, pt.lng], 15); m.openPopup(); }
        }, 'Ver en el mapa')));
      lista.append(t);
    }
  }

  selMaterial.addEventListener('change', cargar);
  selAbierto.addEventListener('change', cargar);
  document.getElementById('cerca').addEventListener('click', async () => {
    estadoUbic.textContent = 'Buscando tu ubicación…';
    miUbicacion = await ubicacionActual();
    if (!miUbicacion) { miUbicacion = LIMA; estadoUbic.textContent = 'No se pudo obtener tu ubicación; se usa el centro de Lima.'; }
    else estadoUbic.textContent = 'Ordenado del más cercano al más lejano.';
    if (marcadorYo) marcadorYo.remove();
    marcadorYo = L.circleMarker(miUbicacion, { radius: 7, color: '#1d5fa8', fillColor: '#1d5fa8', fillOpacity: 1 }).bindPopup('Tú').addTo(mapa);
    mapa.setView(miUbicacion, 13);
    await cargar();
  });

  await cargar();
})().catch(e => alert(e.message));
