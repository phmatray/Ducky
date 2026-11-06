// ducky.js (SPEC §11.9): one plain ES module, imported lazily by JsBridge. Every argument and result is a string, number
// or boolean, except a DotNetObjectReference (INV-23). Held to 100% block coverage by the E2E gate (§17.6).

// Can't be an envelope (a JSON object): storageGet returns it for a value above the inline budget, then read by stream.
const tooLarge = '\u0000ducky:too-large';
const encoder = new TextEncoder();

// Registrations per store id, shared by every store of the document (§11.7).
const registrations = new Map();

// Redux DevTools connections per store id (§11.8).
const connections = new Map();

// Delivery-failure backoff (§11.7): 1 s doubling to 30 s. The prune cap comes from .NET (pruneAfterMs). A delivery still
// unsettled after the first step counts as failed: Blazor Server never settles one sent while the circuit is disconnected.
const firstRetryMs = 1000;
const maxRetryMs = 30000;
// The fast-path prune: what an invocation on a disposed DotNetObjectReference rejects with, on WebAssembly or with
// DetailedErrors. Blazor Server otherwise sends a generic text, shared with any handler exception, so never matched here:
// the prune cap bounds Server (§11.7).
const untracked = /no tracked object/;

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

// Both write, relay to the document's other ids and return false for an id that is not registered, so .NET repairs a
// pruned registration (§11.7).
export function storageSet(area, key, value, id) {
    storage(area).setItem(key, value);
    return relay(area, key, value, id);
}

export function storageRemove(area, key, id) {
    storage(area).removeItem(key);
    return relay(area, key, null, id);
}

// Idempotent per id. pruneAfterMs and maxInlineBytes (BlazorOptions.InlinePayloadBytes) come from .NET; a missing budget
// (a five-argument caller) throws rather than silently sending every value by stream. Both return true: .NET's typed
// InvokeAsync faults on an undefined result (§11.9).
export function watchStorage(id, ref, prefix, crossTab, pruneAfterMs, maxInlineBytes) {
    if (!Number.isInteger(maxInlineBytes) || maxInlineBytes < 0) {
        throw new TypeError('watchStorage: maxInlineBytes');
    }
    if (registrations.has(id)) {
        return true;
    }
    const registration = { ref, prefix, pruneAfterMs, maxInlineBytes, listener: null, sent: 0, needsResync: new Map(), failingSince: null, retryMs: firstRetryMs, timer: null };
    if (crossTab) {
        registration.listener = e => {
            if (e.storageArea === localStorage && (e.key === null || e.key.startsWith(`${prefix}:`))) {
                notify(id, registration, 'tab', 'local', e.key, e.newValue);
            }
        };
        addEventListener('storage', registration.listener);
    }
    registrations.set(id, registration);
    return true;
}

export function unwatchStorage(id) {
    const registration = registrations.get(id);
    if (registration) {
        removeEventListener('storage', registration.listener);
        clearTimeout(registration.timer);
        registrations.delete(id);
    }
    return true;
}

// storage events never fire in the writing document: every other id whose prefix the key carries hears it as "doc".
function relay(area, key, value, writer) {
    for (const [id, registration] of registrations) {
        if (id !== writer && key.startsWith(`${registration.prefix}:`)) {
            notify(id, registration, 'doc', area, key, value);
        }
    }
    return registrations.has(writer);
}

// A value above the id's inline budget crosses as (null, tooLarge) and .NET pulls it by stream. A re-read (from resync)
// carries reread; an event never does, a forwarded clear() (key null) included.
// A settlement for a registration that is no longer the id's (unwatched or pruned meanwhile) is ignored. A delivery
// unsettled after firstRetryMs fails; its late success still recovers. Each delivery has a seq, so a successful re-read
// settles only the failures of deliveries sent before it. A late rejection of a delivery already counted as failed adds
// no failure (its pair may have been settled meanwhile) but still prunes on the untracked-object error.
// Each delivery names its id: a resumed circuit's stale registration can hold the DotNetObject id of the new circuit's
// store, which then rejects it, so the stale id still fails and is pruned at its cap (INV-18).
// Returns the delivery's seq.
function notify(id, registration, source, area, key, value, reread = false) {
    const large = value !== null && utf8Length(value) > registration.maxInlineBytes;
    const seq = ++registration.sent;
    const current = () => registrations.get(id) === registration;
    let counted = false;
    const hung = setTimeout(() => {
        counted = true;
        if (current()) {
            fail(id, registration, seq, source, area, reread, 'unsettled');
        }
    }, firstRetryMs);
    registration.ref.invokeMethodAsync('OnStorageChanged', source, area, key, large ? null : value, large, id).then(
        () => {
            clearTimeout(hung);
            if (current()) {
                if (key === null) {
                    settleReRead(registration, seq, source, area);
                }
                recover(id, registration);
            }
        },
        error => {
            clearTimeout(hung);
            if (current()) {
                fail(id, registration, seq, source, area, reread || counted, error);
            }
        });
    return seq;
}

// A pair stays owed until a re-read sent after its newest failed event succeeds, however late.
function settleReRead(registration, seq, source, area) {
    const pair = `${source} ${area}`;
    const owed = registration.needsResync.get(pair);
    if (owed && owed.failed < seq) {
        registration.needsResync.delete(pair);
    } else if (owed?.rereading === seq) {
        owed.rereading = 0;
    }
}

// The first success after failures re-reads every owed (source, area) pair, once (§11.7).
function recover(id, registration) {
    registration.failingSince = null;
    registration.retryMs = firstRetryMs;
    clearTimeout(registration.timer);
    registration.timer = null;
    resync(id, registration);
}

