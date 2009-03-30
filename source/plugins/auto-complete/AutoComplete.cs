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
			m_cachedCatalog = m_boss.Get<ICachedCsCatalog>();
			m_cachedGlobals = m_boss.Get<ICachedCsDeclarations>();
			
			boss = ObjectModel.Create("CsParser");
			m_locals = boss.Get<ICsLocalsParser>();
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
					m_database = new Database(path, "AutoComplete-" + name);
					
					m_target = new ResolveTarget(new TargetDatabase(m_database), m_locals);
					m_type = new ResolveType(new TargetDatabase(m_database));
					m_members = new ResolveMembers(m_database);
				}
			}
		}
		
		private const int EnterKey = 0x4C;
		private const int BackspaceKey = 0x35;
		
		public bool HandleKey(NSTextView view, NSEvent evt, IComputeRuns computer)
		{
			bool handled = false;
			
			if (m_database != null)
			{
				NSRange range = view.selectedRange();
				
				NSString chars = evt.characters();
				if (range.length == 0 && chars.length() == 1 && chars[0] == '.')
				{
					handled = DoComplete(this.DoCompleteTarget, view, range, computer);
				}
				else if (evt.keyCode() == EnterKey)
				{
					handled = DoComplete(this.DoCompleteExpression, view, range, computer);
				}
				else if (evt.keyCode() == BackspaceKey)
				{
					IAnnotation annotation = m_boss.Get<IAnnotation>();
					if (annotation.IsOpen)
					{
						annotation.Close();
						handled = true;
					}
				}
			}
			
			return handled;
		}
		
		#region Private Methods
		internal static Stopwatch Watch = new Stopwatch();
		
		private bool DoComplete(Func<ITextEditor, NSTextView, NSRange, bool> completer, NSTextView view, NSRange range, IComputeRuns computer)
		{
			bool handled = false;
			
			Watch.Start();
			Log.WriteLine("AutoComplete", "starting auto-complete");
			
			DoUpdateCache(computer);
			
			if (!m_cachedCatalog.IsWithinComment(range.location) && !m_cachedCatalog.IsWithinString(range.location))
				handled = completer(m_boss.Get<ITextEditor>(), view, range);
				
			Watch.Reset();
			
			return handled;
		}
		
		private bool DoCompleteTarget(ITextEditor editor, NSTextView view, NSRange range)
		{
			CsGlobalNamespace globals = m_cachedGlobals.Get();
			if (globals != null)
			{
				Member[] members = null;
				string type = null;
				bool isInstance = false;
				bool isStatic = false;
				
				Member oldMember = DoMatchOldMethod(range);
				if (oldMember != null)
				{
					ResolvedTarget target = m_type.Resolve(oldMember.Type, globals, true, false);
					if (target != null)
					{
						type = target.FullName;
						members = m_members.Resolve(target, globals);
						isInstance = target.IsInstance;
						isStatic = target.IsStatic;
					}
				}
				else
				{
					string expr = DoGetTargetExpr(range.location);
					if (!string.IsNullOrEmpty(expr))
					{
						Tuple2<ResolvedTarget, Variable[]> target = m_target.Resolve(
							m_text.Text, expr, range.location, globals);
						if (target.First != null)
						{
							type = target.First.FullName;
							members = m_members.Resolve(target.First, globals);
							isInstance = target.First.IsInstance;
							isStatic = target.First.IsStatic;
						}
					}
				}
				
				if (type != "System.Void")
				{
					if (members != null && members.Length > 0)
					{
						if (m_controller == null)	
							m_controller = new CompletionsController();
						m_controller.Show(m_boss.Get<ITextEditor>(), view, type, members, 0, isInstance, isStatic);
					}
				}
				else
					Functions.NSBeep();
			}
			
			return false;
		}
		
		private Member DoMatchOldMethod(NSRange range)
		{
			if (m_controller != null)
			{
				string text = m_text.Text;
				int oldIndex = m_controller.CompletedIndex;
				
				if (m_controller.CompletedMember != null && oldIndex < text.Length)
				{
					string oldText = m_controller.CompletedMember.Text;
					int k = oldText.IndexOf('(');
					
					if (k >= 0)
					{
						// Alpha.Beta(actualArg).
						if (string.Compare(oldText, 0, text, oldIndex, k) == 0)
						{
							int i = DoSkipParens(text, oldIndex + k);
							if (range.location == i)
								return m_controller.CompletedMember;
						}
					}
					else
					{
						// Alpha.Beta.
						if (range.location == oldIndex + oldText.Length)
						{
							if (string.Compare(oldText, 0, text, oldIndex, oldText.Length) == 0)
								return m_controller.CompletedMember;
						}
					}
				}
			}
			
			return null;
		}
		
		private int DoSkipParens(string text, int i)
		{
			Trace.Assert(text[i] == '(', "expected a '(' but found '" + text[i] + "'");
			
			int count = 1;
			++i;
			
			while (i < text.Length && count > 0)
			{
				if (text[i] == '(')
					++count;
				else if (text[i] == ')')
					--count;
				
				++i;
			}
			
			return count == 0 ? i : -1;
		}
		
		private bool DoCompleteExpression(ITextEditor editor, NSTextView view, NSRange range)
		{
			CsGlobalNamespace globals = m_cachedGlobals.Get();
			if (globals != null)
			{
				var target = m_target.Resolve(m_text.Text, "<this>", range.location, globals);
				if (target.First != null)
				{
					var members = new List<Member>(m_members.Resolve(target.First, globals));
					foreach (Variable v in target.Second)
					{
						members.AddIfMissing(new Member(v.Name, v.Type));
					}
					
					int prefixLen = 0;
					if (range.length == 0)
					{
						string expr = DoGetTargetExpr(range.location);
						if (!string.IsNullOrEmpty(expr))
						{
							members.RemoveAll(m => !m.Text.StartsWith(expr));
							prefixLen = expr.Length;
						}
					}
					
					if (members.Count > 0)
					{
						if (m_controller == null)	
							m_controller = new CompletionsController();
							
						m_controller.Show(m_boss.Get<ITextEditor>(), view, target.First.FullName, members.ToArray(), prefixLen, target.First.IsInstance, target.First.IsStatic);
					}
				}
			}
			
			return true;
		}
		
		private void DoUpdateCache(IComputeRuns computer)
		{
			int edit;
			CsGlobalNamespace globals;
			m_cachedGlobals.Get(out edit, out globals);
			
			if (edit != m_text.EditCount)
			{
				computer.ComputeRuns(m_text.Text, m_text.EditCount, m_boss);
			}
		}
		
		private string DoGetTargetExpr(int offset)
		{
			string text = m_text.Text;
			
			int index = offset;
			while (index > 0 && (text[index - 1] == '.' || CsHelpers.CanContinueIdentifier(text[index - 1])))
				--index;
			
			string expr = null;
			if (text[index] == '_' || CsHelpers.CanStartIdentifier(text[index]))
				expr = text.Substring(index, offset - index);
				
			return expr;
		}
		#endregion
		
		#region Fields
		private Boss m_boss;
		private IText m_text;
		private Database m_database;
		private CompletionsController m_controller;
		private ICachedCsCatalog m_cachedCatalog;
		private ICachedCsDeclarations m_cachedGlobals;
		private ICsLocalsParser m_locals;
		private ResolveTarget m_target;
		private ResolveType m_type;
		private ResolveMembers m_members;
		#endregion
	}
}
