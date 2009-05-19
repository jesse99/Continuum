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
using MCocoa;
using MObjc;
using Mono.Cecil;
using Shared;
using System;
using System.Collections.Generic;

namespace Disassembler
{
	[ExportClass("NamespaceItem", "AssemblyItem")]
	internal sealed class NamespaceItem : AssemblyItem
	{
		public NamespaceItem(string ns) : base(NSObject.AllocNative("NamespaceItem"))
		{
			Contract.Requires(ns != null, "ns is null");
			
			m_namespace = ns;
		}
		
		public string Namespace
		{
			get {return m_namespace;}
		}
		
		public void Add(TypeDefinition type)
		{
			Contract.Requires(type != null, "type is null");
			
			var item = new TypeItem(type);
			item.retain();
			m_types.Add(item);
		}
		
		public void OnLoaded()
		{
			m_types.Sort((lhs, rhs) => lhs.Label.CompareTo(rhs.Label));
			m_types.ForEach(t => t.OnLoaded());
		}
		
		public override string Label
		{
			get {return m_namespace.Length == 0 ? "globals" : m_namespace;}
		}
		
		public override string FullName
		{
			get {return Label;}
		}
		
		public override int ChildCount
		{
			get {return m_types.Count;}
		}
		
		public override string GetText()
		{
			return string.Empty;
		}
		
		public override AssemblyItem GetChild(int index)
		{
			return m_types[index];
		}
		
		#region Fields
		private string m_namespace;
		private List<TypeItem> m_types = new List<TypeItem>();
		#endregion
	}
	
	[ExportClass("TypeItem", "AssemblyItem")]
	internal sealed class TypeItem : AssemblyItem
	{
		public TypeItem(TypeDefinition type) : base(NSObject.AllocNative("TypeItem"))
		{
			Contract.Requires(type != null, "type is null");
			
			m_type = type;
			
			foreach (MethodDefinition method in type.Constructors)
			{
				var item = new MethodItem(method);
				item.retain();
				m_methods.Add(item);
			}
			
			foreach (MethodDefinition method in type.Methods)
			{
				var item = new MethodItem(method);
				item.retain();
				m_methods.Add(item);
			}
		}
		
		public void OnLoaded()
		{
			for (int i = 0; i < m_methods.Count; ++i)
			{
				MethodItem m1 = m_methods[i];
				for (int j = i + 1; j < m_methods.Count; ++j)
				{
					MethodItem m2 = m_methods[j];
					if (m1.Label == m2.Label)
					{
						m1.DisambiguateLabel();
						m2.DisambiguateLabel();
					}
				}
			}
			
			m_methods.Sort((lhs, rhs) =>
			{
				int result = 0;
				
				if (result == 0)
					result = lhs.Method.Name.CompareTo(rhs.Method.Name);
				
				if (result == 0)
					result = lhs.Method.Parameters.Count.CompareTo(rhs.Method.Parameters.Count);
				
				if (result == 0)
					result = lhs.Label.CompareTo(rhs.Label);
					
				return result;
			});
		}
		
		public override string Label
		{
			get
			{
				if (m_type.DeclaringType != null)
					return m_type.DeclaringType.Name + "/" + m_type.Name;
				else
					return m_type.Name;
			}
		}
		
		public override string FullName
		{
			get {return m_type.FullName;}
		}
		
		public override int ChildCount
		{
			get {return m_methods.Count;}
		}
		
		public override string GetText()
		{
			return string.Empty;
		}
		
		public override AssemblyItem GetChild(int index)
		{
			return m_methods[index];
		}
		
		#region Fields
		private TypeDefinition m_type;
		private List<MethodItem> m_methods = new List<MethodItem>();
		#endregion
	}
	
	[ExportClass("MethodItem", "AssemblyItem")]
	internal sealed class MethodItem : AssemblyItem
	{
		public MethodItem(MethodDefinition method) : base(NSObject.AllocNative("MethodItem"))
		{
			Contract.Requires(method != null, "method is null");
			
			m_method = method;
			m_label = method.Name;
		}
		
		public void DisambiguateLabel()
		{
			var builder = new System.Text.StringBuilder();
			
			builder.Append(m_method.Name);
			builder.Append('(');							// might want to use the AddSpace pref, but that's a project pref so we can't always get at it...
			
			for (int i = 0; i < m_method.Parameters.Count;++i)
			{
				builder.Append(m_method.GetParameterModifier(i));

				ParameterDefinition p = m_method.Parameters[i];
				builder.Append(p.ParameterType.Name);
				builder.Append(' ');
				builder.Append(p.Name);
				
				if (i + 1 < m_method.Parameters.Count)
					builder.Append(", ");
			}
			
			builder.Append(')');
			
			m_label = builder.ToString();
		}
		
		public MethodDefinition Method
		{
			get {return m_method;}
		}
		
		public override string Label
		{
			get {return m_label;}
		}
		
		public override string FullName
		{
			get {return m_method.ToString();}
		}
		
		public override int ChildCount
		{
			get {return 0;}
		}
		
		public override string GetText()
		{
			return m_method.Disassemble();
		}
		
		public override AssemblyItem GetChild(int index)
		{
			throw new InvalidOperationException("methods have no children");
		}
		
		#region Fields
		private MethodDefinition m_method;
		private string m_label;
		#endregion
	}
}
