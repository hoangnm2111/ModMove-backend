namespace ModMove.WebApi.Services;

/// <summary>Ensures uploaded multipart streams are seekable for magic-byte validation without buffering huge files.</summary>
internal static class UploadStreamHelper
{
    private const int MaxBufferedBytes = 6 * 1024 * 1024;

    public static async Task<Stream> OpenSeekableStreamAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var stream = file.OpenReadStream();
        if (stream.CanSeek)
            return stream;

        var buffer = new MemoryStream();
        try
        {
            var chunk = new byte[81920];
            var total = 0;
            while (total < MaxBufferedBytes)
            {
                var read = await stream.ReadAsync(chunk.AsMemory(0, Math.Min(chunk.Length, MaxBufferedBytes - total)), cancellationToken);
                if (read == 0)
                    break;
                buffer.Write(chunk, 0, read);
                total += read;
            }
            buffer.Seek(0, SeekOrigin.Begin);
            return buffer;
        }
        finally
        {
            await stream.DisposeAsync();
        }
    }
}

