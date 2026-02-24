using System;
using System.Diagnostics;
using System.IO;

namespace EasySave.Services
{
    public enum EncryptionMode
    {
        AES,
        XOR
    }

    /// <summary>
    /// Calls the external CryptoSoft.exe process for file encryption.
    /// CryptoSoft is mono-instance (uses a named Mutex internally).
    /// Returns encryption time in ms: >0 = success, &lt;0 = error code.
    /// </summary>
    public class CryptoSoftService
    {
        private string _cryptoSoftPath;

        public EncryptionMode Mode { get; set; } = EncryptionMode.AES;

        public CryptoSoftService()
        {
            _cryptoSoftPath = FindCryptoSoftPath();
        }

        /// <summary>
        /// Encrypts a file by calling external CryptoSoft.exe.
        /// Returns: >0 = encryption time (ms), 0 = no encryption, &lt;0 = error code.
        /// </summary>
        public int EncryptFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return -2;

            if (string.IsNullOrWhiteSpace(_cryptoSoftPath) || !File.Exists(_cryptoSoftPath))
            {
                _cryptoSoftPath = FindCryptoSoftPath();
                if (!File.Exists(_cryptoSoftPath))
                    return -1;
            }

            var sw = Stopwatch.StartNew();

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = _cryptoSoftPath,
                    Arguments = $"\"{filePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    sw.Stop();
                    return -3;
                }

                // Wait up to 60 seconds for CryptoSoft (it may be waiting for the Mutex)
                bool exited = process.WaitForExit(60000);
                sw.Stop();

                if (!exited)
                {
                    try { process.Kill(); } catch { }
                    return -4;
                }

                if (process.ExitCode != 0)
                    return -process.ExitCode;

                return Math.Max(1, (int)sw.ElapsedMilliseconds);
            }
            catch
            {
                sw.Stop();
                return -3;
            }
        }

        /// <summary>
        /// Locates CryptoSoft.exe relative to the application directory.
        /// </summary>
        private static string FindCryptoSoftPath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // When running from build output: go up to Code/ then into CryptoSoft/bin
            string[] searchPaths = new[]
            {
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "CryptoSoft", "bin", "Debug", "net8.0", "CryptoSoft.exe")),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "CryptoSoft", "bin", "Release", "net8.0", "CryptoSoft.exe")),
                Path.GetFullPath(Path.Combine(baseDir, "..", "CryptoSoft", "CryptoSoft.exe")),
                Path.GetFullPath(Path.Combine(baseDir, "CryptoSoft.exe")),
            };

            foreach (var path in searchPaths)
            {
                if (File.Exists(path))
                    return path;
            }

            return searchPaths[0];
        }
    }
}
