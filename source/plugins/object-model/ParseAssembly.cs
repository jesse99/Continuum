// Copyright (C) 2007-2011 Jesse Jones
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
using Gear.Helpers;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace ObjectModel
{
	internal sealed class ParseAssembly : IParseAssembly, IOpened
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public void Opened()
		{
			m_path = Populate.GetDatabasePath(m_boss);
		}
		
		// Parsing the larger system assemblies takes 10s of seconds which is much 
		// too long a period of time to keep the database locked so our transaction
		// is around the types, not the assembly.
		[ThreadModel(ThreadModel.SingleThread)]
		public void Parse(string path, AssemblyDefinition assembly, string id, bool fullParse)
		{
			if (m_database == null)
				m_database = new Database(m_path, "ParseAssembly-" + Path.GetFileNameWithoutExtension(m_path));
			
//	Console.WriteLine("    parsing {0} for thread {1}", assembly.Name.FullName, System.Threading.Thread.CurrentThread.ManagedThreadId);
			var namespaces = new HashSet<string>();
			foreach (ModuleDefinition module in assembly.Modules)
			{
				foreach (TypeDefinition type in module.Types)
				{
					if (fullParse || type.IsPublic)
					{
						if (!namespaces.Contains(type.Namespace))
							namespaces.Add(type.Namespace);
							
						DoParseType(type, id, fullParse);			// max time here is 0.8 secs on a fast machine for type System.Xml.Serialization.XmlSerializationReader (with the old schema)
//						System.Threading.Thread.Sleep(50);	// this doesn't seem to help the main thread too much
					}
				}
			}
			
			DoAddNamespaces(namespaces, id);
		}
		
		// System.Collections.Generic
		#region Private Methods
		[ThreadModel(ThreadModel.SingleThread)]
		private void DoParseTypes(TypeDefinition type, string id, bool fullParse)
		{
			DoParseType(type, id, fullParse);			// max time here is 0.8 secs on a fast machine for type System.Xml.Serialization.XmlSerializationReader (with the old schema)
			
			foreach (TypeDefinition nested in type.NestedTypes)
			{
				if (fullParse || nested.IsNestedPublic || nested.IsNestedFamily || nested.IsNestedFamilyOrAssembly)
					DoParseTypes(nested, id, fullParse);
			}
		}
		
		[ThreadModel(ThreadModel.SingleThread)]
		private void DoAddNamespaces(HashSet<string> namespaces, string id)
		{
			var table = new Dictionary<string, string>();
			
			foreach (string ns in namespaces)
			{
				string[] parts = ns.Split('.');
				
				string parent = parts[0];
				for (int i = 1; i < parts.Length; ++i)
				{
					string children;
					if (!table.TryGetValue(parent, out children))
						children = string.Empty;
						
					if (children.Length == 0)
						children = string.Join(".", parts, i, parts.Length - i);
					else
						children += ";" + string.Join(".", parts, i, parts.Length - i);
						
					table[parent] = children;
					parent += "." + parts[i];
				}
			}
			
			m_database.Update("update namespaces", () =>
			{
				foreach (KeyValuePair<string, string> entry in table)
				{
					m_database.InsertOrReplace("Namespaces",
						entry.Key,
						id,
						entry.Value);
				}
			});
		}
		
		[Conditional("DEBUG")]
		[ThreadModel(ThreadModel.Concurrent)]
		private static void DoValidateRoot(string label, string type)
		{
			string mesg = null;
			
			if (type == null)
				mesg = string.Format("{0} is an null", label);
			
			else if (type.Contains("["))
				mesg = string.Format("{0} is an array ({1})", label, type);
				
			else if (type.Contains("<"))
				mesg = string.Format("{0} has a generic argument ({1})", label, type);
				
			else if (type.Contains("*("))
				mesg = string.Format("{0} is a function pointer ({1})", label, type);
				
			else if (type.Contains("*"))
				mesg = string.Format("{0} is a pointer ({1})", label, type);
				
			else if (type.Contains("&"))
				mesg = string.Format("{0} is a reference ({1})", label, type);
			
			if (mesg != null)
				Contract.Assert(false, mesg);
		}
		
		[Conditional("DEBUG")]
		[ThreadModel(ThreadModel.SingleThread)]
		private void DoValidateRoot(string label, TypeReference type)
		{
			string mesg = null;
			
			if (type != null)
			{
				if (type is TypeSpecification)
					mesg = string.Format("{0} is a {1} ({2})", label, type.GetType(), type.FullName);
				
				else
					// Probably not neccesary to do these checks but it shouldn't hurt.
					DoValidateRoot(label, type.FullName);
			}
			
			if (mesg != null)
				Contract.Assert(false, mesg);
		}
		
		[ThreadModel(ThreadModel.SingleThread)]
		private void DoParseType(TypeDefinition type, string id, bool fullParse)
		{
			if (!DoIsGeneratedCode(type))
			{
				int visibility = 0;
				switch (type.Attributes & TypeAttributes.VisibilityMask)
				{
					case TypeAttributes.Public:
					case TypeAttributes.NestedPublic:
						visibility = 0;
						break;
					
					case TypeAttributes.NestedFamily:
					case TypeAttributes.NestedFamORAssem:
						visibility = 1;
						break;
					
					case TypeAttributes.NotPublic:
					case TypeAttributes.NestedAssembly:
					case TypeAttributes.NestedFamANDAssem:
						visibility = 2;
						break;
					
					case TypeAttributes.NestedPrivate:
						visibility = 3;
						break;
					
					default:
						Contract.Assert(false, "bad visibility: " + (type.Attributes & TypeAttributes.VisibilityMask));
						break;
				}
				
				var gnames = new StringBuilder();
				for (int i = 0; i < type.GenericParameters.Count; ++i)
				{
					gnames.Append(type.GenericParameters[i].Name);
					
					if (i + 1 < type.GenericParameters.Count)
						gnames.Append(';');
				}
				
				uint attributes = 0;
				if (type.IsAbstract)
					attributes |= 0x01;
				if (type.IsSealed)
					attributes |= 0x02;
				if (type.IsInterface)
					attributes |= 0x04;
				if (type.IsValueType)
					attributes |= 0x08;
				if (type.IsEnum)
					attributes |= 0x10;
				if (type.DeclaringType != null)
					attributes |= 0x20;
				if (type.BaseType != null && (type.BaseType.FullName == "System.Delegate" || type.BaseType.FullName == "System.MulticastDelegate"))
					attributes |= 0x40;
				
				m_database.Update("parse " + type.FullName, () =>
				{
					var interfaces = new StringBuilder();
					if (type.HasInterfaces)
					{
						for (int i = 0; i < type.Interfaces.Count; ++i)
						{
							interfaces.Append(DoGetRootName(type.Interfaces[i]));
							interfaces.Append(':');
						}
					}
					
					DoValidateRoot("root_name", type);
					DoValidateRoot("declaring_root_name", type.DeclaringType);
					
					string baseName = DoGetRootName(type.BaseType);
					m_database.InsertOrReplace("Types",
						type.FullName,
						id,
						!string.IsNullOrEmpty(type.Namespace) ? type.Namespace : string.Empty,
						DoGetNameWithoutTick(type.Name.GetTypeName()),
						baseName,
						interfaces.ToString(),
						type.GenericParameters.Count.ToString(),
						gnames.ToString(),
						visibility.ToString(),
						attributes.ToString());
						
					if (type.HasMethods)
						foreach (MethodDefinition method in type.Methods)
							DoParseMethod(type, method, id, fullParse);
					
					if (type.HasFields)
						foreach (FieldDefinition field in type.Fields)
							DoParseField(type, field, id, fullParse);
				});
			}
		}
		
		[ThreadModel(ThreadModel.SingleThread)]
		private void DoAddSpecialType(TypeReference type)
		{
			TypeSpecification spec = type as TypeSpecification;
			if (spec != null)
			{
				ArrayType array = type as ArrayType;
				GenericInstanceType generic = type as GenericInstanceType;
				
				var genericTypes = new StringBuilder();
				if (generic != null && generic.HasGenericArguments)
				{
					for (int i = 0; i < generic.GenericArguments.Count; ++i)
					{
						genericTypes.Append(generic.GenericArguments[i].FullName);
						genericTypes.Append(':');
					}
				}
				
				int kind;
				string kn;
				if (array != null)
				{
					kind = 0;
					kn = "array-type";
				}
				else if (generic != null)
				{
					kind = 1;
					kn = DoGetRootName(type);
				}
				else if (type is PointerType)
				{
					kind = 2;
					kn = "pointer-type";
				}
				else
				{
					kind = 3;
					kn = "other-type";
				}
				
				m_database.InsertOrIgnore("SpecialTypes",
					type.FullName,
					spec.ElementType != null ? spec.ElementType.FullName : string.Empty,
					array != null ? array.Rank.ToString() : "0",
					genericTypes.ToString(),
					kind.ToString(),
					kn);
			}
		}
		
		[ThreadModel(ThreadModel.SingleThread)]
		private void DoParseMethod(TypeDefinition type, MethodDefinition method, string id, bool fullParse)
		{
			// Note that auto-prop methods count as generated code.
			if (method.IsGetter || method.IsSetter || !DoIsGeneratedCode(method))
			{
				if (fullParse || method.IsFamily || method.IsFamilyOrAssembly || method.IsPublic)
				{
					var location = DoGetSourceAndLine(method);
				
					string extendName = string.Empty;
					if (method.IsPublic && method.IsExtension())
					{
						if (method.Parameters[0].ParameterType.IsSpecial())
						{
							DoAddSpecialType(method.Parameters[0].ParameterType);
							extendName = method.Parameters[0].ParameterType.FullName;
						}
						else
							extendName = DoGetRootName(method.Parameters[0].ParameterType);
					}
					
					int access = 0;
					switch (method.Attributes & MethodAttributes.MemberAccessMask)
					{
						case MethodAttributes.Public:
							access = 0;
							break;
							
						case MethodAttributes.Family:
						case MethodAttributes.FamORAssem:
							access = 1;
							break;
							
						case MethodAttributes.Assembly:
						case MethodAttributes.FamANDAssem:
							access = 2;
							break;
							
						case MethodAttributes.Private:
						case MethodAttributes.CompilerControlled:
							access = 3;
							break;
							
						default:
							Contract.Assert(false, "bad access: " + (method.Attributes & MethodAttributes.MemberAccessMask));
							break;
					}
					
					int kind = 0;
					if (method.IsGetter)
						if (method.Name == "get_Item")
							kind = 3;
						else
							kind = 1;
					
					else if (method.IsSetter)
						if (method.Name == "set_Item")
							kind = 4;
						else
							kind = 2;
							
					else if (method.IsAddOn || method.IsRemoveOn || method.IsFire)
						kind = 5;
						
					else if (method.IsConstructor)
						kind = 6;
						
					else if (method.Name.StartsWith("op_"))
						kind = 7;
						
					else if (extendName.Length > 0)
						kind = 8;
						
					else if (method.Name == "Finalize")
						kind = 9;
					
					DoValidateRoot("root_name", method.DeclaringType);
					
					string returnName;
					if (method.ReturnType.IsSpecial())
					{
						DoAddSpecialType(method.ReturnType);
						returnName = method.ReturnType.FullName;
					}
					else
						returnName = DoGetRootName(method.ReturnType);
					
					m_database.InsertOrReplace("Methods",
						DoGetDisplayText(method),
						method.Name,
						returnName,
						type.FullName,
						method.Parameters.Count.ToString(),
						method.GenericParameters.Count.ToString(),
						id,
						extendName,
						access.ToString(),
						method.IsStatic ? "1" : "0",
						location.Item1,
						location.Item2.ToString(),
						kind.ToString());
				}
			}
		}
		
		[ThreadModel(ThreadModel.SingleThread)]
		private void DoParseField(TypeDefinition type, FieldDefinition field, string id, bool fullParse)
		{
			if (!DoIsGeneratedCode(field))
			{
				if (fullParse || field.IsFamily || field.IsFamilyOrAssembly || field.IsPublic)
				{
					int access = 0;
					switch (field.Attributes & FieldAttributes.FieldAccessMask)
					{
						case FieldAttributes.Public:
							access = 0;
							break;
							
						case FieldAttributes.Family:
						case FieldAttributes.FamORAssem:
							access = 1;
							break;
							
						case FieldAttributes.Assembly:
						case FieldAttributes.FamANDAssem:
							access = 2;
							break;
							
						case FieldAttributes.Private:
							access = 3;
							break;
							
						default:
							Contract.Assert(false, "bad access: " + (field.Attributes & FieldAttributes.FieldAccessMask));
							break;
					}
					
					DoValidateRoot("root_name", field.DeclaringType);
					
					string fieldType;
					if (field.FieldType.IsSpecial())
					{
						DoAddSpecialType(field.FieldType);
						fieldType = field.FieldType.FullName;
					}
					else
						fieldType = DoGetRootName(field.FieldType);
					
					m_database.InsertOrReplace("Fields",
						field.Name,
						type.FullName,
						fieldType,
						id,
						access.ToString(),
						field.IsStatic ? "1" : "0");
				}
			}
		}
		
		[ThreadModel(ThreadModel.SingleThread)]
		private string DoGetDisplayText(MethodDefinition method)
		{
			var text = new StringBuilder();
			
			// return-type
			text.Append(DoGetDisplayType(method.ReturnType.FullName));
			text.Append(':');
			
			// declaring-type
			string name = method.DeclaringType.FullName;
			Debug.Assert(!name.Contains(":"), name + " should not have a ':'");
			text.Append(name);
			text.Append(':');
			
			// name
			text.Append(DoGetDisplayName(method));
			text.Append(':');
			
			// generic-args
			if (method.HasGenericParameters && !DoCanDeduceGenerics(method))
				text.Append(DoGetDisplayGargs(method.GenericParameters));
			else if (method.IsConstructor && method.DeclaringType.HasGenericParameters)
				text.Append(DoGetDisplayGargs(method.DeclaringType.GenericParameters));
			text.Append(':');
			
			// arg-types
			if (method.HasParameters)
			{
				for (int i = 0; i < method.Parameters.Count; ++i)
				{
					text.Append(method.GetParameterModifier(i));
					
					ParameterDefinition p = method.Parameters[i];
					string typeName = p.ParameterType.FullName;
					if (typeName.EndsWith("&"))
						typeName = typeName.Remove(typeName.Length - 1);
					text.Append(DoGetDisplayType(typeName));
					
					if (i + 1 < method.Parameters.Count)
						text.Append(";");
				}
			}
			text.Append(':');
			
			// arg-names
			if (method.HasParameters)
			{
				for (int i = 0; i < method.Parameters.Count; ++i)
				{
					ParameterDefinition p = method.Parameters[i];
					
					Debug.Assert(!p.Name.Contains(":"), p.Name + " should not have a ':'");
					Debug.Assert(!p.Name.Contains(";"), p.Name + " should not have a ';'");
					text.Append(p.Name);
					
					if (i + 1 < method.Parameters.Count)
						text.Append(";");
				}
			}
			
			return text.ToString();
		}
		
		[ThreadModel(ThreadModel.SingleThread)]
		private string DoGetDisplayType(string name)
		{
			name = CsHelpers.TrimGeneric(name);
			name = CsHelpers.GetAliasedName(name);
			name = CsHelpers.TrimNamespace(name);
			
			Debug.Assert(!name.Contains(":"), name + " should not have a ':'");
			Debug.Assert(!name.Contains(";"), name + " should not have a ';'");
			
			return name;
		}
		
		[ThreadModel(ThreadModel.SingleThread)]
		private string DoGetDisplayGargs(Collection<GenericParameter> parms)
		{
			var builder = new StringBuilder();
			
			for (int i = 0; i < parms.Count; ++i)
			{
				GenericParameter p = parms[i];
				
				Debug.Assert(!p.Name.Contains(";"), p.Name + " should not have a ';'");
				builder.Append(p.Name);
				
				if (i + 1 < parms.Count)
					builder.Append(";");
			}
			
			string result = builder.ToString();
			Debug.Assert(!result.Contains(":"), result + " should not have a ':'");
			
			return result;
		}
		
		[ThreadModel(ThreadModel.SingleThread)]
		private string DoGetDisplayName(MethodDefinition method)
		{
			string name;
			
			if (method.IsGetter || method.IsSetter)
				name = method.Name.Substring(4);
			
			else if (method.IsConstructor)
				name = DoGetNameWithoutTick(method.DeclaringType.Name);
			
			else if (method.Name == "Finalize")
				name = "~" + method.DeclaringType.Name;
			
			else if (method.Name.StartsWith("op_"))
				name = DoGetOperatorName(method);
			
			else
				name = method.Name;
			
			Debug.Assert(!name.Contains(":"), name + " should not have a ':'");
			
			return name;
		}
		
		[ThreadModel(ThreadModel.SingleThread)]
		private void DoRemoveGenerics(List<GenericParameter> generics, TypeReference type)
		{
			var gp = type as GenericParameter;
			if (gp != null)
				generics.Remove(gp);
			
			var gi = type as GenericInstanceType;
			if (gi != null)
			{
				foreach (TypeReference a in gi.GenericArguments)
				{
					DoRemoveGenerics(generics, a);
				}
			}
			
			var fp = type as FunctionPointerType;
			if (fp != null)
				DoRemoveGenerics(generics, fp.ReturnType);
			
			var ts = type as TypeSpecification;
			if (ts != null)
				DoRemoveGenerics(generics, ts.ElementType);
		}
		
		[ThreadModel(ThreadModel.SingleThread)]
		private bool DoCanDeduceGenerics(MethodDefinition method)
		{
			bool can = true;
			
			if (method.HasGenericParameters)
			{
				var generics = new List<GenericParameter>();
				foreach (GenericParameter g in method.GenericParameters)		// can't use the range ctor because Cecil is antique
					generics.Add(g);
				
				if (method.HasParameters)
				{
					for (int i = 0; i < method.Parameters.Count; ++i)
					{
						DoRemoveGenerics(generics, method.Parameters[i].ParameterType);
					}
				}
				
				can = generics.Count == 0;
			}
			
			return can;
		}
		
		[ThreadModel(ThreadModel.SingleThread)]
		private string DoGetOperatorName(MethodDefinition method)
		{
			string name = DoGetNetOperatorName(method);
			
			if (name == null)
				name = DoGetFSharpOperatorName(method);
				
			if (name == null)
			{
				Log.WriteLine(TraceLevel.Error, "ObjectModel", "bad operator: {0}", method.Name);
				name = method.Name;
			}
			
			return name;
		}
		
		[ThreadModel(ThreadModel.SingleThread)]
		private string DoGetNetOperatorName(MethodDefinition method)
		{
			string name = null;
			
			switch (method.Name)
			{
				// conversion operators
				case "op_Implicit":
					name = "implicit operator " + method.DeclaringType.Name;
					break;
				
				case "op_Explicit":
					name = "explicit operator " + method.DeclaringType.Name;
					break;
				
				// unary operators
				case "op_Decrement":
					name = "operator --";
					break;
					
				case "op_Increment":
					name = "operator ++";
					break;
					
				case "op_Negation":
					name = "operator !";
					break;
					
				case "op_UnaryNegation":
					name = "operator -";
					break;
					
				case "op_UnaryPlus":
					name = "operator +";
					break;
				
				// binary operators
				case "op_Addition":
					name = "operator +";
					break;
					
				case "op_Assign":
					name = "operator =";
					break;
					
				case "op_BitwiseAnd":
					name = "operator &";
					break;
					
				case "op_BitwiseOr":
					name = "operator |";
					break;
					
				case "op_Division":
					name = "operator /";
					break;
					
				case "op_Equality":
					name = "operator ==";
					break;
					
				case "op_ExclusiveOr":
					name = "operator ^";
					break;
					
				case "op_False":
					name = "operator false";
					break;
					
				case "op_GreaterThan":
					name = "operator >";
					break;
					
				case "op_GreaterThanOrEqual":
					name = "operator >=";
					break;
					
				case "op_Inequality":
					name = "operator !=";
					break;
					
				case "op_LeftShift":
					name = "operator <<";
					break;
					
				case "op_LessThan":
					name = "operator <";
					break;
					
				case "op_LessThanOrEqual":
					name = "operator op_LessThanOrEqual<=";
					break;
					
				case "op_LogicalAnd":
					name = "operator &&";
					break;
					
				case "op_LogicalNot":
					name = "operator !";
					break;
					
				case "op_LogicalOr":
					name = "operator ||";
					break;
					
				case "op_Modulus":
					name = "operator %";
					break;
					
				case "op_Multiply":
					name = "operator *";
					break;
					
				case "op_OnesComplement":
					name = "operator ~";
					break;
				
				case "op_RightShift":
					name = "operator >>";
					break;
					
				case "op_Subtraction":
					name = "operator -";
					break;
					
				case "op_True":
					name = "operator true";
					break;
			}
			
			return name;
		}
		
		[ThreadModel(ThreadModel.SingleThread)]
		private string DoGetFSharpOperatorName(MethodDefinition method)
		{
			string name = null;
			
			switch (method.Name)
			{
				case "op_Nil":
					name = "operator []";
					break;
					
				case "op_ColonColon":
					name = "operator ::";
					break;
					
				case "op_Addition":
					name = "operator +";
					break;
					
				case "op_Subtraction":
					name = "operator -";
					break;
					
				case "op_Multiply":
					name = "operator *";
					break;
					
				case "op_Division":
					name = "operator /";
					break;
					
				case "op_Exponentiation":
					name = "operator **";
					break;
					
				case "op_Append":
					name = "operator @";
					break;
					
				case "op_Concatenate":
					name = "operator ^";
					break;
					
				case "op_Modulus":
					name = "operator %";
					break;
					
				case "op_BitwiseAnd":
					name = "operator &&&";
					break;
					
				case "op_BitwiseOr":
					name = "operator |||";
					break;
					
				case "op_ExclusiveOr":
					name = "operator ^^^";
					break;
					
				case "op_LeftShift":
					name = "operator <<<";
					break;
					
				case "op_LogicalNot":
					name = "operator ~~~";
					break;
					
				case "op_RightShift":
					name = "operator >>>";
					break;
					
				case "op_UnaryPlus":
					name = "operator ~+";
					break;
					
				case "op_UnaryNegation":
					name = "operator ~-";
					break;
					
				case "op_Equality":
					name = "operator =";
					break;
					
				case "op_Inequality":
					name = "operator <>";
					break;
					
				case "op_LessThanOrEqual":
					name = "operator <=";
					break;
					
				case "op_GreaterThanOrEqual":
					name = "operator >=";
					break;
					
				case "op_LessThan":
					name = "operator <";
					break;
					
				case "op_GreaterThan":
					name = "operator >";
					break;
					
				case "op_Dynamic":
					name = "operator ?";
					break;
					
				case "op_DynamicAssignment":
					name = "operator ?<-";
					break;
					
				case "op_PipeRight":
					name = "operator |>";
					break;
					
				case "op_PipeRight2":
					name = "operator ||>";
					break;
					
				case "op_PipeRight3":
					name = "operator |||>";
					break;
					
				case "op_PipeLeft":
					name = "operator <|";
					break;
					
				case "op_PipeLeft2":
					name = "operator <||";
					break;
					
				case "op_PipeLeft3":
					name = "operator <|||";
					break;
					
				case "op_Dereference":
					name = "operator !";
					break;
					
				case "op_ComposeRight":
					name = "operator >>";
					break;
					
				case "op_ComposeLeft":
					name = "operator <<";
					break;
					
 				case "op_Quotation":
					name = "operator <@ @>";
					break;
					
 				case "op_QuotationUntyped":
					name = "operator <@@ @@>";
					break;
					
				case "op_Splice":
					name = "operator ~%";
					break;
					
				case "op_SpliceUntyped":
					name = "operator ~%%";
					break;
					
				case "op_AddressOf":
					name = "operator ~&";
					break;
					
				case "op_IntegerAddressOf":
					name = "operator ~&&";
					break;
					
				case "op_BooleanOr":
					name = "operator ||";
					break;
					
				case "op_BooleanAnd":
					name = "operator &&";
					break;
					
				case "op_AdditionAssignment":
					name = "operator +=";
					break;
					
				case "op_SubtractionAssignment":
					name = "operator -=";
					break;
					
				case "op_MultiplyAssignment":
					name = "operator *=";
					break;
					
				case "op_DivisionAssignment":
					name = "operator /=";
					break;
					
				case "op_Range":
					name = "operator ..";
					break;
					
 				case "op_RangeStep":
					name = "operator .. ..";
					break;
					
				case "op_ColonEquals":			// this one wasn't listed in the language reference
					name = "operator :=";
					break;
			}
			
			return name;
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		private static string DoGetRootName(TypeReference inType)
		{
			string root = string.Empty;
			
			if (inType is TypeSpecification)
			{
				TypeReference type = inType;
				
				while (type is TypeSpecification)
				{
					TypeSpecification spec = (TypeSpecification) type;
					type = spec.ElementType;
				}
				
				if (type != null)
				{
					root = type.FullName;
					DoValidateRoot("manufactured root", root);
				}
			}
			else if (inType != null)
				root = inType.FullName;
			
			return root;
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		private static string DoGetNameWithoutTick(string name)
		{
			return CsHelpers.TrimGeneric(name);
		}
		
		// TODO: This code is not quite correct: Cecil lazily populates the method body
		// so if the assembly is referenced by two directory editors and processed at
		// the same time by both of them Cecil gets confused. Not sure how best to fix 
		// this. We could try to change AssemblyCache so that it touches all the method 
		// bodies or we could perhaps somehow change assembly parsing so that it's done 
		// with one thread.
		[ThreadModel(ThreadModel.SingleThread)]
		private Tuple<string, int> DoGetSourceAndLine(MethodDefinition method)
		{
			string source = string.Empty;
			int line = -1;
			
			try
			{
				if (method.HasBody && method.Body.Instructions.Count > 0)	
				{
					Instruction ins = method.Body.Instructions[0];
					while (ins != null && ins.SequencePoint == null)		// note that the first instruction often does not have a sequence point
						ins = ins.Next;
						
					if (ins != null && ins.SequencePoint != null)
					{
						source = ins.SequencePoint.Document.Url;
						line = ins.SequencePoint.StartLine - 1;
					}
				}
			}
			catch (Exception e)
			{
				Log.WriteLine(TraceLevel.Error, "Errors", "Couldn't get source and line for {0}.", method);
				Log.WriteLine(TraceLevel.Error, "Errors", "{0}", e);
			}
			
			return Tuple.Create(source, line);
		}
		
		// Based on the gendarme code.
		[ThreadModel(ThreadModel.SingleThread)]
		private bool DoIsGeneratedCode(TypeDefinition type)
		{
			if (type.HasCustomAttributes)
				if (type.Module.Assembly.MainModule.Runtime >= TargetRuntime.Net_2_0)
					if (DoHasGeneratedAtribute(type.CustomAttributes))
						return true;
			
			switch (type.Name[0])
			{
				case '<': 			// e.g. <Module>, <PrivateImplementationDetails>
				case '$': 			// e.g. $ArrayType$1 nested inside <PrivateImplementationDetails>
					return true;
			}
			
			if (type.IsNested)
				return DoIsGeneratedCode(type.DeclaringType);
				
			return false;
		}
		
		[ThreadModel(ThreadModel.SingleThread)]
		private bool DoIsGeneratedCode(MethodDefinition method)
		{
			if (method.HasCustomAttributes)
				if (DoHasGeneratedAtribute(method.CustomAttributes))
					return true;
			
			return false;
		}
		
		[ThreadModel(ThreadModel.SingleThread)]
		private bool DoIsGeneratedCode(FieldDefinition field)
		{
			if (field.HasCustomAttributes)
				if (DoHasGeneratedAtribute(field.CustomAttributes))
					return true;
			
			return false;
		}
		
		[ThreadModel(ThreadModel.SingleThread)]
		private bool DoHasGeneratedAtribute(Collection<CustomAttribute> attrs)
		{
			foreach (CustomAttribute attr in attrs)
			{
				string fullName = attr.Constructor.DeclaringType.FullName;
				if (fullName == "System.CodeDom.Compiler.GeneratedCodeAttribute")
					return true;
				
				else if (fullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute")
					return true;
			}
			
			return false;
		}
		#endregion
		
		#region Fields 
		private Boss m_boss;
		private Database m_database;
		private string m_path;
		#endregion
	}
}
