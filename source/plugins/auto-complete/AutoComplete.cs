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
			m_styles = m_boss.Get<IStyles>();
		}
		
		public Boss Boss
		{
			get {return m_boss;}
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
				Console.WriteLine("target: {0}", target);
							string type = DoGetType(target, range.location);
							if (type != null)
							{
				Console.WriteLine("type: {0}", type);
								string fullName = DoGetFullTypeName(type);
				Console.WriteLine("fullName: {0}", fullName);
								
								string hash = DoGetAssembly(fullName);
								if (hash != null)
								{
									string[] methods = DoGetMethods(fullName, hash);
				Console.WriteLine("found {0} methods", methods.Length);
									m_window = new TargetWindow(view, fullName, methods);
									m_window.Show();
								}
							}
						}
					}
				}
			}
			
			return handled;
		}
		
		#region Private Methods
		private string DoGetAssembly(string fullName)
		{
			string sql = string.Format(@"
				SELECT hash
					FROM Types 
				WHERE type = '{0}'", fullName);
			string[][] rows = m_database.QueryRows(sql);
			
			return rows.Length > 0 ? rows[0][0] : null;
		}
		
		private string[] DoGetMethods(string fullName, string hash)
		{
			string sql = string.Format(@"
				SELECT name, arg_types, arg_names
					FROM Methods 
				WHERE declaring_type = '{0}' AND hash = '{1}'", fullName, hash);
			string[][] rows = m_database.QueryRows(sql);
			
			var result = from r in rows where DoIsValidMethod(r[0]) select DoGetMethodName(r[0], r[1], r[2]);
			
			return result.ToArray();
		}
		
		private bool DoIsValidMethod(string name)
		{
			return
				!name.Contains(".ctor") &&
				!name.Contains("set_") &&
				!name.Contains("op_");
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
		
		private string DoGetFullTypeName(string type)
		{
			// TODO: if the name doesn't change need to lookup the type
			// name and the type name concatenated with each of the using
			// directives
			return DoGetAliasedName(type);
		}
		
		// TODO: duplicate of FindInDatabase.DoGetRealName
		private string DoGetAliasedName(string name)
		{
			switch (name)
			{
				case "bool":
					return "System.Boolean";
					
				case "byte":
					return "System.Byte";
					
				case "char":
					return "System.Char";
					
				case "decimal":
					return "System.Decimal";
					
				case "double":
					return "System.Double";
					
				case "short":
					return "System.Int16";
					
				case "int":
					return "System.Int32";
					
				case "long":
					return "System.Int64";
				
				case "sbyte":
					return "System.SByte";
					
				case "object":
					return "System.Object";
					
				case "float":
					return "System.Single";
					
				case "string":
					return "System.String";
					
				case "ushort":
					return "System.UInt16";
					
				case "uint":
					return "System.UInt32";
					
				case "ulong":
					return "System.UInt64";
					
				case "void":
					return "System.Void";
					
				default:
					return name;
			}
		}
		
		private string DoGetType(string target, int offset)
		{
			int editCount;
			StyleRun[] runs;
			CsGlobalNamespace globals;
			m_styles.Get(out editCount, out runs, out globals);
			
			string type = null;
			CsMember member = DoFindMember(globals, offset);
			if (member != null)
			{
				type = DoFindArgType(member, target);
			}
			
			return type;
		}
		
		// TODO: this didn't work for the MObjc.Class ctor
		private string DoFindArgType(CsMember member, string name)
		{
			string type = null;
			
			do
			{
				CsIndexer i = member as CsIndexer;
				if (i != null)
				{
					type = DoFindParamType(i.Parameters, name);
					break;
				}
				
				CsMethod m = member as CsMethod;
				if (m != null)
				{
					type = DoFindParamType(m.Parameters, name);
					break;
				}
				
				CsOperator o = member as CsOperator;
				if (o != null)
				{
					type = DoFindParamType(o.Parameters, name);
					break;
				}
			}
			while (false);
			
			return type;
		}
		
		private string DoFindParamType(CsParameter[] parms, string name)
		{
			foreach (CsParameter p in parms)
			{
				if (p.Name == name)
					return p.Type;
			}
			
			return null;
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
			while (index > 0 && DoIsIdentifierPartChar(text[index - 1]))
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
		private IStyles m_styles;
		private Database m_database;
		private TargetWindow m_window;
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
