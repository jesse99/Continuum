// Copyright (C) 2009 Jesse Jones
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using Gear.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Shared
{
	// TODO: Might want to add a Replace method to allow more efficient modification
	// of items then a remove followed by add.
	public abstract class BaseIndexer<ITEM>
	{
		protected BaseIndexer(string name)
		{
			Contract.Requires(!string.IsNullOrEmpty(name), "name is null or empty");
			
			Name = name;
		}
		
		public string Name {get; private set;}
		
		public MultiIndex<ITEM> Collection {get; internal set;}
		
		public abstract int Count {get;}
		
		public abstract IEnumerable<ITEM> GetItems();
		
		#region Internal Methods
		internal abstract void Add(ITEM item);
		
		internal abstract void Clear();
		
		internal abstract bool Remove(ITEM item);
		#endregion
	}
	
	// These are used with MultiIndex. Indexers can be unique which means that
	// each key in the collection appears only once and/or ordered which means
	// that the key implements IComparable<ITEM> and the items returned by 
	// GetItems will be ordered using the key. Complexity is O(log N) or better
	// for all operations unless otherwise noted.
	public abstract class Indexer<KEY, ITEM> : BaseIndexer<ITEM>
	{
		protected Indexer(string name) : base(name)
		{
		}
		
		public abstract IEnumerable<ITEM> GetItems(KEY key);
		
		// Note that these may not be distinct if the indexer is not unique.
		public abstract IEnumerable<KEY> GetKeys();
		
		public abstract bool ContainsItem(KEY key);
		
		public abstract bool RemoveItems(KEY key);
	}
	
	public abstract class UniqueIndexer<KEY, ITEM> : Indexer<KEY, ITEM>
	{
		protected UniqueIndexer(string name, Func<ITEM, KEY> key, IDictionary<KEY, ITEM> items) : base(name)
		{
			m_key = key;
			m_items = items;
		}
		
		// all indexer methods
		public override bool ContainsItem(KEY key)
		{
			return m_items.ContainsKey(key);
		}
		
		public override int Count
		{
			get {return m_items.Count;}
		}
		
		public override IEnumerable<ITEM> GetItems()
		{
			foreach (ITEM item in m_items.Values)
			{
				yield return item;
			}
		}
		
		public override IEnumerable<ITEM> GetItems(KEY key)
		{
			ITEM item;
			if (m_items.TryGetValue(key, out item))
				yield return item;
		}
		
		public override IEnumerable<KEY> GetKeys()
		{
			foreach (KEY key in m_items.Keys)
			{
				yield return key;
			}
		}
		
		public override bool RemoveItems(KEY key)
		{
			bool removed = false;
			
			ITEM item;
			if (m_items.TryGetValue(key, out item))
			{
				Collection.SyncRemove(item, this);
				m_items.Remove(key);
				removed = true;
			}
			
			return removed;
		}
		
		internal override void Add(ITEM item)
		{
			Contract.Assert(!ReferenceEquals(item, null), "null items cannot be given unique keys");
			
			KEY key = m_key(item);
			m_items.Add(key, item);
		}
		
		internal override void Clear()
		{
			m_items.Clear();
		}
		
		internal override bool Remove(ITEM item)
		{
			KEY key = m_key(item);
			return m_items.Remove(key);
		}
		
		// unique indexer methods
		public ITEM GetItem(KEY key)
		{
			return m_items[key];
		}
		
		public bool TryGetItem(KEY key, out ITEM item)
		{
			return m_items.TryGetValue(key, out item);
		}
		
		public void RemoveItem(KEY key)
		{
			Collection.SyncRemove(m_items[key], this);
			m_items.Remove(key);
		}
		
		private Func<ITEM, KEY> m_key;
		private IDictionary<KEY, ITEM> m_items;
	}
	
	public sealed class UnorderedUnique<KEY, ITEM> : UniqueIndexer<KEY, ITEM>
	{
		// Note that the hash code of the object returned by the key function should
		// not change for an item while it is in the collection.
		public UnorderedUnique(string name, Func<ITEM, KEY> key)
			: base(name, key, new Dictionary<KEY, ITEM>())
		{
		}
	}
	
	public sealed class OrderedUnique<KEY, ITEM> : UniqueIndexer<KEY, ITEM> where KEY : IComparable<KEY>
	{
		public OrderedUnique(string name, Func<ITEM, KEY> key)
			: base(name, key, new SortedDictionary<KEY, ITEM>())
		{
		}
	}
	
	public sealed class UnorderedNonUnique<KEY, ITEM> : Indexer<KEY, ITEM>
		where KEY: IEquatable<KEY>
	{
		public UnorderedNonUnique(string name, Func<ITEM, KEY> key) : base(name)
		{
			Contract.Requires(key != null, "key is null");
			
			m_key = key;
		}
		
		// O(N) complexity.
		public override bool ContainsItem(KEY key)
		{
			return GetItems(key).Any();
		}
		
		public override int Count
		{
			get {return m_items.Count;}
		}
		
		public override IEnumerable<ITEM> GetItems()
		{
			foreach (ITEM item in m_items)
			{
				yield return item;
			}
		}
		
		public override IEnumerable<ITEM> GetItems(KEY key)
		{
			foreach (ITEM item in m_items)
			{
				KEY candidate = m_key(item);
				if ((key == null && candidate == null) || (key != null && key.Equals(candidate)))
					yield return item;
			}
		}
		
		public override IEnumerable<KEY> GetKeys()
		{
			foreach (ITEM item in m_items)
			{
				KEY key = m_key(item);
				yield return key;
			}
		}
		
		// O(N) complexity.
		public override bool RemoveItems(KEY key)
		{
			bool removed = false;
			
			for (int i = m_items.Count - 1; i >= 0; --i)
			{
				KEY candidate = m_key(m_items[i]);
				if ((key == null && candidate == null) || (key != null && key.Equals(candidate)))
				{
					Collection.SyncRemove(m_items[i], this);
					m_items.RemoveAt(i);
					removed = true;
				}
			}
			
			return removed;
		}
		
		internal override void Add(ITEM item)
		{
			m_items.Add(item);
		}
		
		internal override void Clear()
		{
			m_items.Clear();
		}
		
		internal override bool Remove(ITEM item)
		{
			return m_items.Remove(item);
		}
		
		private Func<ITEM, KEY> m_key;
		private List<ITEM> m_items = new List<ITEM>();
	}
	
	public sealed class OrderedNonUnique<KEY, ITEM> : Indexer<KEY, ITEM>
		where KEY : IComparable<KEY>
	{
		public OrderedNonUnique(string name, Func<ITEM, KEY> key) : base(name)
		{
			Contract.Requires(key != null, "key is null");
			
			m_key = key;
		}
		
		public override bool ContainsItem(KEY key)
		{
			if (m_dirty)
				DoSort();
				
			int i = m_items.BinarySearch(default(ITEM), new Comparer(key, m_key));
			
			return i >= 0 && i < m_items.Count;
		}
		
		public override int Count
		{
			get {return m_items.Count;}
		}
		
		public override IEnumerable<ITEM> GetItems()
		{
			foreach (ITEM item in m_items)
			{
				yield return item;
			}
		}
		
		public override IEnumerable<ITEM> GetItems(KEY key)
		{
			foreach (int i in DoGetRange(key))
			{
				yield return m_items[i];
			}
		}
		
		public override IEnumerable<KEY> GetKeys()
		{
			if (m_dirty)
				DoSort();
				
			foreach (ITEM item in m_items)
			{
				KEY key = m_key(item);
				yield return key;
			}
		}
		
		public override bool RemoveItems(KEY key)
		{
			var deathRow = new List<int>(DoGetRange(key));
			
			foreach (int i in deathRow)
			{
				Collection.SyncRemove(m_items[i], this);
				m_items.RemoveAt(i);
			}
			
			return deathRow.Count > 0;
		}
		
		internal override void Add(ITEM item)
		{
			m_items.Add(item);
			m_dirty = true;
		}
		
		internal override void Clear()
		{
			m_items.Clear();
			m_dirty = false;
		}
		
		internal override bool Remove(ITEM item)
		{
			KEY key = m_key(item);
			foreach (int i in DoGetRange(key))
			{
				if ((item == null && m_items[i] == null) || (item != null && item.Equals(m_items[i])))
				{
					m_items.RemoveAt(i);
					return true;
				}
			}
			
			return false;
		}
		
		// This could be optimized a bit.
		private IEnumerable<int> DoGetRange(KEY key)
		{
			if (m_dirty)
				DoSort();
			
			var c = new Comparer(key, m_key);
			int i = m_items.BinarySearch(default(ITEM), c);
			if (i >= 0 && i < m_items.Count)
			{
				int k = i;
				while (k < m_items.Count && c.Compare(default(ITEM), m_items[k]) == 0)
				{
					yield return k;
					++k;
				}
				
				k = i - 1;
				while (k >= 0 && c.Compare(default(ITEM), m_items[k]) == 0)
				{
					yield return k;
					--k;
				}
			}
		}
		
		private void DoSort()
		{
			m_items.Sort((lhs, rhs) =>
			{
				IComparable l = (IComparable) m_key(lhs);
				KEY r = m_key(rhs);
				if (l == null && r == null)
					return 0;
				else if (l == null)
					return -1;
				else
					return l.CompareTo(r);
			});
			
			m_dirty = false;
		}
		
		private sealed class Comparer : IComparer<ITEM>
		{
			public Comparer(KEY left, Func<ITEM, KEY> key)
			{
				m_left = left;
				m_key = key;
			}
			
			public int Compare(ITEM lhs, ITEM rhs)
			{
				KEY right = m_key(rhs);
				if (m_left == null && right == null)
					return 0;
				else if (m_left == null)
					return -1;
				else
					return m_left.CompareTo(right);
			}
			
			private KEY m_left;
			private Func<ITEM, KEY> m_key;
		}
		
		private Func<ITEM, KEY> m_key;
		private List<ITEM> m_items = new List<ITEM>();
		private bool m_dirty;
	}
	
	// Allows access to the data in the order in which it was added.
	public abstract class RandomAccessIndexer<ITEM> : Indexer<int, ITEM>
	{
		protected RandomAccessIndexer(string name) : base(name)
		{
		}
		
		// all indexer methods
		public override bool ContainsItem(int index)
		{
			return index >= 0 && index < m_items.Count;
		}
		
		public override int Count
		{
			get {return m_items.Count;}
		}
		
		public override IEnumerable<ITEM> GetItems(int index)
		{
			if (index >= 0 && index < m_items.Count)
				yield return m_items[index];
		}
		
		public override bool RemoveItems(int index)
		{
			bool removed = false;
			
			if (index >= 0 && index < m_items.Count)
			{
				Collection.SyncRemove(m_items[index], this);
				m_items.RemoveAt(index);
				removed = true;
			}
			
			return removed;
		}
		
		public override IEnumerable<int> GetKeys()
		{
			for (int index = 0; index < m_items.Count; ++index)
			{
				yield return index;
			}
		}
		
		internal override void Add(ITEM item)
		{
			m_items.Add(item);
		}
		
		internal override void Clear()
		{
			m_items.Clear();
		}
		
		internal override bool Remove(ITEM item)
		{
			return m_items.Remove(item);
		}
		
		// random access methods
		public ITEM GetItem(int index)
		{
			return m_items[index];
		}
		
		public void RemoveItem(int index)
		{
			m_items.RemoveAt(index);
		}
		
		private List<ITEM> m_items = new List<ITEM>();
	}
}
