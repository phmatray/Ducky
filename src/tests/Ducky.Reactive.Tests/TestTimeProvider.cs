// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Reactive.Tests;

/// <summary>
/// A test time provider that allows manual time advancement.
/// </summary>
public sealed class TestTimeProvider : TimeProvider
{
    private DateTimeOffset _currentTime = DateTimeOffset.UtcNow;
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow()
    {
        lock (_lock)
        {
            return _currentTime;
        }
    }

    /// <summary>
    /// Advances the current time by the specified amount.
    /// </summary>
    /// <param name="time">The amount of time to advance.</param>
    public void Advance(TimeSpan time)
    {
        lock (_lock)
        {
            _currentTime = _currentTime.Add(time);
        }
    }

    /// <summary>
    /// Sets the current time to a specific value.
    /// </summary>
    /// <param name="time">The time to set.</param>
    public void SetUtcNow(DateTimeOffset time)
    {
        lock (_lock)
        {
            _currentTime = time;
        }
    }
}