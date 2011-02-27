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
		public NamespaceItem(string ns, string assemblyPath) : base(NSObject.AllocAndInitInstance("NamespaceItem"))
		{
			Contract.Requires(ns != null, "ns is null");
			
			m_namespace = ns;
			m_assemblyPath = assemblyPath;
		}
		
		public string Namespace
		{
			get {return m_namespace;}
		}
		
		public void Add(TypeDefinition type)
		{
			Contract.Requires(type != null, "type is null");
			
			var item = new TypeItem(type, m_assemblyPath);
			m_types.Add(item);
		}
		
		protected override void OnDealloc()
		{
			m_types.ForEach(t => t.release());
			m_types.Clear();
			
			base.OnDealloc();
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
		
		public override string GetInfo()
		{
			var builder = new System.Text.StringBuilder();
			
			foreach (TypeItem type in m_types)
			{
				builder.AppendLine(type.FullName);
			}
			
			return builder.ToString();
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
		private string m_assemblyPath;
		#endregion
	}
	
	[ExportClass("TypeItem", "AssemblyItem")]
	internal sealed class TypeItem : AssemblyItem
	{
		public TypeItem(TypeDefinition type, string assemblyPath) : base(NSObject.AllocAndInitInstance("TypeItem"))
		{
			Contract.Requires(type != null, "type is null");
			
			m_type = type;
			m_assemblyPath = assemblyPath;
			
			foreach (MethodDefinition method in type.Methods)
			{
				if (method.IsConstructor)
				{
					var item = new MethodItem(method, m_assemblyPath);
					m_methods.Add(item);
				}
			}
			
			foreach (MethodDefinition method in type.Methods)
			{
				if (!method.IsConstructor)
				{
					var item = new MethodItem(method, m_assemblyPath);
					m_methods.Add(item);
				}
			}
		}
		
		protected override void OnDealloc()
		{
			m_methods.ForEach(m => m.release());
			m_methods.Clear();
			
			base.OnDealloc();
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
		
		public override string GetInfo()
		{
			var builder = new System.Text.StringBuilder();
			
			TypeAttributes attrs = m_type.Attributes;
			builder.AppendLine("BeforeFieldInit: " + ((attrs & TypeAttributes.BeforeFieldInit) == TypeAttributes.BeforeFieldInit));
			builder.AppendLine("ClassSize: " + m_type.ClassSize);
			builder.AppendLine("FullName: " + m_type.FullName);
			builder.AppendLine("MetadataToken: " + m_type.MetadataToken);
			builder.AppendLine("Module: " + m_type.Module.Name);
			builder.AppendLine("RTSpecialName: " + ((attrs & TypeAttributes.RTSpecialName) == TypeAttributes.RTSpecialName));
			
			return builder.ToString();
		}
		
		public override string GetText()
		{
			return m_type.Disassemble(m_assemblyPath);
		}
		
		public override AssemblyItem GetChild(int index)
		{
			return m_methods[index];
		}
		
		#region Fields
		private TypeDefinition m_type;
		private List<MethodItem> m_methods = new List<MethodItem>();
		private string m_assemblyPath;
		#endregion
	}
	
	[ExportClass("MethodItem", "AssemblyItem")]
	internal sealed class MethodItem : AssemblyItem
	{
		public MethodItem(MethodDefinition method, string assemblyPath) : base(NSObject.AllocAndInitInstance("MethodItem"))
		{
			Contract.Requires(method != null, "method is null");
			
			m_method = method;
			m_assemblyPath = assemblyPath;
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
		
		public override string GetInfo()
		{
			var builder = new System.Text.StringBuilder();
			
			MethodAttributes attrs = m_method.Attributes;
			builder.AppendLine("CallingConvention: " + m_method.CallingConvention);
			builder.AppendLine("CodeSize: " + (m_method.Body != null ? m_method.Body.CodeSize : 0));
			builder.AppendLine("HideBySig: " + ((attrs & MethodAttributes.HideBySig) == MethodAttributes.HideBySig));
			builder.AppendLine("ImplAttributes: " + DoImplToText(m_method.ImplAttributes));
			builder.AppendLine("InitLocals: " + (m_method.Body != null ? m_method.Body.InitLocals : false));
			builder.AppendLine("MaxStack: " + (m_method.Body != null ? m_method.Body.MaxStackSize : 0));
			builder.AppendLine("MetadataToken: " + m_method.MetadataToken);
			builder.AppendLine("RequireSecObject: " + ((attrs & MethodAttributes.RequireSecObject) == MethodAttributes.RequireSecObject));
			builder.AppendLine("SemanticsAttributes: " + m_method.SemanticsAttributes);
			
			return builder.ToString();
		}
		
		public override string GetText()
		{
			return m_method.Disassemble(m_assemblyPath);
		}
		
		public override AssemblyItem GetChild(int index)
		{
			throw new InvalidOperationException("methods have no children");
		}
		
		#region Private Methods
		// MethodImplAttributes has multiple fields with the same values so ToString
		// won't always return the correct names.
		private static string DoImplToText(MethodImplAttributes attrs)
		{
			var builder = new System.Text.StringBuilder();
			
			MethodImplAttributes type = attrs & MethodImplAttributes.CodeTypeMask;
			switch (type)
			{
				case MethodImplAttributes.IL:
					builder.Append("IL");
					break;
					
				case MethodImplAttributes.Native:
					builder.Append("Native");
					break;
					
				case MethodImplAttributes.OPTIL:
					builder.Append("OPTIL");
					break;
					
				case MethodImplAttributes.Runtime:
					builder.Append("Runtime");
					break;
				
				default:
					Contract.Assert(false, "bad type: " + type);
					break;
			}
			
			if ((attrs & MethodImplAttributes.Unmanaged) == MethodImplAttributes.Unmanaged)
				builder.Append(" | Unmanaged");
			
			if ((attrs & MethodImplAttributes.ForwardRef) == MethodImplAttributes.ForwardRef)
				builder.Append(" | ForwardRef");
			
			if ((attrs & MethodImplAttributes.PreserveSig) == MethodImplAttributes.PreserveSig)
				builder.Append(" | PreserveSig");
			
			if ((attrs & MethodImplAttributes.InternalCall) == MethodImplAttributes.InternalCall)
				builder.Append(" | InternalCall");
			
			if ((attrs & MethodImplAttributes.Synchronized) == MethodImplAttributes.Synchronized)
				builder.Append(" | Synchronized");
			
			if ((attrs & MethodImplAttributes.NoInlining) == MethodImplAttributes.NoInlining)
				builder.Append(" | NoInlining");
			
			return builder.ToString();
		}
		#endregion
		
		#region Fields
		private MethodDefinition m_method;
		private string m_label;
		private string m_assemblyPath;
		#endregion
	}
	
	[ExportClass("ResourcesItem", "AssemblyItem")]
	internal sealed class ResourcesItem : AssemblyItem
	{
		public ResourcesItem() : base(NSObject.AllocAndInitInstance("ResourcesItem"))
		{
		}
		
		public new ResourcesItem Retain()
		{
			Unused.Value = retain();
			return this;
		}
		
		public void Add(Resource resource)
		{
			Contract.Requires(resource != null, "resource is null");
			
			do
			{
				AssemblyLinkedResource alr = resource as AssemblyLinkedResource;
				if (alr != null)
				{
					m_resources.Add(new AssemblyResourceItem(alr));
					break;
				}
				
				EmbeddedResource er = resource as EmbeddedResource;
				if (er != null)
				{
					m_resources.Add(new EmbeddedResourceItem(er));
					break;
				}
				
				LinkedResource lr = resource as LinkedResource;
				if (lr != null)
				{
					m_resources.Add(new LinkedResourceItem(lr));
					break;
				}
				
				Contract.Assert(false, "bad resource type: " + resource.GetType());
			}
			while (false);
		}
		
		protected override void OnDealloc()
		{
			m_resources.ForEach(r => r.release());
			m_resources.Clear();
			
			base.OnDealloc();
		}
		
		public void OnLoaded()
		{
			m_resources.Sort((lhs, rhs) => lhs.Label.CompareTo(rhs.Label));
		}
		
		public override string Label
		{
			get {return "resources";}
		}
		
		public override string FullName
		{
			get {return Label;}
		}
		
		public override int ChildCount
		{
			get {return m_resources.Count;}
		}
		
		public override string GetInfo()
		{
			var builder = new System.Text.StringBuilder();
			
			foreach (ResourceItem r in m_resources)
			{
				builder.AppendLine(r.FullName);
			}
			
			return builder.ToString();
		}
		
		public override string GetText()
		{
			return string.Empty;
		}
		
		public override AssemblyItem GetChild(int index)
		{
			return m_resources[index];
		}
		
		#region Fields
		private List<ResourceItem> m_resources = new List<ResourceItem>();
		#endregion
	}

	[ExportClass("ResourceItem", "AssemblyItem")]
	internal abstract class ResourceItem : AssemblyItem
	{
		protected ResourceItem(Resource resource, string type) : base(NSObject.AllocAndInitInstance(type))
		{
			Contract.Requires(resource != null, "resource is null");
			
			m_name = resource.Name;
		}
		
		public override string Label
		{
			get {return m_name;}
		}
		
		public override string FullName
		{
			get {return m_name;}
		}
		
		public override int ChildCount
		{
			get {return 0;}
		}
		
		public override AssemblyItem GetChild(int index)
		{
			throw new InvalidOperationException("resources have no children");
		}
		
		public override string Extension()
		{
			return ".bin";
		}
		
		#region Fields
		private string m_name;
		#endregion
	}
	
	[ExportClass("AssemblyResourceItem", "ResourceItem")]
	internal sealed class AssemblyResourceItem : ResourceItem
	{
		public AssemblyResourceItem(AssemblyLinkedResource resource) : base(resource, "AssemblyResourceItem")
		{
			m_resource = resource;
		}
		
		public override string GetInfo()
		{
			var builder = new System.Text.StringBuilder();
			
			builder.AppendLine("Assembly: " + m_resource.Assembly.FullName);
			builder.AppendLine("Flags: " + (m_resource.IsPublic ? "public " : " ") + (m_resource.IsPrivate ? "private " : " "));
			
			return builder.ToString();
		}
		
		public override string GetText()
		{
			return string.Empty;
		}
		
		#region Fields
		private AssemblyLinkedResource m_resource;
		#endregion
	}
	
	[ExportClass("EmbeddedResourceItem", "ResourceItem")]
	internal sealed class EmbeddedResourceItem : ResourceItem
	{
		public EmbeddedResourceItem(EmbeddedResource resource) : base(resource, "EmbeddedResourceItem")
		{
			m_resource = resource;
		}
		
		public override string GetInfo()
		{
			var builder = new System.Text.StringBuilder();
			
			builder.AppendLine("Flags: " + (m_resource.IsPublic ? "public " : " ") + (m_resource.IsPrivate ? "private " : " "));
			builder.AppendLine("Size: " + m_resource.GetResourceData().Length + " bytes");
			
			return builder.ToString();
		}
		
		public override string GetText()
		{
			return m_resource.GetResourceData().ToText();
		}
		
		#region Fields
		private EmbeddedResource m_resource;
		#endregion
	}
	
	[ExportClass("LinkedResourceItem", "ResourceItem")]
	internal sealed class LinkedResourceItem : ResourceItem
	{
		public LinkedResourceItem(LinkedResource resource) : base(resource, "LinkedResourceItem")
		{
			m_resource = resource;
		}
		
		public override string GetInfo()
		{
			var builder = new System.Text.StringBuilder();
			
			builder.AppendLine("File: " + m_resource.File);
			builder.AppendLine("Flags: " + (m_resource.IsPublic ? "public " : " ") + (m_resource.IsPrivate ? "private " : " "));
			builder.AppendLine("Hash: " + (m_resource.Hash != null && m_resource.Hash.Length > 0 ? BitConverter.ToString(m_resource.Hash) : "none"));
			
			return builder.ToString();
		}
		
		public override string GetText()
		{
			return string.Empty;
		}
		
		#region Fields
		private LinkedResource m_resource;
		#endregion
	}
}
