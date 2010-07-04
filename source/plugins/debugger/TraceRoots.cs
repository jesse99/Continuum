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

using MObjc.Helpers;
using Mono.Debugger.Soft;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Debugger
{
	internal sealed class Trace
	{
		public Trace(string name, string type, object obj)
		{
			Name = name;
			Type = type;
			Object = obj;
			
			int index = Type.IndexOf("[[");	// strip off stuff like "[[System.Int32, mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]]"
			if (index > 0)
				Type = Type.Substring(0, index);
		}
		
		public void Write(System.IO.StreamWriter stream, int indent)
		{
			for (int i = 0; i < indent; ++i)
			{
				stream.Write('\t');
			}
			stream.WriteLine("{0}\t\t\t\t{1}", Name, Type);
			
			foreach (Trace child in Children)
			{
				child.Write(stream, indent + 1);
			}
		}
		
		public readonly string Name;
		public readonly string Type;
		public readonly object Object;
		public readonly List<Trace> Children = new List<Trace>();
	}
	
	// Prints references from garbage collector roots to a specific object/type or to
	// all objects.
	internal sealed class TraceRoots
	{
		public TraceRoots(IList<ThreadMirror> threads, FieldInfoMirror[] staticFields)
		{
			Contract.Requires(threads != null);
			
			m_threads = threads;
			m_staticFields = staticFields;
		}
		
		// Returns all traces from roots where the leaf satisfies the filter (which
		// may be null).
		public IEnumerable<Trace> Walk(Func<string, object, bool> filter)
		{
			var roots = new List<Trace>();
			
			DoWalkThreadRoots(roots, filter);
			DoWalkStaticRoots(roots, filter);
			
			return roots;
		}
		
		#region Private Methods
		private void DoWalkThreadRoots(List<Trace> roots, Func<string, object, bool> filter)
		{
			foreach (ThreadMirror thread in m_threads)
			{
				string name = ThreadsController.GetThreadName(thread);
//	Console.WriteLine("thread: {0}", name); Console.Out.Flush();
				var root = new Trace("thread " + name, "System.Threading.Thread", thread);
				DoWalkStackFrames(thread, root);
				if (DoFilter(root, filter, 0))
					roots.Add(root);
			}
		}
		
		private void DoWalkStaticRoots(List<Trace> roots, Func<string, object, bool> filter)
		{
			foreach (FieldInfoMirror field in m_staticFields)
			{
				// TODO: need to somehow get thread statics working (presumably the
				// runtime uses TLS to store these but there doesn't appear to be a way
				// to iterate over the thread local items and the soft debugger throws
				// if you try to get their values using the code below).
				if (!field.GetCustomAttributes(false).Any(c => c.Constructor.FullName.Contains("System.ThreadStaticAttribute:.ctor")))
				{
					string name = "static " + field.Name;
					var root = new Trace(name, field.FieldType.FullName, field);
					Value v = field.DeclaringType.GetValue(field);
					DoWalkValue(v, root);
					if (DoFilter(root, filter, 0))
						roots.Add(root);
				}
			}
		}
		
		// Returns true if parent or a child satisfies the filter.
		private bool DoFilter(Trace parent, Func<string, object, bool> filter, int indent)
		{
			bool satisfies = false;
			
			if (filter != null)
			{
				parent.Children.RemoveAll(child => !DoFilter(child, filter, indent + 1));
				satisfies = parent.Children.Count > 0 || filter(parent.Type, parent.Object);
			}
			else
			{
				satisfies = true;
			}
			
			return satisfies;
		}
		
		private void DoWalkStackFrames(ThreadMirror thread, Trace parent)
		{
			StackFrame[] frames = thread.GetFrames();
			for (int i = frames.Length - 1; i >= 0; --i)		// go backwards so that the frames at the top of the stack appear first
			{
				StackFrame frame = frames[i];
			
				string name;
				if (string.IsNullOrEmpty(frame.FileName))
					name = string.Format("method {0}", frame.Method.Name);
				else
					name = string.Format("method {0} at {1}:{2}", frame.Method.Name, frame.FileName, frame.LineNumber);
					
				var child = new Trace(name, string.Empty, frame);
				DoWalkStackFrame(frame, child);
				if (child.Children.Count > 0)
					parent.Children.Add(child);
			}
		}
		
		private void DoWalkStackFrame(StackFrame frame, Trace parent)
		{
			LocalVariable[] locals = frame.Method.GetLocals();
			for (int i = 0; i < locals.Length; ++i)
			{
				Value value = frame.GetValue(locals[i]);
//	Console.WriteLine("local: {0}", locals[i].Name); Console.Out.Flush();
				
				var child = new Trace("local " + locals[i].Name, locals[i].Type.FullName, value);
				DoWalkValue(value, child);
				parent.Children.Add(child);
			}
		}
		
		private void DoWalkValue(Value value, Trace parent)
		{
			if (value is ArrayMirror)
			{
				DoWalkArray((ArrayMirror) value, parent);
			}
			else if (value is ObjectMirror)
			{
				DoWalkObject((ObjectMirror) value, parent);
			}
			else if (value is StructMirror)
			{
				DoWalkStruct((StructMirror) value, parent);
			}
		}
		
		private void DoWalkArray(ArrayMirror value, Trace parent)
		{
			for (int i = 0; i < value.Length; ++i)
			{
//	Console.WriteLine("array index: {0}", i); Console.Out.Flush();
				var child = new Trace("item " + i, value.Type.FullName, value[i]);
				DoWalkValue(value[i], child);
				if (child.Children.Count > 0)
					parent.Children.Add(child);
			}
		}
		
		private void DoWalkObject(ObjectMirror value, Trace parent)
		{
			if (!m_objects.Contains(value.Address))
			{
				m_objects.Add(value.Address);
				foreach (FieldInfoMirror field in value.Type.GetAllFields())
				{
					if (!field.IsStatic)
					{
//	Console.WriteLine("object field: {0} {1}", field.Name, field.FieldType.FullName); Console.Out.Flush();
						Value v = value.GetValue(field);
						
						var child = new Trace(field.Name, field.FieldType.FullName, v);
						DoWalkValue(v, child);
						parent.Children.Add(child);
					}
				}
			}
		}
		
		private void DoWalkStruct(StructMirror value, Trace parent)
		{
			foreach (FieldInfoMirror field in value.Type.GetFields())
			{
				if (!field.IsStatic)
				{
					Value v = value[field.Name];
//	Console.WriteLine("struct field: {0} {1}", field.Name, field.FieldType.FullName); Console.Out.Flush();
					
					var child = new Trace(field.Name, field.FieldType.FullName, v);
					DoWalkValue(v, child);
					parent.Children.Add(child);
				}
			}
		}
		#endregion
		
		#region Fields
		private IList<ThreadMirror> m_threads;
		private FieldInfoMirror[] m_staticFields;
		private HashSet<long> m_objects = new HashSet<long>();
		#endregion
	}
}
