﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using static LanguageExt.Prelude;
using System.Diagnostics.Contracts;

namespace LanguageExt
{
    internal class HSetInternal<T> :
        IReadOnlyCollection<T>,
        ICollection<T>,
        ISet<T>,
        ICollection,
        IAppendable<HSetInternal<T>>,
        ISubtractable<HSetInternal<T>>,
        IEquatable<HSetInternal<T>>
    {
        public static readonly HSetInternal<T> Empty = new HSetInternal<T>(HashTableEmpty, 0);
        static readonly Map<int, Lst<T>> HashTableEmpty = Map<int, Lst<T>>.Empty;

        readonly Map<int, Lst<T>> hashTable;
        readonly int count;

        internal HSetInternal()
        {
            hashTable = HashTableEmpty;
        }

        internal HSetInternal(Map<int, Lst<T>> hashTable, int count)
        {
            this.hashTable = hashTable;
            this.count = count;
        }

        internal HSetInternal(IEnumerable<T> items, bool checkUniqueness = false)
        {
            var set = new HSetInternal<T>();

            if (checkUniqueness)
            {
                foreach (var item in items)
                {
                    set = set.TryAdd(item);
                }
            }
            else
            {
                foreach (var item in items)
                {
                    set = set.Add(item);
                }
            }
        }
        
        [Pure]
        public T this[T key] =>
            Find(key).IfNone(() => failwith<T>("Key doesn't exist in set"));

        [Pure]
        public bool IsEmpty =>
            Count == 0;

        [Pure]
        public int Count =>
            count;

        [Pure]
        public int Length =>
            count;

        public object SyncRoot
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public bool IsSynchronized
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public bool IsReadOnly
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        [Pure]
        public HSetInternal<T> Add(T key)
        {
            if (isnull(key)) throw new ArgumentNullException(nameof(key));

            var ht = hashTable;
            var hash = key.GetHashCode();
            if (ht.ContainsKey(hash))
            {
                var bucket = ht[hash];
                var eq = EqualityComparer<T>.Default;
                foreach(var item in bucket)
                {
                    if(eq.Equals(item, key))
                    {
                        throw new ArgumentException("Key already exists in HSet");
                    }
                }
                ht = ht.SetItem(hash, bucket.Add(key));
            }
            else
            {
                ht = ht.Add(hash, List(key));
            }
            return new HSetInternal<T>(ht, Count + 1);
        }

        [Pure]
        public HSetInternal<T> TryAdd(T key)
        {
            if (isnull(key)) throw new ArgumentNullException(nameof(key));

            var ht = hashTable;
            var hash = key.GetHashCode();
            if (ht.ContainsKey(hash))
            {
                var bucket = ht[hash];
                var eq = EqualityComparer<T>.Default;
                foreach (var item in bucket)
                {
                    if (eq.Equals(item, key))
                    {
                        return this;
                    }
                }
                ht = ht.SetItem(hash, bucket.Add(key));
            }
            else
            {
                ht = ht.Add(hash, List(key));
            }
            return new HSetInternal<T>(ht, Count + 1);
        }

        [Pure]
        public HSetInternal<T> AddOrUpdate(T key)
        {
            if (isnull(key)) throw new ArgumentNullException(nameof(key));

            var ht = hashTable;
            var hash = key.GetHashCode();
            if (ht.ContainsKey(hash))
            {
                var bucket = ht[hash];
                var eq = EqualityComparer<T>.Default;
                var contains = false;
                foreach (var item in bucket)
                {
                    if (eq.Equals(item, key))
                    {
                        contains = true;
                        break;
                    }
                }
                if (contains)
                {
                    bucket = bucket.Filter(x => !eq.Equals(x, key));
                }
                ht = ht.SetItem(hash, bucket.Add(key));
            }
            else
            {
                ht = ht.Add(hash, List(key));
            }
            return new HSetInternal<T>(ht, Count + 1);
        }

        [Pure]
        public HSetInternal<T> AddRange(IEnumerable<T> range)
        {
            if (range == null)
            {
                return this;
            }
            var self = this;
            foreach (var item in range)
            {
                self = self.Add(item);
            }
            return self;
        }

        [Pure]
        public HSetInternal<T> TryAddRange(IEnumerable<T> range)
        {
            if (range == null)
            {
                return this;
            }
            var self = this;
            foreach (var item in range)
            {
                self = self.TryAdd(item);
            }
            return self;
        }

        [Pure]
        public HSetInternal<T> AddOrUpdateRange(IEnumerable<T> range)
        {
            if (range == null)
            {
                return this;
            }
            var self = this;
            foreach (var item in range)
            {
                self = self.AddOrUpdate(item);
            }
            return self;
        }

        [Pure]
        public HSetInternal<T> Remove(T key)
        {
            if (isnull(key)) return this;
            var ht = hashTable;
            var hash = key.GetHashCode();
            if (ht.ContainsKey(hash))
            {
                var eq = EqualityComparer<T>.Default;
                var bucket = ht[hash];
                bucket = bucket.Filter(x => !eq.Equals(x, key));
                return bucket.Count == 0
                    ? new HSetInternal<T>(ht.Remove(hash), Count - 1)
                    : new HSetInternal<T>(ht.SetItem(hash, bucket), Count - 1);
            }
            else
            {
                return this;
            }
        }

        [Pure]
        public Option<T> Find(T key)
        {
            if (isnull(key)) return None;
            var eq = EqualityComparer<T>.Default;
            return hashTable.Find(key.GetHashCode())
                            .Bind(bucket => bucket.Find(x => eq.Equals(x, key)));
        }

        [Pure]
        public IEnumerable<T> FindSeq(T key) =>
            Find(key).AsEnumerable();

        [Pure]
        public R Find<R>(T key, Func<T, R> Some, Func<R> None) =>
            Find(key).Match(Some, None);

        [Pure]
        public HSetInternal<T> SetItem(T key)
        {
            if (isnull(key)) throw new ArgumentNullException(nameof(key));

            var ht = hashTable;
            var hash = key.GetHashCode();
            if (ht.ContainsKey(hash))
            {
                var eq = EqualityComparer<T>.Default;
                var bucket = ht[hash];
                return new HSetInternal<T>(ht.SetItem(hash, bucket.Map(x => eq.Equals(x, key) ? key : x)), Count);
            }
            else
            {
                return this;
            }
        }

        [Pure]
        public HSetInternal<T> TrySetItem(T key)
        {
            if (isnull(key)) throw new ArgumentNullException(nameof(key));

            var ht = hashTable;
            var hash = key.GetHashCode();
            if (ht.ContainsKey(hash))
            {
                var eq = EqualityComparer<T>.Default;
                var bucket = ht[hash];
                return new HSetInternal<T>(ht.SetItem(hash, bucket.Map(x => eq.Equals(x, key) ? key : x)), Count);
            }
            else
            {
                return this;
            }
        }

        [Pure]
        public bool Contains(T key) =>
            !isnull(key) && Find(key).IsSome;

        [Pure]
        public HSetInternal<T> Clear() =>
            Empty;

        [Pure]
        public HSetInternal<T> SetItems(IEnumerable<T> items)
        {
            if (items == null) return this;
            var self = this;
            foreach (var item in items)
            {
                if (isnull(item)) continue;
                self = SetItem(item);
            }
            return self;
        }

        [Pure]
        public HSetInternal<T> TrySetItems(IEnumerable<T> items)
        {
            if (items == null) return this;
            var self = this;
            foreach (var item in items)
            {
                if (isnull(item)) continue;
                self = TrySetItem(item);
            }
            return self;
        }

        [Pure]
        public HSetInternal<T> RemoveRange(IEnumerable<T> keys)
        {
            var self = this;
            foreach (var key in keys)
            {
                self = self.Remove(key);
            }
            return self;
        }

        #region IEnumerable interface

        public IEnumerator<T> GetEnumerator() =>
            AsEnumerable().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() =>
            AsEnumerable().GetEnumerator();

        public IEnumerable<T> AsEnumerable() =>
            hashTable.Values.Bind(x => x);

        #endregion

        [Pure]
        public static HSetInternal<T> operator +(HSetInternal<T> lhs, HSetInternal<T> rhs) =>
            lhs.Append(rhs);

        [Pure]
        public HSetInternal<T> Append(HSetInternal<T> rhs)
        {
            var self = this;
            foreach (var item in rhs)
            {
                if (!self.Contains(item))
                {
                    self = self.Add(item);
                }
            }
            return self;
        }

        [Pure]
        public static HSetInternal<T> operator -(HSetInternal<T> lhs, HSetInternal<T> rhs) =>
            lhs.Subtract(rhs);

        [Pure]
        public HSetInternal<T> Subtract(HSetInternal<T> rhs)
        {
            var self = this;
            foreach (var item in rhs)
            {
                self = self.Remove(item);
            }
            return self;
        }

        public bool Equals(HSetInternal<T> other)
        {
            if (other == null || Count != other.Count) return false;
            var iterx = GetEnumerator();
            var itery = other.GetEnumerator();

            var eq = EqualityComparer<T>.Default;
            for(int i = Count; i >= 0; i-- )
            {
                iterx.MoveNext();
                itery.MoveNext();
                if (!eq.Equals(iterx.Current, itery.Current)) return false;
            }
            return true;
        }

        /// <summary>
        /// Returns True if 'other' is a proper subset of this set
        /// </summary>
        /// <returns>True if 'other' is a proper subset of this set</returns>
        [Pure]
        public bool IsProperSubsetOf(IEnumerable<T> other)
        {
            if (IsEmpty)
            {
                return other.Any();
            }

            var otherSet = new HSetInternal<T>(other);
            if (Count >= otherSet.Count)
            {
                return false;
            }

            int matches = 0;
            bool extraFound = false;
            foreach (var item in otherSet)
            {
                if (Contains(item))
                {
                    matches++;
                }
                else
                {
                    extraFound = true;
                }

                if (matches == Count && extraFound)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns True if 'other' is a proper superset of this set
        /// </summary>
        /// <returns>True if 'other' is a proper superset of this set</returns>
        [Pure]
        public bool IsProperSupersetOf(IEnumerable<T> other)
        {
            if (IsEmpty)
            {
                return false;
            }

            int matchCount = 0;
            foreach (var item in other)
            {
                matchCount++;
                if (!Contains(item))
                {
                    return false;
                }
            }

            return Count > matchCount;
        }

        /// <summary>
        /// Returns True if 'other' is a superset of this set
        /// </summary>
        /// <returns>True if 'other' is a superset of this set</returns>
        [Pure]
        public bool IsSubsetOf(IEnumerable<T> other)
        {
            if (IsEmpty)
            {
                return true;
            }

            var otherSet = new HSetInternal<T>(other);
            int matches = 0;
            foreach (var item in otherSet)
            {
                if (Contains(item))
                {
                    matches++;
                }
            }
            return matches == Count;
        }

        /// <summary>
        /// Returns True if 'other' is a superset of this set
        /// </summary>
        /// <returns>True if 'other' is a superset of this set</returns>
        [Pure]
        public bool IsSupersetOf(IEnumerable<T> other)
        {
            foreach (var item in other)
            {
                if (!Contains(item))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Returns True if other overlaps this set
        /// </summary>
        /// <typeparam name="T">Element type</typeparam>
        /// <param name="setA">Set A</param>
        /// <param name="setB">Set B</param>
        /// <returns>True if other overlaps this set</returns>
        [Pure]
        public bool Overlaps(IEnumerable<T> other)
        {
            if (IsEmpty)
            {
                return false;
            }

            foreach (var item in other)
            {
                if (Contains(item))
                {
                    return true;
                }
            }
            return false;
        }

        bool ISet<T>.Add(T item)
        {
            throw new NotSupportedException();
        }

        public void UnionWith(IEnumerable<T> other)
        {
            throw new NotSupportedException();
        }

        public void IntersectWith(IEnumerable<T> other)
        {
            throw new NotSupportedException();
        }

        public void ExceptWith(IEnumerable<T> other)
        {
            throw new NotSupportedException();
        }

        public void SymmetricExceptWith(IEnumerable<T> other)
        {
            throw new NotSupportedException();
        }

        public bool SetEquals(IEnumerable<T> other) =>
            Equals(new HSetInternal<T>(other));

        void ICollection<T>.Add(T item)
        {
            throw new NotSupportedException();
        }

        void ICollection<T>.Clear()
        {
            throw new NotSupportedException();
        }

        public void CopyTo(T[] array, int index)
        {
            if (array == null) throw new ArgumentNullException(nameof(array));
            if (index < 0 || index > array.Length) throw new IndexOutOfRangeException();
            if (index + Count > array.Length) throw new IndexOutOfRangeException();

            foreach (var element in this)
            {
                array[index++] = element;
            }
        }

        public void CopyTo(Array array, int index)
        {
            if (array == null) throw new ArgumentNullException(nameof(array));
            if (index < 0 || index > array.Length) throw new IndexOutOfRangeException();
            if (index + Count > array.Length) throw new IndexOutOfRangeException();

            foreach (var element in this)
            {
                array.SetValue(element, index++);
            }
        }

        bool ICollection<T>.Remove(T item)
        {
            throw new NotSupportedException();
        }
    }
}