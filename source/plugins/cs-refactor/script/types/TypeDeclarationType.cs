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

using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace CsRefactor.Script
{
	internal sealed class TypeDeclarationType : RefactorType
	{
		private TypeDeclarationType()
		{
		}
		
		public static TypeDeclarationType Instance
		{
			get
			{
				if (ms_instance == null)
					ms_instance = new TypeDeclarationType();
					
				return ms_instance;
			}
		}
		
		public override RefactorType Base
		{
			get {return TypeScopeType.Instance;}
		}
		
		public override string Name
		{
			get {return "TypeDeclaration";}
		}

		public override Type ManagedType
		{
			get {return typeof(CsType);}
		}
				
		protected override void RegisterMethods(RefactorType type)
		{
			type.Register<CsType>("get_Access", this.DoGetAccess);
			type.Register<CsType, string>("AddBase", this.DoAddBase);
			type.Register<CsType, string>("AddMember", this.DoAddMember);
			type.Register<CsType>("get_Attributes", this.DoGetAttributes);
			type.Register<CsType>("get_Bases", this.DoGetBases);
			type.Register<CsType>("get_Constraints", this.DoGetConstraints);
			type.Register<CsType>("get_DeclaringType", this.DoGetDeclaringType);
			type.Register<CsType>("get_Events", this.DoGetEvents);
			type.Register<CsType>("get_Fields", this.DoGetFields);
			type.Register<CsType>("get_FullName", this.DoGetFullName);
			type.Register<CsType>("get_GenericArguments", this.DoGetGenericArguments);
			type.Register<CsType>("get_Indexers", this.DoGetIndexers);
			type.Register<CsType>("get_IsAbstract", this.DoGetIsAbstract);
			type.Register<CsType>("get_IsInternal", this.DoGetIsInternal);
			type.Register<CsType>("get_IsPartial", this.DoGetIsPartial);
			type.Register<CsType>("get_IsPrivate", this.DoGetIsPrivate);
			type.Register<CsType>("get_IsProtected", this.DoGetIsProtected);
			type.Register<CsType>("get_IsPublic", this.DoGetIsPublic);
			type.Register<CsType>("get_IsSealed", this.DoGetIsSealed);
			type.Register<CsType>("get_IsStatic", this.DoGetIsStatic);
			type.Register<CsType>("get_Members", this.DoGetMembers);
			type.Register<CsType>("get_Methods", this.DoGetMethods);
			type.Register<CsType>("get_Modifiers", this.DoGetModifiers);
			type.Register<CsType>("get_Name", this.DoGetName);
			type.Register<CsType>("get_Operators", this.DoGetOperators);
			type.Register<CsType>("get_Properties", this.DoGetProperties);
			type.Register<CsType, string, string>("GetFieldName", this.DoGetFieldName);
			type.Register<CsType, string>("GetUniqueName", this.DoGetUniqueName);
			type.Register<CsType, string, object[]>("HasMember", this.DoHasMember);
		}
		
		#region Private Methods
		private object DoAddBase(CsType type, string name)
		{
			return new AddBaseType(type, name);
		}

		private object DoAddMember(CsType type, string text)
		{
			return new AddMember(type, text.Split('\n'));
		}

		private object DoGetAccess(CsType type)
		{
			return type.Access.ToString().ToLower();
		}

		private object DoGetAttributes(CsType type)
		{
			return type.Attributes;
		}

		private object DoGetBases(CsType type)
		{
			return type.Bases.Names;
		}

		private object DoGetConstraints(CsType type)
		{
			return type.Constraints;
		}

		private object DoGetDeclaringType(CsType type)
		{
			return type.DeclaringType;
		}

		private object DoGetEvents(CsType type)
		{
			return type.Events;
		}

		private object DoGetFields(CsType type)
		{
			return type.Fields;
		}
		
		private object DoGetFullName(CsType type)
		{
			return type.FullName;
		}
		
		private object DoGetGenericArguments(CsType type)
		{
			return type.GenericArguments;
		}
		
		private object DoGetIndexers(CsType type)
		{
			return type.Indexers;
		}
		
		private object DoGetIsPublic(CsType type)
		{
			return (type.Modifiers & MemberModifiers.Public) == MemberModifiers.Public;
		}
		
		private object DoGetIsProtected(CsType type)
		{
			return (type.Modifiers & MemberModifiers.Protected) == MemberModifiers.Protected;
		}
		
		private object DoGetIsInternal(CsType type)
		{
			return (type.Modifiers & MemberModifiers.Internal) == MemberModifiers.Internal;
		}
		
		private object DoGetIsPrivate(CsType type)
		{
			return (type.Modifiers & MemberModifiers.Private) == MemberModifiers.Private;
		}
		
		private object DoGetIsStatic(CsType type)
		{
			return (type.Modifiers & MemberModifiers.Static) == MemberModifiers.Static;
		}
		
		private object DoGetIsAbstract(CsType type)
		{
			return (type.Modifiers & MemberModifiers.Abstract) == MemberModifiers.Abstract;
		}
		
		private object DoGetIsSealed(CsType type)
		{
			if (type is CsStruct)
				return true;
			else
				return (type.Modifiers & MemberModifiers.Sealed) == MemberModifiers.Sealed;
		}
		
		private object DoGetIsPartial(CsType type)
		{
			return (type.Modifiers & MemberModifiers.Partial) == MemberModifiers.Partial;
		}
		
		private object DoGetMembers(CsType type)
		{
			return type.Members;
		}
		
		private object DoGetMethods(CsType type)
		{
			return type.Methods;
		}
		
		private object DoGetModifiers(CsType type)
		{
			return type.Modifiers.ToString().ToLower();
		}
		
		private object DoGetName(CsType type)
		{
			return type.Name;
		}
		
		private object DoGetOperators(CsType type)
		{
			return type.Operators;
		}
		
		private object DoGetProperties(CsType type)
		{
			return type.Properties;
		}
		
		private object DoGetFieldName(CsType type, string modifiers, string inName)
		{
			string name = DoMakeFieldName(inName, modifiers);
			for (int i = 2; i < 102; ++i)
			{
				if (!type.Members.Any(d => d.Name == name))	// TODO: might want to use the db to check base types
					return name;
				
				name = DoMakeFieldName(inName + i, modifiers);
			}
			
			throw new Exception("Couldn't find a unique name after 100 tries.");
		}
		
		private object DoGetUniqueName(CsType type, string inName)
		{
			string name = inName;
			for (int i = 2; i < 102; ++i)
			{
				if (!type.Members.Any(d => d.Name == name))	// TODO: might want to use the db to check base types
					return name;
				
				name = inName + i;
			}
			
			throw new Exception("Couldn't find a unique name after 100 tries.");
		}
		
		private string DoMakeFieldName(string name, string modifiers)
		{
			if (modifiers.Contains("const"))
				return name;
			else if (modifiers.Contains("static"))
				return "ms_" + name;					// TODO: use a preference for the prefix/suffix
			else
				return "m_" + name;
		}
		
		private object DoHasMember(CsType type, string name, object[] inTypes)
		{
			string[] types = new string[inTypes.Length];
			for (int i = 0; i < inTypes.Length; ++i)
			{
				if (inTypes[i] == null)
					throw new Exception(string.Format("Type #{0} is null.", i));

				string n = inTypes[i] as string;
				if (n == null)
					throw new Exception(string.Format("Type #{0} is a {1}, but should be a String.", i, RefactorType.GetName(inTypes[i].GetType())));
					
				types[i] = n;
			}
			
			if (types.Length == 0)
				if (DoHasEnum(type.Enums, name) || 
					DoHasEvent(type.Events, name) || 
					DoHasField(type.Fields, name) || 
					DoHasProperty(type.Properties, name))
					return true;

			return DoHasDelegate(type.Delegates, name, types) ||
				DoHasIndexer(type.Indexers, name, types) ||
				DoHasMethod(type.Methods, name, types) ||
				DoHasOperator(type.Operators, name, types);
		}
		
		private bool DoHasEnum(CsEnum[] members, string name)
		{
			foreach (var member in members)
			{
				if (member.Name == name)
					return true;
			}
			
			return false;
		}
		
		private bool DoHasEvent(CsEvent[] members, string name)
		{
			foreach (var member in members)
			{
				if (member.Name == name)
					return true;
			}
			
			return false;
		}
		
		private bool DoHasField(CsField[] members, string name)
		{
			foreach (var member in members)
			{
				if (member.Name == name)
					return true;
			}
			
			return false;
		}
		
		private bool DoHasProperty(CsProperty[] members, string name)
		{
			foreach (var member in members)
			{
				if (member.Name == name)
					return true;
			}
			
			return false;
		}
		
		private bool DoParametersMatch(CsParameter[] args, string[] types)
		{
			bool matches = args.Length == types.Length;
			
			for (int i = 0; i < args.Length && matches; ++i)
			{
				if (args[i].Type != types[i])
					matches = false;
			}

			return matches;
		}
		
		private bool DoHasDelegate(CsDelegate[] members, string name, string[] types)
		{
			foreach (var member in members)
			{
				if (member.Name == name && DoParametersMatch(member.Parameters, types))
					return true;
			}
			
			return false;
		}
		
		private bool DoHasIndexer(CsIndexer[] members, string name, string[] types)
		{
			foreach (var member in members)
			{
				if (member.Name == name && DoParametersMatch(member.Parameters, types))
					return true;
			}
			
			return false;
		}
		
		private bool DoHasMethod(CsMethod[] members, string name, string[] types)
		{
			foreach (var member in members)
			{
				if (member.Name == name && DoParametersMatch(member.Parameters, types))
					return true;
			}
			
			return false;
		}
		
		private bool DoHasOperator(CsOperator[] members, string name, string[] types)
		{
			foreach (var member in members)
			{
				if (member.Name == name && DoParametersMatch(member.Parameters, types))
					return true;
			}
			
			return false;
		}
		#endregion
		
		private static TypeDeclarationType ms_instance;
	} 
}
