import { request, byId, el, link } from './data.js';
try {
  const cfg = await request('/workspace.json');
  document.title = document.querySelector('h1').textContent = cfg.name;
  for (const item of cfg.links) {
    const url = new URL(item.path, location.origin + '/');
    if (url.origin !== location.origin) continue;
    const card = link('', url);
    card.className = 'tile';
    card.append(el('h2', item.title), el('p', item.description));
    byId('links').append(card);
  }
} catch (e) {
  byId('status').textContent = e.message;
}
