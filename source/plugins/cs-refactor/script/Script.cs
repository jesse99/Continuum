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

using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CsRefactor.Script
{
	internal sealed class Script
	{
		public Script(Method[] methods)
		{
			Contract.Requires(methods != null, "methods is null");
			
			Methods = methods;
		}
		
		public Context Context {get; private set;}

		public Method[] Methods {get; private set;}
		
		public RefactorCommand[] Evaluate(Context context)
		{	
			ScriptType.Instance.RegisterCustomMethods(context, Methods);
						
			Context = context;
			try
			{
				context.TracingEnabled = false;
				context.MethodDepth = 0;
				m_saved.Clear();
				Method method = Methods.FirstOrDefault(m => m.Name == "get_EnableTracing");
				if (method != null)
				{
					object value = method.Evaluate(0, context, new object[0]);
					context.TracingEnabled = Equals(value, true);
				}

				method = Methods.First(m => m.Name == "Run");	// parser will ensure this exists
				object result = method.Evaluate(0, context, new object[0]);

				if (result != null && !typeof(RefactorCommand).IsAssignableFrom(result.GetType()))
					throw new EvaluateException(1, "Run should return null or an Edit, not {0}.", RefactorType.GetName(result.GetType()));
			}
			finally
			{
				Context = null;
			}
			
			return context.Commands;
		}
		
		public override string ToString()
		{
			System.Text.StringBuilder buffer = new System.Text.StringBuilder();
			
			for (int i = 0; i < Methods.Length; ++i)
			{
				Methods[i].Print(buffer);
				
				if (i + 1 < Methods.Length)
					buffer.AppendLine();
			}
			
			return buffer.ToString();
		}
		
		public string GetSaved()
		{
			return string.Join("\n", m_saved.ToArray());
		}
		
		public void Save(string text)		// for unit testing
		{
			m_saved.Add(text);
		}
		
		private List<string> m_saved = new List<string>();
	} 
}
