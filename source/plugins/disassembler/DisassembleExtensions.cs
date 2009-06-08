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
using System.Security;
using System.Security.Permissions;
using System.Text;

namespace Disassembler
{
	internal static class DisassembleExtensions
	{
		public static string Disassemble(this TypeDefinition type)
		{
			var builder = new StringBuilder();
			
			DoAppendTypeHeader(builder, type);
			builder.AppendLine();
			
			for (int i = 0; i < type.Constructors.Count; ++i)
			{
				DoAppendMethod(builder, type.Constructors[i]);
				
				if (i + 1 < type.Constructors.Count || type.Methods.Count > 0 || type.Fields.Count > 0)
				{
					builder.AppendLine();
					builder.AppendLine();
				}
			}
			
			for (int i = 0; i < type.Methods.Count; ++i)
			{
				DoAppendMethod(builder, type.Methods[i]);
				
				if (i + 1 < type.Methods.Count || type.Fields.Count > 0)
				{
					builder.AppendLine();
					builder.AppendLine();
				}
			}
			
			for (int i = 0; i < type.Fields.Count; ++i)
			{
				DoAppendField(builder, type.Fields[i]);
				
				if (i + 1 < type.Fields.Count)
					builder.AppendLine();
			}
			
			return builder.ToString();
		}
		
		public static string Disassemble(this MethodDefinition method)
		{
			var builder = new StringBuilder();
			
			DoAppendMethod(builder, method);
				
			return builder.ToString();
		}
		
		#region Private Methods
		private static void DoAppendTypeHeader(StringBuilder builder, TypeDefinition type)
		{
			if (type.HasCustomAttributes)
				DoAppendCustomAttributes(builder, type.CustomAttributes);
			if ((type.Attributes & TypeAttributes.LayoutMask) != 0 ||					// note that we have to use the 0 literal or the runtime gets confused about which zero enum we're referring to
				(type.Attributes & TypeAttributes.StringFormatMask) != 0 ||
				type.PackingSize != 0)
				builder.AppendLine(type.LayoutToText(true));
			if (type.HasSecurityDeclarations)
				DoAppendSecurity(builder, type.SecurityDeclarations);
			if (type.IsSerializable)
				builder.AppendLine("[System.Serializable()]");
			
			DoAppendTypeAttributes(builder, type.Attributes);
			
			if (type.IsEnum)
				builder.Append("enum ");
			else if (type.IsInterface)
				builder.Append("interface ");
			else if (type.IsValueType)
				builder.Append("struct ");
			else
				builder.Append("class ");
			
			DoAppendTypeName(builder, type);
			
			if (type.BaseType != null || type.HasInterfaces)
			{
				builder.Append(" : ");
				
				if (type.BaseType != null)
				{
					DoAppendTypeName(builder, type.BaseType);
					
					if (type.HasInterfaces)
						builder.Append(", ");
				}
				
				for (int i = 0; i < type.Interfaces.Count; ++i)
				{
					DoAppendTypeName(builder, type.Interfaces[i]);
					
					if (i + 1 < type.Interfaces.Count)
						builder.Append(", ");
				}
			}
			
			builder.AppendLine();
		}
		
		private static void DoAppendField(StringBuilder builder, FieldDefinition field)
		{
			if (field.HasCustomAttributes)
				DoAppendCustomAttributes(builder, field.CustomAttributes);
			if (field.IsNotSerialized)
				builder.AppendLine("[System.NonSerialized()]");
				
			DoAppendFieldAttributes(builder, field.Attributes);
			if (field.IsLiteral)
				builder.Append("const ");
			if (field.IsInitOnly)
				builder.Append("readonly ");
			
			builder.Append(field.FieldType.FullName);
			builder.Append(' ');
			builder.Append(field.Name);
			
			if (field.Constant != null)
			{
				builder.Append(" = ");												// this works for ints but not for strings
				builder.Append(CecilExtensions.ArgToString(field.FieldType, field.Constant, true));	
			}
			else if (field.InitialValue != null)
			{
				builder.Append(" = ");												// not sure when this works
				builder.Append(BitConverter.ToString(field.InitialValue));
			}
			
			builder.AppendLine();
		}
		
