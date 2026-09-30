# ADR-0015: The init buffer is the hydration gate

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §6.3, §6.7, §6.11, §11.5; PER-02, SSR-02; matrix conflict 12; judges' grafts; review rounds 4, 5 and 6

## Context

Correctness-first put a hydration gate in the dispatcher but left init ordering ambiguous and had no overall init timeout; minimal-core used the init buffer but started init under the lock and had no bound; dx-first fired `StoreInitialized` before browser hydration on Server.

## Decision

User actions dispatched before `Ready` are buffered in the dispatcher (soft bound `InitBufferCapacity`, overflow aborts init and drops nothing). Init auto-starts on first use (including `WhenIdleAsync` and a `State` read), never under the lock, is an `Interlocked` state machine (`NotStarted → Starting → Running → Completed`), starts every middleware's `InitializeAsync` before awaiting any, and is bounded by `InitTimeout`, armed only after every middleware's synchronous part returned. An overflow before init is running is a no-op; `Start` re-checks the buffer after the synchronous parts and aborts from `Running`, so every middleware init always runs before `Ready` (review round 3: an overflow between flagging init and starting it used to skip hydration entirely). Cancelling `_initCts` on abort runs every callback even if one throws. No middleware init starts after `Ready`, but the asynchronous part of an aborted init may outlive it, so hooks may run before and concurrently with `InitializeAsync` (documented). `DisposeAsync` retires the coordinator (the timer never aborts a disposed store) and awaits the started middleware inits, bounded, before disposing middleware (recovered review round 2). Review round 4: `Start` wraps each `InitializeAsync` call so a synchronous throw counts as a faulted init and the next middleware still starts; it stops starting middleware once `Retire()` ran, publishes `InitTasks` for the inits it did start and completes a `_prefixDone` signal that dispose awaits, so a dispose racing the synchronous prefix still waits for every started init; an overflow abort is queued to the thread pool rather than run on the caller, so no `Dispatch`, `State` read or `Select` waits for the persistence lock (INV-05). Review round 5: the pool hop goes through an internal IVT-only seam on the store, `QueueWorkItem` (an instance delegate defaulting to `ThreadPool.UnsafeQueueUserWorkItem`), so the flag's failed CAS and `Abort`'s failed CAS are reached deterministically instead of only by a pool race; `Abort` is one CAS `Running → Completed` with a single failure arm (the stale `NotStarted`/`Starting` arm is gone, since an overflow reaches `Abort` only from `Running`); and every lifecycle log call goes through a `SafeLogger` that can't throw, so a throwing logging provider can't skip `MarkReady` or terminate the process from the timer or pool callback. At `Ready` the store appends `StoreInitialized` then the buffer, atomically; processing order across the gate is therefore not enqueue order, and `ActionContext.Id` follows enqueue order only (INV-04). Persistence status is the library slice `PersistenceState`, changed only by the drainer. Review round 6: the shared init task is the completion of `StoreInitialized`'s own pending action, so `InitializeAsync` completes once `StoreInitialized` (and every hydration restore and terminal queued before it) is committed, never at `MarkReady`, which drains only when no other thread is draining; dispose waits for each middleware's own init task only, so one hung init delays only that middleware's disposal.

## Consequences

One load-effect rule in every render mode; FIFO across the gate without `PushFront`; a hanging user middleware cannot kill the store; loading UIs `Select` the status.

## Alternatives considered

Separate gate middleware plus init buffer (rejected: two buffers); load-before-store (rejected: impossible with JS interop and SSR); unbounded buffer without timeout (rejected: liveness).
