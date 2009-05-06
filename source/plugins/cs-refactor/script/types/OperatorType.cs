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
	internal sealed class OperatorType : RefactorType
	{
		private OperatorType()
		{
		}
		
		public static OperatorType Instance
		{
			get
			{
				if (ms_instance == null)
					ms_instance = new OperatorType();
					
				return ms_instance;
			}
		}
		
		public override RefactorType Base
		{
			get {return MemberType.Instance;}
		}
		
		public override string Name
		{
			get {return "Operator";}
		}
		
		public override Type ManagedType
		{
			get {return typeof(CsOperator);}
		}
		
		protected override void RegisterMethods(RefactorType type)
		{
			type.Register<CsOperator>("get_Body", this.DoGetBody);
			type.Register<CsOperator>("get_IsConversion", this.DoGetIsConversion);
			type.Register<CsOperator>("get_IsExplicit", this.DoGetIsExplicit);
			type.Register<CsOperator>("get_IsImplicit", this.DoGetIsImplicit);
			type.Register<CsOperator>("get_Parameters", this.DoGetParameters);
			type.Register<CsOperator>("get_ReturnType", this.DoGetReturnType);
		}
		
		#region Private Methods
		private object DoGetBody(CsOperator member)
		{
			return member.Body;
		}
		
		private object DoGetIsConversion(CsOperator member)
		{
			return member.IsConversion;
		}
		
		private object DoGetIsExplicit(CsOperator member)
		{
			return member.IsExplicit;
		}
		
		private object DoGetIsImplicit(CsOperator member)
		{
			return member.IsImplicit;
		}
		
		private object DoGetParameters(CsOperator member)
		{
			return member.Parameters;
		}

		private object DoGetReturnType(CsOperator member)
		{
			return member.ReturnType;
		}
		#endregion

		private static OperatorType ms_instance;
	} 
}
