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
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace CsRefactor.Script
{
	// ---------------------------------------------------------------------------
	internal sealed class BooleanLiteral : Expression, IEquatable<BooleanLiteral>
	{
		public BooleanLiteral(int line, bool value) : base(line)
		{
			m_value = value;
		}
		
		public override object Evaluate(Context context)
		{
			return m_value;
		}
		
		public override void Print(System.Text.StringBuilder buffer)
		{
			buffer.Append(m_value ? "true" : "false");
		}
		
		public override bool Equals(object obj)
		{
			if (obj == null)      
				return false;
				
			if (obj is bool)
				return m_value == (bool) obj;
			
			BooleanLiteral rhs = obj as BooleanLiteral;
			return this == rhs;
		}
			
		public bool Equals(BooleanLiteral rhs) 
		{
			return this == rhs;
		}
		
		public static bool operator==(BooleanLiteral lhs, BooleanLiteral rhs)
		{
			if (object.ReferenceEquals(lhs, rhs))
				return true;
			
			if ((object) lhs == null || (object) rhs == null)
				return false;
			
			return lhs.m_value == rhs.m_value;
		}
		
		public static bool operator!=(BooleanLiteral lhs, BooleanLiteral rhs)
		{
			return !(lhs == rhs);
		}
		
		public override int GetHashCode()
		{
			return m_value ? 1 : 0;
		}
		
		private bool m_value;
	}
	
	// ---------------------------------------------------------------------------
	internal sealed class From : Expression
	{
		public From(int line, string local, Expression elements, Expression filter, Expression map) : base(line)
		{
			Contract.Requires(!string.IsNullOrEmpty(local), "local is null or empty");
			Contract.Requires(elements != null, "elements is null");
			
			m_local = local;
			m_elements = elements;
			m_filter = filter;
			m_select = map;
		}
		
		public override object Evaluate(Context context)
		{
			Log.WriteLine("Refactor Evaluate", "{0}: From", Line);
			
			object r = m_elements.Evaluate(context);
			
			object[] elements = (object[]) r;
			if (elements == null && r != null)
				throw new EvaluateException(Line, "From expression should return a Sequence, but was a {0}.", RefactorType.GetName(r.GetType()));
			
			var result = new List<object>();
			if (elements != null)
			{
				foreach (object element in elements)
				{
					context.AddLocal(m_local, element);
						
					if (DoIsValidElement(context))
						if (m_select != null)
							result.Add(m_select.Evaluate(context));
						else
							result.Add(element);
						
					context.RemoveLocal(m_local);
				}
			}
			
			return result.ToArray();
		}

		public override void Print(System.Text.StringBuilder buffer)
		{
			buffer.Append("from ");
			buffer.Append(m_local);
			buffer.Append(" in ");
			m_elements.Print(buffer);
			
			if (m_filter != null)
			{
				buffer.Append(" where ");
				m_filter.Print(buffer);
			}
			
			if (m_select != null)
			{
				buffer.Append(" select ");
				m_select.Print(buffer);
			}
		}
		
		private bool DoIsValidElement(Context context)
		{
			bool valid = true;
			
			if (m_filter != null)
			{
				object predicate = m_filter.Evaluate(context);

				if (Equals(predicate, false))
				{
					valid = false;
				}
				else if (!Equals(predicate, true))
				{
					throw new EvaluateException(Line, "Where clause should be a Boolean, but is {0}.", predicate != null ? RefactorType.GetName(predicate.GetType()) : "null");
				}
			}
			
			return valid;
		}
		
		private string m_local;
		private Expression m_elements;
		private Expression m_filter;
		private Expression m_select;
	} 

	// ---------------------------------------------------------------------------
	internal sealed class InvokeMethod : Expression
	{
		public InvokeMethod(int line, Expression target, string method, Expression[] args)	: base(line)
		{
			Contract.Requires(target != null, "target is null");
			Contract.Requires(!string.IsNullOrEmpty(method), "method is null or empty");
			Contract.Requires(args != null, "args is null");
			
			m_target = target;
			m_method = method;
			m_args = args;
		}
		
		public override object Evaluate(Context context)
		{
			Log.WriteLine("Refactor Evaluate", "{0}: invoking {1}", Line, m_method);

			object target = m_target.Evaluate(context);
			object[] args = (from a in m_args select a.Evaluate(context)).ToArray();
			object result = CsRefactor.Script.Evaluate.Call(context, Line, target, m_method, args);
			
			return result;
		}

		public override void Print(System.Text.StringBuilder buffer)
		{
			m_target.Print(buffer);
			buffer.Append('.');
			buffer.Append(m_method);
					
			buffer.Append('(');
			for (int i = 0; i < m_args.Length; ++i)
			{
				m_args[i].Print(buffer);
				if (i + 1 < m_args.Length)
					buffer.Append(", ");
			}
			buffer.Append(')');
		}
		
		private Expression m_target;
		private string m_method;
		private Expression[] m_args;
	} 
	
	// ---------------------------------------------------------------------------
	internal sealed class Local : Expression
	{
		public Local(int line, string name) : base(line)
		{
			m_name = name;
		}
		
		public override object Evaluate(Context context)
		{
			return context.GetLocal(Line, m_name);
		}
		
		public override void Print(System.Text.StringBuilder buffer)
		{
			buffer.Append(m_name);
		}
		
		private string m_name;
	} 

	// ---------------------------------------------------------------------------
	internal sealed class NullLiteral : Expression
	{
		public NullLiteral(int line) : base(line)
		{
		}
		
		public override object Evaluate(Context context)
		{
			return null;
		}

		public override void Print(System.Text.StringBuilder buffer)
		{			
			buffer.Append("null");
		}		
	} 

	// ---------------------------------------------------------------------------
	internal sealed class SelfLiteral : Expression
	{
		public SelfLiteral(int line) : base(line)
		{
		}
		
		public override void Print(System.Text.StringBuilder buffer)
		{
			buffer.Append("self");
		}
		
		public override object Evaluate(Context context)
		{
			return context.Script;
		}
	} 
	
	// ---------------------------------------------------------------------------
	internal sealed class SequenceLiteral : Expression
	{
		public SequenceLiteral(int line, Expression[] value) : base(line)
		{
			Contract.Requires(value != null, "value is null");
			
			m_value = value;
		}
		
		public override object Evaluate(Context context)
		{
			object[] result = new object[m_value.Length];
			
			for (int i = 0; i < m_value.Length; ++i)
			{
				result[i] = m_value[i].Evaluate(context);
			}
			
			return result;
		}
		
		public override void Print(System.Text.StringBuilder buffer)
		{
			buffer.Append('[');
			for (int i = 0; i < m_value.Length; ++i)
			{
				m_value[i].Print(buffer);
				if (i + 1 < m_value.Length)
					buffer.Append(", ");
			}
			buffer.Append(']');
		}
		
		private Expression[] m_value;
	} 
		
	// ---------------------------------------------------------------------------
	internal sealed class StringLiteral : Expression
	{
		public StringLiteral(int line, string value) : base(line)
		{
			Contract.Requires(value != null, "value is null");
			
			m_value = value;
		}
		
		public override object Evaluate(Context context)
		{
//			return m_value;
			return m_value.Replace("\"\"", "\"");
		}
		
		public override void Print(System.Text.StringBuilder buffer)
		{
			buffer.Append('"');
			buffer.Append(m_value);
			buffer.Append('"');
		}
		
		private string m_value;
	} 
		
	// ---------------------------------------------------------------------------
	internal sealed class TypeName : Expression
	{
		public TypeName(int line, string name)	: base(line)
		{
			Contract.Requires(!string.IsNullOrEmpty(name), "name is null or empty");
			
			m_name = name;
		}
		
		public override object Evaluate(Context context)
		{
			return m_name;
		}
		
		public override void Print(System.Text.StringBuilder buffer)
		{
			buffer.Append(m_name);
		}
		
		private string m_name;
	} 

	// ---------------------------------------------------------------------------
	internal sealed class When : Expression
	{
		public When(int line, Expression predicate, Expression trueValue, Expression falseValue) : base(line)
		{
			Contract.Requires(predicate != null, "predicate is null");
			Contract.Requires(trueValue != null, "trueValue is null");
			Contract.Requires(falseValue != null, "falseValue is null");
			
			m_predicate = predicate;
			m_trueValue = trueValue;
			m_falseValue = falseValue;
		}
		
		public override object Evaluate(Context context)
		{
			Log.WriteLine("Refactor Evaluate", "{0}: When", Line);
			
			object result;
			object predicate = m_predicate.Evaluate(context);
			if (Equals(predicate, true))
			{
				result = m_trueValue.Evaluate(context);
			}
			else if (Equals(predicate, false))
			{
				result = m_falseValue.Evaluate(context);
			}
			else
			{
				throw new EvaluateException(m_predicate.Line, "Predicate should be a Boolean, but is {0}.", predicate != null ? RefactorType.GetName(predicate.GetType()) : "null");
			}
			
			return result;
		}
		
		public override void Print(System.Text.StringBuilder buffer)
		{
			m_trueValue.Print(buffer);
			buffer.Append(" when ");
			m_predicate.Print(buffer);
			buffer.Append(" else ");
			m_falseValue.Print(buffer);
		}
		
		private Expression m_predicate;
		private Expression m_trueValue;
		private Expression m_falseValue;
	} 
}
