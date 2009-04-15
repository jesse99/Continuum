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
	// Base class for commands used by Refactor to transform C# source code.
	public abstract class RefactorCommand
	{
		// Returns the first character index which the command will affect.
		public int Offset {get {return m_offset;}}
		
		// The number of characters in the text affected by the command. This includes
		// removing or changing characters, inserting characters after offset, but not
		// inserting new characters at offset.
		public int Length {get {return m_length;}}
		
		public void Preflight(StringBuilder builder)
		{
			Contract.Requires(builder != null, "builder is null");
			
			int oldLen = builder.Length;
			OnFindRange(builder, out m_offset, out m_length);
			Contract.Ensures(oldLen == builder.Length, "OnFindRange should not change the builder");
		}
		
		// Allows commands to change what they insert (but not where they insert) based on
		// the other commands to be executed. I is the index of this. Commands after index
		// i are executed after this.
		public virtual void PreExecute(RefactorCommand[] commands, int i)
		{
			Contract.Requires(commands != null, "commands is null");
		}
		
		// Sequence is: 1) call Preflight for all commands 2) call PreExecute for all commands
		// 3) call Execute for all commands.
		public void Execute(StringBuilder builder)
		{
			Contract.Requires(builder != null, "builder is null");

			if (Offset >= 0)
			{
				m_builder = builder;
				OnExecute();
				m_builder = null;
			}
		}
		
		#region Protected Methods
		// OnFindRange can examine any of the characters in the builder, but should not
		// change or cache the builder. This can return a negative offset if the command
		// does not need to be run.
		protected abstract void OnFindRange(StringBuilder builder, out int offset, out int length);
		
		// Note that it's important that commands be independent of each other so that
		// the edits that they make do not conflict with each other. This means that
		// OnExecute cannot look at characters at indices >= Offset or change characters
		// at indices > Offset. To ensure that this invariant is met all changes to the
		// builder should go through RefactorCommand.
		protected abstract void OnExecute();
		
		// Returns the offset which starts the line the specified offset is within. Either the
		// returned offset is zero or it minus one is \n.
		protected int FindLineStart(StringBuilder builder, int offset)
		{
			Contract.Requires(builder != null, "builder is null");

			while (offset > 0)
			{
				if (builder[offset - 1] == '\n')
					break;
				--offset;
			}
			return offset;
		}
		
		// Returns the starting offset for the line after the one the specified offset is within. 
		// Either the returned offset is builder.Length or it minus one is \n.
		protected int FindNextLineStart(StringBuilder builder, int offset)
		{
			Debug.Assert(builder != null, "builder is null");

			int end = offset;
			
			while (end < builder.Length && builder[end] != '\n')
				++end;
			
			if (end < builder.Length && builder[end] == '\n')
				++end;
				
			return end;
		}
		
		// Returns the whitespace starting at the specified index.
		protected string GetIndent(int start)
		{
			return DoGetIndent(start, Offset + Length);
		}
		
		protected void InsertText(string text)
		{
			m_builder.Insert(Offset, text);
		}
		
		protected void ReplaceText(string text)
		{
			m_builder.Remove(Offset, Length);
			m_builder.Insert(Offset, text);
		}
		
		protected void AddLines(string[] lines)
		{
			Contract.Requires(lines != null, "lines is null");
			Contract.Requires(Offset == 0 || m_builder[Offset - 1] == '\n', "offset is not at the start of a line");
			
			int previous = DoFindPrevLineStart(Offset);	// note that we can't use the current line because we can't safely peek past Offset
			string indent = DoGetIndent(previous, m_builder.Length);
			
			int offset = Offset;
			foreach (string line in lines)
			{
				m_builder.Insert(offset, indent);
				offset += indent.Length;
				
				m_builder.Insert(offset, line);
				offset += line.Length;
				
				m_builder.Insert(offset, '\n');
				offset += 1;
			}
		}
		
		protected string LinesToString(string[] lines)
		{
			Contract.Requires(lines != null, "lines is null");

			string line1 = lines.Length > 0 ? lines[0] : string.Empty;
			string line2 = lines.Length > 1 ? lines[1] : string.Empty;
			
			if (line1.Length > 40)
				line1 = line1.Substring(0, 40) + "...";
			if (line2.Length > 40)
				line2 = line2.Substring(0, 40) + "...";
			
			if (lines.Length == 0)
				return "<no lines>";
			
			else if (lines.Length == 1)
				return line1;
			
			else if (lines.Length == 2)
				return string.Format("1: {0}, 2: {1}", line1, line2);
			
			else
				return string.Format("1: {0}, 2: {1}, ...", line1, line2);
		}
		#endregion
		
		#region Fields		
		// Returns the offset of the start of the previous line offset is within.  
		// Either the returned offset is zero or it minus one is \n.
		private int DoFindPrevLineStart(int first)
		{
			Debug.Assert(first == 0 || m_builder[first - 1] == '\n', "first is not at the start of a line");
			
			if (first > 0)
			{
				--first;
				while (first > 0 && m_builder[first - 1] != '\n')
					--first;
			}
			
			return first;
		}
		
		private string DoGetIndent(int start, int max)
		{
			Contract.Requires(start <= max, "trying to scan into characters that may have been edited");
			Contract.Requires(start == 0 || m_builder[start - 1] == '\n', "start is not at the start of a line");
			
			string indent = string.Empty;
			
			int index = start;
			if (index < m_builder.Length && (m_builder[index] == ' ' || m_builder[index] == '\t'))
			{
				while (index < m_builder.Length && (m_builder[index] == ' ' || m_builder[index] == '\t'))
					++index;
				
				indent = m_builder.ToString(start, index - start);
			}
			Contract.Assert(index <= max, "scanning into characters that may have been edited");
			
			// Line of whitespace with a { on the end.
			if (index < m_builder.Length && m_builder[index] == '{')
			{
				indent += "\t";
			}
			else
			{
				// Line has printable characters and ends with a {.
				while (index < m_builder.Length && m_builder[index] != '{' && m_builder[index] != '\n')
					++index;
				
				if (index < m_builder.Length && m_builder[index] == '{')
				{
					++index;
					while (index < m_builder.Length && (m_builder[index] == ' ' || m_builder[index] == '\t'))
						++index;
					
					if (index < m_builder.Length && m_builder[index] == '\n')
						indent += "\t";
				}
				Contract.Assert(index <= max, "scanning into characters that may have been edited");
			}
			
			return indent;
		}
		#endregion
		
		#region Fields
		private int m_offset;
		private int m_length;
		private StringBuilder m_builder;
		#endregion
	} 
}
