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
	internal sealed class TypeScopeType : RefactorType
	{
		private TypeScopeType()
		{
		}
		
		public static TypeScopeType Instance
		{
			get
			{
				if (ms_instance == null)
					ms_instance = new TypeScopeType();
					
				return ms_instance;
			}
		}
				
		public override RefactorType Base
		{
			get {return DeclarationType.Instance;}
		}
		
		public override string Name
		{
			get {return "TypeScope";}
		}
		
		public override Type ManagedType
		{
			get {return typeof(CsTypeScope);}
		}
		
		protected override void RegisterMethods(RefactorType type)
		{
			type.Register<CsTypeScope>("get_Body", this.DoGetBody);
			type.Register<CsTypeScope>("get_Classes", this.DoGetClasses);
			type.Register<CsTypeScope>("get_Declarations", this.DoGetDeclarations);
			type.Register<CsTypeScope>("get_Delegates", this.DoGetDelegates);
			type.Register<CsTypeScope>("get_Enums", this.DoGetEnums);
			type.Register<CsTypeScope>("get_Interfaces", this.DoGetInterfaces);
			type.Register<CsTypeScope>("get_Namespace", this.DoGetNamespace);
			type.Register<CsTypeScope>("get_Structs", this.DoGetStructs);
			type.Register<CsTypeScope>("get_Types", this.DoGetTypes);
		}
		
		#region Private Methods
		private object DoGetBody(CsTypeScope outer)
		{
			return outer.Body;
		}

		private object DoGetClasses(CsTypeScope outer)
		{
			return outer.Classes;
		}
		
		private object DoGetDeclarations(CsTypeScope outer)
		{
			return outer.Declarations;
		}
		
		private object DoGetDelegates(CsTypeScope outer)
		{
			return outer.Delegates;
		}
		
		private object DoGetEnums(CsTypeScope outer)
		{
			return outer.Enums;
		}

		private object DoGetInterfaces(CsTypeScope outer)
		{
			return outer.Interfaces;
		}

		private object DoGetNamespace(CsTypeScope outer)
		{
			return outer.Namespace;
		}

		private object DoGetStructs(CsTypeScope outer)
		{
			return outer.Structs;
		}

		private object DoGetTypes(CsTypeScope outer)
		{
			return outer.Types;
		}
		#endregion

		private static TypeScopeType ms_instance;
	} 
}
