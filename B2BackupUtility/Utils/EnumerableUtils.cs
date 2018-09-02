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

using System;
using System.Collections.Generic;
using System.Linq;

namespace B2BackupUtility.Utils
{
    /// <summary>
    /// A utility class used for Enumerables
    /// </summary>
    public static class EnumerableUtils
    {
        /// <summary>
        /// Compares two IEnumerables and disregards order
        /// </summary>
        /// <param name="thisList">This list</param>
        /// <param name="otherList">the other list to compare this list to</param>
        /// <param name="comparer">A custom comparer for element type T</param>
        /// <typeparam name="T">The type of the elements</typeparam>
        /// <returns>True if two IEnumerables have the same elements, disregarding order. False otherwise</returns>
        public static bool ScrambledEquals<T>(this IEnumerable<T> thisList, IEnumerable<T> otherList, IEqualityComparer<T> comparer)
        {
            if (thisList == null)
            {
                throw new NullReferenceException("Attempted to deference null reference (this object in fact)");
            }

            if (otherList == null)
            {
                // If we are not null and the other list is null, we cannot be equal
                return false;
            }

            if (comparer == null)
            {
                // Must provide some comparer
                throw new ArgumentNullException("comparer");
            }

            Dictionary<T, int> elementBag = new Dictionary<T, int>(comparer);
            foreach (T currentElement in thisList)
            {
                if (elementBag.ContainsKey(currentElement))
                {
                    elementBag[currentElement]++;
                }
                else
                {
                    elementBag.Add(currentElement, 1);
                }
            }

            foreach (T otherListCurrentElement in otherList)
            {
                if (elementBag.ContainsKey(otherListCurrentElement))
                {
                    elementBag[otherListCurrentElement]--;
                }
                else
                {
                    return false;
                }
            }

            return elementBag.Values.All(c => c == 0);
        }

        /// <summary>
        /// Compares two IEnumerables, disregarding order. This uses the default EqualityComparer for type T
        /// </summary>
        /// <param name="thisList">This list</param>
        /// <param name="otherList">the other list to compare this list to</param>
        /// <typeparam name="T">The type of the elements</typeparam>
        /// <returns>True if two IEnumerables have the same elements, disregarding order. False otherwise</returns>
        public static bool ScrambledEquals<T>(this IEnumerable<T> thisList, IEnumerable<T> otherList)
        {
            return thisList.ScrambledEquals(otherList, EqualityComparer<T>.Default);
        }

        /// <summary>
        /// Gets the hashcode of this IEnumerable by cycling through and XOR'ing all of the element's GetHashCode()
        /// result
        /// </summary>
        /// <typeparam name="T">The type of this element</typeparam>
        /// <param name="thisList">This reference</param>
        /// <returns>This object's hashcode</returns>
        public static int GetHashCodeEnumerable<T>(this IEnumerable<T> thisList)
        {
            if (thisList == null)
            {
                throw new NullReferenceException("Attempted to deference a null object");
            }

            return thisList.Aggregate(0, (acc, e) => e?.GetHashCode() ?? 0 ^ acc);
        }
    }
}