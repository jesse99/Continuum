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
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace CsRefactor
{
	// Adds a new base type to a class, interface, or struct.
	public sealed class AddBaseType : RefactorCommand
	{
		public AddBaseType(CsType type, string name)	
		{
			Contract.Requires(type != null, "type is null");
			Contract.Requires(!string.IsNullOrEmpty(name), "name is null or empty");
			
			m_type = type;
			m_name = name;
		}
		
		// This will execute after OnFindRange.
		public override void PreExecute(RefactorCommand[] commands, int i)
		{
			Contract.Requires(commands != null, "commands is null");
			
			// If there are no bases and we're the last AddBase command then we
			// need to use a colon for the prefix, otherwise we need to use a comma.
			if (m_type.Bases.Names.Length == 0)
			{
				if (i + 1 < commands.Length && Array.FindIndex(commands, i + 1, c => c is AddBaseType) >= 0)
					m_prefix = ", ";
				else
					m_prefix = " : ";
			}
		}
		
		protected override void OnFindRange(StringBuilder builder, out int offset, out int length)
		{
			// Type already includes base.
			if (m_type.Bases.Names.Contains(m_name))
			{
				offset = -1;
			}
			
			// Type has no bases.
			else if (m_type.Bases.Names.Length == 0)
			{
				offset = m_type.Bases.Offset;
				m_prefix = " : ";
			}
			
			// Base class always goes to the front.
			else if (!CsHelpers.IsInterface(m_name))
			{
				offset = m_type.Bases.Offset;
				m_suffix = ", ";
			}
			
			// Bases are sorted.
			else if (m_type.Bases.Names.Length > 1 && DoIsSorted(m_type.Bases.Names))
			{
				offset = DoFindSortedOffset(builder);
				if (offset < m_type.Bases.Offset + m_type.Bases.Length)
					m_suffix = ", ";
				else
					m_prefix = ", ";
			}
			
			// Bases are not sorted.
			else
			{
				offset = m_type.Bases.Offset + m_type.Bases.Length;
				m_prefix = ", ";
			}
			
			length = 0;
		}
		
		protected override void OnExecute()
		{
			Log.WriteLine("Refactor Commands", "AddBaseType type={0}, name={1}", m_type.Name, m_name);
			
			InsertText(m_prefix + m_name + m_suffix);
		}
		
		public override string ToString()
		{
			return string.Format("{0}.AddBase({1})", m_type.Name, m_name);
		}
		
		#region Private Methods
		private int DoFindSortedOffset(StringBuilder builder)
		{
			int offset = m_type.Bases.Offset;
			
			int start = CsHelpers.IsInterface(m_type.Bases.Names[0]) ? 0 : 1;		// ignore base type if any
			for (int i = start; i < m_type.Bases.Names.Length; ++i)
			{
				if (m_type.Bases.Names[i].CompareTo(m_name) > 0)
				{
					string name = m_type.Bases.Names[i];
					
					string text = builder.ToString(offset, m_type.Bases.Length);
					int k = text.IndexOf(name);
					return offset + k;
				}
			}
			
			return offset + m_type.Bases.Length;
		}
		
		private bool DoIsSorted(string[] names)
		{
			int start = CsHelpers.IsInterface(names[0]) ? 1 : 2;		// only interfaces affect sorting
			for (int i = start; i < names.Length; ++i)
			{
				if (names[i - 1].CompareTo(names[i]) > 0)
					return false;
			}
			
			return true;
		}
		#endregion
		
		#region Fields
		private readonly CsType m_type;
		private readonly string m_name;
		private string m_prefix = string.Empty;
		private string m_suffix = string.Empty;
		#endregion
	}
	
	// Adds a new member to a class, interface, or struct. Note that this does
	// not check to see if a method with that signature exists.
	public sealed class AddMember : RefactorCommand
	{
		public AddMember(CsType type, params string[] lines)
		{
			m_type = type;
			m_lines = DoMungeLines(lines);
		}
		
		protected override void OnFindRange(StringBuilder builder, out int offset, out int length)
		{
			m_first = FindLineStart(builder, m_type.Body.First);
			
			// This is the normal case: the start of the line is within the body.			
			if (m_first > m_type.Body.Start)
			{
				offset = m_first;
				length = 0;
			}
			
			// This case will happen if the body is all on one line, e.g. "{}".
			else
			{
				offset = m_type.Body.First;
				length = 0;
			}
		}
		
		protected override void OnExecute()
		{
			Log.WriteLine("Refactor Commands", "AddMember type={0}, name={1}", m_type.Name, LinesToString(m_lines));
				
			if (m_first > m_type.Body.Start)
				AddLines(m_lines);
			else
				InsertText(DoGetEmptyString());
		}
		
		public override string ToString()
		{
			return string.Format("{0}.AddMember({1})", m_type.Name, LinesToString(m_lines));
		}
		
		private string DoGetEmptyString()
		{
			var builder = new StringBuilder();
			
			builder.Append('\n');
			
			string indent = GetIndent(m_first);
			foreach (string line in m_lines)
			{
				builder.Append(indent);
				builder.Append(line);
				builder.Append('\n');
			}
			
			if (indent.Length > 0)
				builder.Append(indent.Remove(0, 1));
			
			return builder.ToString();
		}
		
		// If the type has declarations we want to append an empty line.
		private string[] DoMungeLines(string[] lines)
		{
			string[] result = lines;
			
			if (m_type.Declarations.Length > 0)
			{
				result = new string[lines.Length + 1];
				lines.CopyTo(result, 0);
				result[lines.Length] = string.Empty;
			}
			
			return result;
		}
		
		private CsType m_type;
		private string[] m_lines;
		private int m_first;
	}
	
	// Adds a new member to a class, interface, or struct. Note that this does
	// not check to see if a method with that signature exists.
	public sealed class AddRelativeMember : RefactorCommand
	{
		public AddRelativeMember(CsMember member, bool after, params string[] lines) 
			// : base(after ? member.Offset + member.Length + 1 : member.Offset, DoMungeLines(after, lines))
		{
			m_after = after;
			m_member = member;
			m_lines = DoMungeLines(lines);
		}
		
		protected override void OnFindRange(StringBuilder builder, out int offset, out int length)
		{
			int index;
			if (m_after)
			{
				index = m_member.Offset + m_member.Length + 1;
			}
			else
			{
				index = m_member.Offset;
			}
			
			offset = FindLineStart(builder, index);
			length = 0;
		}
		
		protected override void OnExecute()
		{
			if (m_after)
				Log.WriteLine("Refactor Commands", "AddMemberAfter member={0}, name={1}", m_member.Name, LinesToString(m_lines));
			else
				Log.WriteLine("Refactor Commands", "AddMemberBefore member={0}, name={1}", m_member.Name, LinesToString(m_lines));
				
			AddLines(m_lines);
		}
		
		public override string ToString()
		{
			return string.Format("{0}.AddRelativeMember({1})", m_member.Name, LinesToString(m_lines));
		}
		
		// Add a blank line before or after the new text
		private string[] DoMungeLines(string[] lines)
		{
			string[] result = lines;
			
			if (m_after)
			{
				result = new string[lines.Length + 1];
				lines.CopyTo(result, 1);
				result[0] = string.Empty;
			}
			else
			{
				result = new string[lines.Length + 1];
				lines.CopyTo(result, 0);
				result[lines.Length] = string.Empty;
			}
			
			return result;
		}
		
		private bool m_after;
		private CsMember m_member;
		private string[] m_lines;
	}
	
	// Adds a new using directive to a namespace.
	public sealed class AddUsing : RefactorCommand
	{
		public AddUsing(CsNamespace ns, string name)	
		{
			Contract.Requires(ns != null, "ns is null");
			Contract.Requires(!string.IsNullOrEmpty(name), "name is null or empty");
			
			m_namespace = ns;
			m_name = name;
		}
		
		protected override void OnFindRange(StringBuilder builder, out int offset, out int length)
		{
			if (m_namespace.Uses.Any(u => u.Namespace == m_name))
			{
				offset = -1;
			}
			// If the namespace is not already being used then,
			else
			{
				// if some namespaces are already being used,
				if (m_namespace.Uses.Length > 0)
				{
					offset = -1;
					
					// and they are sorted,
					if (m_namespace.Uses.Length > 1 && DoIsSorted(m_namespace.Uses))
					{
						// then insert the new namespace before the next largest one.
						CsUsingDirective u = m_namespace.Uses.FirstOrDefault(c => c.Namespace.CompareTo(m_name) > 0);
						if (u != null)
						{
							offset = FindLineStart(builder, u.Offset);
						}
					}
					
					// If they are not sorted or the namespace is larger than any of the existing
					// namespaces then add the namespace after the last using.
					if (offset < 0)
					{
						CsUsingDirective last = m_namespace.Uses[m_namespace.Uses.Length - 1];
						offset = FindNextLineStart(builder, last.Offset + last.Length);
					}
				}
				
				// If we don't have namespaces but we do have a declaration then add the
				// namespace before the declaration.
				else if (m_namespace.Declarations.Length > 0)
				{
					offset = FindLineStart(builder, m_namespace.Declarations[0].Offset);
				}
				
				// If the namespace is empty (but is not the global namespace) then add the
				// new namespace just before the trailing '}'.
				else if (m_namespace.Name != "<globals>")
				{
					offset = FindLineStart(builder, m_namespace.Body.Last);
				}
				
				// If the namespace is the global namespace and it is empty then add the
				// namespace to the start.
				else
				{
					offset = FindLineStart(builder, m_namespace.Offset);
				}
			}
			
			length = 0;
		}
		
		protected override void OnExecute()
		{
			Log.WriteLine("Refactor Commands", "AddUsing namespace={0}, name={1}", m_namespace.Name, m_name);
			
			string line;
			if (m_namespace.Uses.Length == 0 && m_namespace.Declarations.Length > 0)
				line = string.Format("using {0};\n", m_name);
			else
				line = string.Format("using {0};", m_name);
			AddLines(new string[]{line});
		}
		
		public override string ToString()
		{
			return string.Format("{0}.AddUsing({1})", m_namespace.Name, m_name);
		}
		
		#region Private Methods
		private bool DoIsSorted(CsUsingDirective[] uses)
		{
			for (int i = 1; i < uses.Length; ++i)
			{
				if (uses[i - 1].Namespace.CompareTo(uses[i].Namespace) > 0)
					return false;
			}
			
			return true;
		}
		#endregion
		
		#region Fields
		private readonly CsNamespace m_namespace;
		private readonly string m_name;
		#endregion
	}
	
	// Changes the access for a member. 
	public sealed class ChangeAccess : RefactorCommand
	{
		public ChangeAccess(CsMember member, string access)
		{
			Contract.Requires(member != null, "member is null");
			Contract.Requires(!string.IsNullOrEmpty(access), "access is null or empty");
			
			m_access = access;
			m_member = member;
		}
		
		protected override void OnFindRange(StringBuilder builder, out int offset, out int length)
		{
			Contract.Requires(builder != null, "builder is null");
			
			// Default to no accessor case which is just a simple insert.
			offset = m_member.Offset;
			length = 0;
			
			// But if an accessor is present we'll need to replace it.
#if TEST
			IScanner scanner = new CsParser.Scanner();
#else
			Boss boss = ObjectModel.Create("CsParser");
			var scanner = boss.Get<IScanner>();
#endif
			scanner.Init(builder.ToString(), offset);
			
			string[] candidates = new string[]{"public", "protected", "internal", "private"};
			while (scanner.Token.Kind == TokenKind.Identifier)
			{
				if (candidates.Any(c => scanner.Token == c))
				{
					offset = scanner.Token.Offset;
					length = scanner.Token.Length + 1;
					break;
				}
				scanner.Advance();
			}
		}
		
		protected override void OnExecute()
		{
			Log.WriteLine("Refactor Commands", "ChangeAccess to='{0}'", m_access);
			
			if (Length == 0)
				InsertText(m_access + " ");
			else
				ReplaceText(m_access + " ");
		}
		
		public override string ToString()
		{
			return string.Format("{0}.ChangeAccess({1})", m_member.Name, m_access);
		}
		
		private CsMember m_member;
		private string m_access;
	}
	
	// Inserts lines at an arbitrary range within a source code file.
	public sealed class Indent : RefactorCommand
	{
		public Indent(int offset, int len, string tabs)
		{
			Contract.Requires(offset >= 0, "offset is negative");
			Contract.Requires(len >= 0, "len is negative");
			Contract.Requires(tabs != null, "tabs is null");
			
			m_offset = offset;
			m_len = len;
			m_tabs = tabs;
		}
		
		protected override void OnFindRange(StringBuilder builder, out int offset, out int length)
		{
			if (m_tabs.Length > 0 && m_len > 0)
			{
				int first = FindLineStart(builder, m_offset);
				
				offset = first;
				length = m_len + m_offset - first - 1;
				
				while (offset + length < builder.Length && builder[offset + length] != '\n')
					++length;
				
				m_newText = builder.ToString(offset, length);
				m_newText = m_tabs + m_newText.Replace("\n", "\n" + m_tabs);
			}
			else
			{
				offset = -1;
				length = 0;
			}
		}
		
		protected override void OnExecute()
		{
			Log.WriteLine("Refactor Commands", "Indent offset={0}, len={1}, tabs='{2}'", m_offset, m_len, m_tabs);
			
			ReplaceText(m_newText);
		}
		
		public override string ToString()
		{
			return string.Format("Indent({0}, {1}, \"{2}\")", m_offset, m_len, m_tabs);
		}
		
		#region Fields
		private readonly int m_offset;
		private readonly int m_len;
		private readonly string m_tabs;
		private string m_newText;
		#endregion
	}
	
	// Inserts lines at at the line after the line index is within.
	public sealed class InsertAfterLine : RefactorCommand
	{
		public InsertAfterLine(int index, int length, params string[] lines)
		{
			m_index = index;
			m_length = length;
			m_lines = lines;
		}
		
		protected override void OnFindRange(StringBuilder builder, out int offset, out int length)
		{
#if DEBUG
			Contract.Requires(builder != null, "builder is null");
#endif
			
			int index = m_index > 0 && m_length > 0 && builder[m_index - 1] == '\n' ? m_index - 1 : m_index;
			offset = FindNextLineStart(builder, index);
			length = 0;
		}
		
		protected override void OnExecute()
		{
			Log.WriteLine("Refactor Commands", "InsertAfterLine index={0}", m_index);
			
			AddLines(m_lines);
		}
		
		public override string ToString()
		{
			return string.Format("InsertAfterLine({0}, {1})", m_index, LinesToString(m_lines));
		}
		
		private int m_index;
		private int m_length;
		private string[] m_lines;
	}
	
	// Inserts lines at at the start of the line index is within.
	public sealed class InsertBeforeLine : RefactorCommand
	{
		public InsertBeforeLine(int index, params string[] lines)
		{
			m_index = index;
			m_lines = lines;
		}
		
		protected override void OnFindRange(StringBuilder builder, out int offset, out int length)
		{
			offset = FindLineStart(builder, m_index);
			length = 0;
		}
		
		protected override void OnExecute()
		{
			Log.WriteLine("Refactor Commands", "InsertBeforeLine index={0}", m_index);
			
			AddLines(m_lines);
		}
		
		public override string ToString()
		{
			return string.Format("InsertBeforeLine({0}, {1})", m_index, LinesToString(m_lines));
		}
		
		private int m_index;
		private string[] m_lines;
	}
	
	// Inserts lines at the start of a block.
	public sealed class InsertFirst : RefactorCommand
	{
		public InsertFirst(CsBody body, params string[] lines)
		{
			m_body = body;
			m_lines = lines;
		}
		
		protected override void OnFindRange(StringBuilder builder, out int offset, out int length)
		{
			offset = FindLineStart(builder, m_body.First);
			length = 0;
		}
		
		protected override void OnExecute()
		{
			Log.WriteLine("Refactor Commands", "InsertFirst name={0}", m_body.Name);
			
			AddLines(m_lines);
		}
		
		public override string ToString()
		{
			return string.Format("{0}.Body.InsertFirst({1})", m_body.Name, LinesToString(m_lines));
		}
		
		private CsBody m_body;
		private string[] m_lines;
	}
	
	// Inserts lines at the end of a block. 
	public sealed class InsertLast : RefactorCommand
	{
		public InsertLast(CsBody body, params string[] lines)
		{
			m_body = body;
			m_lines = lines;
		}
		
		protected override void OnFindRange(StringBuilder builder, out int offset, out int length)
		{
			offset = FindLineStart(builder, m_body.Last);
			length = 0;
		}
		
		protected override void OnExecute()
		{
			Log.WriteLine("Refactor Commands", "InsertLast name={0}", m_body.Name);
			
			AddLines(m_lines);
		}
		
		public override string ToString()
		{
			return string.Format("{0}.Body.InsertLast({1})", m_body.Name, LinesToString(m_lines));
		}
		
		private CsBody m_body;
		private string[] m_lines;
	}
}
