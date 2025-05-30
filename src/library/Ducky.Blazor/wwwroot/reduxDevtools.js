let devTools = null;
let devToolsConfig = null;

/**
 * Initializes the Redux DevTools connection with enhanced configuration.
 * @param {string} storeName - The name of the store to display in DevTools
 * @param {object} config - Configuration options for DevTools
 * @returns {boolean} True if DevTools is available and initialized, false otherwise
 */
export function initDevTools(storeName, config = {}) {
  try {
    if (typeof window !== 'undefined' && window.__REDUX_DEVTOOLS_EXTENSION__) {
      // Enhanced configuration for full feature support
      devToolsConfig = {
        name: storeName,
        realtime: config.realtime !== false,
        trace: config.trace !== false,
        traceLimit: config.traceLimit || 25,
        maxAge: config.maxAge || 50,
        actionSanitizer: config.actionSanitizer,
        stateSanitizer: config.stateSanitizer,
        features: {
          pause: true,
          lock: true,
          persist: true,
          export: true,
          import: 'custom',
          jump: true,
          skip: true,
          reorder: true,
          dispatch: true,
          test: true
        }
      };

      devTools = window.__REDUX_DEVTOOLS_EXTENSION__.connect(devToolsConfig);
      
      console.log(`Redux DevTools initialized for store: ${storeName}`);
      return true;
    } else {
      console.log('Redux DevTools extension not found');
      return false;
    }
  } catch (error) {
    console.error('Failed to initialize Redux DevTools:', error);
    return false;
  }
}

/**
 * Sends an action and state to the Redux DevTools with enhanced metadata.
 * @param {object} action - The action object with type property
 * @param {object} state - The current state
 * @param {object} options - Additional options for the action
 */
export function sendToDevTools(action, state, options = {}) {
  try {
    if (devTools && action && typeof action === 'object' && action.type) {
      // Enhanced action with metadata
      const enhancedAction = {
        ...action,
        _timestamp: options.timestamp || new Date().toISOString(),
        _source: options.source || 'Ducky',
        _duration: options.duration
      };

      devTools.send(enhancedAction, state);
    } else if (!devTools) {
      // DevTools not initialized, this is not an error in production
      return;
    } else {
      console.warn('Invalid action sent to DevTools:', action);
    }
  } catch (error) {
    console.error('Failed to send action to DevTools:', error);
  }
}

/**
 * Subscribes to Redux DevTools messages with comprehensive message handling.
 * @param {object} dotnetRef - The .NET object reference for callbacks
 */
export function subscribeToDevTools(dotnetRef) {
  try {
    if (devTools && dotnetRef) {
      devTools.subscribe((message) => {
        try {
          handleDevToolsMessage(message, dotnetRef);
        } catch (error) {
          console.error('Error handling DevTools message:', error);
        }
      });
      
      console.log('Subscribed to Redux DevTools messages');
    } else {
      console.warn('Cannot subscribe to DevTools: devTools or dotnetRef is null');
    }
  } catch (error) {
    console.error('Failed to subscribe to DevTools:', error);
  }
}

/**
 * Handles different types of DevTools messages.
 * @param {object} message - The message from DevTools
 * @param {object} dotnetRef - The .NET object reference for callbacks
 */
function handleDevToolsMessage(message, dotnetRef) {
  if (message.type !== 'DISPATCH') {
    return;
  }

  const { payload } = message;
  
  switch (payload?.type) {
    case 'JUMP_TO_ACTION':
    case 'JUMP_TO_STATE':
      handleJumpToState(message, dotnetRef);
      break;
      
    case 'RESET':
      handleReset(dotnetRef);
      break;
      
    case 'COMMIT':
      handleCommit(message, dotnetRef);
      break;
      
    case 'ROLLBACK':
      handleRollback(message, dotnetRef);
      break;
      
    case 'SWEEP':
      handleSweep(message, dotnetRef);
      break;
      
    case 'TOGGLE_ACTION':
      handleToggleAction(message, dotnetRef);
      break;
      
    case 'IMPORT_STATE':
      handleImportState(message, dotnetRef);
      break;

    case 'PAUSE_RECORDING':
    case 'LOCK_CHANGES':
      handleRecordingControl(payload.type, dotnetRef);
      break;
      
    default:
      console.log('DevTools action:', payload?.type);
      break;
  }
}

/**
 * Handles jump to state/action operations.
 */
function handleJumpToState(message, dotnetRef) {
  try {
    if (message.state) {
      const state = typeof message.state === 'string' 
        ? message.state 
        : JSON.stringify(message.state);
      
      dotnetRef.invokeMethodAsync('OnDevToolsStateAsync', state)
        .catch(error => console.error('Failed to invoke .NET method:', error));
    }
  } catch (error) {
    console.error('Error handling jump to state:', error);
  }
}

/**
 * Handles reset operations.
 */
function handleReset(dotnetRef) {
  try {
    dotnetRef.invokeMethodAsync('OnDevToolsResetAsync')
      .catch(error => console.error('Failed to invoke .NET reset:', error));
  } catch (error) {
    console.error('Error handling reset:', error);
  }
}

