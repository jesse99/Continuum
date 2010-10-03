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
	internal sealed class FieldType : RefactorType
	{
		private FieldType()
		{
		}
		
		public static FieldType Instance
		{
			get
			{
				if (ms_instance == null)
					ms_instance = new FieldType();
					
				return ms_instance;
			}
		}
		
		public override RefactorType Base
		{
			get {return MemberType.Instance;}
		}
		
		public override string Name
		{
			get {return "Field";}
		}
		
		public override Type ManagedType
		{
			get {return typeof(CsField);}
		}
		
		protected override void RegisterMethods(RefactorType type)
		{
			type.Register<CsField>("get_Type", this.DoGetType);
			type.Register<CsField>("get_Value", this.DoGetValue);
		}
		
		#region Private Methods
		private object DoGetType(CsField type)
		{
			return type.Type;
		}
		
		private object DoGetValue(CsField type)
		{
			return type.Value;
		}
		#endregion
		
		private static FieldType ms_instance;
	}
}
