export function addReduxStorageListener(dotnetRef, storageKey) {
  window.addEventListener("storage", function (e) {
    if (e.key === storageKey) {
      // Notify .NET only if the key matches
      dotnetRef.invokeMethodAsync("OnExternalStateChangedAsync");
    }
  });
}
