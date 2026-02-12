using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CryptoSoft
{
    /// <summary>
    /// CryptoSoft - Simple AES-256 file encryption tool for EasySave v2.0
    /// Usage: CryptoSoft.exe "filePath" [key]
    /// 
    /// Encrypts the file in-place using AES-256-CBC.
    /// If no key is provided, a default key is used.
    /// The IV is prepended to the encrypted file.
    /// 
    /// Exit codes:
    ///   0 = Success
    ///   1 = Invalid arguments
    ///   2 = File not found
    ///   3 = Encryption error
    /// </summary>
    class Program
    {
        private const string DefaultKey = "EasySave2025ProSoftCESISecureKey!"; // 32 chars = 256 bits

        static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.Error.WriteLine("Usage: CryptoSoft.exe <filePath> [key]");
                return 1;
            }

            string filePath = args[0];
            string key = args.Length >= 2 ? args[1] : DefaultKey;

            if (!File.Exists(filePath))
            {
                Console.Error.WriteLine($"File not found: {filePath}");
                return 2;
            }

            try
            {
                EncryptFile(filePath, key);
                Console.WriteLine($"Encrypted: {filePath}");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Encryption error: {ex.Message}");
                return 3;
            }
        }

        /// <summary>
        /// Encrypts a file in-place using AES-256-CBC.
        /// The output format is: [16 bytes IV] + [encrypted data]
        /// </summary>
        static void EncryptFile(string filePath, string key)
        {
            // Read original file content
            byte[] fileContent = File.ReadAllBytes(filePath);

            // Derive a 256-bit key from the string using SHA-256
            byte[] keyBytes;
            using (var sha256 = SHA256.Create())
            {
                keyBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
            }

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

            // Write [IV + encrypted data] back to the file
            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                fs.Write(iv, 0, iv.Length);
                fs.Write(encryptedData, 0, encryptedData.Length);
            }
        }
    }
}
