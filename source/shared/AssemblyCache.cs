// Copyright (C) 2008 Jesse Jones
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

using Gear.Helpers;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Shared
{
	[ThreadModel(ThreadModel.Concurrent)]
	public static class AssemblyCache
	{
		public static AssemblyDefinition Load(string path, bool loadSymbols)
		{
			Contract.Requires(!string.IsNullOrEmpty(path), "path is null or empty");
			
			AssemblyDefinition assembly = null;
			
			AcquireLock();
			try
			{
				Entry entry;
				if (ms_entries.TryGetValue(path, out entry))
					assembly = entry.Assembly;
					
				DateTime time = File.GetLastWriteTime(path);
				if (assembly == null || time > entry.Time)
				{
					assembly = AssemblyFactory.GetAssembly(path);
					entry = new Entry(assembly, time);
					ms_entries[path] = entry;
				}
				
				if (entry != null && loadSymbols)
					entry.LoadSymbols(assembly);
			}
			finally
			{
				ReleaseLock();
			}
			
			return assembly;
		}
		
		// This is used to serialize building assemblies and parsing assemblies.
		// Without this we sometimes wind up with failed builds because Cecil
		// is reading in an assembly while gmcs is trying to write out a new
		// version.
		public static void AcquireLock()
		{
			System.Threading.Monitor.Enter(ms_lock);
		}
		
		public static void ReleaseLock()
		{
			System.Threading.Monitor.Exit(ms_lock);
		}
		
		#region Private Types
		private sealed class Entry
		{
			public Entry(AssemblyDefinition assembly, DateTime time)
			{
				m_reference = new WeakReference(assembly);
				m_time = time;
			}
			
			public AssemblyDefinition Assembly
			{
				get {return (AssemblyDefinition) m_reference.Target;}
			}
			
			public DateTime Time
			{
				get {return m_time;}
			}
			
			public void LoadSymbols(AssemblyDefinition assembly)
			{
				if (!m_loadedSymbols)
				{
					// For some reason we have to force the Mdb assembly to load. 
					// If we don't it isn't found.
					Unused.Value = typeof(Mono.Cecil.Mdb.MdbFactory);
					try
					{
						foreach (ModuleDefinition module in assembly.Modules)
						{
							module.LoadSymbols();		// required if we want to access source and line info
						}
					}
					catch (FileNotFoundException)
					{
						// mdb file is missing
					}
					
					m_loadedSymbols = true;
				}
			}
			
			private readonly WeakReference m_reference;
			private readonly DateTime m_time;
			private bool m_loadedSymbols;
		}
		#endregion
		
		#region Fields
		private static object ms_lock = new object();
		private static Dictionary<string, Entry> ms_entries = new Dictionary<string, Entry>();
		#endregion
	}
}
