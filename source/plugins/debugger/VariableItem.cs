// Copyright (C) 2010 Jesse Jones
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

using MCocoa;
using MObjc;
using Mono.Debugger;
using Shared;
using System;

namespace Debugger
{
	[ExportClass("VariableItem", "NSObject")]
	internal abstract class VariableItem : NSObject
	{
		protected VariableItem(string type) : base(NSObject.AllocAndInitInstance(type))
		{
		}
		
		// Note that this should be used instead of Count because it will not force
		// all children to be loaded.
		public abstract bool IsExpandable {get;}
		
		public abstract int Count {get;}
		
		public abstract VariableItem this[int index] {get;}
		
		public abstract NSAttributedString GetName();
		
		public abstract NSAttributedString GetValue();
		
		public abstract NSAttributedString GetTypeName();
		
		#region Protected Methods
		protected VariableItem CreateVariable(string name, string type, TypeMirror v, ThreadMirror thread)
		{
			VariableItem variable = new TypeValueItem(name, type, v, thread);
			
			return variable;
		}
		
		protected VariableItem CreateVariable(string name, string type, Value v, ThreadMirror thread)
		{
			VariableItem variable = null;
			
			do
			{
				var array = v as ArrayMirror;
				if (array != null)
				{
					variable = new ArrayValueItem(name, type, array, thread);
					break;
				}
				
				var enm = v as EnumMirror;
				if (enm != null)
				{
					variable = new EnumValueItem(name, type, enm);
					break;
				}
				
				var primitive = v as PrimitiveValue;
				if (primitive != null)
				{
					variable = new PrimitiveValueItem(name, type, primitive);
					break;
				}
				
				var str = v as StringMirror;
				if (str != null)
				{
					variable = new StringValueItem(name, type, str);
					break;
				}
				
				// these two have to appear last
				var obj = v as ObjectMirror;
				if (obj != null)
				{
					variable = new ObjectValueItem(name, type, obj, thread);
					break;
				}
				
				var strct = v as StructMirror;
				if (strct != null)
				{
					variable = new StructValueItem(name, type, strct, thread);
					break;
				}
				
				Console.Error.WriteLine("bad type: {0}", v.GetType());
			}
			while (false);
			
			return variable;
		}
		
		protected NSAttributedString CreateString(string text)
		{
			return NSAttributedString.Create(text).Retain();
		}
		
		protected NSAttributedString CreateString(NSColor color, string text)
		{
			var attrs = NSMutableDictionary.Create();
			attrs.setObject_forKey(color, Externs.NSForegroundColorAttributeName);
			return NSAttributedString.Create(text).Retain();
		}
		#endregion
	}
}
