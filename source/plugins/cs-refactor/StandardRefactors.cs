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

using Gear;
using Gear.Helpers;
using Shared;
using System;
//using System.Diagnostics;

namespace CsRefactor
{
	internal sealed class StandardRefactors : IRefactors
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public void Init(string text)
		{
			Contract.Assert(m_refactor == null, "Create a new boss instance instead of calling Init multiple times");
			
			m_refactor = new Refactor(text);
		}
		
		public void QueueAddBaseType(CsType type, string name)
		{
			m_refactor.Queue(new AddBaseType(type, name));
		}
		
		public void QueueAddMember(CsType type, params string[] lines)
		{
			m_refactor.Queue(new AddMember(type, lines));
		}
		
		public void QueueAddRelativeMember(CsMember member, bool after, params string[] lines)
		{
			m_refactor.Queue(new AddRelativeMember(member, after, lines));
		}
		
		public void QueueAddUsing(CsNamespace ns, string name)
		{
			m_refactor.Queue(new AddUsing(ns, name));
		}
		
		public void QueueChangeAccess(CsMember member, string access)
		{
			m_refactor.Queue(new ChangeAccess(member, access));
		}
		
		public void QueueIndent(int offset, int len, string tabs)
		{
			m_refactor.Queue(new Indent(offset, len, tabs));
		}
		
		public void QueueInsertAfterLine(int index, int length, params string[] lines)
		{
			m_refactor.Queue(new InsertAfterLine(index, length, lines));
		}
		
		public void QueueInsertBeforeLine(int index, params string[] lines)
		{
			m_refactor.Queue(new InsertBeforeLine(index, lines));
		}
		
		public void QueueInsertFirst(CsBody body, params string[] lines)
		{
			m_refactor.Queue(new InsertFirst(body, lines));
		}
		
		public void QueueInsertLast(CsBody body, params string[] lines)
		{
			m_refactor.Queue(new InsertLast(body, lines));
		}
		
		public string Process()
		{
			return m_refactor.Process();
		}
		
		#region Fields
		private Boss m_boss;
		private Refactor m_refactor;
		#endregion
	}
}
