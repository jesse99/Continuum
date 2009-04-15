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

namespace CsRefactor.Script
{
	internal abstract class RefactorType
	{
		protected RefactorType()
		{
			RegisterAllMethods();
			
			ms_types.Add(Name, this);
			
			if (Name != "Void")
				ms_names.Add(ManagedType, Name);
		}
		
		// The name of the type within the refactor language.
		public abstract string Name {get;}
		
		// The base class of the refactor type. Note that the base of Object is null.
		public abstract RefactorType Base {get;}
		
		// The type used when evaluating the refactor script.
		public abstract Type ManagedType {get;}
		
		public object Execute(int line, object instance, string method, object[] args)
		{
			Callback callback;
			if (!m_callbacks.TryGetValue(method, out callback))
				throw new EvaluateException(line, "{0} does not respond to the {1} method.", Name, method);
				
			try
			{
				return callback(line, instance, method, args);
			}
			catch (ScriptAbortException)
			{
				throw;
			}
			catch (ScriptException)
			{
				throw;
			}
//			catch (Exception e)
//			{
//				throw new EvaluateException(line, string.Format("{0}.{1} {2}.", Name, method, e.Message));	// the inner exception doesn't have a useful stack trace...
//			}
		}
		
		// Returns the refactor type with the given name or null.
		public static RefactorType FindType(string name)
		{
			RefactorType type;
			Unused.Value = ms_types.TryGetValue(name, out type);
			
			return type;
		}
		
		// Returns the name of the refactor type that matches the specified managed type.
		public static string GetName(Type type)
		{
			string name;
			
			if (type == null)
				name = "Void";
			else if (type.IsArray)
				name = "Sequence";
			else if (typeof(RefactorCommand).IsAssignableFrom(type))
				name = "Edit";
			else if (!ms_names.TryGetValue(type, out name))
				name = "<" + type.Name + ">";		// GetName is used for error reporting so we don't want to do anything too crazy if the type is not valid
				
			return name;
		}
		
		#region Protected Methods
		protected abstract void RegisterMethods(RefactorType type);
		
		protected void RegisterAllMethods()
		{
			m_callbacks.Clear();
			
			RefactorType type = this;
			while (type != null)
			{
				type.RegisterMethods(this);
				type = type.Base;
			}
		}
		
		public delegate object Nullary<T>(T instance);		
		public void Register<T>(string name, Nullary<T> callback)
		{
			Contract.Requires(callback != null, "callback is null");
			
			m_callbacks.Add(name, (int line, object instance, string method, object[] args) => 
			{
#if DEBUG
				Contract.Requires(args != null, "args is null");
#endif
				
				T target = (T) instance;		// this should always work
				
				if (args.Length != 0)
					throw new EvaluateException(line, "{0}.{1} takes zero arguments.", Name, method);
				
				return callback(target);
			});
		}
		
		public delegate object Unary<T, A0>(T instance, A0 a0);
		public void Register<T, A0>(string name, Unary<T, A0> callback)
		{
			Contract.Requires(callback != null, "callback is null");

			m_callbacks.Add(name, (int line, object instance, string method, object[] args) => 
			{
#if DEBUG
				Contract.Requires(args != null, "args is null");
#endif
				
				T target = (T) instance;		// this should always work
				
				if (args.Length != 1)
					throw new EvaluateException(line, "{0}.{1} takes one argument.", Name, method);
				
				if (args[0] != null && !(args[0] is A0))				// can't use as because we need to work with value types
					throw new EvaluateException(line, "Expected a {0} for the first argument to {1}.{2}, not {3}.", GetName(typeof(A0)), Name, method, GetName(args[0].GetType()));
				
				return callback(target, (A0) args[0]);
			});
		}
		
		public delegate object Binary<T, A0, A1>(T instance, A0 a0, A1 a1);
		public void Register<T, A0, A1>(string name, Binary<T, A0, A1> callback)
		{
			Contract.Requires(callback != null, "callback is null");
			
			m_callbacks.Add(name, (int line, object instance, string method, object[] args) => 
			{
#if DEBUG
				Contract.Requires(args != null, "args is null");
#endif
				
				T target = (T) instance;		// this should always work
				
				if (args.Length != 2)
					throw new EvaluateException(line, "{0}.{1} takes two arguments.", Name, method);
				
				if (args[0] != null && !(args[0] is A0))				// can't use as because we need to work with value types
					throw new EvaluateException(line, "Expected a {0} for the first argument to {1}.{2}, not {3}.", GetName(typeof(A0)), Name, method, GetName(args[0].GetType()));
				
				if (args[1] != null && !(args[1] is A1))
					throw new EvaluateException(line, "Expected a {0} for the second argument to {1}.{2}, not {3}.", GetName(typeof(A1)), Name, method, GetName(args[1].GetType()));
				
				return callback(target, (A0) args[0], (A1) args[1]);
			});
		}
		
		public void Register(Context context, Method customMethod)
		{
			Contract.Requires(customMethod != null, "customMethod is null");
			
			if (m_callbacks.ContainsKey(customMethod.Name))
				throw new EvaluateException(1, "The {0} method is already defined.", customMethod.Name);
			
			m_callbacks.Add(customMethod.Name, (int line, object instance, string method, object[] args) => 
			{
#if DEBUG
				Contract.Requires(args != null, "args is null");
#endif
				
				return customMethod.Evaluate(line, context, args);
			});
		}
		#endregion
		
		#region Private Members
		private delegate object Callback(int line, object instance, string method, object[] args);
		
		#endregion
		
		#region Fields
		private Dictionary<string, Callback> m_callbacks = new Dictionary<string, Callback>();
		
		private static Dictionary<string, RefactorType> ms_types = new Dictionary<string, RefactorType>();
		private static Dictionary<Type, string> ms_names = new Dictionary<Type, string>();
		#endregion
	}
}
