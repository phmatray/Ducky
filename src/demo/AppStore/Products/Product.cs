// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace AppStore;

public record Product(
    Guid Id,
    string Name,
    decimal Price,
    string Category)
    : IEntity<Guid>;
