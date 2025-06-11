export function logGroup(label, prevState, action, nextState) {
  // Use group instead of groupCollapsed for important actions
  const isImportant = action && (action.Amount > 1 || action.type?.includes('ERROR'));
  const groupMethod = isImportant ? 'group' : 'groupCollapsed';
  
  console[groupMethod](`%c${label}`, 'color: inherit;');
  
  // Log prev state with gray color
  console.log('%c prev state', 'color: #9E9E9E; font-weight: bold;', prevState);
  
  // Log action with blue color and special formatting
  console.log('%c action    ', 'color: #03A9F4; font-weight: bold;', action);
  
  // Log next state with green color  
  console.log('%c next state', 'color: #4CAF50; font-weight: bold;', nextState);
  
  // If both states are objects, try to show diff
  if (typeof prevState === 'object' && typeof nextState === 'object' && prevState && nextState) {
    const prevKeys = Object.keys(prevState);
    const nextKeys = Object.keys(nextState);
    
    // Check if we're dealing with state slices (fewer keys means we're already filtered)
    if (prevKeys.length < 5 && nextKeys.length < 5) {
      // Show a simple diff
      const changes = [];
      for (const key of new Set([...prevKeys, ...nextKeys])) {
        const prevValue = prevState[key];
        const nextValue = nextState[key];
        
        if (JSON.stringify(prevValue) !== JSON.stringify(nextValue)) {
          changes.push({
            slice: key,
            prev: prevValue,
            next: nextValue
          });
        }
      }
      
      if (changes.length > 0) {
        console.log('%c diff      ', 'color: #FF6B6B; font-weight: bold;', changes);
      }
    }
  }
  
  console.groupEnd();
}
