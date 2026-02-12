using System.Diagnostics;

public class CryptoSoftService : ICryptoService
{
    private readonly string _cryptoSoftPath;

    public CryptoSoftService(string cryptoSoftPath)
    {
        _cryptoSoftPath = cryptoSoftPath;
    }

    public async Task<int> EncryptAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(_cryptoSoftPath))
            return -1;

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _cryptoSoftPath,
                Arguments = $"\"{filePath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        var stopwatch = Stopwatch.StartNew();

        process.Start();
        await process.WaitForExitAsync(cancellationToken);

        stopwatch.Stop();

        if (process.ExitCode == 0)
            return (int)stopwatch.ElapsedMilliseconds;

        return -process.ExitCode;
    }
}
