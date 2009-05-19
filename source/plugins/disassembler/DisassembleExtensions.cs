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
		// The ExceptionHandlers are a little bit weird: there is one ExceptionHandler for
		// each catch/finally block and each ExceptionHandler includes the range for the
		// corresponding try block.
		private static void DoAppendBody(StringBuilder builder, MethodBody body)
		{
			int indent = 0;
			foreach (Instruction ins in body.Instructions)
			{
				DoAppendHandler(builder, body, ins, ref indent);
				
				builder.AppendFormat("{0:X4} ", ins.Offset);
				builder.Append('\t', indent);
				
				string name = ins.OpCode.Name;
				builder.Append(name);
				
				if (ins.Operand != null)
				{
					if (name.Length <= 4)
						builder.Append("\t\t");
					else
						builder.Append('\t');
					
					builder.Append(DoArgToString(ins.Operand));
				}
				
				builder.AppendLine();
			}
		}
		
		private static void DoAppendHandler(StringBuilder builder, MethodBody body, Instruction ins, ref int indent)
		{
			if (body.HasExceptionHandlers)
			{
				string text = null;
				int oldIndent = indent;
				
				if (DoMatchHandler(body, ins, h => h.TryStart) != null)
				{
					text = "try";
					++indent;
				}
				else if (DoMatchHandler(body, ins, h => h.TryEnd) != null)
				{
					--indent;
				}
				else if (DoMatchHandler(body, ins, h => h.HandlerEnd) != null)
				{
					--indent;
				}
				
				ExceptionHandler handler = DoMatchHandler(body, ins, h => h.HandlerStart);
				if (handler != null)
				{
					text = handler.Type.ToString().ToLower();
					if (handler.CatchType != null)
						text += ' ' + handler.CatchType.FullName;
					--oldIndent;
					++indent;
				}
				
				if (text != null)
				{
					builder.AppendFormat("{0:X4} ", ins.Offset);
					builder.Append('\t', oldIndent);
					builder.AppendLine(text);
				}
			}
		}
		
		// This could be optimized a fair amount.
		private static ExceptionHandler DoMatchHandler(MethodBody body, Instruction ins, Func<ExceptionHandler, Instruction> callback)
		{
			foreach (ExceptionHandler handler in body.ExceptionHandlers)
			{
				if (callback(handler).Offset == ins.Offset)
					return handler;
			}
			
			return null;
		}
		
		private static void DoAppendHeader(StringBuilder builder, MethodDefinition method)
		{
			if (method.HasCustomAttributes)
				DoAppendCustomAttributes(builder, method.CustomAttributes);
			if (method.PInvokeInfo != null)
				DoAppendPInvoke(builder, method, method.PInvokeInfo);
			if (method.HasSecurityDeclarations)
				DoAppendSecurity(builder, method.SecurityDeclarations);
			if (method.ImplAttributes != MethodImplAttributes.IL)
				builder.AppendFormat("[System.Runtime.CompilerServices.MethodImplAttribute(System.Runtime.CompilerServices.MethodImplOptions.{0})]{1}", method.ImplAttributes, Environment.NewLine);
			
			DoAppendAttributes(builder, method.Attributes);
			
			if (!method.IsConstructor)
			{
				builder.Append(method.ReturnType.ReturnType.FullName);
				builder.Append(' ');
			}
			
			builder.Append(method.Name);
			if (method.HasGenericParameters)
				DoAppendGenericParams(builder, method.GenericParameters);
				
			DoAppendParams(builder, method);
			builder.AppendLine();
		}
		
		private static void DoAppendPInvoke(StringBuilder builder, MethodDefinition method, PInvokeInfo info)
		{
			builder.Append("[System.Runtime.InteropServices.DllImportAttribute(\"");
			builder.Append(info.Module.Name);
			if (info.EntryPoint != method.Name)
			{
				builder.Append("\", EntryPoint = \"");
				builder.Append(info.EntryPoint);
			}
			builder.AppendLine("\")]");
		}
		
		private static void DoAppendSecurity(StringBuilder builder, SecurityDeclarationCollection secs)
		{
			foreach (SecurityDeclaration sec in secs)
			{
				if (sec.PermissionSet.IsUnrestricted())
				{
					builder.AppendFormat("[{0} {1}]{2}", sec.Action, "unrestricted", Environment.NewLine);
				}
				else
				{
					builder.AppendFormat("[{0} {1}]{2}", sec.Action, sec.PermissionSet, Environment.NewLine);
				}
				
				foreach (object o in sec.PermissionSet)
				{
					builder.AppendFormat("[{0}]{1}", o, Environment.NewLine);
				}
			}
		}
		
		private static void DoAppendCustomAttributes(StringBuilder builder, CustomAttributeCollection attrs)
		{
			foreach (CustomAttribute attr in attrs)
			{
				var args = new List<string>();
				
				attr.Resolve ();			// we need to do this so things like the enums used within arguments show up correctly
				foreach (object o in attr.ConstructorParameters)
				{
					args.Add(DoArgToString(o));
				}
				
				foreach (System.Collections.DictionaryEntry d in attr.Properties)
				{
					args.Add(string.Format("{0} = {1}", d.Key, DoArgToString(d.Value)));
				}
				
				foreach (System.Collections.DictionaryEntry d in attr.Fields)
				{
					args.Add(string.Format("{0} = {1}", d.Key, DoArgToString(d.Value)));
				}
				
				string name = attr.Constructor.DeclaringType.FullName;
				builder.AppendFormat("[{0}({1})]", name, string.Join(", ", args.ToArray()));
				builder.AppendLine();
			}
		}
		
		private static string DoArgToString(object arg)
		{
			if (arg == null)
				return string.Empty;
			
			else if (arg is string)
				return string.Format("\"{0}\"", arg);
			
			else if (arg is Instruction)
				return string.Format("{0:X4}", ((Instruction) arg).Offset);
			
			else
				return arg.ToString();
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
