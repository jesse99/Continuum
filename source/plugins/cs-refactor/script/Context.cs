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

using Gear.Helpers;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CsRefactor.Script
{
	internal sealed class Context
	{
		public Context(Script script, CsGlobalNamespace globals, string text, int selStart, int selLen)
		{
			Contract.Requires(script != null, "script is null");
			Contract.Requires(globals != null, "globals is null");
			Contract.Requires(text != null, "text is null");
			Contract.Requires(selStart >= 0, "selStart is negative");
			Contract.Requires(selLen >= 0, "selLen is negative");
			
			Script = script;
			Text = text;
			Globals = globals;
			SelStart = selStart;
			SelLen = selLen;
		}
		
		public bool TracingEnabled {get; set;}
		
		public int MethodDepth {get; set;}
		
		public Script Script {get; private set;}
		
		public CsGlobalNamespace Globals {get; private set;}
		
		public string Text {get; private set;}
		
		public int SelStart {get; private set;}
		
		public int SelLen {get; private set;}
		
		public object GetLocal(int line, string name)
		{
			object result;
			if (!m_locals.Last().TryGetValue(name, out result))
				throw new EvaluateException(line, "The {0} local is not defined.", name);
			
			return result;
		}
		
		public int PushLocals()
		{
			m_locals.Add(new Dictionary<string, object>());
			return m_locals.Count;
		}
		
		public void PopLocals()
		{
			m_locals.RemoveLast();
		}
		
		public void AddLocal(string name, object value)
		{	
#if DEBUG
			Contract.Requires(!m_locals.Last().ContainsKey(name), string.Format("The {0} local is already defined", name));
#endif
			
			m_locals.Last().Add(name, value);
		}
		
		public void RemoveLocal(string name)
		{
			m_locals.Last().Remove(name);
		}
		
		public RefactorCommand[] Commands {get {return m_commands.ToArray();}}
		
		public void AddCommand(RefactorCommand command)
		{
			// Commands are added after a method returns and methods return whatever
			// they last executed. This means that a method which calls a method which
			// returns an edit will attempt to add the edit twice. This is bad so we
			// check for it here.
			if (m_commands.Count == 0 || m_commands[m_commands.Count - 1] != command)
				m_commands.Add(command);
		}

		#region Fields		
		private List<RefactorCommand> m_commands = new List<RefactorCommand>();
		private List<Dictionary<string, object>> m_locals = new List<Dictionary<string, object>>();
		#endregion
	} 
}
