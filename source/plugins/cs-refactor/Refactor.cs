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

using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace CsRefactor
{
	// The class that does the actual work of refactoring C# code.
	public sealed class Refactor
	{
		public Refactor(string text)
		{
			Contract.Requires(text != null, "text is null");
			
			m_text = text;
		}
		
		public void Queue(RefactorCommand command)
		{
			Contract.Requires(command != null, "command is null");
			
			m_commands.Add(command);
		}
		
		// Executes the queued commands and returns the new text.
		public string Process()
		{
			StringBuilder buffer = new StringBuilder(m_text);
			foreach (RefactorCommand command in m_commands)
			{
				command.Preflight(buffer);
			}
			
			// We'd like commands that insert text at the same offset (like AddMember)
			// to run in the order they were declared in so before sorting we'll reverse
			// the list.
			m_commands.Reverse();
			
			// Apply the commands from the back to the front so that we don't
			// invalidate offsets.
			m_commands.StableSort((lhs, rhs) => rhs.Offset.CompareTo(lhs.Offset));
			
			// Edits cannot overlap (if they do it becomes much harder to
			// properly compose them).
			for (int i = 0; i < m_commands.Count - 1; ++i)
			{
				if (DoIntersects(m_commands[i], m_commands[i + 1].Offset, m_commands[i + 1].Length) || DoIntersects(m_commands[i + 1], m_commands[i].Offset, m_commands[i].Length))
					throw new InvalidOperationException(string.Format("{0} and {1} edits overlap.", m_commands[i].GetType().Name, m_commands[i+1].GetType().Name));
			}
			
			// Give edits a chance to (sanely) affect each other.
			RefactorCommand[] a = m_commands.ToArray();
			for (int i = 0; i < m_commands.Count; ++i)
			{
				m_commands[i].PreExecute(a, i);
			}
			
			// Apply the edits.
			foreach (RefactorCommand command in m_commands)
			{
				command.Execute(buffer);
			}
			
			return buffer.ToString();
		}
		
		#region Private Method
		private bool DoIntersects(RefactorCommand command, int offset, int length)
		{
			if (length > 0)
				return offset >= command.Offset && offset < command.Offset + command.Length;
			else
				return offset > command.Offset && offset < command.Offset + command.Length;
		}
		#endregion
		
		#region Fields
		private string m_text;
		private List<RefactorCommand> m_commands = new List<RefactorCommand>();
		#endregion
	}
}
