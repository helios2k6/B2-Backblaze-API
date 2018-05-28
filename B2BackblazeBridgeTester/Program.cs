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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace B2BackblazeBridgeTester
{
    public static class Program
    {
        private const int Mebibyte = 1024 * 1024;

        public static void Main(string[] args)
        {
            Console.WriteLine("B2 Backblaze Tester v1.1");
            Execute(args[0], args[1], args[2], args[3]).Wait();
        }

        private static async Task Execute(string accountID, string applicationKey, string bucketID, string fileToUpload)
        {
            if (string.IsNullOrEmpty(accountID) || string.IsNullOrEmpty(applicationKey) || File.Exists(fileToUpload) == false)
            {
                Console.WriteLine("Cannot have empty account ID or application key");
            }

            Console.WriteLine(string.Format("Uploading file {0}", fileToUpload));
            Console.WriteLine("Authorizing Account");
            AuthorizeAccountAction authorizeAccountAction = new AuthorizeAccountAction(accountID, applicationKey);
            BackblazeB2ActionResult<BackblazeB2AuthorizationSession> authorizeAccountResult = await authorizeAccountAction.ExecuteAsync();
            if (authorizeAccountResult.HasErrors)
            {
                Console.WriteLine("Could not authorize account");
                return;
            }

            Dictionary<Tuple<int, int>, long> timings = new Dictionary<Tuple<int, int>, long>();
            Dictionary<Tuple<int, int>, string> exceptions = new Dictionary<Tuple<int, int>, string>();

            // Test connection speeds and chunk sizes
            IEnumerable<int> connections = new[] { 25 };
            IEnumerable<int> chunkSizes = new[] { 5 * Mebibyte };

            Console.WriteLine("Running Test");
            // Then we find the area that is likely to have the best 
            foreach (int currentNumConnections in connections)
            {
                foreach (int currentChunkSize in chunkSizes)
                {
                    Console.WriteLine(string.Format("Using {0} connections at {1} bytes", currentNumConnections, currentChunkSize));
                    Tuple<int, int> mapKey = Tuple.Create(currentNumConnections, currentChunkSize);
                    try
                    {
                        Stopwatch watch = new Stopwatch();
                        UploadFileUsingMultipleConnectionsAction uploadFileAction = new UploadFileUsingMultipleConnectionsAction(
                          authorizeAccountResult.Result,
                          fileToUpload,
                          bucketID,
                          currentChunkSize,
                          currentNumConnections
                        );

                        watch.Start();
                        BackblazeB2ActionResult<BackblazeB2UploadMultipartFileResult> uploadResult = await uploadFileAction.ExecuteAsync();
                        watch.Stop();

                        if (uploadResult.HasErrors)
                        {
                            Console.WriteLine("Failed to run test");
                            exceptions.Add(mapKey, uploadResult.Errors.First().Message);
                        }
                        else
                        {
                            Console.WriteLine("Success");
                            timings.Add(mapKey, watch.ElapsedTicks);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Failed to run test");
                        exceptions.Add(mapKey, e.Message);
                    }
                }
            }

            FileInfo info = new FileInfo(fileToUpload);
            Console.WriteLine(string.Format("File length is: {0}", info.Length));
            Console.WriteLine(string.Format("Current ticks per second is: {0}", Stopwatch.Frequency));

            // Print out results
            Console.WriteLine(string.Format("Connections | Bytes | Ticks"));
            foreach (KeyValuePair<Tuple<int, int>, long> entry in timings)
            {
                Console.WriteLine(string.Format("{0} | {1} | {2}", entry.Key.Item1, entry.Key.Item2, entry.Value));
            }

            // Print out exceptions
            foreach (KeyValuePair<Tuple<int, int>, string> entry in exceptions)
            {
                Console.WriteLine(string.Format("ERROR FOR: {0} | {1} mebibytes: {2}", entry.Key.Item1, entry.Key.Item2, entry.Value));
            }
        }
    }
}
