export function getItem(storeName, key) {
    if (storeName === "sessionStorage") {
        const raw = sessionStorage.getItem(key);
        return raw ? JSON.parse(raw) : null;
    }
    if (storeName === "localStorage") {
        const raw = localStorage.getItem(key);
        return raw ? JSON.parse(raw) : null;
    }
    return null;
}

export function setItem(storeName, key, value) {
    const json = JSON.stringify(value);
    if (storeName === "sessionStorage") {
        sessionStorage.setItem(key, json);
        return;
    }
    if (storeName === "localStorage") {
        localStorage.setItem(key, json);
        return;
    }
}

export function removeItem(storeName, key) {
    if (storeName === "sessionStorage") {
        sessionStorage.removeItem(key);
        return;
    }
    if (storeName === "localStorage") {
        localStorage.removeItem(key);
        return;
    }
}

const DB_NAME = "TheBlazorState";
const STORE_NAME = "state";
const DB_VERSION = 1;

function openDb() {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open(DB_NAME, DB_VERSION);
        request.onupgradeneeded = () => {
            const db = request.result;
            if (!db.objectStoreNames.contains(STORE_NAME)) {
                db.createObjectStore(STORE_NAME);
            }
        };
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
}

export async function getItemIndexedDb(key) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction(STORE_NAME, "readonly");
        const store = tx.objectStore(STORE_NAME);
        const request = store.get(key);
        request.onsuccess = () => resolve(request.result ?? null);
        request.onerror = () => reject(request.error);
    });
}

export async function setItemIndexedDb(key, value) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction(STORE_NAME, "readwrite");
        const store = tx.objectStore(STORE_NAME);
        const request = store.put(value, key);
        request.onsuccess = () => resolve();
        request.onerror = () => reject(request.error);
    });
}

export async function removeItemIndexedDb(key) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction(STORE_NAME, "readwrite");
        const store = tx.objectStore(STORE_NAME);
        const request = store.delete(key);
        request.onsuccess = () => resolve();
        request.onerror = () => reject(request.error);
    });
}

// --- Cross-tab sync (fallback for Blazor WASM, not used in Server mode) ---
let _dotNetRef = null;

export function registerCrossTabSync(dotNetReference) {
    _dotNetRef = dotNetReference;
    window.addEventListener('storage', _onStorageEvent);
}

export function unregisterCrossTabSync() {
    window.removeEventListener('storage', _onStorageEvent);
    if (_dotNetRef) {
        _dotNetRef.dispose();
        _dotNetRef = null;
    }
}

function _onStorageEvent(e) {
    if (_dotNetRef && e.key && e.newValue !== null) {
        _dotNetRef.invokeMethodAsync('OnStorageChanged', e.key, e.newValue);
    }
}
