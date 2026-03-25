// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

global using System.Collections.Immutable;
global using System.Diagnostics.CodeAnalysis;
global using Xunit;
global using Shouldly;
global using Ducky;
global using Ducky.Builder;
global using Ducky.Blazor.Middlewares.DevTools;
global using Ducky.Blazor.Middlewares.JsLogging;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Time.Testing;
global using FakeItEasy;
