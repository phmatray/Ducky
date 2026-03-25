// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Demo.ConsoleApp.Counter;

[DuckyAction]
public sealed record Increment(int Amount = 1);

[DuckyAction]
public sealed record Decrement(int Amount = 1);

[DuckyAction]
public sealed record Reset;

[DuckyAction]
public sealed record SetValue(int Value);

[DuckyAction]
public sealed record IncrementAsync(int Amount = 1, int DelayMs = 1000);
