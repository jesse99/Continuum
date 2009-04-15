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
using Shared;
using System;
using System.Diagnostics;

namespace CsRefactor
{
	internal sealed class RefactorScript : IRefactorScript
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public string Execute(string rSource, IText cSource, int selStart, int selLen)
		{
			// Parse the C# code..
			var editor = cSource.Boss.Get<ITextEditor>();
			
			Boss boss = ObjectModel.Create("CsParser");
			var parses = boss.Get<IParses>();
			Parse parse = parses.Parse(editor.Path, cSource.EditCount, cSource.Text);
			CsGlobalNamespace globals = parse.Globals;
			
			// Parse the refactor script.
			var rParser = new CsRefactor.Script.Parser(rSource);
			CsRefactor.Script.Script script = rParser.Parse();
			
			// Evaluate the script.
			var context = new CsRefactor.Script.Context(script, globals, cSource.Text, selStart, selLen);
			RefactorCommand[] commands = script.Evaluate(context);
			
			// Execute the script.
			Refactor refactor = new Refactor(cSource.Text);
			foreach (RefactorCommand command in commands)
			{
				refactor.Queue(command);
			}
			string result = refactor.Process();
			
			// Expand bullet characters.
			DoGetSettings(cSource.Boss);
			result = DoExpandText(result);
			
			return result;
		}
		
		#region Private Methods
		private string DoExpandText(string text)
		{
			int i = text.IndexOf(Constants.Bullet);
			while (i >= 0)
			{
				Contract.Assert(i < text.Length, "bullet is at the end of the text");
				Contract.Assert(text[i + 1] == '(' || text[i + 1] == '[', "character after the bullet is not ( or [");
				
				if (m_addSpace)
					text = text.Substring(0, i) + ' ' + text.Substring(i + 1);
				else
					text = text.Substring(0, i) + text.Substring(i + 1);
				
				i = text.IndexOf(Constants.Bullet, i);
			}
			
			return text;
		}
		
		private void DoGetSettings(Boss textBoss)
		{
			Boss boss = ObjectModel.Create("DirectoryEditorPlugin");
			var find = boss.Get<IFindDirectoryEditor>();
			boss = find.GetDirectoryEditor(textBoss);
			
			var editor = boss.Get<IDirectoryEditor>();
			m_addSpace = editor.AddSpace;
		}
		#endregion
		
		#region Fields
		private Boss m_boss;
		private bool m_addSpace;
		#endregion
	}
}
