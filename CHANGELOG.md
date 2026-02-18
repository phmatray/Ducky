# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## 2.0.0 / 2025-07-03
### Added
- State persistence with hydration support and typed local storage provider
- Persistence demo page showcasing state persistence functionality
- Comprehensive reactive demo console application
- Ducky.Reactive package for enhanced reactive extensions
- AsyncEffectGroup for managing grouped async effects
- DuckySelectorComponent for selector-based state management
- Component generator for reducing boilerplate code
- Middleware validator and custom exceptions for better error handling
- Global error handling with ExceptionHandlingMiddleware
- AsyncEffectRetryMiddleware for automatic retry logic
- ReactiveEffectMiddleware for reactive programming patterns
- Cross-tab synchronization for state consistency across browser tabs
- E2E tests with Playwright
- Docker image support
- Console demo application with Spectre.Console UI
- Redux DevTools integration for debugging
- Reactive action pipeline architecture
- Page layout components and notification system

### Changed
- **BREAKING**: Dropped R3 support in favor of standard .NET reactive extensions
- **BREAKING**: Replaced IRootState with IStateProvider across all components
- **BREAKING**: Simplified IPersistenceProvider interface
- **BREAKING**: Refactored middleware architecture with new pipeline system
- Renamed middleware classes and updated namespaces for consistency
- Enhanced IStore interface with slice management methods
- Refactored namespaces from Ducky.CodeGen.Core to Ducky.Generator.Core
- Improved dependency injection with better service registration
- Replaced Moq with FakeItEasy in tests
- Updated all test assertions to use Shouldly
- Reorganized central package management
- Migrated CodeGen.Core to netstandard2.0
- Enhanced Redux DevTools middleware with better error handling

### Fixed
- Serialization issues with Guid parameters in actions
- AsyncEffect middleware not using after dispatch correctly
- Source generator compilation issues
- Implicit namespace resolution problems
- JavaScript module loading errors
- Middleware inheritance from MiddlewareBase
- CorrelationIdMiddleware functionality
- Redux DevTools initialization when not registered

### Removed
- Unused methods from ActionPipeline and SliceReducers
- Diagnostics functionality from middleware
- Old LoggerProvider implementation
- StateLoggerObserver in favor of EventPublisher
- Obsolete dependency injection code
- DuckyServiceCollectionExtensions file

## 1.0.0 / 2024-12-31
### Added
- Added CHANGELOG
- Initial Release of the package