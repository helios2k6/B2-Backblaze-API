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

using System.Collections.Generic;
using System.IO;

namespace B2BackupUtility
{
    public static class FilePathUtilities
    {
        public static IEnumerable<LocalFileToRemoteFileMapping> GenerateLocalToRemotePathMapping(IEnumerable<string> localFilePaths, bool flattenDirectoryStructure)
        {
            IDictionary<string, ISet<string>> destinationToPossibleDuplicateFiles = new Dictionary<string, ISet<string>>();
            IList<LocalFileToRemoteFileMapping> fileMappings = new List<LocalFileToRemoteFileMapping>();
            foreach (string localFilePath in localFilePaths)
            {
                string destination = GetDestinationFileName(localFilePath, flattenDirectoryStructure);
                if (destinationToPossibleDuplicateFiles.ContainsKey(destination))
                {
                    // This file might be similar to other files we know of. Check all of the files
                    // and see if it's similar to them
                    bool isDuplicate = false;
                    foreach (string possibleDuplicate in destinationToPossibleDuplicateFiles[destination])
                    {
                        if (CommonActions.AreFilesEqual(localFilePath, possibleDuplicate))
                        {
                            isDuplicate = true;
                            break;
                        }
                    }

                    // Remember to add this file to the list of possible duplicates
                    destinationToPossibleDuplicateFiles[destination].Add(localFilePath);
                    if (isDuplicate == false)
                    {
                        // If we're not a duplicate, then we need to rename the destination file to something we know is unique
                        destination = string.Format(
                            "{0}_non_duplicate({1}){2}",
                            Path.GetFileNameWithoutExtension(destination),
                            destinationToPossibleDuplicateFiles[destination].Count,
                            Path.GetExtension(destination)
                        );
                        fileMappings.Add(new LocalFileToRemoteFileMapping
                        {
                            LocalFilePath = localFilePath,
                            RemoteFilePath = destination,
                        });
                    }
                }
                else
                {
                    // Add this entry
                    HashSet<string> possibleDuplicates = new HashSet<string>
                    {
                        localFilePath,
                    };
                    destinationToPossibleDuplicateFiles[destination] = possibleDuplicates;

                    fileMappings.Add(new LocalFileToRemoteFileMapping
                    {
                        LocalFilePath = localFilePath,
                        RemoteFilePath = destination,
                    });
                }
            }
            
            return fileMappings;
        }

        public static string GetDestinationFileName(string localFileName, bool flatten)
        {
            return flatten ? Path.GetFileName(localFileName) : localFileName;
        }
    }
}