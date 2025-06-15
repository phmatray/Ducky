// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky;

/// <summary>
/// Action to hydrate a specific slice with persisted state.
/// </summary>
/// <param name="SliceKey">The key of the slice to hydrate.</param>
/// <param name="State">The state to hydrate with.</param>
public sealed record HydrateSliceAction(string SliceKey, object State);
