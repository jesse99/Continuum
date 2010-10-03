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
	// A collection type which allows efficient access to the data using multiple keys.
	// Usage is like this:
	// private MultiIndex<Employee> m_employees = new MultiIndex<Employee>(
	// 		new OrderedNonUnique<string, Employee>("department", e => e.Department),
	// 		new OrderedUnique<string, Employee>("ssn", e => e.Ssn));
	//
	// var deptIndex = m_employees.Indexer<OrderedNonUnique<string, Employee>>("department");
	// IEnumerable<Employee> employees = deptIndex.GetItems("R&D");
	//
	// var ssnIndex = m_employees.Indexer<OrderedUnique<string, Employee>>("ssn");
	// Employee employee = ssnIndex.GetItem(ssn);
	public sealed class MultiIndex<ITEM> : ICollection<ITEM>
	{
		public MultiIndex(params BaseIndexer<ITEM>[] indexers)
		{
			Contract.Requires(indexers != null, "indexers is null");
			Contract.Requires(indexers.Length > 0, "indexers is empty");
			
			foreach (BaseIndexer<ITEM> indexer in indexers)
			{
				Contract.Requires(indexer != null, "indexer is null");
				Contract.Assert(indexer.Collection == null, "indexer is already part of a collection");
				
				m_indexes.Add(indexer.Name, indexer);
				indexer.Collection = this;
			}
		}
		
		public INDEXER Indexer<INDEXER>(string name) //where INDEXER : BaseIndexer<ITEM> TODO: looks like gmcs has a compiler bug triggered when we enable this
		{
			Contract.Requires(!string.IsNullOrEmpty(name), "name is null or empty");
			
			object temp = m_indexes[name];
			
			return (INDEXER) temp;
		}
		
		#region ICollection<ITEM> Members
		public int Count
		{
			get {return m_indexes.Values.First().Count;}
		}
		
		public bool IsReadOnly
		{
			get {return false;}
		}
		
		public void Add(ITEM item)
		{
			foreach (BaseIndexer<ITEM> index in m_indexes.Values)
			{
				index.Add(item);
			}
		}
		
		public void Clear()
		{
			foreach (BaseIndexer<ITEM> index in m_indexes.Values)
			{
				index.Clear();
			}
		}
		
		public bool Contains(ITEM item)
		{
			IEnumerable<ITEM> items = m_indexes.Values.First().GetItems();
			return items.Contains(item);
		}
		
		public void CopyTo(ITEM[] array, int arrayIndex)
		{
			Contract.Requires(array != null, "array is null");
			Contract.Requires(arrayIndex >= 0, "arrayIndex is too small");
			Contract.Requires(arrayIndex + Count <= array.Length, "arrayIndex is too big");
			
			IEnumerable<ITEM> items = m_indexes.Values.First().GetItems();
			
			int i = arrayIndex;
			foreach (ITEM item in items)
			{
				array[i++] = item;
			}
		}
		
		public bool Remove(ITEM item)
		{
			int count = 0;
			
			foreach (BaseIndexer<ITEM> index in m_indexes.Values)
			{
				if (index.Remove(item))
					++count;
			}
			
			Contract.Assert(count == 0 || count == m_indexes.Count, "not all the items were removed");
			
			return count > 0;
		}
		
		internal void SyncRemove(ITEM item, BaseIndexer<ITEM> src)
		{
			foreach (BaseIndexer<ITEM> index in m_indexes.Values)
			{
				if (index != src)
				{
					bool removed = index.Remove(item);
					Contract.Assert(removed);
				}
			}
		}
		#endregion
		
		#region IEnumerable<ITEM> Members
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
		
		public IEnumerator<ITEM> GetEnumerator()
		{
			IEnumerable<ITEM> items = m_indexes.Values.First().GetItems();
			foreach (ITEM item in items)
			{
				yield return item;
			}
		}
		#endregion
		
		#region Fields
		private Dictionary<string, BaseIndexer<ITEM>> m_indexes = new Dictionary<string, BaseIndexer<ITEM>>();
		#endregion
	}
}
