export function addReduxStorageListener(dotnetRef, storageKey) {
  window.addEventListener("storage", function (e) {
    if (e.key === storageKey) {
      // Notify .NET with the changed key so it can filter appropriately
      dotnetRef.invokeMethodAsync("OnExternalStateChangedAsync", e.key);
    }
  });
}
