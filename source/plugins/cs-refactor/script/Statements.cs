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
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace CsRefactor.Script
{
	[Serializable]
	public sealed class ReturnException : Exception
	{
		public ReturnException()
		{
		}
		
		public ReturnException(object value) : base("Return statement") 
		{
			Result = value;
		}
		
		[SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
		private ReturnException(SerializationInfo info, StreamingContext context) : base(info, context)
		{
			if (info.GetBoolean("HasResult"))
			{
				Type type = (Type) info.GetValue("ResultType", typeof(Type));
				Result = info.GetValue("Result", type);
			}
		}
		
		[SecurityPermission(SecurityAction.LinkDemand, SerializationFormatter = true)]
		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);
			
			info.AddValue("HasResult", Result != null);
			if (Result != null)
			{
				info.AddValue("ResultType", Result.GetType(), typeof(Type));
				info.AddValue("Result", Result, Result.GetType());
			}
		}
		
		public object Result {get; private set;}
	}
	
	// ---------------------------------------------------------------------------
	internal sealed class Conditional : Statement
	{
		public Conditional(int line, Expression[] predicates, Statement[][] blocks) : base(line)
		{
			Contract.Requires(predicates != null, "predicates is null");
			Contract.Requires(blocks != null, "blocks is null");
			Contract.Requires(predicates.Length == blocks.Length, "lengths differ");
			
			m_predicates = predicates;
			m_blocks = blocks;
		}
		
		public override object Evaluate(Context context)
		{
			Log.WriteLine("Refactor Evaluate", "{0}: Conditional", Line);
			
			object result = null;
			for (int i = 0; i < m_predicates.Length; ++i)
			{
				object predicate = m_predicates[i].Evaluate(context);
				if (Equals(predicate, true))
				{
					foreach (Statement statement in m_blocks[i])
					{
						result = statement.Evaluate(context);
					}
					break;
				}
				else if (!Equals(predicate, false))
				{
					throw new EvaluateException(m_predicates[i].Line, "Predicate should be a Boolean, but is {0}.", predicate != null ? RefactorType.GetName(predicate.GetType()) : "null");
				}
			}
			
			return result;
		}
		
		public override void Print(System.Text.StringBuilder buffer, string indent)
		{
			buffer.Append(indent);
			buffer.AppendLine("Conditional");
			indent += "\t";
			
			for (int i = 0; i < m_predicates.Length; ++i)
			{
				buffer.Append(indent);
				buffer.Append("if ");
				m_predicates[i].Print(buffer);
				buffer.AppendLine();
				
				for (int j = 0; j < m_blocks[i].Length; ++j)
				{
					m_blocks[i][j].Print(buffer, indent + "\t");
				}
			}
		}
		
		private Expression[] m_predicates;
		private Statement[][] m_blocks;
	}

	// ---------------------------------------------------------------------------
	internal sealed class For : Statement
	{
		public For(int line, string local, Expression elements, Expression filter, Statement[] block)	 : base(line)
		{
			Contract.Requires(!string.IsNullOrEmpty(local), "local is null or null");
			Contract.Requires(elements != null, "elements is null");
			Contract.Requires(block != null, "block is null");
			
			m_local = local;
			m_elements = elements;
			m_filter = filter;
			m_block = block;
		}
		
		public override object Evaluate(Context context)
		{
			Log.WriteLine("Refactor Evaluate", "{0}: For", Line);

			object result = m_elements.Evaluate(context);

			object[] elements = (object[]) result;
			if (elements == null && result != null)
				throw new EvaluateException(m_elements.Line, "For statement should return a Sequence, but was a {0}.", RefactorType.GetName(result.GetType()));
			
			result = null;
			if (elements != null)
			{
				foreach (object element in elements)
				{
					context.AddLocal(m_local, element);
						
					if (DoIsValidElement(context))
					{
						foreach (Statement statement in m_block)
						{
							result = statement.Evaluate(context);
						}
					}
	
					context.RemoveLocal(m_local);
				}
			}
			
			return result;
		}
		
		public override void Print(System.Text.StringBuilder buffer, string indent)
		{			
			buffer.Append(indent);
			buffer.Append("For ");
			buffer.Append(m_local);
			buffer.Append(" in ");
			m_elements.Print(buffer);
			if (m_filter != null)
			{
				buffer.Append(" where ");
				m_filter.Print(buffer);
			}
			buffer.AppendLine();
			
			indent += "\t";
			for (int i = 0; i < m_block.Length; ++i)
			{
				m_block[i].Print(buffer, indent);
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
					throw new EvaluateException(m_filter.Line, "Where clause should be a Boolean, but is {0}.", predicate != null ? RefactorType.GetName(predicate.GetType()) : "null");
				}
			}
			
			return valid;
		}
		
		private string m_local;
		private Expression m_elements;
		private Expression m_filter;
		private Statement[] m_block;
	}

	// ---------------------------------------------------------------------------
	internal sealed class Let : Statement
	{
		public Let(int line, string[] locals, Expression[] values, Statement[] block)	 : base(line)
		{
			Contract.Requires(locals != null, "locals is null");
			Contract.Requires(values != null, "value is null");
			Contract.Requires(locals.Length == values.Length, "lengths don't match");
			Contract.Requires(block != null, "block is null");
			
			m_locals = locals;
			m_values = values;
			m_block = block;
		}
		
		public override object Evaluate(Context context)
		{
			Log.WriteLine("Refactor Evaluate", "{0}: Let", Line);

			for (int i = 0; i < m_locals.Length; ++i)
			{
				object value = m_values[i].Evaluate(context);
				context.AddLocal(m_locals[i], value);
			}
				
			object result = null;
			foreach (Statement statement in m_block)
			{
				result = statement.Evaluate(context);
			}

			for (int i = 0; i < m_locals.Length; ++i)
			{
				context.RemoveLocal(m_locals[i]);
			}
			
			return result;
		}
		
		public override void Print(System.Text.StringBuilder buffer, string indent)
		{			
			buffer.Append(indent);
			buffer.Append("Let ");
			for (int i = 0; i < m_locals.Length; ++i)
			{
				buffer.Append(m_locals[i]);
				buffer.Append(" = ");
				m_values[i].Print(buffer);
				
				if (i + 1 < m_locals.Length)
					buffer.Append(", ");
			}
			buffer.AppendLine();
			
			indent += "\t";
			for (int i = 0; i < m_block.Length; ++i)
			{
				m_block[i].Print(buffer, indent);
			}
		}
		
		private string[] m_locals;
		private Expression[] m_values;
		private Statement[] m_block;
	}

	// ---------------------------------------------------------------------------
	internal sealed class MethodCall : Statement
	{
		public MethodCall(int line, Expression invoke) : base(line)
		{
			Contract.Requires(invoke != null, "invoke is null");
			
			m_invoke = invoke;
		}
				
		public override object Evaluate(Context context)
		{
			Log.WriteLine("Refactor Evaluate", "{0}: MethodCall", Line);

			object result = m_invoke.Evaluate(context);
			
			RefactorCommand command = result as RefactorCommand;
			if (command != null)
				context.AddCommand(command);
			
			return result;
		}
		
		public override void Print(System.Text.StringBuilder buffer, string indent)
		{
			buffer.Append(indent);
			m_invoke.Print(buffer);
			buffer.AppendLine();
		}
		
		private Expression m_invoke;
	}

	// ---------------------------------------------------------------------------
	internal sealed class Return : Statement
	{
		public Return(int line, Expression expr) : base(line)
		{
			Contract.Requires(expr != null, "expr is null");
			
			m_expr = expr;
		}
				
		public override object Evaluate(Context context)
		{
			Log.WriteLine("Refactor Evaluate", "{0}: Return", Line);

			throw new ReturnException(m_expr.Evaluate(context));
		}
		
		public override void Print(System.Text.StringBuilder buffer, string indent)
		{			
			buffer.Append(indent);
			buffer.Append("return ");
			m_expr.Print(buffer);
			buffer.AppendLine();
		}
		
		private Expression m_expr;
	} 
}