/**
 * Handles commit operations (make current state the new baseline).
 */
function handleCommit(message, dotnetRef) {
  try {
    console.log('DevTools: Commit current state');
    // For commit, we could notify .NET to update the initial state
    if (dotnetRef.invokeMethodAsync) {
      dotnetRef.invokeMethodAsync('OnDevToolsCommitAsync')
        .catch(error => console.error('Failed to invoke .NET commit:', error));
    }
  } catch (error) {
    console.error('Error handling commit:', error);
  }
}

/**
 * Handles rollback operations.
 */
function handleRollback(message, dotnetRef) {
  try {
    console.log('DevTools: Rollback to committed state');
    // Rollback to the last committed state
    if (dotnetRef.invokeMethodAsync) {
      dotnetRef.invokeMethodAsync('OnDevToolsRollbackAsync')
        .catch(error => console.error('Failed to invoke .NET rollback:', error));
    }
  } catch (error) {
    console.error('Error handling rollback:', error);
  }
}

/**
 * Handles sweep operations (remove skipped actions).
 */
function handleSweep(message, dotnetRef) {
  try {
    console.log('DevTools: Sweep skipped actions');
    // This would remove all skipped actions from history
    if (dotnetRef.invokeMethodAsync) {
      dotnetRef.invokeMethodAsync('OnDevToolsSweepAsync')
        .catch(error => console.error('Failed to invoke .NET sweep:', error));
    }
  } catch (error) {
    console.error('Error handling sweep:', error);
  }
}

/**
 * Handles toggle action operations (skip/unskip specific actions).
 */
function handleToggleAction(message, dotnetRef) {
  try {
    const actionIndex = message.payload?.id;
    if (typeof actionIndex === 'number') {
      console.log(`DevTools: Toggle action at index ${actionIndex}`);
      
      if (dotnetRef.invokeMethodAsync) {
        dotnetRef.invokeMethodAsync('OnDevToolsToggleActionAsync', actionIndex)
          .catch(error => console.error('Failed to invoke .NET toggle action:', error));
      }
    }
  } catch (error) {
    console.error('Error handling toggle action:', error);
  }
}

/**
 * Handles state import operations.
 */
function handleImportState(message, dotnetRef) {
  try {
    console.log('DevTools: Import state');
    
    if (message.state) {
      const state = typeof message.state === 'string' 
        ? message.state 
        : JSON.stringify(message.state);
      
      if (dotnetRef.invokeMethodAsync) {
        dotnetRef.invokeMethodAsync('OnDevToolsImportStateAsync', state)
          .catch(error => console.error('Failed to invoke .NET import:', error));
      }
    }
  } catch (error) {
    console.error('Error handling import state:', error);
  }
}

/**
 * Handles recording control operations.
 */
function handleRecordingControl(actionType, dotnetRef) {
  try {
    console.log(`DevTools: ${actionType}`);
    
    const isPaused = actionType === 'PAUSE_RECORDING';
    const isLocked = actionType === 'LOCK_CHANGES';
    
    if (dotnetRef.invokeMethodAsync) {
      dotnetRef.invokeMethodAsync('OnDevToolsRecordingControlAsync', isPaused, isLocked)
        .catch(error => console.error('Failed to invoke .NET recording control:', error));
    }
  } catch (error) {
    console.error('Error handling recording control:', error);
  }
}

/**
 * Sends error information to DevTools.
 * @param {Error} error - The error to report
 * @param {string} context - Context where the error occurred
 */
export function sendErrorToDevTools(error, context = 'Unknown') {
  try {
    if (devTools) {
      const errorAction = {
        type: '@@ERROR',
        payload: {
          message: error.message,
          stack: error.stack,
          context: context,
          timestamp: new Date().toISOString()
        },
        error: true
      };
      
      devTools.send(errorAction, { error: true });
    }
  } catch (err) {
    console.error('Failed to send error to DevTools:', err);
  }
}

/**
 * Disconnects from Redux DevTools (cleanup).
 */
export function disconnectDevTools() {
  try {
    if (devTools) {
      devTools.disconnect();
      devTools = null;
      devToolsConfig = null;
      console.log('Disconnected from Redux DevTools');
    }
  } catch (error) {
    console.error('Failed to disconnect from DevTools:', error);
  }
}

/**
 * Checks if DevTools is currently connected.
 * @returns {boolean} True if connected, false otherwise
 */
export function isDevToolsConnected() {
  return devTools !== null;
}

/**
 * Gets the current DevTools configuration.
 * @returns {object|null} The configuration object or null if not initialized
 */
export function getDevToolsConfig() {
  return devToolsConfig;
}

/**
 * Updates DevTools configuration at runtime.
 * @param {object} newConfig - New configuration options
 */
export function updateDevToolsConfig(newConfig) {
  try {
    if (devToolsConfig) {
      devToolsConfig = { ...devToolsConfig, ...newConfig };
      console.log('DevTools configuration updated:', devToolsConfig);
    }
  } catch (error) {
    console.error('Failed to update DevTools configuration:', error);
  }
}