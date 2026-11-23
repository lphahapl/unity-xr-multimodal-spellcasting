export async function request(url, options) {
  const res = await fetch(url, { cache: 'no-store', ...options });
  const result = await res.json();
  if (!res.ok) throw Object.assign(Error(result.error || res.statusText), { status: res.status });
  return result;
}
export const list = (kind) => request('/api/' + kind);
export const get = (kind, id) => request('/api/' + kind + '/' + encodeURIComponent(id));
export function save(kind, doc, revision) {
  const condition = revision ? { 'If-Match': '"' + revision + '"' } : { 'If-None-Match': '*' };
  return request('/api/' + kind + '/' + encodeURIComponent(doc.id), {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json', ...condition },
    body: JSON.stringify(doc),
  });
}
export function remove(kind, id, revision) {
  return request('/api/' + kind + '/' + encodeURIComponent(id), {
    method: 'DELETE',
    headers: { 'If-Match': '"' + revision + '"' },
  });
}
export const fileURL = (relative) => '/assets/' + relative.split('/').map(encodeURIComponent).join('/');
export async function storeFile(file) {
  return (
    await request('/api/assets?name=' + encodeURIComponent(file.name), {
      method: 'POST',
      headers: { 'Content-Type': 'application/octet-stream' },
      body: file,
    })
  ).path;
}
export const byId = (id) => document.getElementById(id);
export function el(tag, text = '', cls = '') {
  const node = document.createElement(tag);
  if (text) node.textContent = text;
  if (cls) node.className = cls;
  return node;
}
export function link(label, href) {
  const a = el('a', label);
  a.href = href;
  return a;
}