		private static void DoAppendTypeName(StringBuilder builder, TypeReference type)
		{
			if (type.DeclaringType != null)
			{
				DoAppendTypeName(builder, type.DeclaringType);
				builder.Append('/');
			}
			
			if (!string.IsNullOrEmpty(type.Namespace))
			{
				builder.Append(type.Namespace);
				builder.Append('.');
			}
			
			builder.Append(type.Name);
			
			if (type.HasGenericParameters)
				DoAppendGenericParams(builder, type.GenericParameters);
		}
		
		public static void DoAppendMethod(StringBuilder builder,  MethodDefinition method)
		{
			DoAppendMethodHeader(builder, method);
			if (method.HasBody)
				DoAppendBody(builder, method);
		}
		
		private const int PdbHiddenLine = 0xFEEFEE;
		
		private static string[] DoGetSource(MethodDefinition method)
		{
			string[] source = null;
			
			// Find the first sequence point (it won't always be at the first instruction).
			Instruction ins = method.Body.Instructions[0];
			while (ins != null && ins.SequencePoint == null)
				ins = ins.Next;
			
			// Load the source. TODO: Document has a Hash property that we could use
			// to check to see if the source file matches the file used to compile the assembly
			// but it does not appear to be set with gmcs 2.4.
			if (ins != null && ins.SequencePoint.Document != null && ins.SequencePoint.Document.Url != null)
			{
				try
				{
					source = System.IO.File.ReadAllLines(ins.SequencePoint.Document.Url);
				}
				catch
				{
				}
			}
			
			return source;
		}
		
		private static void DoAppendBody(StringBuilder builder, MethodDefinition method)
		{
			string[] source = DoGetSource(method);
			
			// The tab stops in the disassembly are unusual so the source code looks better
			// if we get rid of tabs.
			for (int i = 0; i < source.Length; ++i)
			{
				source[i] = source[i].Replace("\t", "    ");
			}
			
			int indent = 0;
			int lastLine = -1;
			foreach (Instruction ins in method.Body.Instructions)
			{
				if (source != null && ins.SequencePoint != null)
				{
					if (ins.SequencePoint.StartLine != PdbHiddenLine && ins.SequencePoint.StartLine > lastLine)
					{
						if (ins.SequencePoint.StartLine - 1 < source.Length)
						{
							builder.AppendLine();
							if (lastLine > 0)
							{
								for (int i = lastLine + 1; i < ins.SequencePoint.StartLine; ++i)
								{
									builder.AppendFormat("// {0}", source[i - 1]);
									builder.AppendLine();
								}
							}
							builder.AppendFormat("// {0}", source[ins.SequencePoint.StartLine - 1]);
							builder.AppendLine();
							
							lastLine = ins.SequencePoint.StartLine;
						}
					}
				}
				
				DoAppendHandler(builder, method.Body, ins, ref indent);
				
				builder.AppendFormat("{0:X4} ", ins.Offset);
				builder.Append('\t', indent);
				
				string name = ins.OpCode.Name;
				builder.Append(name);
				
				string operand = DoGetOperand(method, ins);
				if (operand != null)
				{
					if (name.Length <= 4)
						builder.Append("\t\t");
					else
						builder.Append('\t');
					
					builder.Append(operand);
				}
				
				builder.AppendLine();
			}
		}
		
		private static string DoGetOperand(MethodDefinition method, Instruction ins)
		{
			string text = null;
			
			switch (ins.OpCode.Code)
			{
				case Code.Ldarg_0:
				case Code.Ldarg_1:
				case Code.Ldarg_2:
				case Code.Ldarg_3:
					int index = ins.OpCode.Code - Code.Ldarg_0;
					if (!method.IsStatic)
						index--;
					
					if (index >= 0)
						text = method.Parameters[index].ToString();
					else
						text = "this";
					break;
					
				case Code.Ldloc_0:
				case Code.Ldloc_1:
				case Code.Ldloc_2:
				case Code.Ldloc_3:
					text = method.Body.Variables[ins.OpCode.Code - Code.Ldloc_0].ToString();
					break;
				
				case Code.Stloc_0:
				case Code.Stloc_1:
				case Code.Stloc_2:
				case Code.Stloc_3:
					text = method.Body.Variables[ins.OpCode.Code - Code.Stloc_0].ToString();
					break;
				
				default:
					if (ins.Operand != null)
						text = CecilExtensions.ArgToString(ins.Operand);
					break;
			}
			
			return text;
		}
		
