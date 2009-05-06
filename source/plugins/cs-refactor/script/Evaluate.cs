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
using System.Text;

namespace CsRefactor.Script
{
	[Serializable]
	internal sealed class EvaluateException : ScriptException
	{
		public EvaluateException()
		{
		}
		
		public EvaluateException(int line, string text) : base(line, text) 
		{
		}
		
		public EvaluateException(int line, string format, params object[] args) : base(line, string.Format(format, args)) 
		{
		}
		
		public EvaluateException(int line, string text, Exception inner) : base (line, text, inner)
		{
		}
		
		[SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
		private EvaluateException(SerializationInfo info, StreamingContext context) : base(info, context)
		{
		}
	}
	
	// Uses the RefactorTypes to evaluate refactor method calls.
	internal static class Evaluate
	{
		public static object Call(Context context, int line, object instance, string method, object[] args)
		{
			RefactorType type;
			if (instance == null)
				type = VoidType.Instance;
			
			else if (instance.GetType().IsArray)
				type = SequenceType.Instance;
			
			else if (!ms_types.TryGetValue(instance.GetType(), out type))
				throw new EvaluateException(line, "{0} is not a valid refactor type.", instance.GetType());
			
			if (context.TracingEnabled)
				Console.WriteLine("{0} {1}.{2}({3})", new string(' ', 4*context.MethodDepth), type.Name, method, DoStringifyArgs(args));
			context.MethodDepth += 1;
			if (context.MethodDepth > 256)
				throw new EvaluateException(line, "Method calls have recursed more than 256 times");
			
			object result = type.Execute(line, instance, method, args);
			
			context.MethodDepth -= 1;
			if (context.TracingEnabled)
				Console.WriteLine("{0} => {1}", new string(' ', 4*context.MethodDepth), result.QuotedStringify());
			
			return result;
		}
		
		#region Private Methods
		static Evaluate()
		{
			DoAddType(AttributeType.Instance);
			DoAddType(BodyType.Instance);
			DoAddType(BooleanType.Instance);
			DoAddType(ClassType.Instance);
			DoAddType(DeclarationType.Instance);
			DoAddType(DelegateType.Instance);
			DoAddType(EditType.Instance);
			DoAddType(EnumType.Instance);
			DoAddType(EventType.Instance);
			DoAddType(ExternAliasType.Instance);
			DoAddType(FieldType.Instance);
			DoAddType(GlobalNamespaceType.Instance);
			DoAddType(IndexerType.Instance);
			DoAddType(InterfaceType.Instance);
			DoAddType(MemberType.Instance);
			DoAddType(MethodType.Instance);
			DoAddType(NamespaceType.Instance);
			DoAddType(ObjectType.Instance);
			DoAddType(OperatorType.Instance);
			DoAddType(TypeScopeType.Instance);
			DoAddType(ParameterType.Instance);
			DoAddType(PropertyType.Instance);
			DoAddType(ScriptType.Instance);
			DoAddType(SequenceType.Instance);
			DoAddType(StringType.Instance);
			DoAddType(StructType.Instance);
			DoAddType(TypeDeclarationType.Instance);
			DoAddType(UsingAliasType.Instance);
			DoAddType(UsingDirectiveType.Instance);
			Unused.Value = VoidType.Instance;
		}
		
		private static void DoAddType(RefactorType type)
		{
			ms_types.Add(type.ManagedType, type);
		}
		
		private static string DoStringifyArgs(object[] args)
		{
			var builder = new StringBuilder();
			
			for (int i = 0; i < args.Length; ++i)
			{
				builder.Append(args[i].QuotedStringify());

				if (i + 1 < args.Length)
					builder.Append(", ");
			}
			
			return builder.ToString();
		}
		#endregion
		
		#region Fields		
		private static Dictionary<Type, RefactorType> ms_types = new Dictionary<Type, RefactorType>();
		#endregion
	} 
}