// rereading is the seq of the pair's re-read that is out and not yet failed (0: none), so only that one's settlement
// frees the pair for another: a stale re-read settling late sends no second one.
function resync(id, registration) {
    for (const owed of registration.needsResync.values()) {
        if (!owed.rereading) {
            owed.rereading = notify(id, registration, owed.source, owed.area, null, null, true);
        }
    }
}

// A lost event makes its pair owed. A lost re-read only keeps retrying a pair still owed: one whose pair is settled was
// redundant, so a slow link can't feed itself.
function fail(id, registration, seq, source, area, reread, error) {
    const pair = `${source} ${area}`;
    let owed = registration.needsResync.get(pair);
    if (!reread) {
        owed ??= { source, area, failed: 0, rereading: 0 };
        owed.failed = Math.max(owed.failed, seq);
        registration.needsResync.set(pair, owed);
    } else if (owed?.rereading === seq) {
        owed.rereading = 0;
    }
    if (untracked.test(String(error))) {
        prune(id);
    } else if (owed) {
        registration.failingSince ??= Date.now();
        if (Date.now() - registration.failingSince > registration.pruneAfterMs) {
            prune(id);
        } else if (registration.timer === null) {
            registration.timer = setTimeout(() => {
                registration.timer = null;
                resync(id, registration);
            }, registration.retryMs);
            registration.retryMs = Math.min(registration.retryMs * 2, maxRetryMs);
        }
    }
}

// The module's own prune drops the whole id: its storage registration and its DevTools connection (§11.7).
function prune(id) {
    unwatchStorage(id);
    devtoolsDisconnect(id);
}

// Redux DevTools (§11.8). false without the extension. devtoolsConnect has no budget argument, so the inline budget
// (BlazorOptions.InlinePayloadBytes) rides in configJson as maxInlineBytes, checked as watchStorage checks it, and is
// removed before the options reach the extension.
export function devtoolsConnect(id, ref, configJson, pruneAfterMs) {
    const { maxInlineBytes, ...options } = JSON.parse(configJson);
    if (!Number.isInteger(maxInlineBytes) || maxInlineBytes < 0) {
        throw new TypeError('devtoolsConnect: maxInlineBytes');
    }
    const extension = globalThis.__REDUX_DEVTOOLS_EXTENSION__;
    if (!extension) {
        return false;
    }
    // ponytail: one connect per id (a store connects once); a second connect of a live id leaks the first's subscription.
    const connection = { extension: extension.connect(options), ref, pruneAfterMs, maxInlineBytes, seq: 0, kept: new Map(), failingSince: null };
    connection.extension.subscribe(message => forward(id, connection, message));
    connections.set(id, connection);
    return true;
}

export function devtoolsInit(id, stateJson) {
    connections.get(id)?.extension.init(JSON.parse(stateJson));
}

// false for an unknown id (never connected, disconnected or pruned): .NET then goes inert (§11.7).
export function devtoolsSend(id, actionJson, stateJson) {
    const connection = connections.get(id);
    connection?.extension.send(JSON.parse(actionJson), JSON.parse(stateJson));
    return connection !== undefined;
}

export function devtoolsDisconnect(id) {
    connections.get(id)?.extension.unsubscribe();
    connections.delete(id);
}

// The pull export of an inbound message above the budget: the kept text, once. Never null nor an empty array (spike
// S-5): one NUL byte when it was already taken or pruned.
export function devtoolsTakeMessage(id, seq) {
    const kept = connections.get(id)?.kept ?? new Map();
    const text = kept.get(seq);
    kept.delete(seq);
    return text ? encoder.encode(text) : new Uint8Array(1);
}

// A jump crosses inline as {type, payload: {type, actionId|index}}, without the extension's state, whatever the budget:
// .NET holds typed history (INV-28), so a jump is always small. Any other message above the budget is kept under its seq
// for devtoolsTakeMessage.
const jump = /^JUMP_TO_(STATE|ACTION)$/;

function forward(id, connection, message) {
    const p = message.payload;
    const isJump = jump.test(p?.type);
    const text = JSON.stringify(isJump ? { type: message.type, payload: { type: p.type, actionId: p.actionId, index: p.index } } : message);
    const seq = ++connection.seq;
    const large = !isJump && utf8Length(text) > connection.maxInlineBytes;
    if (large) {
        connection.kept.set(seq, text);
    }
    const hung = setTimeout(() => devtoolsFailed(id, connection, 'unsettled'), firstRetryMs);
    connection.ref.invokeMethodAsync('OnMessage', large ? null : text, seq, large).then(
        () => {
            clearTimeout(hung);
            connection.failingSince = null;
        },
        error => {
            clearTimeout(hung);
            connection.kept.delete(seq);
            devtoolsFailed(id, connection, error);
        });
}

// Lost messages are not replayed (dev-only); failures only count towards the cap (§11.7). With no retry to fail again,
// the first failure since the last success starts one timer: the connection is dropped if no success came pruneAfterMs
// later, so a dead circuit whose extension stays quiet loses it too. Only the connection: the id's storage registration
// has its own cap, and its deliveries may be succeeding. The untracked-object error still prunes the whole id. A
// settlement for a connection that is no longer the id's is ignored.
// ponytail: a connection that failed once and then sees no inbound message for pruneAfterMs is dropped even if its
// circuit came back (DevTools then goes inert); upgrade path: let devtoolsSend count as a success.
function devtoolsFailed(id, connection, error) {
    if (connections.get(id) !== connection) {
        return;
    }
    if (untracked.test(String(error))) {
        prune(id);
    } else if (connection.failingSince === null) {
        const since = Date.now();
        connection.failingSince = since;
        setTimeout(() => {
            if (connections.get(id) === connection && connection.failingSince === since) {
                devtoolsDisconnect(id);
            }
        }, connection.pruneAfterMs + 1);
    }
}
