// Copyright (C) 2007-2008 Jesse Jones
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
using Mono.Cecil.Cil;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
		public void Parse(string path, AssemblyDefinition assembly, string hash, bool fullParse)		// threaded
		{
			if (m_database == null)
			{
				m_database = new Database(m_path, "ParseAssembly-" + Path.GetFileNameWithoutExtension(m_path));
				DoCreateTables();
			}
			
//	Console.WriteLine("    parsing {0} for thread {1}", assembly.Name.FullName, System.Threading.Thread.CurrentThread.ManagedThreadId);
			Log.WriteLine("ObjectModel", "{0}parsing {1}", fullParse ? "fully " : string.Empty, assembly.Name.FullName);
			
			foreach (ModuleDefinition module in assembly.Modules)
			{
				foreach (TypeDefinition type in module.Types)
				{
					if (fullParse || type.IsPublic || type.IsNestedPublic || type.IsNestedFamily || type.IsNestedFamilyOrAssembly)
					{
						DoParseType(type, hash, fullParse);		// max time here is 0.8 secs on a fast machine for type System.Xml.Serialization.XmlSerializationReader
//						System.Threading.Thread.Sleep(50);	// this doesn't seem to help the main thread too much
					}
				}
			}
		}
		
		#region Private Methods
		private void DoCreateTables()
		{
			// TODO: once sqlite supports it the hash foreign keys should use ON DELETE CASCADE
			m_database.Update("create tables2", () =>
			{
				m_database.Update(@"
					CREATE TABLE IF NOT EXISTS Types(
						type TEXT NOT NULL
							CONSTRAINT no_empty_type CHECK(length(type) > 0),
						hash TEXT NOT NULL REFERENCES Assemblies(hash),
						name TEXT NOT NULL
							CONSTRAINT no_empty_name CHECK(length(name) > 0),
						declaring_type TEXT NOT NULL, 
						namespace TEXT NOT NULL, 
						base_type TEXT NOT NULL, 
						attributes INTEGER NOT NULL
							CONSTRAINT sane_attributes CHECK(attributes >= 0),
						PRIMARY KEY(type, hash)
					)");
				
				m_database.Update(@"
					CREATE TABLE IF NOT EXISTS NameInfo(
						full_name TEXT NOT NULL
							CONSTRAINT no_empty_fullname CHECK(length(full_name) > 0),
						hash TEXT NOT NULL REFERENCES Assemblies(hash),
						file_name TEXT NOT NULL,
						name TEXT NOT NULL
							CONSTRAINT no_empty_name CHECK(length(name) > 0),
						kind INTEGER NOT NULL
							CONSTRAINT sane_kind CHECK(kind >= 0 AND kind <= 3),
						PRIMARY KEY(full_name, hash, name)
					)");
				
				m_database.Update(@"
					CREATE TABLE IF NOT EXISTS Implements(
						type TEXT NOT NULL REFERENCES Types(type),
						hash TEXT NOT NULL REFERENCES Assemblies(hash), 
						interface_type TEXT NOT NULL,
						PRIMARY KEY(type, hash, interface_type)
					)");
				
				m_database.Update(@"
					CREATE TABLE IF NOT EXISTS Methods(
						method TEXT NOT NULL
							CONSTRAINT no_empty_method CHECK(length(method) > 0),
						hash TEXT NOT NULL REFERENCES Assemblies(hash), 
						return_type TEXT NOT NULL,
						name TEXT NOT NULL
							CONSTRAINT no_empty_name CHECK(length(name) > 0),
						arg_types TEXT NOT NULL,
						arg_names TEXT NOT NULL,
						declaring_type TEXT NOT NULL REFERENCES Types(type),
						file TEXT NOT NULL
							CONSTRAINT sane_file CHECK(length(file) = 0 OR substr(file, 1, 1) = '/'),
						line INTEGER NOT NULL
							CONSTRAINT sane_line CHECK(line >= -1),
						attributes INTEGER NOT NULL
							CONSTRAINT sane_attributes CHECK(attributes >= 0),
						semantics INTEGER NOT NULL
							CONSTRAINT sane_semantics CHECK(semantics >= 0),
						PRIMARY KEY(method, hash)
					)");
				
				m_database.Update(@"
					CREATE TABLE IF NOT EXISTS Members(
						text TEXT NOT NULL
							CONSTRAINT no_empty_text CHECK(length(text) > 0),
						type TEXT NOT NULL REFERENCES Types(type),
						namespace TEXT NOT NULL,
						is_protected INTEGER NOT NULL,
						is_static INTEGER NOT NULL,
						return_type TEXT NOT NULL,
						arg_names TEXT NOT NULL,
						hash TEXT NOT NULL REFERENCES Assemblies(hash), 
						PRIMARY KEY(text, type, namespace)
					)");
				
				m_database.Update(@"
					CREATE TABLE IF NOT EXISTS Fields(
						name TEXT NOT NULL
							CONSTRAINT no_empty_name CHECK(length(name) > 0),
						declaring_type TEXT NOT NULL REFERENCES Types(type),
						hash TEXT NOT NULL REFERENCES Assemblies(hash), 
						type TEXT NOT NULL,
						attributes INTEGER NOT NULL
							CONSTRAINT sane_attributes CHECK(attributes >= 0),
						PRIMARY KEY(name, declaring_type, hash)
					)");
			});
		}
		
		private void DoParseType(TypeDefinition type, string hash, bool fullParse)		// threaded
		{
			if (!DoIsGeneratedCode(type))
			{
				m_database.Update("parse " + type.FullName, () =>
				{
					m_database.Insert("Types",
						type.FullName,
						hash,
						type.Name.GetTypeName(),
						type.DeclaringType != null ? type.DeclaringType.FullName : string.Empty,
						!string.IsNullOrEmpty(type.Namespace) ? type.Namespace : string.Empty,
						type.BaseType != null ? type.BaseType.FullName : string.Empty,
						((uint) type.Attributes).ToString());
						
					if (type.HasInterfaces)
					{
						foreach (TypeReference i in type.Interfaces)
						{
							m_database.Insert("Implements",
								type.FullName,
								hash,
								i.FullName.GetTypeName());
						}
					}
					
					var files = new List<string>();
					if (type.HasConstructors)
					{
						foreach (MethodDefinition method in type.Constructors)
						{
							string fileName = DoParseMethod(type, method, hash, fullParse);
							if (fileName.Length > 0 && !files.Contains(fileName))
								files.Add(fileName);
						}
					}
					
					if (type.HasMethods)
					{
						foreach (MethodDefinition method in type.Methods)
						{
							string fileName = DoParseMethod(type, method, hash, fullParse);
							if (fileName.Length > 0 && !files.Contains(fileName))
								files.Add(fileName);
						}
					}
					
					if (type.HasFields)
					{
						foreach (FieldDefinition field in type.Fields)
						{
							DoParseField(type, field, hash, fullParse);
						}
					}
					
					if (files.Count == 0)
						files.Add(string.Empty);
					
					int kind;
					if (type.IsInterface)
						kind = 1;
					else
						kind = type.IsSealed ? 3 : 2;
					foreach (string file in files)
					{
						m_database.InsertOrReplace("NameInfo",
							type.FullName,
							hash,
							file,
							type.Name,							// adds names like String or List`1
							kind.ToString());
						
						m_database.InsertOrReplace("NameInfo",
							type.FullName,
							hash,
							file,
							DoGetNameWithoutTick(type.Name),	// adds List
							kind.ToString());
					}
				});
			}
		}
		
		private string DoGetNameWithoutTick(string name)	// theaded
		{
			int i = name.IndexOf('`');
			if (i >= 0)
				name = name.Substring(0, i);
			
			return name;
		}
		
		// TODO: This code is not quite correct: Cecil lazily populates the method body
		// so if the assembly is referenced by two directory editors and processed at
		// the same time by both of them Cecil gets confused. Not sure how best to fix 
		// this. We could try to change AssemblyCache so that it touches all the method 
		// bodies or we could perhaps somehow change assembly parsing so that it's done 
		// with one thread.
		private Tuple2<string, int> DoGetSourceAndLine(MethodDefinition method)	// theaded
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
			
			return Tuple.Make(source, line);
		}
		
		private string DoParseMethod(TypeDefinition type, MethodDefinition method, string hash, bool fullParse)		// threaded
		{
			string fileName = string.Empty;
			
			// Note that auto-prop methods count as generated code.
			if (method.IsGetter || method.IsSetter || !DoIsGeneratedCode(method))
			{
				var location = DoGetSourceAndLine(method);
				fileName = Path.GetFileName(location.First);
				
				if (fullParse || method.IsFamily || method.IsFamilyAndAssembly || method.IsFamilyOrAssembly || method.IsPublic)
				{
					var argTypes = new StringBuilder();
					var argNames = new StringBuilder();
					for (int i = 0; i < method.Parameters.Count; ++i)
					{
						ParameterDefinition p = method.Parameters[i];
						string name = p.ParameterType.FullName;
						
						if (DoIsParams(p))
							name = "params " + name;
							
						if (name.EndsWith("&"))
						{
							name = (p.IsOut ? "out " : "ref ") + name;
							name = name.Remove(name.Length - 1);
						}
						
						argTypes.Append(name);
						argNames.Append(p.Name);
						
						if (i + 1 < method.Parameters.Count)
						{
							argTypes.Append(':');
							argNames.Append(':');
						}
					}
					
					m_database.Insert("Methods",
						method.ToString(),
						hash,
						method.ReturnType.ReturnType.FullName.GetTypeName(),
						method.Name,
						argTypes.ToString(),
						argNames.ToString(),
						method.DeclaringType.FullName,
						location.First,
						location.Second.ToString(),
						((ushort) method.Attributes).ToString(),
						((ushort) method.SemanticsAttributes).ToString());
						
					if (!method.IsPrivate && !method.IsAbstract && !method.IsAddOn && !method.IsConstructor && !method.IsFire && !method.IsOther && !method.IsRemoveOn)
						if (!method.Name.StartsWith("op_") && method.Name != "Finalize")
							m_database.InsertOrReplace("Members",
								DoGetMethodText(method, 0),
								method.DeclaringType.FullName,
								string.Empty,								// not an extension method
								method.IsFamilyAndAssembly || method.IsFamilyOrAssembly || method.IsFamily ? "1" : "0",
								method.IsStatic ? "1" : "0",
								method.ReturnType.ReturnType.FullName.GetTypeName(),
								argNames.ToString(),
								hash);
					
					if (!type.IsInterface)		// TODO: might want to remove this when we can get file/line for interfaces
						m_database.InsertOrReplace("NameInfo",
							method.ToString(),
							hash,
							fileName,
							method.Name,
							"0");
							
					if (method.IsPublic && DoHasExtensionAtribute(method.CustomAttributes))
						m_database.InsertOrReplace("Members",
							DoGetMethodText(method, 1),
							method.Parameters[0].ParameterType.FullName.GetTypeName(),
							type.Namespace,
							"0",
							"0",					// extension methods are declared as statics, but not used that way
							method.ReturnType.ReturnType.FullName.GetTypeName(),
							argNames.ToString(),
							hash);
				}
			}
			
			return fileName;
		}
		
		private string DoGetMethodText(MethodDefinition method, int firstArg)
		{
			var builder = new StringBuilder();
			
			if (method.IsGetter || method.IsSetter)
			{
				builder.Append(method.Name.Substring(4));
			}
			else
			{
				builder.Append(method.Name);
				
				builder.Append('(');
				for (int i = firstArg; i < method.Parameters.Count; ++i)
				{
					ParameterDefinition p = method.Parameters[i];
					
					string type = CsHelpers.GetAliasedName(p.ParameterType.FullName);
					if (type == p.ParameterType.FullName)
					{
						type = DoTrimNamespace(type);
						type = DoTrimGeneric(type);
					}
					builder.Append(type);
					
					builder.Append(' ');	
					builder.Append(p.Name);
					
					if (i + 1 < method.Parameters.Count)
						builder.Append(", ");
				}
				builder.Append(')');
			}
			
			return builder.ToString();
		}
		
		// System.Collections.Generic.IEnumerable`1<TSource>:System.Func`2<TSource,System.Boolean>
		private string DoTrimNamespace(string type)
		{
			while (true)
			{
				int j = type.IndexOf('.');
				if (j < 0)
					break;
					
				int i = j;
				while (i > 0 && char.IsLetter(type[i - 1]))
					--i;
					
				type = type.Substring(0, i) + type.Substring(j + 1);
			}
			
			return type;
		}
		
		private string DoTrimGeneric(string type)
		{
			while (true)
			{
				int i = type.IndexOf('`');
				if (i < 0)
					break;
					
				int count = 1;
				while (i + count < type.Length && char.IsDigit(type[i + count]))
					++count;
					
				if (count > 1)
				{
					type = type.Substring(0, i) + type.Substring(i + count);
				}
			}
			
			return type;
		}
		
		private void DoParseField(TypeDefinition type, FieldDefinition field, string hash, bool fullParse)		// threaded
		{
			if (!DoIsGeneratedCode(field))
			{
				if (fullParse || field.IsFamily || field.IsFamilyOrAssembly || field.IsFamilyOrAssembly || field.IsPublic)
				{
					m_database.InsertOrReplace("Fields",
						field.Name,
						field.DeclaringType.FullName.GetTypeName(),
						hash,
						field.FieldType.FullName.GetTypeName(),
						((ushort) field.Attributes).ToString());
					
					if (!field.IsPrivate)
						m_database.InsertOrReplace("Members",
							field.Name,
							field.DeclaringType.FullName.GetTypeName(),
							string.Empty,								// not an extension method
							field.IsFamilyAndAssembly || field.IsFamilyOrAssembly || field.IsFamily ? "1" : "0",
							field.IsStatic ? "1" : "0",
							field.FieldType.FullName.GetTypeName(),
							string.Empty,
							hash);
				}
			}
		}
		
		// Based on the gendarme code.
		private bool DoIsGeneratedCode(TypeReference type)	// theaded
		{
			if (type.HasCustomAttributes)
				if (type.Module.Assembly.Runtime >= TargetRuntime.NET_2_0)
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
		
		private bool DoIsGeneratedCode(MethodDefinition method)	// theaded
		{
			if (method.HasCustomAttributes)
				if (DoHasGeneratedAtribute(method.CustomAttributes))
					return true;
			
			return false;
		}
		
		private bool DoIsGeneratedCode(FieldDefinition field)	// theaded
		{
			if (field.HasCustomAttributes)
				if (DoHasGeneratedAtribute(field.CustomAttributes))
					return true;
			
			return false;
		}
		
		private bool DoHasGeneratedAtribute(CustomAttributeCollection attrs)	// theaded
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
		
		private bool DoHasExtensionAtribute(CustomAttributeCollection attrs)	// theaded
		{
			foreach (CustomAttribute attr in attrs)
			{
				string fullName = attr.Constructor.DeclaringType.FullName;
				if (fullName == "System.Runtime.CompilerServices.ExtensionAttribute")
					return true;
			}
			
			return false;
		}
		
		private bool DoIsParams(ParameterDefinition param)
		{
			if (param.HasCustomAttributes)
			{
				foreach (CustomAttribute a in param.CustomAttributes)
				{
					if (a.Constructor.DeclaringType.Name == "ParamArrayAttribute")
						return true;
				}
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
