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
using System.Runtime.InteropServices;
using System.Text;

namespace AutoComplete
{
	internal sealed class AutoComplete : IAutoComplete
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
			m_text = m_boss.Get<IText>();
			m_tokens = m_boss.Get<ISearchTokens>();
			
			boss = ObjectModel.Create("CsParser");
			m_locals = boss.Get<ICsLocalsParser>();
			m_parses = boss.Get<IParses>();
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
					
					string path = Paths.GetAssemblyDatabase(name);
					
					// TODO: this doesn't help for simple queries, but maybe it would
					// for more complex queries (e.g. chained method calls)
//					database.Update("cache size", () =>
//					{
//						database.Update("PRAGMA cache_size = 4000");
//					});
					
					m_database = new TargetDatabase(new Database(path, "AutoComplete-" + name));
					m_members = new ResolveMembers(m_database);
				}
			}
		}
		
		public bool HandleKey(NSTextView view, NSEvent evt, IComputeRuns computer)
		{
			bool handled = false;
			
			if (m_database != null)
			{
				NSRange range = view.selectedRange();
				
				NSString chars = evt.characters();
				if (range.length == 0 && chars.length() == 1 && chars[0] == '.')
				{
					view.insertText(NSString.Create('.'));
					Unused.Value = DoComplete(this.DoCompleteDot, view, range, computer);
					handled = true;
				}
				else if (range.length == 0 && chars.length() == 1 && chars[0] == '\t')
				{
					handled = DoCompleteTab(view, evt, computer, range);
				}
				
				if (!handled)
				{
					var annotation = m_boss.Get<IArgsAnnotation>();
					handled = annotation.HandleKey(view, evt);
				}
			}
			
			return handled;
		}
		
		#region Private Methods
		// Returns the shortest declaration which intersects offset.
		private CsDeclaration DoFindDeclaration(CsTypeScope scope, int offset)
		{
			var decs = new List<CsDeclaration>(scope.Declarations.Length + 1);
			decs.Add(scope);
			
			CsDeclaration result = null;
			int resultLength = int.MaxValue;
			
			// For every declaration,
			while (decs.Count > 0)
			{
				// if the declaration is within the range,
				CsDeclaration candidate = decs.Pop();
				if (candidate.Offset <= offset && offset < candidate.Offset + candidate.Length)
				{
					// use the declaration if it is shorter than what we have so far.
					if (candidate.Length < resultLength)
					{
						result = candidate;
						resultLength = candidate.Length;
					}
				}
				
				// If the declaration is a namespace or type then check the
				// inner declarations as well.
				CsTypeScope inner = candidate as CsTypeScope;
				if (inner != null)
					decs.AddRange(inner.Declarations);
			}
			
			return result;
		}
		
		private bool DoCompleteTab(NSTextView view, NSEvent evt, IComputeRuns computer, NSRange range)
		{
			bool handled = false;
			
			TimeSpan delta = DateTime.Now - m_lastTabTime;
			if (range.location > 2 && delta.TotalSeconds < GetDblTime()/60.0 && range.location == m_lastTabIndex + 1)
			{
				handled = DoComplete(this.DoCompleteStem, view, range, computer);
			}
			
			m_lastTabTime = DateTime.Now;
			m_lastTabIndex = range.location;
			
			return handled;
		}
		
		internal static Stopwatch Watch = new Stopwatch();
		
		private bool DoComplete(Func<ITextEditor, NSTextView, NSRange, bool> completer, NSTextView view, NSRange range, IComputeRuns computer)
		{
			bool handled = false;
			
			string text = m_boss.Get<IText>().Text;
			if (range.location >= text.Length || !CsHelpers.CanStartIdentifier(text[range.location]))
			{
				Watch.Start();
				Log.WriteLine("AutoComplete", "starting auto-complete");
				
				var editor = m_boss.Get<ITextEditor>();
				DoUpdateCache(editor, computer);
				
				if (!m_tokens.IsWithinComment(range.location) && !m_tokens.IsWithinString(range.location))
				{
					handled = completer(editor, view, range);
				}
				
				Watch.Reset();
			}
			
			return handled;
		}
		
		private bool DoCompleteDot(ITextEditor editor, NSTextView view, NSRange range)
		{
			bool handled = false;
			
			string stem = DoGetTargetStem(range, -1);
			if (stem.StartsWith("new ") || stem.StartsWith("using "))
			{
				handled = DoCompleteNamespaceDot(view, stem);
			}
			else
			{
				if (stem.Length > 0)
					handled = DoCompleteNamespaceDot(view, stem);
					
				if (!handled)
					handled = DoCompleteMethodDot(editor, view, range);
			}
			
			return handled;
		}
		
		private bool DoCompleteNamespaceDot(NSTextView view, string stem)
		{
			stem = stem.Substring(stem.IndexOf(' ') + 1);
			Member[] namespaces = DoGetNamespacesNamed(stem);
			
			if (namespaces.Length > 0)
			{
				if (m_controller == null)	
					m_controller = new CompletionsController();
				
				string label = stem + " Namespaces";
				m_controller.Show(m_boss.Get<ITextEditor>(), view, label, namespaces, null, false, false);
			}
			
			return namespaces.Length > 0;
		}
		
		private bool DoCompleteMethodDot(ITextEditor editor, NSTextView view, NSRange range)
		{
			bool handled = false;
			
			Parse parse = m_parses.TryParse(editor.Path);
			CsGlobalNamespace globals = parse != null ? parse.Globals : null;
			if (globals != null)
			{
				var context = DoFindDeclaration(globals, range.location) as CsMember;

				Boss boss = ObjectModel.Create("CsParser");
				var locals = boss.Get<ICsLocalsParser>();
				var nameResolver = new ResolveName(context, m_database, locals, m_text.Text, range.location, globals);
				var exprResolver = new ResolveExpr(m_database, globals, nameResolver);
				
				Member[] members = null;
				string type = null;
				bool isInstance = false;
				bool isStatic = false;
				
				ResolvedTarget target = exprResolver.Resolve(context, m_text.Text, range.location);
				if (target != null)
				{
					type = target.TypeName;
					members = m_members.Resolve(context, target, globals);
					isInstance = target.IsInstance;
					isStatic = target.IsStatic;
				}
				
				if (type != "System.Void")
				{
					if (members != null && members.Length > 0)
					{
						if (m_controller == null)	
							m_controller = new CompletionsController();
							
						string label = type;
						if (isInstance && !isStatic)
							label += " Members";
						else if (isInstance)
							label += " Instance Members";
						else if (isStatic)
							label += " Static Members";
						
						m_controller.Show(m_boss.Get<ITextEditor>(), view, label, members, null, isInstance, isStatic);
						handled = true;
					}
				}
				else
					Functions.NSBeep();
			}
			
			return handled;
		}
		
		private bool DoCompleteStem(ITextEditor editor, NSTextView view, NSRange range)
		{
			bool handled = false;
			
			Parse parse = m_parses.TryParse(editor.Path);
			CsGlobalNamespace globals = parse != null ? parse.Globals : null;
			if (globals != null)
			{
				string stem = DoGetTargetStem(range, -2);
				if (stem.Length > 0)
				{
					string label = string.Empty;
					Member[] members;
					bool isInstance = false, isStatic = false;
				
					if (stem.StartsWith("new "))
					{
						stem = stem.Substring(stem.IndexOf(' ') + 1);
						
						label = "Constructors";
						members = DoGetConstructorsNamed(globals, ref stem);
					}
					else
					{
						label = "Names";
						members = DoGetMembersNamed(globals, range.location - 1, stem, ref isInstance, ref isStatic);
					}
					
					if (members.Length > 0)
					{
						if (m_controller == null)	
							m_controller = new CompletionsController();
						
						m_controller.Show(m_boss.Get<ITextEditor>(), view, label, members, stem, isInstance, isStatic);
					}
					
					handled = true;			// if it was recognized as a double tab then we never want to add the second tab
				}
			}
			
			return handled;
		}
		
		private Member[] DoGetMembersNamed(CsGlobalNamespace globals, int location, string stem, ref bool isInstance, ref bool isStatic)
		{
			var result = new List<Member>();
			
			var context = DoFindDeclaration(globals, location) as CsMember;
			var nameResolver = new ResolveName(context, m_database, m_locals, m_text.Text, location, globals);
			ResolvedTarget target = nameResolver.Resolve("<this>");
			if (target != null)
			{
				var members = new List<Member>(m_members.Resolve(context, target, globals));
				foreach (Variable v in nameResolver.Variables)
				{
					members.AddIfMissing(new Member(v.Name, v.Type));
				}
				
				if (stem.Length > 0)
					members.RemoveAll(m => !m.Name.StartsWith(stem));
				
				result = members;
				isInstance = target.IsInstance;
				isStatic = target.IsStatic;
			}
			
			return result.ToArray();
		}
		
		private Member[] DoGetNamespacesNamed(string name)
		{
			var members = new List<Member>(m_database.GetNamespaces(name));
			Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "db namespaces: {0}", members.ToDebugString());
			
			string[] names = m_parses.FindNamespaces(name);
			Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "parsed namespaces: {0}", names.ToDebugString());
			members.AddIfMissingRange(from n in names select new Member(n));
			
			return members.ToArray();
		}
		
		private Member[] DoGetConstructorsNamed(CsGlobalNamespace globals, ref string stem)
		{
			var members = new List<Member>();
			
			int j = stem.LastIndexOf('.');
			if (j > 0)
			{
				string ns = stem.Substring(0, j);
				stem = stem.Substring(j + 1);
				DoAddConstructors(ns, stem, members);
			}
			else
			{
				DoAddConstructors(null, stem, members);
				
				for (int i = 0; i < globals.Namespaces.Length; ++i)
				{
					DoAddConstructors(globals.Namespaces[i].Name, stem, members);
				}
				
				for (int i = 0; i < globals.Uses.Length; ++i)
				{
					DoAddConstructors(globals.Uses[i].Namespace, stem, members);
				}
			}
			
			return members.ToArray();
		}
		
		private void DoAddConstructors(string ns, string stem, List<Member> members)
		{
			CsType[] types = m_parses.FindTypes(ns, stem);
			
			foreach (CsType type in types)
			{
				if (type is CsClass || type is CsStruct)
				{
					if ((type.Modifiers & (MemberModifiers.Abstract | MemberModifiers.Static)) == 0)
					{
						var ctors = from m in type.Methods where m.IsConstructor select m;
						foreach (CsMethod ctor in ctors)
						{
							DoAddConstructor(type.GenericArguments, type.FullName, type.Name, ctor.Parameters, members);
						}
						
						if (type is CsStruct || !ctors.Any())
							DoAddConstructor(type.GenericArguments, type.FullName, type.Name, new CsParameter[0], members);
					}
				}
			}
			
			members.AddIfMissingRange(m_database.GetCtors(ns, stem));
		}
		
		private void DoAddConstructor(string gargs, string typeName, string name, CsParameter[] parameters, List<Member> members)
		{
			string text = name;
			
			if (gargs != null)
				text += "<" + gargs.Replace(",", ", ") + ">";
				
			var anames = from p in parameters select p.Name;
			text += "(" + string.Join(";", (from p in parameters select p.Type + " " + p.Name).ToArray()) + ")";
			
			var member = new Member(text, anames.Count(), "System.Void", typeName);
			member.Label = typeName;
			
			members.AddIfMissing(member);
		}
		
		private void DoUpdateCache(ITextEditor editor, IComputeRuns computer)
		{
			Parse parse = m_parses.TryParse(editor.Path);
			
			if (parse != null && parse.Edit != m_text.EditCount)
			{
				computer.ComputeRuns(editor.Boss, editor.Path, m_text.Text, m_text.EditCount);
			}
		}
		
		private string DoGetTargetName(int offset)
		{
			string text = m_text.Text;
			
			int index = offset;
			while (index > 0 && (text[index - 1] == '.' || CsHelpers.CanContinueIdentifier(text[index - 1])))
				--index;
			
			string expr = string.Empty;
			if (text[index] == '_' || CsHelpers.CanStartIdentifier(text[index]))
				expr = text.Substring(index, offset - index + 1);
			
			return expr;
		}
		
		private string DoGetTargetStem(NSRange range, int delta)
		{
			string name = string.Empty;
			
			int offset = range.location + delta;
			if (offset > 0 && offset < m_text.Text.Length)
			{
				name = DoGetTargetName(offset);
				if (name.Length > 0)
				{
					offset -= name.Length;
					
					if (offset >= 3 && string.Compare(m_text.Text, offset - 3, "new ", 0, 4) == 0)
						name = "new " + name;
					
					else if (offset >= 5 && string.Compare(m_text.Text, offset - 5, "using ", 0, 6) == 0)
						name = "using " + name;
				}
			}
			
			return name;
		}
		
		[DllImport("/System/Library/Frameworks/Carbon.framework/Carbon")]
		private extern static uint GetDblTime();
		#endregion
		
		#region Fields
		private Boss m_boss;
		private IText m_text;
		private TargetDatabase m_database;
		private CompletionsController m_controller;
		private ISearchTokens m_tokens;
		private IParses m_parses;
		private ICsLocalsParser m_locals;
		private ResolveMembers m_members;
		private DateTime m_lastTabTime;
		private int m_lastTabIndex = -1;
		#endregion
	}
}
