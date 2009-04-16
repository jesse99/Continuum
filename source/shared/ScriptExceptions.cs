// Copyright (C) 2008 Jesse Jones
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

using System;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace Shared
{
	// Base class used when parsing or execution of a script fails.
	[Serializable]
	public abstract class ScriptException : Exception
	{
		protected ScriptException()
		{
		}
		
		protected ScriptException(int line, string text) : base(string.Format("Line {0}: {1}", line, text)) 
		{
			Line = line;
		}
		
		protected ScriptException(int line, string text, Exception inner) : base (string.Format("Line {0}: {1}", line, text), inner)
		{
			Line = line;
		}
		
		public int Line {get; private set;}
		
		[SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
		protected ScriptException(SerializationInfo info, StreamingContext context) : base(info, context)
		{
			Line = info.GetInt32("Line");
		}
		
		[SecurityPermission(SecurityAction.LinkDemand, SerializationFormatter = true)]
		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);
			
			info.AddValue("Line", Line);
		}
	}
	
	// Used when a script cannot run.
	[Serializable]
	public sealed class ScriptAbortException : Exception
	{
		public ScriptAbortException()
		{
		}
		
		public ScriptAbortException(string text) : base(text)
		{
		}
		
		public ScriptAbortException(string text, Exception inner) : base (text, inner)
		{
		}
		
		[SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
		private ScriptAbortException(SerializationInfo info, StreamingContext context) : base(info, context)
		{
		}
	}
}
