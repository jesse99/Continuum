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

using Gear;
using Mono.Cecil;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace ObjectModel
{
	internal sealed class ShortForm
	{		
		public ShortForm(Boss boss, TextWriter writer)
		{
			m_boss = boss;
			m_writer = writer;
		}
		
		public void Write(string fullName)
		{
			TypeDefinition original = null;
			
			m_assembly = null;
			
			string name = fullName;
			while (name != null)
			{
				TypeDefinition type = DoProcessType(name, name == fullName);
				if (type == null)
					if (name != fullName)
						break;
					else
						throw new Exception(string.Format("Could not find {0} in the assembly", fullName));
						
				if (original == null)
					original = type;
				
				name = type.BaseType != null ? type.BaseType.FullName : null;
				if ("System.Object" == name)			// note that we don't want to do this in the loop condition in case we are displaying System.Object
					break;
			}
			
			DoWrite(original);
		}
		
		public static string GetModifiers(TypeDefinition type, TypeAttributes attrs)
		{
			string result = string.Empty;
			
			if ((uint) attrs == uint.MaxValue)
				return "? ";
			
			switch (attrs & TypeAttributes.VisibilityMask)
			{
				case TypeAttributes.NotPublic:
				case TypeAttributes.NestedAssembly:
					result += "internal ";
					break;
					
				case TypeAttributes.Public:
				case TypeAttributes.NestedPublic:
					result += "public ";
					break;
					
				case TypeAttributes.NestedPrivate:
					result += "private ";
					break;
					
				case TypeAttributes.NestedFamily:
					result += "protected ";
					break;
					
				case TypeAttributes.NestedFamANDAssem:
					result += "protected&internal ";
					break;
					
				case TypeAttributes.NestedFamORAssem:
					result += "protected internal ";
					break;
					
				default:
					Trace.Fail("bad visibility: " + (attrs & TypeAttributes.VisibilityMask));
					break;
			}
			
			if (type != null && !type.IsInterface)				// interfaces normally have 'abstract'
			{
				if ((attrs & TypeAttributes.Abstract) == TypeAttributes.Abstract)
					result += "abstract ";
				
				if ((attrs & TypeAttributes.Sealed) == TypeAttributes.Sealed)
					result += "sealed ";
			}
			
			switch (attrs & TypeAttributes.ClassSemanticMask)
			{
				case TypeAttributes.Class:
					result += "class ";
					break;
				
				case TypeAttributes.Interface:
					result += "interface ";
					break;
				
				default:
					Trace.Fail("bad semantics: " + (attrs & TypeAttributes.ClassSemanticMask));
					break;
			}
			
			return result;
		}
		
		#region Private Methods
		private TypeDefinition DoProcessType(string fullName, bool includeCtors)
		{
			TypeDefinition type = null;
			
			var objects = m_boss.Get<IObjectModel>();
			
			string[] paths = objects.FindTypeAssemblyPaths(fullName);
			foreach (string path in paths)
			{
				AssemblyDefinition assembly = AssemblyCache.Load(path, false);
				type = assembly.MainModule.Types[fullName];
				if (type != null)
				{
					if (m_assembly == null)
						m_assembly = path;
					DoGetMembers(type, includeCtors);
					break;
				}
			}
			
			return type;
		}
		
		public void DoWrite(TypeDefinition type)
		{
			string indent = string.Empty;
			
			m_writer.WriteLine("// {0}", Path.GetFileName(m_assembly));
			if (!string.IsNullOrEmpty(type.Namespace))
			{
				m_writer.WriteLine("namespace {0}", type.Namespace);
				m_writer.WriteLine("{");
				indent += "\t";
			}
			
			m_writer.Write(indent);
			DoWriteType(type);
			m_writer.Write(indent);
			m_writer.WriteLine("{");
			
			// constructors
			string indent2 = indent + "\t";
			if (m_ctors.Count > 0)
			{
				m_writer.WriteLine("{0}// constructors", indent2);
				
				m_ctors.Sort((lhs, rhs) =>
				{
					int result = rhs.Access.CompareTo(lhs.Access);
					if (result == 0)
						result = lhs.Text.Length.CompareTo(rhs.Text.Length);
					return result;
				});
				
				for (int i = 0; i < m_ctors.Count; ++i)
				{
					m_writer.WriteLine("{0}{1}", indent2, m_ctors[i].Text);
					
					if (i + 1 < m_ctors.Count || m_events.Count > 0 || m_properties.Count > 0 || m_staticMethods.Count > 0 || m_instanceMethods.Count > 0 || m_fields.Count > 0)
						m_writer.WriteLine(indent2);
				}
			}
			
			// events
			if (m_events.Count > 0)
			{
				m_writer.WriteLine("{0}// events", indent2);
				
				m_events.Sort((lhs, rhs) =>
				{
					int result = rhs.Access.CompareTo(lhs.Access);
					if (result == 0)
						result = lhs.Name.CompareTo(rhs.Name);
					return result;
				});
				
				for (int i = 0; i < m_events.Count; ++i)
				{
					m_writer.WriteLine("{0}{1}", indent2, m_events[i].Text);
					
					if (i + 1 < m_events.Count || m_properties.Count > 0 || m_staticMethods.Count > 0 || m_instanceMethods.Count > 0 || m_fields.Count > 0)
						m_writer.WriteLine(indent2);
				}
			}
			
			// properties
			if (m_properties.Count > 0)
			{
				m_writer.WriteLine("{0}// properties", indent2);
				
				m_properties.Sort((lhs, rhs) =>
				{
					int result = rhs.Access.CompareTo(lhs.Access);
					if (result == 0)
						result = lhs.Name.CompareTo(rhs.Name);
					return result;
				});
				
				for (int i = 0; i < m_properties.Count; ++i)
				{
					m_writer.WriteLine("{0}{1}", indent2, m_properties[i].Text);
					
					if (i + 1 < m_properties.Count || m_staticMethods.Count > 0 || m_instanceMethods.Count > 0 || m_fields.Count > 0)
						m_writer.WriteLine(indent2);
				}
			}
			
			// static methods
			if (m_staticMethods.Count > 0)
			{
				m_writer.WriteLine("{0}// static methods", indent2);
				
				m_staticMethods.Sort((lhs, rhs) =>
				{
					int result = rhs.Access.CompareTo(lhs.Access);
					if (result == 0)
						result = lhs.Name.CompareTo(rhs.Name);
					if (result == 0)
						result = lhs.Text.Length.CompareTo(rhs.Text.Length);
					return result;
				});
				
				for (int i = 0; i < m_staticMethods.Count; ++i)
				{
					m_writer.WriteLine("{0}{1}", indent2, m_staticMethods[i].Text);
					
					if (i + 1 < m_staticMethods.Count || m_instanceMethods.Count > 0 || m_fields.Count > 0)
						m_writer.WriteLine(indent2);
				}
			}
			
			// instance methods
			if (m_instanceMethods.Count > 0)
			{
				m_writer.WriteLine("{0}// methods", indent2);
				
				m_instanceMethods.Sort((lhs, rhs) =>
				{
					int result = rhs.Access.CompareTo(lhs.Access);
					if (result == 0)
						result = lhs.Name.CompareTo(rhs.Name);
					if (result == 0)
						result = lhs.Text.Length.CompareTo(rhs.Text.Length);
					return result;
				});
				
				for (int i = 0; i < m_instanceMethods.Count; ++i)
				{
					m_writer.WriteLine("{0}{1}", indent2, m_instanceMethods[i].Text);
					
					if (i + 1 < m_instanceMethods.Count || m_fields.Count > 0)
						m_writer.WriteLine(indent2);
				}
			}
			
			// fields
			if (m_fields.Count > 0)
			{
				m_writer.WriteLine("{0}// fields", indent2);
				
				m_fields.Sort((lhs, rhs) =>
				{
					int result = rhs.Access.CompareTo(lhs.Access);
					if (result == 0)
						result = lhs.Name.CompareTo(rhs.Name);
					return result;
				});
				
				for (int i = 0; i < m_fields.Count; ++i)
				{
					m_writer.WriteLine("{0}{1}", indent2, m_fields[i].Text);
					
					if (i + 1 < m_fields.Count)
						m_writer.WriteLine(indent2);
				}
			}
			
			m_writer.Write(indent);
			m_writer.WriteLine("}");
			if (!string.IsNullOrEmpty(type.Namespace))
			{
				m_writer.WriteLine("}");
			}
		}
		
#if false
		private void DoWriteAttributes(CustomAttributeCollection attrs)
		{
			foreach (CustomAttribute attrs in type.CustomAttributes)
			{
				m_writer.Write("[{0}(]", attr.Constructor.Name);
				for (int i = 0; i < attr.ConstructorParameters.Count; ++i)
				{
					m_writer.WriteLine("{0}", attr.ConstructorParameters[0]);
					if (i + 1 < attr.ConstructorParameters.Count)
						m_writer.WriteLine(", ");
				}
				m_writer.WriteLine(")]");
			}
		}
#endif
		
		private void DoWriteType(TypeDefinition type)
		{
			m_writer.Write("{0}", GetModifiers(type, type.Attributes));
			m_writer.Write(DoGetQualifiedTypeName(type, false));
			if (type.BaseType != null)
			{
				m_writer.Write(" : ");
				m_writer.Write(DoGetQualifiedTypeName(type.BaseType, false));
			}
			if (type.HasInterfaces)
			{
				if (type.BaseType != null)
					m_writer.Write(", ");
				else
					m_writer.Write(" : ");
				
				for (int i = 0; i < type.Interfaces.Count; ++i)
				{
					m_writer.Write(DoGetQualifiedTypeName(type.Interfaces[i], false));
					
					if (i + 1 < type.Interfaces.Count)
						m_writer.Write(", ");
				}
			}
			m_writer.WriteLine();
		}
		
		public void DoGetMembers(TypeDefinition type, bool includeCtors)
		{
			// constructors
			if (includeCtors)
			{
				foreach (MethodDefinition method in type.Constructors)
				{
					if (!method.IsPrivate)
						DoGetCtor(type, method);
				}
			}
			
			// events
			foreach (EventDefinition e in type.Events)
			{
				DoGetEvent(type, e);
			}
			
			// properties
			foreach (PropertyDefinition prop in type.Properties)
			{
				MethodDefinition getter = prop.GetMethod != null && !prop.GetMethod.IsPrivate ? prop.GetMethod : null;
				MethodDefinition setter = prop.SetMethod != null && !prop.SetMethod.IsPrivate ? prop.SetMethod : null;
				
				if (getter != null || setter != null)
				{
					DoGetProperty(type, prop, getter, setter);
				}
			}
			
			// methods
			foreach (MethodDefinition method in type.Methods)
			{
				if (!method.IsPrivate)
				{
					if (!method.IsGetter && !method.IsSetter && !method.IsAddOn && !method.IsRemoveOn && !method.IsFire)
					{
						DoGetMethod(type, method);
					}
				}
			}
			
			// fields
			foreach (FieldDefinition field in type.Fields)
			{
				if (!field.IsPrivate)
					DoGetField(field);
			}
		}
		
		private string DoGetTypeName(TypeReference type, bool useAlias)
		{
			string name = useAlias ? DoGetShortName(type) : type.Name;
			
			int i = name.IndexOf('`');
			if (i >= 0)
				name = name.Substring(0, i);
				
			return name;
		}
		
		private string DoGetQualifiedTypeName(TypeReference type, bool useAlias)
		{
			var builder = new StringBuilder();
			
			builder.Append(DoGetTypeName(type, useAlias));
			
			if (type.HasGenericParameters)
			{
				DoGetGenericParams(builder, type.GenericParameters);
			}
			else
			{
				var gt = type as GenericInstanceType;
				if (gt != null && gt.HasGenericArguments)
					DoGetGenericArgs(builder, gt.GenericArguments);
			}
			
			return builder.ToString();
		}
		
		private void DoGetCtor(TypeDefinition type, MethodDefinition method)
		{
			var builder = new StringBuilder();
			
			DoGetMethodModifiers(type, builder, method.Attributes);
			string name = DoGetTypeName(type, false);
			builder.Append(name);
			if (method.HasGenericParameters)
				DoGetGenericParams(builder, method.GenericParameters);
			DoGetParams(builder, method.Parameters);
			
			DoAdd(m_ctors, new Member(name, method.Attributes & MethodAttributes.MemberAccessMask, builder.ToString()));
		}
		
		private void DoGetEvent(TypeDefinition type, EventDefinition e)
		{
			var builder = new StringBuilder();
			
			MethodDefinition m = e.AddMethod ?? e.InvokeMethod ?? e.RemoveMethod;
			
			DoGetMethodModifiers(type, builder, m.Attributes);
			builder.Append("event ");
			builder.Append(DoGetQualifiedTypeName(e.EventType, true));
			builder.AppendFormat(" {0};", e.Name);
			
			DoAdd(m_events, new Member(e.Name, m.Attributes & MethodAttributes.MemberAccessMask, builder.ToString()));
		}
		
		private void DoGetProperty(TypeDefinition type, PropertyDefinition prop, MethodDefinition getter, MethodDefinition setter)
		{
			var builder = new StringBuilder();
			
			MethodDefinition m = getter ?? setter;
			
			DoGetMethodModifiers(type, builder, m.Attributes);
			builder.Append(DoGetQualifiedTypeName(prop.PropertyType, true));
			if (prop.HasParameters)				// TODO: might want to include the prop.Name as IndexerNameAttribute for languages that don't support the array indexer notation
			{
				builder.Append(" this[");
				builder.Append(DoGetQualifiedTypeName(m.Parameters[0].ParameterType, true));
				builder.Append(" ");
				builder.Append(m.Parameters[0].Name);
				builder.AppendFormat("] {0}", "{");					// need this weird code or string.Format freaks out
			}
			else
				builder.AppendFormat(" {0} {1}", prop.Name, "{");
			if (getter != null)
			{
				builder.Append("get;");
				if (setter != null)
					builder.Append(" ");
			}
			if (setter != null)
			{
				if ((m.Attributes & MethodAttributes.MemberAccessMask) != (setter.Attributes & MethodAttributes.MemberAccessMask))
					DoGetMethodModifiers(type, builder, setter.Attributes & MethodAttributes.MemberAccessMask);
				builder.Append("set;");
			}
			builder.Append("}");
			
			DoAdd(m_properties, new Member(prop.Name, m.Attributes & MethodAttributes.MemberAccessMask, builder.ToString(), m));
		}
		
		private void DoGetMethod(TypeDefinition type, MethodDefinition method)
		{
			var builder = new StringBuilder();
			
			DoGetMethodModifiers(type, builder, method.Attributes);
			if (method.Name != "op_Implicit" && method.Name != "op_Explicit")
				builder.Append(DoGetQualifiedTypeName(method.ReturnType.ReturnType, true));
			string name = DoGetMethodName(method);
			builder.AppendFormat(" {0}", name);
			if (method.HasGenericParameters)
				DoGetGenericParams(builder, method.GenericParameters);
			DoGetParams(builder, method.Parameters);
			
			if (method.IsStatic)
				DoAdd(m_staticMethods, new Member(name, method.Attributes & MethodAttributes.MemberAccessMask, builder.ToString(), method));
			else
				DoAdd(m_instanceMethods, new Member(name, method.Attributes & MethodAttributes.MemberAccessMask, builder.ToString(), method));
		}
		
		private void DoGetField(FieldDefinition field)
		{
			var builder = new StringBuilder();
			
			DoGetFieldModifiers(builder, field.Attributes);
			builder.Append(DoGetQualifiedTypeName(field.FieldType, true));
			builder.AppendFormat(" {0};", field.Name);
			
			DoAdd(m_fields, new Member(field.Name, (MethodAttributes) (ushort) (field.Attributes & FieldAttributes.FieldAccessMask), builder.ToString()));
		}
		
		private string DoGetMethodName(MethodDefinition method)
		{
			switch (method.Name)
			{
				case "op_Decrement":
					return "operator--";
				
				case "op_Increment":
					return "operator++";
				
				case "op_UnaryNegation":
					return "operator-";
				
				case "op_UnaryPlus":
					return "operator+";
				
				case "op_LogicalNot":
					return "operator!";
				
				case "op_AddressOf":
					return "operator&";
				
				case "op_OnesComplement":
					return "operator~";
								
				case "op_PointerDereference":
					return "operator*";
								
				case "op_Addition":
					return "operator+";
				
				case "op_Subtraction":
					return "operator-";
				
				case "op_Multiply":
					return "operator*";
				
				case "op_Division":
					return "operator/";
				
				case "op_Modulus":
					return "operator%";
				
				case "op_ExclusiveOr":
					return "operator^";
				
				case "op_BitwiseAnd":
					return "operator&";
				
				case "op_BitwiseOr":
					return "operator|";
				
				case "op_LogicalAnd":
					return "operator&&";
				
				case "op_LogicalOr":
					return "operator||";
				
				case "op_Assign":
					return "operator=";
				
				case "op_LeftShift":
					return "operator<<";
				
				case "op_RightShift":
					return "operator>>";
				
				case "op_Equality":
					return "operator==";
				
				case "op_Inequality":
					return "operator!=";
				
				case "op_GreaterThan":
					return "operator>";
				
				case "op_GreaterThanOrEqual":
					return "operator>=";
				
				case "op_LessThan":
					return "operator<";
				
				case "op_LessThanOrEqual":
					return "operator<=";
								
				case "op_MultiplicationAssignment":
					return "operator*=";
				
				case "op_SubtractionAssignment":
					return "operator-=";
				
				case "op_ExclusiveOrAssignment":
					return "operator^=";
				
				case "op_LeftShiftAssignment":
					return "operator<<=";
				
				case "op_RightShiftAssignment":
					return "operator>>=";
				
				case "op_ModulusAssignment":
					return "operator%=";
				
				case "op_AdditionAssignment":
					return "operator+=";
				
				case "op_BitwiseAndAssignment":
					return "operator&=";
				
				case "op_BitwiseOrAssignment":
					return "operator=";
				
				case "op_DivisionAssignment":
					return "operator/=";
				
				case "op_Implicit":
					return "implicit operator " + DoGetQualifiedTypeName(method.ReturnType.ReturnType, true);
				
				case "op_Explicit":
					return "explicit operator " + DoGetQualifiedTypeName(method.ReturnType.ReturnType, true);
				
				default:
					return method.Name;
			}
		}
		
		private void DoGetParams(StringBuilder builder, ParameterDefinitionCollection pc)
		{
			builder.Append("(");
			for (int i = 0; i < pc.Count; ++i)
			{
				ParameterDefinition param = pc[i];
				
				if (param.HasCustomAttributes)
				{
					foreach (CustomAttribute a in param.CustomAttributes)
					{
						if (a.Constructor.DeclaringType.Name == "ParamArrayAttribute")
						{
							builder.Append("params ");
							break;
						}
					}
				}
				
				string tname = DoGetQualifiedTypeName(param.ParameterType, true);
				if (tname.EndsWith("&"))
				{
					if (param.IsOut)
						builder.Append("out ");
					else
						builder.Append("ref ");
					
					tname = tname.Remove(tname.Length - 1);
				}
				
				builder.Append(tname);
				builder.Append(" ");
				builder.Append(param.Name);
				
				if (i + 1 < pc.Count)
					builder.Append(", ");
			}
			builder.Append(");");
		}
		
		private void DoGetGenericParams(StringBuilder builder, GenericParameterCollection pc)
		{
			builder.Append("<");
			for (int i = 0; i < pc.Count; ++i)
			{
				builder.Append(pc[i].Name);
				
				if (i + 1 < pc.Count)
					builder.Append(", ");
			}
			builder.Append(">");
		}
		
		private void DoGetGenericArgs(StringBuilder builder, GenericArgumentCollection gc)
		{
			builder.Append("<");
			for (int i = 0; i < gc.Count; ++i)
			{
				builder.Append(gc[i].Name);
				
				if (i + 1 < gc.Count)
					builder.Append(", ");
			}
			builder.Append(">");
		}
		
		private string DoGetShortName(TypeReference type)
		{
			string name = CsHelpers.GetAliasedName(type.FullName);
			if (name == type.FullName)
				name = type.Name;
			
			return name;
		}
		
		private void DoGetMethodModifiers(TypeDefinition type, StringBuilder builder, MethodAttributes attrs)
		{
			if (!type.IsInterface)		// note that interface methods are normally decorated with 'public new abstract'
			{
				switch (attrs & MethodAttributes.MemberAccessMask)
				{
					case MethodAttributes.Compilercontrolled:
						builder.Append("compiler ");
						break;
					
					case MethodAttributes.Private:
						builder.Append("private ");
						break;
					
					case MethodAttributes.FamANDAssem:
						builder.Append("protected&internal ");
						break;
					
					case MethodAttributes.Assem:
						builder.Append("internal ");
						break;
					
					case MethodAttributes.Family:
						builder.Append("protected ");
						break;
					
					case MethodAttributes.FamORAssem:
						builder.Append("protected internal ");
						break;
					
					case MethodAttributes.Public:
						builder.Append("public ");
						break;
					
					default:
						Trace.Fail("bad access: " + (attrs & MethodAttributes.MemberAccessMask));
						break;
				}
				
				if ((attrs & MethodAttributes.VtableLayoutMask) == MethodAttributes.NewSlot)
					builder.Append("new ");
				
				if ((attrs & MethodAttributes.Static) == MethodAttributes.Static)
					builder.Append("static ");
				
				if ((attrs & MethodAttributes.Abstract) == MethodAttributes.Abstract)
					builder.Append("abstract ");
				else if ((attrs & MethodAttributes.Virtual) == MethodAttributes.Virtual)
					if ((attrs & MethodAttributes.VtableLayoutMask) == MethodAttributes.ReuseSlot)
						builder.Append("override ");
					else if ((attrs & MethodAttributes.Final) == 0)
						builder.Append("virtual ");
						
				if ((attrs & MethodAttributes.Final) == MethodAttributes.Final)
					builder.Append("sealed ");
			}
		}
		
		private void DoGetFieldModifiers(StringBuilder builder, FieldAttributes attrs)
		{
			switch (attrs & FieldAttributes.FieldAccessMask)
			{
				case FieldAttributes.Compilercontrolled:
					builder.Append("compiler ");
					break;
					
				case FieldAttributes.Private:
					builder.Append("private ");
					break;
				
				case FieldAttributes.FamANDAssem:
					builder.Append("protected&internal ");
					break;
				
				case FieldAttributes.Assembly:
					builder.Append("internal ");
					break;
				
				case FieldAttributes.Family:
					builder.Append("protected ");
					break;
				
				case FieldAttributes.FamORAssem:
					builder.Append("protected internal ");
					break;
				
				case FieldAttributes.Public:
					builder.Append("public ");
					break;
				
				default:
					Trace.Fail("bad access: " + (attrs & FieldAttributes.FieldAccessMask));
					break;
			}
			
			if ((attrs & FieldAttributes.Static) == FieldAttributes.Static)
				builder.Append("static ");
			
			if ((attrs & FieldAttributes.InitOnly) == FieldAttributes.InitOnly)
				builder.Append("readonly ");
			
			if ((attrs & FieldAttributes.Literal) == FieldAttributes.Literal)
				builder.Append("const ");
		}
		
		// Don't add members that match members that have already been added
		// (this can be a little tricky because derived methods may use the new or
		// sealed keywords).
		private void DoAdd(List<Member> list, Member member)
		{
			if (!list.Any(m => m.Key == member.Key)) 
				list.Add(member);
		}
		#endregion
		
		#region Private Types
		private sealed class Member
		{
			public Member(string name, MethodAttributes access, string text)
			{
				Name = name;
				Access = access;
				Text = text;
				Key = text;
			}
			
			public Member(string name, MethodAttributes access, string text, MethodDefinition key)
			{
				Name = name;
				Access = access;
				Text = text;
				Key = key.Name;
				
				for (int i = 0; i < key.Parameters.Count; ++i)
				{
					Key += key.Parameters[i].ParameterType.FullName;
				}
			}
			
			// E.g. "FullPath".
			public string Name {get; private set;}
			
			public MethodAttributes Access {get; private set;}
			
			// E.g. "internal  string FullPath {get;}".
			public string Text {get; private set;}
			
			public string Key {get; private set;}
		}
		#endregion
		
		#region Fields
		private Boss m_boss;
		private TextWriter m_writer;
		private string m_assembly;
			
		private List<Member> m_ctors = new List<Member>();
		private List<Member> m_events = new List<Member>();
		private List<Member> m_properties = new List<Member>();
		private List<Member> m_staticMethods = new List<Member>();
		private List<Member> m_instanceMethods = new List<Member>();
		private List<Member> m_fields = new List<Member>();
		#endregion
	}
}	
