// ducky.js (SPEC §11.9): one plain ES module, imported lazily by JsBridge. Every argument and result is a string, number
// or boolean, except a DotNetObjectReference (INV-23). Held to 100% block coverage by the E2E gate (§17.6).

// Can't be an envelope (a JSON object): storageGet returns it for a value above the inline budget, then read by stream.
const tooLarge = '\u0000ducky:too-large';
const encoder = new TextEncoder();

// Registrations per store id, shared by every store of the document (§11.7). The listeners and the relay to the other
// ids come with cross-tab (M7-01).
const registrations = new Map();

const storage = area => area === 'local' ? localStorage : sessionStorage;

// What crosses the Blazor Server hub: UTF-8 bytes of the JSON-encoded value, never UTF-16 code units (§11.9).
const utf8Length = value => encoder.encode(JSON.stringify(value)).length;

export function storageGet(area, key, maxInlineBytes) {
    const value = storage(area).getItem(key);
    return value !== null && utf8Length(value) > maxInlineBytes ? tooLarge : value;
}

// Never null nor an empty array, which Blazor's stream reference rejects (spike S-5): one NUL byte when nothing is kept,
// which .NET reads (Length <= 1) as not found.
export function storageGetStream(area, key) {
    const value = storage(area).getItem(key);
    return value ? encoder.encode(value) : new Uint8Array(1);
}

// Both return false for an id that is not registered, so .NET repairs a pruned registration (§11.7).
export function storageSet(area, key, value, id) {
    storage(area).setItem(key, value);
    return registrations.has(id);
}

export function storageRemove(area, key, id) {
    storage(area).removeItem(key);
    return registrations.has(id);
}

export function watchStorage(id, ref, prefix, crossTab, pruneAfterMs) {
    registrations.set(id, { ref, prefix, crossTab, pruneAfterMs });
}

export function unwatchStorage(id) {
    registrations.delete(id);
}
