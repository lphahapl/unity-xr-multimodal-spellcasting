if (location.protocol === 'file:') {
  const route = document.currentScript.dataset.route || '';
  location.replace('http://127.0.0.1:4190/' + route + location.search + location.hash);
}
