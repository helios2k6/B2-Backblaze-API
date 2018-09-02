/* 
 * Copyright (c) 2015 Andrew Johnson
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy of 
 * this software and associated documentation files (the "Software"), to deal in 
 * the Software without restriction, including without limitation the rights to use,
 * copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the
 * Software, and to permit persons to whom the Software is furnished to do so,
 * subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all 
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
 * FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
 * COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN
 * AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
 * WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

using B2BackblazeBridge.Actions;
using B2BackblazeBridge.Core;
using B2BackupUtility.Database;
using B2BackupUtility.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace B2BackupUtility.Commands
{
    /// <summary>
    /// The base class that represents all actions
    /// </summary>
    public abstract class BaseCommand
    {
        #region private fields
        private static TimeSpan OneHour => TimeSpan.FromMinutes(60);
        private static int DefaultBufferSize => 104857600;

        private readonly IEnumerable<string> _rawArgs;
        private readonly Lazy<FileDatabaseManifest> _fileManifest;
        private readonly Lazy<Config> _config;

        private BackblazeB2AuthorizationSession _authorizationSession;
        private FileDatabaseManifest _fileDatabaseManifest;
        private string ApplicationKey => _config.Value.ApplicationKey;
        private string ApplicationKeyID => _config.Value.ApplicationKeyID;
        #endregion

        #region protected properties
        /// <summary>
        /// Get the bucket ID
        /// </summary>
        protected string BucketID => _config.Value.BucketID;

        /// <summary>
        /// Checks to see if this command is using an encryption key
        /// </summary>
        protected bool IsEncrypted => EncryptionKey != null;

        /// <summary>
        /// The encryption key that is being used with this command. Can be null
        /// </summary>
        protected string EncryptionKey => _config.Value.EncryptionKey;

        /// <summary>
        /// The initialization vector that's used for AES encryption. Can be null
        /// </summary>
        protected string InitializationVector => _config.Value.InitializationVector;
        #endregion

        #region public properties
        /// <summary>
        /// The option to specify a config file
        /// </summary>
        public static string ConfigOption => "--config";
        #endregion

        #region public methods
        /// <summary>
        /// Execute this action
        /// </summary>
        public abstract void ExecuteAction();
        #endregion

        #region ctor
        public BaseCommand(IEnumerable<string> rawArgs)
        {
            _rawArgs = rawArgs;
            _config = new Lazy<Config>(DeserializeConfig);
        }
        #endregion

        #region protected methods
        protected void LogCritical(string message)
        {
            Loggers.Logger.Log(LogLevel.CRITICAL, message);
        }

        protected void LogWarn(string message)
        {
            Loggers.Logger.Log(LogLevel.WARNING, message);
        }

        protected void LogInfo(string message)
        {
            Loggers.Logger.Log(LogLevel.INFO, message);
        }

        protected void LogVerbose(string message)
        {
            Loggers.Logger.Log(LogLevel.VERBOSE, message);
        }

        protected void LogDebug(string message)
        {
            Loggers.Logger.Log(LogLevel.DEBUG, message);
        }

        protected BackblazeB2AuthorizationSession GetOrCreateAuthorizationSession()
        {
            if (_authorizationSession == null || _authorizationSession.SessionExpirationDate - DateTime.Now < OneHour)
            {
                AuthorizeAccountAction authorizeAccountAction = new AuthorizeAccountAction(ApplicationKeyID, ApplicationKey);
                BackblazeB2ActionResult<BackblazeB2AuthorizationSession> authorizationSessionResult = authorizeAccountAction.Execute();
                if (authorizationSessionResult.HasErrors)
                {
                    string errorMessage = authorizationSessionResult.Errors.First().Message;
                    throw new InvalidOperationException($"Could not authorize the account with Application Key ID: ${ApplicationKeyID} and Application Key: ${ApplicationKey}. ${errorMessage}");
                }
                _authorizationSession = authorizationSessionResult.Result;
            }

            return _authorizationSession;
        }

        protected FileDatabaseManifest GetOrCreateFileDatabaseManifest()
        {
            // Check to see if this has to be initialized
            if (_fileDatabaseManifest == null)
            {

            }
        }

        /// <summary>
        /// Checks to see if an option exists
        /// </summary>
        /// <param name="option">Gets whether an argument exists</param>
        /// <returns>True if an argument exists. False otherwise</returns>
        protected bool DoesOptionExist(string option)
        {
            return _rawArgs.Any(t => t.Equals(option, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Attempts to get the value for the specified option. If it doesn't exist, an
        /// exception is thrown
        /// </summary>
        /// <param name="option">The option to retrieve the value for</param>
        /// <returns>The value to the option</returns>
        protected string GetArgumentOrThrow(string option)
        {
            if (TryGetArgument(option, out string value))
            {
                return value;
            }

            throw new InvalidOperationException($"Was not able to retrieve value for option {option}");
        }

        /// <summary>
        /// This will attempt to get the value of an argument that is passed in. This cannot
        /// get multiple arguments passed in to a single options
        /// </summary>
        /// <param name="option">The option to get arguments for</param>
        /// <param name="value">The value found</param>
        /// <returns>True if an argument was found. False otherwise</returns>
        protected bool TryGetArgument(string option, out string value)
        {
            bool returnNextItem = false;
            foreach (string arg in _rawArgs)
            {
                if (returnNextItem)
                {
                    value = arg;
                    return true;
                }

                if (arg.Equals(option, StringComparison.OrdinalIgnoreCase))
                {
                    returnNextItem = true;
                }
            }

            value = null;
            return false;
        }
        #endregion

        #region private methods
        private Config DeserializeConfig()
        {
            bool hasConfigFile = TryGetArgument(ConfigOption, out string configFilePath);
            if (hasConfigFile == false)
            {
                throw new InvalidOperationException("You must provide a config file");
            }

            return JsonConvert.DeserializeObject<Config>(System.IO.File.ReadAllText(configFilePath));
        }

        private static byte[] SerializeManifest(FileDatabaseManifest fileDatabaseManifest, Config config)
        {
            using (MemoryStream serializedManifestStream = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(fileDatabaseManifest))))
            using (MemoryStream compressedMemoryStream = new MemoryStream())
            {
                // It's very important that we dispose of the GZipStream before reading from the memory stream
                using (GZipStream compressionStream = new GZipStream(compressedMemoryStream, CompressionMode.Compress, true))
                {
                    serializedManifestStream.CopyTo(compressionStream);
                }

                return EncryptBytes(compressedMemoryStream.ToArray(), config);
            }
        }

        private static byte[] EncryptBytes(byte[] bytes, Config config)
        {
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = Convert.FromBase64String(config.EncryptionKey);
                aesAlg.IV = Convert.FromBase64String(config.InitializationVector);

                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msEncrypt = new MemoryStream())
                using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                {
                    // It's important we dispose of the BinaryWriter before attempting to read from the 
                    // memory stream with the encrypted bytes
                    using (BinaryWriter swEncrypt = new BinaryWriter(csEncrypt))
                    {
                        //Write all data to the stream.
                        swEncrypt.Write(bytes);
                    }
                    return msEncrypt.ToArray();
                }
            }
        }

        private static FileDatabaseManifest DeserializeManifest(byte[] encryptedBytes, Config config)
        {
            using (MemoryStream deserializedMemoryStream = new MemoryStream())
            {
                using (MemoryStream compressedBytesStream = new MemoryStream(DecryptBytes(encryptedBytes, config)))
                using (GZipStream decompressionStream = new GZipStream(compressedBytesStream, CompressionMode.Decompress))
                {
                    decompressionStream.CopyTo(deserializedMemoryStream);
                }

                return JsonConvert.DeserializeObject<FileDatabaseManifest>(
                    Encoding.UTF8.GetString(deserializedMemoryStream.ToArray())
                );
            }
        }

        private static byte[] DecryptBytes(byte[] bytes, Config config)
        {
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = Convert.FromBase64String(config.EncryptionKey);
                aesAlg.IV = Convert.FromBase64String(config.InitializationVector);

                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                // Create the streams used for decryption.
                using (MemoryStream msDecrypt = new MemoryStream(bytes))
                using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                using (BinaryReader srDecrypt = new BinaryReader(csDecrypt))
                {
                    byte[] buffer = new byte[DefaultBufferSize];
                    List<byte> returnValues = new List<byte>();
                    while (true)
                    {
                        int bytesRead = srDecrypt.Read(buffer, 0, DefaultBufferSize);
                        if (bytesRead < 1)
                        {
                            break;
                        }

                        for (int i = 0; i < bytesRead; i++)
                        {
                            returnValues.Add(buffer[i]);
                        }
                    }

                    return returnValues.ToArray();
                }
            }
        }
        #endregion
    }
}
