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
		
		public bool HandleKey(NSTextView view, NSEvent evt)
		{
			bool handled = false;
			
			try
			{
				if (m_database != null)
				{
					NSRange range = view.selectedRange();
					
					NSString chars = evt.characters();
					if (range.length == 0 && chars.length() == 1 && chars[0] == '.')
					{
						view.insertText(NSString.Create('.'));
						Unused.Value = DoComplete(this.DoCompleteDot, view, range);
						handled = true;
					}
					else if (range.length == 0 && chars.length() == 1 && chars[0] == '\t')
					{
						handled = DoCompleteTab(view, evt, range);
					}
					
					if (!handled)
					{
						var annotation = m_boss.Get<IArgsAnnotation>();
						handled = annotation.HandleKey(view, evt);
					}
				}
			}
			catch (Exception e)
			{
				Log.WriteLine(TraceLevel.Error, "Errors", "Autocomplete failed:");
				Log.WriteLine(TraceLevel.Error, "Errors", e.Message);
				
				NSString title = NSString.Create("Auto-complete failed.");
				NSString message = NSString.Create(e.Message);
				Unused.Value = Functions.NSRunAlertPanel(title, message);
			}
			
			return handled;
		}
		
		// Returns the shortest declaration which intersects offset.
		internal static CsDeclaration FindDeclaration(CsTypeScope scope, int offset)
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
		
		#region Private Methods
		private bool DoCompleteTab(NSTextView view, NSEvent evt, NSRange range)
		{
			bool handled = false;
			
			TimeSpan delta = DateTime.Now - m_lastTabTime;
			if (range.location > 2 && delta.TotalSeconds < GetDblTime()/60.0 && range.location == m_lastTabIndex + 1)
			{
				string text = m_boss.Get<IText>().Text;
				char c1 = range.location >= 1 ? text[range.location - 1] : '\x00';
				char c2 = range.location >= 2 ? text[range.location - 2] : '\x00';
						
				// DoUpdateCache is kind of slow so we want to avoid it if the user is just 
				// typing a bunch of tabs.
				if (c1 == '\t' && c2 != '\t')
					handled = DoComplete(this.DoCompleteStem, view, range);
			}
			
			m_lastTabTime = DateTime.Now;
			m_lastTabIndex = range.location;
			
			return handled;
		}
		
		internal static Stopwatch Watch = new Stopwatch();
		
		private bool DoComplete(Func<ITextEditor, NSTextView, NSRange, bool> completer, NSTextView view, NSRange range)
		{
			bool handled = false;
			
			string text = m_boss.Get<IText>().Text;
			if (range.location >= text.Length || !CsHelpers.CanStartIdentifier(text[range.location]))
			{
				Watch.Start();
				Log.WriteLine("AutoComplete", "starting auto-complete");
				
				var editor = m_boss.Get<ITextEditor>();
				DoUpdateCache(editor);
				
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
			Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "DoCompleteNamespaceDot is checking for namespaces using {0}", stem);
			Item[] namespaces = DoGetNamespacesNamed(stem);
			
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
				var context = FindDeclaration(globals, range.location) as CsMember;

				Boss boss = ObjectModel.Create("CsParser");
				var locals = boss.Get<ICsLocalsParser>();
				var nameResolver = new ResolveName(context, m_database, locals, m_text.Text, range.location, globals);
				var exprResolver = new ResolveExpr(m_database, globals, nameResolver);
				
				Item[] items = null;
				string type = null;
				bool isInstance = false;
				bool isStatic = false;
				
				Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "DoCompleteMethodDot is resolving an expression");
				ResolvedTarget target = exprResolver.Resolve(context, m_text.Text, range.location);
				if (target != null)
				{
					type = target.TypeName;
					items = m_members.Resolve(context, target, globals);
					isInstance = target.IsInstance;
					isStatic = target.IsStatic;
				}
				
				if (type != "System.Void")
				{
					if (items != null && items.Length > 0)
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
						
						m_controller.Show(m_boss.Get<ITextEditor>(), view, label, items, null, isInstance, isStatic);
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
					Item[] items;
					bool isInstance = false, isStatic = false;
				
					if (stem.StartsWith("new "))
					{
						stem = stem.Substring(stem.IndexOf(' ') + 1);
						
						Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "DoCompleteStem is getting ctors");
						label = "Constructors";
						items = DoGetConstructorsNamed(globals, ref stem);
					}
					else
					{
						Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "DoCompleteStem is getting names");
						label = "Names";
						items = DoGetNames(globals, range.location - 1, stem, ref isInstance, ref isStatic);
					}
					
					if (items.Length > 0)
					{
						if (m_controller == null)	
							m_controller = new CompletionsController();
						
						m_controller.Show(m_boss.Get<ITextEditor>(), view, label, items, stem, isInstance, isStatic);
					}
					
					handled = true;			// if it was recognized as a double tab then we never want to add the second tab
				}
			}
			
			return handled;
		}
		
		private Item[] DoGetNames(CsGlobalNamespace globals, int location, string stem, ref bool isInstance, ref bool isStatic)
		{
			Profile.Start("AutoComplete::DoGetNames");
			var result = new List<Item>();
			
			var context = FindDeclaration(globals, location) as CsMember;
			var nameResolver = new ResolveName(context, m_database, m_locals, m_text.Text, location, globals);
			ResolvedTarget target = nameResolver.Resolve("<this>");
			if (target != null)
			{
				var items = new List<Item>(m_members.Resolve(context, target, globals));
				foreach (Variable v in nameResolver.Variables)
				{
					items.AddIfMissing(new NameItem(v.Name, v.Type + ' ' + v.Name, v.Filter, v.Type));
				}
				
				if (stem.Length > 0)
					items.RemoveAll(m => !m.Text.StartsWith(stem));
				
				result = items;
				isInstance = target.IsInstance;
				isStatic = target.IsStatic;
			}
			
			if (stem.Length > 0)
			{
				DoAddAliasedTypes(result, stem);
				DoAddRealTypes(result, globals, stem);
			}
			
			Profile.Stop("AutoComplete::DoGetNames");
			return result.ToArray();
		}
		
		private void DoAddAliasedTypes(List<Item> items, string stem)
		{
			IEnumerable<string> aliases = CsHelpers.GetAliasedNames();
			foreach (string alias in aliases)
			{
				if (alias.StartsWith(stem))
				{
					var item = new NameItem(alias, CsHelpers.GetRealName(alias), "System types", CsHelpers.GetRealName(alias));
					items.AddIfMissing(item);
				}
			}
		}
		
		private void DoAddRealTypes(List<Item> items, CsGlobalNamespace globals, string stem)
		{
			var namespaces = new List<string>();
			
			DoAddParsedTypes(items, (string) null, stem);
			namespaces.AddIfMissing(string.Empty);
			
			for (int i = 0; i < globals.Namespaces.Length; ++i)
			{
				DoAddParsedTypes(items, globals.Namespaces[i].Name, stem);
				namespaces.AddIfMissing(globals.Namespaces[i].Name);
			}
			
			for (int i = 0; i < globals.Uses.Length; ++i)
			{
				DoAddParsedTypes(items, globals.Uses[i].Namespace, stem);
				namespaces.AddIfMissing(globals.Uses[i].Namespace);
			}
		
			items.AddIfMissingRange(m_database.GetStemmedTypes(namespaces.ToArray(), stem));
		}
		
		private void DoAddParsedTypes(List<Item> items, string ns, string stem)
		{
			CsType[] types = m_parses.FindTypes(ns, stem);
			
			foreach (CsType type in types)
			{
				var item = new NameItem(type.Name, type.FullName, ns + " types", type.FullName);
				items.AddIfMissing(item);
			}
		}
		
		private Item[] DoGetNamespacesNamed(string name)
		{
			Profile.Start("AutoComplete::DoGetNamespacesNamed");
			var items = new List<Item>(m_database.GetNamespaces(name));
			Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "db namespaces: {0}", items.ToDebugString());
			
			string[] names = m_parses.FindNamespaces(name);
			Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "parsed namespaces: {0}", names.ToDebugString());
			
			foreach (string n in names)
			{
				var item = new NameItem(n, name + '.' + n, name + " types");
				items.AddIfMissing(item);
			}
			
			Profile.Stop("AutoComplete::DoGetNamespacesNamed");
			return items.ToArray();
		}
		
		private Item[] DoGetConstructorsNamed(CsGlobalNamespace globals, ref string stem)
		{
			Profile.Start("AutoComplete::DoGetConstructorsNamed");
			var items = new List<Item>();
			var namespaces = new List<string>();
			
			int j = stem.LastIndexOf('.');
			if (j > 0)
			{
				string ns = stem.Substring(0, j);
				stem = stem.Substring(j + 1);
				DoAddConstructors(ns, stem, items);
			}
			else
			{
				DoAddConstructors(null, stem, items);
				namespaces.AddIfMissing(string.Empty);
				
				for (int i = 0; i < globals.Namespaces.Length; ++i)
				{
					DoAddConstructors(globals.Namespaces[i].Name, stem, items);
					namespaces.AddIfMissing(globals.Namespaces[i].Name);
				}
				
				for (int i = 0; i < globals.Uses.Length; ++i)
				{
					DoAddConstructors(globals.Uses[i].Namespace, stem, items);
					namespaces.AddIfMissing(globals.Uses[i].Namespace);
				}
			}
			
			items.AddIfMissingRange(m_database.GetStemmedCtors(namespaces.ToArray(), stem));
			
			Profile.Stop("AutoComplete::DoGetConstructorsNamed");
			return items.ToArray();
		}
		
		private void DoAddConstructors(string ns, string stem, List<Item> items)
		{
			CsType[] types = m_parses.FindTypes(ns, stem);
			
			foreach (CsType type in types)
			{
				if (type is CsClass || type is CsStruct)
				{
					if ((type.Modifiers & (MemberModifiers.Abstract | MemberModifiers.Static)) == 0)
					{
						string nsName = type.Namespace != null ? type.Namespace.Name : "<globals>";
						
						var ctors = from m in type.Methods where m.IsConstructor select m;
						foreach (CsMethod ctor in ctors)
						{
							DoAddConstructor(nsName, type.GenericArguments, type.FullName, type.Name, ctor.Parameters, items);
						}
						
						if (type is CsStruct || !ctors.Any())
							DoAddConstructor(nsName, type.GenericArguments, type.FullName, type.Name, new CsParameter[0], items);
					}
				}
			}
		}
		
		private void DoAddConstructor(string ns, string inGargs, string typeName, string name, CsParameter[] parameters, List<Item> items)
		{
			string[] gargs = null;
			if (inGargs != null)
				gargs = inGargs.Split(',');
				
			string[] argTypes = (from p in parameters select p.ModifiedType).ToArray();
			string[] argNames = (from p in parameters select p.Name).ToArray();
			
			string nsName = ns == "<globals>" ? "global" : ns;
			var item = new MethodItem("System.Void", name, gargs, argTypes, argNames, typeName, nsName + " constructors");
			items.AddIfMissing(item);
		}
		
		private void DoUpdateCache(ITextEditor editor)
		{
//			Profile.Start("AutoComplete::DoUpdateCache");
//			Parse parse = m_parses.TryParse(editor.Path);
			
//			if (parse != null && parse.Edit != m_text.EditCount)
//			{
//				computer.ComputeRuns(editor.Boss, editor.Path, m_text.Text, m_text.EditCount);
//			}
//			Profile.Stop("AutoComplete::DoUpdateCache");
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
