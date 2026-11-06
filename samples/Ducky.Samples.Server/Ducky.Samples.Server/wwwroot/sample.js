// Circuit controls for the E2E specs (SPEC §17.6): drop the connection and restore it, or pause and resume the circuit.
// A document-level listener, so the buttons work while Blazor is disconnected.
document.addEventListener('click', event => {
    const action = event.target.closest('[data-circuit]')?.dataset.circuit;
    switch (action) {
        case 'drop': Blazor._internal.forceCloseConnection(); break;
        case 'reconnect': Blazor.reconnect(); break;
        case 'pause': Blazor.pauseCircuit(); break;
        case 'resume': Blazor.resumeCircuit(); break;
    }
});
