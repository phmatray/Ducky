# S-4: CsCheck `SampleParallel` within the CI budget (week-1 prototype, SPEC §23)

Question: does a CsCheck `SampleParallel` linearizability property at 50 repetitions (`DUCKY_REPEAT`) stay under
3 minutes, and with which operation counts? Answer: `docs/spec/spikes.md` (S-4).

- `Dispatcher.Tests/DispatcherStub.cs`: the dispatcher stub, a lock-protected queue with one drainer (the thread that
  finds no drainer drains outside the lock until the queue is empty); `DispatchAsync` completes after its action is
  reduced and committed (`RunContinuationsAsynchronously`).
- `Dispatcher.Tests/LinearizabilityTests.cs`: `Linearizability_DispatchVsModel` in the §17.3 shape (`[Theory]` over
  `DUCKY_REPEAT`, default 50, 10 s `WaitAsync`): awaited `DispatchAsync` and `ReadState` against a sequential reducer
  model. The final state and every read must match one linearization.

Run (the counts default to the S-4 answer; `S4_SEQ` and `S4_PAR` override them, `iter` is CsCheck's and `CsCheck_Iter`
sets it, which is how the `iter` 1000 rows were measured; `CsCheck_Threads` sets the CsCheck threads, default the logical
CPU count; `S4_STUB` picks the stub: `correct` (default), `stranded` (the INV-03 bug of the detection column: the drainer
clears its flag after releasing the lock) or `naive` (no lock, no drainer), and both buggy stubs must fail):

    cd spikes/S-4-dispatcher/Dispatcher.Tests
    dotnet build -c Release
    time dotnet test --project . -c Release --no-build

Outside every solution and gate (SPEC §18); `bin/`, `obj/` and `TestResults/` are scratch.
