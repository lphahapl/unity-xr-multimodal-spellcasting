const app = 'http://127.0.0.1:4190';
if (!new URLSearchParams(location.search).has('legacy-export')) {
  location.replace(app + '/rig/' + location.search + location.hash);
} else {
  // This page executes in the previous origin so its IndexedDB stays scoped to that Chrome profile.
  window.addEventListener('message', async (event) => {
    if (
      event.origin !== app ||
      self === parent ||
      event.source !== parent ||
      event.data?.type !== 'read-rig-library'
    )
      return;
    try {
      const q = (req) =>
        new Promise((ok, fail) => {
          req.onsuccess = () => ok(req.result);
          req.onerror = () => fail(req.error);
        });
      let snapshot = null;
      if ((await indexedDB.databases()).some((d) => d.name === 'RigWorkbenchLibrary')) {
        const db = await q(indexedDB.open('RigWorkbenchLibrary'));
        try {
          const keys = ['collections', 'versions', 'notes', 'files'],
            tx = db.transaction(keys);
          const [collections, versions, notes, files, ids] = await Promise.all([
            ...keys.map((k) => q(tx.objectStore(k).getAll())),
            q(tx.objectStore('files').getAllKeys()),
          ]);
          snapshot = {
            collections,
            versions,
            notes,
            files: new Map(ids.map((id, i) => [id, files[i]])),
          };
        } finally {
          db.close();
        }
      }
      parent.postMessage({ token: event.data.token, snapshot }, app);
    } catch (e) {
      parent.postMessage({ token: event.data.token, error: e.message }, app);
    }
  });
}
