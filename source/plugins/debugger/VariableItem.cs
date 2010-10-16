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
using MObjc.Helpers;
using Mono.Debugger.Soft;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;

using Debug = Debugger;

namespace Debugger
{
	// These form a tree representing all of the values the user is currently examining
	// in the variables window. Note that we try to preserve the objects that form the
	// tree to prevent the outline view from closing open items. 
	[ExportClass("VariableItem", "NSObject")]
	internal sealed class VariableItem : NSObject, IFormattable
	{
		public VariableItem(ThreadMirror thread, LiveStackFrame frame) : this(thread, frame.Method.DeclaringType.FullName + " Stack Frame", null, null, frame, 0)
		{
		}
		
		// name is a field name, local variable name, etc.
		// parent is a LiveStackFrame, ObjectMirror, ArrayMirror, etc. Parents are always non-null except for the LiveStackFrame parent.
		// key is an integral index, a FieldInfoMirror, LocalVariable, etc.
		// value is the object associated with the parent/key. It will be a Value or a primitive type (like char or int).
		public VariableItem(ThreadMirror thread, string name, VariableItem parent, object key, object value, int index) : base(NSObject.AllocAndInitInstance("VariableItem"))
		{
			Contract.Requires(thread != null);
			Contract.Requires(!string.IsNullOrEmpty(name));
			Contract.Requires(parent != null || value is LiveStackFrame);
			Contract.Requires(key != null || value is LiveStackFrame);
			Contract.Requires(value != null);					// null debugger values are PrimitiveValues with a null Value
			Contract.Requires(index >= 0);
			
			Parent = parent;
			Key = key;
			Value = value;
			m_index = index;
			
			Item item = GetItem.Invoke(thread, parent != null ? parent.Value : null, Key, Value);
			AttributedName = NSMutableAttributedString.Create(name).Retain();
			AttributedType = NSAttributedString.Create(item.Type).Retain();
			AttributedValue = NSAttributedString.Create(item.Text).Retain();
			NumberOfChildren = item.Count;
			
			// If the value is decorated with DebuggerTypeProxyAttribute then we need to
			// use a proxy value instead of the original value.
			object replacement = DoGetProxyValue(value, thread);
			
			// If a property or field of the value is decorated with DebuggerBrowsableAttribute
			// and RootHidden then we need to display just the value of that property or field.
			replacement = DoGetRootValue(replacement ?? value, thread);
			
			// If we found a replacement for the original value then,
			if (replacement != null)
			{
				// we need to use that value,
				Value = replacement;
				
				// the children will be those of the replacement,
				item = GetItem.Invoke(thread, parent != null ? parent.Value : null, Key, Value);
				NumberOfChildren = item.Count;
				
				// and if the original value didn't provide a custom ToString see if the replacement does.
				if (!string.IsNullOrEmpty(item.Text))
					AttributedValue = NSAttributedString.Create(item.Text).Retain();
			}
		}
		
		public NSMutableAttributedString AttributedName {get; private set;}
		public NSAttributedString AttributedType {get; private set;}
		public NSAttributedString AttributedValue {get; private set;}
		
		public VariableItem Parent {get; private set;}
		public object Key {get; private set;}
		public object Value {get; private set;}
		
		public int NumberOfChildren {get; private set;}
		
		public VariableItem GetChild(ThreadMirror thread, int index)
		{
			Contract.Requires(index >= 0);
			Contract.Requires(index < NumberOfChildren);
			
			if (m_children == null)
			{
				m_children = new VariableItem[NumberOfChildren];
				
				if (DoShouldEagerlyLoad())
					DoEagerLoad(thread);
			}
			
			if (m_children[index] == null)
				m_children[index] = Debug::GetChild.Invoke(thread, this, Value, index);
			
			return m_children[index];
		}
		
		public void RefreshValue(ThreadMirror thread, object value)
		{
			Contract.Requires(value != null);
			
			Value = value;
			Refresh(thread);
		}
		
