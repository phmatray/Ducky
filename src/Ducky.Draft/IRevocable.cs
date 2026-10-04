namespace Ducky.Draft;

/// <summary>A draft that can be revoked: once revoked, every member throws <see cref="ObjectDisposedException"/>.</summary>
/// <remarks>Drafts implement it explicitly, so it stays out of IntelliSense.</remarks>
public interface IRevocable
{
    /// <summary>Revokes the draft and the drafts it caches.</summary>
    void Revoke();
}
