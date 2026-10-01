// The storage exports of SPEC §11.9 that S-5 and the S-7 import probe need.
const area = name => (name === "session" ? sessionStorage : localStorage);
export function storageSet(name, key, value) { area(name).setItem(key, value); return true; }
export function storageRemove(name, key) { area(name).removeItem(key); return true; }
export function storageGetStream(name, key) { return new TextEncoder().encode(area(name).getItem(key) ?? ""); }
export function utf8Length(name, key) { return new TextEncoder().encode(area(name).getItem(key) ?? "").length; }
export function nulStream() { return new Uint8Array(1); }
export function returnNull() { return null; }
export function echo(value) { return value; }
