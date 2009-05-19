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
using Mono.Cecil;
using Mono.Cecil.Cil;
using Shared;
using System;
using System.Collections.Generic;
using System.Text;

namespace Disassembler
{
	internal static class DisassembleExtensions
	{
		public static string Disassemble(this MethodDefinition method)
		{
			var builder = new StringBuilder();
			
			DoAppendHeader(builder, method);
			if (method.HasBody)
				DoAppendBody(builder, method.Body);
			else
				builder.AppendLine("// no body");
				
			return builder.ToString();
		}
		
		#region Private Methods
		private static void DoAppendBody(StringBuilder builder, MethodBody body)
		{
			foreach (Instruction ins in body.Instructions)
			{
				builder.AppendFormat("{0:X4} ", ins.Offset);
				
				string name = ins.OpCode.Name;
				builder.Append(name);
				
				if (ins.Operand != null)
				{
					builder.Append('\t');
						
					if (ins.Operand is string)
						builder.AppendFormat("\"{0}\"", ins.Operand.ToString().EscapeAll());
					else if (ins.Operand is Instruction)
						builder.AppendFormat("{0:X4}", ((Instruction) ins.Operand).Offset);
					else
						builder.Append(ins.Operand.ToString());
				}
				
				builder.AppendLine();
			}
		}
		
		private static void DoAppendHeader(StringBuilder builder, MethodDefinition method)
		{
			// TODO: custom attributes
			DoAppendAttributes(builder, method.Attributes);
			
			if (!method.IsConstructor)
			{
				builder.Append(method.ReturnType.ReturnType.FullName);
				builder.Append(' ');
			}
			
			builder.Append(method.DeclaringType.FullName);
			builder.Append("::");
			builder.Append(method.Name);
			if (method.HasGenericParameters)
				DoAppendGenericParams(builder, method.GenericParameters);
				
			DoAppendParams(builder, method);
			builder.AppendLine();
		}
		
		private static void DoAppendAttributes(StringBuilder builder, MethodAttributes attrs)
		{
			switch (attrs & MethodAttributes.MemberAccessMask)
			{
				case MethodAttributes.Compilercontrolled:
					builder.Append("compiler-controlled ");
					break;
					
				case MethodAttributes.Private:
					builder.Append("private ");
					break;
					
				case MethodAttributes.FamANDAssem:
					builder.Append("family-and-assembly ");
					break;
					
				case MethodAttributes.Assem:
					builder.Append("assembly ");
					break;
					
				case MethodAttributes.Family:
					builder.Append("family ");
					break;
					
				case MethodAttributes.FamORAssem:
					builder.Append("family-or-assembly ");
					break;
					
				case MethodAttributes.Public:
					builder.Append("public ");
					break;
				
				default:
					Contract.Assert(false, "bad access: " + (attrs & MethodAttributes.MemberAccessMask));
					break;
			}
			
			if ((attrs & MethodAttributes.NewSlot) == MethodAttributes.NewSlot)
				builder.Append("new ");
				
			if ((attrs & MethodAttributes.Static) == MethodAttributes.Static)
				builder.Append("static ");
				
			if ((attrs & MethodAttributes.Virtual) == MethodAttributes.Virtual)
				builder.Append("virtual ");
				
			if ((attrs & MethodAttributes.Abstract) == MethodAttributes.Abstract)
				builder.Append("abstract ");
				
			if ((attrs & MethodAttributes.Final) == MethodAttributes.Final)
				builder.Append("final ");
		}
		
		private static void DoAppendGenericParams(StringBuilder builder, GenericParameterCollection parms)
		{
			builder.Append("<");
			for (int i = 0; i < parms.Count; ++i)
			{
				builder.Append(parms[i].Name);
				
				if (i + 1 < parms.Count)
					builder.Append(", ");
			}
			builder.Append(">");
		}
		
		private static void DoAppendParams(StringBuilder builder, MethodDefinition method)
		{
			builder.Append('(');
			for (int i = 0; i < method.Parameters.Count; ++i)
			{
				builder.Append(method.GetParameterModifier(i));
				
				ParameterDefinition param = method.Parameters[i];
				builder.Append(param.ParameterType.FullName);
				builder.Append(' ');
				builder.Append(param.Name);
				
				if (i + 1 < method.Parameters.Count)
					builder.Append(", ");
			}
			builder.Append(')');
		}
		#endregion
	}
}
