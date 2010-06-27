// Copyright (C) 2010 Jesse Jones
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

using MObjc.Helpers;
using Mono.Debugger.Soft;
using Shared;
using System;
using System.Collections.Generic;

using Debug = Debugger;

namespace Debugger
{
	// The values within an IEnumerable collection.
	// TODO: It seems like this code should work, but DoLoad returns too many values and the
	// values are all null. 
#if DOES_NOT_WORK
	internal sealed class EnumerableValue
	{
		public EnumerableValue(VariableItem parentItem, ObjectMirror parent)
		{
			Contract.Requires(parentItem != null);
			Contract.Requires(parent != null);
			
			Parent = parentItem;
			Instance = parent;
			Type = parent.Type;
		}
		
		public VariableItem Parent {get; private set;}

		public ObjectMirror Instance {get; private set;}
		
		public int Length
		{
			get
			{
				Contract.Assert(m_children != null);
					
				return m_children.Count;
			}
		}
		
		public TypeMirror Type {get; private set;}
		
		public string GetText(ThreadMirror thread)
		{
			return string.Empty;
		}
		
		public VariableItem GetChild(ThreadMirror thread, VariableItem parent, int index)
		{
			return m_children[index];
		}
		
		public void Reload(ThreadMirror thread)
		{
			DoLoad(thread);
		}
		
		#region Private Methods
		private void DoLoad(ThreadMirror thread)	// TODO: need to trap exceptions here
		{
			m_children = new List<VariableItem>();
			
			Value enumerator = new InvokeMethod().UnsafeInvoke(thread, Instance, "GetEnumerator");
			while (true)
			{
				Value moved = new InvokeMethod().UnsafeInvoke(thread, enumerator, "MoveNext");
				var pv = (PrimitiveValue) moved;
				if (pv.Equals(false))
				{
					break;
				}
				
				string name = m_children.Count.ToString();
				if (m_children.Count == 20)
				{
					m_children.Add(new VariableItem(thread, name, Parent, this, "…", m_children.Count));
					break;
				}
				
				Value value = EvalMember.Evaluate(thread, enumerator, "Current");
				m_children.Add(new VariableItem(thread, name, Parent, this, value, m_children.Count));
			}
		}
		#endregion
		
		#region Fields
		private List<VariableItem> m_children;
		#endregion
	}
#endif	// DOES_NOT_WORK
}
