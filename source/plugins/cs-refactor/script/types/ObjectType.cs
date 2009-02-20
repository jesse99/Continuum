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
	internal sealed class ObjectType : RefactorType
	{
		private ObjectType()
		{
		}
		
		public static ObjectType Instance 
		{
			get 
			{
				if (ms_instance == null)
					ms_instance = new ObjectType();
					
				return ms_instance;
			}
		}
		
		public override RefactorType Base
		{
			get {return null;}
		}
		
		public override string Name
		{
			get {return "Object";}
		}

		public override Type ManagedType
		{
			get {return typeof(object);}
		}
		
		protected override void RegisterMethods(RefactorType type)
		{
			type.Register<object, object>("op_Equals", this.DoEquals);
			type.Register<object, string>("op_IsType", this.DoIsType);
			type.Register<object, object>("op_NotEquals", this.DoNotEquals);
		}
		
		#region Private Methods
		private object DoEquals(object instance, object rhs)
		{
			return Equals(instance, rhs);
		}

		private object DoIsType(object instance, string name)
		{
			bool matches = false;
			
			RefactorType rtype = FindType(name);
			if (rtype == null)
				throw new InvalidOperationException(string.Format("{0} is not a valid refactor type name.", name));
			
			if (instance == null)
			{
				matches = name == "Void" || name == "Object";
			}
			else
			{				
				Type type = instance.GetType();
				if (type.IsArray)
				{
					if (name == "Sequence" || name == "Object")
						matches = true;
				}
				else if (typeof(RefactorCommand).IsAssignableFrom(type))
				{
					if (name == "Edit" || name == "Object")
						matches = true;
				}
				else
				{
					while (type != null && !matches)
					{
						if (type == rtype.ManagedType)
							matches = true;
												
						type = type.BaseType;
					}
				}
			}
			
			return matches;
		}

		private object DoNotEquals(object instance, object rhs)
		{
			return !((bool) DoEquals(instance, rhs));
		}
		#endregion

		private static ObjectType ms_instance;
	} 
}
