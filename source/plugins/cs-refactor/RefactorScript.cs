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
		
		public string Execute(string rSource, string cSource, int selStart, int selLen)
		{
			// Parse the C# code. Note that we don't use ICachedCsDeclarations here because
			// we want to ensure that the text is well formed. TODO: but it has a malformed
			// flag now so we can use it if the editCount is current.
			Boss boss = ObjectModel.Create("CsParser");
			var cParser = boss.Get<ICsParser>();
			CsGlobalNamespace globals = cParser.Parse(cSource);
			
			// Parse the refactor script.
			var rParser = new CsRefactor.Script.Parser(rSource);
			CsRefactor.Script.Script script = rParser.Parse();
			
			// Evaluate the script.
			var context = new CsRefactor.Script.Context(script, globals, cSource, selStart, selLen);
			RefactorCommand[] commands = script.Evaluate(context);
			
			// Execute the script.
			Refactor refactor = new Refactor(cSource);
			foreach (RefactorCommand command in commands)
			{
				refactor.Queue(command);
			}
			string result = refactor.Process();
			
			return result;
		}
		
		#region Fields
		private Boss m_boss;
		#endregion
	}
}