		// Update the value and recursively all the children to reflect state changes in the debugee.
		public void Refresh(ThreadMirror thread)
		{
			Item item = GetItem.Invoke(thread, Parent != null ? Parent.Value : null, Key, Value);
			DoRefreshValueText(item.Text);
			DoRefreshTypeText(item.Type);
			
			if (item.Count == NumberOfChildren)
			{
				if (m_children != null)
				{
					for (int i = 0; i < item.Count; ++i)
					{
						VariableItem child = m_children[i];
						
						if (child != null)
						{
							VariableItem newChild = Debug::GetChild.Invoke(thread, this, Value, child.m_index);
							newChild.autorelease();
							Contract.Assert(child.AttributedName.ToString() == newChild.AttributedName.ToString(), string.Format("oldName: {0}, newName: {1}", child.AttributedName.ToString(), newChild.AttributedName.ToString()));
							
							child.RefreshValue(thread, newChild.Value);
						}
					}
				}
			}
			else
			{
				DoDeleteChildren();
				NumberOfChildren = item.Count;
			}
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		public override string ToString()
		{
			return ToString("G", null);
		}
		
		public string ToString(string format)
		{
			return ToString(format, null);
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		public override string ToString(string format, IFormatProvider provider)
		{
			if (provider != null)
			{
				ICustomFormatter formatter = provider.GetFormat(GetType()) as ICustomFormatter;
				if (formatter != null)
					return formatter.Format(format, this, provider);
			}
			
			var builder = new System.Text.StringBuilder();
			DoToString(builder, format, 0);
			
			return builder.ToString();
		}
		
		#region Protected Methods
		[ThreadModel(ThreadModel.Concurrent)]
		protected override void OnDealloc()
		{
			if (AttributedName != null)		// this may be called if a ctor throws so we need all the null checks
				AttributedName.release();
			
			if (AttributedType != null)
				AttributedType.release();
			
			if (AttributedValue != null)
				AttributedValue.release();
			
			DoDeleteChildren();
			
			base.OnDealloc();
		}
		#endregion
		
		#region Private Methods
		private void DoToString(System.Text.StringBuilder builder, string format, int depth)
		{
			switch (format)
			{
				case "":
				case null:
				case "g":
				case "G":
					builder.AppendFormat("{0} {1} {2}", AttributedType.ToString(), AttributedName.ToString(), AttributedValue.ToString());
					break;
					
				case "f":
				case "F":
					builder.Append(' ', 3*depth);
					builder.AppendFormat("{0} {1} {2} (key = {3})", AttributedType.ToString(), AttributedName.ToString(), AttributedValue.ToString(), Key);
					if (Parent != null)
					{
						builder.AppendLine();
						Parent.DoToString(builder, format, depth + 1);
					}
					break;
				
				default:
					builder.Append(base.ToString(format, null));
					break;
			}
		}
		
		// There are a couple of reasons why we want to eagerly create children: for most
		// types we want to sort the children by name and we need them all to do that.
		// In addition, StackFrames don't remain useable for very long so we need to get
		// all of their children while we can.
		//
		// Conversely collection classes may be very large so we only want to load the 
		// children that we must (NSOutlineView will be smart and only ask for the children
		// that need to be rendered).
		private bool DoShouldEagerlyLoad()
		{
			bool lazy = Value is ArrayMirror || Value is StringMirror;
			
			return !lazy;
		}
		
		private void DoEagerLoad(ThreadMirror thread)
		{
			for (int i = 0; i < NumberOfChildren; ++i)
			{
				Contract.Assert(m_children[i] == null);
				m_children[i] = Debug::GetChild.Invoke(thread, this, Value, i);
			}
			
			Array.Sort(m_children, (lhs, rhs) =>
			{
				// Sort so that properties appear before fields (for people with sane
				// naming conventions).
				string l = lhs.AttributedName.ToString();
				string r = rhs.AttributedName.ToString();
				if (char.IsUpper(l[0]) && !char.IsUpper(r[0]))
					return -1;
				else if (!char.IsUpper(l[0]) && char.IsUpper(r[0]))
					return +1;
				return l.CompareTo(r);
			});
		}
		
		private void DoDeleteChildren()
		{
			if (m_children != null)
			{
				foreach (VariableItem child in m_children)
				{
					if (child != null)
						child.release();
				}
				m_children = null;
			}
		}
		
		private void DoRefreshValueText(string newValue)
		{
			NSAttributedString str;
			NSColor nameColor = NSColor.blackColor();
			
			ObjectMirror obj = Value as ObjectMirror;
			if (obj != null && obj.IsCollected)
			{
				str = DoCreateString("garbage collected", NSColor.disabledControlTextColor());
				nameColor = NSColor.disabledControlTextColor();
			}
			else
			{
				string oldValue = AttributedValue.ToString();
				if (newValue != oldValue)
				{
					if (newValue.Length == 0)
					{
						str = DoCreateString(string.Empty, NSColor.redColor());
						nameColor = NSColor.redColor();						// we've gone from having value text to not having it (probably because the value went from null to non-null), so make it more obvious that it has changed
					}
					else
					{
						str = DoCreateString(newValue, NSColor.redColor());
					}
				}
				else
				{
					str = NSAttributedString.Create(newValue).Retain();
				}
			}
			
			AttributedValue.release();										// note that we want to reset the text even if it has not changed so that we go from red back to black
			AttributedValue = str;
			
			NSRange range = new NSRange(0, (int) AttributedName.length());
			AttributedName.addAttribute_value_range(Externs.NSForegroundColorAttributeName, nameColor, range);
		}
		
		// The declared type should not change but we show the actual type which may.
		private void DoRefreshTypeText(string newType)
		{
			NSAttributedString str;
			
			string oldType = AttributedType.ToString();
			if (newType != oldType)
				if (newType.Length > 0)
					str = DoCreateString(newType, NSColor.redColor());
				else
					str = NSAttributedString.Create(oldType).Retain();	// this will happen if the value goes to null
			else
				str = NSAttributedString.Create(newType).Retain();
			
			AttributedType.release();
			AttributedType = str;
		}
		
		private NSAttributedString DoCreateString(string newValue, NSColor color)
		{
			var attrs = NSMutableDictionary.Create();
			attrs.setObject_forKey(color, Externs.NSForegroundColorAttributeName);
			NSAttributedString result = NSAttributedString.Create(newValue, attrs).Retain();
			
			return result;
		}
		
		// Types may declare that the value of a single property or field is to be displayed
		// in place of the memebers of the type. This is used, for example, by the proxy
		// collection classes to show just the Items property.
		private object DoGetRootValue(object original, ThreadMirror thread)
		{
			object result = null;
			
			if (original is ObjectMirror)
			{
				ObjectMirror oo = (ObjectMirror) original;
				result = DoGetRootValue(oo, oo.Type.GetAllProperties(), thread);
				result = result ?? DoGetRootValue(oo, oo.Type.GetAllFields(), thread);
			}
			else if (original is StructMirror)
			{
				StructMirror ss = (StructMirror) original;
				result = DoGetRootValue(ss, ss.Type.GetAllProperties(), thread);
				result = result ?? DoGetRootValue(ss, ss.Type.GetAllFields(), thread);
			}
			
			return result;
		}
		
		private object DoGetRootValue(Value target, IEnumerable<PropertyInfoMirror> props, ThreadMirror thread)
		{
			foreach (PropertyInfoMirror prop in props)
			{
				var attr = prop.GetAttribute<DebuggerBrowsableAttribute>();
				if (attr != null && attr.State == DebuggerBrowsableState.RootHidden)
				{
					return EvalMember.Evaluate(thread, target, prop.Name);
				}
			}
			
			return null;
		}
		
		private object DoGetRootValue(Value target, IEnumerable<FieldInfoMirror> fields, ThreadMirror thread)
		{
			foreach (FieldInfoMirror field in fields)
			{
				var attr = field.GetAttribute<DebuggerBrowsableAttribute>();
				if (attr != null && attr.State == DebuggerBrowsableState.RootHidden)
				{
					return EvalMember.Evaluate(thread, target, field.Name);
				}
			}
			
			return null;
		}
		
		// Types may declare a proxy type which debuggers are supposed to use when showing
		// instances of the type. For example, collections typically declare a proxy which exposes
		// the elements of the collection as an array.
		private object DoGetProxyValue(object original, ThreadMirror thread)
		{
			object result = null;
			
			System.Diagnostics.DebuggerTypeProxyAttribute attr = null;
			string originalTypeName = null;
			if (original is ObjectMirror)
			{
				ObjectMirror oo = (ObjectMirror) original;
				attr = oo.Type.GetAttribute<System.Diagnostics.DebuggerTypeProxyAttribute>();
				originalTypeName = oo.Type.FullName;
			}
			else if (original is StructMirror)
			{
				StructMirror ss = (StructMirror) original;
				attr = ss.Type.GetAttribute<System.Diagnostics.DebuggerTypeProxyAttribute>();
				originalTypeName = ss.Type.FullName;
			}
			
			if (attr != null)
			{
				if (!string.IsNullOrEmpty(attr.ProxyTypeName))
				{
					TypeMirror type = DoGetType(thread, attr.ProxyTypeName, originalTypeName);
					if (type != null)
					{
						MethodMirror method = type.FindMethod(".ctor", 1);
						try
						{
							if (method != null)
							{
								result = type.InvokeMethod(thread, method, new Value[]{(Value) original}, InvokeOptions.DisableBreakpoints | InvokeOptions.SingleThreaded);
							}
							else
							{
								Log.WriteLine(TraceLevel.Error, "Debugger", "DoGetValue> couldn't find a one argument ctor in {0}", type.FullName);
							}
						}
						catch (Exception e)
						{
							Log.WriteLine(TraceLevel.Error, "Debugger", "DoGetValue> {0}", e.Message);
						}
					}
				}
			}
			
			return result;
		}
		
		private TypeMirror DoGetType(ThreadMirror thread, string proxyTypeName, string originalTypeName)
		{
			TypeMirror type = thread.GetType(proxyTypeName);
			
			if (type != null && type.FullName.Length > 2 && type.FullName[type.FullName.Length - 2] == '`')
			{
				// The proxy type is an unbound generic so we need to bind the type arguments using
				// the original type. Proxy will be a name like "System.Collections.Generic.CollectionDebuggerView`1"
				// original will be a name like "System.Collections.Generic.List`1[[System.String, mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]]".
				int i = originalTypeName.IndexOf('`');
				if (i > 0 && i + 1 < originalTypeName.Length && originalTypeName[i + 1] == type.FullName[type.FullName.Length - 1])
				{
					proxyTypeName = type.FullName + originalTypeName.Substring(i + 2);
					Log.WriteLine(TraceLevel.Verbose, "Debugger", "using {0} for {1}", proxyTypeName, originalTypeName);
					type = thread.GetType(proxyTypeName);
				}
				else
				{
					// Either the original type is not generic or the number of generic arguments does not match.
					Log.WriteLine(TraceLevel.Error, "Debugger", "DoGetType> {0} and {1} are not compatible", originalTypeName, proxyTypeName);
				}
			}
			else if (type != null)
			{
				Log.WriteLine(TraceLevel.Verbose, "Debugger", "used {0} for {1}", proxyTypeName, originalTypeName);
			}
			
			return type;
		}
		#endregion
		
		#region Fields
		private VariableItem[] m_children;
		private int m_index;						// cache the index (because we sort by name so the old indices become invalid when reloading)
		#endregion
	}
}
