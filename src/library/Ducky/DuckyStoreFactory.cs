// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Ducky.Pipeline;

namespace Ducky;

/// <summary>
/// Factory for creating fully-wired <see cref="DuckyStore"/> instances.
/// </summary>
public static class DuckyStoreFactory
{
    /// <summary>
    /// Builds the <see cref="ActionPipeline"/>, applies the configured middlewares, and returns a configured <see cref="DuckyStore"/>.
    /// </summary>
    /// <param name="dispatcher">Your action dispatcher.</param>
    /// <param name="slices">Any initial slices to add to the store.</param>
    /// <param name="configurePipeline">Pipeline configuration action.</param>
    /// <returns>A configured <see cref="DuckyStore"/> instance.</returns>
    public static DuckyStore CreateStore(
        IDispatcher dispatcher,
        IEnumerable<ISlice> slices,
        Action<ActionPipeline>? configurePipeline = null)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        ActionPipeline pipeline = new(dispatcher);

        // Compose the pipeline according to user-supplied configuration
        configurePipeline?.Invoke(pipeline);

        // Instantiate the store with the configured pipeline
        return new DuckyStore(dispatcher, pipeline, slices);
    }
}
