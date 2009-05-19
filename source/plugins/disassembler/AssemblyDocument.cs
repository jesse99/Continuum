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

//using Gear;
using Gear.Helpers;
using MCocoa;
using MObjc;
using Mono.Cecil;
using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Disassembler
{
	[ExportClass("AssemblyDocument", "NSDocument")]
	internal sealed class AssemblyDocument : NSDocument
	{
		private AssemblyDocument(IntPtr instance) : base(instance)
		{
			ActiveObjects.Add(this);
		}
		
		public new void makeWindowControllers()
		{
			NSWindowController controller = new AssemblyController(this);
			addWindowController(controller);
		}
		
		public bool readFromData_ofType_error(NSData data, NSString typeName, IntPtr outError)
		{
			bool read = true;
			
			try
			{
				byte[] bytes = new byte[data.length()];
				Marshal.Copy(data.bytes(), bytes, 0, bytes.Length);
				
				AssemblyDefinition assembly = AssemblyFactory.GetAssembly(bytes);
				DoGetNamespaces(assembly);
			}
			catch (Exception e)
			{
				NSMutableDictionary userInfo = NSMutableDictionary.Create();
				userInfo.setObject_forKey(NSString.Create("Couldn't read the assembly."), Externs.NSLocalizedDescriptionKey);
				userInfo.setObject_forKey(NSString.Create(e.Message), Externs.NSLocalizedFailureReasonErrorKey);
				
				NSObject error = NSError.errorWithDomain_code_userInfo(Externs.Cocoa3Domain, 1, userInfo);
				Marshal.WriteIntPtr(outError, error);
				
				read = false;
			}
			
			return read;
		}
		
		public NamespaceItem[] Namespaces
		{
			get {return m_namespaces.ToArray();}
		}
		
		#region Private Methods
		// HasCustomAttributes/CustomAttributes
		// HasSecurityDeclarations/SecurityDeclarations
		// Kind
		// Name
		// Runtime
		private void DoGetNamespaces(AssemblyDefinition assembly)
		{
			// AssemblyReferences
			// HasCustomAttributes/CustomAttributes
			// ExternTypes
			// Image
			// MemberReferences
			// ModuleReferences
			// Resources
			// TypeReferences
			foreach (ModuleDefinition module in assembly.Modules)
			{
				foreach (TypeDefinition type in module.Types)
				{
					Contract.Assert(type.Namespace != null);
					
					string ns;
					if (type.DeclaringType != null)
						ns = type.DeclaringType.Namespace;
					else
						ns = type.Namespace;
					
					NamespaceItem item = m_namespaces.Find(n => n.Namespace == ns);
					if (item == null)
					{
						item = new NamespaceItem(ns);
						item.retain();
						m_namespaces.Add(item);
					}
					
					item.Add(type);
				}
			}
			
			m_namespaces.Sort((lhs, rhs) => lhs.Label.CompareTo(rhs.Label));
			m_namespaces.ForEach(n => n.OnLoaded());
		}
		#endregion
		
		#region Fields
		private List<NamespaceItem> m_namespaces = new List<NamespaceItem>();
		#endregion
	}
}
