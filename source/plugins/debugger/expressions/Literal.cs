// Copyright (C) 2010 Jesse Jones
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

using Mono.Debugger.Soft;
using Shared;

namespace Debugger
{
	internal sealed class Literal<T> : Expression
	{
		public Literal(T value)
		{
			m_value = value;
		}
		
		public override object Evaluate(StackFrame frame)
		{
			return m_value;
		}
		
		public override string ToString()
		{
			if (m_value == null)
				return "null";
			else if (m_value is char)
				return string.Format("'{0}'", CharHelpers.ToText((char) (object) m_value));
			else if (m_value is string)
				return string.Format("\"{0}\"", ((string) (object) m_value).EscapeAll());
			else
				return m_value.ToString();
		}
		
		private T m_value;
	}
}
