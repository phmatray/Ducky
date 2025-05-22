let devTools = null;

export function initDevTools(storeName) {
  if (window.__REDUX_DEVTOOLS_EXTENSION__) {
    devTools = window.__REDUX_DEVTOOLS_EXTENSION__.connect({ name: storeName });
    return true;
  }
  return false;
}

export function sendToDevTools(action, state) {
  if (devTools) {
    devTools.send(action, state);
  }
}

// Optionally: listen to messages FROM DevTools (for time travel)
export function subscribeToDevTools(dotnetRef) {
  if (devTools) {
    devTools.subscribe((message) => {
      if (message.type === 'DISPATCH' && message.state) {
        dotnetRef.invokeMethodAsync('OnDevToolsState', message.state);
      }
    });
  }
}