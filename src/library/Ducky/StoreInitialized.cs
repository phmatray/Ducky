// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky;

/// <summary>
/// Represents an action that is dispatched when the store is initialized.
/// </summary>
public sealed record StoreInitialized : IKeyedAction
{
    /// <inheritdoc />
    public string TypeKey => "store/initialized";
}
