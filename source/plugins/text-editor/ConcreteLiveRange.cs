// Copyright (C) 2008 Jesse Jones
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

using Gear;
using Shared;
using System;
using System.Diagnostics;

namespace TextEditor
{
	internal sealed class ConcreteLiveRange : LiveRange
	{
		public ConcreteLiveRange(Boss boss, int index, int length)
		{
			Trace.Assert(boss != null, "boss is null");
			Trace.Assert(index >= 0, "index is negative");
			Trace.Assert(length >= 0, "length is negative");
			
			m_boss = boss;
			m_index = index;
			m_length = length;
		}
		
		public override Boss Boss
		{
			get {return m_boss;}
		}
		
		public override bool IsValid
		{
			get {return m_index >= 0;}
		}
		
		public override int Index
		{
			get {return m_index;}
		}
		
		public override int Length
		{
			get {return m_length;}
		}
		
		internal void Reset(int index)
		{
			Trace.Assert(index >= -1, "index is too negative");
			
			if (index != m_index)
			{
				m_index = index;
				m_dirty = true;
			}
		}
		
		// LiveRanges are normally associated with a UI, but the UI
		// cannot be updated until the characters have been laid out 
		// again.
		internal void LayoutCompleted()
		{
			if (m_dirty)
			{
				Fire();
				m_dirty = false;
			}
		}
		
		#region Fields 
		private Boss m_boss;
		private int m_index;
		private int m_length;
		private bool m_dirty;
		#endregion
	}
}
