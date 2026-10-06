namespace Fatewake.Infrastructure.Art;

public interface IArtBinaryStorage
{
    Task PutAsync(string storageKey,Stream content,string contentType,CancellationToken ct=default);
    Task<Stream> OpenReadAsync(string storageKey,CancellationToken ct=default);
    Task<bool> ExistsAsync(string storageKey,CancellationToken ct=default);
}

public sealed class FileSystemArtBinaryStorage(string rootPath):IArtBinaryStorage
{
    public async Task PutAsync(string storageKey,Stream content,string contentType,CancellationToken ct=default)
    {
        var path=PathFor(storageKey);Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var output=File.Create(path);await content.CopyToAsync(output,ct);
    }
    public Task<Stream> OpenReadAsync(string storageKey,CancellationToken ct=default)
        => Task.FromResult<Stream>(File.OpenRead(PathFor(storageKey)));
    public Task<bool> ExistsAsync(string storageKey,CancellationToken ct=default)
        => Task.FromResult(File.Exists(PathFor(storageKey)));
    private string PathFor(string key)
    {
        var safe=key.Replace('\\','/').TrimStart('/');
        if(safe.Contains("../",StringComparison.Ordinal))throw new ArgumentException("Invalid storage key.",nameof(key));
        return Path.Combine(rootPath,safe.Replace('/',Path.DirectorySeparatorChar));
    }
}
