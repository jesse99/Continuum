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
	internal sealed class DelegateType : RefactorType
	{
		private DelegateType()
		{
		}
		
		public static DelegateType Instance 
		{
			get
			{
				if (ms_instance == null)
					ms_instance = new DelegateType();
					
				return ms_instance;
			}
		}
		
		public override RefactorType Base
		{
			get {return MemberType.Instance;}
		}
		
		public override string Name
		{
			get {return "Delegate";}
		}
		
		public override Type ManagedType
		{
			get {return typeof(CsDelegate);}
		}
		
		protected override void RegisterMethods(RefactorType type)
		{
			type.Register<CsDelegate>("get_Constraints", this.DoGetConstraints);
			type.Register<CsDelegate>("get_GenericArguments", this.DoGetGenericArguments);
			type.Register<CsDelegate>("get_Namespace", this.DoGetNamespace);
			type.Register<CsDelegate>("get_Parameters", this.DoGetParameters);
			type.Register<CsDelegate>("get_ReturnType", this.DoGetReturnType);
		}
		
		#region Private Methods
		private object DoGetConstraints(CsDelegate type)
		{
			return type.Constraints;
		}
		
		private object DoGetGenericArguments(CsDelegate type)
		{
			return type.GenericArguments;
		}
		
		private object DoGetNamespace(CsDelegate type)
		{
			return type.Namespace;
		}
		
		private object DoGetParameters(CsDelegate type)
		{
			return type.Parameters;
		}
		
		private object DoGetReturnType(CsDelegate type)
		{
			return type.ReturnType;
		}
		#endregion
		
		private static DelegateType ms_instance;
	}
}
