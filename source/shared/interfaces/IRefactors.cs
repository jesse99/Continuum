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

namespace Shared
{
	// Applies refactor commands to a text buffer.
	public interface IRefactors : IInterface
	{
		void Init(string text);
		
		// Adds a base type to a class, interface, or struct.
		void QueueAddBaseType(CsType type, string name);
		
		// Adds a new member to a class, interface, or struct. Note that this does
		// not check to see if a method with that signature exists.
		void QueueAddMember(CsType type, params string[] lines);
		
		// Adds a new member to a class, interface, or struct. Note that this does
		// not check to see if a method with that signature exists.
		void QueueAddRelativeMember(CsMember member, bool after, params string[] lines);
		
		// Adds a new using directive to a namespace.
		void QueueAddUsing(CsNamespace ns, string name);
		
		// Changes the access for a member. 
		void QueueChangeAccess(CsMember member, string access);
		
		// Inserts lines at an arbitrary range within a source code file.
		void QueueIndent(int offset, int len, string tabs);
		
		// Inserts lines at at the line after the line index is within.
		void QueueInsertAfterLine(int index, int length, params string[] lines);
		
		// Inserts lines at at the start of the line index is within.
		void QueueInsertBeforeLine(int index, params string[] lines);
		
		// Inserts lines at the start of a block.
		void QueueInsertFirst(CsBody body, params string[] lines);
		
		// Inserts lines at the end of a block.
		void QueueInsertLast(CsBody body, params string[] lines);
		
		// Executes the commands and returns the new text. Note that
		// the commands may not be executed in the order in which they
		// were queued.
		string Process();
	}
}
