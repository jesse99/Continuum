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
using MCocoa;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AutoComplete
{
	internal sealed class AutoComplete : IAutoComplete
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
			m_text = m_boss.Get<IText>();
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public void Close()
		{
			if (m_controller != null)
			{
				m_controller.window().release();
				m_controller.release();
				m_controller = null;
			}
		}
		
		public void OnPathChanged()
		{
			Boss boss = ObjectModel.Create("DirectoryEditorPlugin");
			var find = boss.Get<IFindDirectoryEditor>();
			boss = find.GetDirectoryEditor(m_boss);
			
			if (boss != null)
			{
				var editor = boss.Get<IDirectoryEditor>();
				if (editor.Path != null)
				{
					string name = System.IO.Path.GetFileName(editor.Path);
					
					string path = System.IO.Path.Combine(Paths.SupportPath, name + ".db");
					m_database = new Database(path);
					
					m_target = new Target(m_boss.Get<IStyles>(), m_database);
				}
			}
		}
		
		public bool HandleKey(NSTextView view, NSEvent evt)
		{
			bool handled = false;
			
			if (m_database != null)
			{
				NSRange range = view.selectedRange();
				
				NSString chars = evt.characters();
				if (range.length == 0 && chars.length() == 1)
				{
					if (chars[0] == '.')
					{
						string target = DoGetTarget(range.location);
						if (target != null)
						{
							if (m_target.FindType(target, range.location))
							{
								string[] methods = DoGetMethods();
								if (methods.Length > 0)
								{
									if (m_controller == null)	
										m_controller = new CompletionsController();
									m_controller.Show(view, m_target.FullTypeName, methods);
								}
							}
						}
					}
				}
			}
			
			return handled;
		}
		
		#region Private Methods
		private string[] DoGetMethods()
		{
			var result = new List<string>();
			
			if (m_target.Hash != null)
			{
				string sql = string.Format(@"
					SELECT name, arg_types, arg_names, attributes
						FROM Methods 
					WHERE declaring_type = '{0}' AND hash = '{1}'", m_target.FullTypeName, m_target.Hash);
				string[][] rows = m_database.QueryRows(sql);
				
				var methods = from r in rows
					where DoIsValidMethod(r[0], ushort.Parse(r[3]), m_target.IsInstanceCall)
					select DoGetMethodName(r[0], r[1], r[2]);
				result.AddRange(methods);
			}
			
			// Note that indexers are not counted because they are not preceded with a dot.
			if (m_target.Type != null)
			{
				foreach (CsField field in m_target.Type.Fields)
				{
					if (m_target.IsInstanceCall == ((field.Modifiers & MemberModifiers.Static) == 0))
						result.Add(field.Name);
				}
				
				foreach (CsMethod method in m_target.Type.Methods)
				{
					if (!method.IsConstructor && !method.IsFinalizer)
					{
						if (m_target.IsInstanceCall == ((method.Modifiers & MemberModifiers.Static) == 0))
							result.Add(method.Name + "(" + string.Join(", ", (from p in method.Parameters select p.Type + " " + p.Name).ToArray()) + ")");
					}
				}
				
				foreach (CsProperty prop in m_target.Type.Properties)
				{
					if (prop.HasGetter)
					{
						if (m_target.IsInstanceCall == ((prop.Modifiers & MemberModifiers.Static) == 0))
							result.Add(prop.Name);
					}
				}
			}
			
			return result.ToArray();
		}
		
		private bool DoIsValidMethod(string name, ushort attributes, bool instanceCall)
		{
			bool valid;
			
			if (instanceCall)
				valid = (attributes & 0x0010) == 0;
			else
				valid = (attributes & 0x0010) != 0;
				
			if (valid && name.Contains(".ctor"))
				valid = false;
			
			if (valid && name.Contains("set_"))
				valid = false;
			
			if (valid && name.Contains("op_"))
				valid = false;
				
			if (valid && name.Contains("add_"))
				valid = false;
				
			if (valid && name.Contains("remove_"))
				valid = false;
				
			if (valid && name == "Finalize")
				valid = false;
				
			return valid;
		}
		
		private string DoGetMethodName(string mname, string argTypes, string argNames)
		{
			var builder = new StringBuilder(mname.Length + argTypes.Length + argNames.Length);
			
			if (mname.StartsWith("get_"))
			{
				builder.Append(mname.Substring(4));
			}
			else
			{
				builder.Append(mname);
				
				builder.Append('(');
				string[] types = argTypes.Split(new char[]{':'}, StringSplitOptions.RemoveEmptyEntries);
				string[] names = argNames.Split(new char[]{':'}, StringSplitOptions.RemoveEmptyEntries);
				for (int i = 0; i < types.Length; ++i)
				{
					string type = types[i];
					if (ms_aliases.ContainsKey(type))
						type = ms_aliases[type];
					builder.Append(type);
					
					builder.Append(' ');
					
					string name = names[i];
					builder.Append(name);
					
					if (i + 1 < types.Length)
						builder.Append(", ");
				}
				builder.Append(')');
			}
			
			return builder.ToString();
		}
		
		// Find the last member offset intersects
		private CsMember DoFindMember(CsNamespace ns, int offset)
		{
			CsMember member = null;
			
			for (int i = 0; i < ns.Namespaces.Length && member == null; ++i)
			{
				member = DoFindMember(ns.Namespaces[i], offset);
			}
			
			for (int i = 0; i < ns.Types.Length && member == null; ++i)
			{
				CsType type = ns.Types[i];
				
				for (int j = 0; j < type.Members.Length && member == null; ++j)
				{
					CsMember candidate = type.Members[j];
					if (candidate.Offset <= offset && offset < candidate.Offset + candidate.Length)
						member = candidate;
				}
			}
			
			return member;
		}
		
		private string DoGetTarget(int offset)
		{
			string text = m_text.Text;
			
			int index = offset;
			while (index > 0 && (text[index - 1] == '.' || DoIsIdentifierPartChar(text[index - 1])))
				--index;
			
			string target = null;
			if (text[index] == '_' || DoIsLetter(text[index]))
				target = text.Substring(index, offset - index);
			
			return target;
		}
		
		// TODO: these two are (mostly) duplicates of CsParser.Scanner.
		private bool DoIsLetter(char ch)
		{
			if (char.IsLetter(ch))			// fast path
				return true;
				
			UnicodeCategory cat = char.GetUnicodeCategory(ch);
			switch (cat)
			{
				case UnicodeCategory.UppercaseLetter:
				case UnicodeCategory.LowercaseLetter:
				case UnicodeCategory.TitlecaseLetter:
				case UnicodeCategory.ModifierLetter:
				case UnicodeCategory.OtherLetter:
				case UnicodeCategory.LetterNumber:
					return true;
			}
			
			return false;
		}
		
		private bool DoIsIdentifierPartChar(char ch)
		{
			if (char.IsLetterOrDigit(ch) || ch == '_')	// fast path
				return true;
							
			UnicodeCategory cat = char.GetUnicodeCategory(ch);
			switch (cat)
			{
				case UnicodeCategory.DecimalDigitNumber:
				case UnicodeCategory.ConnectorPunctuation:
				case UnicodeCategory.NonSpacingMark:
				case UnicodeCategory.SpacingCombiningMark:
				case UnicodeCategory.Format:
					return true;
			}
			
			return false;
		}
		#endregion
		
		#region Fields
		private Boss m_boss;
		private IText m_text;
		private Database m_database;
		private CompletionsController m_controller;
		private Target m_target;
		
		private static Dictionary<string, string> ms_aliases = new Dictionary<string, string>	// TODO: ShortForm.cs has the same list
		{
			{"System.Boolean", "bool"},
			{"System.Byte", "byte"},
			{"System.Char", "char"},
			{"System.Decimal", "decimal"},
			{"System.Double", "double"},
			{"System.Int16", "short"},
			{"System.Int32", "int"},
			{"System.Int64", "long"},
			{"System.SByte", "sbyte"},
			{"System.Object", "object"},
			{"System.Single", "float"},
			{"System.String", "string"},
			{"System.UInt16", "ushort"},
			{"System.UInt32", "uint"},
			{"System.UInt64", "ulong"},
			{"System.Void", "void"},
			
			{"System.Boolean[]", "bool[]"},
			{"System.Byte[]", "byte[]"},
			{"System.Char[]", "char[]"},
			{"System.Decimal[]", "decimal[]"},
			{"System.Double[]", "double[]"},
			{"System.Int16[]", "short[]"},
			{"System.Int32[]", "int[]"},
			{"System.Int64[]", "long[]"},
			{"System.SByte[]", "sbyte[]"},
			{"System.Object[]", "object[]"},
			{"System.Single[]", "float[]"},
			{"System.String[]", "string[]"},
			{"System.UInt16[]", "ushort[]"},
			{"System.UInt32[]", "uint[]"},
			{"System.UInt64[]", "ulong[]"},
			{"System.Void[]", "void[]"},
		};
		#endregion
	}
}
