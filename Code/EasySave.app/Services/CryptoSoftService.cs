<<<<<<< HEAD
﻿using System;
=======
<<<<<<< HEAD
﻿using System.Diagnostics;

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
=======
using System;
>>>>>>> origin/main
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace EasySave.Services
{
    public enum EncryptionMode
    {
        AES,
        XOR
    }

    public class CryptoSoftService
    {
        private const string DefaultKey = "EasySave2025ProSoftCESISecureKey!";

        public EncryptionMode Mode { get; set; } = EncryptionMode.AES;

        /// <summary>
        /// Encrypts a file in-place using the selected mode. Returns encryption time in ms.
        /// 0 = file not found or error, >0 = time in ms.
        /// </summary>
        public int EncryptFile(string filePath)
        {
            return Mode switch
            {
                EncryptionMode.XOR => EncryptFileXor(filePath),
                _ => EncryptFileAes(filePath)
            };
        }

        private int EncryptFileAes(string filePath)
        {
            if (!File.Exists(filePath))
                return 0;

            var sw = Stopwatch.StartNew();

            try
            {
                byte[] fileContent = File.ReadAllBytes(filePath);

                byte[] keyBytes;
                using (var sha256 = SHA256.Create())
                {
                    keyBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(DefaultKey));
                }
        };

                byte[] encryptedData;
                byte[] iv;

                using (var aes = Aes.Create())
                {
                    aes.KeySize = 256;
                    aes.BlockSize = 128;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.Key = keyBytes;
                    aes.GenerateIV();
                    iv = aes.IV;

                    using (var encryptor = aes.CreateEncryptor())
                    using (var ms = new MemoryStream())
                    {
                        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                        {
                            cs.Write(fileContent, 0, fileContent.Length);
                            cs.FlushFinalBlock();
                        }
                        encryptedData = ms.ToArray();
                    }
                }

                using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                {
                    fs.Write(iv, 0, iv.Length);
                    fs.Write(encryptedData, 0, encryptedData.Length);
                }

                sw.Stop();
                return Math.Max(1, (int)sw.ElapsedMilliseconds);
            }
            catch
            {
                sw.Stop();
                return 0;
            }
        }

        private int EncryptFileXor(string filePath)
        {
            if (!File.Exists(filePath))
                return 0;

            var sw = Stopwatch.StartNew();

            try
            {
                byte[] fileContent = File.ReadAllBytes(filePath);
                byte[] keyBytes = Encoding.UTF8.GetBytes(DefaultKey);

                for (int i = 0; i < fileContent.Length; i++)
                {
                    fileContent[i] ^= keyBytes[i % keyBytes.Length];
                }

                File.WriteAllBytes(filePath, fileContent);

                sw.Stop();
                return Math.Max(1, (int)sw.ElapsedMilliseconds);
            }
            catch
            {
                sw.Stop();
                return 0;
            }
        }
>>>>>>> origin/main
    }
}
