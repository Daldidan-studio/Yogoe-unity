mergeInto(LibraryManager.library, {
  // 배포 후 예전 Build/IndexedDB/캐시가 남는 경우 — 저장·캐시 비우고 강제 리로드
  YogoeClearBrowserCacheAndReload: function () {
    function bustReload() {
      try {
        var u = new URL(window.location.href);
        u.searchParams.set("_yogoe_cb", String(Date.now()));
        window.location.replace(u.toString());
      } catch (e) {
        window.location.reload();
      }
    }

    function clearStorage() {
      try { localStorage.clear(); } catch (e) {}
      try { sessionStorage.clear(); } catch (e) {}
    }

    var chain = Promise.resolve();

    if (typeof caches !== "undefined" && caches.keys) {
      chain = chain.then(function () {
        return caches.keys().then(function (keys) {
          return Promise.all(keys.map(function (k) { return caches.delete(k); }));
        });
      });
    }

    if (navigator.serviceWorker && navigator.serviceWorker.getRegistrations) {
      chain = chain.then(function () {
        return navigator.serviceWorker.getRegistrations().then(function (regs) {
          return Promise.all(regs.map(function (r) { return r.unregister(); }));
        });
      });
    }

    if (typeof indexedDB !== "undefined" && indexedDB.databases) {
      chain = chain.then(function () {
        return indexedDB.databases().then(function (dbs) {
          return Promise.all((dbs || []).map(function (db) {
            if (!db || !db.name) return Promise.resolve();
            return new Promise(function (resolve) {
              var req = indexedDB.deleteDatabase(db.name);
              req.onsuccess = req.onerror = req.onblocked = function () { resolve(); };
            });
          }));
        });
      });
    }

    // Unity IDBFS 메모리 동기화 시도(있을 때만)
    chain = chain.then(function () {
      return new Promise(function (resolve) {
        try {
          if (typeof FS !== "undefined" && FS.syncfs) {
            FS.syncfs(false, function () { resolve(); });
            return;
          }
        } catch (e) {}
        resolve();
      });
    });

    clearStorage();
    chain.then(bustReload).catch(bustReload);
  }
});
