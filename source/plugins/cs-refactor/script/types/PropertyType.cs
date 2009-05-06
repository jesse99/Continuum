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
	internal sealed class PropertyType : RefactorType
	{
		private PropertyType()
		{
		}
		
		public static PropertyType Instance
		{
			get
			{
				if (ms_instance == null)
					ms_instance = new PropertyType();
					
				return ms_instance;
			}
		}
		
		public override RefactorType Base
		{
			get {return MemberType.Instance;}
		}
		
		public override string Name
		{
			get {return "Property";}
		}
		
		public override Type ManagedType
		{
			get {return typeof(CsProperty);}
		}
		
		protected override void RegisterMethods(RefactorType type)
		{
			type.Register<CsProperty>("get_GetterAccess", this.DoGetGetterAccess);
			type.Register<CsProperty>("get_GetterAttributes", this.DoGetGetterAttributes);
			type.Register<CsProperty>("get_GetterBody", this.DoGetGetterBody);
			type.Register<CsProperty>("get_HasGetter", this.DoGetHasGetter);
			type.Register<CsProperty>("get_HasSetter", this.DoGetHasSetter);
			type.Register<CsProperty>("get_ReturnType", this.DoGetReturnType);
			type.Register<CsProperty>("get_SetterAccess", this.DoGetSetterAccess);
			type.Register<CsProperty>("get_SetterAttributes", this.DoGetSetterAttributes);
			type.Register<CsProperty>("get_SetterBody", this.DoGetSetterBody);
		}
		
		#region Private Methods
		private object DoGetGetterAccess(CsProperty member)
		{
			return member.GetterAccess != 0 ? member.GetterAccess.ToString().ToLower() : null;
		}
		
		private object DoGetGetterAttributes(CsProperty member)
		{
			return member.GetterAttributes;
		}
		
		private object DoGetGetterBody(CsProperty member)
		{
			return member.GetterBody;
		}
		
		private object DoGetHasGetter(CsProperty member)
		{
			return member.HasGetter;
		}
		
		private object DoGetHasSetter(CsProperty member)
		{
			return member.HasSetter;
		}
		
		private object DoGetReturnType(CsProperty member)
		{
			return member.ReturnType;
		}
		
		private object DoGetSetterAccess(CsProperty member)
		{
			return member.SetterAccess != 0 ? member.SetterAccess.ToString().ToLower() : null;
		}
		
		private object DoGetSetterAttributes(CsProperty member)
		{
			return member.SetterAttributes;
		}		

		private object DoGetSetterBody(CsProperty member)
		{
			return member.SetterBody;
		}
		#endregion

		private static PropertyType ms_instance;
	} 
}