		// The ExceptionHandlers are a little bit weird: there is one ExceptionHandler for
		// each catch/finally block and each ExceptionHandler includes the range for the
		// corresponding try block.
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
		
		private static void DoAppendMethodHeader(StringBuilder builder, MethodDefinition method)
		{
			if (method.HasCustomAttributes)
				DoAppendCustomAttributes(builder, method.CustomAttributes);
			if (method.PInvokeInfo != null)
				DoAppendPInvoke(builder, method, method.PInvokeInfo);
			if (method.HasSecurityDeclarations)
				DoAppendSecurity(builder, method.SecurityDeclarations);
			if (method.ImplAttributes != MethodImplAttributes.IL)
				builder.AppendFormat("[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.{0})]{1}", method.ImplAttributes, Environment.NewLine);
			
			DoAppendMethodAttributes(builder, method.Attributes);
			
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
			builder.Append("[System.Runtime.InteropServices.DllImport(\"");
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
				builder.AppendLine(sec.ToText(true));
			}
		}
		
		private static void DoAppendCustomAttributes(StringBuilder builder, CustomAttributeCollection attrs)
		{
			foreach (CustomAttribute attr in attrs)
			{
				builder.AppendLine(attr.ToText(true));
			}
		}
		
		private static void DoAppendTypeAttributes(StringBuilder builder, TypeAttributes attrs)
		{
			switch (attrs & TypeAttributes.VisibilityMask)
			{
				case TypeAttributes.NotPublic:
				case TypeAttributes.NestedAssembly:
					builder.Append("assembly ");
					break;
					
				case TypeAttributes.Public:
				case TypeAttributes.NestedPublic:
					builder.Append("public ");
					break;
					
				case TypeAttributes.NestedPrivate:
					builder.Append("private ");
					break;
					
				case TypeAttributes.NestedFamily:
					builder.Append("family ");
					break;
					
				case TypeAttributes.NestedFamANDAssem:
					builder.Append("family-and-assembly ");
					break;
					
				case TypeAttributes.NestedFamORAssem:
					builder.Append("family-or-assembly ");
					break;
					
				default:
					Contract.Assert(false, "bad visibility: " + (attrs & TypeAttributes.VisibilityMask));
					break;
			}
			
			if ((attrs & TypeAttributes.Abstract) == TypeAttributes.Abstract)
				builder.Append("abstract ");
			
			if ((attrs & TypeAttributes.Sealed) == TypeAttributes.Sealed)
				builder.Append("sealed ");
			
			if ((attrs & TypeAttributes.SpecialName) == TypeAttributes.SpecialName)
				builder.Append("special-name ");
			
			if ((attrs & TypeAttributes.Import) == TypeAttributes.Import)
				builder.Append("import ");
		}
		
		private static void DoAppendFieldAttributes(StringBuilder builder, FieldAttributes attrs)
		{
			switch (attrs & FieldAttributes.FieldAccessMask)
			{
				case FieldAttributes.Compilercontrolled:
					builder.Append("compiler-controlled ");
					break;
					
				case FieldAttributes.Private:
					builder.Append("private ");
					break;
					
				case FieldAttributes.FamANDAssem:
					builder.Append("family-and-assembly ");
					break;
					
				case FieldAttributes.Assembly:
					builder.Append("assembly ");
					break;
					
				case FieldAttributes.Family:
					builder.Append("family ");
					break;
					
				case FieldAttributes.FamORAssem:
					builder.Append("family-or-assembly ");
					break;
					
				case FieldAttributes.Public:
					builder.Append("public ");
					break;
					
				default:
					Contract.Assert(false, "bad access: " + (attrs & FieldAttributes.FieldAccessMask));
					break;
			}
			
			if ((attrs & FieldAttributes.Static) == FieldAttributes.Static)
				builder.Append("static ");
			
			if ((attrs & FieldAttributes.SpecialName) == FieldAttributes.SpecialName)
				builder.Append("special-name ");
		}
		
		private static void DoAppendMethodAttributes(StringBuilder builder, MethodAttributes attrs)
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
