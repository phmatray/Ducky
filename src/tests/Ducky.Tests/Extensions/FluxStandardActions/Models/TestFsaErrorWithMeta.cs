// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Tests.Extensions.FluxStandardActions.Models;

public sealed record TestFsaErrorWithMeta<TMeta>(Exception Payload, TMeta Meta)
    : FsaError<TMeta>(Payload, Meta)
{
    public override string TypeKey => "error/action";
}
